import { expect, test } from "bun:test";
import { BACKPACK_SLOTS } from "../inventory.ts";
import type { Percept } from "../protocol.ts";
import { settle, TestAgent } from "../test-agent.ts";
import { arrived, inventory, item, merchantItem, merchantWindow, money, npcCreate, ownPosition, popup, stopped } from "../test-percepts.ts";
import { ANSWER_TIMEOUT_MS, buy, sell, TALK_CLOSE_IN, talkTo, TRADE_TIMEOUT_MS } from "./trade.ts";

const EIRA = 0x2c54;
const AT = { x: 294899, y: 641231, z: 4848 };
const SHORT_SWORD = merchantItem(0, "short sword", 150, { objectType: 19 });
const FALCATA = merchantItem(1, "falcata", 250, { level: 2, objectType: 19 });

// Greeen 100 units from Eira with 3 silver and a bear claw in his backpack, unless he's elsewhere.
async function nearEira(percepts: Percept[] = [], at = { x: AT.x + 100, y: AT.y, z: AT.z }) {
  const t = new TestAgent();
  await t.perceive(ownPosition(at.x, at.y, at.z), npcCreate(EIRA, "Eira", AT.x, AT.y, AT.z, { peaceful: true, realm: "Hibernia" }), money(300), inventory([40, item("bear claw")]), ...percepts);
  return t;
}

async function started<T>(t: TestAgent, running: Promise<T>) {
  await settle();
  return { t, outcome: running };
}

test("buy talks to the merchant, buys the item and succeeds once it lands in the backpack and the money drops", async () => {
  const t = await nearEira();
  const { outcome } = await started(t, t.agent.run(buy, EIRA, 1));
  expect(t.take()).toEqual([{ name: "talkTo", objectId: EIRA }]);

  await t.perceive(merchantWindow(SHORT_SWORD, FALCATA));
  expect(t.take()).toEqual([{ name: "buy", index: 1, count: 1 }]);

  await t.perceive(inventory([41, item("falcata", { level: 2 })]));
  await t.perceive(money(50));

  expect(await outcome).toEqual({ ok: true });
});

test("buy runs within reach of a merchant that's too far to talk to", async () => {
  const t = await nearEira([], { x: AT.x + 1000, y: AT.y, z: AT.z });
  await started(t, t.agent.run(buy, EIRA, 1));
  expect(t.take()).toEqual([{ name: "moveTo", ...AT, within: TALK_CLOSE_IN }]);

  await t.perceive(arrived(AT.x + TALK_CLOSE_IN, AT.y, AT.z));

  expect(t.take()).toEqual([{ name: "talkTo", objectId: EIRA }]);
});

test("buy fails with tooFar when it can't get within reach", async () => {
  const t = await nearEira([], { x: AT.x + 1000, y: AT.y, z: AT.z });
  const { outcome } = await started(t, t.agent.run(buy, EIRA, 1));

  await t.perceive(stopped(AT.x + 900, AT.y, AT.z, "Unreachable"));

  expect(await outcome).toEqual({ ok: false, reason: "tooFar" });
});

test("buy fails with outOfView for a merchant it doesn't see", async () => {
  const t = await nearEira();

  expect(await t.agent.run(buy, 0x999, 1)).toEqual({ ok: false, reason: "outOfView" });
  expect(t.take()).toEqual([]);
});

test("buy fails with timeout when the merchant's window never comes", async () => {
  const t = await nearEira();
  const { outcome } = await started(t, t.agent.run(buy, EIRA, 1));

  await t.clock.advance(ANSWER_TIMEOUT_MS);

  expect(await outcome).toEqual({ ok: false, reason: "timeout" });
});

test("buy doesn't talk again to the merchant whose window it's trading in", async () => {
  const t = await nearEira();
  const first = t.agent.run(buy, EIRA, 0);
  await settle();
  await t.perceive(merchantWindow(SHORT_SWORD, FALCATA), inventory([41, item("short sword")]), money(150));
  await first;
  t.take();

  await started(t, t.agent.run(buy, EIRA, 0));

  expect(t.take()).toEqual([{ name: "buy", index: 0, count: 1 }]);
});

