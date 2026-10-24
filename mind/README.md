# Mind

The part of an Agent that lives outside the server and makes its decisions (see `CONTEXT.md` and ADRs 0003 to 0005). One Bun process per Agent. Its WebSocket connection to the server is the Agent's network connection: starting a Mind logs the Agent's Character in, and the Character leaves the world when the Mind quits or is lost.

A coding agent such as Claude Code plays through it (ADR 0009). The Mind is a stdio MCP server, which the coding agent starts, with one tool, `execute_code`, that runs a TypeScript snippet against the Agent. What the coding agent reads, and how to run it, is in `agents/`.

## Running

1. On the server, set the server property `agent_mind_token` to a secret of your choice. `agent_mind_port` is the port (10310 unless changed, and a change needs a restart).
2. Copy `mind/.env.example` to `mind/.env` and set `MIND_TOKEN` to the same secret.
3. Create the Agent's Account (its name starts with `agent`) and Character with the real client.
4. Copy `agents/_template` to `agents/<name>` and put the Character's name in place of `CHARACTER` in its `.mcp.json`.
5. Start Claude Code in `agents/<name>` and tell it to play. It starts `bun ../../mind/mcp.ts <character>`, which logs the Character in.

The Wiki in `agents/wiki` is written by `/agent wiki <realm> <folder>` on the server, for example from its console. Run it again when the world's data changes.

While it runs:

- Each `execute_code` call runs a snippet: one at a time, aborting any still running, for 2 minutes unless the call says otherwise (at most 25, since Claude Code gives up on an MCP call after 30). It returns the snippet's console output and result or error, a summary of the Agent's Self, Surroundings and Inventory, and the chat since the last call.
- A Human saying `stop` next to the Agent aborts the running snippet, and the Agent stands still with attack off.
- The Journal goes to `journal/` in the Agent's folder, one file per session: every Percept, every Primitive and every snippet with how it ended.
- When Claude Code exits, the Mind stops the snippet and quits the Character through the normal quit path. If the connection ends while the session goes on, the next call logs the Character in again.

Who gets in (ADR 0005):
- Starting a second Mind for an Agent that's online with a live Mind hands it the connection. The Character stays in the world, and the first Mind won't log it in again.
- A Mind for an Agent that's still linkdead is refused, and tries again every 5 s until the server has removed the Character (at most the 60 s link-death window), then logs in.
- Every other refusal (a wrong token, a Character on a Human's Account, the Account logged in with the real client) is what `execute_code` answers with.

## Layout

- `mcp.ts`: the entry point, the MCP server.
- `src/mind.ts`: the `execute_code` tool: it compiles and runs snippets on the Runner, aborts them, and says how each went. It also hears `stop` and writes the Journal.
- `src/snippet-globals.ts`: what every snippet has at hand besides `agent`.
- `src/summary.ts` and `src/journal.ts`: the lines a result ends with, and the Journal's lines.
- `src/connection.ts`: the connection to the server. It knows nothing about what the Mind decides.
- `src/tactic.ts`: what every Tactic gets (ADR 0008): the Agent's Primitives, its Self, Surroundings and Inventory, and `waitFor`, which waits on them with a timeout. Stopping the Agent ends every Tactic running on it.
- `src/tactics/`: one Tactic each: `kill`, `rest`, `train`, `runTo`, `equip` and `trade` (`talkTo`, `buy` and `sell`), the porcelain, mechanical and without judgement.
- `src/runner.ts`: runs one top-level Tactic at a time, and leaves the Agent standing still with attack off when it stops one.
- `src/self.ts`: what the Agent believes about its own Character, built only from Percepts: where it is, which way it faces, how fast it may run, one movement status (standing, moving to a target, arrived, or stopped with a reason), its health and whether it sits or is dead, its level, experience, spec points and skills.
- `src/surroundings.ts`: what the Agent believes is around it, built only from Percepts: the players and NPCs it knows of, in one collection by object ID, with their names and where they last were, and for NPCs their level, realm, health and whom they attack. It forgets what's out of view when the Agent's Character moves, never by time.
- `src/inventory.ts`: what the Agent believes it carries, built only from Percepts: the item in each equipment and backpack slot, its money in copper, its weight against its capacity, the last merchant window and the last examined text. Equipment slots have names (`rightHand`, `torso`) as well as the client's numbers. It also tracks which merchant the client talks to, from the Primitives sent, because buying and selling go to the client's target.
- `src/test-agent.ts` and `src/test-percepts.ts`: an Agent on a clock the test moves on by hand, and Percepts to feed it, for Tactic tests.
- `src/protocol.ts`: the message types, generated from `GameServer/agents/MindProtocol.cs`. Don't edit it by hand. A server unit test fails when it's stale and names the command that regenerates it.

`bun test` runs the Mind's tests, the check that every snippet in `agents/learnings` typechecks among them, and `bun run typecheck` checks the types (after `bun install`).
