import { expect, test } from "bun:test";
import type { MovedPercept, NpcCreatePercept, ObjectCreatePercept, ObjectDeletePercept, ObjectUpdatePercept, OwnPositionPercept, Percept, PerceptMessage, PlayerCreatePercept, PlayerPositionPercept } from "./protocol.ts";
import { distance, headingTowards, Surroundings, VISIBILITY_DISTANCE } from "./surroundings.ts";

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
    powerPercent: 100,
    endurancePercent: 100,
  };
}

function npcCreate(objectId: number, name: string, x: number, y: number, z: number, heading = 0, speed = 0): NpcCreatePercept {
  return {
    kind: "npcCreate",
    x,
    y,
    z,
    objectId,
    speed,
    zSpeed: 0,
    heading,
    model: 0x190,
    size: 40,
    level: 3,
    statue: false,
    realm: "None",
    ghost: false,
    equipped: false,
    torch: false,
    peaceful: false,
    flying: false,
    pet: false,
    untargetable: false,
    nameHidden: false,
    stealthed: false,
    questIndicator: "None",
    name,
    guildName: "",
  };
}

function objectCreate(objectId: number, name: string, x: number, y: number, z: number, heading = 0): ObjectCreatePercept {
  return { kind: "objectCreate", x, y, z, objectId, heading, model: 637, realm: "Hibernia", emblem: 0, underwater: false, keepBanner: false, yours: false, name };
}

function objectUpdate(objectId: number, x: number, y: number, z: number, heading: number, speed: number): ObjectUpdatePercept {
  return {
    kind: "objectUpdate",
    x,
    y,
    z,
    objectId,
    speed,
    zSpeed: 0,
    heading,
    targetId: 0,
    healthPercent: 100,
    realm: "None",
    untargetable: false,
    nameHidden: false,
    underwater: false,
    flying: false,
    zoneId: 101,
  };
}

function objectDelete(objectId: number): ObjectDeletePercept {
  return { kind: "objectDelete", objectId };
}

function ownPosition(x: number, y: number, z: number): OwnPositionPercept {
  return { kind: "ownPosition", x, y, z, objectId: 17, heading: 0, zoneXOffset: 0, zoneYOffset: 0, region: 200, divingEnabled: false, underwater: false };
}

function moved(x: number, y: number, z: number): MovedPercept {
  return { kind: "moved", x, y, z, heading: 1024, speed: 191, targetX: 0, targetY: 500000, targetZ: 2000 };
}

// The Agent at (500000, 500000, 2000) with Vue 1000 units away, Bob 5000 away and Ann 5999 away, all along X.
function surroundingsAround(): Surroundings {
  const surroundings = new Surroundings();
  surroundings.perceive(message(1000, ownPosition(500000, 500000, 2000)));
  surroundings.perceive(message(1100, playerCreate(0x123, "Vue", 501000, 500000, 2000)));
  surroundings.perceive(message(1200, playerCreate(0x124, "Bob", 505000, 500000, 2000)));
  surroundings.perceive(message(1300, playerCreate(0x125, "Ann", 505999, 500000, 2000)));
  return surroundings;
}

function names(surroundings: Surroundings): string[] {
  return surroundings.players.map((player) => player.name);
}

