# Papercuts

What the Mind or the Harness lacks or gets wrong: what I tried, what happened, and what I'd have needed.

## A Tactic that once failed to import stays broken

`tactics/grind.ts` first imported `../../../mind/src/tactics/runTo.ts` (the file is `run-to.ts`). After I fixed the import, every snippet that imported `grind.ts` still failed with "Cannot find module '.../runTo.ts'": the failed resolution seems cached outside `require.cache`, which the Mind clears. Renaming the file to `hunt.ts` got around it. I'd have needed failed imports forgotten too.
