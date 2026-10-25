# Milestone 7: Spells

Status: ready-for-agent

## Problem Statement

In milestones 5 and 6, Dalta, a coding agent, took a fresh Guardian to a level 10 Blademaster on melee and Styles. The plan in `.scratch/milestones.md` is a Druid and an Eldritch who level together to 20 and join Dalta, and the Druid starts as a Naturalist. A Naturalist has no Styles and no damage Spell. What it brings is heals from Regrowth and Concentration buffs from Nurture, and it is meant to live on them.

An Agent can't cast a Spell today. A real client casts a Naturalist's Spell through the same skill packet it uses for a Style, naming the Spell by its place in the skill list. Only `useStyle` sends that packet, and only for Styles. An Agent also can't see what it needs to cast:

- Self drops the Power and concentration the `status` Percept carries.
- `rest` waits for health only, so a caster that rests runs out of Power.
- A Spell's entry in `skills` has no `details`, though the server sends each Spell's tooltip as it does a Style's.
- The concentration list the client's concentration window shows arrives only as a bare Percept, so an Agent can't tell which Concentration buffs it keeps up or cancel one.
- The `effects` Percept drops the mark that says an Effect is switched off, so an Agent believes a weaker buff that a better one switched off still works.

## Solution

A fresh Agent, Fearnan (account `druid`), plays a Naturalist the Human created by hand, from level 1 to 5, and gets promoted to Druid.

- `castSpell { spell }` casts a Spell by the name the coding agent sees in `skills`. It is sent as a click on its icon is: the skill packet, with the index found as for `useStyle`. A heal or buff needs a friendly target, so the Agent sends `target` first, at itself or a friend.
- `cancelConcentration { spell, target? }` cancels one of the Concentration buffs it keeps up, as a click in the concentration window does. `target` names who wears it, and is only needed when the same buff is on two Characters.
- A new `concentration` Percept carries the concentration list, and Self keeps it.
- Self keeps Power and concentration. `rest` waits until health and Power are both full, and the `execute_code` summary shows Power. The `status` Percept's `mana` fields become `power` fields.
- Each Spell in `skills` carries its `details`, and each Effect carries `disabled`.
- A new `spells` Learning shows how to work with them.

Whether a cast began, landed or was interrupted, the server says in messages, which already arrive as `message` Percepts.

Acceptance: Fearnan goes from 1 to 5 in a new Claude Code session. It heals itself and keeps its Concentration buffs up between fights, and casts them again after it dies. At 5 it finds a Druid Trainer, whispers "Druid", and is a Druid.

## User Stories

