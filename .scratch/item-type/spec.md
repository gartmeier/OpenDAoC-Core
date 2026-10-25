# Item type

Status: ready-for-agent

## Problem Statement

Dalta, a coding agent, can't tell which equipment slot an item goes in. Three "Wraps" dropped. She took them for cloaks, tried one in the cloak, jewel, neck, waist and both wrist slots, got "can't go there!" each time, and sold all three as unwearable. They were rings. A Human sees that at a glance: the icon is a ring.

The item and merchant Percepts carry what the 1.127 packets carry: level, armour factor or DPS, speed, hand, damage type, object type, model and so on. The packets don't carry the item's slot, the server's item type that it checks every equip against. The client works it out from its own data files: it picks the icon by model. The Mind has no such data, so Dalta guesses from names, which works only for the server's generated items.

## Solution

Every item in the Inventory and every item in a merchant's window carries its item type: the equipment slot it's made for, as the server's slot number. Dalta then knows a Wrap is a ring before she tries it on, and knows which slot a merchant's piece is for before she buys it, as a Human does from the icon.

This bends ADR 0001, since the packet doesn't carry it. ADR 0006 now records the exception: what a client works out about an item from its own data files may reach the Agent directly.

## User Stories

1. As Dalta, I want each item in my Inventory to say which equipment slot it's made for, so that I put it on without trying every slot.
2. As Dalta, I want a dropped ring to say it's a ring whatever its name, so that I don't sell a "Wrap" as unwearable.
3. As Dalta, I want each item in a merchant's window to say which slot it's for, so that I buy the piece I need without guessing from its name.
4. As Dalta, I want the item type to be the same slot numbers as `SLOT`, so that I compare it with the slots I already use.
5. As Dalta, I want the field's documentation to say which slots accept which item types, so that I know a ring fits either ring slot, a bracer either wrist, and a right-hand weapon the two-hand slot too.
6. As Dalta, I want a left-hand weapon to say it's for the left hand, so that I know it can go in my off hand.
7. As Dalta, I want a two-handed weapon to say it's two-handed, so that I know it takes both hands.
8. As Dalta, I want an item that isn't equipment, such as a bear claw or a recall stone, to say it goes in no equipment slot, so that I don't try to wear it.
9. As Dalta, I want my Lesson about Wraps to point me at the item type, so that I read it instead of a name table.
10. As a future Agent, I want the shared trading Learning to say the merchant window gives each piece's slot, so that I don't learn the old workaround.
11. As the Human, I want the papercut gone from Dalta's papercuts once it ships, so that her list shows only what still gets in her way.
12. As the Human, I want the exception to ADR 0001 recorded in ADR 0006, so that the next Percept that wants a client-side fact has a precedent and a limit.
13. As the Human, I want the item type to tell the Agent hardly more than the icon would, so that Agents still know only what a Human would.
14. As a Mind developer, I want the TypeScript types regenerated from the C# definitions, so that both sides agree.

## Implementation Decisions

- **Protocol.** `InventoryItem` and `MerchantItem` each gain an `ItemType` integer: the item's `Item_Type` as the server stores it, an `eInventorySlot` number. Its comment says what it is and which slots accept it, mirroring the server's slot check:
  - head, hands, feet, torso, legs, arms, jewel, cloak, neck, waist and mythical: only that slot;
  - left or right wrist (33, 34): either wrist;
  - left or right ring (35, 36): either ring;
  - right hand (10): the right hand or the two-hand slot;
  - left hand (11): either hand or the two-hand slot;
  - two-handed (12): the two-hand slot;
  - ranged (13): the ranged slot;
  - shields, instruments and arrows also depend on their object type; anything else (such as 0 or 40) goes in no equipment slot.

  The comment also says the packet doesn't carry it, and why it's here anyway (ADR 0006).
- **Projection.** The inventory and merchant window projections fill `ItemType` from the item they're given, unchanged. No other field changes.
- **Codegen.** Regenerate the TypeScript, as before. The Mind's Inventory stores items as the Percepts give them, so it needs no change.
- **No helper.** The Mind gets no function saying which slots an item fits. The field's comment is enough, and Dalta can write one in her Tactics if she needs it.
- **ADR 0006** already has the new consequence (written during the grilling): what a client works out about an item from its own data files may reach the Agent directly; the item and merchant Percepts carry the server's item type, which tells a little more than the icon does.
- **Agent files, once it works in game:**
  - Delete "An item doesn't say which slot it goes in" from Dalta's papercuts.
  - Rewrite Dalta's Lesson on Wraps: an item's `itemType` says its slot; a Wrap is a ring; try every slot that type allows before calling an item unwearable. Drop the name → slot table from the Lesson.
  - Fix the trading Learning's line that a merchant's window doesn't say which slot a piece is for.
  - Keep the name → slot section in Dalta's notes from source: it's still true.
  - Leave Dalta's own Tactics alone (her `junk` Tactic guesses armour slots from names). She owns them, and the Lesson points her at the field.

## Testing Decisions

- **Good tests** check what crosses the seam: server state in, Percepts out, matching what the server stores and what its packets write.
- **Percept projection (the one seam):** the existing inventory and merchant window projection tests expect `ItemType` on each item. One new case: a generated "Wrap" ring projects the ring item type, a left-hand weapon projects left hand, and a backpack-only item projects its non-equipment type. Prior art: the existing inventory slots and merchant window projection tests.
- **Codegen:** the existing protocol test fails until the TypeScript is regenerated. Nothing new.
- **Mind:** no new tests; Inventory's behaviour doesn't change.
- **Manual check:** Dalta (or a snippet) reads `itemType` on a ring in the backpack and on a piece in a merchant's window, and equips the ring in a ring slot.

## Out of Scope

- A model → slot table or any other way of deriving the slot on the Mind's side.
- A Mind helper that checks which slots an item fits.
- Changing Dalta's Tactics.
- Other client-side item knowledge, such as icon names or item descriptions.
- Item type for items on the ground or in a trade window.

## Further Notes

- Facts this spec relies on, from the server as it stands:
  - The 1.127 item data in an inventory update and a merchant window carries level, the two value fields, hand, damage type, object type, weight, condition, durability, quality, bonus, bonus level, model, colour, effect and name. Not the item type.
  - The examine window shows the item type only in the GM's technical block, not to a Human.
  - The server checks a move against the item type only (and object type for shields, instruments, arrows and bolts), never the model.
  - Model → item type isn't a function: weapons share models across hands and between one- and two-handers, and a few jewellery and armour models appear in more than one slot (model 101 is neck, jewel or mythical). Ring model 103 is never anything but a ring. So the item type tells slightly more than the icon, mostly which hand a weapon is for, which the `hand` field mostly says already.
  - Generated "Fist Wraps" are hand-to-hand weapons, not rings: the item type gets that right where a name rule wouldn't.
- Relevant decisions: ADR 0001 (Percepts only as a client knows), ADR 0006 (static knowledge is fair; now including what the client knows about an item from its own data files), ADR 0009 (the coding agent plays through these Percepts).
- The grilling dropped one clean-up item: the `shop` stand-in that guessed a piece's slot from the last word of its name no longer exists.
