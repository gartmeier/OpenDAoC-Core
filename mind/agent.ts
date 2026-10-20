// The echo Mind: logs an Agent's Character in, prints every Percept it gets and says back what others say next to it, until the Agent leaves the world. Ctrl+C quits.
// Every Percept also goes to the Agent's Surroundings, which the printed lines name players from.
// Usage: bun mind/agent.ts <character>
// The server address and token come from mind/.env (see mind/.env.example).

import { Connection } from "./src/connection.ts";
import { echo } from "./src/echo.ts";
import { loadEnvFile } from "./src/env.ts";
import type { Percept, PerceptMessage } from "./src/protocol.ts";
import { type Position, Surroundings } from "./src/surroundings.ts";

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

const surroundings = new Surroundings();

// Status lines go to stderr, so stdout holds only Percepts.
const connection = new Connection({ url, token, character, log: (line) => console.error(line) }, {
  onPercept: (message, connection) => {
    surroundings.perceive(message);
    console.log(formatPercept(message));
    const reply = echo(message.percept, character);

    if (!reply)
      return;

    console.error(`Saying "${reply.text}".`);
    void connection.send(reply).then((result) => {
      if (!result.queued)
        console.error(`The say wasn't sent: ${result.reason}`);
    });
  },
});

const ending = await connection.run();
// Leaving the world and being taken over by another Mind are both the Agent's normal ways out.
process.exit(ending.kind === "left" || ending.kind === "replaced" ? 0 : 1);

function formatPercept({ sequence, elapsedMs, percept }: PerceptMessage): string {
  return `#${sequence}\t${(elapsedMs / 1000).toFixed(3)} s\t${describe(percept)}`;
}

function describe(percept: Percept): string {
  switch (percept.kind) {
    case "bare":
      return percept.call;
    case "message":
      return `${percept.chatType} ${percept.chatLocation}\t${percept.text}`;
    case "barePacket":
      return `${percept.call} 0x${percept.code.toString(16).toUpperCase().padStart(2, "0")}`;
    case "playerCreate":
      return `playerCreate #${percept.objectId} ${percept.name}\t${at(percept)} heading ${percept.heading}`;
    case "playerPosition":
      return `playerPosition #${percept.objectId} ${surroundings.player(percept.objectId)?.name ?? "(unknown)"}\t${at(percept)} heading ${percept.heading} speed ${percept.speed} zone ${percept.zoneId}`;
    case "objectDelete":
      return `objectDelete #${percept.objectId}`;
  }
}

// As `/gloc` prints a position.
function at({ x, y, z }: Position): string {
  return `X:${x} Y:${y} Z:${z}`;
}
