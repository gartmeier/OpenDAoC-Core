# Milestone 4b: Inventory and gear

Status: resolved

## Problem Statement

Milestone 4 proved that an Agent can fight, rest, train, die and come back. But Greeen can't get past level 2 or 3, because he never puts on armour or replaces his training sword, and blue Mobs grind him down. He doesn't know what he carries, how much it weighs or how much money he has. He can't equip anything, sell his loot or buy anything.

The next milestone hands Greeen to a coding agent (ADR 0009), and it will need the same: to see its Inventory, compare gear and pick the upgrade, sell what it doesn't need, and notice when it carries too much. Before then I want the plumbing proven by mechanical Tactics, so that when the coding agent struggles, it isn't because a Percept or Primitive is missing.

## Solution

The Harness gains Primitives for everything a client does with items and merchants:
- move an item from one slot to another, which also equips and unequips;
- examine an item;
- pick up an item from the ground;
- destroy an item;
- talk to an NPC, which opens a merchant's window;
- buy and sell.

It turns the server's inventory, weight, money, examine and merchant calls into typed Percepts. The Mind gains a third belief, Inventory: what's equipped, what's in the backpack, the money, and the weight carried against capacity.

Porcelain Tactics `equip`, `buy` and `sell` sit on the Primitives. A stand-in `shop` Tactic runs in Connla, Greeen's starting town. It sells everything that isn't equipped. Then it buys a blade from Eira if his weapon is below his level, and leather from Erskine for every armour slot whose piece is below his level, as far as his money goes, and equips them. The Human starts it by saying `shop`. The Grind runs it by itself after each level-up, and when Greeen carries 80% of his weight capacity or his backpack is full.

Acceptance: `shop` gears Greeen up in Connla, and from `/player level 1` the Grind takes him to level 5 unattended. That's the goal milestone 4 couldn't reach without gear.

## User Stories

1. As the Human, I want to say `shop` next to Greeen in Connla, so that he sells his loot and buys and equips gear he can afford.
2. As the Human, I want `shop` to sell everything Greeen doesn't have equipped, so that his loot turns into money and his backpack empties.
3. As the Human, I want `shop` to buy a blade first when his weapon is below his level, so that his damage keeps up with the Mobs he fights.
4. As the Human, I want the blade to be the lowest-level one he can afford that's at least his level, so that his money goes as far as it can.
5. As the Human, I want the new blade put in his right hand, so that it never lands in his shield hand.
6. As the Human, I want `shop` then to buy leather for every armour slot whose piece is below his level, as far as his money goes, so that his armour keeps up too.
7. As the Human, I want each piece to be the cheapest one he can afford that's at least his level, so that he covers as many slots as he can.
8. As the Human, I want what he buys equipped at once, so that it protects him in his next fight.
9. As the Human, I want what he replaced sold on his next visit, so that old gear doesn't weigh him down.
10. As the Human, I want `shop` to finish without buying anything when nothing is affordable, so that a poor Agent goes back to grinding rather than waiting.
11. As the Human, I want `shop` to fail with a reason when the merchant isn't in view, so that I know why he didn't shop.
12. As the Human, I want the Grind to shop after each level-up, so that his gear keeps up with his level without me.
13. As the Human, I want the Grind to shop when Greeen carries 80% of his weight capacity, so that he never slows down from carrying too much.
14. As the Human, I want the Grind to shop when his backpack is full, so that his loot isn't left on the ground.
15. As the Human, I want the Grind to run home after shopping and carry on, so that a shop is just a step in the grind.
16. As the Human, I want `stop` and Ctrl+C to stop a shop as they stop a grind, so that I'm always in control.
17. As the Human, I want `/player level 1` and `grind` to take Greeen to level 5 unattended, so that I know gear closes the gap milestone 4 left.
18. As the Human, I want every new Primitive sent as the 1.127 client sends it, through the real packet handlers (ADR 0002), so that Agents trade, equip and loot under exactly the rules Humans do.
19. As the Human, I want the item, examine and merchant Percepts to carry only what a real client is shown (ADR 0001), so that the Agent knows nothing a Human wouldn't.
20. As the Human, I want Primitive results to keep reporting dispatch only (ADR 0004), so that the Agent learns whether a purchase worked from what the client is told.
21. As the Human, I want the Mind to print every new Percept and Primitive, so that I can see what Greeen sold, bought and equipped.
22. As a Mind developer, I want a move-item Primitive from one slot to another, so that I can equip, unequip and rearrange as a Human drags items.
23. As a Mind developer, I want an examine Primitive for a backpack, equipment or merchant item, so that I can read its full details as a Human does with shift-click.
24. As a Mind developer, I want a pick-up Primitive for an item on the ground, so that I can take loot the server didn't pick up for me, for example once the backpack had room again.
25. As a Mind developer, I want a destroy Primitive for an item in the backpack, so that I can get rid of something nobody will buy.
26. As a Mind developer, I want a talk-to Primitive for an NPC, so that I can open a merchant's window, and later speak to trainers and stable masters.
27. As a Mind developer, I want buy and sell Primitives, so that I can trade with the merchant I'm talking to.
28. As a Mind developer, I want a Percept for every change to my Inventory's slots, with each item's name, level, armour factor or DPS, speed, quality, condition, durability, weight, object type and count, so that I can compare gear.
29. As a Mind developer, I want a Percept with my weight and my capacity, so that I can tell when I carry too much.
30. As a Mind developer, I want a Percept with my money, so that I know what I can afford.
31. As a Mind developer, I want the examine window as plain text lines, so that I see an item's bonuses and requirements as a Human reads them.
32. As a Mind developer, I want a Percept with a merchant's items, each with its place in the window, level, armour factor or DPS, speed, object type, price, weight and whether my class can use it, so that I can choose what to buy.
33. As a Mind developer, I want Inventory to hold what's equipped by slot, the 40 backpack slots, my money and my weight against capacity, built only from Percepts, so that Tactics read beliefs instead of parsing Percepts.
34. As a Mind developer, I want Inventory to name equipment slots by their meaning (right hand, torso and so on) as well as by the client's slot numbers, so that code reads well.
35. As a Mind developer, I want an `equip` Tactic that moves an item to a given equipment slot and succeeds once Inventory shows it there, so that equipping is one call with a visible outcome.
36. As a Mind developer, I want a `buy` Tactic that talks to a merchant if needed, buys an item from its window and succeeds once the item is in my backpack, so that buying is one call.
37. As a Mind developer, I want a `sell` Tactic that sells a backpack item to the merchant I'm talking to and succeeds once it's gone and my money has gone up, so that selling is one call.
38. As a Mind developer, I want each of these to fail with a reason (`outOfView`, `tooFar`, `notEnoughMoney`, `noRoom`, `notBought`, `notSold`, `notEquipped`, `timeout`), so that the Tactic that ran it can decide what to do.
39. As a Mind developer, I want the TypeScript types for the new Primitives and Percepts generated from the C# definitions, so that both sides always agree.
40. As a Mind developer, I want to test the new Tactics against the fake Agent on a fake clock, so that I can check shopping without a server.

