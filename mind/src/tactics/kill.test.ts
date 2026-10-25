import { expect, test } from "bun:test";
import { headingTowards } from "../surroundings.ts";
import { settle, TestAgent } from "../test-agent.ts";
import { arrived, died, npcCreate, objectDelete, objectRemove, objectUpdate, ownPosition, status, stopped } from "../test-percepts.ts";
import { CLOSE_IN, kill, NO_DAMAGE_MS } from "./kill.ts";

const WOLF = 0x1f0b;

// Greeen (object ID 17) at (296000, 642000, 4848), and a wolf pup 500 units away along +X.
async function fight() {
  const t = new TestAgent();
  await t.perceive(ownPosition(296000, 642000, 4848), npcCreate(WOLF, "wolf pup", 296500, 642000, 4848));
  const outcome = t.agent.run(kill, WOLF);
  await settle();
  return { t, outcome };
}

// The run to the wolf ended next to it, and Greeen turned to it.
async function nextToIt() {
  const { t, outcome } = await fight();
  await t.perceive(arrived(296420, 642000, 4848));
  t.take();
  return { t, outcome };
}

test("targets the Mob, attacks, and runs to it", async () => {
  const { t } = await fight();

  expect(t.take()).toEqual([
    { name: "target", objectId: WOLF },
    { name: "attack", on: true },
    { name: "moveTo", x: 296500, y: 642000, z: 4848, within: CLOSE_IN },
  ]);
});

test("faces the Mob once the run to it has arrived", async () => {
  const { t } = await fight();
  t.take();

  await t.perceive(arrived(296420, 642000, 4848));

  expect(t.take()).toEqual([{ name: "face", heading: headingTowards({ x: 296420, y: 642000, z: 4848 }, { x: 296500, y: 642000, z: 4848 })! }]);
});

test("succeeds once the Mob leaves the world dead, and turns attack off", async () => {
  const { t, outcome } = await nextToIt();

  await t.perceive(objectRemove(WOLF, 0));

  expect(await outcome).toEqual({ ok: true });
  expect(t.take()).toEqual([{ name: "attack", on: false }]);
});

test("fights on while the Mob shows 0% health but is still in the world", async () => {
  const { t, outcome } = await nextToIt();
  let ended = false;
  void outcome.then(() => (ended = true));

  await t.perceive(objectUpdate(WOLF, 296500, 642000, 4848, { healthPercent: 0 }));

  expect(ended).toBe(false);
  expect(t.take()).toEqual([]);
});

test("runs after the Mob when it moves out of melee range, from the news of where it is", async () => {
  const { t } = await nextToIt();

  await t.perceive(objectUpdate(WOLF, 296900, 642000, 4848));

  expect(t.take()).toEqual([{ name: "moveTo", x: 296900, y: 642000, z: 4848, within: CLOSE_IN }]);
});

test("doesn't run again while it runs to where the Mob still is", async () => {
  const { t } = await fight();
  t.take();

  await t.perceive(status(100), objectUpdate(WOLF, 296510, 642000, 4848));

  expect(t.take()).toEqual([]);
});

test("fails with died when the Agent dies, and turns attack off", async () => {
  const { t, outcome } = await nextToIt();

  await t.perceive(died(17, WOLF));

  expect(await outcome).toEqual({ ok: false, reason: "died" });
  expect(t.take()).toEqual([{ name: "attack", on: false }]);
});

test("fails with outOfView when the Mob goes out of view or leaves the world alive", async () => {
  for (const gone of [objectDelete(WOLF), objectRemove(WOLF, 1)]) {
    const { t, outcome } = await nextToIt();

    await t.perceive(gone);

    expect(await outcome).toEqual({ ok: false, reason: "outOfView" });
  }
});

test("fails with outOfView for a Mob it doesn't know, without sending anything", async () => {
  const t = new TestAgent();
  await t.perceive(ownPosition(296000, 642000, 4848));

  expect(await t.agent.run(kill, WOLF)).toEqual({ ok: false, reason: "outOfView" });
  expect(t.take()).toEqual([]);
});

test("fails with unreachable when no route gets to the Mob", async () => {
  const { t, outcome } = await fight();

  await t.perceive(stopped(296100, 642000, 4848, "Unreachable"));

  expect(await outcome).toEqual({ ok: false, reason: "unreachable" });
});

test("fails with noDamage when nobody hits or is hit for 30 s", async () => {
  const { t, outcome } = await nextToIt();

  await t.clock.advance(NO_DAMAGE_MS);

  expect(await outcome).toEqual({ ok: false, reason: "noDamage" });
});

test("health falling on either side keeps the fight going, and combat text alone doesn't", async () => {
  const { t, outcome } = await nextToIt();
  let ended = false;
  void outcome.then(() => (ended = true));

  await t.clock.advance(NO_DAMAGE_MS - 1000);
  await t.perceive(objectUpdate(WOLF, 296500, 642000, 4848, { healthPercent: 60 }));
  await t.clock.advance(NO_DAMAGE_MS - 1000);
  await t.perceive(status(80));
  await t.clock.advance(NO_DAMAGE_MS - 1000);
  expect(ended).toBe(false);

  await t.perceive({ kind: "message", text: "You miss!", chatType: "CT_YouHit", chatLocation: "CL_ChatWindow" });
  await t.clock.advance(1000);
  expect(await outcome).toEqual({ ok: false, reason: "noDamage" });
});

test("ends as stopped when the Agent is stopped, and leaves attack to whoever stopped it", async () => {
  const { t, outcome } = await nextToIt();

  t.agent.stop();

  expect(await outcome).toEqual({ ok: false, reason: "stopped" });
  expect(t.take()).toEqual([]);
});