test("a player create makes the player known, with its name, position and heading, and no speed yet", () => {
  const surroundings = new Surroundings();
  surroundings.perceive(message(1500, playerCreate(0x123, "Vue", 531234, 478901, 2345, 2048)));

  expect(surroundings.player(0x123)).toEqual({
    kind: "player",
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
    kind: "player",
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

test("an object remove forgets the player, as when it quits", () => {
  const surroundings = new Surroundings();
  surroundings.perceive(message(1500, playerCreate(0x123, "Vue", 531234, 478901, 2345)));
  surroundings.perceive(message(1600, playerCreate(0x124, "Bob", 531000, 478000, 2345)));
  surroundings.perceive(message(3000, { kind: "objectRemove", objectId: 0x123, objectType: 2 }));

  expect(surroundings.named("Vue")).toBeUndefined();
  expect(surroundings.players.map((player) => player.name)).toEqual(["Bob"]);
});

test("a player created again is known afresh", () => {
  const surroundings = new Surroundings();
  surroundings.perceive(message(1500, playerCreate(0x123, "Vue", 531234, 478901, 2345)));
  surroundings.perceive(message(1600, playerPosition(0x123, 531400, 478950, 2350, 1024, 191)));
  surroundings.perceive(message(5000, playerCreate(0x123, "Vue", 540000, 470000, 2000, 512)));

  expect(surroundings.player(0x123)).toEqual({
    kind: "player",
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

test("the visibility distance is the server's", () => {
  expect(VISIBILITY_DISTANCE).toBe(6000);
});

test("forgets players farther than the visibility distance when the Agent runs away from them", () => {
  const surroundings = surroundingsAround();

  surroundings.perceive(message(2000, moved(499000, 500000, 2000)));

  expect(names(surroundings)).toEqual(["Vue", "Bob"]);

  surroundings.perceive(message(2200, moved(498999, 500000, 2000)));

  expect(names(surroundings)).toEqual(["Vue"]);
});

test("forgets players farther than the visibility distance when the server jumps the Agent away", () => {
  const surroundings = surroundingsAround();

  surroundings.perceive(message(2000, { kind: "jump", x: 507001, y: 500000, z: 2000, objectId: 17, heading: 0, houseNumber: 0 }));

  expect(names(surroundings)).toEqual(["Bob", "Ann"]);
});

test("forgets players farther than the visibility distance wherever a run ends", () => {
  const arrived = surroundingsAround();
  arrived.perceive(message(2000, { kind: "arrived", x: 496000, y: 500000, z: 2000 }));
  expect(names(arrived)).toEqual(["Vue"]);

  const stopped = surroundingsAround();
  stopped.perceive(message(2000, { kind: "stopped", x: 500000, y: 505000, z: 2000, reason: "MovedByServer" }));
  expect(names(stopped)).toEqual(["Vue"]);
});

// As `/faceloc` sends it: the Agent hasn't moved.
test("forgets nothing for a jump that sets only the heading", () => {
  const surroundings = surroundingsAround();
  surroundings.perceive(message(2000, moved(499000, 500000, 2000)));

  surroundings.perceive(message(3000, { kind: "jump", x: 0, y: 0, z: 0, objectId: 17, heading: 2048, houseNumber: 0 }));

  expect(names(surroundings)).toEqual(["Vue", "Bob"]);
});

// Only the Agent's own movement counts: the server stops sending a player who walks out of view, and doesn't always say it's gone.
test("keeps a player who moved out of view while the Agent stood still", () => {
  const surroundings = surroundingsAround();

  surroundings.perceive(message(2000, playerPosition(0x123, 520000, 500000, 2000, 0, 191)));

  expect(names(surroundings)).toEqual(["Vue", "Bob", "Ann"]);
});

test("forgets nothing while the Agent's position is unknown", () => {
  const surroundings = new Surroundings();
  surroundings.perceive(message(1100, playerCreate(0x123, "Vue", 501000, 500000, 2000)));
  surroundings.perceive(message(1200, { kind: "maxSpeed", maxSpeedPercent: 100, turningDisabled: false, waterSpeed: 0 }));

  expect(names(surroundings)).toEqual(["Vue"]);
});

test("nothing is forgotten by time, even after the Agent moved", () => {
  const surroundings = surroundingsAround();
  surroundings.perceive(message(2000, moved(499000, 500000, 2000)));

  surroundings.perceive(message(10 * 60 * 60 * 1000, { kind: "bare", call: "SendUpdatePoints" }));

  expect(names(surroundings)).toEqual(["Vue", "Bob"]);
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

test("an NPC create makes the NPC known, with its name, position, heading, speed, level and what it is, at full health and attacking nobody", () => {
  const surroundings = new Surroundings();
  surroundings.perceive(message(1500, npcCreate(0x456, "rat", 531234, 478901, 2345, 3072, 120)));

  expect(surroundings.npc(0x456)).toEqual({
    kind: "npc",
    objectId: 0x456,
    name: "rat",
    position: { x: 531234, y: 478901, z: 2345 },
    heading: 3072,
    speed: 120,
    lastHeardMs: 1500,
    level: 3,
    realm: "None",
    peaceful: false,
    pet: false,
    untargetable: false,
    healthPercent: 100,
    targetId: 0,
  });
});

test("an object update moves a known NPC, says when it was last heard from, and gives its health and whom it attacks", () => {
  const surroundings = new Surroundings();
  surroundings.perceive(message(1500, npcCreate(0x456, "rat", 531234, 478901, 2345)));
  surroundings.perceive(message(6500, { ...objectUpdate(0x456, 531400, 478950, 2350, 1024, 150), healthPercent: 40, targetId: 17, realm: "Midgard", untargetable: true }));

  expect(surroundings.npc(0x456)).toEqual({
    kind: "npc",
    objectId: 0x456,
    name: "rat",
    position: { x: 531400, y: 478950, z: 2350 },
    heading: 1024,
    speed: 150,
    lastHeardMs: 6500,
    level: 3,
    realm: "Midgard",
    peaceful: false,
    pet: false,
    untargetable: true,
    healthPercent: 40,
    targetId: 17,
  });
});

test("an NPC that attacks the Agent is found by the Agent's object ID", () => {
  const surroundings = new Surroundings();
  surroundings.perceive(message(1500, npcCreate(0x456, "rat", 531234, 478901, 2345)));
  surroundings.perceive(message(1600, npcCreate(0x457, "wolf", 531234, 478901, 2345)));
  surroundings.perceive(message(6500, { ...objectUpdate(0x457, 531234, 478901, 2345, 0, 0), targetId: 17 }));

  expect(surroundings.attacking(17).map((npc) => npc.name)).toEqual(["wolf"]);
});

test("an object update for an object it doesn't know is ignored", () => {
  const surroundings = new Surroundings();
  surroundings.perceive(message(6500, objectUpdate(0x456, 531400, 478950, 2350, 1024, 150)));

  expect(surroundings.objects).toEqual([]);
});

test("an object delete or remove forgets the NPC, and only that one", () => {
  const surroundings = new Surroundings();
  surroundings.perceive(message(1500, npcCreate(0x456, "rat", 531234, 478901, 2345)));
  surroundings.perceive(message(1600, npcCreate(0x457, "spider", 531000, 478000, 2345)));
  surroundings.perceive(message(1700, npcCreate(0x458, "snake", 532000, 479000, 2345)));
  surroundings.perceive(message(3000, objectDelete(0x456)));
  surroundings.perceive(message(3100, { kind: "objectRemove", objectId: 0x458, objectType: 1 }));

  expect(surroundings.npc(0x456)).toBeUndefined();
  expect(surroundings.npc(0x458)).toBeUndefined();
  expect(surroundings.npcs.map((npc) => npc.name)).toEqual(["spider"]);
});

test("an NPC created again is known afresh", () => {
  const surroundings = new Surroundings();
  surroundings.perceive(message(1500, npcCreate(0x456, "rat", 531234, 478901, 2345)));
  surroundings.perceive(message(6500, objectUpdate(0x456, 531400, 478950, 2350, 1024, 150)));
  surroundings.perceive(message(9000, npcCreate(0x456, "rat", 540000, 470000, 2000, 512)));

  expect(surroundings.npc(0x456)?.position).toEqual({ x: 540000, y: 470000, z: 2000 });
  expect(surroundings.npc(0x456)?.speed).toBe(0);
});

test("players and NPCs are known side by side, each found by ID and by name", () => {
  const surroundings = new Surroundings();
  surroundings.perceive(message(1500, playerCreate(0x123, "Vue", 531234, 478901, 2345)));
  surroundings.perceive(message(1600, npcCreate(0x456, "rat", 531000, 478000, 2345)));

  expect(surroundings.objects.map((known) => [known.kind, known.name])).toEqual([["player", "Vue"], ["npc", "rat"]]);
  expect(surroundings.players.map((player) => player.name)).toEqual(["Vue"]);
  expect(surroundings.npcs.map((npc) => npc.name)).toEqual(["rat"]);
  expect(surroundings.object(0x456)?.name).toBe("rat");
  expect(surroundings.player(0x456)).toBeUndefined();
  expect(surroundings.npc(0x123)).toBeUndefined();
  expect(surroundings.named("RAT")?.kind).toBe("npc");
  expect(surroundings.named("vue")?.kind).toBe("player");
});

test("the distance from a player to an NPC follows the NPC as it moves", () => {
  const surroundings = new Surroundings();
  surroundings.perceive(message(1500, playerCreate(0x123, "Vue", 531000, 478901, 2345)));
  surroundings.perceive(message(1600, npcCreate(0x456, "rat", 531234, 478901, 2345)));
  expect(distance(surroundings.named("Vue")!.position, surroundings.named("rat")!.position)).toBe(234);

  surroundings.perceive(message(6600, objectUpdate(0x456, 531100, 478901, 2345, 0, 150)));
  expect(distance(surroundings.named("Vue")!.position, surroundings.named("rat")!.position)).toBe(100);
});

test("an NPC isn't forgotten by time", () => {
  const surroundings = new Surroundings();
  surroundings.perceive(message(1500, npcCreate(0x456, "rat", 531234, 478901, 2345)));
  surroundings.perceive(message(10 * 60 * 60 * 1000, { kind: "bare", call: "SendUpdatePoints" }));

  expect(surroundings.named("rat")?.position).toEqual({ x: 531234, y: 478901, z: 2345 });
});

// The server never deletes an NPC that goes out of its range, so only the distance from Self forgets it.
test("an NPC farther than the visibility distance is forgotten when the Agent's Character moves", () => {
  const surroundings = new Surroundings();
  surroundings.perceive(message(1500, npcCreate(0x456, "rat", 531234 + VISIBILITY_DISTANCE + 1, 478901, 2345)));
  surroundings.perceive(message(1600, npcCreate(0x457, "spider", 531234 + VISIBILITY_DISTANCE, 478901, 2345)));
  expect(surroundings.npcs.map((npc) => npc.name)).toEqual(["rat", "spider"]);

  surroundings.perceive(message(2000, { kind: "jump", x: 531234, y: 478901, z: 2345, objectId: 17, heading: 0, houseNumber: 0 }));

  expect(surroundings.npcs.map((npc) => npc.name)).toEqual(["spider"]);
});

test("an object create makes the World object known, with its name, position, heading and model, standing still", () => {
  const surroundings = new Surroundings();
  surroundings.perceive(message(1500, objectCreate(0x789, "Dalta's Grave", 531234, 478901, 2345, 1024)));

  expect(surroundings.worldObjects).toEqual([
    {
      kind: "object",
      objectId: 0x789,
      name: "Dalta's Grave",
      position: { x: 531234, y: 478901, z: 2345 },
      heading: 1024,
      speed: 0,
      lastHeardMs: 1500,
      model: 637,
    },
  ]);
});

test("World objects are known beside players and NPCs, a Grave found by its name", () => {
  const surroundings = new Surroundings();
  surroundings.perceive(message(1500, playerCreate(0x123, "Vue", 531234, 478901, 2345)));
  surroundings.perceive(message(1600, npcCreate(0x456, "rat", 531000, 478000, 2345)));
  surroundings.perceive(message(1700, objectCreate(0x789, "Dalta's Grave", 531100, 478500, 2345)));

  expect(surroundings.objects.map((known) => [known.kind, known.name])).toEqual([["player", "Vue"], ["npc", "rat"], ["object", "Dalta's Grave"]]);
  expect(surroundings.players.map((player) => player.name)).toEqual(["Vue"]);
  expect(surroundings.npcs.map((npc) => npc.name)).toEqual(["rat"]);
  expect(surroundings.worldObjects.map((worldObject) => worldObject.name)).toEqual(["Dalta's Grave"]);
  expect(surroundings.named("dalta's grave")?.objectId).toBe(0x789);
});

test("an object update moves a known World object, and changes nothing else about it", () => {
  const surroundings = new Surroundings();
  surroundings.perceive(message(1500, objectCreate(0x789, "door", 531234, 478901, 2345)));
  surroundings.perceive(message(6500, { ...objectUpdate(0x789, 531300, 478901, 2345, 2048, 150), healthPercent: 40 }));

  expect(surroundings.object(0x789)).toEqual({
    kind: "object",
    objectId: 0x789,
    name: "door",
    position: { x: 531300, y: 478901, z: 2345 },
    heading: 2048,
    speed: 0,
    lastHeardMs: 6500,
    model: 637,
  });
});

// As when a Character prays at its Grave.
test("an object remove forgets the World object", () => {
  const surroundings = new Surroundings();
  surroundings.perceive(message(1500, objectCreate(0x789, "Dalta's Grave", 531234, 478901, 2345)));
  surroundings.perceive(message(3000, { kind: "objectRemove", objectId: 0x789, objectType: 0 }));

  expect(surroundings.worldObjects).toEqual([]);
});

test("a World object farther than the visibility distance is forgotten when the Agent's Character moves", () => {
  const surroundings = new Surroundings();
  surroundings.perceive(message(1500, objectCreate(0x789, "Dalta's Grave", 531234 + VISIBILITY_DISTANCE + 1, 478901, 2345)));
  surroundings.perceive(message(2000, { kind: "jump", x: 531234, y: 478901, z: 2345, objectId: 17, heading: 0, houseNumber: 0 }));

  expect(surroundings.worldObjects).toEqual([]);
});

test("heading 0 faces +Y and a quarter turn faces -X, as the server measures headings", () => {
  const here = { x: 1000, y: 2000, z: 0 };

  expect(headingTowards(here, { x: 1000, y: 2500, z: 0 })).toBe(0);
  expect(headingTowards(here, { x: 500, y: 2000, z: 0 })).toBe(1024);
  expect(headingTowards(here, { x: 1000, y: 1500, z: 0 })).toBe(2048);
  expect(headingTowards(here, { x: 1500, y: 2000, z: 0 })).toBe(3072);
  expect(headingTowards(here, { x: 1001, y: 2500, z: 0 })).toBe(4095);
});

test("the heading towards a point is the one the captured client ran along towards it", () => {
  // The captured first run went (-29.3, -28.3) per update at heading 1523 (see the fixtures README).
  const heading = headingTowards({ x: 0, y: 0, z: 0 }, { x: -29.3 * 20, y: -28.3 * 20, z: 0 })!;

  expect(Math.abs(heading - 1523)).toBeLessThanOrEqual(2);
});

test("only X and Y count towards a heading, and nothing faces a point straight above or below", () => {
  expect(headingTowards({ x: 0, y: 0, z: 0 }, { x: 0, y: 100, z: 5000 })).toBe(0);
  expect(headingTowards({ x: 0, y: 0, z: 0 }, { x: 0, y: 0, z: 100 })).toBeUndefined();
});