## Implementation Decisions

- **Capture first.** The Human records a capture with the real 1.127 client, with markers said before each part:
  - moving an item within the backpack;
  - equipping and unequipping an armour piece and a weapon, by dragging;
  - examining an item in the backpack and one in a merchant's window;
  - talking to a merchant, buying an item and selling one;
  - destroying an item;
  - picking up an item from the ground (`/autoloot off` and a kill first).

  It's checked in with the other 1.127 fixtures, with a section in their README, and the builders are written against it.
- **Primitives (ADR 0002).** All in the Harness area, each built as the captured client sends it and handled by the real packet handler on the client tick.
  - **`moveItem { from, to }`:** client slot numbers, read by `PlayerMoveItemRequestHandler`. Equipment slots are 10–37 and backpack slots 40–79. Moving to the paper doll slot (100) is left out: the server would then pick the slot from the item, and some swords would land in the left hand.
  - **`examine { slot }` and `examineMerchantItem { index }`:** the detail request, for an Inventory slot or a place in the open merchant window. The text arrives as an `examined` Percept.
  - **`pickUp { objectId }`:** the server picks up the client's current target, so the Harness targets the object first if it isn't already, as a click does. The server checks ownership and a 256-unit range.
  - **`destroy { slot }`:** backpack items only, as the server allows.
  - **`talkTo { objectId }`:** the interact request a right-click sends, which needs the NPC within 192 units. A merchant answers with its window, a trainer or stable master with its text. It targets the NPC first if it isn't already.
  - **`buy { index, count }` and `sell { slot }`:** the buy and sell requests, for the merchant the client targets. Both need it within 256 units.
  - The Harness rejects slot numbers or counts the packets can't carry, with a reason, as it does for object IDs.
