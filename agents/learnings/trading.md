# Trading and gear

Learned by the stand-in `shop` of milestone 4b in Connla, a town in Shannon Estuary.

## Merchants

The Wiki has every merchant with its wares, prices and indexes. In Connla, Eira sells weapons (one blade per level up to 5 for 150 to 450 copper, then ferrite blades from 2400) and Erskine sells armour (rawhide leather, level 2, about 375 copper for the whole set). Erskine stands inside a building: routes go through its door.

- `talkTo(agent, merchantId)` gets within 192 units and opens its window. `agent.inventory.merchant` then holds what it sells.
- `buy(agent, merchantId, index)` buys one, talking to it first if needed. The index is the Wiki's, its page times 30 plus its place. It fails with `notEnoughMoney`, `noRoom`, `outOfView`, `tooFar` or `notBought`.
- `sell(agent, merchantId, slot)` sells what's in a backpack slot. Merchants won't take some things, such as the training sword or a recall stone: those fail with `notSold`.
- Money is in copper: `agent.inventory.money`. Gear was the bottleneck all through milestone 4b: selling loot is what pays for it.

```ts
const eira = agent.surroundings.named("Eira");
if (!eira) return "Eira isn't in view";

const opened = await talkTo(agent, eira.objectId);
return opened.ok ? agent.inventory.merchant?.items.map((item) => `${item.page * 30 + item.place}: ${item.name} level ${item.level}, ${item.price} copper`) : opened;
```

## Selling everything in the backpack

```ts
const erskine = agent.surroundings.named("Erskine");
if (!erskine) return "Erskine isn't in view";

const results: string[] = [];

for (const { slot, item } of agent.inventory.backpack) {
  const sold = await sell(agent, erskine.objectId, slot);
  results.push(`${item.name}: ${sold.ok ? "sold" : sold.reason}`);
}

return { results, money: agent.inventory.money };
```

## Equipping

`equip(agent, fromSlot, toSlot)` moves an item onto you and succeeds once the Inventory shows it there. What was there swaps into `fromSlot`. Put a one-handed weapon in `SLOT.rightHand` yourself: the server would put some swords in the left hand.

```ts
const sword = agent.inventory.backpack.find(({ item }) => item.name === "falcata");
return sword ? await equip(agent, sword.slot, SLOT.rightHand) : "no falcata in the backpack";
```

An item's `itemType`, in the Inventory and in a merchant's window, is the slot it's made for, in the same numbers as `SLOT`: an armour piece's goes straight to `equip`. Read it rather than the name: a "Wrap" is a ring, but "Fist Wraps" are a weapon. A ring (35 or 36) fits either ring slot, a wrist item (33 or 34) either wrist, and a left-hand weapon (11) either hand; 40 is backpack only.

## Loot and weight

- `/autoloot` is on: loot goes into your backpack by itself while it has room. With no room the item stays on the ground for 180 s, and only those who damaged the Mob may pick it up, with the `pickUp` Primitive.
- `agent.inventory.weight` against `agent.inventory.capacity`, in pounds. Above capacity the server slows you down.
- Examining an item (`{ name: "examine", slot }`) puts its full text, bonuses included, in `agent.inventory.examined`.
