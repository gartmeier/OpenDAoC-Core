import { expect, test } from "bun:test";
import { BACKPACK_SLOTS, Inventory, merchantIndex, SLOT } from "./inventory.ts";
import type { Percept } from "./protocol.ts";
import { encumbrance, examined, inventory, item, merchantItem, merchantWindow, message, money, objectDelete } from "./test-percepts.ts";

function inventoryAfter(...percepts: Percept[]): Inventory {
  const known = new Inventory();

  for (const percept of percepts)
    known.perceive(message(percept));

  return known;
}

test("knows nothing it carries before the server says", () => {
  const known = new Inventory();

  expect(known.item(SLOT.rightHand)).toBeUndefined();
  expect(known.backpack).toEqual([]);
  expect(known.money).toBe(0);
  expect(known.weight).toBe(0);
  expect(known.capacity).toBe(0);
  expect(known.merchant).toBeUndefined();
  expect(known.examined).toBeUndefined();
});

test("holds the items the server puts in slots, and forgets those it empties", () => {
  const sword = item("training sword", { level: 1 });
  const claw = item("bear claw");
  const known = inventoryAfter(inventory([SLOT.rightHand, sword], [41, claw], [40]), inventory([41]));

  expect(known.item(SLOT.rightHand)).toEqual(sword);
  expect(known.equipped("rightHand")).toEqual(sword);
  expect(known.item(41)).toBeUndefined();
  expect(known.backpack).toEqual([]);
});

test("lists the backpack in slot order, and how many of its slots are free", () => {
  const known = inventoryAfter(inventory([45, item("wolf pelt")], [SLOT.torso, item("jerkin")], [40, item("bear claw")]));

  expect(known.backpack.map(({ slot, item }) => [slot, item.name])).toEqual([[40, "bear claw"], [45, "wolf pelt"]]);
  expect(known.freeBackpackSlots).toBe(BACKPACK_SLOTS - 2);
  expect(known.backpackFull).toBe(false);
});

test("is full when every backpack slot holds something", () => {
  const slots: [number, ReturnType<typeof item>][] = Array.from({ length: BACKPACK_SLOTS }, (_, i) => [40 + i, item(`claw ${i}`)]);

  expect(inventoryAfter(inventory(...slots)).backpackFull).toBe(true);
});

test("leaves its own slots alone for a vault's window, which numbers the vault's slots", () => {
  const known = inventoryAfter(inventory([41, item("bear claw")]), { ...inventory([41]), window: "PlayerVault" });

  expect(known.item(41)?.name).toBe("bear claw");
});

test("knows its money in copper, and its weight against its capacity", () => {
  const known = inventoryAfter(money(3_01_82), encumbrance(72, 58));

  expect(known.money).toBe(30182);
  expect(known.weight).toBe(58);
  expect(known.capacity).toBe(72);
});

test("keeps the last merchant window, from the merchant it last talked to", () => {
  const known = new Inventory();
  known.sent({ name: "talkTo", objectId: 0x2c54 });
  known.perceive(message(merchantWindow(merchantItem(0, "short sword", 150))));

  expect(known.merchant?.objectId).toBe(0x2c54);
  expect(known.merchant?.items.map((sold) => sold.name)).toEqual(["short sword"]);
  expect(known.trading(0x2c54)).toBe(true);
  expect(known.trading(0x2c55)).toBe(false);
});

// Buying and selling go to whatever the client targets, so a window stops being one to trade in once the Agent targets something else, or the merchant goes out of view.
test("stops trading with the merchant once something else is targeted, or the merchant goes out of view", () => {
  const known = new Inventory();
  known.sent({ name: "talkTo", objectId: 0x2c54 });
  known.perceive(message(merchantWindow(merchantItem(0, "short sword", 150))));

  known.sent({ name: "target", objectId: 0x101 });
  expect(known.trading(0x2c54)).toBe(false);
  expect(known.merchant?.objectId).toBe(0x2c54);

  known.sent({ name: "target", objectId: 0x2c54 });
  expect(known.trading(0x2c54)).toBe(true);

  known.perceive(message(objectDelete(0x2c54)));
  expect(known.trading(0x2c54)).toBe(false);
});

test("keeps the last examined text", () => {
  const known = inventoryAfter(examined("falcata", "- 3 Strength"));

  expect(known.examined).toEqual({ caption: "falcata", lines: ["- 3 Strength"] });
});

test("finds a merchant item by the index buy takes", () => {
  expect(merchantIndex(merchantItem(1, "falcata", 250, { page: 2 }))).toBe(61);
});
