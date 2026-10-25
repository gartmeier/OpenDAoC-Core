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
- At level 15 a sitting tick is int(2.5 + 3.75) = 6 HP every 3 s, 2 HP/s; measured 1.75–1.85 HP/s over whole rests, the in-combat ticks included.
- No campfire or resting-place bonus exists. Only items (`ItemBonus`), regen buffs (`spells/RegenBuff.cs`, `AllRegenBuff.cs`) and the Regeneration realm ability add to it. No Hibernian merchant in the wiki sells a regen item.
- A Mob out of combat with nothing on its aggro list heals 10% of its max health per tick.

## Camp bonus and the XP cap, in order (`serverrules/NpcKillRewardProcessor.cs`)

- The base XP is capped first (125% of a Mob of your level), then the camp bonus (55% outdoors, 66% in a dungeon zone, times the spawn's own `CampBonus`) is added on the capped base. So a +1 Mob pays 1.25× a yellow one, camp bonus included, and +2 or more pays the same as +1.

## Styles and defence (`ECS-Components/AttackComponent.cs`)

- A Style's defensive mod (`BonusToDefense`) applies while your last swing was a Style that hit: the Mob's miss chance goes up by that many points. Taunting Blade's −10 took a bantam spectre's misses from 18.9% to about 8%; Enervating Blade's +10 would take them to about 29%; Lunging Blade gives +5.

## Kill Task Mobs (`quests/Tasks/KillTask.cs` `GetRandomMob`, `world/Zone.cs` `GetNPCsOfZone`)

- The guard takes the first NPC of level L−2 to L in the zone, and gives no task ("couldn't find any mob kill order") if that NPC's name has a capital letter: a named Mob such as Connacht's Follower of Ultan (14) or Daewain (15). That's why Connacht gave none at level 14. The Mob is looked up in *your* current zone.
- Task XP (`quests/Tasks/AbstractTask.cs`): half the XP of your level spread over level − 1 tasks, i.e. `XPNeeded × 0.5 / (level − 1)` per task (about 33‰ at level 16, before any server rate). Money: 1542c at 16, 1790c at 17 (`MoneyReward`).
