# Milestone 8: Damage spells

Status: resolved

## Problem Statement

The plan in `.scratch/milestones.md` is a Druid and an Eldritch who level together to 20 and join Dalta. The Eldritch starts as a Magician. A Magician has no weapon skills worth fighting with. It lives on damage Spells cast from range, and stays alive by Kiting: sprinting away from a Mob, turning round and casting at it before it closes in.

Milestone 7 gave Fearnan, a Naturalist, `castSpell`, but only for a hybrid's Spells, which sit in the skill list. A Magician is a list caster. Its Spells come in spell lines, which the client is sent apart from the skill list and casts with its own packet, naming a line and a Spell's level. So an Agent playing a Magician:

- sees none of its Spells, because the Harness never projects the spell lines;
- can't cast any of them, because `castSpell` finds only Spells in the skill list and is refused with "has no Spell";
- would keep the Spells it had when it first cast, even if it could see them, because the server's cached list of a Character's spell lines is only refreshed by the real packet library, which the Harness never runs.

## Solution

A fresh Agent, Orla (account `agentorla`), plays a Lurikeen Magician the Human created by hand, from level 1 to 5, and gets promoted to Eldritch. It plays while Fearnan, in another zone, plays its own run to 5.

- `castSpell { spell }` also casts a list caster's Spell by name. The Harness looks the name up in the spell lines it was last sent and sends the client's cast packet (0x7D) with the line's index and the Spell's level, as a click on its icon does.
- A list caster's Spells appear in the `skills` Percept as `page: "Spells"` entries with their tooltip `details`, as a hybrid's do. A Spell gained on levelling appears with the next skills update.
- The server's list of a Character's spell lines is refreshed whenever its skills are projected, as the real packet library does when it sends them.
- The `spells` Learning gains a Damage Spells section and a Kiting section.

Kiting needs nothing new: `/sprint` already works through the `command` Primitive, and `face` and `moveTo` exist. Whether a damage Spell hit, was resisted or was interrupted, the server says in messages, which already arrive as `message` Percepts.

Acceptance: Orla goes from 1 to 5 in a new Claude Code session, killing Mobs with its damage Spells. At 5 it finds an Eldritch Trainer, whispers "Eldritch", and is an Eldritch. The run records whether and how it kited.

## User Stories

