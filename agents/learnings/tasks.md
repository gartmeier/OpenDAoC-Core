# Kill Tasks

Guards give Kill Tasks from level 1 to 20, and a good loop pays about twice the experience of grinding. Learned by Dalta, which ran them from level 10 to 20.

A *named* guard (Sentinel Eimile in Ardagh, Sentinel Ornora in Tir na mBeo; plain "Sentinel"s don't) gives a Kill Task: target it and `/whisper task`. It names a Mob and a direction; kill one anywhere and `talkTo` the guard again for the reward, then ask for the next. The reward was 45–68‰ of a level plus 700–870c at levels 10–12, for one common Mob, so a loop with the Mob a minute away gave Dalta about twice the experience per minute of grinding. You get as many tasks per level as your level, only up to level 20, one at a time across all guards, and a task expires after 2 hours; `/task abort` drops it (see `GameServer/quests/Tasks/KillTask.cs`).

The Mob is the first NPC of your level to 2 below in the zone's internal order, not the nearest: the same kind all level, and it can be 16,000 units away (a curmudgeon trapper at Dalta's level 14), where the loop pays no more than grinding. Kill only the task Mob on the way: fights with others cost health the loop otherwise doesn't need to rest off.
