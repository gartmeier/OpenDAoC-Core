# 01: Talk to NPCs that answer in words

**What to build:** Porcelain for an NPC that answers in words instead of a merchant window, such as a trainer. In Dalta's run, `talkTo` timed out on Ea, the Blademaster trainer, because she has no merchant window. Her dialog still opened, and she asked "…life as a [Blademaster]?". Dalta then had to target her and send `/whisper Blademaster` by hand. See the spec's acceptance comments: `.scratch/coding-agent/spec.md`.

A trainer answers in a message (a popup or say) whose text starts with its name, "Ea says, …". The `message` Percept has no speaker, as a client gets none, so the porcelain goes by the text, as a Human reading the client does. A [keyword] in the words is answered by whispering it to the NPC while it's the target, within 512 units, as clicking the keyword does in the client.

- **`talkTo(agent, npcId)`** succeeds once the NPC answers in either form: its merchant window, as now, or a `message` Percept after the `talkTo` whose text starts with the NPC's name. It still fails with `outOfView`, `tooFar` or `timeout`, and the Outcome stays bare. The words reach the coding agent in the call's Chat, and a snippet can read them from the `message` Percept. `buy` and `sell` keep working on merchants as they do now.
- **`whisper(agent, npcId, keyword)`**, a new porcelain Tactic and snippet global. It gets within whispering range, targets the NPC, whispers the keyword, and succeeds once the NPC's words come back, as `talkTo` recognises them. It fails with `outOfView` (the NPC isn't in view), `tooFar` (it can't get within range) or `timeout` (no words came).
- **The guide** (`agents/CLAUDE.md`) and the `execute_code` tool description list `whisper` with the other porcelain Tactics.
- **The fighting Learning** (`agents/learnings/fighting.md`) gets a "Promoting" section after Training, from Dalta's Lesson: at level 5, find the class trainer in the Wiki, `talkTo` it, then `whisper` the class's name.

**Blocked by:** None (can start immediately)

**Status:** resolved

- [x] `talkTo` succeeds on a trainer's words, on a merchant window, and fails with `timeout` when the NPC says nothing (tests on the fake Agent, beside the existing trade tests)
- [x] Words from another NPC, or words from before the `talkTo`, don't count as the NPC's answer
- [x] `whisper` targets the NPC, sends the whisper and succeeds on its words. It fails with `outOfView`, `tooFar` and `timeout` (tests on the fake Agent)
- [x] `whisper` is a snippet global, and the snippet-globals declaration the Learnings test checks against has it
- [x] The guide and the tool description list `whisper`
- [x] `fighting.md` has a Promoting section whose snippet typechecks in the Learnings test

## Comments

**2026-10-24 (agent):** Done. Not yet tried on the live server: the tests run on the fake Agent. Where it differs from or adds to the ticket above:

- **An NPC's words are a say line** (`<speaker> says, "…"`) whose speaker is the NPC's name, in any case and with or without its article (`The town guard says, …`), as the server writes them. A message that only starts with the name, or another NPC whose name starts with it, doesn't count. The say-line parsing that hears `stop` now shares this, in `mind/src/say-line.ts`.
- **`talkTo`'s constants are renamed** to `TALK_CLOSE_IN` and `ANSWER_TIMEOUT_MS`, since they're no longer only a merchant's.
- **`whisper` runs to 400 units** from an NPC farther than 512, and sends `target` and then `/whisper <keyword>`.
- **The Mind's README** lists `whisper` too.
- The approach (run within range, else `tooFar`) is now in `talkTo`, `whisper` and `kill` alike; left as it is.
