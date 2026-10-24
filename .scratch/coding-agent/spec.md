# Milestone 5: A coding agent plays

Status: ready-for-agent

## Problem Statement

Milestones 1 to 4b built an Agent's body: a Harness that logs a Character in, Primitives to move, fight, train and trade, Percepts that say what a client would see, beliefs built from them, and porcelain Tactics on top. Every decision so far was made by Tactics a Human had an LLM write, started by saying `come`, `grind` or `shop`.

No LLM has played yet. ADR 0009 says a stock coding agent, such as Claude Code, plays by running TypeScript against the Mind, writes its own Tactics, and keeps its own notes and Lessons as files. I want to see how it copes: hand it a fresh level 1 Character with level 50 as its goal, watch where it gets stuck, improve the Harness, the Mind and what it reads, and start again with a fresh Character.

Today a coding agent couldn't even start. The Mind is a command-line process that logs the Character out when it exits, so a script per idea would log the Character in and out each time. There's nothing to teach it the workflow or the game, and nowhere for its own Tactics and Lessons to live.

## Solution

The Mind becomes a stdio MCP server with one tool, `execute_code`, as rs-sdk's is. Claude Code starts it from the Agent's folder, it logs the Character in, and the Character stays in the world for the whole session. Each call runs a TypeScript snippet against the Agent, with the porcelain Tactics at hand, and returns its console output, its result or error, a short summary of Self, Surroundings and Inventory, and the chat since the last call.

Following rs-sdk, the coding agent reads three kinds of shared material:
- a guide that teaches the tool, the API and the workflow;
- Learnings: hand-written working patterns and gotchas from milestones 4 and 4b;
- a Wiki generated from the server's database: each zone of a realm with its Mobs and NPCs, and each merchant with its wares.

Each Agent has its own folder for its Tactics, Lessons, papercuts and Journal. The `come`, `grind` and `shop` stand-ins and the say commands go. Only `stop` stays, which aborts the running snippet. Everything else a Human says reaches the coding agent as chat.

Acceptance: from a fresh level 1 Character, with the Human only nudging it on when it stops, the coding agent finds its way, writes at least one Tactic and one Lesson of its own, and reaches level 5. How far it got, why it stopped each time and what it filed as papercuts are recorded, as material for the next run.

## User Stories

