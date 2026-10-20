# 02: Tracer bullet: a Mind logs its Agent in and out

**What to build:** The Human starts `bun mind/agent.ts <char>`, and the Agent's Character enters the world through the Harness, driven by its Mind over a local WebSocket. Every call the server makes on the Agent's packet library scrolls past in the Mind's console as a bare Percept. Ctrl+C makes the Agent quit through the normal quit path, and the Mind exits once the server has disconnected it. Killing the Mind's process makes the Agent go linkdead under the server's normal rules. The Mind's connection is the Agent's network connection (ADR 0005).

On the server:
- An endpoint for Minds on its own listener, bound to localhost, with its port and shared token as server properties. With no token configured, every Mind is refused.
- `hello` runs milestone 1's login checks and starts the Harness client's world entry. `entered` follows once the Character is in the world, and `refused` (with a reason) if the checks or world entry fail.
- Every packet-library call produces one bare Percept (the call's name only), in order, with a sequence number and time. Property reads produce none. Percepts are formatted on the game loop and sent off it, through a queue that never waits.
- A `quit` Primitive sends the real `/quit`. Its `primitiveResult` says only whether it was queued (ADR 0004).
- The Harness sends pings and position updates only while a Mind is connected. A closed connection, or one that misses the WebSocket keep-alive, disconnects the Harness client as a closed socket would. When the Harness client disconnects, the server closes the WebSocket normally.
- The Primitive, Percept and protocol message types are C# types in the Harness area. A generator turns them into the Mind's checked-in TypeScript.

In the Mind (a Bun project in a new top-level folder):
- A connection module that knows nothing about what the Mind does. It connects, sends `hello`, waits for `entered` or `refused`, delivers Percepts to a handler and sends Primitives. On Ctrl+C it sends `quit` and waits for the server to close the connection.
- An entry point that takes the Character name from the command line and the server address and token from the environment (an untracked local env file), and prints every Percept to stdout.

See the spec: `.scratch/mind-connection/spec.md`.

**Blocked by:** None (can start immediately)

**Status:** ready-for-human

- [ ] Starting a Mind for an Agent's Character puts the Character in the world at its saved location, and the Mind reports that it entered
- [ ] A refused login (unknown Character, a Human's Account, no token configured) gets a clear reason in the Mind, which then exits
- [ ] The Mind prints a Percept for every call the server makes on the Agent's packet library, in order, with sequence and time
- [x] Percepts never block the game loop: no network I/O on the game loop thread, and a stalled Mind doesn't hold it up
- [ ] An idle Agent with a connected Mind stays online without link death or timeout
- [ ] Ctrl+C on the Mind makes the Agent quit through the normal quit path, and the Mind exits after the server disconnects it
- [x] Killing the Mind's process makes the Agent go linkdead, and it's gone after the link-death window
- [x] The Agent's Character is saved and its session released after either way out
- [ ] Shutting the server down with a Mind connected behaves as it does for an online Human
- [x] A unit test generates the TypeScript protocol types from the C# types and fails if the checked-in file differs, naming the command that regenerates it
- [x] The milestone 1 trace still records every call, one file per login

## Comments

**2026-10-20 (agent):** Built on branch `claude/mind-tracer-bullet-e8fcb5`. The remaining checks need the Human with a running server and the real client, so this is back with the Human.

What exists now:
- **Protocol.** `MindProtocol.cs` (in `GameServer/agents`) defines every message as a C# record with System.Text.Json polymorphism. Mind to server: `hello` (token, character) and `primitive` (id, primitive). Server to Mind: `refused` (reason, retry), `entered`, `percept` (sequence, elapsedMs, percept) and `primitiveResult` (id, queued, reason only when not queued). Primitives are discriminated by `name` (only `quit` so far), Percepts by `kind` (only `bare`, which carries `call`, the packet-library method's name). `replaced` is left to 05.
- **Codegen.** `MindProtocolTypeScript` reads the same JSON attributes and writes `mind/src/protocol.ts`, which is checked in. `UT_MindProtocolTypeScript` fails when the file is stale and names the command that regenerates it: `dotnet test Tests/Tests.csproj --filter UT_MindProtocolTypeScript -e REGENERATE_MIND_PROTOCOL=1`.
- **Percepts.** `PerceptProjection` turns a call into a bare Percept and a property read into nothing. `HarnessPacketLib` hands every call to the Harness client's `MindLink`, next to the trace, which is unchanged. The link numbers Percepts from 1 and stamps the milliseconds since the hello, on the game loop. It queues them on an unbounded channel that never waits, and a sender drains it off the game loop, as the trace does.
- **Endpoint.** `MindEndpoint` runs its own Kestrel listener on `ws://localhost:<agent_mind_port>/mind`, started and stopped by the server's started and stopped events. It's bound to localhost only and separate from the API host. Two new server properties in an `agents` category: `agent_mind_port` (default 10310, read at startup) and `agent_mind_token` (default empty, read on each hello). With no token every Mind is refused, and the server log says so at startup. WebSocket keep-alive pings go out every 10 s, and a Mind that doesn't answer within 20 s counts as lost. Bun answers pings by itself (checked).
- **Handshake.** `MindConnection` checks the first message is a `hello` with the right token (compared in constant time), then runs `AgentLogin.TryStart`, milestone 1's checks unchanged, on the game loop. A refusal goes back as `refused` and the server closes the connection normally. Otherwise the Harness client enters the world as in milestone 1, now with the link attached, so the world entry's calls arrive as Percepts too. The client sends `entered` on the game loop at the moment world entry completes, so it's in order with the Percepts. A world entry that fails or times out is sent as `refused`.
- **Primitives.** A `primitive` message is dispatched on the game loop. `quit` queues the same `/quit` command packet as `/agent logout`. The result says only whether it was queued; outside the world it isn't, with the reason "<char> isn't in the world."
- **Liveness (ADR 0005).** The Harness client watches its link on every tick, where a closed socket would be noticed. Once the Mind is lost it stops liveness and disconnects the way a closed socket does. While Playing, the server's `OnDisconnect` makes that a link death. During world entry it fails the entry instead, which gives the connection up only once the server isn't loading the Character any more. When the Harness client disconnects for any reason, the link closes the WebSocket normally after everything queued has been sent.
- **Shutdown.** The listener stops on the server's stopped event, before the server saves its players through `ClientService.SavePlayers`, Agents included, as for Humans. A connected Mind sees its connection drop, as a Human's client would.
- **The Mind** (`mind/`, a Bun project):
  - `src/connection.ts` connects, sends `hello`, and reports `entered` or `refused`. It hands Percepts to a handler and sends Primitives, returning their results. Ctrl+C in the world sends `quit` and waits for the server to close the connection; a second Ctrl+C drops it, which leaves the Character linkdead. Ctrl+C before `entered` drops the connection. `run()` ends as `left` (a normal close after `entered`), `refused` or `lost`.
  - `agent.ts` takes the Character from the command line, and `MIND_SERVER_URL` and `MIND_TOKEN` from `mind/.env` (see `mind/.env.example`; git ignores `.env`). Bun only loads `.env` from the working directory, so the entry point loads `mind/.env` itself. It prints `#<sequence>  <seconds> s  <call>` for every Percept on stdout, and status lines on stderr. It exits 0 when the server disconnected the Agent, 1 otherwise.
  - `mind/README.md` says how to run it.
- `/agent login` and `/agent soak` still work: their Harness clients have no link and keep milestone 1's unconditional liveness. Ticket 03 removes them.
- Tests:
  - Server, 139 passing in all. New are `UT_PerceptProjection`, `UT_MindLink` (Percepts in call order with sequence and time; sending never waits and happens off the caller's thread; close after the queue drains; refusal; loss; a failing send), `UT_MindProtocol` (the JSON both ways, and unreadable messages) and `UT_MindProtocolTypeScript`. `UT_MindConnection` covers the refusals before the game loop (no token, wrong token, first message not a hello, a Mind closing before its hello) over a real WebSocket. `UT_MindEndpoint` starts the real listener, gets refused without a token and closed normally, and gets 400 for a plain HTTP request.
  - Mind, 5 passing in `bun test`: hello first, Percepts in order, `left` on a normal close, `refused` with the reason, Ctrl+C sending `quit` and waiting, a Primitive's result, and `lost`. `bun run typecheck` is clean.

For the Human, when the server runs this build:
1. Start the server once so it creates the two properties. Set `agent_mind_token` to a secret in the `serverproperty` table, then `/serverproperties refresh` (or restart). Copy `mind/.env.example` to `mind/.env` and set `MIND_TOKEN` to the same secret. At startup the server log should say "Minds can connect at ws://localhost:10310/mind".
2. From the repository root, `bun mind/agent.ts greeen`. Greeen should appear at his saved location, Percepts numbered from `#1` should scroll past, and stderr should say "Greeen is in the world." A new trace file should appear under `agent-traces`, with as many calls as the Mind printed Percepts.
3. Leave him for at least 3 minutes. He should stay visible and not linkdead, and the server log should show no "Position update timeout" or "Hard timeout".
4. Ctrl+C. Greeen should sit through the quit delay and vanish, and the Mind should print "The server has disconnected Greeen." and exit. The server log should show "agent1 just disconnected." with no link death. Start it again: he should be back where he was.
5. Start the Mind again, then kill its process (close its terminal, or `Stop-Process` on `bun`). You should see Greeen go linkdead, and he should be gone about 60 s later. The log should show the link death, then the quit and save. Start the Mind again after that: he should come back.
6. Refusals, each of which should print a clear reason and exit:
   - `bun mind/agent.ts nobody`.
   - A Character on a Human's Account.
   - A wrong `MIND_TOKEN`.
   - An empty `agent_mind_token` on the server (refresh after clearing it).
7. With a Mind connected, `/shutdown 10`. The server should save Greeen with the other players and stop without hanging. The Mind should report a lost connection and exit. After the restart, Greeen should log in where he was.
8. `/agent login greeen` and `/agent logout greeen` should still work as in milestone 1 until 03 retires them.

Known gaps:
- **Ctrl+C on Windows isn't verified.** The Mind handles `SIGINT` through `process.on`, which Bun maps to the console's Ctrl+C. Step 4 is the first real check.
- **Left to 05:**
  - A Mind for an Agent that's already online, or still linkdead, is refused without retry.
  - After the hello, messages that aren't Primitives are ignored rather than refused.
  - A Mind that never sends its hello keeps its connection open.
  - Anything unreadable as the first message gets one generic refusal.
- **A stalled Mind grows its queue.** The queue is unbounded, the risk the spec accepts.
- **A refused quit leaves the Mind waiting.** If the server refuses the quit (dead, mounted, in combat), Ctrl+C just waits; a second Ctrl+C drops the connection. The Mind only learns why once the message call is projected (04).
- **`agent_mind_port` needs a restart** to take effect.

**2026-10-20 (Human, recorded by agent from the server log and traces):** Ran the checks on this build (server started 08:52:55, token set and refreshed at 08:54:10). Five logins, each with its own trace:
- **Entering the world.** Every Mind got Greeen into the world within about a second (for example 08:55:06 → 08:55:07, Region Hibernia).
- **Ctrl+C.** The run at 09:01:51 quit cleanly. The Mind sent `/quit`, Greeen sat through the 20 s quit delay, the server saved him and sent him to the character screen, and the client disconnected without a link death. The server closed the Mind's connection after that. The other three runs (08:55:06, 08:58:46, 09:00:29) sent the quit too, but their connection dropped 0.3 to 7 s into the delay, so they went linkdead. Each was removed and saved 60 s later.
- **Why those runs dropped.** In the terminal panel, PowerShell exits when a program it's running survives Ctrl+C. Bun and Node do the same, with any SIGINT handler. The tab then closes and kills the Mind. Reproduced with a fake server, and fixed: the Mind now reads Ctrl+C as a key, with stdin in raw mode, so the console never raises the signal (`mind/src/ctrl-c.ts`; SIGINT is still the fallback without a terminal). Against the fake server, Ctrl+C now sends `quit`, the Mind waits for the server's close 5 s later and exits, and the shell stays open.
- **Link death.** It works as for a Human: "Linkdead player Greeen(agent1) will quit in 60 seconds", then removed, saved and disconnected 60 s later.
- **Refusals.** An unknown Character (`greeex`) was refused with "No Character is named 'greeex'." Minds started while Greeen was still linkdead were refused with "Greeen is already online as an Agent.", without retry, which is 05's.
- **Saving and session.** Both ways out end with "Your character has been saved.", and each later login got in, so the session was released.
- **Trace.** One file per login, recording every call: 6950 calls for the 2 min 20 s session.

Still to check, with the Ctrl+C fix:
1. Ctrl+C in the terminal panel: Greeen should quit after the delay without going linkdead, the Mind should print "The server has disconnected Greeen." and exit, and the tab should stay open.
2. Idle for at least 3 minutes. The longest session so far was 2 min 20 s, without a timeout.
3. Greeen appears at his saved location, and the Mind prints numbered Percepts and "Greeen is in the world." (not visible in the server log).
4. Refusals for a Character on a Human's Account, a wrong token, and an empty `agent_mind_token`.
5. `/shutdown 10` with a Mind connected.
6. `/agent login` and `/agent logout` still work.