1. As the Human, I want a capture with the real 1.127 client from account `agentorla` of a Magician killing a Mob with its damage Spell, so that the cast packet is built byte for byte as the client sends it.
2. As the Human, I want to create the Lurikeen Magician Orla by hand with the real client, so that the Character starts as a Human's would.
3. As the Human, I want Orla to be a fresh Agent with an empty folder, so that I see what the guide and Learnings alone teach a new damage caster.
4. As the Human, I want to start a new Claude Code session in Orla's folder and tell it to play from 1 to 5 and get promoted to Eldritch, so that a run starts as earlier milestones' did.
5. As the Human, I want Orla and Fearnan online at the same time, each in its own Claude Code session, so that I don't wait for one run to finish before the next starts.
6. As the Human, I want to deploy this milestone's Harness between two of Fearnan's sessions, so that the restart doesn't cut a run short.
7. As the Human, I want to nudge Orla only when it stops, so that I learn when and why it stops.
8. As the Human, I want the run to record how far Orla got, how long it took and why it stopped each time, so that I can compare it with Dalta's and Fearnan's runs.
9. As the Human, I want the run to record whether Orla kited, how, and whether it worked, so that I know whether the Learning teaches it.
10. As the Human, I want the run to record how Orla found an Eldritch Trainer, so that I know whether the Wiki is enough.
11. As the Human, I want the run to record its papercuts, so that the next Primitive or Percept comes from a need.
12. As the Human, I want the run to record anything Orla and Fearnan did to each other, so that I know what two Agents online at once need before milestone 10.
13. As the Human, I want no speed-check entries for Orla in the server log, so that sprinting and casting don't break the position the server trusts.
14. As the coding agent, I want my damage Spells, buffs and stun in `agent.self.skills` with `page: "Spells"`, so that I find a Magician's Spells where a Naturalist finds its own.
15. As the coding agent, I want each of them to carry its `details` (what it does, its damage, range, cast time and Power cost), so that I know what a Spell does and costs, as a Human hovering its icon would.
16. As the coding agent, I want a Spell I gain on levelling to appear in `agent.self.skills`, so that I know when I can cast something new.
17. As the coding agent, I want to cast a Magician's Spell by name with `castSpell`, the same Primitive a Naturalist uses, so that I don't care whether my class is a hybrid.
18. As the coding agent, I want a Spell I don't have refused at dispatch with a reason, so that a typo doesn't fail silently.
19. As the coding agent, I want a Style's name refused by `castSpell`, so that I don't mix the two up.
20. As the coding agent, I want the server's messages when my damage Spell begins, hits, is resisted, is interrupted or can't start (out of range, no target, not enough Power) in the chat after each call, so that I can judge whether it worked.
21. As the coding agent, I want to cast at a Mob I've targeted without answering anything else, so that a cast at an enemy works as one at myself does.
22. As the coding agent, I want to cast my self buffs (Lesser Powerward, Powerward, Lesser Powerbarrier) on myself with `castSpell`, so that I go into fights with them up.
23. As the coding agent, I want to sprint with `command "/sprint"` and run with `moveTo`, so that I can get away from a Mob.
24. As the coding agent, I want to `face` a Mob, so that I turn round to cast at it after running.
25. As the coding agent, I want a Learning with working snippets that target a Mob, cast a damage Spell at it and wait for the hit, the resist or the interrupt, so that I don't have to rediscover the pattern.
26. As the coding agent, I want the Learning to show Kiting (sprint away, stop, face, cast while the Mob is far enough away to finish the cast, and again) with the numbers it rests on, so that I know when there's time for one more cast.
27. As the coding agent, I want the Learning to say that moving or being hit interrupts a cast, that a Mob runs about as fast as I do unless I sprint, that sprinting costs endurance, that Mobs don't leash but give up about 25 s after the fight leaves them, and that a Mob below a third of its health slows down, so that I avoid the gotchas.
28. As the coding agent, I want the Learning to say that Prismatic Flare at level 5 stuns a Mob for 3 s, so that I know I can buy time with it.
29. As a Mind developer, I want no change to the protocol's shapes, so that the TypeScript stays as generated and Fearnan's snippets keep working.
30. As a Mind developer, I want the Learning's snippets typechecked by the existing Learnings test, so that it never teaches code that doesn't compile.
31. As a Harness developer, I want the Spell looked up in the spell lines the server last projected, so that the line index matches what the server reads it against.
32. As a Harness developer, I want the cast packet to carry the client's position, speed, heading and target-in-view bits as the skill packet does, so that casting doesn't move the Character or put its target out of view.
33. As a Harness developer, I want the cast handled by the real packet handler on the client tick, so that the server decides everything as it does for a Human (ADR 0002).
34. As a Harness developer, I want the server's list of spell lines refreshed when skills are projected, so that a Spell gained on levelling can be cast and delved.

## Implementation Decisions

- **Vocabulary.** The root glossary gains **Kiting** (_Avoid_: pulling). No ADR changes.
- **Milestone ladder.** Milestone 8 now says "on direct damage Spells". A Magician has no bolts from 1 to 5; they come with the Eldritch's Void line.
- **Capture.** The Human's capture from account `agentorla` on 2026-10-25 is checked in with the other 1.127 fixtures as a Spells capture of a list caster, with a README section. What it shows:
  - Each cast is 0x7D, 24 bytes: position (x, y, z), speed, heading, flags, Spell level, line index, and two bytes the server doesn't read. It's the same position block as the skill packet.
  - The first cast is a self buff: line 1, level 1, no target bits.
  - Then Gleam Ray is cast at a Mob about 13 times: line 0, level 1, flags 0xA000.
  - Through the fight the client answers the server's line-of-sight checks with 0xD0, roughly every 200 ms.
- **`castSpell` (ADR 0002).** Unchanged in shape. The Harness looks the name up among the hybrid Spells in the skill list first, as now.
  - If the name isn't there, the Harness looks among the Spells of the spell lines it last projected, and queues 0x7D with that line's index and the Spell's level. The position, speed, heading and target-in-view bits are written as for the skill packet.
  - No match in either is refused at dispatch as now: `Orla has no Spell "X".`
  - There is still no `line`. No two Spells a Magician or an Eldritch can learn share a name.
  - Whether the server then accepts the cast isn't known at dispatch (ADR 0004).
