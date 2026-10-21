# 05: When the server moves the Agent

**What to build:** When the server sets Greeen's position itself (a GM `/jump`, a pull back by the speed check), his run ends and he takes the server's position, as a real client snaps to it. When the server sets his maximum speed to 0 (for example while rooted), his run ends too. Surroundings forgets what's now out of view.

- A projection for the server's jump call: position and heading.
- The Harness ends the current run on a jump, takes the server's position, heading and zone, and sends `stopped` with the reason "moved by the server". A maximum speed of 0 ends the run with `stopped`, reason "unable to move".
- Self follows the jump.
- Surroundings forgets objects farther than the visibility distance (6000) from Self whenever Self's position changes, including a jump. Never by time.
- Regenerated TypeScript.

See the spec: `.scratch/movement/spec.md`.

**Blocked by:** 03

**Status:** ready-for-agent

- [x] The jump call gives a typed Percept with exactly what the 1.127 packet carries
- [x] Server unit tests: a jump mid-run ends the run at the server's position; a maximum speed of 0 ends it
- [x] Self (`bun test`): a jump sets position and status
- [x] Surroundings (`bun test`): objects beyond 6000 from Self are forgotten when Self moves, including on a jump; nothing is forgotten by time
- [x] In game: `/say come`, then a GM `/jump`s Greeen mid-run; he's at the new spot, and the Mind prints `stopped` with the server-move reason

## Comments

**2026-10-20 (agent):** Built on branch `claude/m3-05-server-moves-01a164`, cut from ticket 03's branch (714f4005e to 2f9f6521b). Every automated box holds: server 238 passing in Release (15 new), Mind 53 passing in `bun test` (13 new), `bun run typecheck` clean. The in-game box needs the Human with a running server and the real client, so this is back with the Human.

