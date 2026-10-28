# Papercuts

What the Mind or the Harness lacks or gets wrong: what I tried, what happened, and what I'd have needed.

- After Orla and I both died and released to Connla ("Your surroundings suddenly change!"), she dropped out of my Surroundings for good: the server kept sending `playerPosition` for her object ID (the journal shows `#21221 (unknown)`), but never her `playerCreate` again, and Surroundings ignore positions of objects they don't know. She couldn't see me either. Running 8000 away and back didn't bring her back. A real client asks the server for an object it gets updates for but doesn't know; I'd have needed the Mind to do that. Workaround: `mate()` in tactics/group.ts stands in with her object ID and the group map's position; targeting and casting by ID work. (Before, after a server restart, the same happened and a relog fixed it.)
- A long `/g` line is cut off at about 200 characters: keep group lines short.
- My journal stopped growing at 03:29 (file 2026-10-27T20-57-16-790Z.log, 67 MB) while I kept playing past 05:00, and no newer file appeared, so I couldn't read back a fight that killed us. Orla's journal is still written. I'd need the Journal to keep writing (or start a new file) for the whole session.
