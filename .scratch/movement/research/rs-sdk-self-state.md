# rs-sdk: how the bot's own state is kept apart from what is around it

**Question.** We are about to keep two beliefs in the Mind: **Surroundings** (who and what is near, and where) and a separate term, not yet named, for the Agent's own state (own position, heading, max speed, moving / arrived / stopped, later HP). How does rs-sdk split "me" from "what's near me", and what can we learn before we name ours?

**Sources.** Primary sources only, read on 2026-10-20 from rs-sdk at commit `b565f2d920bf6f5ec427bc6f66dc6fd64a2e15c1`, the same commit as [the earlier rs-sdk research](../../mind-connection/research/rs-sdk.md). Every `rs:` link points at that commit. Line numbers are given as `L12-34` next to a file link. Nothing here comes from memory or from the author's talk.

---

## 1. Where "self" sits in the snapshot

- **One snapshot, no "self" container.** `BotWorldState` is one flat object. The bot's own state is spread over several top-level fields that sit next to the nearby collections ([rs:sdk/types.ts][types-sdk] L558-602):
  - **Self:** `player: PlayerState | null`, `skills`, `inventory`, `equipment`, `combatStyle`, `prayers`, `isSkulled`, `autoRetaliateEnabled`, `opFeedback`.
  - **Nearby:** `nearbyNpcs: NearbyNpc[]`, `nearbyPlayers: NearbyPlayer[]`, `nearbyLocs: NearbyLoc[]`, `groundItems: GroundItem[]`.
  - **Neither:** `tick`, `revision`, `inGame`, `gameMessages`, `dialog`, `interface`, `shop`, `bank`, `trade`, `ge`, `modalOpen`, `combatEvents`.
- **`PlayerState` is the closest thing to a self record.** Its fields are `name`, `combatLevel`, `hp`, `maxHp`, `x`, `z`, `worldX`, `worldZ`, `level` (the map plane, not a skill level), `runEnergy`, `runWeight`, `animId`, `spotanimId`, `combat: PlayerCombatState`, `isDead`, `lifeId`, `respawnCount`, `lastDeathTick` ([rs:sdk/types.ts][types-sdk] L23-52). `PlayerCombatState` holds `inCombat`, `targetIndex`, `targetType`, `lastDamageTick` (L8-21).
- **Collector.** `BotStateCollector.collectState()` builds every field in one object literal. `player` comes from `collectPlayerState()`, and the four nearby lists come from `collectNearbyNpcs/Players/Locs/GroundItems()` ([rs:StateCollector.ts][collector] L148-196). All of them read the game client's `localPlayer` ([rs:StateCollector.ts][collector] L552-556, L892-897, L1000-1005, L1062-1068).
- **The client side uses a different name.** The collector returns the webclient's own `BotState` type, imported from `./types.js` ([rs:StateCollector.ts][collector] L18-62, L89, L148). The SDK publishes it as `BotWorldState`.
- **No `getPlayer()`.** The SDK has `get*`/`find*` accessors for skills, inventory, equipment, bank and every nearby list ([rs:sdk/index.ts][index] L834-1000). For the self record, the docs read `sdk.getState()!.player!` directly ([rs:sdk/README.md][sdk-readme] L113-115; [rs:CLAUDE.md][claude] L67-69).

## 2. What the self part holds, and how "moving" and "arrived" are decided

### No movement state is published

- **What is missing.** `PlayerState` has no moving flag, no destination, no route or waypoint, no run toggle, no heading and no speed ([rs:sdk/types.ts][types-sdk] L23-52).
- **What counts as activity.** `animId` (`-1 = idle/none`) is the only field that says the character is doing something.
- **Movement-related signals.** The only other signal is `opFeedback`, a set of counters of `UNSET_MAP_FLAG` packets ([rs:sdk/types.ts][types-sdk] L538-556).
  - **How the collector classifies them.** It counts an unset as a probable *rejection* when the player's tile did not change since the previous tick ([rs:StateCollector.ts][collector] L201-238).
  - **The documented caveat.** The doc comment warns that "the server also unsets the map flag when a walk finishes normally". So "the walk ended" and "the op was refused" share one signal.