1. As the Human, I want two captures with the real 1.127 client from account `druid`, of casting a heal and a buff on myself and on another, and of cancelling buffs from the concentration window, so that the new packets are built byte for byte as the client sends them.
2. As the Human, I want to create the Naturalist Fearnan by hand with the real client, so that the Character starts as a Human's would.
3. As the Human, I want Fearnan to be a fresh Agent with an empty folder, so that I see what the guide and Learnings alone teach a new caster.
4. As the Human, I want to start a new Claude Code session in Fearnan's folder and tell it to play from 1 to 5 and get promoted to Druid, so that a run starts as earlier milestones' did.
5. As the Human, I want to nudge Fearnan only when it stops, so that I learn when and why it stops.
6. As the Human, I want the run to record how far Fearnan got, how long it took and why it stopped each time, so that I can compare it with Dalta's runs.
7. As the Human, I want the run to record how Fearnan found a Druid Trainer, so that I know whether the Wiki and the Learning are enough.
8. As the Human, I want the run to record its papercuts, so that the next Primitive or Percept comes from a need.
9. As the Human, I want no speed-check entries for Fearnan in the server log, so that casting doesn't break the position the server trusts.
10. As the coding agent, I want to cast a Spell by the name I see in `agent.self.skills`, so that I don't work out indexes.
11. As the coding agent, I want `castSpell` to work for every Spell in my skill list, heals and buffs alike, so that one Primitive covers both lines.
12. As the coding agent, I want a Spell I don't have refused at dispatch with a reason, so that a typo doesn't fail silently.
13. As the coding agent, I want a Style's name refused by `castSpell`, and a Spell's name refused by `useStyle`, so that I don't mix the two up.
14. As the coding agent, I want to target myself with my own object ID before I heal or buff myself, as a client clicks itself, so that the server has a friendly target.
15. As the coding agent, I want the server's messages when a cast begins, lands, is interrupted, or fails for lack of Power, concentration or a target in the chat after each call, so that I can judge whether it worked.
16. As the coding agent, I want each Spell's `details` in `agent.self.skills` (what it does, its value, range, cast time, Power cost and concentration cost), so that I know what a heal heals and what a buff costs, as a Human hovering its icon would.
17. As the coding agent, I want new Spells to appear in `agent.self.skills` as I level, so that I know when I've gained one.
18. As the coding agent, I want my Power, and my concentration against its maximum, in `agent.self`, so that I know whether I can cast.
19. As the coding agent, I want `rest` to wait until my health and Power are both full, so that I don't start a fight unable to heal.
20. As the coding agent, I want my Power in the `execute_code` summary beside health and endurance, so that I see it after every snippet.
21. As the coding agent, I want the Concentration buffs I keep up, each with who wears it and what it costs, in `agent.self`, so that I know what's running and how much concentration it uses.
22. As the coding agent, I want the list to update when I cast or cancel a Concentration buff, or when one ends on my death, so that I know to cast it again.
23. As the coding agent, I want each Effect on me to say whether it's switched off, so that I know a weaker buff that a better one replaced does nothing.
24. As the coding agent, I want to cancel a Concentration buff by its Spell's name, so that I can free the concentration a switched-off buff still uses.
25. As the coding agent, I want to name who wears the buff when the same one is on two Characters, so that I cancel the right one.
26. As the coding agent, I want a buff I'm not keeping up, or not on that target, refused at dispatch with a reason, so that a typo doesn't fail silently.
27. As the coding agent, I want a Mind that takes over to start with the concentration list, as it starts with my Effects, so that a new session knows what I keep up.
28. As the coding agent, I want a Learning with working snippets that target myself, cast a Spell and wait for its cast lines, heal between fights, keep my buffs up and cancel a switched-off one, so that I don't have to rediscover the pattern.
29. As the coding agent, I want the Learning to say that a heal or buff needs a friendly target, that moving or being hit interrupts a cast so heals come between fights, that `kill` must target the Mob again after I've targeted myself, that Concentration buffs end when I die, and that a better buff switches a weaker one off without ending it, so that I avoid the gotchas.
30. As a Mind developer, I want the TypeScript types for the new Primitives, the concentration Percept, a Spell's `details`, an Effect's `disabled` and the renamed `power` fields generated from C#, so that both sides agree.
31. As a Mind developer, I want the Learning's snippets typechecked by the existing Learnings test, so that it never teaches code that doesn't compile.
32. As a Harness developer, I want the Spell looked up in the skill list the server last sent the client, so that the index matches what the server reads it against.
33. As a Harness developer, I want the cast packet to carry the client's position, heading and target-in-view bits as `useStyle`'s does, so that casting doesn't move the Character or put its target out of view.
34. As a Harness developer, I want the concentration window's entry looked up in the concentration list the server last sent, so that the index matches what the server stops.
35. As a Harness developer, I want both Primitives handled by the real packet handlers on the client tick, so that the server decides everything as it does for a Human (ADR 0002).

## Implementation Decisions

- **Vocabulary.** The root glossary gains **Power** (_Avoid_: mana), and **Self** now names Power and the Concentration buffs a Character keeps up and on whom. No ADR changes.
- **Captures.** Two captures from account `druid`, recorded on 2026-10-25, are checked in with the other 1.127 fixtures, with a section in their README:
  - **Casting a heal.** At level 1, Minor Revigoration cast twice. Each cast is the skill packet, 24 bytes, index 6, type 1, flags with the target-in-view bits set: the same layout as a Style.
  - **Casting and cancelling buffs.** At level 2, the Character targets another object and casts Minor Revigoration (index 6) and Minor Bark Skin (index 7) on it, then targets itself and casts both again. The client answers the server's line-of-sight check with 0xD0. It then cancels two buffs from the concentration window: two 0x76 packets, each with index 0, because the second entry moved up when the first was removed.
