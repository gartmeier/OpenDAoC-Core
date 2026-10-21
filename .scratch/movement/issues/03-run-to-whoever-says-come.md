# 03: Run to whoever says `come`

**What to build:** The tracer bullet. The Human says `come` next to Greeen, and he runs to a point 100 units short of where the Human stood, under exactly the rules a Human's client runs under. `stop` stops him. Ctrl+C during a run stops him and then quits.

On the server:
- `moveTo(x, y, z)` and `stop` Primitives. `moveTo` runs in a straight line at the maximum speed the server last told the client. A new `moveTo` replaces the current one. Results report only whether they were queued (ADR 0004).
- A pure stepping class, like the liveness one. Given start, target, speed and elapsed game time, it gives the position, heading and Z to send, and whether the target is reached. Z is interpolated linearly from start to target, the heading points at the target, it never overshoots after a stall, and the zone ID is the zone at the current X and Y in the region's static layout (ADR 0006).
- While running, the Harness sends running position updates (ticket 01's builder) at the running rate from the capture. Standing, it falls back to the idle liveness unchanged. Only while a Mind is connected (ADR 0005).
- Percepts the Harness works out itself:
  - `moved` with each position update while running: position, heading, speed;
  - `arrived` with where the Character ended up;
  - `stopped` with where it ended up and the reason: `stop`, or replaced by a new `moveTo`.
- Projections for the Agent's own position at world entry and for the maximum speed update.
- Regenerated TypeScript.

In the Mind:
- A Self module, fed Percepts: own position, heading, maximum speed, and a single movement status (standing, moving to a target, arrived, stopped with a reason), built only from Percepts. Type name `Self`, never a variable named `self`.
- A Come module. A say line from someone else whose whole text is `come` (case-insensitive) gives a `moveTo` to the point 100 units short of the speaker's position in Surroundings, along the line from Self. A speaker not in Surroundings gives nothing. `stop` from someone else gives `stop`. Anything else gives nothing.
- The connection's Ctrl+C sends `stop` and then `quit`, because the server refuses `/quit` while the Character is moving.
- The entry point runs the Come Mind. The echo module and its tests are deleted.

See the spec: `.scratch/movement/spec.md`.

**Blocked by:** 01, 02

**Status:** ready-for-agent

- [x] Stepping (server unit tests): exact arrival, heading towards the target, Z interpolation, no overshoot after a stall, a stop mid-run, the zone ID changing at a zone border within a region
- [x] Own position at world entry and maximum speed give typed Percepts
- [x] Self (`bun test`): status through `moved`, `arrived`, `stopped`; maximum speed
- [x] Come (`bun test`): another speaker's `come` gives the right `moveTo`; an unknown speaker, own lines, other chat types and longer lines give nothing; `stop` gives `stop`
- [x] Connection (`bun test`): Ctrl+C sends `stop` before `quit`
- [x] In game: `/say come` from about 30 m on flat ground makes Greeen run, smoothly in the Human's client with the running animation, to about 100 units short of the Human; the Mind prints `arrived`
- [x] In game: `/say come` then `/say stop` stops him mid-run; the Mind prints `stopped` with the stop reason
- [ ] In game: Ctrl+C during a run stops him, and he quits through the normal path
- [ ] The server log has no speed-check entries for Greeen after these runs

## Comments

**2026-10-20 (agent):** Built on branch `claude/m3-03-run-to-come-81dd36` (85364a065 to 46d1b72f6). Every automated box holds: server 223 passing in Release, Mind 40 passing in `bun test`, `bun run typecheck` clean. The in-game boxes need the Human with a running server and the real client, so this is back with the Human.

Decisions:
- **The ramp is copied, the stop isn't.** A run speeds up from standing at 265 units/s² (both captured starts gain about 53 per 200 ms) and slows down at 480 units/s² so that it stops exactly at the target. That gives the captured "one update with the speed falling to about 100, then one with speed 0, about 40 units on" at the end of a run. The X and Y the Harness sends always follow from the speed it claims, so the speed check sees at most the maximum speed. `stop`, though, ends the run where the Character is at once, and its speed-0 position update goes into the inbound queue straight away, instead of gliding the captured ~40 units on. The server refuses `/quit` while the Character's speed isn't 0, and Ctrl+C sends `quit` right after `stop`. With a glide, that quit would be refused. A new `moveTo` mid-run keeps the speed and turns at once, as a client steering while running does.
- **Rate and flags.** One running position update every 200 ms of game time (the capture: 200 or 233, mean 212; the server broadcasts at most one per 200 ms), the first on the tick after a `moveTo`, as the captured client sends its first as soon as it moves. Running updates set the heading's 0x1000 flag, as nearly every captured one does. The idle position update is unchanged and comes back 2133 ms after the last running one.
- **Maximum speed** is what the server last told the client: the max speed packet's percentage times 191 / 100, taken from the `maxSpeed` Percept the client was sent. A change mid-run applies at once (a lower one) or ramps (a higher one). A maximum speed of 0 makes a run wait where it is without ending, like a rooted client holding its run key. Ticket 05 should end it with "unable to move".
- **`moved` carries the run's target** (`targetX`, `targetY`, `targetZ`) as well as the position, heading and speed the spec lists. Self's "moving to a target" has to come from a Percept, and the client knows where it's running to.
- **Zone ID** is the zone at the current X and Y from the region's static zone list (`Region.GetZone`). Outside every zone it stays the start's, since an unknown zone ID sends the client to the character screen and a wrong existing one isn't checked.
- **Come** needs the whole say text `come` or `stop`, any case, from someone else. `come` from a speaker within 100 units gives nothing yet, because the face for that is ticket 04. `stop` from anyone else gives `stop`, even from someone not in Surroundings, because it needs no position.

What exists now:
- Server (`GameServer/agents`): `StraightRun` (the pure stepper), `HarnessMovement` (runs it on the client tick, only while a Mind is connected, and works out `moved`, `arrived`, `stopped`), the `moveTo` and `stop` Primitives (`moveTo` needs all of `x`, `y`, `z`), and projections `ownPosition` (mirrors `PacketLib1124.SendPlayerPositionAndObjectID`: global position, object ID, heading, the dungeon zone offsets, the region skin, diving flags, and the server name in housing regions) and `maxSpeed` (mirrors `PacketLib168.SendUpdateMaxSpeed`: the percentage, turning disabled, water speed). `HarnessPacketLib` hands each Percept to `HarnessClient.OnPerceived`, where ticket 05 can add the jump. `MindLink.Perceive(Percept)` sends the Harness's own Percepts in sequence with the server's. `mind/src/protocol.ts` is regenerated.
- Tests: `UT_StraightRun` covers exact arrival, heading (including the capture's 1523), Z, no overshoot after a stall, never passing the target, a stop mid-run, a zone border, the ramp and the slow-down against the capture, distance per update under the maximum speed at 33, 200 and 233 ms, short runs, starting too fast to stop, max speed changes and max speed 0. `UT_PerceptProjection` covers own position (including a dungeon and outside any zone) and max speed. `UT_MindProtocol` covers reading `moveTo` (and refusing one without `z`), `stop`, and the `stopped` reason as a name. There are also `UT_Liveness` and `UT_HarnessClient` refusals. `WorldDoubles` now builds regions with several zones and an ID, and `TestPlayer` has a movement component and settable `GetModified`.
- Mind: `src/self.ts` (`Self`), `src/come.ts` (`Come`), Ctrl+C in `src/connection.ts` sends `stop` then `quit`, `agent.ts` runs the Come Mind, and the echo module and its tests are gone. Against a fake server, the Mind printed (some lines left out):

      #1	0.100 s	ownPosition #17	X:296000 Y:642000 Z:4848 heading 0 region 200
      #2	0.200 s	maxSpeed 100% (191.0 per second)
      #4	0.400 s	CT_Say CL_ChatWindow	Vue says, "come"
      Sending moveTo X:298900 Y:642000 Z:4848.
      #5	0.500 s	moved	X:296001 Y:642000 Z:4848 heading 3072 speed 19.6 to X:298900 Y:642000 Z:4848
      #6	0.600 s	arrived	X:298900 Y:642000 Z:4848
      #8	0.800 s	stopped (Stop)	X:298900 Y:642000 Z:4848

For the Human, with the server running a build of this branch (the Mind setup is unchanged; a fresh worktree needs `CoreServer/config/serverconfig.xml` copied in before it builds). Test on flat open ground: runs go in a straight line, through anything in the way.
1. `bun mind/agent.ts greeen`. While he enters the world, the Mind prints `ownPosition` and `maxSpeed 100% (191.0 per second)` (or Greeen's own percentage).
2. Stand 300 to 500 units from Greeen, in view, with `/gmstealth off`, and `/say come`. A say only reaches players within 512 units (`WorldMgr.SAY_DISTANCE`), so from farther away Greeen never hears it and nothing happens. The Mind's `ownPosition` and your `playerPosition` lines give both positions. The Mind prints `Sending moveTo ...` and `moved` lines about five a second. In your client he turns towards where you stood and runs there with the running animation, smoothly, without jumps, speeding up at the start and slowing at the end. He stops about 100 units short of you, and the Mind prints `arrived`. He doesn't turn to face you yet (ticket 04).
3. `/gloc` standing where he stopped, or `/gloc` on him: his position matches the `arrived` line to within a unit or two.
4. Walk away, `/say come`, and `/say stop` while he runs. He stops at once, and the Mind prints `stopped (Stop)`.
5. `/say come` again, and press Ctrl+C in the Mind's terminal while he runs. He stops, then quits through the normal path, quit delay included. The Mind exits once the server has disconnected him.
6. Start the Mind again. `/say come`, run a few steps to the side while he runs, and `/say come` again while you're still within 512 units of him. The Mind prints `stopped (Replaced)`, and he turns towards the new spot without stopping.
7. Search the server log and the cheat log (`Debug/logs/server.log`, `Debug/logs/cheat.log`) for `Speed hack`. There are no entries for Greeen.

Known gaps:
- **No terrain.** Z is interpolated between start and target, so he floats or sinks on hills (milestone 3b).
- **No face** after arriving, and nothing for a speaker within 100 units (ticket 04).
- **A server move doesn't end a run** (a `/jump`, a pull-back by the speed check), and a maximum speed of 0 makes a run wait rather than end (ticket 05).
- **The target field** (data offsets 22–23) of running position updates stays 0, since the Harness has no target yet.
- **Not tested automatically:** the movement loop through the server's handlers and its speed check. Steps 2, 6 and 7 cover them.

**2026-10-21 (agent):** The Human said `/say come` and nothing happened. Greeen's trace (`Greeen-20261021-085215-217.tsv`) has no say line after Vue's `playerCreate` at 35.3 s: the say never reached Greeen's client. A say only reaches players within 512 units (`GameLiving.Say`, `WorldMgr.SAY_DISTANCE`), and step 2 said to stand about 1000 units away. Steps 2 and 6 now say to stay within 512 units. The spec's "about 30 m" can't be met by a say: a `come` run is at most about 410 units (512 minus the 100 stand-off).

**2026-10-21 (Human, recorded by agent):** From within say range, `/say come` works: Greeen runs to the Human. Greeen's trace shows three `come` lines from Vue (at 193.0, 218.7 and 235.2 s), and `server.log` has no `Speed hack` entries so far (no `cheat.log` was written). Still to check: `/say stop` mid-run (step 4), Ctrl+C mid-run (step 5), a replaced run (step 6), and the logs after all of them (step 7).

**2026-10-21 (Human, recorded by agent):** `/say stop` stops him mid-run (trace: two `stop` lines from Vue, at 285.6 and 292.2 s). **Ctrl+C found a bug.** The stop and the quit went through ("You will quit after sitting for 20 seconds" at 323.7 s), but 2.1 s later he stood up ("You are no longer waiting to quit"), and the second Ctrl+C dropped the connection, so he went linkdead as designed.

The cause: a run ended on a fraction of a unit. `PlayerMovementComponent.UpdatePosition` keeps only whole units (`Owner.X = (int) newPosition.X`), and the next idle position update repeats `player.X`. To the server, the difference of a fraction is a move (`EqualsXY` compares exactly), so `OnPlayerMove` stood the sitting Character up, which cancels a quit. It would also stand him up after any sit following a run. Fixed by ending every run on whole units (`HarnessMovement.WholeUnits`), so the idle update repeats exactly what the client last sent. `arrived` and `stopped` carry those whole units. Server 226 tests pass. Re-check step 5 (Ctrl+C mid-run) on a build with this fix.
