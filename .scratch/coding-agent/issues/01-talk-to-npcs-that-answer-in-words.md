# 01: Talk to NPCs that answer in words

**What to build:** Porcelain for an NPC that answers in words instead of a merchant window, such as a trainer. In Dalta's run, `talkTo` timed out on Ea, the Blademaster trainer, because she has no merchant window. Her dialog still opened, and she asked "…life as a [Blademaster]?". Dalta then had to target her and send `/whisper Blademaster` by hand. See the spec's acceptance comments: `.scratch/coding-agent/spec.md`.

A trainer answers in a message (a popup or say) whose text starts with its name, "Ea says, …". The `message` Percept has no speaker, as a client gets none, so the porcelain goes by the text, as a Human reading the client does. A [keyword] in the words is answered by whispering it to the NPC while it's the target, within 512 units, as clicking the keyword does in the client.

- **`talkTo(agent, npcId)`** succeeds once the NPC answers in either form: its merchant window, as now, or a `message` Percept after the `talkTo` whose text starts with the NPC's name. It still fails with `outOfView`, `tooFar` or `timeout`, and the Outcome stays bare. The words reach the coding agent in the call's Chat, and a snippet can read them from the `message` Percept. `buy` and `sell` keep working on merchants as they do now.
- **`whisper(agent, npcId, keyword)`**, a new porcelain Tactic and snippet global. It gets within whispering range, targets the NPC, whispers the keyword, and succeeds once the NPC's words come back, as `talkTo` recognises them. It fails with `outOfView` (the NPC isn't in view), `tooFar` (it can't get within range) or `timeout` (no words came).
- **The guide** (`agents/CLAUDE.md`) and the `execute_code` tool description list `whisper` with the other porcelain Tactics.
- **The fighting Learning** (`agents/learnings/fighting.md`) gets a "Promoting" section after Training, from Dalta's Lesson: at level 5, find the class trainer in the Wiki, `talkTo` it, then `whisper` the class's name.

**Blocked by:** None (can start immediately)

**Status:** ready-for-agent

- [ ] `talkTo` succeeds on a trainer's words, on a merchant window, and fails with `timeout` when the NPC says nothing (tests on the fake Agent, beside the existing trade tests)
- [ ] Words from another NPC, or words from before the `talkTo`, don't count as the NPC's answer
- [ ] `whisper` targets the NPC, sends the whisper and succeeds on its words. It fails with `outOfView`, `tooFar` and `timeout` (tests on the fake Agent)
- [ ] `whisper` is a snippet global, and the snippet-globals declaration the Learnings test checks against has it
- [ ] The guide and the tool description list `whisper`
- [ ] `fighting.md` has a Promoting section whose snippet typechecks in the Learnings test