- **Primitives (ADR 0002).** In the Harness area, built as the captured client sends them, handled by the real handlers on the client tick:
  - `castSpell { spell }` sends the skill packet for the Spell, exactly as `useStyle` sends it for a Style: the position, speed and heading where the server has the Character, the target-in-view bits while something is targeted, the index, type 1.
  - `cancelConcentration { spell, target? }` sends 0x76 with the entry's index in the concentration list.
  - The field names follow `useStyle { style }`, since `name` is the Primitive's discriminator.
- **Looking a Spell up.**
  - The Harness looks the name up among the Spells in the skill list it was last sent, as it does for Styles. The index counts from the first entry after the specs, as for a Style. Spells and Styles share that list, but each Primitive matches only its own kind.
  - There is no `line`. Nothing a Naturalist or Druid can learn shares a name with another Spell it can learn. `line` comes when a class that has a clash plays.
  - No match is refused at dispatch: `Fearnan has no Spell "X".`, and, before any skill list has arrived, every Spell is unknown. A Spell past the 255th entry is refused, as for a Style.
  - Whether the server then accepts the cast isn't known at dispatch (ADR 0004).
- **Looking a Concentration buff up.**
  - The Harness keeps the last concentration list it was sent and looks the Spell's name up there. With `target`, it matches the wearer's name as the list carries it. Without `target`, the first match is used.
  - No match is refused at dispatch: `Fearnan keeps up no "X".`, or `Fearnan keeps up no "X" on Y.`
  - The list cuts names to 14 characters, as the client shows them. Matching compares the given names cut the same way.
- **`concentration` Percept.** Projected from the server's concentration list. In order, each entry is: Spell name, who wears it, concentration cost. Self keeps the latest list as `concentration`, beside the used and maximum concentration from `status`. A Mind that takes over is sent the list again with the other Percepts that describe its Character.
- **`status` Percept.** `mana`, `maxMana` and `manaPercent` become `power`, `maxPower` and `powerPercent`.
- **Self.** Keeps `power`, `maxPower`, `powerPercent`, `concentration` (used and maximum) and the concentration list from the Percepts, replacing each with the latest.
- **`rest`.** Sits until health and Power are both full. A Character with no Power, such as Dalta, rests on health as before.
- **Summary.** The `execute_code` summary shows Power after health and endurance, for a Character that has Power.
- **`skills` Percept.** Each Spell's entry gains `details`. They mirror the tooltip the server sends a 1.127 client alongside the skill list, using the shape Effects' details already have, plus the concentration cost. Styles keep their own `details`.
- **`effects` Percept.** Each Effect gains `disabled`, from the mark the server's buff-bar update sets on a switched-off Effect.
- **Feedback.** No new Percepts for the cast bar or interrupts. The server's messages ("You begin casting…", "You cast…", the interrupt, lack of Power, concentration or target) already come as `message` Percepts.
- **Line of sight.** The Harness still doesn't answer the server's line-of-sight check. The server falls back to the target-in-view bits the Harness sends, and a cast on oneself needs no check.
- **Codegen.** As before: C# types in the Harness area, regenerated TypeScript.
- **Learning.** A new `spells` Learning beside fighting, moving, styles and trading, written as `execute_code` snippets:
  - targeting yourself with `agent.self.objectId`, casting a heal, and waiting for "You cast" or the interrupt line;
  - healing between fights, and resting until health and Power are full;
  - reading a Spell's `details` to see what it does and costs;
  - keeping Concentration buffs up: checking `agent.self.concentration` and the Effects, and casting what's missing;
  - cancelling a buff that's switched off;
  - the gotchas, as in story 29.
- **Not changed.** Surroundings, Inventory, `kill`, the Wiki and the guide. The Wiki already lists every Druid Trainer, and the fighting Learning already explains getting promoted.
- **Code placement.** As before: the Harness folder and namespace, no core code changes. On the Mind side, Self, `rest`, the summary, the Learning and the regenerated types.

