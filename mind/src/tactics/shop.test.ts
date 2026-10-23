import { expect, test } from "bun:test";
import { SLOT } from "../inventory.ts";
import { TestAgent } from "../test-agent.ts";
import { item, npcCreate, objectUpdate, ownPosition, player } from "../test-percepts.ts";
import { BLADES, LEATHER, shop } from "./shop.ts";
import { EIRA, ERSKINE, inTown, Town } from "./test-town.ts";

const TRAINING_SWORD = item("training sword", { level: 1, objectType: BLADES, hand: 2 });

async function shopping(level: number, town: Town) {
  const t = new TestAgent();
  await t.perceive(...town.welcome(level));
  const outcome = await inTown(t, town, t.agent.run(shop));
  return { t, outcome };
}

test("sells everything in the backpack to Erskine first", async () => {
  const town = new Town(0, [[SLOT.rightHand, TRAINING_SWORD], [40, item("bear claw")], [41, item("wolf pelt")]]);

  const { outcome } = await shopping(1, town);

  expect(outcome).toEqual({ ok: true });
  const trades = town.sent.filter((primitive) => ["talkTo", "sell", "buy"].includes(primitive.name));
  expect(trades.slice(0, 3)).toEqual([{ name: "talkTo", objectId: ERSKINE.objectId }, { name: "sell", slot: 40 }, { name: "sell", slot: 41 }]);
  expect(town.slots.has(40) || town.slots.has(41)).toBe(false);
  expect(town.slots.get(SLOT.rightHand)).toEqual(TRAINING_SWORD);
});

test("buys a blade before armour, and puts it in the right hand", async () => {
  const town = new Town(1000, [[SLOT.rightHand, TRAINING_SWORD]]);

  await shopping(2, town);

  expect(town.bought[0]).toBe("falcata");
  expect(town.slots.get(SLOT.rightHand)?.name).toBe("falcata");
  expect(town.bought.slice(1).every((name) => name.startsWith("rawhide"))).toBe(true);
});

test("picks the lowest-level blade at least its level that it can afford and use with one hand", async () => {
  const rich = new Town(399, [[SLOT.rightHand, TRAINING_SWORD]]);
  await shopping(3, rich);
  expect(rich.bought[0]).toBe("broadsword");

  const poor = new Town(300, [[SLOT.rightHand, TRAINING_SWORD]]);
  await shopping(3, poor);
  // A falcata is cheaper, but below its level.
  expect(poor.bought).not.toContain("falcata");
  expect(poor.bought).not.toContain("broadsword");
});

test("keeps a blade that's at least its level", async () => {
  const town = new Town(1000, [[SLOT.rightHand, item("falcata", { level: 2, objectType: BLADES })]]);

  await shopping(2, town);

  expect(town.sent).not.toContainEqual({ name: "talkTo", objectId: EIRA.objectId });
});

test("buys the cheapest armour pieces first, as far as the money goes, skipping slots already at its level, and equips each", async () => {
  const town = new Town(160, [
    [SLOT.rightHand, item("falcata", { level: 2, objectType: BLADES })],
    [SLOT.head, item("rawhide helm", { level: 2, objectType: LEATHER })],
    [SLOT.torso, item("ragged jerkin", { level: 1, objectType: LEATHER })],
  ]);

  await shopping(2, town);

  expect(town.bought).toEqual(["rawhide gloves", "rawhide sleeves", "rawhide boots"]);
  expect([SLOT.hands, SLOT.arms, SLOT.feet].map((slot) => town.slots.get(slot)?.name)).toEqual(["rawhide gloves", "rawhide sleeves", "rawhide boots"]);
  // The old jerkin stays on until it can afford a new one.
  expect(town.slots.get(SLOT.torso)?.name).toBe("ragged jerkin");
});

test("buys nothing and still succeeds when it can't afford anything", async () => {
  const town = new Town(10, [[SLOT.rightHand, TRAINING_SWORD]]);

  const { outcome } = await shopping(2, town);

  expect(outcome).toEqual({ ok: true });
  expect(town.bought).toEqual([]);
});

// Fights come first: the Grind takes the attacker on next.
test("fails with attacked, trading no more, as soon as something attacks it", async () => {
  const town = new Town(1000, [[SLOT.rightHand, TRAINING_SWORD], [40, item("bear claw")], [41, item("wolf pelt")]]);
  const t = new TestAgent();
  await t.perceive(...town.welcome(2), npcCreate(0x101, "wolf", 295600, 641200, 4848));
  const running = t.agent.run(shop);
  const original = town.answer.bind(town);
  // The wolf turns on Greeen as he sells his first item.
  town.answer = (primitive) => [...(primitive.name === "sell" ? [objectUpdate(0x101, 295600, 641200, 4848, { targetId: 17 })] : []), ...original(primitive)];

  expect(await inTown(t, town, running)).toEqual({ ok: false, reason: "attacked" });
  expect(town.sent.filter((primitive) => primitive.name === "sell")).toEqual([{ name: "sell", slot: 40 }]);
  expect(town.bought).toEqual([]);
});

test("fails with outOfView when either merchant isn't in view", async () => {
  const t = new TestAgent();
  await t.perceive(ownPosition(295500, 641200, 4848), player(2), npcCreate(EIRA.objectId, "Eira", EIRA.x, EIRA.y, EIRA.z, { peaceful: true }));

  expect(await t.agent.run(shop)).toEqual({ ok: false, reason: "outOfView" });
  expect(t.take()).toEqual([]);
});
