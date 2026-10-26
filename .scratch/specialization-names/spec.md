# Specialization names

Status: resolved

## Problem Statement

Orla, an Eldritch, can't train its Light Magic line with the `train` Tactic and be told it worked. `skills` names a Specialization by the name the client shows, "Light Magic", but `/train` looks lines up by their database name, "Light". So `train(agent, "Light Magic", 3)` fails with "The provided skill could not be found.", and `train(agent, "Light", 5)` trains the line but still fails with `notTrained`, because `agent.self.specs` has no "Light". Orla's workaround is to train with "Light" and check `specs` itself, which its papercut and a Lesson record.

The same split hits every Specialization whose two names differ: Light, Mana and Void (the "… Magic" lines), and Bows and Shields (shown as "Bow" and "Shield"). Styles already name their line by the database name, so a Shield Style's `line` is "Shields" while `specs` has "Shield".

## Solution

The skills Percept names a Specialization by its database name: "Light", not "Light Magic". `specs`, a Style's `line` and `/train` then agree on one name, and `train(agent, "Light", 5)` trains the line and succeeds. Neither `/train` nor the Mind's `train` changes.

## User Stories

1. As an Agent, I want `train(agent, "Light", 5)` to succeed once Light Magic is at 5, so that I can trust its Outcome.
2. As an Agent, I want `agent.self.specs` to name each line as `/train` takes it, so that I can pass a key from `specs` straight to `train`.
3. As an Agent, I want a Style's `line` to match a key in `specs`, so that I can tell how far a line is from the Style's level without mapping names.
4. As an Agent playing a shield class, I want `specs` to say "Shields", so that it matches my Shield Styles' `line`.
5. As an Agent playing an archer, I want `specs` to say "Bows", so that `train` with that name waits on the right key.
6. As an Agent, I want lines whose two names are the same, such as Blades or Celtic Dual, to stay as they are, so that my Tactics keep working.
7. As an Agent, I want Abilities, Spells and Styles to keep the names they have, so that `useAbility`, `castSpell` and `useStyle` work unchanged.
8. As an Agent, I want the Learning on training to say how `specs` names a line, so that I don't try the name the client shows.
9. As Orla, I want my papercut about `train` removed and my Lesson brought up to date once it's fixed, so that I don't keep an outdated workaround.
10. As the Human, I want a Percept to carry a line's database name only where fair, so that the Agent knows no more than a Human could look up (ADR 0006).
11. As the Human, I want the core `/train` command left as it is, so that the experiment doesn't change the game.
12. As the Human, I want the Journal to show the same name the Mind used, so that I can read a training step without translating names.

## Implementation Decisions

- **Fairness (ADR 0006).** A Specialization's database name is static game data a Human learns from a fan site or by trying `/train`, so a Percept may carry it. ADR 0006 already has this as a consequence.
- **Projection.** A Specialization entry in the skills Percept carries the Specialization's database name (its `KeyName`) as its `name`, encoded as other names are. Its level is unchanged. Every other skill page keeps the name the packet carries.
- **Protocol.** The entry's shape stays `{ page: "Specialization", name, level }`. Its doc comment says `name` is the database name, such as "Light" for Light Magic, and the TypeScript is regenerated so the comment reaches the Mind.
- **Harness.** Nothing changes: it reads Specialization entries only to count where the skills after them start.
- **Mind.** Nothing changes: `train` types `/train <line> <level>` and waits for `specs.get(line)`, which now agree.
- **`/train` stays as it is.** The server isn't changed to accept display names.
- **Learning.** The Training section of the fighting Learning says `specs` names a line as `/train` takes it, such as "Light" for Light Magic.
- **Orla's files.** Remove the `train` papercut from Orla's papercuts, and make its Lesson say to train Light with `train(agent, "Light", n)`, without the advice to ignore `notTrained`. Leave Orla's other uncommitted changes as they are.

## Testing Decisions

- A good test checks what the Mind receives, not how the projection builds it.
- **Projection:** a Specialization whose database and display names differ ("Light" and "Light Magic") is listed as a Specialization entry named "Light" with its level. Prior art: the `SkillEntries` tests in the PerceptProjection unit tests, which already list Specialization, Ability and Style entries.
- The existing Harness and protocol tests already use database names ("Light", "Void", "Shields") and keep passing.
- **Not tested automatically:** training in game, covered by the manual acceptance test.
- **Manual acceptance test:**
  1. Restart the server so it runs this Harness.
  2. Start a Claude Code session in `agents/orla` and tell it to keep levelling.
  3. At its next level, Orla trains Light with `train(agent, "Light", n)`, and it returns `{ ok: true }`.
  4. Record on this spec what `specs` showed, what `train` returned and what the server said.

## Out of Scope

- Training through the trainer window, as the 1.127 client does. `/train` stays the way to train.
- Changing `/train` to accept the name the client shows.
- Database names for Abilities: `useAbility` looks them up in the skill list the Harness sent, so its names never clash.
- A glossary entry: `CONTEXT.md` doesn't change.

## Further Notes

- The Specializations an Agent can train whose names differ, from the server's database: Light (Light Magic), Mana (Mana Magic), Void (Void Magic), Bows (Bow) and Shields (Shield). The other differences are careers and Master Levels, which `/train` doesn't train.
- `/train` matches a line by the start of its database name, so `/train Bow` already reached Bows. A rule like "take the first word" would be wrong: `/train Celtic` matches more than one line.
- The client trains by position in the trainer window and uses an ability by position in the skill list. Only `/train` names a line, and it names it by database name.
- The TypeScript generator doesn't carry doc comments, so the comment on the Specialization entry stays in the C# and `protocol.ts` didn't change. The Learning tells the Agent which name `specs` uses.

## Comments

**2026-10-26 (agent):** The acceptance check, from Orla's Journal `2026-10-26T20-34-58-161Z.log`, at level 5 with 5 spec points:

- `train(agent, "Light", 5)` sent `/train Light 5`. The server answered "You spend 5 points and specialize further in Light Magic", "You have 0 specialization points left this level." and "You gain power in the Shadow Control spell list!".
- The next skills Percept listed Light 5, Mana 1 and Void 1, which is what `train` waits for, and the snippet succeeded. The Journal doesn't record its return value. Shadowblast appeared.
- No papercut about training since.
