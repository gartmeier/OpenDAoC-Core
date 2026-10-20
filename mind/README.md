# Mind

The part of an Agent that lives outside the server and makes its decisions (see `CONTEXT.md` and ADRs 0003 to 0005). One Bun process per Agent. Its WebSocket connection to the server is the Agent's network connection: starting a Mind logs the Agent's Character in, and the Character leaves the world when the Mind quits or is lost.

## Running

1. On the server, set the server property `agent_mind_token` to a secret of your choice. `agent_mind_port` is the port (10310 unless changed, and a change needs a restart).
2. Copy `mind/.env.example` to `mind/.env` and set `MIND_TOKEN` to the same secret.
3. From the repository root: `bun mind/agent.ts <character>`. The Character must belong to an Agent Account.

The Mind prints every Percept to stdout and status lines to stderr. It's an echo Mind: when someone says something next to the Agent, the Agent says it back. Ctrl+C makes the Agent quit through the normal quit path, and the Mind exits once the server has disconnected it. A second Ctrl+C drops the connection instead, and the Character goes linkdead.

## Layout

- `agent.ts`: the entry point.
- `src/connection.ts`: the connection to the server. It knows nothing about what the Mind decides.
- `src/echo.ts`: what the echo Mind decides. Given a Percept and the Agent's Character, it returns a `say` Primitive or nothing.
- `src/protocol.ts`: the message types, generated from `GameServer/agents/MindProtocol.cs`. Don't edit it by hand. A server unit test fails when it's stale and names the command that regenerates it.

`bun test` runs the Mind's tests and `bun run typecheck` checks the types (after `bun install`).
