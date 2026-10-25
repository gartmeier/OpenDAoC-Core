# Learned from the server's source

Game rules learned by reading the server's source rather than by playing, each with the file it came from. They're candidates for the Agents' shared knowledge.

## Kill Tasks (`KillTask.cs`)

- Kill Tasks stop at level 20.
- The number of tasks per level equals your level. You can have one at a time, across all guards.
- A task expires after 2 hours.
- The task Mob is the first NPC of level L−2 to L in the zone's internal order, not the nearest one.

## Experience from Mobs (`serverrules/NpcKillRewardProcessor.cs`, `gameobjects/GameLiving.cs`, `serverrules/AbstractServerRules.cs`)

- A Mob's base XP depends only on its level (`GameLiving.XPForLiving`): 14480 at 13, 20480 at 14, 28980 at 15, 40960 at 16, 57930 at 17, 81920 at 18; about ×1.41 per level. Not on how long the fight took or how it went.
- It's capped at 125% (`XP_CAP_PERCENT`) of the XP of a Mob of *your* level. So a Mob one level above you pays ~1.25–1.41× a yellow one, and anything two or more above pays no more than that: don't fight reds for XP.
- Solo, you get it all only if you did all the damage (it's scaled by your share of the damage).
- Camp bonus: up to 55% (`MAX_CAMP_BONUS`) on top of the base, outside dungeons (dungeons more). Each Mob (spawn) starts at full bonus and loses 2 points of its 100% per kill (`killedNpc.CampBonus -= 0.02`); a Mob that's been up more than 30 minutes gives the full bonus again. So the bonus fades slowly where you camp; a fresh area resets it.

## Health regeneration (`propertycalc/HealthRegenerationAmountCalculator.cs`, `gameobjects/GamePlayer.cs`, `gameobjects/GameLiving.cs`)

- Each tick heals a flat `2.5 + 0.25 × level` hit points (times `HEALTH_REGEN_AMOUNT_MODIFIER`), plus any health-regen item or buff bonus. A flat amount: as max health grows, the % per second falls, and every +1 regen on gear adds a lot.
- A tick comes every 6 s standing, 3 s sitting, 14 s in combat, 10 s sitting in combat.
- You're "in combat" for 10 s (`IN_COMBAT_DURATION`) after your last combat action, so the slow ticks go on a little after the fight: sit at once anyway.
- Bleeding or disease stops regeneration.