- **Self has no route tile; NPCs do.**
  - **Self:** position is derived from `localPlayer.x/z`. `worldX = mapBuildBaseX + (player.x >> 7)` ([rs:StateCollector.ts][collector] L610-619).
  - **NPCs:** `NearbyNpc` publishes both. `x`/`z` are the "interpolated render position, which trails a moving NPC by 1-3 tiles". `tileX`/`tileZ` are the "authoritative SW route tile" ([rs:sdk/types.ts][types-sdk] L88-96; [rs:StateCollector.ts][collector] L959-983).
  - **What the source doesn't say.** Nothing states whether the self position lags in the same way.

### "Arrived" and "stuck" are inferred by porcelain, by comparing snapshots

There are at least four separate implementations, each with its own thresholds:

| Where | Arrived | Stopped / stuck | Bounds |
|---|---|---|---|
| `waitForMovementComplete` ([rs:sdk/actions-helpers.ts][helpers] L294-374) | Chebyshev distance from `worldX/worldZ` to the target ≤ tolerance | tile unchanged for `STUCK_TICKS = 2` ticks | tick budget `max(3, ceil(dist/2 × 1.5))`, assuming 2 tiles/tick; a 15 s wall-clock failsafe in case states stop arriving |
| `walkStepToward` (L378-414) | as above | moved `< 2` tiles (Euclidean) *and* `stoppedMoving` | one `sendWalk` per step |
| `walkTo` / `_walkToRoute` ([rs:sdk/actions.ts][actions] L1068-1321) | distance ≤ caller's tolerance (default 3) after any step | 3 consecutive stuck steps: try a door, then re-query the path. 3 iterations without beating `bestRemaining`: `Stuck at (x, z)` | 50 path iterations, a 120 s deadline, a refusal when state is older than 10 s (`stale_state`), and an abort on death |
| `talkTo` / `interactLoc` / `interactNpc` ([rs:sdk/actions.ts][actions] L1033-1041, L4172-4221) | n/a: success is an *effect* (dialog, interface, `animId !== -1`, plane change, "teleport") | fine `x/z` unchanged for 2 ticks counts as "idle", and they give up | a 30 s safety net, plus a 2 s grace for late evidence |

More detail on these:

- **`walkTo` decides progress by distance to the target, not distance moved.** "Progress is *closing on the destination*, not distance walked: an oscillating route can move 5+ tiles every iteration while never getting closer" ([rs:sdk/actions.ts][actions] L1134-1137, L1305-1317).
- **`walkTo` detects death through `lifeId`.** It records `lifeId` at the start and aborts if `isDead` is set or `lifeId` changes, because "an orphaned walkTo that keeps marching after a respawn re-enters the danger zone that killed it" (L1105-1117).
- **`waitForIdle`** is documented as "Wait for player to stop moving." ([rs:sdk/API.md][api] L127). The code waits for one state change, then waits until `player.x/z` equal the position from *before* the call ([rs:sdk/actions.ts][actions] L3348-3363).
  - My reading: it resolves when the bot is back where it started, so a walk that ends somewhere else would hit the timeout, and `waitForCondition` throws on timeout ([rs:sdk/index.ts][index] L1868-1890). I did not run it.

## 3. One stream, one cadence, and mixed queries

- **Same stream, same moment.** Self and nearby state are built in the same `collectState` call for the same tick and share one `revision` ([rs:StateCollector.ts][collector] L148-196).
  - **Cadence.** The client publishes once per game tick and again right after each action result ([rs:state-fanout.ts][fanout] L1-7).
  - **Trigger.** The SDK docs say updates arrive "once per server tick (~300ms) when PLAYER_INFO is received", PLAYER_INFO being the own-player update ([rs:sdk/index.ts][index] L1916-1931).
  - **No separate self cadence.** Nothing updates self state on its own schedule.
- **Nearby entries are measured from self when they are collected:**
  - **Distance.** Every `NearbyNpc`, `NearbyPlayer`, `NearbyLoc` and `GroundItem` carries a `distance`, computed from `localPlayer` at collection time ([rs:StateCollector.ts][collector] L922-924, L1020-1022).
  - **Sort order.** Each list is sorted by that distance (L995-997, L1049-1051).
  - **Scan radius.** Locs and ground items are scanned within 15 tiles of the player's tile (L1079-1088, L1275).
  - **Reachability.** `reachable` comes from a flood fill out of the player's position, redone every collect because "the player moves and doors open between snapshots" (L153-155).
