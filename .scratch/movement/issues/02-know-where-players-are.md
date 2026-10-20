# 02: Know where players are

**What to build:** The Mind knows where the other players near its Agent are, and keeps them in **Surroundings**. The Human runs around next to Greeen and sees the Mind print Percepts with the Human's position, in global coordinates, as it changes.

On the server:
- Player create and object delete get hand-written Percept projections, each with exactly the fields its 1.127 packet carries and naming the packet-library override it mirrors. Positions are global.
- Other players' movement reaches a client as a ready-built 1.127 player position packet passed to the packet library's raw send call, not as a typed call (see ADR 0001's consequences). That one layout is decoded into a typed Percept, mirroring the server code that builds it. Every other raw packet becomes a bare Percept naming its packet code.
- The generated TypeScript is regenerated.

In the Mind:
- A Surroundings module, fed Percepts. It keeps known players by object ID with name, position, heading, speed and when each was last heard from: created on player create, moved on player position, removed on object delete. It answers "who is called X" and "how far is this point from that one", working distances out when asked. It never forgets by time. Forgetting by distance from Self comes in ticket 05, NPCs in ticket 06.
- The entry point feeds every Percept to Surroundings. Surroundings knows nothing about echo or Come.

See the spec: `.scratch/movement/spec.md`.

**Blocked by:** None (can start immediately)

**Status:** ready-for-agent

- [x] Player create gives a typed Percept with exactly what the 1.127 packet carries, positions global
- [x] Object delete gives a typed Percept with the object ID
- [x] The 1.127 player position packet given to the raw send call gives a typed Percept; a test feeds the server's own 1.127 builder output through the decoder and gets back what went in
- [x] Any other raw packet gives a bare Percept naming its packet code
- [x] The generated TypeScript is up to date and the codegen test passes
- [x] Surroundings (`bun test`): create, move, delete; lookup by name; distance worked out on request; nothing forgotten by time
- [x] In game: running around next to Greeen, the Mind prints the Human's positions as they change, and they match `/loc`

## Comments

**2026-10-20 (agent):** Built on branch `claude/m3-02-players-surroundings-5467ca`. The last box needs the Human with a running server and the real client, so this is back with the Human.

What exists now:
- **Player create Percept** (`playerCreate`). Mirrors `PacketLib1124.SendPlayerCreate`, which 1.125 to 1.127 don't override. It carries what the packet writes: global `x`, `y`, `z`, `sessionId`, `objectId`, `heading`, `model`, the display `level`, the `realm` as the Agent's Character sees it (the packet's two bits, as `None`, `Albion`, `Midgard` or `Hibernia`), the `dead`, `swimming`, `stealthed`, `wireframe` and `vampiir` flags, the seven face bytes, and the `name`, `guildName`, `lastName`, `prefixName` and `title` the server rules give the viewer. `horse` is there only while the player rides one. As in the library, nothing is sent for a player outside any zone, or one the Agent's Character can't see, or before the Agent has a Character.
- **Object delete Percept** (`objectDelete`). Mirrors `PacketLib168.SendObjectDelete`, both overloads: the `objectId`.
- **Raw packets.** A packet the server built itself and handed to `SendUDP` or `SendTCP` gives a `barePacket` Percept with the call and the packet `code`, for example `SendUDP 0xBA` for another player's heading update. The one exception is the 1.127 player position: an `SendUDP` packet with code `0xA9` and exactly the 40 bytes `PlayerPositionUpdateHandler.CreateOutPak1127` writes is decoded into a `playerPosition` Percept: global `x`, `y`, `z`, `speed`, `fallSpeed`, `sessionId`, `objectId`, `zoneId`, `stateFlags`, `steedSeat`, `heading` (0 to 4095) and `headingFlags` (the 4 bits above it the player's client sent), `actionFlags`, `roleplay`, `healthPercent`, `attacking`, `manaPercent` and `endurancePercent`. The packet is shared with every other client it goes to, so it's only read. A `0xA9` of another layout (older clients get those) stays a bare packet Percept.
- **Protocol.** `mind/src/protocol.ts` is regenerated. The generator now writes `float` as `number` and turns a record that isn't polymorphic (`PlayerCreateHorse`) into a plain interface. The realm enum is `PacketRealm`, because `Realm` collides with the server's `DOL.GS.Realm` namespace.
- **Surroundings** (`mind/src/surroundings.ts`). Fed every `PerceptMessage`, it keeps the players it knows of by object ID: `name`, `position`, `heading`, `speed` and `lastHeardMs` (the `elapsedMs` of the last Percept about it). Player create makes a player known (speed 0, since the packet has none), player position moves it, object delete forgets it, and a second create replaces it. A position for a player it doesn't know is ignored, because without its create the Agent doesn't know who it is. `named(name)` finds a player in any case, `player(objectId)` by ID, `players` lists them, and `distance(from, to)` works a 3D distance out when asked. Nothing is forgotten by time. It knows nothing about echo.
- **Entry point.** `mind/agent.ts` feeds every Percept to Surroundings before printing it, and the echo behaviour is unchanged. The new Percepts print as, for example, `playerCreate #291 Vue	X:531234 Y:478901 Z:2345 heading 2048`, `playerPosition #291 Vue	X:531400 Y:478950 Z:2350 heading 1024 speed 191 zone 101` (the name from Surroundings), `SendUDP 0xBA` and `objectDelete #291`. Positions print as `/gloc` prints them.
- Tests:
  - Server, 175 passing in Release. `UT_PerceptProjection`: player create gives every field the packet carries; its state flags; the realm's two bits; nothing for a player the Agent can't see, one outside any zone, or before the Agent has a Character; both object delete calls; the position handler's own `CreateOutPak1127` output, fed through the decoder, gives back what went in (a mutation of the heading offset fails it); its `CreateOutPak1124` output and other raw UDP and TCP packets give bare packet Percepts with their codes; the raw packet is left as it was. `UT_MindProtocol`: a player create writes its realm as a name and leaves out a missing horse. `UT_MindProtocolTypeScript` passes on the regenerated file.
  - The projection tests can't make a real `GamePlayer`: `GameObject`'s type initializer reads the database, and the constructor needs an account vault, an inventory and a world. `Tests/UnitTests/WorldDoubles.cs` installs a server with an empty database and test server rules, makes `TestPlayer`s (a `GamePlayer` subclass) without running constructors, and gives them a region holding one real `Zone`. Tickets 03, 05 and 06 can reuse it for zones and objects.
  - Mind, 27 passing in `bun test` (10 new for Surroundings: create, move, a position for an unknown player, delete, a second create, lookup by name, nothing forgotten after ten hours, other Percepts change nothing, distance, distance following a moving player). `bun run typecheck` is clean.
  - Against a fake server, `bun mind/agent.ts greeen` printed the four lines above.

For the Human, when the server runs this build (the Mind setup is unchanged). The client's `/loc` prints zone coordinates; `/gloc` prints the global ones the Mind prints.
1. `bun mind/agent.ts greeen`. Log in with your own Character somewhere Greeen can see you, or walk up to him. The Mind prints `playerCreate #<id> <your name>` with your position.
2. Stand still and type `/gloc`. The `playerCreate` line's X, Y and Z match it, and its heading matches `/gloc`'s Heading.
3. Run around next to Greeen. The Mind prints `playerPosition #<same id> <your name>` lines while you move, at most about five a second, and none while you stand still. Stop, `/gloc` again: the last `playerPosition` line's X, Y, Z and heading match it, and its zone matches `/gloc`'s Zone.
4. Turn on the spot without moving. The Mind prints `SendUDP 0xBA`, the heading update, which isn't decoded.
5. `/quit` to the character screen. The Mind prints `objectDelete #<same id>`. Log back in next to Greeen: a new `playerCreate` with your name.

Known gaps:
- **Heading updates aren't decoded.** Turning on the spot reaches the Mind as a bare `SendUDP 0xBA`, so Surroundings keeps the old heading until the player next moves. Decode it when a Mind needs it (ADR 0001).
- **The steed and guild packets around a player create are missing.** `PacketLib1124.SendPlayerCreate` also sends the steed's NPC create, the player's guild ID and its guild banner. They're written inside the real library, which the Harness doesn't run, so the Agent never gets them. Mounts are out of scope.
- **The horse is untested.** `horse` reads the player's inventory, which `TestPlayer` doesn't have. While a player rides, the position packet's heading carries its steed's object ID instead.
- **A `0xA9` is decoded by its length.** It's decoded only when it's exactly the 1.127 layout's length, which the Harness client always gets (it's a 1.127 client).
- **`/loc` doesn't match.** It's the client's own command and prints zone coordinates. Use `/gloc`.