Decisions:
- **The jump Percept** (`jump`) mirrors `PacketLib168.SendPlayerJump`, which no later library overrides. It carries what the packet writes: global `x`, `y`, `z`, `objectId`, `heading` and `houseNumber` (0 outside a house). `z` is the packet's 16 bits, so a negative Z comes out as 65536 minus it. For `/faceloc`, `/facegloc` and `houseface` the server sends only the heading, with 0 for the position, and that's how the Percept carries it too. The client can only tell that case from the zeros, so the Harness and Self do the same.
- **A jump with a position ends the run** where the server put the Character, with `stopped` and reason `MovedByServer`, after the `jump` Percept. The Harness sends no position update of its own for it. Instead it drops any position update still queued from before the jump (a `stop`'s speed-0 update can sit in the queue until the next client tick), so the next one, idle or running, carries the server's position. The server's speed check pauses for 750 ms on the jump and starts its samples over, so it never sees the jump as movement. A heading-only jump doesn't end a run: the Character hasn't moved, and the run's own heading overtakes it at the next position update, as a `face` during a run is meant to.
- **A maximum speed of 0 ends the run** where the Character is, at once, with a speed-0 position update and `stopped` with reason `UnableToMove`, as `stop` does. A `moveTo` while the maximum speed is 0 doesn't start a run: the Mind hears `stopped (UnableToMove)` where the Character stands, and nothing is sent. That includes a `moveTo` before the server has sent any maximum speed. `StraightRun` still waits at a maximum speed of 0, but the Harness never asks it to any more.
- **Self after a jump** takes the position and heading, and its status becomes `standing`, as at world entry: it's no longer where an earlier run arrived. If it was running, the Harness's `stopped (MovedByServer)` comes next and sets that status. A heading-only jump sets only the heading.
- **Surroundings** takes the Agent's position from the same Percepts as Self (`ownPosition`, `jump`, `moved`, `arrived`, `stopped`), through `ownPositionIn` in `self.ts`, never from Come. Whenever that position changes it forgets every player more than 6000 from it, Z included (exactly 6000 stays). It forgets nothing when a player moves out of view while the Agent stands still, and nothing by time.
- **Test seam.** `HarnessMovement` now talks to its client through a small `IMovingClient` interface (the Character, the packet queue, the Mind), which `HarnessClient` implements, so `UT_HarnessMovement` can run it without a booted world.

What exists now:
- Server: `JumpPercept` and the two new `StopReason` values in `MindProtocol.cs`, the projection in `PerceptProjection.cs`, `HarnessMovement.OnJump` and the max-speed-0 handling, and `HarnessClient.Unqueue`. `mind/src/protocol.ts` is regenerated.
- Tests: `UT_PerceptProjection` (a jump gives what the packet carries; a heading-only jump; Z in 16 bits; nothing before the Agent has a Character). `UT_HarnessMovement` (a stop ends a run where the Character is; a jump mid-run ends the run at the server's position, for good; queued position updates are dropped and other packets kept; a `moveTo` after a jump runs from the server's position; a jump while standing does nothing; a heading-only jump leaves the run going; a maximum speed of 0 ends the run with a speed-0 update; a lower one doesn't; a `moveTo` at maximum speed 0, or before any, doesn't start). Mind: Self (5 new) and Surroundings (8 new).
- The Mind prints the new Percept as `jump #17	X:280000 Y:630000 Z:4000 heading 1024`, or `jump #17	heading 2048` for a heading-only one. Against a fake server, a run the server jumped mid-way printed:

      #5	0.500 s	moved	X:296001 Y:642000 Z:4848 heading 3072 speed 19.6 to X:298900 Y:642000 Z:4848
      #6	0.600 s	jump #17	X:280000 Y:630000 Z:4000 heading 1024
      #7	0.700 s	stopped (MovedByServer)	X:280000 Y:630000 Z:4000
      #8	0.800 s	jump #17	heading 2048
      #9	0.900 s	maxSpeed 0% (0.0 per second)
      #10	1.000 s	stopped (UnableToMove)	X:280000 Y:630000 Z:4000

For the Human, with the server running a build of this branch (it includes ticket 03; a fresh worktree needs `CoreServer/config/serverconfig.xml` copied in before it builds). Use your GM Character, with `/gmstealth off`, on flat open ground in the same region as Greeen.

A `/say` only reaches 512 units (`WorldMgr.SAY_DISTANCE`), so Greeen has to be within that to hear `come`, and a run from there lasts about 2 s. Make the commands that have to land mid-run into macros on your quickbar first (`/macro <name> <command>`, then drag the button onto the bar).
1. `bun mind/agent.ts greeen`, and stand in view of Greeen.
2. Pick a spot about 1500 units from Greeen, `/gloc` there and note X, Y and Z. Make a macro `/jump Greeen to <X> <Y> <Z>` with them, and a macro `/speed 0`.
3. Stand about 450 units from Greeen and `/say come`. While he runs, press the jump macro. In your client he's at the spot at once and doesn't slide back. The Mind prints `jump #<his id>` with that position, then `stopped (MovedByServer)` at the same position.
4. Walk to within about 450 units of him at the new spot and `/say come` again. He runs from there to about 100 units short of you, and the Mind prints `moved` lines starting at the new spot, then `arrived`.
5. Target Greeen and type `/speed` to see his maximum speed (191 unless he has one of his own). Step about 450 units away, `/say come`, and press the `/speed 0` macro while he runs. He stops where he is. The Mind prints `maxSpeed 0%` and `stopped (UnableToMove)`.
6. `/say come` once more. He doesn't move, and the Mind prints `Sending moveTo ...` and straight away `stopped (UnableToMove)`. Then `/speed <the value from step 5>` to give him his speed back (it's saved with his Character). The Mind prints `maxSpeed 100%` (or his percentage), and `/say come` works again.
7. Search the server log and the cheat log (`Debug/logs/server.log`, `Debug/logs/cheat.log`) for `Speed hack`. There are no entries for Greeen.

Known gaps:
- **A jump to another region isn't handled.** The server sends a region change instead of a jump for that, and region changes are out of scope for this milestone. Keep the `/jump` in Greeen's region.
- **The speed check's own pull-back isn't tried in game.** It goes through the same `GamePlayer.MoveTo` and `SendPlayerJump` as `/jump`, and step 7 should find nothing to pull back.
- **Surroundings holds only players.** When ticket 06 adds NPCs to it, its forgetting has to cover them too.
- **Ticket 03's hand-over steps 2 and 6 can't work as written.** They have `come` said from about 1000 and 3000 units away, beyond the 512 a `/say` reaches, so Greeen never hears it. Say it from within 512 there too.
- **Not tested automatically:** the jump through the server's handlers and its speed check, and `HarnessClient.Unqueue` against its real queue. Steps 3, 4 and 7 cover them.

