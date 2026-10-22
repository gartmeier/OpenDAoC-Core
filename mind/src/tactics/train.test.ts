import { expect, test } from "bun:test";
import { settle, TestAgent } from "../test-agent.ts";
import { ownPosition, skills, spec } from "../test-percepts.ts";
import { affordableLevel, train, TRAIN_TIMEOUT_MS } from "./train.ts";

async function training() {
  const t = new TestAgent();
  await t.perceive(ownPosition(296000, 642000, 4848), skills(spec("Blades", 1), spec("Parry", 1)));
  const outcome = t.agent.run(train, "Blades", 3);
  await settle();
  return { t, outcome };
}

test("types /train and succeeds once the spec's new level arrives", async () => {
  const { t, outcome } = await training();
  expect(t.take()).toEqual([{ name: "command", text: "/train Blades 3" }]);

  await t.perceive(skills(spec("Blades", 3), spec("Parry", 1)));

  expect(await outcome).toEqual({ ok: true });
});

test("fails with notTrained when the new level never arrives", async () => {
  const { t, outcome } = await training();

  await t.clock.advance(TRAIN_TIMEOUT_MS);

  expect(await outcome).toEqual({ ok: false, reason: "notTrained" });
});

test("succeeds at once, typing nothing, when the spec is already there", async () => {
  const t = new TestAgent();
  await t.perceive(skills(spec("Blades", 3)));

  expect(await t.agent.run(train, "Blades", 2)).toEqual({ ok: true });
  expect(t.take()).toEqual([]);
});

test("each next level costs that level in points", () => {
  expect(affordableLevel(1, 0)).toBe(1);
  expect(affordableLevel(1, 2)).toBe(2);
  expect(affordableLevel(1, 4)).toBe(2);
  expect(affordableLevel(1, 5)).toBe(3);
  expect(affordableLevel(2, 3)).toBe(3);
});