- **Percepts (ADR 0001).** New typed projections of calls the server already makes on the packet library, reading what each call's 1.127 packet carries:
  - **`inventory`:** from the item and slot updates. The slots it changes, each with its item's fields as the packet writes them, or empty. No stat bonuses: the packet carries none.
  - **`encumbrance`:** weight carried and capacity.
  - **`money`:** copper, silver, gold, mithril and platinum.
  - **`examined`:** the caption and text lines of the item detail window the server sends for an examine. Other detail windows the server sends this way come through the same Percept.
  - **`merchantWindow`:** for each page, every item with its place, level, armour factor or DPS, speed, hand, damage type, object type, whether the Agent's class can use it, weight, price and name.
- **Codegen.** As before: C# types in the Harness area, regenerated TypeScript.
- **Inventory (Mind).**
  - A belief beside Self and Surroundings, built only from Percepts. It holds every slot the server has told it about, from the 40 backpack slots and the equipment slots, with the item fields from the `inventory` Percept, plus money (in copper) and weight against capacity.
  - Named equipment slots map to the client's numbers: right hand 10, left hand 11, two-handed 12, ranged 13, head 21, hands 22, feet 23, torso 25, cloak 26, legs 27, arms 28, and so on. The backpack is 40–79.
  - It also keeps the last merchant window it saw and which merchant sent it, and the last examined text.
- **Porcelain Tactics (ADR 0008).**
  - **`equip(item slot, equipment slot)`:** sends `moveItem` and succeeds when Inventory shows the item in the equipment slot. Fails with `notEquipped` after a timeout.
  - **`buy(merchant, index)`:** runs within reach of the merchant if needed and talks to it unless its window is already the last one seen. Fails with `notEnoughMoney` or `noRoom` from what Inventory says, or with `outOfView` or `tooFar`. Otherwise it buys, and succeeds when a new item lands in the backpack and the money drops; `notBought` otherwise.
  - **`sell(merchant, slot)`:** the same reach and talking, then sells. Succeeds when the slot empties and the money rises; `notSold` otherwise.
  - Item positions come from Percepts only. The Mind finds merchants by name in Surroundings.
- **Stand-in `shop` Tactic.** Deliberately simple, for the coding agent to replace:
  - It knows two merchants by name, static knowledge as a Human's memory of Connla would be: Eira (blades) and Erskine (leather). It fails with `outOfView` when either isn't in view.
  - Steps:
    1. Sell every backpack item to Erskine.
    2. Weapon: if the right hand holds no blade at least Greeen's level, buy from Eira the lowest-level blade that's at least his level, that his class can use and that he can afford, and equip it in the right hand.
    3. Armour: for each of helm, gloves, boots, jerkin, leggings and sleeves whose equipped piece is missing or below his level, buy from Erskine the cheapest leather piece for that slot that's at least his level and that he can afford, cheapest slots first, and equip it in its slot. The slot comes from the name's last word.
  - It succeeds even when it could afford nothing.
- **Grind.**
  - It runs `shop` after each level-up, when Greeen carries 80% of his capacity, and when no backpack slot is free.
  - It runs home afterwards, then carries on.
  - Training comes before shopping after a level-up. Fights still come first when something attacks him.
- **Entry point.** `shop` said next to the Agent starts the `shop` Tactic in place of whatever runs, like `come` and `grind`. It prints the new Percepts and Primitives.
- **Code placement.** As in milestone 4: the Harness folder and namespace, no core code changes. The Tactics and Inventory go in the Mind project.

## Testing Decisions

- **Good tests** check behaviour seen from outside: Percepts and time in, Primitives and outcomes out, and packets matching what the real client sent.
- **Packet builders (existing seam):** golden-byte tests for every new Primitive against the new capture, masking only fields the README documents as varying.
- **Percept projection (existing seam):** each new Percept projected from server state matches what its 1.127 packet writes, for an item, an empty slot, a stack, a merchant page and a detail window. Prior art: the `status`, `points` and `skills` projection tests.
- **Protocol (existing seam):** the new Primitives and Percepts round-trip as JSON, and the codegen test fails until the TypeScript is regenerated.
- **Harness client (existing seam):** `pickUp` and `talkTo` target first when needed, and bad slots or counts are refused with a reason.
- **Inventory (`bun test`):** the `inventory`, `encumbrance` and `money` Percepts update it, and empty slots clear. Prior art: Self and Surroundings tests.
- **Tactics (existing fake Agent seam):**
  - `equip`, `buy` and `sell`: success, and each failure reason.
  - `shop`:
    - sells everything first;
    - buys the weapon before armour;
    - picks the lowest-level affordable blade at least his level;
    - takes the cheapest armour pieces first and skips slots already at his level;
    - buys nothing when he's poor;
    - fails with `outOfView`.
  - Grind: shops after a level-up, at 80% weight and with a full backpack, then runs home.
  - Prior art: the milestone 4 Tactic tests.