1. As the Human, I want to create a fresh level 1 Character on a new Agent Account with the real client, so that each run starts from nothing.
2. As the Human, I want to set up a new Agent's folder from a template and name its Character in one place, so that starting a run takes a minute.
3. As the Human, I want to start Claude Code in the Agent's folder and tell it to play, so that the coding agent takes over the Character.
4. As the Human, I want the Character to enter the world when Claude Code starts the Mind and to leave through the normal quit path when Claude Code exits, so that a session is one stay in the world.
5. As the Human, I want the Agent to stay in the world between `execute_code` calls, so that the coding agent can think between snippets without logging out.
6. As the Human, I want to say `stop` next to the Agent and have the running snippet aborted and the Agent left standing with attack off, so that I'm always in control.
7. As the Human, I want everything else I say next to the Agent to reach the coding agent as chat, so that I can talk to it.
8. As the Human, I want `execute_code` and file edits in the Agent's folder allowed without prompts, so that permission prompts don't stall it while I'm away.
9. As the Human, I want to nudge the coding agent on when it stops, so that I learn when and why it stops.
10. As the Human, I want a Journal of every Percept, Primitive and snippet written to a file in the Agent's folder, so that I can study a run afterwards.
11. As the Human, I want the coding agent's Tactics, Lessons and papercuts checked in with the Agent's folder, so that I can study what it wrote in diffs.
12. As the Human, I want an Agent's folder kept after its run, so that its Lessons can be promoted into Learnings for the next Agent.
13. As the Human, I want to generate the Wiki for a realm with one admin command, so that it's redone when the world's data changes.
14. As the Human, I want every snippet in the Learnings checked by a test, so that a Learning never teaches code that doesn't compile.
15. As the Human, I want the stand-ins and say commands removed, so that the coding agent starts from the porcelain and nothing tells it how to grind.
16. As the Human, I want Primitives and Percepts unchanged by this milestone, so that the coding agent's interface is the one milestones 3 to 4b proved, and new ones are added only when it turns out to need them (ADR 0009).
17. As the coding agent, I want one tool that runs a TypeScript snippet with top-level `await` against the Agent, so that I can try an idea in a few lines.
18. As the coding agent, I want the Agent and the porcelain Tactics (`kill`, `rest`, `train`, `runTo`, `equip`, `talkTo`, `buy`, `sell`) available to every snippet, so that I don't import them each time.
19. As the coding agent, I want to import my own Tactics from my folder in a snippet, so that what I wrote earlier builds up.
20. As the coding agent, I want a snippet's `console.log` output in the result, bounded to its first and last lines, so that I see what it printed without flooding my context.
21. As the coding agent, I want the snippet's return value in the result as JSON, so that I can read what it found.
22. As the coding agent, I want an error's message, the first lines of its stack and how long the snippet ran, so that I can fix it.
23. As the coding agent, I want a summary of my level, experience, health, position and status, so that I see where I stand after every call.
24. As the coding agent, I want the nearest NPCs and players with their level, health and distance in the summary, so that I see what's around me.
25. As the coding agent, I want what I wear, how full my backpack is, my money and my weight in the summary, so that I know what I carry.
26. As the coding agent, I want the chat and game messages since my last call in the result, so that I hear what was said and what happened.
27. As the coding agent, I want a timeout per call, two minutes unless I say otherwise and at most sixty, so that I can run a long grind once it works.
28. As the coding agent, I want a new call to abort the one still running, so that I'm never fighting my own earlier snippet.
29. As the coding agent, I want to be told when a snippet was aborted by a timeout, a new call or the Human's `stop`, so that I know why it ended.
30. As the coding agent, I want to be told when the Agent isn't in the world and have the next call log it back in, so that a server restart doesn't end my session.
31. As the coding agent, I want a guide that explains the tool, the snippet's environment, where the API is documented and the workflow, so that I can start playing on my own.
32. As the coding agent, I want the guide to say my goal is level 50 and that I play as a normal player, so that I know what to aim at and what I may do.
33. As the coding agent, I want the guide to tell me to start with short snippets and save what works as Tactic files, so that I build on what I proved.
34. As the coding agent, I want to keep Lessons in a file I read before I next act, so that what I learned survives my context being compacted.
35. As the coding agent, I want a papercuts file to note what the Harness or Mind lacks, so that the Human can grow it.
36. As the coding agent, I want Learnings on fighting, trading and moving, with working snippets, so that I don't rediscover what earlier milestones found.
37. As the coding agent, I want a Wiki page per zone listing its Mobs with their level range and where they spawn, so that I can choose where to hunt.
38. As the coding agent, I want each zone page to list its other NPCs with their role and position, so that I can find merchants and trainers.
39. As the coding agent, I want a Wiki page per merchant listing its wares with level, price and type, so that I can plan what to buy.
40. As the coding agent, I want a Wiki index of the realm's zones with their Mob levels, so that I know where to go as I level.
41. As a Mind developer, I want the snippet runner built on the existing Agent, Runner and fake Agent, so that it's tested like the Tactics.
42. As a Mind developer, I want an aborted snippet's sends and waits never to come back, so that a snippet that catches every error can't spin after it was aborted.
43. As a Mind developer, I want the Journal lines to be the ones the Mind prints today, so that the record reads as it always has.
44. As a Mind developer, I want the Wiki pages built from the world the server has loaded, so that they show what the Agent will meet.

## Implementation Decisions

- **Vocabulary.** The root glossary now has **Learning** and **Wiki**, and says a Lesson belongs to one Agent. ADR 0006 now counts fan-wiki game data as static knowledge. ADR 0009's consequences now record the MCP Mind and that a restart is a new Agent.
- **The Mind as an MCP server.**
  - A stdio MCP server replaces the command-line entry point. It takes the Character's name as its argument and the server address and token from the Mind's env file as before.
  - It connects when it starts, so the Character enters the world when Claude Code starts it. When Claude Code closes its input, it stops any snippet and quits the Character through the normal quit path, as Ctrl+C did.
  - Who gets in is unchanged (ADR 0005): a second Mind takes over, and a linkdead Character is retried until it's gone.
  - When the connection has ended, `execute_code` says so, and the next call connects again before it runs.
  - Nothing goes to stdout but MCP messages. Lines that went to the console go to the Journal.
- **`execute_code { code, timeout? }`.**
  - `timeout` is in minutes: 2 by default, clamped to 0.1 to 60, as rs-sdk's is.
  - The snippet is transpiled with Bun and run as an async function with top-level `await`. Its globals are `agent` (the Agent, with `self`, `surroundings`, `inventory`, `send`, `waitFor`, `sendAndWait` and `run`) and the porcelain Tactics. It may `await import()` Tactic files from the Agent's folder. Nothing sandboxes it (ADR 0003).
  - One snippet runs at a time, on the existing Runner. A new call, the timeout, or a `stop` said next to the Agent aborts it, and the Agent is left standing with attack off, as the Runner does now.
  - Each snippet gets its own handle on the Agent. Once it's aborted, that handle's sends and waits never resolve, as rs-sdk does after an abort, so a loop that swallows errors stops instead of spinning. The next snippet's handle works as normal.
  - The result, in order:
    1. **Console:** what the snippet logged, bounded to its first and last 300 lines and 2000 characters a line.
    2. **Result** as JSON, or **Error** with its message, the first lines of its stack and the time it ran. An abort is an error that says what aborted it.
    3. **State:** level, experience, health, endurance, position, zone and status from Self; the nearest NPCs and players with name, level, health and distance from Surroundings; what's equipped, backpack slots used, money and weight against capacity from Inventory.
    4. **Chat:** every `message` Percept since the last call, newest 50.
  - The tool result is marked as an error when the snippet failed or was aborted.
