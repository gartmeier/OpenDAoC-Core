# Lessons

What I've learned while playing, which I read again before I next act.

- Dalta is a Guardian who starts in Howth (Silvermine Mts., ~X:342100 Y:591600). Low Mobs (lucradans, badger cubs, water beetle larvae, large frogs, skeletal pawns L0–2) are ~4000 units south.
- With training gear, yellow Mobs killed me 3 times at level 1. Fight blue ones (my level − 1) only: `tactics/grind.ts` does that, resting to 100% between fights.
- If the Self says level 0 with nothing equipped, another Mind took over the connection and the state wasn't resent. Pass `knownLevel` to `grind`, and run 7000 units away and back to reload the Mobs nearby. The next level up resends the Self.
- After a takeover, check `agent.self.objectId`. If it's undefined, `dead` sticks and attackers go unseen: see papercuts.md for the fix.
- Loot from low Mobs sells for 1–2 copper; coin drops are the money. Howth armour (Reinforced): Kevain tacuil L2, Bevin nadurtha L3, Twm (X:342350 Y:592888) cailiocht L5 for ~1575c the set.
- Blademaster Trainer Ea is at X:351072 Y:552741 (the town ~38000 south of Howth). Promote at 5: talk to her, then `/whisper Blademaster`.
- Keep snippets under 30 minutes: the MCP call gives up after 30 minutes without a response.
- Promoted to Blademaster at level 5 with Ea in Ardagh: `talkTo` times out (a trainer has no merchant window) but still interacts, and Ea asks "[Blademaster]?". Then `target` Ea and `/whisper Blademaster`.
- Good level 4–5 camp: lough wolf cadgers (L2–3) and rat boys (L3) just west of Ardagh, ~X:349000–350000 Y:548000–550000. With a short sword they gave ~42‰ each at level 4.
- At level 4, L1 Mobs give no XP and no coins. L3 small walking rocks hit for 20 and killed me with the training sword.
- A route can stop at 191 units from an NPC up a step (Ysbail in Ardagh), just outside talking range: approach from another side.
- Bound at Ardagh's Bindstone (X:351830 Y:554515) on 2026-10-24, so deaths now bring me back to Ardagh, not Howth. Right after login `/bind` says "must wait 10 seconds": wait, then try again.
- Styles work (checked 2026-10-24 at Blades 5): Return Blade after a parry is cheap (~4% end) for +7; Shining Blade costs ~10% for +1, so keep it for when endurance is spare. Details in `learnings/styles.md`.
- Hastener Filidh Medyr stands in Ardagh (X:352100 Y:554297) and Howth (X:343403 Y:591201). `talkTo` it and it casts a speed buff at once, no whisper needed: max speed 191 → 281 for 10 minutes, but a fight ends it. Get it before a long run, not before a grind. I can't see buffs: `agent.self.maxSpeed` is the only sign it's on.
- Kill Tasks (checked 2026-10-24 at level 5): target a *named* guard (Sentinel Eimile, X:352314 Y:553334 in Ardagh; plain "Sentinel"s don't give them), then `/whisper task`. `/task` alone only shows the task you have. The guard names a Mob and a direction; kill one, then `talkTo` the guard again. One small walking rock gave 62 xp, and the reward was 287 xp + 142c. There's a limit of tasks per level.
- Small walking rocks (L3) hit for 20 and took 75% of my health at level 5, and a queued Shining Blade was never performed in that fight (sent before attack mode started?).
