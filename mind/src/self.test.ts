import { expect, test } from "bun:test";
import type { MovedPercept, OwnPositionPercept, Percept, PerceptMessage } from "./protocol.ts";
import { Self } from "./self.ts";

function message(percept: Percept): PerceptMessage {
  return { type: "percept", sequence: 1, elapsedMs: 1000, percept };
}

function ownPosition(x: number, y: number, z: number, heading: number): OwnPositionPercept {
  return { kind: "ownPosition", x, y, z, objectId: 17, heading, zoneXOffset: 0, zoneYOffset: 0, region: 200, divingEnabled: false, underwater: false };
}

function moved(x: number, y: number, z: number, heading: number): MovedPercept {
  return { kind: "moved", x, y, z, heading, speed: 191, targetX: 300000, targetY: 640000, targetZ: 4848 };
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
