# Using an item

Status: ready-for-agent

## Problem Statement

Dalta, a coding agent, runs a Kill Task loop that takes it 16000 units out to a Mob and 16000 units back. Its backpack has a Personal Bind Recall Stone, which a Human uses, by right-clicking it or clicking it on the quickbar, to go straight back to the Bind point. Dalta can't use it. No Primitive sends the packet a client sends to use an item, and `command` can't help, because the server has no `/use` command. Dalta wrote this down as the papercut "No way to use an item (2026-10-25)" and asked for a `useItem` Primitive for a backpack or equipment slot.

Even if it could use the stone, Dalta wouldn't find out that it can, or how. Examining the stone says only "Cannot be traded to other players. Cannot be sold to merchants. Cannot be destroyed." Nothing mentions what it does, its 10 s cast or its 30-minute reuse.

## Solution

One new Primitive, `useItem { slot, second? }`, uses the item in a backpack or equipment slot, as a right-click or a quickbar click does. With `second`, it uses the item's second spell, which some charged gear has. The Harness sends the client's use slot packet, built as the captured 1.127 client sends it, and the real packet handler does the rest.

Nothing else changes in the Harness or the Mind. Whether the use worked, failed while moving or in combat, or is waiting out its reuse timer, the server says in messages, which already arrive as `message` Percepts. The teleport arrives as the `jump` Percept that releasing after a death already produces.

The fighting Learning's Binding section gets a "Recalling" part: what the stone does, what it needs, which messages mean what, and a snippet that recalls.

Acceptance: Dalta, in a new Claude Code session, told that `useItem` exists and where the Learning is, recalls at least once after a Kill Task kill and lands at its Bind point.

## User Stories

1. As the Human, I want to record a capture with the real 1.127 client of Dalta using its Recall Stone, so that the new packet is built byte for byte as the client sends it.
2. As the Human, I want the capture to use the stone from the backpack and from the quickbar, so that I know whether the two send the same packet.
3. As the Human, I want the capture to use the stone while moving, so that I know what the client writes in the packet's speed and flags then.
4. As the Human, I want the capture to use the stone again right after a use that worked, so that I know the client sends the same packet on the reuse timer.
5. As the Human, I want Dalta to carry on as the same Agent, with its Character, folder, Lessons and Tactics, so that I see how it fits recalling into the loop it already wrote.
6. As the Human, I want to start a new Claude Code session in Dalta's folder and tell it about `useItem` and the Learning, so that a run starts as earlier ones did.
7. As the Human, I want no speed-check entries for Dalta in the server log, so that using an item and the teleport don't break the position the server trusts.
8. As the Human, I want the papercut struck in Dalta's papercuts with a pointer to this spec, so that the list shows what's been dealt with.
9. As the coding agent, I want to use an item by the slot I see it in in `agent.inventory`, so that I use it the way I already sell, examine and move items.
10. As the coding agent, I want `useItem` to work for backpack and equipment slots, so that I can use a stone in my backpack or charged gear I wear.
11. As the coding agent, I want `useItem` with `second` to use an item's second spell, so that I can use both charges of gear that has two.
12. As the coding agent, I want a slot number the packet can't carry refused at dispatch with a reason, so that a wild number doesn't fail silently.
13. As the coding agent, I want the server's answer to using an empty slot in the chat after my call, so that I see what a Human would see.
14. As the coding agent, I want the server's messages about the use (you must be standing still, you've been in combat recently, you must wait before using it again) in the chat after each call, so that I can judge whether it worked.
15. As the coding agent, I want to see the teleport in `agent.self.position`, so that I know I've recalled.
16. As the coding agent, I want a Learning that says the Personal Bind Recall Stone teleports me to my Bind point, so that I know it's worth using, which examining it doesn't tell me.
17. As the coding agent, I want the Learning to say that the stone takes a 10 s cast standing still, fails within 10 s of a fight and works once every 30 minutes, so that I plan my loop around it.
18. As the coding agent, I want the Learning to quote the messages for success, moving, combat and the reuse timer, so that my Tactic can tell them apart.
19. As the coding agent, I want a working snippet that recalls and waits for the jump, so that I don't have to rediscover the pattern.
20. As a Mind developer, I want the TypeScript type for the new Primitive generated from C#, so that both sides agree.
21. As a Mind developer, I want the Learning's snippet typechecked by the existing Learnings test, so that it never teaches code that doesn't compile.
22. As a Harness developer, I want the use slot packet to carry the client's position, speed, heading and target-in-view bits as the captured client writes them, so that using an item doesn't move the Character or put its target out of view.
23. As a Harness developer, I want `useItem` handled by the real packet handler on the client tick, so that the server decides everything as it does for a Human (ADR 0002).