**2026-10-20 (Human, recorded by agent):** Ran the checks above on a server built from this branch at `f30b444e8` and reports that `/gloc` matches. The Mind's output (`percepts.log`, not committed) shows:
- Vue (object 17737) created at 146.1 s, then 57 `playerPosition` lines up to 217.7 s while running, with `SendUDP 0xBA` heading updates in between when turning on the spot. The last line, `X:296715 Y:642621 Z:4848 heading 1158 speed 0 zone 202`, is where Vue stopped.
- Before that, from 54.0 s to 145.7 s, Vue was GM-stealthed: one `playerCreate`, then an `objectDelete` every 2 s or so instead of positions. That's the server hiding a stealthed GM from a player, as `PlayerPositionUpdateHandler.BroadcastPosition` does. `/gmstealth off` ended it. After the relog at 244.5 s Vue was stealthed again.
- Positions also arrive about every 2 s while standing still, with speed 0: the client's idle updates, which the server broadcasts too. The hand-off above was wrong to say none come.
- **Found a gap.** `/quit` gave no `objectDelete`, only a bare `SendObjectRemove`, so Surroundings kept Vue after he left. A player who quits leaves through `GameObject.RemoveFromWorld`, which calls `SendObjectRemove` (the `RemoveObject` packet, 0xA2), not `SendObjectDelete`. Fixed in `2f964e323`: it's now an `objectRemove` Percept with the `objectId` and `objectType` (2 for a player, 1 for a living NPC, 0 for anything else, a dead NPC included), mirroring `PacketLib168.SendObjectRemove`, and Surroundings forgets on it as on a delete. Server 177 and Mind 28 tests pass.

The in-game box holds. One re-check for the fix: with this build, `/quit` next to Greeen should print `objectRemove #<your id> type 2`.