- **Porcelain mixes self and nearby all the time:**
  - **"Then nearest."** `find*` returns the "shortest matching name wins, then nearest". The "nearest" part only works because the lists arrive pre-sorted by self-relative distance: `shortestNameMatch` keeps the first match on ties ([rs:sdk/index.ts][index] L937-990; [rs:sdk/action-quantity.ts][aq] L51-75).
  - **Combat target.** `player.combat.targetIndex` is resolved by looking it up in `nearbyNpcs` ([rs:sdk/formatter.ts][fmt] L40-44, L176-184).
  - **Door handling.** `walkTo` reads `nearbyLocs` and their `distance` to choose a door to open (L1146-1150 of [rs:sdk/actions.ts][actions]).
  - **Readiness.** `waitForReady` requires a valid self position *and* at least one nearby entity before it counts the game as ready ([rs:sdk/index.ts][index] L1837-1851).
- **A change of plane makes nearby data stale.** `NearbyLoc.level` is "always the player's plane at scan time. A loc carried across a climb/descend is stale: re-scan instead" ([rs:sdk/types.ts][types-sdk] L196-201).
- **HP is in two places.** It is in `player.hp/maxHp` and in the `Hitpoints` skill. The brief summary reads the skill, not `player.hp` ([rs:sdk/formatter.ts][fmt] L35-38).

## 4. Naming

- **For the bot itself:**
  - `player`, with the type `PlayerState`; `localPlayer` inside the client.
  - "we"/"us" in doc comments: "the NPC/player we're targeting", "Tick when we last took damage" ([rs:sdk/types.ts][types-sdk] L11-20).
  - "your own position" in a user-facing error ([rs:sdk/actions.ts][actions] L1080-1087).
  - `includeSelf`/`fromSelf` for the bot's own chat lines ([rs:sdk/index.ts][index] L733-741).
  - The dumps title the section `## Player` ([rs:sdk/README.md][sdk-readme] L36-60; [rs:sdk/formatter.ts][fmt] L163-170).
- **For what is around it:**
  - "Nearby" is the usual prefix: `NearbyNpc`, `NearbyPlayer`, `NearbyLoc`, `findNearby*`, `getNearby*`. The exception is `GroundItem`/`groundItems`.
  - The whole snapshot is the "World State" (`BotWorldState`, `# World State`).
  - Other characters are "nearby players" or "other players" ([rs:CLAUDE.md][claude] L227-228).
- **"Player" means two things.** It names both the bot (`state.player`) and every other character (`nearbyPlayers`). That ambiguity costs them extra code:
  - The SDK fills in `worldX/worldZ` on nearby players so `walkTo(p.worldX, ...)` "can't silently become walkTo(undefined, undefined)" ([rs:sdk/index.ts][index] L1977-1984).
  - `walkTo` explains in its error message which field to use for which ([rs:sdk/actions.ts][actions] L1080-1087).
- **One name, two units.**
  - `PlayerState.x/z` hold the client's scene-local sub-tile coordinates: 128 units per tile, the same raw values the collector shifts `>> 7` to get tiles ([rs:StateCollector.ts][collector] L615-618, L955).
  - `NearbyNpc.x/z` and `NearbyPlayer.x/z` are world tiles.
  - `PlayerState.x/z` have no doc comment ([rs:sdk/types.ts][types-sdk] L30-33).
  - My reading, not verified: `interactLoc` treats `player.x/z` moving by more than 8 as "a jump far beyond walking range", meaning a teleport ([rs:sdk/actions.ts][actions] L4183-4191). In sub-tile units, 8 is less than one tile.
- **No explanation of the split.** I found no comment or doc that explains why self and nearby are laid out this way. The layout is never discussed.

## 5. Lessons recorded about walking state

