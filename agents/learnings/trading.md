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

The leather pieces go to `SLOT.head`, `SLOT.hands`, `SLOT.feet`, `SLOT.torso`, `SLOT.legs` and `SLOT.arms`. A merchant's window doesn't say which slot a piece is for: its name does (helm, gloves, boots, jerkin, leggings, sleeves).

## Dialogs and Healers

Some NPCs ask a question in a Dialog, a window with Accept and Decline, or only OK. Healers, Enchanters and Blacksmiths do, and so do respecs and quests. The open Dialog is `agent.self.dialog`, with its `text` and whether it asks yes or no (`yesNo`), until you answer it or a newer one takes its place. It isn't chat, so it doesn't show up with the NPC's words.

- `answer(agent, accept)` accepts or declines the open Dialog. It fails with `noDialog` when none is open. What the server does next comes in chat.
- Dying costs Constitution: "You've lost some constitution, go to a healer". `agent.self.constitutionLost` says how many points, and max health is lower until a Healer restores them.
- Talking to a Healer cures resurrection sickness at once, then asks in a Dialog what restoring the Constitution costs. It asks nothing when no Constitution is lost. It restores as many points as you can afford, at least one.

```ts
const healer = agent.surroundings.named("Ionhar");
if (!healer) return "Ionhar isn't in view";

const talked = await talkTo(agent, healer.objectId);
if (!talked.ok) return talked;

const dialog = await agent.waitFor(() => agent.self.dialog, 5_000);
if (!dialog) return { constitutionLost: agent.self.constitutionLost, asked: "nothing" };

const answered = await answer(agent, true);
await agent.waitFor(() => agent.self.constitutionLost === 0, 5_000);
return { asked: dialog.text, answered, constitutionLost: agent.self.constitutionLost, maxHealth: agent.self.maxHealth };
```

## Loot and weight

- `/autoloot` is on: loot goes into your backpack by itself while it has room. With no room the item stays on the ground for 180 s, and only those who damaged the Mob may pick it up, with the `pickUp` Primitive.
- `agent.inventory.weight` against `agent.inventory.capacity`, in pounds. Above capacity the server slows you down.
- Examining an item (`{ name: "examine", slot }`) puts its full text, bonuses included, in `agent.inventory.examined`.
