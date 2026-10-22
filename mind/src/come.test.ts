import { expect, test } from "bun:test";
import { Come, STAND_OFF } from "./come.ts";
import type { Percept, PerceptMessage, PlayerCreatePercept } from "./protocol.ts";
import { Self } from "./self.ts";
import { Surroundings } from "./surroundings.ts";

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

// The Harness's arrived Percept, which Self hears before Come, as in the Come Mind.
function arrive(agentSelf: Self, x: number, y: number, z: number): Percept {
  const percept: Percept = { kind: "arrived", x, y, z };
  agentSelf.perceive(message(percept));
  return percept;
}

// Greeen stands at (296000, 642000, 4848), and Vue 3000 units away along +X, 2 units higher.
function world() {
  const agentSelf = new Self();
  agentSelf.perceive(message({ kind: "ownPosition", x: 296000, y: 642000, z: 4848, objectId: 17, heading: 0, zoneXOffset: 0, zoneYOffset: 0, region: 200, divingEnabled: false, underwater: false }));
  const surroundings = new Surroundings();
  surroundings.perceive(message(playerCreate(291, "Vue", 299000, 642000, 4850)));
  return { come: new Come("Greeen"), agentSelf, surroundings };
}

test("another speaker's come runs to where they stand, ending 100 units short along the route", () => {
  const { come, agentSelf, surroundings } = world();

  expect(come.decide(sayLine('Vue says, "come"'), agentSelf, surroundings)).toEqual({ name: "moveTo", x: 299000, y: 642000, z: 4850, within: STAND_OFF });
  expect(STAND_OFF).toBe(100);
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

// Close in a straight line may still be far along the route, with a wall between them. The Harness arrives at once when it's close along the route too.
test("come from a speaker already within 100 units still runs to them, and turns to face them on arriving", () => {
  const { come, agentSelf, surroundings } = world();
  // 60 units along +X and 50 along +Y: atan2(-60, 50) is -571 of a 4096 turn, heading 3525.
  surroundings.perceive(message(playerCreate(292, "Ruwen", 296060, 642050, 4848)));

  expect(come.decide(sayLine('Ruwen says, "come"'), agentSelf, surroundings)).toEqual({ name: "moveTo", x: 296060, y: 642050, z: 4848, within: STAND_OFF });
  expect(come.decide(arrive(agentSelf, 296000, 642000, 4848), agentSelf, surroundings)).toEqual({ name: "face", heading: 3525 });
});

test("arriving under a speaker straight above the Agent gives nothing: there's no way to face them", () => {
  const { come, agentSelf, surroundings } = world();
  surroundings.perceive(message(playerCreate(292, "Ruwen", 296000, 642000, 4900)));
  come.decide(sayLine('Ruwen says, "come"'), agentSelf, surroundings);

  expect(come.decide(arrive(agentSelf, 296000, 642000, 4848), agentSelf, surroundings)).toBeUndefined();
});

test("a come that can't get to the speaker gives nothing more when the run ends", () => {
  const { come, agentSelf, surroundings } = world();
  come.decide(sayLine('Vue says, "come"'), agentSelf, surroundings);

  expect(come.decide({ kind: "stopped", x: 298000, y: 642000, z: 4848, reason: "Unreachable" }, agentSelf, surroundings)).toBeUndefined();
});

test("arriving after a come turns to face the speaker", () => {
  const { come, agentSelf, surroundings } = world();
  come.decide(sayLine('Vue says, "come"'), agentSelf, surroundings);

  // Vue is along +X from where Greeen arrives, which is heading 3072.
  expect(come.decide(arrive(agentSelf, 298900, 642000, 4848), agentSelf, surroundings)).toEqual({ name: "face", heading: 3072 });
});

test("arriving faces where the speaker was last known to be, not where they said come", () => {
  const { come, agentSelf, surroundings } = world();
  come.decide(sayLine('Vue says, "come"'), agentSelf, surroundings);
  // Vue walked on to 500 units along +Y of where Greeen arrives.
  surroundings.perceive(message(playerCreate(291, "Vue", 298900, 642500, 4850)));

  expect(come.decide(arrive(agentSelf, 298900, 642000, 4848), agentSelf, surroundings)).toEqual({ name: "face", heading: 0 });
});

test("arriving faces only once per come", () => {
  const { come, agentSelf, surroundings } = world();
  come.decide(sayLine('Vue says, "come"'), agentSelf, surroundings);
  come.decide(arrive(agentSelf, 298900, 642000, 4848), agentSelf, surroundings);

  expect(come.decide(arrive(agentSelf, 298900, 642000, 4848), agentSelf, surroundings)).toBeUndefined();
});

test("arriving faces whoever said the latest come that ran", () => {
  const { come, agentSelf, surroundings } = world();
  surroundings.perceive(message(playerCreate(292, "Ruwen", 296000, 645000, 4848)));
  come.decide(sayLine('Vue says, "come"'), agentSelf, surroundings);
  come.decide(sayLine('Ruwen says, "come"'), agentSelf, surroundings);
  come.decide({ kind: "stopped", x: 296500, y: 642000, z: 4848, reason: "Replaced" }, agentSelf, surroundings);

  // Ruwen is along +Y from where Greeen arrives.
  expect(come.decide(arrive(agentSelf, 296000, 644900, 4848), agentSelf, surroundings)).toEqual({ name: "face", heading: 0 });
});

test("arriving after a stop, or once the speaker has gone from Surroundings, gives nothing", () => {
  const stopped = world();
  stopped.come.decide(sayLine('Vue says, "come"'), stopped.agentSelf, stopped.surroundings);
  stopped.come.decide(sayLine('Vue says, "stop"'), stopped.agentSelf, stopped.surroundings);

  expect(stopped.come.decide(arrive(stopped.agentSelf, 298900, 642000, 4848), stopped.agentSelf, stopped.surroundings)).toBeUndefined();

  const gone = world();
  gone.come.decide(sayLine('Vue says, "come"'), gone.agentSelf, gone.surroundings);
  gone.surroundings.perceive(message({ kind: "objectDelete", objectId: 291 }));

  expect(gone.come.decide(arrive(gone.agentSelf, 298900, 642000, 4848), gone.agentSelf, gone.surroundings)).toBeUndefined();
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