- **Not tested automatically:** the live server, covered by the manual acceptance test.
- **Manual acceptance test:**
  1. Stand Greeen in Connla with some loot and money, and say `shop`. He sells his loot, buys what he can afford from Eira and Erskine and equips it, and the Mind's output shows it.
  2. `/player level 1` (money stays), then say `grind` where he hunts. Leave him until he says he's level 5. Deaths are fine. He shops on the way, after level-ups and when heavy.
  3. Say `stop` during a shop: he stops and stands.
  4. Check the server log has no speed-check entries.

## Out of Scope

- Shields, cloaks, jewellery and anything but blades and leather in the stand-in. The Primitives and Percepts cover all items.
- Comparing items by their examined bonuses in the stand-in. That's for the coding agent.
- Repairing, dyeing, crafting, vaults, trading with players and the market.
- Horses, kill tasks, trainers' dialogs and choosing a class. `talkTo` and `/whisper` reach them, and the coding agent will use them.
- Styles and spells.
- The coding agent itself (milestone 5).

## Further Notes

- Vocabulary follows the root glossary, which now has **Inventory**, and drops Plan, Planning, Reflection and Tactic Library (ADR 0009).
- Relevant decisions:
  - ADR 0001: Percepts only as a client knows.
  - ADR 0002: Primitives through the real handlers.
  - ADR 0004: dispatch-only results.
  - ADR 0008: Tactics are async functions.
  - ADR 0009: the coding agent plays next, so these Primitives and Percepts are its interface.
- Facts this spec relies on, from the server and its database as they stand:
  - **Item data the client gets:**
    - The 1.127 item data in an inventory update carries level, armour factor or DPS, speed or absorption, hand, damage type, object type, weight, condition, durability, quality, bonus level, model, colour, effect, spell names and the name (prefixed with a count for stacks). It doesn't carry stat bonuses.
    - Those come only in the detail window an examine opens, as text.
  - **Weight:**
    - Capacity is the Character's strength.
    - Above capacity the server slows the Character, says so, and sends the weight and a lower maximum speed.
  - **Loot:**
    - `/autoloot` is on by default, so loot goes into the backpack by itself while it has room.
    - With no room the server says the backpack is full, and the item stays on the ground for 180 s.
    - Only those who damaged the Mob may pick it up.
  - **Merchant packets:**
    - Buying and selling act on the client's current target and need it within 256 units. Talking needs 192.
    - The merchant window doesn't carry an item's equipment slot.
  - **Connla** (area centre 295946, 641999, radius 2000) is Greeen's starting town:
    - **Eira** (294899, 641231) sells blades, one per level: short sword 1, falcata 2, broadsword 3, long sword 4, bastard sword 5 (150 to 450 copper), and ferrite swords for 6–10 (2400 copper and up).
    - **Erskine** (295981, 641115) sells leather sets of helm, gloves, boots, jerkin, leggings and sleeves:
      - rawhide, level 2, armour factor 4, 375 copper for the set;
      - tanned, level 5, armour factor 10, about 60 silver;
      - cured, level 10;
      - hard, level 15;
      - rigid, level 20.
    - A Guardian wears cloth, leather and reinforced armour. The Blademaster trainer Allistar and the stable master Aelerogh are in Connla too.
  - Local Mobs drop coins (a few copper) and the server's random items. They have no loot tables of their own.
- **Money is the bottleneck:** from Greeen's level 4 with 182 copper, a falcata or broadsword and a few rawhide pieces are what he can afford. Selling loot is what pays for upgrades.
- **Risk:** the 80% weight trigger depends on the `encumbrance` Percept arriving whenever weight changes. If the server sends it only on login and when over capacity, the stand-in falls back to the backpack-full trigger. Check this in the capture.
- **Milestone ladder:**
  - 1 login and logout (done) → 2 Mind connection (done) → 3 movement (done) → 3b terrain and pathing (done) → 4 hand-written grind (done) → **4b inventory and gear (this spec)** → 5 a coding agent plays (ADR 0009).
  - After 5: styles and spells, character creation and groups, as the coding agent turns out to need them.

## Comments

**2026-10-23 (agent):** Implemented, not yet tested in game. Where it differs from or adds to the spec above:

