// The Mind: logs an Agent's Character in and prints every Percept it gets, until the Agent leaves the world.
// Said next to the Agent, `come` runs it to the speaker, `grind` levels it to 5 around where it stands, and `stop` stops either. Ctrl+C stops it and quits.
// Every Percept also goes to the Agent's Self and Surroundings, which the printed lines name players and NPCs from.
// Usage: bun mind/agent.ts <character>
// The server address and token come from mind/.env (see mind/.env.example).

import { heardCommand } from "./src/commands.ts";
import { Connection } from "./src/connection.ts";
import { onCtrlC } from "./src/ctrl-c.ts";
import { loadEnvFile } from "./src/env.ts";
import type { Percept, PerceptMessage, Primitive } from "./src/protocol.ts";
import { Runner } from "./src/runner.ts";
import type { Position } from "./src/surroundings.ts";
import { Agent } from "./src/tactic.ts";
import { come } from "./src/tactics/come.ts";
import { grind } from "./src/tactics/grind.ts";

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
const log = (line: string) => console.error(line);
const agent = new Agent({
  send: (primitive) => {
    log(`Sending ${describePrimitive(primitive)}.`);
    return connection.send(primitive);
  },
  log,
});
const { self: agentSelf, surroundings } = agent;
const runner = new Runner(agent);

const connection = new Connection({ url, token, character, log }, {
  onPercept: (message) => {
    agent.perceive(message);
    console.log(formatPercept(message));
    const command = heardCommand(message.percept, character);

    switch (command?.word) {
      case "come":
        void runner.start(`come to ${command.speaker}`, come, command.speaker);
        break;
      case "grind":
        void runner.start("grind", grind);
        break;
      case "stop":
        void runner.stop();
        break;
    }
  },
});

// The Tactic stops first, leaving the Character standing with attack off, and then the connection quits it. The second Ctrl+C reaches the connection at once.
let interrupted = false;
const stopHandlingCtrlC = onCtrlC(() => {
  if (interrupted)
    return connection.interrupt();

  interrupted = true;
  void runner.stop().then(() => connection.interrupt());
});
const ending = await connection.run({ handleCtrlC: false });
stopHandlingCtrlC();
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
      return `playerPosition #${percept.objectId} ${surroundings.object(percept.objectId)?.name ?? "(unknown)"}\t${at(percept)} heading ${percept.heading} speed ${percept.speed} zone ${percept.zoneId}`;
    case "npcCreate":
      return `npcCreate #${percept.objectId} ${percept.name}\t${at(percept)} heading ${percept.heading} speed ${percept.speed} level ${percept.level}`;
    case "objectUpdate": {
      const target = percept.targetId ? ` target #${percept.targetId}` : "";
      const destination = percept.destination ? ` to ${at(percept.destination)}` : "";
      return `objectUpdate #${percept.objectId} ${surroundings.object(percept.objectId)?.name ?? "(unknown)"}\t${at(percept)} heading ${percept.heading} speed ${percept.speed} zone ${percept.zoneId} health ${percept.healthPercent}%${target}${destination}`;
    }
    case "objectDelete":
      return `objectDelete #${percept.objectId}`;
    case "objectRemove":
      return `objectRemove #${percept.objectId} type ${percept.objectType}`;
    case "ownPosition":
      return `ownPosition #${percept.objectId}	${at(percept)} heading ${percept.heading} region ${percept.region}`;
    case "jump":
      return percept.x === 0 && percept.y === 0 ? `jump #${percept.objectId}\theading ${percept.heading}` : `jump #${percept.objectId}\t${at(percept)} heading ${percept.heading}`;
    case "maxSpeed":
      return `maxSpeed ${percept.maxSpeedPercent}% (${agentSelf.maxSpeed.toFixed(1)} per second)`;
    case "routed":
      return `routed (${percept.reachesTarget ? "reaches the target" : "can't reach the target"})\t${percept.corners.map(at).join(" → ")}`;
    case "moved":
      return `moved	${at(percept)} heading ${percept.heading} speed ${percept.speed.toFixed(1)} to ${at({ x: percept.targetX, y: percept.targetY, z: percept.targetZ })}`;
    case "arrived":
      return `arrived	${at(percept)}`;
    case "stopped":
      return `stopped (${percept.reason})	${at(percept)}`;
    case "status":
      return `status\thealth ${percept.healthPercent}% (${percept.health}/${percept.maxHealth}) endurance ${percept.endurancePercent}%${percept.sitting ? " sitting" : ""}`;
    case "points":
      return `points\texperience ${percept.levelPermill}‰ spec points ${percept.specPoints}`;
    case "player":
      return `player\tlevel ${percept.level} ${percept.name} the ${percept.className}`;
    case "skills":
      return `skills\t${percept.skills.map((skill) => `${skill.name} ${skill.level}`).join(", ")}`;
    case "died":
      return `died #${percept.objectId} ${nameOf(percept.objectId)}, killed by #${percept.killerId} ${nameOf(percept.killerId)}`;
    case "revived":
      return `revived #${percept.objectId} ${nameOf(percept.objectId)}`;
  }
}

// The Agent's own Character isn't in its Surroundings.
function nameOf(objectId: number): string {
  return objectId === agentSelf.objectId ? character! : (surroundings.object(objectId)?.name ?? "(unknown)");
}

function describePrimitive(primitive: Primitive): string {
  switch (primitive.name) {
    case "moveTo":
      return primitive.within === undefined ? `moveTo ${at(primitive)}` : `moveTo ${at(primitive)} within ${primitive.within}`;
    case "say":
      return `say "${primitive.text}"`;
    case "face":
      return `face heading ${primitive.heading}`;
    case "target":
      return `target #${primitive.objectId} ${nameOf(primitive.objectId)}`;
    case "attack":
      return `attack ${primitive.on ? "on" : "off"}`;
    case "command":
      return `command ${primitive.text}`;
    default:
      return primitive.name;
  }
}

// As `/gloc` prints a position, in whole units.
function at({ x, y, z }: Position): string {
  return `X:${Math.round(x)} Y:${Math.round(y)} Z:${Math.round(z)}`;
}
