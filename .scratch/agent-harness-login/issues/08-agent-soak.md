# 08: Soak: `/agent soak <char> <n>`

**What to build:** An admin runs `/agent soak <char> <n>` and the Agent does n full login/logout cycles one after another. Each cycle is checked to reach in-world play and then fully disconnect. After the run, the online client count and session allocation are compared against their values before the run. The command stops at the first failing cycle and reports pass or fail, the failing cycle's number and the reason. This is milestone 1's repeatable acceptance test. See the spec.

**Blocked by:** 04, 05

**Status:** resolved

- [x] `/agent soak <char> <n>` is admin-only and runs cycles sequentially, never in parallel
- [x] A cycle that doesn't reach in-world play, or doesn't fully disconnect, counts as a failure
- [x] Leaked clients or sessions after the run are reported as a failure
- [x] The first failure stops the run and reports the cycle number and reason
- [x] `/agent soak <char> 50` passes with a clean server log (milestone 1 acceptance)

## Comments

**2026-10-03 (agent, from 02):** Session allocation can't be read directly. `SessionIdAllocator` is private to `BaseServer` and has no count. To detect leaks, either compare `ClientService.Instance.ClientCount` and check that the Agent's old session ID no longer resolves through `GetClientBySessionId`, or add a small read-only count seam in its own commit.

**2026-10-04 (agent):** Built, and `/agent soak greeen 50` passed from the server console. The in-game run needs the Human, so this is back with the Human.

What exists now:
- **`/agent soak <char> <n>`** (admin only, like every `/agent` subcommand) runs n login/logout cycles one after another. Each cycle logs the Character in the way `/agent login` does and quits it straight away the way `/agent logout` does. Each clean cycle is reported as "Cycle k of n was clean.", and a cycle takes about as long as the quit delay. Only one soak runs at a time. A second one is refused while the first is still going.
- **A cycle is clean** if:
  - the Character reached the world, and
  - the server then disconnected its Harness client within the 90 s quit limit, and
  - the client never went linkdead (soft or hard), and
  - the Character isn't left in the world.

  Anything else fails the cycle with a reason, for example a refused login, "didn't reach the world within 30 s", "hasn't quit within 90 s" or "went linkdead". Most reasons point at the cycle's trace.
- **The first unclean cycle stops the run.** The command reports "Soak of <char> failed at cycle k of n." with the reason. A failed cycle leaves its Harness client to clean up after itself, as `/agent login` and `/agent logout` do.
- **Leaks.** After a clean run, the server's client count and session count are compared with their values before the run. Any difference fails it ("The server has 1 more session than before the run…").
  - Session allocation is read through a new read-only count on the session ID allocator (`BaseServer.SessionCount`), in its own commit. The spec's list of core seams now includes it.
  - The counts are server-wide, so a Human connecting or disconnecting during a run shows up as a leak. The message says so.
- **The start and result of a run go to the server log** (`Soak of <char> started/passed/failed`), so the window to check for a clean log is easy to find.
- **Tests.** `UT_Soak` covers the runner with fake cycles: cycles never overlap, the first unclean or throwing cycle stops the run and is reported with its number, and leaks fail a clean run. `UT_SessionIdAllocator` covers the count.

Changed along the way:
- `/agent login`'s checks moved to `AgentLogin` so the soak makes the same ones. The already-logged-in check now ignores a client that has disconnected. Such a client stays in the client service's view until the next tick, so a login straight after a logout could be refused.
- `/agent` typed at the server console now runs on the game loop, as an in-game admin's does. Before, its awaits resumed on the thread pool, so a soak's next login ran while the game loop was still finishing the previous disconnect.

Run on this build from the server console (2026-10-04, `Debug\CoreServer.exe`):
- `/agent soak greeen 3` passed. `/agent soak nosuchname 2` failed at cycle 1 with "No Character is named 'nosuchname'.". `/agent soak greeen 0` showed the syntax.
- `/agent soak greeen 50` passed: 16:58:18 → 17:15:50, 50 × "agent1 just disconnected.", with no link death, hard timeout, error or exception in the log. The only warning was one "Long TimerService.Tick for … QuitTimer … 43ms" on the first cycle's quit, the Character save. It also appeared on the first cycle of the 3-cycle run.
- An earlier 50-cycle attempt stopped after 27 clean cycles because Windows went to sleep at 16:08 (Kernel-Power 42 in the System log). That wasn't a server problem.

For the Human, when the server runs this build:
1. From your admin Character, `/agent soak greeen 50`. It takes about 17 minutes. It should end with "Soak of greeen passed: 50 clean cycles…", and the server log between its "started" and "passed" lines should have no "Linkdead player", "Hard timeout" or errors.
2. While it runs, Greeen should keep appearing next to his saved spot and vanishing again. Try `/agent soak greeen 2` meanwhile: it should be refused as already going.
3. With a privilege level 1 Account, `/agent soak greeen 1` should be refused like any other admin command.

Known gaps:
- **The first cycle after a server start takes about 60 s.** A Character that has never fought counts as having fought at game loop tick 0, and the game loop clock starts with the server. So for the first 40 s after a start, `/quit` gets the full 60 s combat quit delay instead of 20 s. This is the server's behaviour, not the Harness's, and it stays within the 90 s limit.
- **A leak is only found after the whole run**, so it has no cycle number. The spec asks for a before/after comparison. Per-cycle counting would also be thrown off by Humans connecting during the run.
- **No upper limit on n.** Each cycle takes about 20 s.

**2026-10-17 (Human, recorded by agent):** Ran `/agent soak greeen 50` from the admin Character and stopped it after 21 cycles, which the Human judged enough. The server log agrees and counts slightly more:
- The run started at 00:13:13.
- 23 "agent1 just disconnected." lines follow, about 20 s apart, before the log ends at 00:21:00 with cycle 24 entering the world.
- No link death, hard timeout, error or exception.
- The only warnings are long-tick lines:
  - one 39 ms for handling the `/agent soak` command itself;
  - two `QuitTimer` ticks, 70 ms and 37 ms (Character saves).

Together with the 50-cycle console run above, all acceptance items are met. Checks 2 (a second soak refused while one runs) and 3 (privilege level 1 refused) weren't reported. Check 3 uses the same dispatcher check the Human ran for 07.
