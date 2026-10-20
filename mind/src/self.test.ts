import { expect, test } from "bun:test";
import type { JumpPercept, MovedPercept, OwnPositionPercept, Percept, PerceptMessage } from "./protocol.ts";
import { ownPositionIn, Self } from "./self.ts";

function message(percept: Percept): PerceptMessage {
  return { type: "percept", sequence: 1, elapsedMs: 1000, percept };
}

function ownPosition(x: number, y: number, z: number, heading: number): OwnPositionPercept {
  return { kind: "ownPosition", x, y, z, objectId: 17, heading, zoneXOffset: 0, zoneYOffset: 0, region: 200, divingEnabled: false, underwater: false };
}

function moved(x: number, y: number, z: number, heading: number): MovedPercept {
  return { kind: "moved", x, y, z, heading, speed: 191, targetX: 300000, targetY: 640000, targetZ: 4848 };
}

function jump(x: number, y: number, z: number, heading: number): JumpPercept {
  return { kind: "jump", x, y, z, objectId: 17, heading, houseNumber: 0 };
}

function selfAt(x: number, y: number, z: number): Self {
  const agentSelf = new Self();
  agentSelf.perceive(message(ownPosition(x, y, z, 1523)));
  return agentSelf;
}

test("knows nothing about where it is before the server says", () => {
  const agentSelf = new Self();

  expect(agentSelf.position).toBeUndefined();
  expect(agentSelf.maxSpeed).toBe(0);
  expect(agentSelf.movement).toEqual({ kind: "standing" });
});

test("takes its position and heading from the server at world entry, and stands", () => {
  const agentSelf = selfAt(296600, 642629, 4848);

  expect(agentSelf.position).toEqual({ x: 296600, y: 642629, z: 4848 });
  expect(agentSelf.heading).toBe(1523);
  expect(agentSelf.movement).toEqual({ kind: "standing" });
});

test("is moving to the target while it runs, wherever it has got to", () => {
  const agentSelf = selfAt(296600, 642629, 4848);

  agentSelf.perceive(message(moved(296591.7, 642621, 4848, 1523)));

  expect(agentSelf.position).toEqual({ x: 296591.7, y: 642621, z: 4848 });
  expect(agentSelf.heading).toBe(1523);
  expect(agentSelf.movement).toEqual({ kind: "moving", target: { x: 300000, y: 640000, z: 4848 } });
});

test("has arrived where the run ended", () => {
  const agentSelf = selfAt(296600, 642629, 4848);
  agentSelf.perceive(message(moved(296591.7, 642621, 4848, 1523)));

  agentSelf.perceive(message({ kind: "arrived", x: 300000, y: 640000, z: 4848 }));

  expect(agentSelf.position).toEqual({ x: 300000, y: 640000, z: 4848 });
  expect(agentSelf.movement).toEqual({ kind: "arrived" });
});

test("has stopped where the run ended, with the reason", () => {
  const agentSelf = selfAt(296600, 642629, 4848);
  agentSelf.perceive(message(moved(296591.7, 642621, 4848, 1523)));

  agentSelf.perceive(message({ kind: "stopped", x: 296580, y: 642610, z: 4848, reason: "Stop" }));

  expect(agentSelf.position).toEqual({ x: 296580, y: 642610, z: 4848 });
  expect(agentSelf.movement).toEqual({ kind: "stopped", reason: "Stop" });
});

test("is moving again after a run was replaced, to the new target", () => {
  const agentSelf = selfAt(296600, 642629, 4848);
  agentSelf.perceive(message(moved(296591.7, 642621, 4848, 1523)));
  agentSelf.perceive(message({ kind: "stopped", x: 296580, y: 642610, z: 4848, reason: "Replaced" }));
  expect(agentSelf.movement).toEqual({ kind: "stopped", reason: "Replaced" });

  agentSelf.perceive(message({ ...moved(296570, 642620, 4848, 1000), targetX: 296000, targetY: 643000, targetZ: 4900 }));

  expect(agentSelf.movement).toEqual({ kind: "moving", target: { x: 296000, y: 643000, z: 4900 } });
});

test("takes the position and heading the server jumps it to, and stands", () => {
  const agentSelf = selfAt(296600, 642629, 4848);
  agentSelf.perceive(message({ kind: "arrived", x: 300000, y: 640000, z: 4848 }));

  agentSelf.perceive(message(jump(250000, 600000, 3000, 1024)));

  expect(agentSelf.position).toEqual({ x: 250000, y: 600000, z: 3000 });
  expect(agentSelf.heading).toBe(1024);
  expect(agentSelf.movement).toEqual({ kind: "standing" });
});