- **Captured on 2026-10-23** in `items-merchants.tsv`. The builders match it byte for byte. Against the handlers' read logic it differed in: parameter 0x0000 on every request, two trailing 0s on the buy request, a 12-byte detail request with `1A000000` after the kind, and a one-byte pick up request.
- **No `destroy`.** The 1.127 client can't destroy an item: dragging it out of the window moves it to slot 1, the ground, and the server drops it. `moveItem` to slot 1 does the same, so there's no `destroy` Primitive.
- **The 80% weight risk is gone.** `GamePlayer.UpdateEncumbrance` sends the encumbrance whenever the weight carried or the capacity changes, not only above capacity.
- **An item's weight is the whole stack's,** in tenths of a pound, as the server's item has it. Encumbrance is in pounds.
- **`count` comes from the packet only where it carries one** (generic items, arrows, bolts, poisons, thrown weapons), and is 1 otherwise. Any stack of more than one also has its count in front of its name.
- **Which merchant a window is from** isn't in the packet. The Inventory takes it from the last `talkTo` sent, and stops counting the window as one to trade in once the Agent targets something else or the merchant goes out of view, because buying and selling go to the client's target.
- **`talkTo` is a porcelain Tactic too**, which `buy` and `sell` share: it gets within reach and opens the merchant's window, failing with `outOfView`, `tooFar` or `timeout`.
- **`shop` buys only one-handed blades,** since it puts the blade in the right hand. A sale, purchase or equip that fails is logged and skipped, and `shop` still succeeds.
- **The Grind shops for its load only once it has picked up more** since it last shopped, so a backpack the merchant won't buy from doesn't send it shopping on every step. It shops after a level-up whatever happened at the last shop.
- **The Grind runs to Connla's market before it shops** when either merchant isn't in view, to a spot between Eira and Erskine from their known positions, at home's height, which the navmesh corrects (ADR 0007). Then it runs home. `shop` itself still fails with `outOfView` when a merchant isn't in view, as the spec says. The summary now counts shops too.
- **The glossary's Inventory** now says that which merchant it's talking to comes from the Primitives sent, the one thing it doesn't learn from Percepts.
- **`moveItem` goes between the Character's own slots only,** 1 (the ground) to 99 (the horse's bags). The Harness refuses the paper doll (100) and anything above 1000, which would give the item to another object.
- **`shop` fails with `attacked`** before its next trade once something attacks the Agent, so that in the Grind fights still come first.
- **Routes now go through doors that open by clicking,** because Erskine stands inside a building. The navmesh marks those doors the same whether they're open or closed, so ADR 0006 still holds, and its consequences now say so. The Agent walks through such a door even while it's closed: the server doesn't stop it, though a Human's client would until they clicked it open.
- **Routes step across short gaps at doors.** Eira's hut has a raised floor, and zone 202's navmesh leaves it an island about 23 units from its door, so no route reached her. When a leg can't reach its end, the route now goes towards the door nearest the end, or else the door nearest its start, from both sides, and steps across in a straight line where the two meet within 64 units of each other and of the door. On the real navmesh, routes from the square, from Erskine and from behind the hut all reach her now, and routes from inside the hut back to Erskine or the square leave through the same door (ADR 0007).
- **The Grind fights yellow Mobs too, and has no leash.** Mobs exactly one level below Greeen ran out within 1500 units of home between respawns, so he wandered more than he fought. He now fights the nearest hostile Mob in view of his own level or one below, however far from home, and with none in view wanders within 1500 units of where he is. He still runs home after dying and after shopping.

**2026-10-24 (agent):** The Human's in-game acceptance run, with the Grind's yellow Mobs and no leash:

1. `shop` in Connla: Greeen sold his loot, bought a short sword from Eira inside her hut and wielded it, and bought rawhide gloves and boots from Erskine and put them on. Items a merchant won't take (the training sword, the recall stone) are offered again at every shop.
2. From `/player level 1`, `grind` took him to level 5 unattended, shopping after each level-up. The Mind's summary: `Reached level 5 in 233 min 48 s: 45 kills, 209 deaths, 34 rests, 3 shops. Tactic failures: died 209.` Levels 1 to 4 took 40 minutes. Level 4 to 5 took 3 h 21 min: mudmen killed him 208 times, mostly the same one (#8101), because after each death he runs home and attacks it again. The Grind gives up on a Mob only when it can't reach or damage it, not when it keeps losing to it. Money stayed the bottleneck: he could rarely afford the next piece.
3. `stop` during a shop stopped him standing.
4. The server logs have no speed-check entries (`Speed hack (teleport)` or `(kick)`).

**2026-10-24 (Human, recorded by agent):** Resolved. The run proves the item and merchant Primitives, the Percepts, Inventory and the porcelain Tactics work end to end, which is what this milestone was for. Its time, kills and deaths aren't a baseline: no comparison of the coding agent against the hand-written Tactics is planned.
