# Dialogs

Status: resolved

## Problem Statement

Dalta, a coding agent, died and lost Constitution: "You've lost some constitution, go to a healer". Talking to the Healer cured its resurrection sickness. The Healer then asked "It will cost ... to have your lost constitution restored. Do you accept?" in a Dialog with Accept and Decline. A Human sees that window and clicks Accept. The Mind got the server's Dialog only as a bare Percept with no text, and has no Primitive to answer it. So Dalta's max health stayed at 172 instead of 179, and it couldn't even tell how much Constitution it had lost. A Human sees that too: Constitution turns red in the stats window. Many other NPCs ask the same kind of question (Enchanters, Blacksmiths, respecs, quests), and the server's typed Dialogs (group and guild invites) are just as invisible.

## Solution

The Harness turns every Dialog the server opens into a Dialog Percept with its text and whether it asks Accept or Decline or only says OK. It also turns the server's stats window into a stats Percept with the lost Constitution. The Self keeps the open Dialog as `agent.self.dialog`, and the lost Constitution as `agent.self.constitutionLost`. An `answerDialog` Primitive accepts or declines the open Dialog the way the real client does, and a porcelain `answer(agent, accept)` lets a Tactic do it in one line. A Mind that takes over is told the lost Constitution and any Dialog still open. Dalta can then talk to a Healer, accept, and see its lost Constitution go to 0.

## User Stories

1. As the Human, I want to record a capture with the real 1.127 client of a Character accepting and declining a Healer's Dialog, so that the answer packet is built byte for byte as the client sends it.
2. As an Agent, I want to know when the server opens a Dialog, so that I don't miss a question an NPC asks me.
3. As an Agent, I want a Dialog's text, so that I know what I'm accepting, such as what a Healer charges.
4. As an Agent, I want to know whether a Dialog asks Accept or Decline or only says OK, so that I know whether there's a choice to make.
5. As an Agent, I want to accept a Dialog, so that a Healer restores my lost Constitution.
6. As an Agent, I want to decline a Dialog, so that I can refuse a cost or offer I don't want.
7. As an Agent, I want my answer to go through the same handler a client's does, so that the server treats it exactly as a Human's click.
8. As an Agent, I want the open Dialog kept on my Self, so that a Tactic can read it after the `talkTo` that opened it.
9. As an Agent, I want a newer Dialog to replace the open one, so that I always see the question the server is waiting on, as the server declines the older custom Dialog itself.
10. As an Agent, I want the Dialog gone from my Self once I've answered it, so that I don't answer a question twice or believe it's still asked.
11. As an Agent, I want no Dialog on my Self before the server opens one, so that what I believe comes only from what I was told.
12. As an Agent, I want the server's typed Dialogs, such as a group or guild invite, told and answered the same way, so that one Percept and one Primitive cover every Dialog.
13. As an Agent, I want to know how much Constitution I've lost, so that I know when to visit a Healer.
14. As an Agent, I want my lost Constitution to go to 0 after a Healer restores it, so that I can check the Dialog worked.
15. As an Agent, I want my lost Constitution after only some points were restored, so that I know when I couldn't afford them all.
16. As an Agent, I want to know nothing about my lost Constitution before the server tells me, so that I don't believe it's 0 when it isn't.
17. As an Agent, I want my lost Constitution at world entry, so that I know about it after logging back in.
18. As an Agent whose Mind takes over, I want the new Mind told my lost Constitution, so that it doesn't start blind to it.
19. As an Agent whose Mind takes over while a Dialog is open, I want the new Mind told that Dialog, so that it can still answer the question the server is waiting on.
20. As an Agent whose Mind takes over after I answered a Dialog, I want the new Mind not told it again, so that it doesn't answer it twice.
21. As an Agent writing a Tactic, I want `answer(agent, accept)`, so that I don't have to echo the Dialog's code and data myself.
22. As an Agent writing a Tactic, I want `answer` to fail with a reason when no Dialog is open, so that I can tell a Healer didn't ask anything, such as when no Constitution was lost.
23. As an Agent writing a Tactic, I want to wait until a Dialog is open after `talkTo`, so that I answer only once the question has come.
24. As an Agent, I want an `answerDialog` with fields out of range refused at dispatch with a reason, as other Primitives are, so that I learn about my mistake straight away.
25. As an Agent, I want a Learning that shows a Healer's Dialog answered, so that I know Dialogs exist and how to answer them.
26. As a Human studying the Journal, I want the Dialog and stats Percepts and the `answerDialog` Primitive recorded with their contents, so that I can see what the Agent was asked and how it answered.
27. As the Human, I want Dialog in the glossary, so that the Agents, the docs and I use one word for these windows and never confuse them with popup chat messages.
28. As the Human, I want Dalta's Healer papercut removed once it's fixed, so that the papercuts list only what's still missing.