- **Spell line index and level.** They follow the server's own list of a Character's usable spell lines, as the real packet library sends it: baseline lines first, then spec lines, each with its Spells of level up to the line's level. The server resolves a cast by the first Spell of that level in that line, so the level identifies the Spell.
- **Refreshing the lines.** The server caches a Character's usable spell lines and only rebuilds them when asked. The real packet library asks every time it sends skills, and the Harness's packet library doesn't run it. So when the Harness projects skills, it asks for the lines to be rebuilt, as the real one does. Without this, Spells gained on levelling can be neither cast nor delved.
- **`skills` Percept.** A list caster's Spells join it as `page: "Spells"` entries after everything else, in line order then level, each with `details` built as a hybrid Spell's are. The client gets them as spell lines, a different packet. Folding them into one Percept keeps one place to look, and nothing in the protocol changes shape. A `spellLines` Percept comes only when a class needs to know a Spell's line.
- **Line of sight.** The Harness still doesn't answer the server's line-of-sight checks. With no check started, the server falls back to the target-in-view bits the Harness sends, for a Mob as for oneself. The capture shows a real client answering them during a cast at a Mob. If the tests or the run show a cast at a Mob failing for want of an answer, answering 0xD0 joins this milestone.
- **Kiting.** No new Primitive or porcelain. `command "/sprint"` toggles sprint, and `moveTo`, `stop` and `face` exist. Orla writes its own Kiting Tactic (ADR 0009).
- **Learning.** The `spells` Learning gains two sections, written as `execute_code` snippets, with numbers from the server, untested until Orla plays:
  - **Damage Spells:** reading which Spells do damage from their `details`, targeting a Mob, casting and waiting for the hit, the resist, the interrupt, "too far away" or "can't see your target".
  - **Kiting:** sprint on, run away, sprint off, stop, `face` the Mob, cast while it's further than a cast's worth of its running away (2.6 s at about 190 units a second is about 500 units, against a range of 1500), and again. Plus the gotchas in story 27 and Prismatic Flare's stun.
  - The opening paragraph, which names only a Naturalist's Spells, also names a Magician's.
- **Two Agents online.** Orla and Fearnan each run their own Claude Code session and Mind, by Character name, against the same server. They start in different zones and aren't grouped. Nothing is built for it. What happens between them is recorded.
- **Agent folder.** Orla's folder is made from the template, with its MCP config naming Orla, as Fearnan's was.
- **Not changed.** The protocol, Self, Surroundings, Inventory, `kill`, `rest`, the summary, the Wiki and the guide. The Wiki already lists Nainsi, an Eldritch Trainer about 1,100 units from Orla's start.
- **Code placement.** As before: the Harness folder and namespace, no core code changes. On the Mind side, only the Learning.

## Testing Decisions

- **Good tests** check behaviour seen from outside:
  - a Primitive and the server's skills and spell lines in, a queued packet or a refusal out;
  - server state in, a Percept out;
  - packets matching what the real client sent.

  They don't check how the lookup is built.
- **Harness dispatch (existing seam, the main one).** On a Character double that is a list caster, with Spells in two baseline lines:
  - `castSpell` by name queues 0x7D with the line index and level that the real cast handler resolves back to that Spell;
  - a hybrid's Spells still go out as the skill packet;
  - an unknown name and a Style's name are each refused with a reason;
  - after the Character levels and its skills are projected again, a Spell it just gained is found and resolves.

  Prior art: milestone 7's `castSpell` dispatch tests.
- **Packet builder (existing seam).** Golden bytes for 0x7D against Orla's capture, masking only fields the README documents as varying. Prior art: the skill packet test against the cast captures.
- **Percept projection (existing seam).** A list caster's Spells appear in `skills` as `page: "Spells"` entries with `details`, and a Spell gained after a level-up appears in the next one. Prior art: the existing `skills` projection tests.
- **Learnings (existing seam).** The new sections' snippets typecheck as `execute_code` snippets.
- **Not tested automatically:** the live server, line of sight, Kiting and the coding agent, covered by the manual acceptance test.
- **Manual acceptance test:**
  1. Check in the capture with its README section.
  2. Deploy between two of Fearnan's sessions.
  3. Start a new Claude Code session in `agents/orla` and tell it to play from level 1 to 5 and get promoted to Eldritch.
  4. Leave it, nudging it only when it stops.
  5. Orla reaches level 5 on its damage Spells.
  6. It finds an Eldritch Trainer, whispers "Eldritch", and the server says it's an Eldritch.
  7. Record on this spec:
     - how far it got and how long it took;
     - whether and how it kited;
     - how it found the trainer;
     - why it stopped each time;
     - anything between it and Fearnan;
     - its papercuts.
  8. Check the server log has no speed-check entries for Orla.

