// The Come Mind: logs an Agent's Character in, prints every Percept it gets, and runs to whoever says `come` next to it and turns to face them, until the Agent leaves the world. `stop` stops it. Ctrl+C stops it and quits.
// Every Percept also goes to the Agent's Self and Surroundings, which the printed lines name players from.
// Usage: bun mind/agent.ts <character>
// The server address and token come from mind/.env (see mind/.env.example).

import { Come } from "./src/come.ts";
import { Connection } from "./src/connection.ts";
import { loadEnvFile } from "./src/env.ts";
import type { Percept, PerceptMessage, Primitive } from "./src/protocol.ts";
import { Self } from "./src/self.ts";
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

// Not `self`, which is a JavaScript global.
const agentSelf = new Self();
const surroundings = new Surroundings();
const come = new Come(character);

// Status lines go to stderr, so stdout holds only Percepts.
const connection = new Connection({ url, token, character, log: (line) => console.error(line) }, {
  onPercept: (message, connection) => {
    agentSelf.perceive(message);
    surroundings.perceive(message);
    console.log(formatPercept(message));
    const primitive = come.decide(message.percept, agentSelf, surroundings);

    if (!primitive)
      return;

    console.error(`Sending ${describePrimitive(primitive)}.`);
    void connection.send(primitive).then((result) => {
      if (!result.queued)
        console.error(`The ${primitive.name} wasn't sent: ${result.reason}`);
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
    case "objectRemove":
      return `objectRemove #${percept.objectId} type ${percept.objectType}`;
    case "ownPosition":
      return `ownPosition #${percept.objectId}	${at(percept)} heading ${percept.heading} region ${percept.region}`;
    case "maxSpeed":
      return `maxSpeed ${percept.maxSpeedPercent}% (${agentSelf.maxSpeed.toFixed(1)} per second)`;
    case "moved":
      return `moved	${at(percept)} heading ${percept.heading} speed ${percept.speed.toFixed(1)} to ${at({ x: percept.targetX, y: percept.targetY, z: percept.targetZ })}`;
    case "arrived":
      return `arrived	${at(percept)}`;
    case "stopped":
      return `stopped (${percept.reason})	${at(percept)}`;
  }
}

function describePrimitive(primitive: Primitive): string {
  switch (primitive.name) {
    case "moveTo":
      return `moveTo ${at(primitive)}`;
    case "say":
      return `say "${primitive.text}"`;
    case "face":
      return `face heading ${primitive.heading}`;
    default:
      return primitive.name;
  }
}

// As `/gloc` prints a position, in whole units.
function at({ x, y, z }: Position): string {
  return `X:${Math.round(x)} Y:${Math.round(y)} Z:${Math.round(z)}`;
}