- **False "arrived".** The final waypoint must use the caller's tolerance, because "the stepper reported 'arrived' up to 2 tiles short, the outer check kept failing, and walkTo ended in 'Stuck at' on open ground" ([rs:sdk/actions.ts][actions] L1282-1291).
- **Dead-end loop.** Re-walking a partial path that ends in the same place loops forever. They now fail fast and cite "Varrock-house-to-bank dead-end loop, bug report 2026-08-10" (L1131-1133, L1192-1213).
- **Phantom doors.** A door in static collision data may be missing from the live map. They skip it "so a phantom door cannot cause endless re-planning" (L1255-1260).
- **Stale state.** `walkTo` refuses to start or continue on state older than 10 s (L1089-1100). Agents are told to check `sdk.getStateAge()` after a pause ([rs:CLAUDE.md][claude] L256-258).
- **Distance metric.** It is now Chebyshev, "the game's own metric"; "the old Euclidean 1.41 rejected" a diagonal neighbour ([rs:sdk/actions-helpers.ts][helpers] L417-423). `walkStepToward` still uses Euclidean for "moved" (L405-411), and so do the `learnings/walking.md` snippets ([rs:learnings/walking.md][l-walk] L35-41, L54-62).
- **Agents are told not to trust walk success.** "Always check if you actually arrived" by re-reading `player.worldX/worldZ` after `walkTo`. There is a retry loop with 500 ms sleeps per waypoint ([rs:learnings/walking.md][l-walk] L28-63).
- **Idle versus done.** The interaction methods add a 2 s grace because "the idle window can expire a tick before the server's dialog arrives" ([rs:sdk/actions.ts][actions] L1054-1062).

---

## Implications for us (interpretation, not fact)

> Everything below is my reading of the findings and should be grilled. Terms are from `CONTEXT.md`.

### Mapping

| rs-sdk | Ours |
|---|---|
| `BotWorldState` (the whole snapshot) | Self state + **Surroundings**. "world state" is already on Surroundings' _Avoid_ list, which fits: neither of our terms is the whole thing. |
| `player` + `skills`/`inventory`/`equipment`/... | the new self term, about the Agent's own **Character** |
| `nearbyNpcs/Players/Locs`, `groundItems` | **Surroundings** |
| `sendWalk` / `walkTo` | Primitive `moveTo` (plus `stop`, `face`); `walkTo` is closer to a **Tactic** |
| position deltas between snapshots, `STUCK_TICKS`, `animId` | Percepts `moved` / `arrived` / `stopped`, which the Harness works out as a client would |

### What to take from it

1. **Give "moving / arrived / stopped" a home in the self state.** rs-sdk publishes no movement state. Each porcelain method re-derives it from position deltas with its own thresholds, and at least four different definitions resulted (§2). Our Harness already reports `arrived` and `stopped`. The self module should reduce them into one movement status (for example `moving to T` / `arrived at P` / `stopped at P`) that every Tactic reads. No Tactic should re-invent stuck detection.
2. **`arrived` should carry where, not just whether.** rs-sdk's false "arrived" came from a tolerance mismatch. If the Percept carries the final position (and the target), a Tactic can judge "close enough" itself.
3. **Keep one coordinate unit and one field name for position in both modules.** rs-sdk's `x/z` mean sub-tiles on self and tiles on others, which has produced fallback code, error-message coaching and, I suspect, at least one bug (§4).
4. **Don't store distances in Surroundings.** rs-sdk can bake `distance` in because every snapshot is rebuilt from scratch. We build Surroundings incrementally from Percepts, so a stored distance goes stale the moment we move. Compute "nearest" and "distance to" at query time, from the self position and a Surroundings entry. Put those helpers in a small module that reads both, not in either one.
5. **A self change can invalidate Surroundings.** For example: a region or zone change, death and release, or a teleport. rs-sdk documents this only as a comment about stale locs after a climb. It should be an explicit rule in the self → Surroundings direction.
6. **Name the life, not just the HP.** `lifeId`/`isDead` let `walkTo` abort when the character dies on the way. When HP arrives, a "life" counter in the self state is a cheap way to cancel Tactics that predate a death.
7. **Freshness belongs to both.** rs-sdk refuses to walk on state older than 10 s. Each self field (at least position) should carry when it was last updated by a Percept.
8. **Avoid "player".** rs-sdk's own code shows the cost of using one word for self and for others. CONTEXT.md already avoids "player" for Agents and Humans.

### Candidate names for the self-state term

Checked against CONTEXT.md. Character, Agent, Human, Harness, Mind, Percept, Surroundings, Tactic, Plan, Lesson and Journal are taken. The _Avoid_ lists rule out toon, avatar, bot, mimic, NPC, player, body, brain, world model and world state.

