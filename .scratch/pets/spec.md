# Milestone 9: Pets

Status: ready-for-agent

## Problem Statement

Fearnan became a Druid at the end of milestone 7. A Druid fights through its pet: Call Minor Nature Spirit, which Fearnan already has at Nature 1, summons a wolf, lynx or bear cub or a minor nature spirit that fights for it until it dies or is released. The plan in `.scratch/milestones.md` has the Druid's pet protect the Eldritch in milestone 11, so the Druid has to learn to fight with it first.

Fearnan can summon the pet today, since that's an ordinary `castSpell`. Everything after that is out of its reach:

- No Primitive sends the pet window's buttons, so Fearnan can't send the pet in, call it back or set it to passive. A summoned pet only defends its owner and itself. It doesn't join in when its owner attacks.
- The server's pet window packet reaches the Mind only as a bare Percept, so Fearnan can't tell which pet in its Surroundings is its own. `pet` on an NPC is true for anyone's pet. Fearnan also can't see the pet's aggression and walk settings, or notice that the pet is gone.
- No Learning says how a pet behaves.

## Solution

Fearnan keeps its memory and Lessons and plays the Druid solo from level 5 to 7, fighting with its pet.

- `commandPet { order }` clicks one button in the pet window. `order` is one of `aggressive`, `defensive`, `passive`, `follow`, `stay`, `goTarget`, `here`, `attack` or `release`. `attack` and `goTarget` act on the current target, as in the client, so the Agent sends `target` first.
- A new `petWindow` Percept carries what the client's pet window shows: which NPC is the pet, whether the window opened, changed or closed, and the pet's aggression and walk settings. Self keeps them as `pet`, which is cleared when the window closes. The pet's health and target come from Surroundings, by its id.
- A new `pets` Learning shows how to work with them.

Whether an order worked, the server says in messages ("You command the wolf cub to kill your target!"), which already arrive as `message` Percepts.

Acceptance: Fearnan goes from 5 to 7 in a new Claude Code session, with its pet out in most fights. It sends the pet in and summons it again after the pet dies or after Fearnan dies.

Meanwhile Orla waits at level 5, apart from its run for the `abilities` spec, which goes on in parallel.

## User Stories

1. As the Human, I want a capture with the real 1.127 client from account `druid`, of summoning a pet and then clicking every button in the pet window, each announced with `/say` first, so that the pet command packet is built byte for byte as the client sends it and the capture is easy to read.
2. As the Human, I want Fearnan to keep its memory and Lessons, so that what it learned about cast delays and resting carries into the run to 20.
3. As the Human, I want to start a new Claude Code session in Fearnan's folder and tell it to keep levelling to 7, so that a run starts as earlier milestones' did.
4. As the Human, I want to nudge Fearnan only when it stops, so that I learn when and why it stops.
5. As the Human, I want the run to record how Fearnan found its pet, how often the pet was out, which orders it used, its resummons and deaths, and what the server answered, so that I can judge whether the Learning is enough.
6. As the Human, I want the run to record its papercuts, so that the next Primitive, Percept or porcelain change comes from a need.
7. As the Human, I want no speed-check entries for Fearnan in the server log, so that pet commands don't break the position the server trusts.
8. As the Human, I want Orla to wait at 5, so that the two Agents start milestone 10 close in level.
9. As the coding agent, I want to summon my pet with `castSpell`, as any other Spell, so that there's nothing new to learn for it.
10. As the coding agent, I want to send my pet at my target with one Primitive, so that it fights the Mob I fight.
11. As the coding agent, I want to call my pet back, make it follow, stay or go to my target, so that I can keep it out of a fight or bring it to one.
12. As the coding agent, I want to set my pet aggressive, defensive or passive, so that it fights only when I want it to.
13. As the coding agent, I want to release my pet, so that I can summon a new one.
14. As the coding agent, I want each order to be one click in the pet window, as a Human's is, so that the server decides what it does.
15. As the coding agent, I want an unknown order named when it's refused, so that a typo doesn't fail silently.
16. As the coding agent, I want to know which NPC in my Surroundings is my pet, so that I can read its health and what it's fighting.
17. As the coding agent, I want my pet's aggression and walk settings in `agent.self.pet`, so that I see an order took effect and that the other settings stayed as they were.
18. As the coding agent, I want `agent.self.pet` cleared when my pet dies or I lose it, so that I know to summon it again.
19. As the coding agent, I want a Mind that takes over to know my pet straight away, so that a new session doesn't summon a second one.
20. As the coding agent, I want the server's answers to my orders in the chat after each call, such as "You command the wolf cub to kill your target!" or "Your target is too far away to attack with your pet!", so that I can judge whether it worked.
21. As the coding agent, I want my pet's hits and the hits on it in the chat, as the client shows them, so that I can follow its fight.
22. As the coding agent, I want a Learning with working snippets that summon the pet, find it in my Surroundings, send it at a Mob and wait for the fight to end, and summon it again when it's gone, so that I don't have to rediscover the pattern.
23. As the coding agent, I want the Learning to say how the pet behaves on its own:
    - it starts defensive and following;
    - it doesn't join my attacks unless I order `attack`;
    - it defends me when I'm hit and fights back when it's hit;
    - it is released if it gets too far from me;
    - a Mob it hits puts some of its anger on me too.

    So that I plan fights around it.
