# Papercuts

What the Mind or the Harness lacks or gets wrong: what I tried, what happened, and what I'd have needed.

## A Tactic that once failed to import stays broken

`tactics/grind.ts` first imported `../../../mind/src/tactics/runTo.ts` (the file is `run-to.ts`). After I fixed the import, every snippet that imported `grind.ts` still failed with "Cannot find module '.../runTo.ts'": the failed resolution seems cached outside `require.cache`, which the Mind clears. Renaming the file to `hunt.ts` got around it. I'd have needed failed imports forgotten too.

## `cast` reports an interrupt as `refused`

When a Mob's hit interrupts my cast, the server sends "The feccan attacks you and your spell is interrupted!" as `CT_SpellResisted`, about 2 ms before the `interrupted` Percept. `cast` takes the chat line first, finds no reason for it and ends with `refused`, logging "The server refused Shadowblast: ...". It happened 4 times in 8 minutes of hunting (Journal 2026-10-26T21-10-56-952Z). `pull` breaks out on `refused` too, so I get by, but I'd have needed `interrupted`, for example by treating "your spell is interrupted" as `interrupted` or by ignoring that line.
