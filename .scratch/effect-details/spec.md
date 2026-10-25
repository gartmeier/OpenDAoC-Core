# Effect details

Status: ready-for-agent

## Problem Statement

Dalta, a coding agent, sees each Effect on its Character only by name, whether it's a buff and its time left. A Human sees much more: hovering a spell Effect's icon in the buff bar shows its tooltip, and right-clicking it opens the same as a window: its level, duration, power cost, target, range, casting time, magic type and a sentence saying what it does, such as "The target's speed is increased to 147% of normal for 10 minutes." The server pushes that tooltip to a 1.110+ client with the buff bar, but the Harness never passes it on, so Dalta can't tell what a debuff a Mob put on it does, whether a buff reached everyone near or only it, or which resist a debuff goes against.

## Solution

Every spell Effect in the Mind's buff bar carries its details: the fields of its tooltip window, each named after the window's label, with values a Tactic can read directly and the window's own words for the target and the magic type. An ability Effect, such as Sprint, Stealth or Quick Cast, has no details, as a Human's client shows only its name and time left and right-clicking it does nothing. The Agent then knows what a Human sees of each Effect, and no more.

## User Stories

1. As an Agent, I want each spell Effect's description, so that I know what it does to me.
2. As an Agent, I want a description with the spell's own numbers, such as "increased to 147% of normal", so that I know how strong it is.
3. As an Agent, I want a spell Effect's level, so that I can compare it with other Effects of its kind.
4. As an Agent, I want a spell Effect's full duration in seconds, so that I can tell how long a fresh one lasts against its time left.
5. As an Agent, I want a spell Effect's target in the client's words, such as "Realm" or "Realm area effect", so that I know whether it reached only me or everyone near.
6. As an Agent writing a Tactic, I want to compare the target with a plain string, so that a Tactic can tell an area buff from a single-target one without parsing text.
7. As an Agent, I want a spell Effect's magic type in the client's words, such as "Energy", so that I know which resist a debuff goes against.
8. As an Agent, I want a spell Effect's power cost, range, area radius and casting time when the window shows them, so that I know what it took to cast.
9. As an Agent writing a Tactic, I want an instant spell's casting time as 0, so that I don't need a special case.
10. As an Agent, I want a field the window doesn't show left out, so that I read only what a Human sees.
11. As an Agent, I want details on a debuff a Mob put on me, so that I can tell what it does before reacting.
12. As an Agent, I want details on a Style's proc that landed on me, such as a slow, so that I know how it slows me.
13. As an Agent, I want no details on an ability Effect such as Sprint, Stealth or Quick Cast, so that I know no more than a Human, whose client shows only its name and time left.
14. As an Agent, I want details with every buff bar, so that a Mind that started or took over mid-session knows them too, though a client is sent each tooltip only once.
15. As an Agent writing a Tactic, I want to read an Effect's details from the Self, so that a Tactic can check a buff's target or magic type.
16. As an Agent whose skill pages will one day list Spells, I want the same details shape for them, so that a Spell I can cast and an Effect on me read alike.
17. As a Human studying the Journal, I want the Effects Percept recorded with its details, so that I can see what the Agent knew.

## Implementation Decisions

- **Details.** A new protocol record, `SpellDetails`, holds a spell's tooltip window. It's named after the Spell rather than the Effect, so the Spells on a caster's skill pages can reuse it later. Its shape:

  ```ts
  details?: {
    level: number;            // Level: 44
    durationSeconds?: number; // Duration: 20 minutes → 1200
    powerCost?: number;       // Power cost: 28
    target: string;           // Target: "Realm", in the client's words
    range?: number;           // Range: 1000
    castingSeconds: number;   // Casting time: 4.0 → 4, instant → 0
    magicType?: string;       // Magic Type: "Energy"
    radius?: number;          // Area radius: 350
    description: string;      // "The target's melee attackers receive 4.6 damage per second for 20 minutes."
  }
  ```

- **Where it comes from.** The 1.127 client draws its tooltip and its right-click window from the structured spell tooltip the server pushes with the buff bar for every spell Effect, because the server property for forced tooltip updates is on (it's on in this server's database, and that's the default). The projection builds `SpellDetails` from the same spell fields that tooltip carries, mirroring the server's spell tooltip builder, without its index and function fields. The description is the server's own sentence for the spell, with a sub-spell's lines after it, as the tooltip carries it.
- **Which fields are present.** A field is present only when the client's window shows it: no duration for a spell without a timer, no power cost, range or radius when 0, and no magic type for a spell without one. Level, target, casting time and description are always present.
- **Target and magic type names.** The tooltip carries them as ids the client turns into words. The projection gives the client's words: "Realm" and "Energy", as seen in the client, and "Realm area effect" for a realm spell with an area radius, as seen for a Hastener's speed buff. Words for the other ids come from the client's text where they can be found; an id without known words is named after the server's own enum.
- **Every bar.** Each Effects Percept carries every spell Effect's details, though the server sends a client each spell's tooltip only once, as the client keeps it. A Mind can start or take over while the Character stays in the world, and the client would still know the tooltip.
- **Ability Effects.** An Effect that isn't a spell has no details: the server pushes no tooltip for it, and answers no right-click on its icon.
- **Self.** Each Effect on the Self has the details its last Percept carried, absent for an ability Effect.
- **Journal.** The Effects Percept's Journal line gains each Effect's description.
- **Protocol.** The record is added to the Mind protocol and its generated TypeScript types.
- **Docs.** The moving Learning's Hastener section mentions the description. No glossary term: a Spell's details are what the client shows of a Spell. No ADR.

## Testing Decisions

- Tests check what goes in and comes out at a seam: the Percept a server call gives, and what the Self believes after a Percept. They don't check how the projection reads the spell.
- **Projection** (the existing projection tests): a spell Effect on the test player comes out with its details, each field named and valued as the window shows it, including the target and magic type in the client's words, the duration in seconds, an instant cast as 0 and the server's description; fields the window leaves out are absent; a realm spell with a radius is "Realm area effect"; an ability Effect has no details. The world test doubles gain a spell Effect made from a real Spell and its spell handler, so the description is the server's own.
- **Self** (the existing Self tests): an Effect keeps the details its Percept carried; an ability Effect has none.
- Prior art: the buff bar projection tests and the Self's Effects tests from the Effects feature; Style details on skill entries for a details record built from what a tooltip shows.

## Out of Scope

- Details for ability Effects such as Sprint, Stealth and Quick Cast, which a Human's client doesn't show.
- Details for the Spells on a caster's skill pages, which can reuse `SpellDetails` later.
- Songs' extra tooltip fields.
- A Primitive to examine an Effect.
- Recast time, concentration and frequency, which the windows seen so far don't show.

## Further Notes

- Seen in the client: "Aura of Global Feedback" shows Level 44, Duration 20 minutes, Power cost 28, Target Realm, Range 1000, Casting time 4.0 seconds, Magic Type Energy, and its description. A Hastener's "Speed of the Realm" shows Level 1, Duration 10 minutes, Target Realm area effect, Range 1500, Area radius 350, Casting time Instant, and its description.
- The window's durations read as minutes ("20 minutes"); `durationSeconds` gives the same as seconds.
