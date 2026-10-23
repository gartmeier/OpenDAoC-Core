import { expect, test } from "bun:test";
import { SLOT } from "../inventory.ts";
import { settle, TestAgent } from "../test-agent.ts";
import { inventory, item } from "../test-percepts.ts";
import { equip, EQUIP_TIMEOUT_MS } from "./equip.ts";

const FALCATA = item("falcata", { level: 2 });
const TRAINING_SWORD = item("training sword");

async function equipping() {
  const t = new TestAgent();
  await t.perceive(inventory([SLOT.rightHand, TRAINING_SWORD], [41, FALCATA]));
  const outcome = t.agent.run(equip, 41, SLOT.rightHand);
  await settle();
  return { t, outcome };
}

test("moves the item to the equipment slot and succeeds once it's there", async () => {
  const { t, outcome } = await equipping();
  expect(t.take()).toEqual([{ name: "moveItem", from: 41, to: SLOT.rightHand }]);

  await t.perceive(inventory([SLOT.rightHand, FALCATA], [41, TRAINING_SWORD]));

  expect(await outcome).toEqual({ ok: true });
});

test("tells a moved item from one of the same name that was there before", async () => {
  const t = new TestAgent();
  await t.perceive(inventory([SLOT.rightHand, item("falcata", { level: 2 })], [41, FALCATA]));
  const outcome = t.agent.run(equip, 41, SLOT.rightHand);
  await settle();

  await t.perceive(inventory([41]));
  await t.clock.advance(EQUIP_TIMEOUT_MS - 1);
  await t.perceive(inventory([SLOT.rightHand, item("falcata", { level: 2 })], [41, item("falcata", { level: 2 })]));

  expect(await outcome).toEqual({ ok: true });
});

test("fails with notEquipped when the item never gets there", async () => {
  const { t, outcome } = await equipping();

  await t.clock.advance(EQUIP_TIMEOUT_MS);

  expect(await outcome).toEqual({ ok: false, reason: "notEquipped" });
});

test("fails with notEquipped at once, moving nothing, when there's nothing in the slot", async () => {
  const t = new TestAgent();

  expect(await t.agent.run(equip, 41, SLOT.rightHand)).toEqual({ ok: false, reason: "notEquipped" });
  expect(t.take()).toEqual([]);
});
