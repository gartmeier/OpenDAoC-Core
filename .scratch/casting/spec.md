# Casting

Status: resolved

## Problem Statement

An Agent casts a Spell with `castSpell` and then learns how it went only from the chat. Each Tactic that casts writes its own `sendAndWait` around `castSpell`, with its own regex over the server's lines ("You cast a", "interrupt", "must wait", "enough power", "too far away", …). Orla has three of these and Fearnan two, and the spells Learning teaches the pattern in three more snippets, so every new caster copies it again. The copies don't agree:

- Fearnan waits out "You must wait N seconds" for N seconds and casts again. Orla's `pull` waits a fixed second, and Orla's `buffUp` gives up and never checks that the buff went up.
- Orla's `pull` mixes "the Mob hit me" into the same regex as the cast's own answers, and its `quickCast` doesn't match "must wait" at all.
- A line the regex doesn't know, or a cast that lands with no line the regex matches, ends as a 10 s timeout that looks the same as "the server ignored it".

The client is told much more than the chat. It's sent a packet when any caster in view starts a cast and how long it takes, one when a Spell lands on its target and whether it took, one when a caster is interrupted, and one with each Spell or Ability that goes on Cooldown and for how long. The Harness passes all four on as bare Percepts that name only the call, so the Mind can't use them.

## Solution

One simple porcelain Tactic, `cast`, casts a Spell at a target and ends with an Outcome that says why it failed. It checks what it can before sending anything, gets the Character ready, optionally uses Quick Cast first, waits out the cast delay after a fight by itself, and reads the answer from the new Percepts and, for refusals the server gives only as chat, from the chat.

The Harness projects the four packets as Percepts, and Self believes which Spell the Agent is casting and which of its skills are on Cooldown.

Orla's and Fearnan's casting Tactics are rewritten on `cast`, Orla's `quickCast` helper goes away, and the spells Learning teaches `cast` instead of the regex.

## User Stories