24. As the coding agent, I want the Learning to say that a second summon is refused while a pet is out, that the summon costs a quarter of my Power, that the pet's level follows mine, and that the next pet comes at Nature 7, so that I avoid the gotchas.
25. As the coding agent, I want the Learning to say that each order changes only its own row of the pet window, so that I know `attack` doesn't undo `passive` and the like.
26. As a Mind developer, I want the TypeScript types for `commandPet` and the `petWindow` Percept generated from C#, so that both sides agree.
27. As a Mind developer, I want the Learning's snippets typechecked by the existing Learnings test, so that it never teaches code that doesn't compile.
28. As a Harness developer, I want `commandPet` handled by the real pet window handler on the client tick, so that the server decides everything as it does for a Human (ADR 0002).
29. As a Harness developer, I want the pet window projected only from what the server sends the client, so that the Agent knows its pet as a client does (ADR 0001).

## Implementation Decisions

- **Vocabulary.** The root glossary gains **Pet**: a creature a Character summons and commands, which fights for it until it dies or is released (_Avoid_: summon, minion). **Self** now names the Character's pet and its settings. No ADR changes.
- **Capture.** The Human's capture from account `druid`, of Fearnan summoning a pet and then clicking each of the nine buttons once, each announced with `/say`, is checked in as `pet-window.tsv` with a Pets section in the fixtures README. The README documents the 0x8A layout as the capture shows it and which fields vary.
- **Primitive (ADR 0002).** `commandPet { order }` sends the pet command packet (0x8A) as the captured client sends it for that button, handled by the real handler on the client tick. The handler reads three bytes, one per row of the pet window, with 0 meaning "leave alone":
  - aggression: `aggressive` 1, `defensive` 2, `passive` 3;
  - walk: `follow` 1, `stay` 2, `goTarget` 3, `here` 4;
  - command: `attack` 1, `release` 2.

  One `order` sets one byte, so each Primitive is one click.
- **No pet check at dispatch.** The Harness keeps no pet state for dispatch. It sends the packet whether or not a pet is out, and whatever the server does, likely nothing, is the answer (ADR 0004). Only an unknown or missing `order` is refused, when the Primitive is read.
- **`petWindow` Percept (ADR 0001).** Projected from the server's pet window packet. It carries:
  - the pet's `objectId`;
  - `action`: `open`, `update` or `close`;
  - `aggression`: `aggressive`, `defensive` or `passive`;
  - `walk`: `follow`, `stay`, `goTarget` or `here`.

  The packet's effect icons aren't carried. A Mind that takes over is sent the current pet window with the other Percepts that describe its Character, as it is sent the concentration list.
- **Self.** Keeps `pet: { objectId, aggression, walk } | undefined`. It is set on `open`, updated on `update` and cleared on `close`.
- **Surroundings.** Unchanged. The pet already arrives as an NPC with `pet: true`, a health percent and a target. The Agent finds its own by `agent.self.pet.objectId`.
- **Feedback.** No new Percepts for orders or the pet's fight. The server's messages already come as `message` Percepts: the summon, each order, the refusals and losing control, and the pet's hits and the hits on it.
- **Porcelain.** `rest`, `kill` and the other porcelain Tactics are unchanged. Neither knows about the pet, and Fearnan works around them in its own code. Better porcelain is a later decision.
- **Codegen.** As before: the C# record, regenerated TypeScript. The Journal names the order.
- **Learning.** A new `pets` Learning beside fighting, moving, spells, styles and trading, written as `execute_code` snippets:
  - summoning the pet and waiting for "now under your control";
  - finding it in Surroundings by `agent.self.pet.objectId`, and reading its health and target;
  - targeting a Mob and sending the pet in with `attack`, then waiting for the Mob to die;
  - summoning it again when `agent.self.pet` is cleared;
  - the pet's behaviour and gotchas, as in stories 23–25.
- **Code placement.** As before: the Harness folder and namespace, with no core code changes. On the Mind side: Self, the Learning and the regenerated types.

## Testing Decisions

- **Good tests** check behaviour seen from outside:
  - a Primitive in, a queued packet or a refusal out;
  - server state in, a Percept out;
  - Percepts in, Self out;
  - packets matching what the real client sent.
- **Harness dispatch (existing seam, the main one).** Each `order` queues 0x8A with the byte the handler reads back as that order and 0 in the other two. The packet is queued whether or not a pet is out. Prior art: the `useAbility` and `castSpell` dispatch tests in the Harness client tests.
- **Packet builder (existing seam).** Golden bytes for all nine buttons against `pet-window.tsv`, masking only fields the README documents as varying. Prior art: the Quick Cast test against `quick-cast.tsv`.
- **Percept projection (existing seam).** Prior art: the existing `concentration` and `effects` projection tests.
  - The pet window becomes a `petWindow` Percept for open, update and close, with the pet's id and both settings.
  - A Mind that takes over is sent the current pet window.
