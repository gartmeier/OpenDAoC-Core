import { expect, test } from "bun:test";
import { settle, TestAgent } from "../test-agent.ts";
import { npcCreate, objectUpdate, ownPosition, status } from "../test-percepts.ts";
import { rest, REST_TIMEOUT_MS } from "./rest.ts";

async function resting(healthPercent = 40) {
  const t = new TestAgent();
  await t.perceive(ownPosition(296000, 642000, 4848), status(healthPercent), npcCreate(0x1f0b, "wolf pup", 296500, 642000, 4848));
  const outcome = t.agent.run(rest, 90);
  await settle();
  return { t, outcome };
}

test("sits, then stands and succeeds once health reaches the threshold", async () => {
  const { t, outcome } = await resting();
  expect(t.take()).toEqual([{ name: "sit" }]);

  await t.perceive(status(70, { sitting: true }), status(90, { sitting: true }));

  expect(await outcome).toEqual({ ok: true });
  expect(t.take()).toEqual([{ name: "stand" }]);
});

test("stands and fails with attacked as soon as an NPC attacks the Agent", async () => {
  const { t, outcome } = await resting();
  t.take();

  await t.perceive(objectUpdate(0x1f0b, 296050, 642000, 4848, { targetId: 17 }));

  expect(await outcome).toEqual({ ok: false, reason: "attacked" });
  expect(t.take()).toEqual([{ name: "stand" }]);
});

test("fails with attacked when health drops", async () => {
  const { t, outcome } = await resting();

  await t.perceive(status(35, { sitting: true }));

  expect(await outcome).toEqual({ ok: false, reason: "attacked" });
});

test("doesn't sit while something attacks the Agent", async () => {
  const t = new TestAgent();
  await t.perceive(ownPosition(296000, 642000, 4848), npcCreate(0x1f0b, "wolf pup", 296050, 642000, 4848), objectUpdate(0x1f0b, 296050, 642000, 4848, { targetId: 17 }));

  expect(await t.agent.run(rest, 90)).toEqual({ ok: false, reason: "attacked" });
  expect(t.take()).toEqual([]);
});

test("stands and fails with timeout when health never gets there", async () => {
  const { t, outcome } = await resting();

  await t.clock.advance(REST_TIMEOUT_MS);

  expect(await outcome).toEqual({ ok: false, reason: "timeout" });
  expect(t.take()).toEqual([{ name: "sit" }, { name: "stand" }]);
});