1. As an Agent, I want to cast a Spell at a target with one call, so that I don't write a `sendAndWait` and a regex every time.
2. As an Agent, I want `cast` to succeed only once the Spell has landed, so that a success means the Spell did something.
3. As an Agent, I want an instant Spell to succeed as soon as it lands, so that `cast` doesn't wait for a casting time that isn't there.
4. As an Agent, I want an instant heal or buff to succeed although the server sends no start for it, so that friendly instants work like any other Spell.
5. As an Agent, I want a bolt to succeed when it's launched, so that `cast` doesn't wait for the bolt to fly.
6. As an Agent, I want `cast` to fail with `noEffect` when the Spell lands but doesn't take (resisted, nothing to heal, or a better buff already up), so that I don't think it worked.
7. As an Agent, I want `cast` to fail with `interrupted` when something hits me mid-cast, so that I know to deal with the Mob first.
8. As an Agent, I want `cast` to wait out "You must wait N seconds" and cast again by itself, up to 3 casts in all, so that the first cast after a fight doesn't fail.
9. As an Agent, I want the Cooldown's and the potion's "wait N seconds" lines handled the same way, so that a Cooldown the client thought was over doesn't fail the cast.
10. As an Agent, I want `cast` to fail with `mustWait` when the wait keeps coming back, so that I notice something is still swinging at me.
11. As an Agent, I want `cast` to fail with `noSpell` before sending anything when I don't have the Spell, so that a typo doesn't look like a server refusal.
12. As an Agent, I want `cast` to fail with `cooldown` before sending anything when the Spell is on Cooldown, so that I can pick another Spell.
13. As an Agent, I want `cast` to fail with `noPower` before sending anything when I haven't the Power, so that I rest instead.
14. As an Agent, I want `cast` to fail with `wrongTarget` before sending anything when I aim a friendly Spell at a Mob, so that a cast the server drops without a word doesn't end as a timeout.
15. As an Agent, I want `cast` to fail with `tooFar`, `notInView` or `noTarget` from the server's answer, whether it comes before the cast starts or as it ends, so that I can move closer, find the Mob or target it.
16. As an Agent, I want any other refusal to end the cast as `refused`, with the server's words in the log, so that I can learn what it means.
17. As an Agent, I want `cast` to end with `timeout` if the server doesn't answer at all, so that a Spell the server silently ignores doesn't hang my Tactic (ADR 0008).
18. As an Agent, I want `cast` to stand me up first if I'm sitting, so that "You can't cast while sitting" never comes.
19. As an Agent, I want `cast` to stop a run in progress first, so that moving doesn't interrupt my own cast.
20. As an Agent, I want `cast` to target what I give it before casting, so that I don't send `target` myself each time.
21. As an Agent, I want to cast a heal or a buff on myself by giving my own object id, so that friendly Spells work the same way as damage Spells.
22. As an Agent, I want to pass `quickCast` to `cast`, so that a Spell I need to get off while a Mob hits me isn't interrupted.
23. As an Agent, I want `cast` with `quickCast` to turn my attack off and use Quick Cast first, so that "You can not cast when you are in melee combat" never comes.
24. As an Agent, I want `cast` with `quickCast` to fail with `cooldown` before sending anything when Quick Cast is on Cooldown, so that I can cast without it or fight on.
25. As an Agent, I want `cast` with `quickCast` to fail with `noQuickCast` when Quick Cast doesn't come up, so that I don't cast a normal Spell thinking it's quickcasted.
26. As an Agent, I want `agent.self.casting` to say which Spell I'm casting and when it should land, so that I don't start a second one on top of it.
27. As an Agent, I want `agent.self.casting` to clear when my cast lands or is interrupted, so that I can trust it between casts.
28. As an Agent, I want `agent.self.cooldowns` to say when each of my Spells and Abilities is ready again, so that I can choose one that's ready.
29. As an Agent, I want a Percept when a Mob in view starts casting, with how long it takes, so that I can see a caster Mob coming.
30. As an Agent, I want a Percept when a Mob in view is interrupted, so that I can tell my hits stopped its Spell.
31. As an Agent, I want a Percept when a Spell lands on someone in view, saying who cast it, on whom and whether it took, so that I can tell a Spell landed on me.
32. As an Agent, I want the Journal to show cast starts, landings, interrupts and Cooldowns, so that the Human can follow a fight.
33. As the Human, I want these Percepts to carry only what the client is sent, so that the Agent knows no more than a client (ADR 0001).
34. As the Human, I want `castSpell` to stay a Primitive that reports dispatch only, so that `cast` is the porcelain over it (ADR 0004, ADR 0008).
35. As a coding agent, I want the spells Learning to teach `cast`, so that I don't copy the old regex.
36. As Orla, I want `pull` and `buffUp` built on `cast`, and `quickCast` replaced by `cast`'s flag, so that each handles every answer the same way.
37. As Fearnan, I want `healSelf` and `buffSelf` built on `cast`, so that they lose their own regexes.

## Implementation Decisions

- **Why now.** The Spells, Damage spells and Abilities milestones left Percepts for the cast bar, interrupts and Cooldowns out until an Agent needed them (ADR 0009). Two casters now each parse the chat in their own way, and some of those copies go wrong.
- **Projection (Harness).** Four calls the projection passes on as bare Percepts today become typed Percepts, each mirroring what the client is sent:
  - Spell cast animation → `castStarted { casterId, castingMs }`. It's sent for any caster in view, Mobs included, after every check the server makes before a cast starts. An instant harmful Spell sends it with 0; an instant friendly Spell doesn't send it at all. The client effect number isn't passed on: a client shows it only as an animation.
  - Spell effect animation → `spellLanded { casterId, targetId, success }`. `success` is the packet's success byte, and `casterId` is 0 when the caster is out of the target's view or a player target can't detect it, as the packet writes it. It's 0 for a resist, for a heal with nothing to heal and for a buff a better one kept off. A bolt sends it with 1 at launch, and a miss shows only in the chat. Pulsing Spells send it on every pulse, and AoE Spells once per target.
  - Interrupt animation → `interrupted { objectId }`. The server sends it for an attack or a move mid-cast, not for a cast that fails its checks as it ends.
  - Disable skill → `cooldowns { skills: { name, seconds }[] }`. The packet names each skill by its place in the client's own skill lists; the projection names it as the skills Percept names it. The server sends nothing when a Cooldown ends; it sends 0 only for skills listed at world entry.
