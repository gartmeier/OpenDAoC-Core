# Lessons

What I've learned while playing, which I read again before I next act.

## Where things are

- **Bound in Basar (Connacht), Bindstone X:338784 Y:444567.** Bard Eltonos (Hastener) X:339096 Y:445326; Cubert (reinforced armour, buys loot) X:338104 Y:445373; Sentinel Andreasa (named, on the tower). Ardee, 25000 north (X:342186 Y:469324): Garnock sells blades (quartz long sword L14 54 DPS, 24000c), Sentinel Moya. Tir na mBeo is ~70000 (6–7 min) further, through Ardee.
- Grind spots from Basar: fishing bear foragers L13–14 (neutral) at X:325900–328600 Y:438400–440000 and X:310000–311500 Y:435700–437700; **bantam spectres L15 (aggressive), ~30 at X:307000–309400 Y:438700–440400**. Fishing bears L18–20 near Basar: exclude them, and alp luachra, pookha, streaming wisp, young irewood.
- **East Lough Derg is unreachable**: every run past X≈354000–359600 (the lough) stops `unreachable` at Z:4614, from the south (Y:541000), middle (Y:516000) and north (Y:511000). Dergans, roanes, poachers and the X:365000+ camps can't be reached on foot.
- Lough Derg west (old haunts): Tir na mBeo Bindstone X:344830 Y:528095, Ornora X:345307 Y:526666, Boyd. Lough wolves, lucradans, badgers, wolfhounds and curmudgeons west of it (L7–13). Blackthorns call lunantishee swarms: exclude them. Ardagh (Silvermine) Bindstone X:351830 Y:554515.

## Kill Tasks are the best XP

- Target a *named* guard and `/whisper task`; kill the Mob it names, then `talkTo` it. `tactics/task.ts` `taskLoop` does the loop with any guard (`ORNORA`, `EIMILE`) and grinds a `camp` once the guard gives no more.
- Ornora's tasks: lough wolf (levels 10–11), badger "South East" (level 12, one at X:347387 Y:519877, near the blackthorns). Reward 45‰ at level 10, 61‰ at 11, 68‰ at 12, plus ~730–870c. A lough wolf loop takes ~90 s with no resting (regen while running covers the fight): **43‰/min**, against 20–25‰/min grinding.
- Server rules (`KillTask.cs`): tasks only up to **level 20**; as many per level as the level (13 at 13); one task at a time across all guards; the Mob is the *first* NPC of level L-2..L in the zone's subzone order (not the nearest), so it's the same kind all level, often far off; it expires after 2 hours. Guardians Andraste and J'akk (Tir na mBeo) give tasks too, but not on top of Ornora's.
- Loop only the task Mob (`band: [99,99]`): grabbing red wolfhounds on the way cost 50–70% HP each and dropped the loop to 26‰/min. Badger loop: ~115 s, 73‰ + ~10‰ kill at level 13, **43‰/min**; two thirds of it is running.
- Level 14's task is the curmudgeon trapper at X:329900 Y:536600, 16000 units from Ornora: ~230 s a loop, **22‰/min**, no better than grinding. Grinding trappers there until 40% and healing on the run back (`stayAfterPrefer`) gave 21‰/min: trappers and ghostly siabras (L11–12, X:351000 Y:527800, 5500 east of Ornora) pay only ~13‰ per 25% HP at level 14. Next: find better-paying Mobs (yellow/orange) near a guard.
- `tactics/junk.ts`: `equipBetterArmour` (by examined AF) and `sellJunk` (drops the unsellable training gear). Selling in the `inTown` hook kept me under the weight limit: I'd got encumbered (slow) at 63/58 lb.
- No Kill Task in Connacht at level 14 (Andreasa, Moya: "couldn't find any mob kill order"). Tasks expire: a kill for a task taken ~25 min earlier counted for nothing. `/task abort` drops one. `/task` answers in a text window the Mind can't see.