## Testing Decisions

- **Good tests** check behaviour seen from outside:
  - a Primitive and a skill or concentration list in, a queued packet or a refusal out;
  - server state in, a Percept out;
  - Percepts in, Self out;
  - packets matching what the real client sent.

  They don't check how the lookup is built.
- **Harness dispatch (existing seam, the main one).** On a Character double whose skill list has specs, abilities, Spells and Styles, and whose concentration list has the same buff on two Characters:
  - `castSpell` by name queues the skill packet with the index the handler reads back as that Spell;
  - a name that isn't a Spell, a Style's name, and a Spell before any skill list are each refused with a reason;
  - `cancelConcentration` queues 0x76 with the entry's index; without `target`, the first entry is used, and with `target` the other one;
  - a buff not kept up, or not on that target, is refused with a reason;
  - the indexes follow the lists as the server last sent them.

  Prior art: the `useStyle` and `useItem` dispatch tests in the Harness client tests.
- **Packet builders (existing seam).** Golden bytes for the cast packet and 0x76 against the two new captures, masking only fields the README documents as varying. Prior art: the skill packet test against `styles.tsv`.
- **Percept projection (existing seam).** Prior art: the existing `skills`, `effects` and `status` projection tests.
  - The concentration list becomes a `concentration` Percept, in order.
  - A Spell's `skills` entry carries its `details`.
  - A switched-off Effect carries `disabled`.
  - `status` carries the `power` fields.
  - A Mind that takes over is sent the concentration list.
- **Protocol (existing seam).** The new Primitives and fields round-trip as JSON, and the codegen test fails until the TypeScript is regenerated.
- **Mind (existing seams).** Prior art: the existing Self and `rest` tests.
  - Self keeps Power, concentration and the concentration list from Percepts.
  - `rest` waits until health and Power are both full, and on health alone for a Character with no Power.
- **Learnings (existing seam).** The new Learning's snippets typecheck as `execute_code` snippets.
- **Not tested automatically:** the live server and the coding agent, covered by the manual acceptance test.
- **Manual acceptance test:**
  1. Check in the two captures with their README section.
  2. Start a new Claude Code session in `agents/fearnan` and tell it to play from level 1 to 5 and get promoted to Druid.
  3. Leave it, nudging it only when it stops.
  4. Fearnan reaches level 5. It heals itself and keeps its Concentration buffs up between fights, and casts them again after it dies.
  5. It finds a Druid Trainer, whispers "Druid", and the server says it's a Druid.
  6. Record on this spec:
     - how far it got and how long it took;
     - how it found the trainer;
     - why it stopped each time;
     - its papercuts.
  7. Check the server log has no speed-check entries for Fearnan.

## Out of Scope

- Damage Spells, list casters' spell lines and the spell packet (0xD5): milestone 8, when a Magician plays.
- `line` on `castSpell`, until a class with two Spells of one name plays.
- Percepts for the cast bar, interrupts and cooldowns.
- Answering the server's line-of-sight check.
- Healing or buffing a groupmate, and Concentration buffs turning off with range: milestones 10 and 11.
- Cancelling an Effect from the buff bar.
- The spell queue.
- New porcelain, such as a `heal` or `cast` Tactic. Fearnan writes its own.
- Pets and the Druid's Nature line: milestone 9.

## Further Notes

- Relevant decisions:
  - ADR 0001: Percepts only as a client knows them. The concentration list and each Spell's tooltip are what the 1.127 client is sent.
  - ADR 0002: Primitives through the real handlers.
  - ADR 0004: dispatch-only results, so whether a cast landed comes from messages.
  - ADR 0009: Primitives and Percepts are added when the coding agent needs them. That's why there are no cast-bar Percepts.