- **Say commands.** Only `stop` is heard, from anyone but the Agent itself. The `come`, `grind` and `shop` Tactics, the `test-town` fixture and the say-command parsing for them are deleted. The porcelain stays as it is.
- **Journal.** One file per session in the Agent's folder, named by when it started, ignored by git. Each Percept and Primitive gets the line the Mind prints today, and each snippet gets a line with its code when it starts and one with how it ended. The writer is passed in, so tests can read it.
- **Shared material,** in one `agents` folder at the repo root:
  - **The guide,** a `CLAUDE.md` Claude Code loads from the Agent's folder's parent. It covers what `execute_code` does and returns, the snippet's globals, where the Mind's source documents the API (Primitives and Percepts in the generated protocol types, Self, Surroundings, Inventory, the Agent and the porcelain), the workflow (start with snippets of seconds, save what works as Tactic files, read `lessons.md` before acting and add to it, file papercuts, background nothing, `stop` aborts), the goal of level 50, and that it plays as a normal player. It holds no game knowledge.
  - **Learnings,** one markdown file each for fighting (kill, rest, train, release after death), trading (talkTo, buy, sell, equip, what merchants won't take) and moving (runTo, doors, the navmesh's gaps), written as snippets for `execute_code`. They're distilled from milestones 4 and 4b, Connla's merchants included.
  - **The Wiki,** generated, checked in.
  - **A template Agent folder** with the MCP config naming its Character, Claude Code settings, an empty `lessons.md`, `papercuts.md` and `tactics/`. The Human copies it to `agents/<name>/` and sets the Character's name.
- **Per-Agent folder.** Claude Code runs from it. Its settings allow the Mind's `execute_code` tool and edits and writes inside the folder. Its Tactics are TypeScript in the shape of ADR 0008 and import the porcelain from the Mind.
- **The Wiki generator.**
  - `/agent wiki <realm>` writes the realm's pages from the world the server has loaded, as static game data (ADR 0006).
  - An index lists the realm's zones with the level range of their Mobs.
  - A page per zone lists its Mobs grouped by name, with their level range, how many there are and where they spawn, and its other NPCs with their guild or role and position.
  - A page per merchant lists its wares by page, with name, level, armour factor or DPS, object type and price.
  - The pages are built by a function from the world's objects to markdown, separate from writing files, so it can be tested on world doubles.
- **Code placement.** As before: the generator in the Harness area and an admin subcommand, no other core changes. The MCP server, snippet runner and Journal go in the Mind project, which gains the MCP SDK as a dependency.

## Testing Decisions

- **Good tests** check behaviour seen from outside: a snippet, Percepts and time in; the tool's text, Primitives and Journal lines out. They don't reach into how the runner stores state.
- **`execute_code` (new seam, the main one).** The tool's handler, called in-process as the stdio server would call it, on the fake Agent and fake clock:
  - a snippet's console output, result and error come back, bounded as specified;
  - the summary reflects Self, Surroundings and Inventory, and the chat is only what came since the last call;
  - porcelain Tactics and the Agent are globals;
  - the timeout, a new call and a heard `stop` each abort the running snippet, say why and leave the Agent standing with attack off;
  - an aborted snippet's sends and waits never resolve, and the next snippet runs normally;
  - `stop` from the Agent itself is ignored;
  - a call while disconnected says so;
  - the Journal gets the Percept, Primitive and snippet lines.
  - Prior art: the Tactic tests on the fake Agent, and the Runner's tests.