## Implementation Decisions

- **Glossary.** CONTEXT.md already has **Dialog**: a question the server puts to a Character in a window, answered with Accept or Decline, or only acknowledged with OK. _Avoid_: popup, prompt. The **Self** entry already includes the lost Constitution and the open Dialog, plus the exception that an answered Dialog is known from the Primitive the Mind sent.
- **Capture first.** The Human records a capture with the real 1.127 client, with markers said before each part:
  - a Character that lost Constitution talking to a Healer and accepting;
  - after another death, talking to the Healer and declining.

  It's checked in with the other 1.127 fixtures, with a section in their README, and the answer packet is built against it. It settles what the client writes in the fields the server reads and in the ones it doesn't.
- **Dialog Percept.** A new Percept, kind `dialog`. It mirrors the server's Dialog packet: the dialog code, data1 to data3, whether it asks yes or no (or only OK), and the text. Both of the server's calls that write that packet project to it: the custom Dialog call and the typed dialog box call. The custom Dialog's fields are the ones its packet writes: the custom code, the Agent's session ID, the custom marker, and yes/no when it has a response callback.
- **stats Percept.** A new Percept, kind `stats`, from the server's stats window call. It carries only the lost Constitution, which the 1.127 packet sends as its own byte. The rest of the stats window is left out until a papercut asks for it.
- **`answerDialog` Primitive (ADR 0002).** `answerDialog { code, data1, data2, data3, accept }` sends the dialog response packet, built as the captured client sends it and handled by the real handler on the client tick. The Mind echoes the fields the Dialog Percept carried, as the client does. Fields out of range are refused at dispatch with a reason, like the other Primitives' are. Whether the server then does anything with it isn't known at dispatch (ADR 0004).
- **Takeover.**
  - The calls the Harness makes to tell a new Mind what world entry tells a client gain the stats window call, where world entry makes it.
  - The Harness remembers the last Dialog Percept it passed to the Mind. It forgets it when it dispatches `answerDialog`. A new Dialog replaces it. A Takeover then tells the new Mind the remembered Dialog again, after the world-entry calls, since the server keeps no copy of the text to resend. This is the one piece of new Harness state.
- **Self.**
  - `dialog`: the last Dialog Percept's contents. It's replaced by each new one and absent before the first. It's cleared when the Mind sends `answerDialog`, as the Inventory follows the Primitives the Mind sent for the merchant it's talking to.
  - `constitutionLost`: the number from the last stats Percept, absent before the first.
- **Porcelain.** A new porcelain Tactic, `answer(agent, accept)`, joins the snippet globals.
  - It fails with reason `noDialog` when the Self has no open Dialog.
  - Otherwise it sends `answerDialog` with the open Dialog's echoed fields and succeeds once the Primitive is queued.
  - It doesn't wait for the server's reply. The chat that follows says what happened.
- **Protocol.** Both Percepts and the Primitive are added to the Mind protocol, and the TypeScript types are regenerated, like every other.
- **Learning.** The trading Learning gains a short section on Dialogs. Its snippet talks to a Healer, waits for `agent.self.dialog`, reads its text, answers it, and checks `agent.self.constitutionLost`. It also notes that a Healer asks nothing when no Constitution is lost.
- **Docs.** Dalta's "Healer's Constitution dialog can't be answered" papercut is removed. Dalta writes its own Lesson about Healers. No ADR: nothing here is hard to reverse.
- **Code placement.** As before: the Harness folder and namespace, no core code changes. The Self, the porcelain and the Learning change on the Mind side.

## Testing Decisions

- **Good tests** check behaviour seen from outside, at the seams that exist:
  - a server call in, a Percept out
  - a Primitive in, a queued packet or a refusal out
  - the calls a Takeover makes, and what it tells the new Mind
  - Percepts and sent Primitives in, beliefs on the Self out

  They don't check how the Harness stores the remembered Dialog.
- **Percept projection (existing seam).**
  - The custom Dialog call, with and without a response callback, gives a `dialog` Percept with the custom code, the session ID, the custom marker, yes/no or OK, and the text.
  - The typed dialog box call gives one with its code, data, type and text.
  - The stats window call gives a `stats` Percept with the test player's lost Constitution, 0 when none is lost.
  - Prior art: the max speed and buff bar projection tests.
- **Packet builders (existing seam).** Golden bytes for the dialog response, accepting and declining, against the new capture. Prior art: the item and merchant builder tests.
- **Harness dispatch and Takeover (existing seam).**
  - `answerDialog` queues the dialog response packet.
  - Fields out of range are refused with a reason.
  - A Takeover makes the stats window call in its place.
  - It tells the new Mind an open Dialog after the world-entry calls, the newest when two came, and none once `answerDialog` was dispatched.
  - Prior art: the `sell` and `talkTo` dispatch tests, the refusal cases, and the Takeover test that expects the buff bar call.
