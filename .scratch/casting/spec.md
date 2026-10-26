# Casting

Status: needs-triage

## Problem Statement

An Agent casts a Spell with `castSpell` and then learns how it went only from the chat. Each Tactic that casts writes its own `sendAndWait` around `castSpell`, with its own regex over the server's lines ("You cast a", "interrupt", "must wait", "enough power", "too far away", …). Orla has three of these and Fearnan two, and the spells Learning teaches the pattern in three more snippets, so every new caster copies it again. The copies don't agree:

- Fearnan waits out "You must wait N seconds" for N seconds and casts again. Orla's `pull` waits a fixed second, and Orla's `buffUp` gives up and never checks that the buff went up.
- Orla's `pull` mixes "the Mob hit me" into the same regex as the cast's own answers, and its `quickCast` doesn't match "must wait" at all.
- A line the regex doesn't know, or a cast that lands with no line the regex matches, ends as a 10 s timeout that looks the same as "the server ignored it".

The client is told much more than the chat. It's sent a packet when any caster in view starts a cast and how long it takes, one when a Spell lands on its target and whether it took, one when a caster is interrupted, and one with each Spell or Ability that goes on cooldown and for how long. The Harness passes all four on as bare Percepts that name only the call, so the Mind can't use them.

## Solution

The Harness projects those four packets as Percepts. Self believes whether the Agent is casting, which Spell, and until when, and which of its skills are on cooldown. A porcelain `cast` Tactic casts a Spell at a target and ends with an Outcome that says why it failed, waiting for the cast to start and land from Percepts and waiting out the cast delay after a fight by itself. Only the refusals the server gives before a cast starts, which reach a client only as chat, are still read from the chat, in this one place.

Orla's and Fearnan's casting Tactics are rewritten on `cast`, and the spells Learning teaches `cast` instead of the regex.

## User Stories

1. As an Agent, I want to cast a Spell at a target with one call, so that I don't write a `sendAndWait` and a regex every time.
2. As an Agent, I want `cast` to succeed only once the Spell has landed, so that a success means the Spell did something.
3. As an Agent, I want `cast` to fail with `interrupted` when something hits me mid-cast, so that I know to deal with the Mob first.
4. As an Agent, I want `cast` to fail with `resisted` when the target resists, so that I can cast again instead of thinking it landed.
5. As an Agent, I want `cast` to wait out "You must wait N seconds" and cast again by itself, a few times at most, so that the first cast after a fight doesn't fail.
6. As an Agent, I want `cast` to fail with `mustWait` when the delay keeps coming back, so that I notice something is still swinging at me.
7. As an Agent, I want `cast` to fail with `noPower` before sending anything when I haven't the Power, so that I rest instead.
8. As an Agent, I want `cast` to fail with `tooFar`, `notInView` or `noTarget` from the server's answer, so that I can move closer, find the Mob or target it.
9. As an Agent, I want `cast` to fail with `noSpell` when I don't have the Spell, so that a typo doesn't look like a server refusal.
10. As an Agent, I want `cast` to fail with `cooldown` when the Spell is on cooldown, without sending it, so that I can pick another Spell.
11. As an Agent, I want `cast` to stand me up first if I'm sitting, so that "You can't cast while sitting" never comes.
12. As an Agent, I want `cast` to stop a run in progress first, so that moving doesn't interrupt my own cast.
13. As an Agent, I want `cast` to target what I give it before casting, so that I don't send `target` myself each time.
14. As an Agent, I want to cast a heal or a buff on myself by giving my own object id, so that friendly Spells work the same way as damage Spells.
15. As an Agent, I want an instant Spell to succeed as soon as it lands, so that `cast` doesn't wait for a cast time that isn't there.
16. As an Agent, I want `cast` to end with `timeout` if neither a start nor a refusal comes, so that a Spell the server silently ignores doesn't hang my Tactic (ADR 0008).
17. As an Agent, I want any other refusal the server gives to end the cast as `refused`, with its words in the log, so that I can learn what it means.
18. As an Agent, I want `agent.self.casting` to say which Spell I'm casting and when it should land, so that I don't start a second one on top of it.
19. As an Agent, I want `agent.self.casting` to clear when my cast lands or is interrupted, so that I can trust it between casts.
20. As an Agent, I want `agent.self.cooldowns` to say when each of my Spells and Abilities is ready again, so that I can choose one that's ready.
21. As an Agent, I want Quick Cast's 30 s cooldown to show in `agent.self.cooldowns`, so that I know when I can use it again without trying.
22. As an Agent, I want a cast after Quick Cast to go through `cast` like any other, so that Quick Cast needs no casting code of its own.
23. As an Agent, I want a Percept when a Mob in view starts casting, with how long it takes, so that I can see a caster Mob coming.
24. As an Agent, I want a Percept when a Mob in view is interrupted, so that I can tell my hits stopped its Spell.
25. As an Agent, I want a Percept when a Spell lands on someone in view, saying who cast it, on whom and whether it took, so that I can tell a Spell landed on me.
26. As an Agent, I want the Journal to show cast starts, landings, interrupts and cooldowns, so that the Human can follow a fight.
27. As the Human, I want these Percepts to carry only what the client is sent, so that the Agent knows no more than a client (ADR 0001).
28. As the Human, I want `castSpell` to stay a Primitive that reports dispatch only, so that `cast` is the porcelain over it (ADR 0004, ADR 0008).
29. As a coding agent, I want the spells Learning to teach `cast`, so that I don't copy the old regex.
30. As Orla, I want `pull`, `quickCast` and `buffUp` built on `cast`, so that each handles every answer the same way.
31. As Fearnan, I want `healSelf` and `buffSelf` built on `cast`, so that they lose their own regexes.

