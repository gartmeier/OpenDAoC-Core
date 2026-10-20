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

**Status:** ready-for-agent

- [ ] Starting a Mind for an Agent's Character puts the Character in the world at its saved location, and the Mind reports that it entered
- [ ] A refused login (unknown Character, a Human's Account, no token configured) gets a clear reason in the Mind, which then exits
- [ ] The Mind prints a Percept for every call the server makes on the Agent's packet library, in order, with sequence and time
- [ ] Percepts never block the game loop: no network I/O on the game loop thread, and a stalled Mind doesn't hold it up
- [ ] An idle Agent with a connected Mind stays online without link death or timeout
- [ ] Ctrl+C on the Mind makes the Agent quit through the normal quit path, and the Mind exits after the server disconnects it
- [ ] Killing the Mind's process makes the Agent go linkdead, and it's gone after the link-death window
- [ ] The Agent's Character is saved and its session released after either way out
- [ ] Shutting the server down with a Mind connected behaves as it does for an online Human
- [ ] A unit test generates the TypeScript protocol types from the C# types and fails if the checked-in file differs, naming the command that regenerates it
- [ ] The milestone 1 trace still records every call, one file per login