- **Protocol (existing seam).** The new Percepts and the Primitive round-trip as JSON, and the codegen test fails until the TypeScript is regenerated.
- **Self (existing seam).**
  - No Dialog and no lost Constitution before the first Percept.
  - A `dialog` Percept opens one, a later one replaces it, and sending `answerDialog` clears it.
  - A `stats` Percept sets `constitutionLost`, and a later one replaces it.
  - Prior art: the Self's Effects and max speed tests, and the Inventory's merchant tests for a belief that follows a sent Primitive.
- **Porcelain (existing seam).** On the test Agent, `answer` sends `answerDialog` with the open Dialog's fields and `accept`, and fails with `noDialog` when none is open. Prior art: the trade porcelain tests.
- **Learnings (existing seam).** The new snippet typechecks as an `execute_code` snippet.
- **Not tested automatically:** the live server and the coding agent. The Human checks them by playing Dalta: after a death, talk to a Healer, answer the Dialog, and see `constitutionLost` go to 0 and max health come back.

## Out of Scope

- The rest of the stats window: base stats, buffs, item bonuses and caps, resists, max health from the stats packet.
- Custom text windows and the `/task` papercut. The text window is already projected as `examined`.
- Dialogs that need more than Accept or Decline, such as the trade window, the emblem dialog and housing dialogs, which come as their own packets.
- Waiting for the server's reply in `answer`, or telling from chat whether an answer worked.
- Grouping or joining guilds. The typed Dialogs can be answered, but nothing more about groups or guilds is added.

## Further Notes

- **Captured on 2026-10-25** in `healer-dialog.tsv`, checked in with its README section (Dialogs). The Human, possessing Dalta, talked to a Healer and declined, then talked to it again and accepted. The dialog response has parameter 0xFFFF, unlike the item packets' 0x0000. Its 8 bytes are data1 to data3 as the server's Dialog sent them (`0003` session ID, `0001` custom marker, `0000`), the code `06` and the response, `00` to decline and `01` to accept. That's the order the handler reads them in.
- **Found while implementing.**
  - The Harness's packet library replaces the real one, so it does what the real one does to game state too. For a custom Dialog, it declines the one still open and hands the player the new callback, which the response handler calls. Without that, accepting did nothing. For a group or guild invite, it leaves the inviter's name, which the handler checks before it acts.
  - Group and guild invites, leaving a guild, and taking or aborting a quest each write the Dialog packet through a call of their own, not the typed dialog box call. They project to the Dialog Percept too.
  - A portal stone opens the realm's warmap through the typed dialog box call. It's a map rather than a question, so it gives no Percept.
  - In Dalta's in-game check, max health stayed at its old value after the Healer restored Constitution, until the next status update. The Healer sends only the stats packet, which also carries max health, so the stats Percept carries it too and the Self takes it from there.
  - The Journal describes the Dialog and stats Percepts and the `answerDialog` Primitive. The guide and the `execute_code` tool description list `answer` among the porcelain.
- The server declines an open custom Dialog itself when it opens another, so the Self keeping only the newest one matches what the server waits on. A typed Dialog and a custom one open at once would leave only the newest on the Self. That's not expected while Agents play alone.
- World entry already sends the stats window, so a newly entered Agent gets its lost Constitution without any change to world entry.
- A Healer restores as many points as the Character can afford, at least one. After a partial restore, `constitutionLost` shows what's left.
- Relevant decisions:
  - ADR 0001: Percepts only as a client would know them. The stats packet carries the lost Constitution, and the Dialog packet carries the text.
  - ADR 0002: `answerDialog` goes through the real dialog response handler.
  - ADR 0004: a Primitive's result reports dispatch only.

## Comments

**2026-10-25 (agent):** Dalta's in-game check:

1. After a death at level 20, `agent.self.constitutionLost` read 5.
2. Dalta talked to Gralon, a Healer in Howth. The Journal shows the `dialog` Percept (CustomDialog, yes or no) with "It will cost 96 silver, 30 copper to have your lost constitution restored. Do you accept?", and `answer(agent, true)` sent `answerDialog` accept.
3. 32 ms later the money went down by 9,630 copper, chat said "Gralon restores 5 points of your lost constitution.", and the `stats` Percept said 0 lost, so `constitutionLost` went 5 → 0.
4. Max health stayed at 488 until the next status update brought 496. bdb200715 fixed that: the `stats` Percept now carries max health too. It's covered by the tests but hasn't been seen in game yet.

Dalta wrote a Lesson about Healers. Its Healer and Constitution papercuts are deleted, and its note on `/stand` became a Lesson. Resolved.