## Implementation Decisions

- **Why now.** The Spells, Damage spells and Abilities milestones left Percepts for the cast bar, interrupts and cooldowns out until an Agent needed them (ADR 0009). Two casters now each parse the chat in their own way, and some of those copies go wrong.
- **Projection (Harness).** Four calls the projection passes on as bare Percepts today become typed Percepts, each mirroring what the client is sent:
  - Spell cast animation → `castStarted { casterId, castingMs }`. It's sent for any caster in view, Mobs included. The client effect number isn't passed on: a client shows it only as an animation.
  - Spell effect animation → `spellLanded { casterId, targetId, success }`. `success` is the packet's success byte. Pulsing Spells send it on every pulse.
  - Interrupt animation → `interrupted { objectId }`.
  - Disable skill → `cooldowns { skills: { name, seconds }[] }`. Each disabled Spell or Ability is named as the skills Percept names it, which the client works out from its own skill list. A cooldown of 0 means it's ready again.
- **Protocol.** The four Percepts are added to the Mind protocol and the TypeScript is generated again.
- **Self.**
  - `casting`: undefined, or `{ spell, landsAt }`. It's set by a `castStarted` whose caster is the Agent, naming the Spell from the last `castSpell` sent (the client knows what it clicked, as Self already knows which Dialog it answered). It clears on a `spellLanded` or an `interrupted` from the Agent, or once `landsAt` is well past.
  - `cooldowns`: each skill name with the time it's ready again, on the Agent's clock, from the `cooldowns` Percept. Names past their time aren't listed.
- **The `cast` Tactic.** It lives with the other porcelain. `cast(agent, spell, targetId)` returns an Outcome:
  - Before sending anything, it fails with `noSpell` (the Spell isn't in `skills`), `cooldown` (it's in `cooldowns`) or `noPower` (Power is below its cost). It stands up if sitting and stops a run in progress, then targets `targetId`.
  - It sends `castSpell` and waits for one of these: a `castStarted` from the Agent, an `interrupted` from the Agent, or a refusal line in the chat. After a start, it waits for the Agent's `spellLanded` or `interrupted`.
  - It succeeds on a `spellLanded` with success. It fails with `resisted` when success says it didn't take, `interrupted` on an interrupt, and `timeout` once the casting time plus a margin passes with neither.
  - The refusal lines are read in `cast` alone, since a client gets them only as chat: "must wait N seconds" (wait N seconds and cast again, at most 3 times, then `mustWait`), not enough Power (`noPower`), too far away (`tooFar`), not visible or can't see (`notInView`) and select a target (`noTarget`). Any other refusal ends the cast as `refused`, and its words go to the log.
  - A cast with no start and no refusal within a short timeout fails with `timeout`.
- **Quick Cast.** No Tactic of its own. `useAbility` turns it on, and the next `cast` is the quickcast one.
- **Learning.** The spells Learning's Casting, Healing and Concentration sections use `cast`, and its table of chat lines becomes the list of `cast`'s reasons. The guide adds `cast` to the list of porcelain Tactics.
- **Agents' Tactics.** Orla's `pull`, `quickCast` and `buffUp` and Fearnan's `healSelf` and `buffSelf` call `cast` and lose their casting regexes. `pull` still watches for the Mob's first hit on its own, from Surroundings.

## Testing Decisions

- A good test drives the Agent with Percepts and checks the Primitives sent and the Outcome, never how `cast` tracks its state.
- **Projection:** each of the four calls is projected with the fields above. A disabled Spell and a disabled Ability are named by their skill names. Prior art: the PerceptProjection unit tests.
- **Protocol:** the four Percepts serialise as the generated TypeScript expects. Prior art: the MindProtocol unit tests.
- **Self:** `casting` is set by the Agent's own `castStarted`, ignores another caster's and clears on landing, on interrupt and once `landsAt` is past. `cooldowns` lists a skill until its time is up. Prior art: the Self tests.
- **`cast`:** one test per Outcome, each feeding the Percepts or chat line that leads to it on the fake clock: lands, resisted, interrupted, must wait then lands, must wait three times, each refusal, no answer, and the checks made before sending. Also an instant Spell, standing up first, and stopping a run first. Prior art: the `kill` and `rest` tests with the test Agent and its fake clock.
- **Manual acceptance test:**
  1. Restart the server so it runs this Harness.
  2. Orla hunts for 10 minutes with its rewritten `pull`.
  3. Fearnan heals and buffs itself between fights with its rewritten `healSelf` and `buffSelf`.
  4. Record on this spec each `cast` Outcome seen and anything that timed out.

## Out of Scope

- Naming the Spell another caster casts. A client sees only its animation.
- The spell queue, and casting a second Spell while one is in progress.
- Answering the server's line-of-sight check.
- Kiting porcelain.
- Cooldowns for Styles, which the Combat results spec may take up.

## Further Notes

- The server sends a cast animation with a time of 0 for an instant Spell, then its effect animation.
- A bolt's effect animation is sent when it's launched, with how long it flies. `cast` succeeds then, before the damage arrives.
- How an attack interrupts a cast, from the server's code, is in the spells Learning: a swing that comes before the halfway point interrupts the cast, and every swing holds the next cast off for a few seconds.
