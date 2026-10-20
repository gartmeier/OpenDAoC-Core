// Logs an Agent's Character in and prints every Percept it gets, until the Agent leaves the world. Ctrl+C quits.
// Usage: bun mind/agent.ts <character>
// The server address and token come from mind/.env (see mind/.env.example).

import { Connection } from "./src/connection.ts";
import { loadEnvFile } from "./src/env.ts";
import type { PerceptMessage } from "./src/protocol.ts";

const character = Bun.argv[2];

if (!character) {
  console.error("Usage: bun mind/agent.ts <character>");
  process.exit(2);
}

await loadEnvFile(new URL(".env", import.meta.url));
const url = process.env.MIND_SERVER_URL || "ws://localhost:10310/mind";
const token = process.env.MIND_TOKEN;

if (!token) {
  console.error("MIND_TOKEN isn't set. Copy mind/.env.example to mind/.env and set it to the server property agent_mind_token.");
  process.exit(2);
}

// Status lines go to stderr, so stdout holds only Percepts.
const connection = new Connection({ url, token, character, log: (line) => console.error(line) }, {
  onPercept: (message) => console.log(formatPercept(message)),
});

const ending = await connection.run();
process.exit(ending.kind === "left" ? 0 : 1);

function formatPercept({ sequence, elapsedMs, percept }: PerceptMessage): string {
  const details = percept.kind === "bare" ? percept.call : JSON.stringify(percept);
  return `#${sequence}\t${(elapsedMs / 1000).toFixed(3)} s\t${details}`;
}