test("buy fails with notEnoughMoney, buying nothing, for an item that costs more than it has", async () => {
  const t = await nearEira([money(200)]);
  const { outcome } = await started(t, t.agent.run(buy, EIRA, 1));
  t.take();

  await t.perceive(merchantWindow(SHORT_SWORD, FALCATA));

  expect(await outcome).toEqual({ ok: false, reason: "notEnoughMoney" });
  expect(t.take()).toEqual([]);
});

test("buy fails with noRoom, buying nothing, when the backpack is full", async () => {
  const full = inventory(...Array.from({ length: BACKPACK_SLOTS }, (_, i): [number, ReturnType<typeof item>] => [40 + i, item(`claw ${i}`)]));
  const t = await nearEira([full]);
  const { outcome } = await started(t, t.agent.run(buy, EIRA, 1));
  t.take();

  await t.perceive(merchantWindow(SHORT_SWORD, FALCATA));

  expect(await outcome).toEqual({ ok: false, reason: "noRoom" });
  expect(t.take()).toEqual([]);
});

test("buy fails with notBought when nothing lands, or the merchant sells nothing there", async () => {
  const t = await nearEira();
  const { outcome } = await started(t, t.agent.run(buy, EIRA, 1));
  await t.perceive(merchantWindow(SHORT_SWORD, FALCATA));

  await t.clock.advance(TRADE_TIMEOUT_MS);
  expect(await outcome).toEqual({ ok: false, reason: "notBought" });

  expect(await t.agent.run(buy, EIRA, 5)).toEqual({ ok: false, reason: "notBought" });
});

test("sell talks to the merchant, sells the item and succeeds once the slot empties and the money rises", async () => {
  const t = await nearEira();
  const { outcome } = await started(t, t.agent.run(sell, EIRA, 40));
  expect(t.take()).toEqual([{ name: "talkTo", objectId: EIRA }]);

  await t.perceive(merchantWindow(SHORT_SWORD));
  expect(t.take()).toEqual([{ name: "sell", slot: 40 }]);

  await t.perceive(inventory([40]), money(302));

  expect(await outcome).toEqual({ ok: true });
});

test("sell fails with notSold when the merchant doesn't take it, or there's nothing in the slot", async () => {
  const t = await nearEira();
  const { outcome } = await started(t, t.agent.run(sell, EIRA, 40));
  await t.perceive(merchantWindow(SHORT_SWORD));

  await t.clock.advance(TRADE_TIMEOUT_MS);
  expect(await outcome).toEqual({ ok: false, reason: "notSold" });

  expect(await t.agent.run(sell, EIRA, 41)).toEqual({ ok: false, reason: "notSold" });
});

const EA = 0x2c60;

// Greeen 100 units from Ea, the Blademaster trainer, who has no merchant window.
async function nearEa() {
  const t = new TestAgent();
  await t.perceive(ownPosition(AT.x + 100, AT.y, AT.z), npcCreate(EA, "Ea", AT.x, AT.y, AT.z, { peaceful: true, realm: "Hibernia" }));
  return t;
}

test("talkTo succeeds once an NPC with no merchant window answers in words", async () => {
  const t = await nearEa();
  const { outcome } = await started(t, t.agent.run(talkTo, EA));
  expect(t.take()).toEqual([{ name: "talkTo", objectId: EA }]);

  await t.perceive(popup("Ea says, \"Greeen, do you choose the Path of Harmony, and life as a [Blademaster]?\""));

  expect(await outcome).toEqual({ ok: true });
});

test("talkTo doesn't take another NPC's words, or what the NPC said before, for its answer", async () => {
  const t = await nearEa();
  await t.perceive(popup("Ea says, \"Do you wish to learn some more, Greeen?\""));
  const { outcome } = await started(t, t.agent.run(talkTo, EA));

  await t.perceive(popup("Ea guard says, \"Well met, Greeen.\""), popup("Ea is busy."));
  await t.clock.advance(ANSWER_TIMEOUT_MS);

  expect(await outcome).toEqual({ ok: false, reason: "timeout" });
});

test("talkTo takes an NPC's words said with its article for its answer", async () => {
  const t = await nearEa();
  await t.perceive(npcCreate(0x2c61, "town guard", AT.x, AT.y, AT.z, { peaceful: true, realm: "Hibernia" }));
  const { outcome } = await started(t, t.agent.run(talkTo, 0x2c61));

  await t.perceive(popup("The town guard says, \"Move along.\""));

  expect(await outcome).toEqual({ ok: true });
});
