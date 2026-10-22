# Mind

The part of an Agent that lives outside the server and makes its decisions (see `CONTEXT.md` and ADRs 0003 to 0005). One Bun process per Agent. Its WebSocket connection to the server is the Agent's network connection: starting a Mind logs the Agent's Character in, and the Character leaves the world when the Mind quits or is lost.

## Running

1. On the server, set the server property `agent_mind_token` to a secret of your choice. `agent_mind_port` is the port (10310 unless changed, and a change needs a restart).
2. Copy `mind/.env.example` to `mind/.env` and set `MIND_TOKEN` to the same secret.
3. From the repository root: `bun mind/agent.ts <character>`. The Character must belong to an Agent Account.

The Mind prints every Percept to stdout, and status lines, every Primitive it sends and how each Tactic ended to stderr. Say these next to the Agent:

- `come`: the Agent runs to where the speaker is, along a route on the navmesh, stops 100 units short of them and turns to face them. The Agent must be able to see the speaker.
- `grind`: the Agent levels to 5 around where it stands, unattended. It fights hostile Mobs within a level of its own and within 1500 units, fights back first when attacked, rests below 50% health, puts every spec point into Blades with `/train`, and releases and runs back when it dies. At level 5 it says so, and the Mind prints a summary: time taken, kills, deaths, rests and Tactic failures.
- `stop`: whatever the Agent is doing stops, and it stands still with attack off.

A new `come` or `grind` replaces whatever the Agent was doing. Ctrl+C does what `stop` does, then makes the Agent quit through the normal quit path (the server refuses `/quit` while the Character moves), and the Mind exits once the server has disconnected it. A second Ctrl+C drops the connection instead, and the Character goes linkdead.

Who gets in (ADR 0005):
- Starting a second Mind for an Agent that's online with a live Mind hands it the connection. The Character stays in the world, and the first Mind says it was replaced and exits.
- A Mind for an Agent that's still linkdead is refused, and tries again every 5 s until the server has removed the Character (at most the 60 s link-death window), then logs in. Ctrl+C stops retrying.
- Every other refusal (a wrong token, a Character on a Human's Account, the Account logged in with the real client) makes the Mind print the reason and exit.

The Mind exits with 0 when the server disconnected its Agent or another Mind took over, and 1 otherwise.

## Layout

- `agent.ts`: the entry point.
- `src/connection.ts`: the connection to the server. It knows nothing about what the Mind decides.
- `src/tactic.ts`: what every Tactic gets (ADR 0008): the Agent's Primitives, its Self and Surroundings, and `waitFor`, which waits on them with a timeout. Stopping the Agent ends every Tactic running on it.
- `src/tactics/`: one Tactic each. `kill`, `rest`, `train`, `come` and `runTo` are the porcelain, mechanical and without judgement. `grind` runs them with simple stand-in rules, for LLM Planning to replace.
- `src/runner.ts`: runs one top-level Tactic at a time, and leaves the Agent standing still with attack off when it stops one.
- `src/commands.ts`: reads `come`, `grind` and `stop` from say lines.
- `src/self.ts`: what the Agent believes about its own Character, built only from Percepts: where it is, which way it faces, how fast it may run, one movement status (standing, moving to a target, arrived, or stopped with a reason), its health and whether it sits or is dead, its level, experience, spec points and skills.
- `src/surroundings.ts`: what the Agent believes is around it, built only from Percepts: the players and NPCs it knows of, in one collection by object ID, with their names and where they last were, and for NPCs their level, realm, health and whom they attack. It forgets what's out of view when the Agent's Character moves, never by time.
- `src/test-agent.ts` and `src/test-percepts.ts`: an Agent on a clock the test moves on by hand, and Percepts to feed it, for Tactic tests.
- `src/protocol.ts`: the message types, generated from `GameServer/agents/MindProtocol.cs`. Don't edit it by hand. A server unit test fails when it's stale and names the command that regenerates it.

`bun test` runs the Mind's tests and `bun run typecheck` checks the types (after `bun install`).