## Implementation Decisions

- **Vocabulary.** No new glossary terms. "Item" is used in its ordinary sense. No ADR changes.
- **Capture first.** The Human records a capture with the real 1.127 client while possessing Dalta at a quiet spot, with a marker said before each part:
  1. using the Recall Stone from the backpack;
  2. dragging it to the quickbar and using it from there;
  3. using it while moving;
  4. using it again right after a use that worked.

  Done: it's checked in with the other 1.127 fixtures as `use-item.tsv`, with a "Using an item" section in their README, and the builder is written against it. No markers were said, so its parts are told apart by timing and position. It settled what reading the handler left open:
  - the layout is the use skill packet's: X, Y and Z as little-endian floats, the speed as a float, the heading, 2 bytes of flags, the slot, the type and 2 bytes of 0;
  - the slot is the inventory slot itself (40 for the first backpack slot), from the backpack and the quickbar alike;
  - the type is 1 from the backpack and 0 from the quickbar;
  - the flags carry no target-in-view bits with nothing targeted, and 0x4000 while moving.

  The server's replies aren't in an inbound capture. The Learning takes them from the server's code (see Further Notes), and the acceptance run checks them.
- **Primitive (ADR 0002).** In the Harness area, built as the captured client sends it, handled by the real handler on the client tick:
  - `useItem { slot, second? }` sends the use slot packet for that slot.
  - Without `second`, the type byte is 1, as a right-click in the backpack sends. With `second`, it's 2, which the server reads as the item's second spell. The quickbar's 0 isn't used: for a weapon or horse slot it means a click on the slot, which is out of scope.
  - The packet starts with the client's position, speed and heading, then flags with the strafing and target-in-view bits, which the server takes from it as it does from a position update. The Harness writes these as `useStyle`'s skill packet does.
- **Refusals.** Only a slot the packet can't carry, in its one byte, is refused at dispatch, with a reason in the style of the other item Primitives. The Harness doesn't track what's in each slot: an empty slot, a slot outside the backpack and equipment, or `second` on an item without a second spell all go to the server, whose reply or silence the Mind judges from Percepts (ADR 0004).
- **Feedback.** No new Percepts. The server's messages about the use come as `message` Percepts and appear in `execute_code`'s chat. The teleport comes as the existing `jump` Percept, which the Harness's movement already follows.
- **Codegen.** As before: the C# type in the Harness area, regenerated TypeScript.
- **Learning.** A "Recalling" part in the Binding section of the fighting Learning, written as an `execute_code` snippet:
  - the Personal Bind Recall Stone teleports you to your Bind point, and examining it doesn't say so;
  - it takes a 10 s cast, standing still, and fails if you've been in combat in the last 10 s, so stop, wait out the fight, then use it;
  - it works once every 30 minutes;
  - the messages for a use that worked, moving, combat and the reuse timer, quoted from the server's code;
  - a snippet that finds the stone in the backpack, uses it, and waits for the position to change, with a timeout.
- **Not changed.** Porcelain Tactics, Self, Surroundings, Inventory, examining, the Wiki, the guide and the `execute_code` summary.
- **Code placement.** As before: the Harness folder and namespace, no core code changes. Only the Learning and the regenerated types change on the Mind side.

## Testing Decisions

- **Good tests** check behaviour seen from outside: a Primitive in, a queued packet or a refusal out; packets matching what the real client sent. They don't check how the packet is assembled.
- **Harness dispatch (existing seam, the main one).** On a Character double:
  - `useItem` for a backpack slot queues a use slot packet that the handler reads back as that slot and type 1;
  - `useItem` with `second` queues type 2;
  - the packet claims the target is in view while something is targeted, as `useStyle`'s does;
  - a slot the packet can't carry is refused with a reason.
  - Prior art: the `useStyle` and `sell` dispatch tests in the Harness client tests.
