import { expect, test } from "bun:test";
import { Come } from "./come.ts";
import type { MoveToPrimitive, Percept, PerceptMessage, PlayerCreatePercept } from "./protocol.ts";
import { Self } from "./self.ts";
import { distance, Surroundings } from "./surroundings.ts";

function message(percept: Percept): PerceptMessage {
  return { type: "percept", sequence: 1, elapsedMs: 1000, percept };
}

function sayLine(text: string): Percept {
  return { kind: "message", text, chatType: "CT_Say", chatLocation: "CL_ChatWindow" };
}

function playerCreate(objectId: number, name: string, x: number, y: number, z: number): PlayerCreatePercept {
  return {
    kind: "playerCreate",
    x,
    y,
    z,
    sessionId: objectId + 1000,
    objectId,
    heading: 0,
    model: 0x02be,
    level: 23,
    realm: "Hibernia",
    dead: false,
    swimming: false,
    stealthed: false,
    wireframe: false,
    vampiir: false,
    eyeSize: 0,
    lipSize: 0,
    moodType: 0,
    eyeColor: 0,
    hairColor: 0,
    faceType: 0,
    hairStyle: 0,
    name,
    guildName: "",
    lastName: "",
    prefixName: "",
    title: "",
  };
}

// Greeen stands at (296000, 642000, 4848), and Vue 3000 units away along +X, 2 units higher.
function world() {
  const agentSelf = new Self();
  agentSelf.perceive(message({ kind: "ownPosition", x: 296000, y: 642000, z: 4848, objectId: 17, heading: 0, zoneXOffset: 0, zoneYOffset: 0, region: 200, divingEnabled: false, underwater: false }));
  const surroundings = new Surroundings();
  surroundings.perceive(message(playerCreate(291, "Vue", 299000, 642000, 4850)));
  return { come: new Come("Greeen"), agentSelf, surroundings };
}

test("another speaker's come runs to 100 units short of where they stand, along the line from Self", () => {
  const { come, agentSelf, surroundings } = world();

  const primitive = come.decide(sayLine('Vue says, "come"'), agentSelf, surroundings) as MoveToPrimitive;

  expect(primitive.name).toBe("moveTo");
  expect(primitive.x).toBeCloseTo(298900);
  expect(primitive.y).toBeCloseTo(642000);
  expect(distance(primitive, { x: 299000, y: 642000, z: 4850 })).toBeCloseTo(100);
  expect(distance(primitive, { x: 296000, y: 642000, z: 4848 })).toBeCloseTo(distance({ x: 296000, y: 642000, z: 4848 }, { x: 299000, y: 642000, z: 4850 }) - 100);
});

test("come works in any case", () => {
  const { come, agentSelf, surroundings } = world();

  for (const words of ["Come", "COME", "cOmE"])
    expect(come.decide(sayLine(`Vue says, "${words}"`), agentSelf, surroundings)?.name).toBe("moveTo");
});

test("a speaker found by name in any case is the same speaker", () => {
  const { come, agentSelf, surroundings } = world();

  expect(come.decide(sayLine('vue says, "come"'), agentSelf, surroundings)?.name).toBe("moveTo");
});

test("come from a speaker not in Surroundings gives nothing", () => {
  const { come, agentSelf, surroundings } = world();

  expect(come.decide(sayLine('Ruwen says, "come"'), agentSelf, surroundings)).toBeUndefined();
  expect(come.decide(sayLine('the town guard says, "come"'), agentSelf, surroundings)).toBeUndefined();
});

test("come before Self knows where it is gives nothing", () => {
  const { come, surroundings } = world();

  expect(come.decide(sayLine('Vue says, "come"'), new Self(), surroundings)).toBeUndefined();
});

test("come from a speaker already within 100 units gives nothing", () => {
  const { come, agentSelf, surroundings } = world();
  surroundings.perceive(message(playerCreate(292, "Ruwen", 296060, 642050, 4848)));

  expect(come.decide(sayLine('Ruwen says, "come"'), agentSelf, surroundings)).toBeUndefined();
});

test("another speaker's stop gives stop", () => {
  const { come, agentSelf, surroundings } = world();

  expect(come.decide(sayLine('Vue says, "stop"'), agentSelf, surroundings)).toEqual({ name: "stop" });
  expect(come.decide(sayLine('Vue says, "STOP"'), agentSelf, surroundings)).toEqual({ name: "stop" });
});

test("a stop from a speaker not in Surroundings still gives stop: it needs no position", () => {
  const { come, agentSelf, surroundings } = world();

  expect(come.decide(sayLine('Ruwen says, "stop"'), agentSelf, surroundings)).toEqual({ name: "stop" });
});

test("the Agent's own lines give nothing", () => {
  const { come, agentSelf, surroundings } = world();

  expect(come.decide(sayLine('You say, "come"'), agentSelf, surroundings)).toBeUndefined();
  expect(come.decide(sayLine('Greeen says, "come"'), agentSelf, surroundings)).toBeUndefined();
  expect(come.decide(sayLine('greeen says, "stop"'), agentSelf, surroundings)).toBeUndefined();
});

test("other chat types give nothing", () => {
  const { come, agentSelf, surroundings } = world();

  for (const chatType of ["CT_Send", "CT_Group", "CT_Emote", "CT_System", "CT_Staff"] as const)
    expect(come.decide({ kind: "message", text: 'Vue says, "come"', chatType, chatLocation: "CL_ChatWindow" }, agentSelf, surroundings)).toBeUndefined();
});

test("longer lines, and lines that don't read as someone saying words, give nothing", () => {
  const { come, agentSelf, surroundings } = world();

  for (const text of ['Vue says, "come here"', 'Vue says, "please come"', 'Vue says, " come"', 'Vue says, "come!"', 'Vue says, "stop it"', 'Vue yells, "come"', 'Vue says, "come', "come", ""])
    expect(come.decide(sayLine(text), agentSelf, surroundings)).toBeUndefined();
});

test("other Percepts give nothing", () => {
  const { come, agentSelf, surroundings } = world();

  expect(come.decide({ kind: "bare", call: "SendUpdatePoints" }, agentSelf, surroundings)).toBeUndefined();
  expect(come.decide({ kind: "arrived", x: 1, y: 2, z: 3 }, agentSelf, surroundings)).toBeUndefined();
});
