import { expect, test } from "bun:test";
import { headingTowards } from "../surroundings.ts";
import { settle, TestAgent } from "../test-agent.ts";
import { arrived, ownPosition, playerCreate, playerPosition, stopped } from "../test-percepts.ts";
import { come, STAND_OFF } from "./come.ts";

// Greeen at (296000, 642000, 4848), and Vue 3000 units away along +X, 2 units higher.
async function coming() {
  const t = new TestAgent();
  await t.perceive(ownPosition(296000, 642000, 4848), playerCreate(291, "Vue", 299000, 642000, 4850));
  const outcome = t.agent.run(come, "vue");
  await settle();
  return { t, outcome };
}

test("runs to the speaker, the stand-off distance short of them along the route", async () => {
  const { t } = await coming();

  expect(t.take()).toEqual([{ name: "moveTo", x: 299000, y: 642000, z: 4850, within: STAND_OFF }]);
});

test("on arriving, turns to where the speaker is now", async () => {
  const { t, outcome } = await coming();
  t.take();

  await t.perceive(playerPosition(291, 299000, 643000, 4850), arrived(298900, 642000, 4850));

  expect(await outcome).toEqual({ ok: true });
  expect(t.take()).toEqual([{ name: "face", heading: headingTowards({ x: 298900, y: 642000, z: 4850 }, { x: 299000, y: 643000, z: 4850 })! }]);
});

test("fails with the reason the run stopped, without turning", async () => {
  const { t, outcome } = await coming();
  t.take();

  await t.perceive(stopped(297000, 642000, 4848, "Unreachable"));

  expect(await outcome).toEqual({ ok: false, reason: "unreachable" });
  expect(t.take()).toEqual([]);
});

test("a run it replaced stopping isn't its own run ending", async () => {
  const { t, outcome } = await coming();
  let ended = false;
  void outcome.then(() => (ended = true));

  await t.perceive(stopped(296100, 642000, 4848, "Replaced"));

  expect(ended).toBe(false);
});

test("does nothing for a speaker it doesn't know of", async () => {
  const t = new TestAgent();
  await t.perceive(ownPosition(296000, 642000, 4848));

  expect(await t.agent.run(come, "Vue")).toEqual({ ok: false, reason: "unknownSpeaker" });
  expect(t.take()).toEqual([]);
});
