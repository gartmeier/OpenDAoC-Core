# 04: Face the speaker

**What to build:** When Greeen arrives after a `come`, he turns to face whoever called him. If the speaker is already within 100 units, he only turns.

- A `face(heading)` Primitive, sent as the heading update a real client sends (ticket 01's builder). A `face` during a run is immediately overtaken by the run's own heading and doesn't end the run.
- The Come module: on `arrived` after a `come`, it gives a `face` towards that speaker's last known position in Surroundings. A speaker within 100 units of Self gives a `face` instead of a `moveTo`.
- Regenerated TypeScript.

See the spec: `.scratch/movement/spec.md`.

**Blocked by:** 03

**Status:** ready-for-agent

- [x] `face` sends the heading update bytes the capture shows (golden bytes via ticket 01's builder)
- [x] Come (`bun test`): `arrived` after `come` gives a `face` towards the speaker; a speaker within 100 units gives a `face` only
- [ ] In game: after `/say come`, Greeen arrives and turns to face the Human
- [ ] In game: `/say come` from right next to him makes him turn without moving

## Comments

**2026-10-20 (agent):** Built on branch `claude/m3-04-face-e47e88`, cut from ticket 03's branch, which isn't in main yet (4df9ce482, 2895132cb). Both automated boxes hold: server 231 passing in Release, Mind 49 passing in `bun test`, `bun run typecheck` clean. The in-game boxes need the Human with a running server and the real client, so this is back with the Human.

Decisions:
- **`face` turns at once: one heading update (0xBA) with the final heading.** The captured keyboard turn sends the heading reached so far every 200 or 233 ms, about 630 heading units a second (1.3 turns in 8.4 s). The server sets the Character's heading from each 0xBA and broadcasts it to other clients; neither sees the rate, only the headings. A client turned with the mouse can reach any heading within one 200 ms interval, so a single 0xBA is something a real client sends. Stepping at the keyboard rate would make a half turn take over 3 s and need its own stepping next to the run's. If the turn looks wrong in your client, a rate can be added.
- **Bytes.** The 0xBA is ticket 01's builder with the Character's health % and action flags, as the capture has them. Position updates keep their idle cadence after a face, as while the captured client turns.
- **The next position update carries the face's heading.** The idle position update is built from the Character's heading, which the server only sets once it reads the 0xBA on the next client tick. An idle update queued in that window would have turned him back, so until the server has read the 0xBA, the idle update carries the faced heading. Like the idle update before this ticket, it still leaves out the heading's 0x1000 flag.
- **During a run, `face` sends nothing and still reports queued.** A running client sends no heading update (the capture: steering while running only changes the position updates' heading). The run's next position update carries its own heading, which overtakes the face, and the run goes on with no `stopped`.
- **A heading outside 0 to 4095 is refused** with a reason, as a Mind developer's mistake. `face` without a heading is refused by the protocol, like `moveTo` without `z`.
- **Come** remembers whose `come` started the run in progress and, on `arrived`, faces where Surroundings last had that speaker, then forgets them: one face per `come`. A later `come` that runs replaces the speaker, and `stop` forgets them. A speaker within 100 units gives a `face` instead of a `moveTo`. A speaker straight above or below (same X and Y) gives nothing, since no heading faces them. The heading is worked out in X and Y by `headingTowards` in `src/surroundings.ts`, next to `distance`: h points along (−sin θ, cos θ), θ = h × 2π / 4096, rounded.

What exists now:
- Server (`GameServer/agents`): `FacePrimitive(Heading)` in the protocol, its dispatch in `HarnessClient`, and `HarnessClient.HeadingUpdate`. `mind/src/protocol.ts` is regenerated.
- Tests: `UT_HarnessClient` dispatches a `face` to a Harness client in the world and checks the queued packet against the capture's first heading update, byte for byte. It also checks that a `face` during a run queues nothing and doesn't end the run, that a heading outside a full turn is refused, that an idle position update carries a queued face's heading, and that a `face` is refused outside the world. `UT_MindProtocol` reads `face` and refuses one without a heading. `TestPlayer` has a settable health %, and `WorldDoubles.SetSessionId` works on any client.
- Mind: `Come` gives `face` on `arrived` and from within 100 units. Tests cover the arrival face, the speaker's last known position, one face per come, the latest come that ran, nothing after `stop` or once the speaker is gone, the close-by face and a speaker straight overhead. `headingTowards` has its own tests, including the capture's heading 1523. `agent.ts` prints `Sending face heading N.`, and the Mind README says what Come does now.

For the Human, with the server running a build of this branch (it includes ticket 03's work, so ticket 03's in-game steps can be done in the same session; a fresh worktree needs `CoreServer/config/serverconfig.xml` copied in before it builds). Test on flat open ground.
1. `bun mind/agent.ts greeen` and wait for `maxSpeed`.
2. Stand about 1000 units from Greeen, off to one side of the way he faces, in view with `/gmstealth off`, and `/say come`. He runs to about 100 units short of you. When the Mind prints `arrived`, it prints `Sending face heading N.`, and in your client he turns to face you.
3. Note how the turn looks: a snap or a smooth turn. Either is what a mouse-turned client looks like. Say if it looks wrong, and a turn at the captured keyboard rate can be added.
4. Stand still for at least 3 s after he has turned. He keeps facing you, with no flick back when his next idle position update goes out.
5. Walk round behind him, staying within about 100 units (3 m, close enough to touch), and `/say come`. He turns to face you without moving. The Mind prints `Sending face heading N.` and no `moved` lines.
6. `/say come` again, and before he arrives walk about 300 units to one side. When he arrives, he faces where you stand now rather than where you said `come` (if your client has sent him your new position by then).
7. Search the server log and the cheat log (`Debug/logs/server.log`, `Debug/logs/cheat.log`) for `Speed hack`. There are no entries for Greeen.

Known gaps:
- **Self's heading doesn't follow a `face`.** No Percept says the client turned, and Self is built only from Percepts, so `Self.heading` keeps the last `moved` or `ownPosition` heading. Come doesn't need it.
- **Turning disabled isn't honoured.** The max speed packet says when the client mustn't turn (`turningDisabled`), and a real client then sends no heading update. `face` still sends one, and the server's heading handler doesn't check either. Ticket 05 changes how the max speed Percept is handled, so it can take this.
- **A `come` from close by while he runs** gives a `face`, which the run overtakes; he carries on to the first speaker and faces them on arrival.
- **Not tested automatically:** how the server and other clients take the heading update. Steps 2 to 5 cover it.