- **Wiki generation (existing C# unit-test seam).** The page-building function on world doubles: a zone with Mobs of several levels, NPCs that aren't Mobs, and a merchant with a page of wares produce the expected markdown. Prior art: the Percept projection tests and the world doubles.
- **Learnings (new docs test, as rs-sdk has).** Every `ts` fence in the Learnings typechecks against a declaration of a snippet's globals.
- **Porcelain:** the existing Tactic tests stay. The tests of the deleted stand-ins and say commands go with them.
- **Not tested automatically:** Claude Code playing on the live server, which is the manual acceptance test.
- **Manual acceptance test:**
  1. Create a fresh level 1 Character on a new Agent Account with the real client, run `/agent wiki` for its realm, and set up its folder from the template.
  2. Start Claude Code in the folder and tell it to play. The Character enters the world.
  3. Leave it, nudging it on only when it stops. Say `stop` once while a snippet runs: the snippet ends and the Agent stands.
  4. It reaches level 5, and its folder has at least one Tactic file and one Lesson of its own.
  5. Exit Claude Code: the Character leaves the world.
  6. Record on this spec how far it got, how long it took, why it stopped each time and its papercuts.
  7. Check the server log has no speed-check entries.

## Out of Scope

- A Stop hook or loop that keeps Claude Code playing by itself. The Human nudges it, and when it stops is part of what we watch.
- A generated API reference. The coding agent reads the Mind's source.
- Item pages beyond what merchants sell, loot tables, quests and other realms' pages.
- New Primitives or Percepts. They come when the coding agent turns out to need them.
- Sandboxing snippets or the coding agent's Tactics (ADR 0003).
- Comparing the coding agent's play with the hand-written Tactics. No baseline is planned.
- Choosing a model, and any Planning or Reflection loop of our own (ADR 0009).
- Character creation by an Agent: the Human creates it.
- Several Agents at once and groups.

## Further Notes

- Relevant decisions:
  - ADR 0003: the Mind is out of process, unsandboxed, and MCP is not how Tactics reach Primitives.
  - ADR 0005: the Mind's connection is the Agent's.
  - ADR 0006: static knowledge, the Wiki included.
  - ADR 0008: Tactics are async functions.
  - ADR 0009: a stock coding agent plays.
- rs-sdk's choices this follows (see the rs-sdk research from milestone 2): the `execute_code` tool and its result, one execution per bot with a new call aborting the last, never-resolving calls after an abort, a `CLAUDE.md` guide, hand-written learnings checked by a test, and a wiki generated from the game's data.
- Claude Code loads every `CLAUDE.md` from the folder it starts in up to the repo root, so the repo's own short `CLAUDE.md` reaches the coding agent too. It's about the issue tracker and does no harm.
- The Wiki's Mob positions are spawn points, which a Human could read on a fan site. Where a Mob is right now still comes only from Percepts (ADR 0006).
- **Milestone ladder:** 1 login and logout → 2 Mind connection → 3 movement → 3b terrain and pathing → 4 hand-written grind → 4b inventory and gear (all done) → **5 a coding agent plays (this spec)**. After 5: runs, improvements and restarts, and Primitives, Percepts, Learnings and Wiki pages as the coding agent turns out to need them.

## Comments

**2026-10-24 (agent):** Implemented. Not yet played by a coding agent: the manual acceptance test is the Human's. A live check logged Greeen in through the MCP server, ran a snippet whose result showed his real State and Chat, and quit him through the normal quit path when its input closed. Where it differs from or adds to the spec above:

- **A call while out of the world logs in and then runs** in the same call, and says so in its Console. A failed login is the call's error. The first call can also wait for the login the Mind started at startup.
- **Logging in again forgets the old beliefs.** After a lost connection the Mind starts Self, Surroundings and Inventory afresh, because objects seen before a server restart are gone.
- **The State has no zone,** since Self doesn't track one. **Players near the Agent show only name and distance,** because their Percepts carry no level or health.
- **`SLOT` is a snippet global too,** for naming equipment slots. A snippet's own `console` goes to its own Console, so what an aborted snippet logs later never shows in the next one's.
- **A result that JSON can't hold** (circular, BigInt) comes back as Bun prints it, and maps and sets come back as objects and arrays.
- **Each snippet imports the Agent's files afresh,** so an edited Tactic takes effect in the next snippet.
- **`/agent wiki <realm> <folder>`** takes the folder to write to. The database leaves every zone's realm unset, so a zone without one belongs to the realm most of its region's guards, merchants and trainers belong to. That brings in shared areas such as the Thidranki battleground, and leaves out dungeon regions with no realm NPCs, such as Muire Tomb.
- **The Wiki lists at most 5 spawn points of a Mob,** spread from west to east, with how many more there are. Zone pages were up to 47 KB with all of them. It also leaves out zones with no NPCs, and tells apart zones and merchants of the same name. The index sorts zones by their lowest Mob, with each zone's merchants, and merchant pages have each item's speed.
- **The Wiki's tests run on plain records,** the pages a function of zones and NPCs, not on world doubles. Reading those records from the loaded world (which NPCs are Mobs, the pet and untargetable filters, the realm fallback) is untested, and was checked only by generating Hibernia's Wiki: 41 zones and 429 merchants. The Wiki is checked in.
- **The template's settings** add the shared folder and the Mind as directories Claude Code may read, and a 62-minute MCP tool timeout so a 60-minute snippet isn't cut short.