// The Harness ends the run with `stopped` right after the jump.
test("has stopped where the server jumped it to mid-run, moved by the server", () => {
  const agentSelf = selfAt(296600, 642629, 4848);
  agentSelf.perceive(message(moved(296591.7, 642621, 4848, 1523)));

  agentSelf.perceive(message(jump(250000, 600000, 3000, 1024)));
  agentSelf.perceive(message({ kind: "stopped", x: 250000, y: 600000, z: 3000, reason: "MovedByServer" }));

  expect(agentSelf.position).toEqual({ x: 250000, y: 600000, z: 3000 });
  expect(agentSelf.heading).toBe(1024);
  expect(agentSelf.movement).toEqual({ kind: "stopped", reason: "MovedByServer" });
});

// As `/faceloc` sends it.
test("takes only the heading from a jump without a position", () => {
  const agentSelf = selfAt(296600, 642629, 4848);
  agentSelf.perceive(message({ kind: "arrived", x: 300000, y: 640000, z: 4848 }));

  agentSelf.perceive(message(jump(0, 0, 0, 2048)));

  expect(agentSelf.position).toEqual({ x: 300000, y: 640000, z: 4848 });
  expect(agentSelf.heading).toBe(2048);
  expect(agentSelf.movement).toEqual({ kind: "arrived" });
});

test("has stopped where it stands when it can't move, for example while rooted", () => {
  const agentSelf = selfAt(296600, 642629, 4848);
  agentSelf.perceive(message(moved(296591.7, 642621, 4848, 1523)));

  agentSelf.perceive(message({ kind: "maxSpeed", maxSpeedPercent: 0, turningDisabled: false, waterSpeed: 0 }));
  agentSelf.perceive(message({ kind: "stopped", x: 296580, y: 642610, z: 4848, reason: "UnableToMove" }));

  expect(agentSelf.maxSpeed).toBe(0);
  expect(agentSelf.position).toEqual({ x: 296580, y: 642610, z: 4848 });
  expect(agentSelf.movement).toEqual({ kind: "stopped", reason: "UnableToMove" });
});

test("the Percepts that say where its own Character is give that position, and no others", () => {
  expect(ownPositionIn(ownPosition(1, 2, 3, 0))).toEqual({ x: 1, y: 2, z: 3 });
  expect(ownPositionIn(jump(4, 5, 6, 0))).toEqual({ x: 4, y: 5, z: 6 });
  expect(ownPositionIn(moved(7, 8, 9, 0))).toEqual({ x: 7, y: 8, z: 9 });
  expect(ownPositionIn({ kind: "arrived", x: 10, y: 11, z: 12 })).toEqual({ x: 10, y: 11, z: 12 });
  expect(ownPositionIn({ kind: "stopped", x: 13, y: 14, z: 15, reason: "Stop" })).toEqual({ x: 13, y: 14, z: 15 });

  expect(ownPositionIn(jump(0, 0, 0, 2048))).toBeUndefined();
  expect(ownPositionIn({ kind: "objectDelete", objectId: 291 })).toBeUndefined();
  expect(ownPositionIn({ kind: "maxSpeed", maxSpeedPercent: 100, turningDisabled: false, waterSpeed: 0 })).toBeUndefined();
});

test("works out its maximum speed from the percentage the server sends", () => {
  const agentSelf = new Self();

  agentSelf.perceive(message({ kind: "maxSpeed", maxSpeedPercent: 100, turningDisabled: false, waterSpeed: 0 }));
  expect(agentSelf.maxSpeed).toBe(191);

  agentSelf.perceive(message({ kind: "maxSpeed", maxSpeedPercent: 117, turningDisabled: false, waterSpeed: 0 }));
  expect(agentSelf.maxSpeed).toBeCloseTo(223.47);
});

test("other Percepts change nothing", () => {
  const agentSelf = selfAt(296600, 642629, 4848);

  agentSelf.perceive(message({ kind: "bare", call: "SendUpdatePoints" }));
  agentSelf.perceive(message({ kind: "message", text: 'Vue says, "come"', chatType: "CT_Say", chatLocation: "CL_ChatWindow" }));

  expect(agentSelf.position).toEqual({ x: 296600, y: 642629, z: 4848 });
  expect(agentSelf.movement).toEqual({ kind: "standing" });
});