- **Protocol.** The four Percepts are added to the Mind protocol and the TypeScript is generated again.
- **Self.**
  - `casting`: undefined, or `{ spell, landsAt }`. It's set by a `castStarted` whose caster is the Agent, naming the Spell from the last `castSpell` sent, as a client knows which icon it clicked. It clears on a `spellLanded` or an `interrupted` from the Agent, or once `landsAt` is well past. An instant friendly Spell never sets it.
  - `cooldowns`: each skill name with the time it's ready again, on the Agent's clock, from the `cooldowns` Percept. Names past their time aren't listed.
- **The `cast` Tactic.** It lives with the other porcelain. `cast(agent, spell, targetId, { quickCast? })` returns an Outcome.
  1. **Checks, sending nothing:** `noSpell` (the Spell isn't in `skills`), `cooldown` (it's in `cooldowns`, or `quickCast` is set and Quick Cast is), `noPower` (Power is below its cost) and `wrongTarget` (a Realm or Group Spell, and `targetId` is a Mob; a Self Spell lands on the caster whatever is targeted).
  2. **Getting ready:** it stands up if sitting, stops a run in progress and targets `targetId`.
  3. **Quick Cast:** with `quickCast`, it turns attack off, uses Quick Cast and waits a few seconds for the "Quick Cast" Effect. Without it, it fails with `noQuickCast`. It never waits for Quick Cast's Cooldown.
  4. **Casting:** it sends `castSpell` and takes the first answer:
     - a `spellLanded` from the Agent: it succeeds, or fails with `noEffect` when success says it didn't take;
     - an `interrupted` from the Agent: `interrupted`;
     - a chat line of the type the server gives every cast refusal (`SpellResisted`):
       - any "wait N seconds" line (the hold after a swing, a Cooldown, a potion): it waits N seconds, plus a margin since the server rounds down, and casts again, at most 3 casts in all, then fails with `mustWait`;
       - "resists the effect": no answer, since the landing's success already says so, and the Agent's Pet's resists come to it as the same line;
       - too far away: `tooFar`; can't see or not visible: `notInView`; select a target: `noTarget`; not enough Power: `noPower`;
       - any other line: `refused`, with its words in the log.
  5. **Timing out:** with no answer within 3 s of sending, it fails with `timeout`. A `castStarted` from the Agent pushes that out to its casting time plus 2 s. Both are constants, not parameters.
- **Attack.** `cast` turns attack off only for Quick Cast. The server stops the Character's attack by itself when a Spell with a casting time starts.
- **Learning.** The spells Learning's Casting, Healing, Concentration and Quick Cast sections use `cast`, and its table of chat lines becomes the list of `cast`'s reasons. It says a bolt's miss shows only in the chat. The guide adds `cast` to the list of porcelain Tactics.
- **Agents' Tactics.** Orla's `pull` and `buffUp` and Fearnan's `healSelf` and `buffSelf` call `cast` and lose their casting regexes. Orla's `quickCast` helper is deleted; `pull` casts with `quickCast` once the Mob is on it. `pull` reads `cast`'s Outcome and then Surroundings to see whether the Mob is on it, without watching for hits while `cast` runs.

## Testing Decisions