## Fighting and resting

- **Resting is ~70% of the time.** Sitting heals a flat ~1.8 HP/s, so XP per HP lost decides. At level 15 every kind paid about the same, **0.5–0.6‰ per % of health**, so 13–16‰/min: bantam spectre L15 24‰ for ~47% (27 s); anger sprite L13 12.5‰ for ~20% (21 s, X:315000 Y:433500); faerie beetle L17 31‰ for 93% (0.33‰/%: never fight +2). Mob XP rises ~1.41× a level and so does what they cost, so picking kinds gains little; my gear and level are the lever.
- XP is set by the Mob's level only, capped at 1.25× a Mob of my level, camp bonus on top of the capped base: fight yellow and orange (+1), never higher. Grey/green pay little.
- Taunting Blade (def −10) lets the Mob hit 92% instead of 81%, but adds ~30% to my damage: still the best filler (Enervating Blade's +10 def with no damage comes out a little worse).
- At level 16 with the drops below, spectres (now blue) pay 16‰ for ~25%: 14.6‰/min.
- Rest below 65%: a yellow can cost 60–70%, and I died fighting one at 63%.
- `kill` counts the run to the Mob in its 30 s `noDamage` timeout: `grind` runs within 800 first. After a level up, band [0,0] finds nothing where the Mobs were yellow: use [-1,1].
- Styles (`fight.ts`): Taunting Blade queued as filler; on a parry it cancels the filler and queues Return Blade, then Lunging Blade (slows the Mob). Needs **`/cancelstyle` ON** (it's a toggle, now on): without it Return Blade only became Taunting Blade's backup and never fired. Endurance runs out over 3–4 fights.
- Blademasters get 2× level spec points; Blades can't pass my level. `trainBlades` puts Blades at my level, the rest into Parry and Celtic Dual. Check `specPoints` after. Quote the line: `/train "Celtic Dual" 7`.

## Gear and money

- **Spectre drops at L15–16 beat the shops**: in ~40 kills, a Thrustbender Falcata (L16, 6.0 DPS at 96% = 5.8 effective, right hand; shop swords are 85% quality), Edgebender Reinforced Boots (AF36), Fortifying Reinforced Sleeves (AF37), Bodybender Ring L16, Icebender Jewel (`SLOT.jewel`, +11 hits). Examine them each level (`equipBetterArmour`, then weapons by "Effective Damage"); drop the rest (daggers and stilettos are thrust: useless) to stay under the weight limit. The Sturdy Gem fits no slot I tried (waist, wrist, cloak).
- No Kill Task in Connacht: the guard picks the zone's *first* L−2..L NPC and refuses if it's a named one (from-source.md).
- **Look in the backpack after fights and before selling**: drops beat shop gear (Razored Sword 45 DPS L11, Whirling Falcata 42 DPS L10, AF20–22 vest, sleeves and helm, all at level 10). Items a level or two above mine can be worn. Magic drops sell for 10–100c, so equip the better one first.
- Both hands swing: chat calls them by base name ("your falcata", "your sword"). The Whirling Falcata can't go in the left hand ("can't go there"); the Razored Sword can. A Protector's Wrap fits no slot (cloak and waist tried).
- Hits land mostly on torso and legs: upgrade those first. Effective AF is capped at 2× level (shell tacuil leggings AF30 showed 28 at L14). Bought shell tacuil leggings (Cubert, 21600c); wore the dropped Edgebender Bracelet (left wrist, +9 hits, Slash, Quickness). Shop weapons come at 85% quality: the quartz long sword's 54 DPS is 46 effective.
- Ardagh's Ysbail buys loot from the east side (X:352200 Y:552559). Nobody takes the training sword, training shield or Blademaster Initiate Gloves.
- After another Mind takes over the connection, Self can show level 0 and miss its object ID: see papercuts.md. Keep snippets under 25 minutes.
