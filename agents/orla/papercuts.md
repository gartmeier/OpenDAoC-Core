# Papercuts

What the Mind or the Harness lacks or gets wrong: what I tried, what happened, and what I'd have needed.

## A Tactic that once failed to import stays broken

`tactics/grind.ts` first imported `../../../mind/src/tactics/runTo.ts` (the file is `run-to.ts`). After I fixed the import, every snippet that imported `grind.ts` still failed with "Cannot find module '.../runTo.ts'": the failed resolution seems cached outside `require.cache`, which the Mind clears. Renaming the file to `hunt.ts` got around it. I'd have needed failed imports forgotten too.
## A `moveTo` without `z` hangs `runTo` for 5 minutes

I called `runTo(agent, { x: 296400, y: 637000 })` with no `z`. The Journal says "Sending moveTo X:296400 Y:637000 Z:NaN. The moveTo wasn't sent: The connection closed first. Refused: ... MoveToPrimitive was missing required properties including: 'z'." `runTo` still waited its full 5 minutes and returned `timeout`, and I never moved. Passing `z` is on me, but I'd have needed `runTo` to fail at once when the Primitive isn't sent, or the type to require `z`.

## `cast` casts before the stand has taken

A feccan came at me just as `rest` sat me down, and `rest` sent `stand` 33 ms after `sit`. `pull` then called `cast`, which sent `stand` again and `castSpell` right away, without waiting. The server answered "You can't cast while sitting!" (`refused`), the Quick Cast after it too, and I fought with the staff at half health. "You stand up" came only seconds later (Journal 2026-10-26T21-25-24-011Z, around #77743–#77757). I'd have needed `cast` to wait until the server says I'm standing before it casts.