**2026-10-21 (Human, recorded by agent):** First in-game try on a server built from `aa5ddbf0a`. The server sent the jumps and the maximum speed updates, and the server log has no `Speed hack` entries. But after a restart of the Mind, `come` did nothing at all. Greeen had logged in while Vue was already in the world, and the server never sent him Vue's player create. Surroundings didn't know Vue, so Come ignored every `come`. The server sends a player's create to those around only when that player enters the world (`GamePlayer.AddToWorld`). A real client that enters after it asks for it, with a create request (0x7C, `PlayerCreationRequestHandler`), once it gets that player's positions. The Harness never asked. This has been a gap since ticket 02, and it never showed because the Human always logged in after Greeen.

Fixed in `b58888c39`: when the Harness gets a player position for a player it hasn't had the create of, it sends the create request with that player's session ID. It asks once, then again after 5 s if no create came. A delete or remove makes the player unknown again. The request's layout is what the handler reads from a 1.126 or later client: the session ID, low byte first. None of the captures has another player around, so it isn't checked against a real client yet. A `/agent capture` of the Human's client logging in next to someone would settle it. Server 250 passing in Release (this branch now has main's ticket 03 fix merged in), Mind 53, typecheck clean.

Re-check, with the server rebuilt from `b58888c39` and restarted: log Vue in first, then start Greeen's Mind. The Mind prints `playerCreate #<Vue's id> Vue` within a second or so of Vue's first `playerPosition` line. Then `/say come` from within 512 units works, and the steps above apply.

**2026-10-21 (Human, recorded by agent):** On a server built from `b58888c39`, with Vue logged in before Greeen, Greeen got Vue's player create 0.36 s after entering the world, in answer to his create request. The Human then captured his own client logging in next to Greeen. It sends the request with parameter 0x0000 and data `0400 0000` (Greeen's session ID 4, low byte first, then two zero bytes). The Harness now sends exactly that (`70d8d8bb2`), and the first 189 s of the capture are checked in as `login-next-to-agent.tsv` with a golden-bytes test. Server 252 passing in Release.

Step 5 holds. The Mind printed, for a `come` and a `/speed 0` on Greeen 0.4 s later:

    #42813  852.799 s  CT_Say CL_ChatWindow  Vue says, "come"
    Sending moveTo X:296158 Y:642934 Z:4852.
    #42814  852.832 s  moved  X:296105 Y:642820 Z:4850 heading 3809 speed 0.0 to X:296158 Y:642934 Z:4852
    #42818  853.032 s  moved  X:296107 Y:642825 Z:4850 heading 3809 speed 53.0 to X:296158 Y:642934 Z:4852
    #42820  853.166 s  maxSpeed 0% (0.0 per second)
    #42821  853.166 s  stopped (UnableToMove)  X:296111 Y:642833 Z:4850

The run ended where he was, in the tick the maximum speed arrived, with the unable-to-move reason. The server log has no `Speed hack` entries for Greeen. The jump mid-run (step 3) and the box are still to do.

**2026-10-21 (Human, recorded by agent):** Step 3 and the last box hold, on the same server and Mind. The Human said `come` from about 470 units, and pressed a macro for `/jump Greeen to 296493 643830 4856` while Greeen ran. The Mind printed:

    #51007  1015.740 s  CT_Say CL_ChatWindow  Vue says, "come"
    Sending moveTo X:296122 Y:643203 Z:4855.
    #51008  1015.766 s  moved  X:296111 Y:642833 Z:4850 heading 4076 speed 0.0 to X:296122 Y:643203 Z:4855
    #51013  1015.966 s  moved  X:296111 Y:642838 Z:4850 heading 4076 speed 53.0 to X:296122 Y:643203 Z:4855
    #51015  1016.166 s  moved  X:296112 Y:642854 Z:4850 heading 4076 speed 106.0 to X:296122 Y:643203 Z:4855
    #51018  1016.366 s  moved  X:296112 Y:642881 Z:4851 heading 4076 speed 159.0 to X:296122 Y:643203 Z:4855
    #51044  1016.499 s  jump #17737  X:296493 Y:643830 Z:4856 heading 4076
    #51045  1016.499 s  stopped (MovedByServer)  X:296493 Y:643830 Z:4856

The run ended in the tick of the jump, at the server's position, with the server-move reason. The macro fired twice; the second jump, 0.6 s later, found him standing and ended nothing. The server log has no `Speed hack` entries for Greeen. Every box holds.
