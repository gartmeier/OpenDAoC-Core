# Abilities

Status: resolved

## Problem Statement

Orla became an Eldritch at the end of milestone 8, and an Eldritch has Quick Cast: the next Spell can't be interrupted, so it lands even with the Mob already hitting. Quick Cast shows in `skills` as an Ability, but no Primitive can use one. `useStyle` matches only Styles and `castSpell` only Spells. The same goes for Sprint, which only `/sprint` reaches, and for Realm Abilities later on.

## Solution

`useAbility { ability }` uses an Ability or a Realm Ability by its name in `skills`, as a click on its icon does. The spells Learning gains a section on Abilities and Quick Cast, with the Human's routine: cast from range until the Mob reaches you, Quick Cast and cast once more, then the staff, or kite.

## Implementation Decisions

- **Capture.** The Human's capture from account `admin` on 2026-10-25, of Vue, a level 50 Mentalist, clicking Quick Cast four times, is checked in as `quick-cast.tsv` with an Abilities section in the fixtures README. Quick Cast is the skill packet (0xBB), byte for byte as a Style or a hybrid's Spell, at index 3.
- **Primitive (ADR 0002).** `useAbility { ability }` sends the skill packet with the index of the Ability or Realm Ability of that name in the skill list the server last sent, counting as for `useStyle`. Anything else of that name, or nothing, is refused at dispatch: `Orla has no Ability "X".`. Whether it worked comes from the server's messages (ADR 0004).
- **Names** are the Ability's name in the database, as the skill list carries them: Quick Cast is "Quickcast", one word. Its Effect in the buff bar is "Quick Cast", from the server's language file.
- **Realm Abilities** are Abilities to the server, used by the same handler, so the same Primitive covers them.
- **`/sprint`** keeps working through `command`.
- **Codegen.** As before: the C# record, regenerated TypeScript. The Journal names the ability it used.
- **Learning.** In the spells Learning: what `useAbility` does, Quick Cast's messages and rules from the server's code, the Human's routine, and a snippet for its last two steps. The Kiting section mentions sprinting with `useAbility`.

## Testing Decisions

- **Packet builder:** golden bytes for the four Quick Cast clicks against `quick-cast.tsv`.
- **Harness dispatch:** on an Eldritch's skill list, Sprint, Quick Cast and a Realm Ability queue the skill packet with their indexes; an unknown name, a specialization's, a Spell's and a Style's are refused, and so is everything before any skill list. `castSpell` and `useStyle` refuse an Ability's name.
- **Protocol:** the Primitive reads from JSON, and a missing `ability` is named.
- **Learnings:** the new snippet typechecks.
- **Not tested automatically:** using Quick Cast in a fight, covered by the manual acceptance test.
- **Manual acceptance test:**
  1. Restart the server so it runs this Harness.
  2. Start a new Claude Code session in `agents/orla` and tell it to keep levelling.
  3. Leave it, nudging it only when it stops.
  4. Orla finds `useAbility` and uses Quick Cast when a Mob reaches it.
  5. Record on this spec:
     - whether it found Quick Cast on its own, or from the Learning;
     - how often it used it, and whether it saved fights;
     - what the server answered each time, refusals included;
     - its papercuts.
  6. Check the server log has no speed-check entries for Orla.

## Out of Scope

- Percepts for an ability's reuse timer: the server's messages say it.
- Abilities that are Spells (the `AbilitiesSpell` page).

## Further Notes

- **Quick Cast, from the server's code.** Eldritch, Enchanter and Mentalist have it from promotion. It lasts 3 s, and ends without a message if no Spell starts. The quickcasted Spell can't be interrupted and costs twice the Power. Its cast time doesn't change, whatever the tooltip says. After it, Quick Cast can't be used for 30 s. It's refused while attack is on, while casting, and when dead. Using it again while it's up turns it off.
- Follows the note on `.scratch/damage-spells/spec.md`.

## Comments

**2026-10-26 (agent):** The acceptance run, from Orla's Claude Code session `23679387-da03-4840-9861-3de702024605` (about 7 minutes):

- **Finding it.** Asked to show what it saw, Orla listed Sprint, Quickcast, Weaponry: Staves and Armor Ability: Cloth on its Abilities page, and noticed "Quickcast" is one word. The Learning still said "Quick Cast" then. The Human fixed it, and Orla read it again. In town, `useAbility "Quickcast"` answered "You have activated quickcast.", a second use "Your next spell will not be quickcasted.", and the "Quick Cast" Effect ended after 3 s without a cast. Once, `effects` was still empty when it read it right after the message, so Orla waits for the message.
- **In a fight.** The Human told it to try Quickcast on a Mob. Two mudmen:
  - Level 4: Quickcast never went off. Orla tried it right after the first hit, while it was still casting, and the server refused: "You can't prepare a quickcast while casting a spell!". It finished the fight with the staff at 16% health.
  - Orla then gave its own `hunt.ts` a `quickCast` that waits for the running cast to land or break and tries once more.
  - Level 3: two casts from range hit for 15 and 18. A third was hit mid-cast without being interrupted, then resisted. Then "You have activated quickcast.", and the quickcasted Major Ethereal Concussion hit for 28 with the mudman on Orla. One staff hit killed it, and Orla ended at 80% health.
- **What it got wrong in the Learning.** The Learning said every hit interrupts a cast. On this server a hit starts an interrupt timer as long as the attacker's swing. A cast is interrupted only if it reaches its halfway point while the timer runs, and no new cast can start meanwhile. Quickcast skips both. The Learning now says so, and its snippet waits for a running cast before trying Quickcast again.
- **Power.** A quickcasted Major Ethereal Concussion cost 8, twice as much. Orla keeps that much back.
- **Papercuts.** None about `useAbility`. Orla's own `pull` reported a kill as `outOfView` because the corpse left its Surroundings first, and it will fix that itself.
- **Speed checks.** None for Orla in the server log.
