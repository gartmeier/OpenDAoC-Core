# A play stint

What an orchestrating session gives each subagent that plays Dalta for one stint of a long, unattended run.

You play Dalta, a Hibernian Blademaster, toward level 50 on an OpenDAoC server, through the `mcp__mind__execute_code` tool. Load it first with ToolSearch "select:mcp__mind__execute_code". The orchestrator starts the next stint after you report.

Before you act, read:

- `agents/CLAUDE.md`, the rules of play;
- `agents/dalta/lessons.md` and `agents/dalta/papercuts.md`;
- `agents/learnings/*.md`;
- `agents/dalta/tactics/*.ts`.

Imports are relative to `agents/dalta/`. The wiki is in `agents/wiki/`.

## How to play a stint

- **Evaluate often.** Use short runs (10 minutes or less, timeout 12 or less), read each log, and adjust. Measure XP per minute and where the time goes, and attack the biggest waste.
- **Persist what you find:**
  - short Lessons in `agents/dalta/lessons.md`: fix or delete wrong ones, and keep it tidy and short;
  - papercuts in `agents/dalta/papercuts.md`;
  - broadly useful gotchas in `agents/learnings/`;
  - better tactics in `agents/dalta/tactics/`.
- **Commit regularly** from the repo root `C:\Users\joshu\Code\OpenDAoC-Core`, staging only files under `agents/dalta` and `agents/learnings`. Don't push. Keep line endings as the files have them. End each commit message with a blank line, then:

  ```
  Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
  Claude-Session: https://claude.ai/code/session_0186arFuEhWjdRhgjDwZaoUE
  ```

- **Rules:**
  - One `execute_code` at a time.
  - Never run anything outside `execute_code` that connects as the Character.
  - Keep each call under 25 minutes.
  - You may read the server's source (e.g. `GameServer/`) to learn game rules. Check `agents/dalta/from-source.md` first; it may already have the answer. Record every rule you learn there, with the file it came from, so the Human can add it to the Agents' knowledge and no one has to search again.
  - If a Human says `stop` in chat, stop and report.
- **When to stop:** play for about 90 minutes, then stop at a safe point (not mid-fight).

## Final report

Twelve lines or fewer, covering:

- level and ‰, deaths, money, gear and specs;
- where you are, and any open task;
- the time breakdown, and the XP per minute of what you tried;
- what worked best;
- the Lessons and papercuts you added, and your commits;
- what the next stint should do.
