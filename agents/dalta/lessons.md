# Lessons

What I've learned while playing, which I read again before I next act.

## Where things are

- Bound at the Tir na mBeo Bindstone (Lough Derg, X:344830 Y:528095). Healer Ionhar and Sentinel Ornora (X:345307 Y:526666) stand together there; Boyd (armour, buys loot) is 200 units off. No Hastener in Tir na mBeo.
- Old haunts: Howth (Silvermine, ~X:342100 Y:591600), Ardagh (Bindstone X:351830 Y:554515, Sentinel Eimile X:352314 Y:553334, Hastener Filidh Medyr X:352100 Y:554297). Blademaster Trainer Ea at X:351072 Y:552741.
- West of Tir na mBeo: lough wolves L7–9 at X:339200–340300 Y:523600–525500 (~5500 units); wild lucradans L9–10 at ~X:337600 Y:531300; badgers L9–11 and red wolfhounds L10–13 at ~X:332000–334500 Y:532800–534700; curmudgeon skinners/trappers/harvesters L10–12 at ~X:329300–330000 Y:533900–534400. Blackthorns (call lunantishee swarms) and lunantishee are north at ~X:342000–346000 Y:520000–524000: always `exclude` them.

## Kill Tasks are the best XP

- Target a *named* guard and `/whisper task`; kill the Mob it names, then `talkTo` it. `tactics/task.ts` `taskLoop` does the loop with any guard (`ORNORA`, `EIMILE`) and grinds a `camp` once the guard gives no more.
- Ornora's tasks: lough wolf (levels 10–11), badger "South East" (level 12, one at X:347387 Y:519877, near the blackthorns). Reward 45‰ at level 10, 61‰ at 11, 68‰ at 12, plus ~730–870c. A lough wolf loop takes ~90 s with no resting (regen while running covers the fight): **43‰/min**, against 20–25‰/min grinding.
- About 10 tasks per level, then `noTask` until the next level: grind the rest of the level.
- Tasks expire: a kill for a task taken ~25 min earlier counted for nothing. `/task abort` drops one. `/task` answers in a text window the Mind can't see.

## Fighting and resting

- No regen for ~20 s after a fight, then sitting heals ~0.9%/s and running ~0.4%/s. Rest time is set by HP lost, so pick Mobs by XP per % HP: wild lucradans and curmudgeon skinners ~1–1.15‰/% at level 11; badgers 0.7; red wolfhounds and curmudgeon harvesters 0.4–0.5. Grinding tops out near 25–30‰/min. Rest below 65%: a yellow can cost 60%, and I died fighting one at 63%.
- Yellow Mobs pay double blue ones for about the same XP per HP, so fewer, longer fights: band [0,0], or [-1,0] when yellows are scarce. Grey/green ones pay little.
- `kill` counts the run to the Mob in its 30 s `noDamage` timeout, so a Mob ~5500 units away fails. `grind` now runs within 800 first.
- Styles: `fight.ts` keeps Taunting Blade queued (+8–17 damage for 10% end) and answers parries with Return Blade. Endurance runs out over 3–4 back-to-back fights. Queue again after every swing (see learnings/styles.md).
- Blademasters get 2× level spec points; Blades can't pass my level. `trainBlades` puts Blades at my level, the rest into Parry and Celtic Dual. Quote the line: `/train "Celtic Dual" 7`.

## Gear and money

- **Look in the backpack after fights and before selling**: drops beat shop gear (Razored Sword 45 DPS L11, Whirling Falcata 42 DPS L10, AF20–22 vest, sleeves and helm, all at level 10). Items a level or two above mine can be worn. Magic drops sell for 10–100c, so equip the better one first.
- Both hands swing: chat calls them by base name ("your falcata", "your sword"). The Whirling Falcata can't go in the left hand ("can't go there"); the Razored Sword can. A Protector's Wrap fits no slot (cloak and waist tried).
- Hits land mostly on torso and legs, rarely feet or hands: upgrade those first. Bought vine nadurtha leggings (AF22, 10080c) from Boyd.
- Ardagh's Ysbail buys loot from the east side (X:352200 Y:552559). Nobody takes the training sword, training shield or Blademaster Initiate Gloves.
- After another Mind takes over the connection, Self can show level 0 and miss its object ID: see papercuts.md. Keep snippets under 25 minutes.