## Out of Scope

- Bolts, the Void line, QuickCast and the Eldritch's spec lines: they come with the promotion, and Orla stops at 5.
- `line` on `castSpell`, and a `spellLines` Percept.
- Kiting porcelain, or a Primitive for sprint.
- Answering the server's line-of-sight check, unless a cast at a Mob turns out to need it.
- Percepts for the cast bar, interrupts and cooldowns.
- The spell queue.
- Anything built for two Agents online at once, and grouping: milestone 10.
- Fixing Fearnan's papercuts from milestone 7.

## Further Notes

- Relevant decisions:
  - ADR 0001: Percepts only as a client knows them. A list caster's spell lines and each Spell's tooltip are what the 1.127 client is sent.
  - ADR 0002: Primitives through the real handlers.
  - ADR 0004: dispatch-only results, so whether a Spell hit comes from messages.
  - ADR 0009: Primitives and Percepts are added when the coding agent needs them. That's why Kiting gets no Primitive.
- Facts this spec relies on, from the server and its database as they stand:
  - **A Magician** (class 51) is a list caster with the baseline lines Way of the Sun (Light) and Way of the Moon (Mana). It promotes at 5 to Eldritch, Enchanter or Mentalist, and only an Elf or a Lurikeen can be an Eldritch. Orla is a Lurikeen. It has no QuickCast; an Eldritch does.
  - **Its Spells from 1 to 5.** None of them can be cast while moving. Being hit interrupts them too.

    | Level | Spell | Line | Kind | Cast | Power | Range |
    |---|---|---|---|---|---|---|
    | 1 | Gleam Ray | Sun | direct damage, 5 heat | 2.6 s | 2 | 1500 |
    | 1 | Lesser Powerward | Moon | self armour buff 13, 20 min | 3 s | 2 | self |
    | 2 | Gleam Streak | Sun | direct damage, 9 heat | 2.6 s | 2 | 1500 |
    | 2 | Aura of Turning | Moon | damage shield, 20 min | 4 s | 2 | 1000 |
    | 3 | Gleam Blast | Sun | direct damage, 13 heat | 2.6 s | 3 | 1500 |
    | 3 | Powerward | Moon | self armour buff 22, 20 min | 3 s | 3 | self |
    | 4 | Greater Aura of Turning | Moon | damage shield, 20 min | 4 s | 3 | 1000 |
    | 5 | Prismatic Flare | Sun | stun, 3 s | 2.5 s | 4 | 1500 |
    | 5 | Lesser Powerbarrier | Moon | self armour buff 32, 20 min | 3 s | 4 | self |

  - **Direct damage, not bolts.** It hits at the end of the cast with no travel time and can't be blocked. Bolts come only in the Eldritch's Void line.
  - **The cast packet** is 0x7D: position, speed, heading, flags, Spell level, line index. The server resolves the line by its index in the Character's usable spell lines, and the Spell as the first in that line of that level.
  - **Speeds.** A Character runs at 191, or about 248 sprinting, which costs endurance. Mobs near Orla's start run at about 145 to 200, most at 180 to 200. A Mob below a third of its health slows, down to a fifth of its speed near death.
  - **Chase.** A Mob follows up to 5,000 units, has no leash, and goes home about 25 s after it was last in a fight. Many low Mobs don't attack first.
  - **Line of sight.** The server checks line of sight during a player's cast at another. With the Harness starting no check, it falls back to the target-in-view bits.
  - **Promotion.** An Eldritch Trainer promotes a level 5 Elf or Lurikeen Magician who whispers "Eldritch". Nainsi, in Shannon Estuary, is about 1,100 units from Orla's start. The Wiki lists every Eldritch Trainer.
- **Milestone ladder:** `.scratch/milestones.md`. This spec is milestone 8.

## Comments

