# Combat results

Status: needs-triage

## Problem Statement

Dalta, a Blademaster, fights with Styles through its own `fight` Tactic. Most of `fight` is a state machine fed by about ten chat regexes: "attacks you and you parry the blow" to see a parry, "You attack" / "You miss" / "You fumble" to see its own swing end, "You perform your", "too fatigued", "You prepare to perform", "no longer preparing" and more. A swing or a parry worded in a way the regex doesn't know goes unseen, and a reactive Style such as Return Blade then never fires.

The client is told every swing in view as a combat animation: who swung at whom, whether it missed, hit, was parried, blocked or evaded or fumbled, whether a Style was used, and the defender's health afterwards. It's also told when its own attack mode turns on or off. The Harness passes both on as bare Percepts, so Dalta can't see either one except through the chat. That's how Dalta found out late that a Style queued as the Mob dies turns attack on again with no target, after which the next NPC it targeted, a guard or a merchant, got swung at.

## Solution

The Harness projects the combat animation as an `attack` Percept and the attack mode as an `attackMode` Percept. Self believes whether attack is on. The styles Learning teaches reading parries and swings from `attack` Percepts, and Dalta's `fight` drops the regexes those replace. What a client gets only as chat (which Style is prepared, a Style performed or failed, fatigue) stays chat.

## User Stories

1. As an Agent, I want a Percept for every swing in view, saying who swung at whom and the result, so that I don't read combat from the chat.
2. As a Blademaster, I want to see a Mob's swing at me that I parried as an `attack` Percept with the result `parry`, so that I can answer with Return Blade every time.
3. As an Agent with a shield, I want my blocks as `attack` Percepts with the result `block`, so that I can use Styles that open on a block.
4. As an Agent, I want my evades as `attack` Percepts with the result `evade`, so that I can use Styles that open on an evade.
5. As an Agent, I want each of my own swings as an `attack` Percept, so that I know when a swing ends and the queued Style is used up.
6. As an Agent, I want an `attack` Percept to say whether it was a styled hit, so that I know my Style went off without reading the chat.
7. As an Agent, I want an `attack` Percept to carry the defender's health afterwards, so that I can follow a fight as the client's target window does.
8. As an Agent, I want my misses and fumbles told apart from hits, so that I can count them.
9. As an Agent, I want to see other Characters' swings in view, so that I can tell who a Mob is fighting.
10. As an Agent, I want a Percept when my attack turns on or off, so that I know when the server turned it on by itself.
11. As an Agent, I want `agent.self.attacking` to say whether my attack is on, so that I can turn it off before I target a guard or a merchant.
12. As an Agent, I want `kill` to keep working as it does, so that nothing that relies on it breaks.
13. As Dalta, I want `fight` to see parries and swings from `attack` Percepts, so that it stops missing parries worded differently.
14. As Dalta, I want `fight` to turn attack off only when `agent.self.attacking` says it's on, so that it sends no `attack` it doesn't need.
15. As a coding agent, I want the styles Learning to show reacting to a parry from an `attack` Percept, so that the next fighter starts from it.
16. As the Human, I want the Journal to show each swing's attacker, defender and result in one short line, so that I can read a fight.
17. As the Human, I want the `attack` Percept to carry only what the client is sent, so that the Agent knows no more than a client (ADR 0001).

## Implementation Decisions

- **Projection (Harness).**
  - Combat animation → `attack { attackerId, defenderId, result, styled, defenderHealthPercent }`, for a swing that has both an attacker and a defender. One with no attacker, such as a bleed's, or no defender, such as drawing a bow, isn't projected.
  - `result` is one of `miss`, `parry`, `block`, `evade`, `fumble`, `hit` and `other`, from the packet's result byte as the server fills it: 0 miss, 1 parry, 2 block, 3 evade, 4 fumble, 10 an unstyled hit, 11 a styled hit. Any other byte is `other`. `styled` is true for 11.
  - The server shows the attacker's own client the swing on its selected target, not always the real defender. The Percept carries what the client is sent.
  - Attack mode → `attackMode { on }`.
- **Protocol.** Both Percepts are added and the TypeScript is generated again.
- **Self.** `attacking`: whether attack is on, from the last `attackMode`. Self doesn't guess it from the `attack` Primitives sent, because the server turns attack on and off by itself.
- **Surroundings.** Unchanged. `attack` Percepts aren't stored. A Tactic waits on them.
- **Learning.** The styles Learning's Reactive Styles section reacts to `attack` Percepts with the Agent as defender and the result `parry`, and ends a swing on the Agent's own `attack` Percept. It names the chat lines that are still needed: preparing, performing, failing and fatigue.
- **Dalta's `fight`.** Parries and the end of its own swing come from `attack` Percepts. The final `attack` off is sent only while `agent.self.attacking` is true. The preparing, performing and fatigue regexes stay.

## Testing Decisions

- A good test checks what the Mind receives and believes, not how the projection builds it.
- **Projection:** a swing with each result byte gives its `result` and `styled`. A swing with no attacker or no defender gives nothing. Attack mode gives `attackMode`. Prior art: the PerceptProjection unit tests.
- **Protocol:** both Percepts serialise as the generated TypeScript expects. Prior art: the MindProtocol unit tests.
- **Self:** `attacking` follows `attackMode` both ways. Prior art: the Self tests.
- **Not tested automatically:** Dalta's `fight`, an Agent's own Tactic. It's covered by the manual acceptance test.
- **Manual acceptance test:**
  1. Restart the server so it runs this Harness.
  2. Dalta fights 10 Mobs with `fight` and logs each fight with `describe`.
  3. The parries counted match the parry lines in the Journal, and Return Blade fires after them as often as before or more.
  4. After each fight `agent.self.attacking` is false.
  5. Record the counts on this spec.

## Out of Scope

- Naming the Style used in a styled hit. The packet carries an animation, which the client can't always map back to a Style.
- Which Style is prepared, and fatigue: the client gets these only as chat.
- Style cooldowns.
- Rewriting `kill` on `attack` Percepts, such as counting damage from them.
- Ranged combat.
