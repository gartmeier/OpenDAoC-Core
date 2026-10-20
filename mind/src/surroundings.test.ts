import { expect, test } from "bun:test";
import type { ObjectDeletePercept, Percept, PerceptMessage, PlayerCreatePercept, PlayerPositionPercept } from "./protocol.ts";
import { distance, Surroundings } from "./surroundings.ts";

function message(elapsedMs: number, percept: Percept): PerceptMessage {
  return { type: "percept", sequence: 1, elapsedMs, percept };
}

function playerCreate(objectId: number, name: string, x: number, y: number, z: number, heading = 0): PlayerCreatePercept {
  return {
    kind: "playerCreate",
    x,
    y,
    z,
    sessionId: objectId + 1000,
    objectId,
    heading,
    model: 0x02be,
    level: 23,
    realm: "Midgard",
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

function playerPosition(objectId: number, x: number, y: number, z: number, heading: number, speed: number): PlayerPositionPercept {
  return {
    kind: "playerPosition",
    x,
    y,
    z,
    speed,
    fallSpeed: 0,
    sessionId: objectId + 1000,
    objectId,
    zoneId: 101,
    stateFlags: 0,
    steedSeat: 0,
    heading,
    headingFlags: 0,
    actionFlags: 0,
    roleplay: false,
    healthPercent: 100,
    attacking: false,
    manaPercent: 100,
    endurancePercent: 100,
  };
}

function objectDelete(objectId: number): ObjectDeletePercept {
  return { kind: "objectDelete", objectId };
}

test("a player create makes the player known, with its name, position and heading, and no speed yet", () => {
  const surroundings = new Surroundings();
  surroundings.perceive(message(1500, playerCreate(0x123, "Vue", 531234, 478901, 2345, 2048)));

  expect(surroundings.player(0x123)).toEqual({
    objectId: 0x123,
    name: "Vue",
    position: { x: 531234, y: 478901, z: 2345 },
    heading: 2048,
    speed: 0,
    lastHeardMs: 1500,
  });
});

test("a player position moves a known player, and says when it was last heard from", () => {
  const surroundings = new Surroundings();
  surroundings.perceive(message(1500, playerCreate(0x123, "Vue", 531234, 478901, 2345)));
  surroundings.perceive(message(2700, playerPosition(0x123, 531400, 478950, 2350, 1024, 191)));

  expect(surroundings.player(0x123)).toEqual({
    objectId: 0x123,
    name: "Vue",
    position: { x: 531400, y: 478950, z: 2350 },
    heading: 1024,
    speed: 191,
    lastHeardMs: 2700,
  });
});

test("a player position for a player it doesn't know is ignored", () => {
  const surroundings = new Surroundings();
  surroundings.perceive(message(2700, playerPosition(0x123, 531400, 478950, 2350, 1024, 191)));

  expect(surroundings.players).toEqual([]);
});

test("an object delete forgets the player, and only that one", () => {
  const surroundings = new Surroundings();
  surroundings.perceive(message(1500, playerCreate(0x123, "Vue", 531234, 478901, 2345)));
  surroundings.perceive(message(1600, playerCreate(0x124, "Bob", 531000, 478000, 2345)));
  surroundings.perceive(message(3000, objectDelete(0x123)));

  expect(surroundings.player(0x123)).toBeUndefined();
  expect(surroundings.players.map((player) => player.name)).toEqual(["Bob"]);
});

test("a player created again is known afresh", () => {
  const surroundings = new Surroundings();
  surroundings.perceive(message(1500, playerCreate(0x123, "Vue", 531234, 478901, 2345)));
  surroundings.perceive(message(1600, playerPosition(0x123, 531400, 478950, 2350, 1024, 191)));
  surroundings.perceive(message(5000, playerCreate(0x123, "Vue", 540000, 470000, 2000, 512)));

  expect(surroundings.player(0x123)).toEqual({
    objectId: 0x123,
    name: "Vue",
    position: { x: 540000, y: 470000, z: 2000 },
    heading: 512,
    speed: 0,
    lastHeardMs: 5000,
  });
});

test("a player is found by name, in any case", () => {
  const surroundings = new Surroundings();
  surroundings.perceive(message(1500, playerCreate(0x123, "Vue", 531234, 478901, 2345)));
  surroundings.perceive(message(1600, playerCreate(0x124, "Bob", 531000, 478000, 2345)));

  expect(surroundings.named("vue")?.objectId).toBe(0x123);
  expect(surroundings.named("BOB")?.objectId).toBe(0x124);
  expect(surroundings.named("Greeen")).toBeUndefined();
});

// The server doesn't send a player who stands still again.
test("nothing is forgotten by time", () => {
  const surroundings = new Surroundings();
  surroundings.perceive(message(1500, playerCreate(0x123, "Vue", 531234, 478901, 2345)));
  surroundings.perceive(message(10 * 60 * 60 * 1000, { kind: "bare", call: "SendUpdatePoints" }));

  expect(surroundings.named("Vue")?.position).toEqual({ x: 531234, y: 478901, z: 2345 });
});

test("other Percepts change nothing", () => {
  const surroundings = new Surroundings();
  surroundings.perceive(message(1500, playerCreate(0x123, "Vue", 531234, 478901, 2345)));
  const before = surroundings.players;

  surroundings.perceive(message(1600, { kind: "message", text: 'Vue says, "hello"', chatType: "CT_Say", chatLocation: "CL_ChatWindow" }));
  surroundings.perceive(message(1700, { kind: "barePacket", call: "SendUDP", code: 0xba }));

  expect(surroundings.players).toEqual(before);
});

test("the distance between two points is worked out when asked, Z included", () => {
  expect(distance({ x: 1000, y: 2000, z: 0 }, { x: 1300, y: 2400, z: 0 })).toBe(500);
  expect(distance({ x: 0, y: 0, z: 0 }, { x: 2, y: 3, z: 6 })).toBe(7);
});

test("the distance to a player follows the player as it moves", () => {
  const surroundings = new Surroundings();
  const here = { x: 531000, y: 478901, z: 2345 };
  surroundings.perceive(message(1500, playerCreate(0x123, "Vue", 531234, 478901, 2345)));
  expect(distance(here, surroundings.named("Vue")!.position)).toBe(234);

  surroundings.perceive(message(1600, playerPosition(0x123, 531100, 478901, 2345, 0, 191)));
  expect(distance(here, surroundings.named("Vue")!.position)).toBe(100);
});
