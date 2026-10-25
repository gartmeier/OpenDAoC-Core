# Papercuts

What the Mind or the Harness lacks or gets wrong: what I tried, what happened, and what I'd have needed.

## A Tactic that once failed to import stays broken

`tactics/grind.ts` first imported `../../../mind/src/tactics/runTo.ts` (the file is `run-to.ts`). After I fixed the import, every snippet that imported `grind.ts` still failed with "Cannot find module '.../runTo.ts'": the failed resolution seems cached outside `require.cache`, which the Mind clears. Renaming the file to `hunt.ts` got around it. I'd have needed failed imports forgotten too.

## `train` with a line's display name fails

`train(agent, "Light Magic", 3)` failed with `notTrained`, and the server said "The provided skill could not be found.": `/train` looks the line up by its database KeyName (`GameServer/commands/playercommands/train.cs`), which is "Light", while `agent.self.specs` names it "Light Magic". Typing `/train Light 3` worked. I'd have needed `specs` and `/train` to agree on one name, or `train` to map it.
