# Mind

The part of an Agent that lives outside the server and makes its decisions (see `CONTEXT.md` and ADRs 0003 to 0005). One Bun process per Agent. Its WebSocket connection to the server is the Agent's network connection: starting a Mind logs the Agent's Character in, and the Character leaves the world when the Mind quits or is lost.

## Running

1. On the server, set the server property `agent_mind_token` to a secret of your choice. `agent_mind_port` is the port (10310 unless changed, and a change needs a restart).
2. Copy `mind/.env.example` to `mind/.env` and set `MIND_TOKEN` to the same secret.
3. From the repository root: `bun mind/agent.ts <character>`. The Character must belong to an Agent Account.

The Mind prints every Percept to stdout and status lines to stderr. It's a Come Mind: when someone the Agent can see says `come` next to it, the Agent runs in a straight line to 100 units short of where they stood and turns to face them, and `stop` stops it. From within 100 units it only turns. The Mind prints `moved` lines while it runs, then `arrived`, or `stopped` with the reason. Ctrl+C stops the Agent and makes it quit through the normal quit path (the server refuses `/quit` while the Character moves), and the Mind exits once the server has disconnected it. A second Ctrl+C drops the connection instead, and the Character goes linkdead.

There's no terrain yet: runs go in a straight line, through walls and trees, and Z goes linearly from start to target, so test on flat open ground.

Who gets in (ADR 0005):
- Starting a second Mind for an Agent that's online with a live Mind hands it the connection. The Character stays in the world, and the first Mind says it was replaced and exits.
- A Mind for an Agent that's still linkdead is refused, and tries again every 5 s until the server has removed the Character (at most the 60 s link-death window), then logs in. Ctrl+C stops retrying.
- Every other refusal (a wrong token, a Character on a Human's Account, the Account logged in with the real client) makes the Mind print the reason and exit.

The Mind exits with 0 when the server disconnected its Agent or another Mind took over, and 1 otherwise.

## Layout

- `agent.ts`: the entry point.
- `src/connection.ts`: the connection to the server. It knows nothing about what the Mind decides.
- `src/come.ts`: what the Come Mind decides. Given a Percept, Self and Surroundings, it returns a `moveTo`, `face` or `stop` Primitive, or nothing. It's the only part about coming.
- `src/self.ts`: what the Agent believes about its own Character, built only from Percepts: where it is, which way it faces, how fast it may run, and one movement status (standing, moving to a target, arrived, or stopped with a reason).
- `src/surroundings.ts`: what the Agent believes is around it, built only from Percepts: the players and NPCs it knows of, in one collection by object ID, with their names and where they last were. It forgets what's out of view when the Agent's Character moves, never by time.
- `src/protocol.ts`: the message types, generated from `GameServer/agents/MindProtocol.cs`. Don't edit it by hand. A server unit test fails when it's stale and names the command that regenerates it.

`bun test` runs the Mind's tests and `bun run typecheck` checks the types (after `bun install`).