- **Packet builder (existing seam).** Golden bytes for the use slot packet against the backpack use in `use-item.tsv`, masking only fields the README documents as varying. Prior art: the use skill builder test.
- **Protocol (existing seam).** The new Primitive round-trips as JSON, and the codegen test fails until the TypeScript is regenerated. Prior art: the `useStyle` protocol test.
- **Learnings (existing seam).** The new snippet typechecks as an `execute_code` snippet.
- **Not tested automatically:** the live server and the coding agent, covered by the manual acceptance test.
- **Manual acceptance test:**
  1. Record the capture with the real client, as above. Done.
  2. Start a new Claude Code session in Dalta's folder, tell it that `useItem` exists and that the fighting Learning's Binding section explains recalling, and tell it to play on.
  3. Leave it, nudging it only when it stops.
  4. Dalta recalls at least once after a Kill Task kill and lands at its Bind point.
  5. Check the server log has no speed-check entries for Dalta.
  6. Strike the papercut in Dalta's papercuts with a pointer to this spec, and record on this spec how the run went and any new papercuts.

## Out of Scope

- Fixing the server's examine text for the Recall Stone. It's set up as a potion, and the potion examine only prints spells from the potion spell line, which the stone's spell isn't in. The Learning says what the stone does instead, as a fan site would (ADR 0006).
- Refusing an empty slot, or a slot outside the backpack and equipment, at dispatch. That would need the Harness to track the items it last sent its client.
- Clicks on the weapon and horse slots, where the use type switches weapons, fires a bow or summons a horse. `useItem` sends them, but nothing is designed or tested for them.
- New Percepts for item use: the casting bar, the reuse timer, charges. They come when a papercut asks.
- A porcelain `recall` or `useItem` Tactic. Dalta writes its own from the Learning's snippet.
- Showing items' spells or charges in the Inventory.
- A fresh Agent: Dalta carries on.

## Further Notes

- Relevant decisions:
  - ADR 0001: Percepts only as a client knows them.
  - ADR 0002: Primitives through the real handlers.
  - ADR 0004: dispatch-only results, so whether a use worked comes from messages and the `jump` Percept.
  - ADR 0006: static world knowledge is fair, so the Learning can say what the stone does.
  - ADR 0009: Primitives and Percepts are added when the coding agent needs them. This one comes from Dalta's papercut.
- Facts this spec relies on, from the server and its database as they stand:
  - **The use slot handler** reads, for 1.124+ clients, the position as three little-endian floats, the speed as a float and the heading, then 2 bytes of flags, the slot as a byte and the use type as a byte. Its flags give strafing, target in view and ground target in view, as the use skill packet's do.
  - **Use types:** 0 is a click, 1 the first use and 2 the second. For most items 0 and 1 both cast the item's first spell. 2 casts its second spell. On the weapon and horse slots, 0 means a click on the slot. A magical backpack item such as the Recall Stone ignores 2 altogether, so `second` does nothing on the stone.
  - **The server's replies to using the stone,** from its code: "You use Personal Bind Recall Stone." when it starts; "You must be standing still to use this item!" while moving; "You have been in combat recently and cannot use this item!" within 10 s of combat; "You move and interrupt your spellcast!" when the Character moves during the cast; "You are already casting a spell." during another cast; and "You must wait 29 more minutes and 50 seconds to discharge this item!", or "You must wait 40 more seconds to discharge this item!", on the reuse timer.
  - **The capture** (`use-item.tsv`): the use from the backpack and the first from the quickbar were each followed by the Character moving within 2 s, and no teleport. The use while moving gave no teleport. The next standing use teleported Dalta 10.8 s later from X:341809 Y:591946 to its Bind point at X:338704 Y:444627. Three uses there, 2–5 s later, met the reuse timer.
  - **The Personal Bind Recall Stone** is a magical backpack item whose spell is Personal Recall Stone (`GatewayPersonalBind`), with a 10 s cast and a 30-minute reuse (`CanUseEvery` 1800 s). It can't be traded, sold or destroyed. Its spell fails while the Character moves, within 10 s of combat, while carrying a relic, and in RvR zones or instances on non-PvE servers.
  - **Items with uses in the database:** 2,228 of 34,545 have a first spell: potions, the Recall Stones, and charged equipment from level 12. Only 189 have a second spell, all gear of level 45 to 51.
  - **Examining the stone** gives only "Cannot be traded to other players. Cannot be sold to merchants. Cannot be destroyed.", as Dalta's Journal of 2026-10-24 shows.
  - **The teleport** reaches the client as the jump packet, which the Harness already turns into the `jump` Percept and follows in its movement.
- **Milestone ladder:** 1 login and logout → 2 Mind connection → 3 movement → 3b terrain and pathing → 4 hand-written grind → 4b inventory and gear → 5 a coding agent plays → 6 Styles (all done) → **using an item (this spec)**, from Dalta's papercuts, with the others to follow as the coding agent needs them.

## Comments