**2026-10-25 (agent):** Implemented, not yet tested in game. Where it differs from or adds to the spec above:

- **Captured on 2026-10-25** as `cast-damage.tsv`, with a "Spells of a list caster" section in the fixtures README. Lesser Powerward went out as level 1 in line 1, Gleam Ray as level 1 in line 0, and the first Gleam Ray was clicked while still running.
- **The spell lines are looked up first, not second.** The `skills` Percept now carries a list caster's Spells after everything else, so the Harness's copy of the skill list has them too. Looking there first would find them and send the skill packet with an index the server reads as something else. A hybrid's Spells are never in the spell lines, so the order changes nothing for Fearnan.
- **The Harness keeps the spell lines it was last sent,** taken from the server when the skills Percept arrives, as it keeps the skill list. Before any skills Percept, every Spell is unknown, as for a hybrid.
- **Tests check the server resolves the packet back to the Spell,** through `UseSpellHandler`'s own lookup.
- **`TestPlayer` gained list spells,** cached as the server caches them, and the specialization and skill-list fields the projection reads.
- **`agents/orla`** was already created by the Human, the same as the template with Orla's name.
- **The Learning hasn't been run in a fight.** Its message texts are the server's, and its snippets typecheck. Orla's run is the test, Kiting above all: whether `runTo` takes a point 900 units away on uneven ground is untested.

**2026-10-25 (agent):** After Orla's run: a `useAbility { ability }` Primitive for Quick Cast, which Orla gets as an Eldritch. A client sends it as the skill packet with the ability's index, as in the Human's capture `Debug/packet-captures/admin-20261025-203702-240.tsv` (Vue, index 3). The server refuses it while attack is on. Then the spells Learning gains the Human's routine: cast, let the Mob reach you, Quick Cast, and if it's still alive, the staff, or kite.

**2026-10-26 (agent):** Orla's run, from its Journal (`agents/orla/journal/2026-10-25T20-48-19-162Z.log`), Lessons, papercuts and commits:

- **How far and how long.** Level 1 to 5 and promoted to Eldritch in one session of about 61 minutes: 17 kills from 1 to 3 on sand crabs and beach rats south of Connla, 16 from 3 to 4 mostly on mudmen, 8 from 4 to 5. It died 6 times: to a sand crab, charging in with the staff before it pulled from range, twice to a level 1 large frog at level 1, once to a sandman, and twice at level 4 fighting back with the staff alone. Fearnan played in Connla at the same time.
- **Casting.** 260 casts: 29 Gleam Ray, 39 Gleam Streak, 188 Gleam Blast and 4 Powerward. 168 hits, 21 resists, 4 interrupts and 6 "too far away". 25 casts went at a Mob that had just died or gone out of view, and the server said "You must select a target for this spell!". `castSpell` by name worked from the first cast, and each new Spell appeared after levelling.
- **Kiting.** It never kited: no `/sprint`, no running away to cast. On the Human's advice it opened every fight with Gleam Ray from about 1350 units and cast until the Mob first hit it, then finished it with the staff, which hit harder at level 1. Later it learned to answer a Mob that came at it with Spells, not the staff. That's the Human's routine without Quick Cast, which a Magician doesn't have. The Learning's Kiting section is still untested.
- **The trainer.** It found Nainsi, the Eldritch Trainer in Connla, talked to Nainsi with `talkTo` and whispered "Eldritch".
- **Stops and nudges.** It never stopped. Its Claude Code session (`~/.claude/projects/C--Users-joshu-Code-OpenDAoC-Core-agents-orla/`) was told once to level to 5, become an Eldritch and shut the PC down, after a first try that named Druid by mistake was cleared. It ran each hunt as a background `execute_code` call of 10 to 15 minutes and committed between them. The Human sent one message on the way, to shut down only once Fearnan was 5 too, which it checked with `/who all`. The two pieces of advice, open every fight from range and rest to full Power, the Human wrote into its `lessons.md` while it played.
- **Papercuts.** Two, neither about this milestone: a Tactic that once failed to import kept failing after the fix (renaming the file got around it), and `train` with "Light Magic", the name `specs` shows, fails where `/train Light` works.
- **Between Orla and Fearnan.** Nothing got in the way. Orla saw Fearnan with `/who all` and noted it in its Lessons.
- **Speed checks.** None for Orla in the server log.