- **Protocol (existing seam).** `commandPet` reads from JSON. A missing `order` is named, and so is an unknown one. The codegen test fails until the TypeScript is regenerated.
- **Mind (existing seam).** `petWindow` Percepts set, update and clear `agent.self.pet`. Prior art: the existing Self tests for the concentration list.
- **Learnings (existing seam).** The new Learning's snippets typecheck as `execute_code` snippets.
- **Not tested automatically:** the live server and the coding agent, covered by the manual acceptance test.
- **Manual acceptance test:**
  1. Check in the capture with its README section.
  2. Restart the server so it runs this Harness.
  3. Start a new Claude Code session in `agents/fearnan` and tell it to keep levelling to 7.
  4. Leave it, nudging it only when it stops.
  5. Fearnan reaches level 7 with its pet out in most fights. It sends the pet in, and summons it again after the pet dies or after Fearnan dies.
  6. Record on this spec:
     - how it found the pet;
     - how often the pet was out, and which orders it used;
     - its resummons and deaths;
     - what the server answered, refusals included;
     - how far it got, how long it took and why it stopped each time;
     - its papercuts.
  7. Check the server log has no speed-check entries for Fearnan.

## Out of Scope

- The pet's effect icons in `petWindow`, and the pet's health in Self. Surroundings has the health, and anything more would come from a papercut.
- `rest` waiting for the pet's health, and `kill` sending the pet in. Better porcelain comes later.
- A pet porcelain Tactic, such as `summonPet` or `fightWithPet`. Fearnan writes its own.
- A dispatch check that a pet is out.
- Requiring Fearnan to train Nature. Call Minor Nature Spirit carries it to 7, and its spec choices are its own.
- Other classes' pets, charmed pets and novelty pets.
- Pets protecting a groupmate: milestone 11.

## Further Notes

- **Relevant decisions:**
  - ADR 0001: Percepts only as a client knows them. The pet window is what the 1.127 client is sent.
  - ADR 0002: Primitives go through the real handlers.
  - ADR 0004: results are dispatch-only, so whether an order worked comes from messages.
  - ADR 0009: Primitives and Percepts are added when the coding agent needs them.
- **Facts this spec relies on**, from the server and its database as they stand:
  - **Fearnan now:** level 5, Druid (class 47), with specs Nurture 4, Regrowth 1 and Nature 1. A Naturalist has no pet. Nature comes with the Druid.
  - **Druid pet Spells** are in the Druid Nature Spec line, of type `SummonDruidPet`. All are Target Self, cast in 3 s, cost 25% of Power, and last until the pet dies or is released.

    | Nature | Spell | Max pet level |
    |---|---|---|
    | 1 | Call Minor Nature Spirit | 7 |
    | 7 | Call Lesser Nature Spirit | 12 |
    | 12 | Call Nature Spirit | 20 |

  - **The pet** is 75% of the caster's level, rounded down: 3 at 5, 4 at 6, 5 at 7.
    - The Minor template is one of wolf cub, lynx cub, bear cub or minor nature spirit, at random.
    - It spawns just ahead of the caster.
    - The summon says "The {0} is now under your control." A second summon while a pet is out is refused with "You already have a charmed creature, release it first!".
  - **Pet behaviour** (the default controlled-mob brain):
    - It starts Defensive and Follow.
    - Defensive doesn't look for Mobs, and Aggressive does, within 512.
    - Passive never fights back.
    - It doesn't join the owner's attacks.
    - It defends the owner when the owner is attacked (unless Passive), and fights back when hit.
    - An `attack` order switches a Passive pet to Defensive.
    - Follow, Stay and Here make it disengage first, and an Aggressive pet with a target becomes Defensive.
    - It is released when more than 10,000 from its owner.
    - Its damage gives the Mob aggro on the pet and an extra 30% on its owner.
  - **Orders:**
    - `attack` needs a target the Character may attack within 2000, and doesn't check line of sight. Otherwise the server says "Your target is too far away to attack with your pet!".
    - `goTarget` needs a target within 1250. Otherwise it says "You must select a destination for your controlled creature." or "Your target is too far away for your pet to reach!".
    - Each accepted order says "You command {0} to …" (follow you, stay in this position, come to you, go to your target, kill your target, attack all enemies, defend you, disengage from combat).
    - `release` releases the pet. Losing control says "You lose control of {0}!".
  - **The pet window packet** (0x88) carries:
    - the pet's object id (0 when there is none);
    - the action (open, update or close);
    - aggression and walk;
    - up to 8 effect icons.

    It's sent on summon (open), when a setting or the pet's icons change (update), and when control is lost (close). It carries no health and no target: those come with the pet's ordinary NPC updates.
- **Milestone ladder:** `.scratch/milestones.md`. This spec is milestone 9.

## Comments
