# Spell kinds

Status: needs-triage

## Problem Statement

An Agent finds out what one of its Spells does only from the sentence in its tooltip. So every Tactic that picks a Spell matches the start of that sentence: Fearnan's `healSelf` looks for "Heals", Orla's `pull` for "Inflicts" (and reads the damage with `/Inflicts (\d+)/`), Orla's `buffUp` for "Increases your armor factor". A Spell whose sentence is worded differently, such as a heal over time, isn't found. Each new caster writes its own version.

Concentration buffs have a second naming problem. The concentration window cuts a name longer than 14 characters to its first 12 and "..", and `agent.self.concentrationBuffs` names them as the window does. So to tell whether a buff is up, Fearnan and the spells Learning each write a `cut()` helper to cut the Spell's name the same way. The client knows which of its own Spells each cut name is.

## Solution

`SpellDetails` gains `spellType`, the server's name for what the Spell does, such as `DirectDamage`, `Heal` or `ArmorFactorBuff`. It's static game data a Human reads on a fan wiki (ADR 0006). Self names a Concentration buff by its whole Spell name whenever it's one of the Agent's own Spells. The spells Learning picks Spells by `spellType` and drops `cut()`, and Orla's and Fearnan's Tactics follow.

## User Stories

1. As an Agent, I want each of my Spells to say what kind of Spell it is, so that I can pick a heal without matching words in its tooltip.
2. As an Agent, I want damage Spells told apart from damage over time and debuffs, so that I pull with the one that hits at once.
3. As an Agent, I want armour, strength and other buffs told apart by kind, so that I cast the one I want.
4. As an Agent, I want a heal over time to show as a heal of its own kind, so that I don't miss it or mistake it for an instant heal.
5. As an Agent, I want an Effect's details to carry the same `spellType`, so that I can tell what a debuff on me does.
6. As an Agent, I want the best Spell of a kind to be the highest level one, so that I can pick it by sorting on `level`.
7. As an Agent, I want `agent.self.concentrationBuffs` to name my own buffs by their whole Spell name, so that I can compare it with the name in `skills`.
8. As an Agent, I want a buff whose Spell I don't have to keep the name the window shows, so that I still see it.
9. As an Agent, I want `cancelConcentration` to keep taking the whole or the cut name, so that it works either way.
10. As a coding agent, I want the spells Learning to pick Spells by `spellType`, so that I don't copy the prefix matching.
11. As a coding agent, I want the spells Learning to drop `cut()`, so that I compare names directly.
12. As Orla, I want `pull` and `buffUp` to pick their Spells by `spellType`, so that they don't depend on how a tooltip is worded.
13. As Fearnan, I want `healSelf` and `buffSelf` to pick by `spellType` and compare buffs by whole name, so that they lose their helpers.
14. As the Human, I want `spellType` to be the server's own name for the kind of Spell, so that it matches what a fan wiki lists.

## Implementation Decisions

- **Fairness (ADR 0006).** What a Spell does is static game data: fan wikis list each Spell's type. A Percept may carry it, as the skills Percept already carries a Specialization's database name.
- **Projection (Harness).** `SpellDetails` gains `spellType`, the server's spell type name. It's filled wherever `SpellDetails` is, for Spells in the skills Percept and for Effects alike.
- **Protocol.** The field's doc comment says it's the server's name for the kind of Spell, and the TypeScript is generated again.
- **Self.** When a `concentration` Percept arrives, Self names each buff by the whole name of the Agent's own Spell that cuts to it. A buff with no such Spell keeps the name the window shows. The cut rule matches the Harness's (14 characters, cut to 12 and "..").
- **Wiki.** Unchanged. The class pages already show what each Spell does.
- **Learning.** The spells Learning's sections on finding a Spell, healing and Concentration buffs use `spellType` and whole names, and lose `cut()`.
- **Agents' Tactics.** Orla's `damageSpell` and `buffUp`, and Fearnan's `healSelf` and `buffSelf`, pick by `spellType`. Fearnan compares buffs by whole name. Orla's choice of damage Spell may keep reading the damage from the description, which is what the tooltip shows.

## Testing Decisions

- A good test checks what the Mind receives and believes, not how it's built.
- **Projection:** a direct damage Spell, a heal and a buff in the skills Percept each carry their `spellType`, and so does an Effect's details. Prior art: the PerceptProjection unit tests for Spell details.
- **Self:** a cut buff name that matches one of the Agent's Spells shows as the whole name. One that matches none keeps the cut name. Prior art: the Self tests for Concentration buffs.
- **Manual acceptance test:**
  1. Restart the server so it runs this Harness.
  2. Fearnan, after dying and releasing, casts its Concentration buffs again with `buffSelf`, and on its next run casts none it already keeps up.
  3. Orla's `pull` picks the same damage Spell as before.
  4. Record what each picked on this spec.

## Out of Scope

- A number for what a Spell does (damage, heal or bonus), beyond what its description shows.
- A type column on the Wiki's class pages.
- Kinds for Styles or Abilities.