**1. Self** (my pick)

> _Draft:_ What an Agent currently believes about its own Character, such as where it is, which way it faces and whether it is moving, built only from Percepts. _Avoid_: player, me, local player, character state.

- **Pros:**
  - "Self and Surroundings" is a natural pair, and it reads well in code: `self.position`, `surroundings.nearest(...)`.
  - It matches rs-sdk's own incidental words (`includeSelf`, "your own position").
  - It clashes with no existing term or _Avoid_ entry.
- **Cons:**
  - `self` is a global in browsers, workers and Bun (`self === globalThis`). The module and type should be `Self`/`self.ts`, with a local variable like `me` or `own`.
  - It could be misread as the Agent's persona or identity, which CONTEXT.md puts in **Agent** ("persona plus memory"). The definition must say "its own Character's state".

**2. Proprioception**

> The body's sense of its own position and movement. It pairs with Percept.

- **Pros:**
  - It is precise for position, heading and moving/arrived/stopped.
  - It sits inside the perception metaphor we already use.
- **Cons:**
  - It is jargon and long.
  - It stretches badly over HP, mana and inventory (that would be "interoception").
  - It leans on a body metaphor, and "body" is on the Harness _Avoid_ list.

**3. Condition**

- **Pros:**
  - It covers HP, endurance, dead/alive and "moving" well, and it is plain English.
- **Cons:**
  - It is weak for position and heading.
  - It collides with the predicate sense of "condition" (rs-sdk's `waitForCondition`), which our Tactic waits may well adopt.
  - It is easy to read as "status effect".

**Rejected:**

- **Player** / **PlayerState:** on the _Avoid_ list, and it is the very ambiguity rs-sdk pays for.
- **Character State:** blurs the server's Character (truth) with the Mind's belief about it.
- **Avatar**, **Body:** on _Avoid_ lists.
- **Status:** already used for the triage `Status:` line on issues.

<!-- Link references (pinned commit) -->
[types-sdk]: https://github.com/MaxBittker/rs-sdk/blob/b565f2d920bf6f5ec427bc6f66dc6fd64a2e15c1/sdk/types.ts
[index]: https://github.com/MaxBittker/rs-sdk/blob/b565f2d920bf6f5ec427bc6f66dc6fd64a2e15c1/sdk/index.ts
[actions]: https://github.com/MaxBittker/rs-sdk/blob/b565f2d920bf6f5ec427bc6f66dc6fd64a2e15c1/sdk/actions.ts
[helpers]: https://github.com/MaxBittker/rs-sdk/blob/b565f2d920bf6f5ec427bc6f66dc6fd64a2e15c1/sdk/actions-helpers.ts
[aq]: https://github.com/MaxBittker/rs-sdk/blob/b565f2d920bf6f5ec427bc6f66dc6fd64a2e15c1/sdk/action-quantity.ts
[fmt]: https://github.com/MaxBittker/rs-sdk/blob/b565f2d920bf6f5ec427bc6f66dc6fd64a2e15c1/sdk/formatter.ts
[api]: https://github.com/MaxBittker/rs-sdk/blob/b565f2d920bf6f5ec427bc6f66dc6fd64a2e15c1/sdk/API.md
[sdk-readme]: https://github.com/MaxBittker/rs-sdk/blob/b565f2d920bf6f5ec427bc6f66dc6fd64a2e15c1/sdk/README.md
[claude]: https://github.com/MaxBittker/rs-sdk/blob/b565f2d920bf6f5ec427bc6f66dc6fd64a2e15c1/CLAUDE.md
[l-walk]: https://github.com/MaxBittker/rs-sdk/blob/b565f2d920bf6f5ec427bc6f66dc6fd64a2e15c1/learnings/walking.md
[collector]: https://github.com/MaxBittker/rs-sdk/blob/b565f2d920bf6f5ec427bc6f66dc6fd64a2e15c1/server/webclient/src/bot/StateCollector.ts
[fanout]: https://github.com/MaxBittker/rs-sdk/blob/b565f2d920bf6f5ec427bc6f66dc6fd64a2e15c1/server/gateway/state-fanout.ts
