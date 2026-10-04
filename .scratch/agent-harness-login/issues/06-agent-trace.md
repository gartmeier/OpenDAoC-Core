# 06: Trace of everything the server tells the Agent

**What to build:** The Harness packet library records every call the server makes to tell the Agent's client something, per Agent, with order and time, into a trace the Human can read after a session. Recording never blocks or slows the game loop (calls arrive on the game loop thread, and writing happens off it). The trace is the inventory of future Percepts. It's a research artifact, not the Journal. See the spec and ADR 0001.

**Blocked by:** 03

**Status:** resolved

- [x] After a login/idle/logout session, a per-Agent trace exists listing each server-to-client call in order with a timestamp and its salient arguments
- [x] A trace of one full world entry is readable and shows the sequence the server sends
- [x] Recording does no file or network I/O on the game loop thread
- [x] Several Agents' traces don't interleave into one another

## Comments

**2026-10-03 (agent):** Built. The remaining checks need the Human with a running server, so this is back with the Human.

What exists now:
- Every `/agent login` starts a trace in its own file, `agent-traces/<char>-<yyyyMMdd-HHmmss-fff>.tsv` under the server's root directory. `/agent login` prints the path. The trace closes when the Harness client disconnects (logout, timeout, link-death quit), so one file covers one login/idle/logout session. Several Agents, or several soak cycles, never share a file.
- `HarnessPacketLib` hands every call the server makes to `AgentTrace.Record` before doing anything else with it. Property reads (the bow and dual-wield animation IDs) are left out, because they tell the client nothing.
- Each line has: sequence number, milliseconds since the trace started, client state, then the call with named arguments. Two comment lines head the file: the Character and the UTC start time, then the column names.
  - Example: `SendMessage(msg: "...", type: CT_System, loc: CL_SystemWindow)`.
  - Strings are quoted and escaped, so every call stays on one line.
  - Game objects show as type, name and object ID, e.g. `GamePlayer "Greeen" #12`. Other objects with a `Name` show as type and name. Anything else shows as its type.
  - Collections list their first 10 items, then how many more there are.
- Calls are formatted where they're made, on the game loop, because their arguments are live game objects. The finished line goes into an unbounded channel, so recording never waits. A background task opens the file (directory creation included), writes the lines, and flushes after each batch. If opening or writing fails, it logs one error and the trace stops recording.
- `UT_AgentTrace` covers recording, not content: one line per call in order, one line per call even with newlines in the arguments, objects named and collection items listed, no waiting on a stalled writer and no writes on the recording thread, nothing after disposal, a writer that can't be opened, and two traces kept apart.

For the Human, when the server runs this build:
1. `/agent login <char>`, let it idle a minute, then `/agent logout <char>`. Open the file `/agent login` named. It should start with the world-entry sequence: character overview and select, then the burst after world init (player init, regions, inventory, skills, objects nearby). It should go on with ping replies and whatever the server sends while idle, and end with the quit messages and `SendPlayerQuit`.
2. Log in two Agents (or the same one twice, a session apart). Each login should get its own file.

Known gaps:
- **The channel is unbounded.** If the disk stalls, queued lines grow in memory until it recovers. One Agent's call volume makes this a non-issue for this milestone; revisit at 08 (multiple Agents).
- **Calls made after the client disconnects aren't traced.** The trace closes on the Disconnected event, after the quit path has run.

**2026-10-04 (Human, recorded by agent):** Ran two login/idle/logout sessions with Greeen. Each got its own file under `Debug/agent-traces`:
- `Greeen-20261003-164105-920.tsv`: 62 s, 3,320 calls.
- `Greeen-20261004-100722-547.tsv`: 315 s, 15,633 calls.

Both read the same way, in order, with elapsed times that never go backwards:
- **Login and world entry.** Login granted, session ID, character overview, regions, a ping reply, then login granted and session ID again. Then about 50 calls while the client is in WorldEnter, within 20 ms: equip messages, speed and encumbrance, position and object ID, equipment and inventory items, skills, money, stats, resists, icons, quest list, points, guild ID, keep info (11 keeps), weather, time, server news, and player init finished. Then Playing: friends, game open reply, disabled skills, and a burst of NPC and object creates for the surroundings, ending with "(Region) You have entered Connla."
- **Idle.** A ping reply every 5 s. Object updates for nearby NPCs make up about 90% of all lines (2,906 of 3,320, and 15,036 of 15,633).
- **Logout.** Quit statistics, "You sit down", the 20 s countdown, then `SendPlayerQuit(totalOut: False)` exactly once, the Character's own object removed, "Your character has been saved.", and the character screen.

No formatting errors appeared in either file. One weak spot: `SendDisableSkill` showed its items as `ValueTuple<Skill, Int32>`. Tuples now list their elements, e.g. `(Specialization "Slash", 30000)`, with a test.

All acceptance items are met. Notes for later Percept work:
- Object updates dominate and carry no position yet (only type, name and object ID).
- Calls that `IPacketLib` takes as `ReadOnlySpan` never reached the trace in these sessions.