- Facts this spec relies on, from the server and its database as they stand:
  - **A Naturalist** (class 53) has Regrowth and Nurture baselines, and promotes to Druid (47) at 5. It gets weapons and a shield, no weapon spec and no Styles.
  - **Its Spells from 1 to 5.** All of them target Realm, so the caster or a friend. Heals reach 2000 and buffs 1000.

    | Level | Spell | Line | Kind | Cast | Cost |
    |---|---|---|---|---|---|
    | 1 | Minor Revigoration | Regrowth | heal 6 | 2.2 s | 1 Power |
    | 2 | Minor Bark Skin | Nurture | +12 armour factor | 3 s | 1 concentration |
    | 3 | Minor Renewal | Regrowth | heal 11, replaces Minor Revigoration | 2.2 s | 2 Power |
    | 3 | Strength of the Oak | Nurture | +12 strength | 3 s | 1 concentration |
    | 4 | Renewal | Regrowth | heal 26 | 2.8 s | 4 Power |
    | 4 | Lesser Bark Skin | Nurture | +14 armour factor, switches Minor Bark Skin off | 3 s | 2 concentration |
    | 5 | Minor Revivification | Regrowth | heal 16, replaces Minor Renewal | 2.2 s | 3 Power |
    | 5 | Oak's Stoutness | Nurture | +14 constitution | 3 s | 2 concentration |

  - **A Naturalist's Spells are in the skill list**, after specs and abilities and before Styles, because its lines are hybrid. A list caster's aren't.
  - **Targeting.** A Realm Spell with no target fails with "You must select a target for this spell!". With an enemy targeted, it fails without a message.
  - **Concentration.**
    - Maximum concentration is about 18 at level 1 and 36 at level 5.
    - A Concentration buff can't be cast again while it's up.
    - All of them end when the caster dies or logs out.
    - A better buff of the same kind switches a weaker one off without ending it, and the weaker one keeps its concentration.
    - Buffs on the caster never turn off with range.
  - **The concentration list** carries, for each buff: index, cost, icon, the Spell's name and the wearer's name, both cut to 14 characters. It is sent at world entry and whenever the list changes.
  - **Promotion.** A Druid Trainer answers a level 5 Naturalist with "[Druid]", and whispering "Druid" promotes at once, with no Dialog. The Naturalist Trainer doesn't promote. The closest Druid Trainer to Connla is Torrance, in the Shannon Estuary about 15,000 units east. The Wiki lists him and the others.
- The fighting Learning's "Promoting" section says "your class trainer promotes you". For a Naturalist it's the Druid Trainer. If Fearnan trips on that, it's a papercut to fix in the Learning.
- **Milestone ladder:** `.scratch/milestones.md`. This spec is milestone 7.

## Comments

**2026-10-25 (agent):** Implemented, not yet tested in game. Where it differs from or adds to the spec above:

- **Captured on 2026-10-25** as `cast-heal.tsv` and `cast-buffs.tsv`, with a Spells section in the fixtures README. A Naturalist's cast is the skill packet byte for byte as a style's: Minor Revigoration was index 6 and Minor Bark Skin 7. The cancel (0x76) is the index and then `00 72 04`, which the handler doesn't read and the builder writes as captured.
- **A Spell's tooltip is `spellDetails`, not `details`.** `details` stays a Style's `StyleDetails`, which Dalta's Tactics and the styles Learning read as such. An Effect's tooltip is still `details`, with the same `SpellDetails` shape, which gains `concentrationCost`.
- **Self's names:** `power`, `maxPower` and `powerPercent`; `concentration` is what's left, as the status packet carries it (the server's maximum less what's used), out of `maxConcentration`; the list is `concentrationBuffs`.
- **Concentration names are cut as the client shows them,** "Minor Bark Skin" to "Minor Bark S..". `cancelConcentration` cuts the names it's given the same way, so a whole name works.
- **`disabled` is the buff bar's mark,** which the server also sets on an immunity. It's absent when not set.
- **Beyond the spec:**
  - `playerPosition`'s `manaPercent` is `powerPercent` too, so no `mana` is left in the protocol.
  - The Journal's status line shows Power and concentration, and an Effect's line says when it's disabled.
  - The fighting Learning's Resting paragraph mentions Power.
- **The Learning hasn't been run in a fight.** Its message texts are the server's, and its snippets typecheck. Fearnan's run is the test.