- A good test drives the Agent with Percepts and checks the Primitives sent and the Outcome, never how `cast` tracks its state.
- **Projection:** each of the four calls is projected with the fields above. A disabled Spell and a disabled Ability are named by their skill names. Prior art: the PerceptProjection unit tests.
- **Protocol:** the four Percepts serialise as the generated TypeScript expects. Prior art: the MindProtocol unit tests.
- **Self:** `casting` is set by the Agent's own `castStarted`, ignores another caster's and clears on landing, on interrupt and once `landsAt` is past. `cooldowns` lists a skill until its time is up. Prior art: the Self tests.
- **`cast`:** one test per Outcome, each feeding the Percepts or chat line that leads to it on the fake clock: lands after a start, instant harmful, instant friendly with no start, `noEffect` from success 0 and from a resist line, `interrupted`, must wait then lands, must wait three times, each refusal line before a start and as the cast ends, an unknown line, no answer, a start with no landing, and each check made before sending. Also standing up first, stopping a run first, and `quickCast`: attack off and Quick Cast before the cast, Quick Cast on Cooldown, and Quick Cast not coming up. Prior art: the `kill` and `rest` tests with the TestAgent and its fake clock.
- **Manual acceptance test:**
  1. Restart the server so it runs this Harness.
  2. Orla grinds to level 7 with its rewritten `pull` and `buffUp`, quickcasting once a Mob is on it.
  3. Record on this spec each `cast` Outcome seen and anything that timed out.

## Out of Scope

- Naming the Spell another caster casts. A client sees only its animation.
- The spell queue, and casting a second Spell while one is in progress.
- Answering the server's line-of-sight check.
- Kiting porcelain.
- Cooldowns for Styles, which the Combat results spec may take up.
- Telling a pulse of a song or pulsing Spell from a new cast's landing in `self.casting`. No Agent plays a class with them yet.

## Further Notes

- Order of what a client sees for a cast of its own:
  - a Spell with a casting time: `castStarted` (its time), then after that time `spellLanded`;
  - hit before the halfway point: `castStarted`, then `interrupted`;
  - instant harmful Spell or bolt: `castStarted` (0), then `spellLanded`;
  - instant heal or buff: `spellLanded` only;
  - refused before it starts: a chat line only;
  - failing its checks as it ends: `castStarted`, then a chat line only.
- The client's own Cooldown ends about a second before the server's, so a cast right at its end gets "You must wait 1 second to recast this type of spell!".
- How an attack interrupts a cast, from the server's code, is in the spells Learning: a swing that comes before the halfway point interrupts the cast, and every swing holds the next cast off for a few seconds.

## Comments

**2026-10-27 (agent):** The acceptance run, from Orla's Claude Code session `94f5335f-fc34-4cf0-95a0-638b70a28cb3` (about an hour, from 28% of level 5 to level 7, about 100 kills, no deaths). Journals 2026-10-26T21-10-56-952Z and 2026-10-26T21-25-24-011Z.

- **Landed.** Every Shadowblast that reached a live Mob landed, and `pull` cast on after each. Lesser Powerbarrier from `buffUp` landed on Orla.
- **`noEffect`.** 26 resists, each read from the landing's success 0, not the resist line.
- **Quick Cast.** 11 `landed`. 8 `noTarget`, when `pull` quickcast at a Mob that had just died: Orla's `pull`, not `cast`. 1 `cooldown`, caught before sending.
- **`tooFar`.** 4, and `pull` ran closer and cast again.
- **`noSpell` and `wrongTarget`.** Orla tried each on purpose, and nothing was sent.
- **`interrupted`.** 9 hits interrupted a cast. The first came back as `refused`, because the server sends "... attacks you and your spell is interrupted!" as `CT_SpellResisted` just before the interrupt. Fixed in 99f5be04c.
- **`refused`.** 2, "You can't cast while sitting!". A feccan came at Orla as `rest` sat her down, and the server silently drops a `/stand` within 1.5 s of the `/sit`. `cast` now waits until the server says it stands, stands again once, and fails with `sitting` otherwise. Fixed in 895e2fc75.
- **Timeouts.** One Quick Cast `timeout`, cut short by a Mind restart. None otherwise.
- **`mustWait`, `noPower`, `notInView`, `noQuickCast`.** Didn't come up.
- **Other papercuts.** A `runTo` whose `moveTo` the Harness refused waited out its 5 minutes. `sendAndWait` now gives up at once on a Primitive that isn't sent (895e2fc75). A status once read "power 83% (31/31)" for a second, from the server's own packet, which the next status put right. Left as it is.
