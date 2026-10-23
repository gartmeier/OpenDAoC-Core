# Milestone 4: Hand-written grind

Status: ready-for-agent

## Problem Statement

After milestone 3b an Agent can log in, run anywhere in region 200, turn and talk, but it can't fight, rest, train or die and come back. It also can't tell its own health, level or experience. Milestone 5 hands the Agent's decisions to an LLM. Before that, I need to know the Primitives and Percepts are enough to actually play: if the LLM then fails to level, it should be the LLM's fault, not a missing Percept. I also want a baseline to compare the LLM against, and a set of mechanical Tactics, the way rs-sdk's porcelain sits on its plumbing, for the LLM to build on.

## Solution

The Harness gains four Primitives: `target`, `attack`, `sit` and `stand`, plus `command`, which types any slash command the way a Human does. It also turns the server's status, points, player, skills, death and revive updates into typed Percepts. Self grows to hold the Agent's health, endurance, level, experience, specs, spec points, and whether it sits or is dead.

In the Mind, Tactics become async functions over Primitives (ADR 0008). Human-written porcelain Tactics (`kill`, `rest`, `train`) wait on Self and Surroundings with timeouts and end as succeeded, or failed with a reason. A hand-written Grind Tactic runs them with deliberately simple stand-in rules, which the LLM replaces in milestone 5. Come is rewritten as a Tactic too.

The Human stands the Agent somewhere in Hibernia's starting area and says `grind`. The Agent hunts Mobs around that spot, rests, trains, dies, releases, runs back and carries on until level 5. Then it says it's done and the Mind prints a summary. `stop` ends it at any time.

## User Stories

1. As the Human, I want to say `grind` next to the Agent, so that it starts levelling around where it stands.
2. As the Human, I want to say `stop` next to a grinding Agent, so that it stops fighting, running and sitting and just stands.
3. As the Human, I want to say `come` next to the Agent as before, so that milestone 3b's behaviour is unchanged.
4. As the Human, I want a new `grind` or `come` to replace whatever the Agent was doing, so that I never have to say `stop` first.
5. As the Human, I want Ctrl+C to stop the Agent's Tactic and quit as before, so that shutting down works the same while it grinds.
6. As the Human, I want to leave a grinding Agent alone and come back to it at level 5, so that I know the plumbing is enough to play.
7. As the Human, I want the Agent to say it's done when it reaches level 5, so that I see it in game.
8. As the Human, I want the Mind to print a summary at level 5 (time taken, kills, deaths and rests), so that I have a baseline for the LLM in milestone 5.
9. As the Human, I want the Agent to stay online after it finishes, so that I can look at it in game.
10. As the Human, I want the Agent to fight only Mobs from its own level minus 1 to plus 1, so that it neither wastes time on Mobs worth nothing nor dies to ones far above it.
11. As the Human, I want the Agent to leave a Mob that's already fighting someone else, so that it never steals a Human's kill.
12. As the Human, I want the Agent to hunt only within 1500 units of where it heard `grind`, so that it can't drift into higher-level areas.
13. As the Human, I want the Agent to run to a random spot in its hunting area when no Mob is in view, so that it finds more Mobs instead of standing still.
14. As the Human, I want the Agent to fight back first when a Mob attacks it, whatever it was doing, so that aggressive Mobs don't kill it while it rests or runs.
15. As the Human, I want the Agent to sit when its health is below 50% and nothing is attacking it, and to stand at 90%, so that it doesn't start fights it can't win.
16. As the Human, I want the Agent to put all its spec points into Blades with `/train` after each level, so that it gets stronger as it levels.
17. As the Human, I want the Agent to release when it dies, run home from its bind point and carry on, so that dying doesn't end the grind.
18. As the Human, I want the grind to fail with a reason when home can't be reached from the bind point, so that the Agent never runs back and forth forever.
19. As the Human, I want every Percept, Primitive and Tactic outcome printed, so that I can see why the Agent did what it did.
20. As the Human, I want to reset the Agent with `/player level 1` and run the acceptance test again, so that I don't need a new Agent each time.
21. As the Human, I want `target`, `attack`, `sit` and `stand` sent as the same packets a real 1.127 client sends, through the real packet handlers (ADR 0002), so that Agents fight under exactly the rules Humans do.
22. As the Human, I want `command` to go through the same command handler as a Human's slash commands, so that `/train` and `/release` behave as they do for a Human.
23. As the Human, I want the Agent's position updates to carry its target and its attack state as a real client's do, so that nothing about the server's view of an Agent differs from a Human's.
24. As the Human, I want Primitive results to keep reporting dispatch only (ADR 0004), so that the Agent learns whether a fight is going well only from what a client is told.
25. As a Mind developer, I want a `target` Primitive taking an object ID, so that I can select a Mob as a click would.
26. As a Mind developer, I want an `attack` Primitive that turns auto-attack on or off, so that I can start and end a fight.
27. As a Mind developer, I want `sit` and `stand` Primitives, so that I can rest.
28. As a Mind developer, I want a `command` Primitive that takes a slash command as a Human types it, so that one Primitive covers `/train`, `/release` and the many other things a Human does by typing.
29. As a Mind developer, I want typed Percepts for my health, endurance and concentration, and whether I'm sitting or dead, so that I can tell when to rest and when I've died.
30. As a Mind developer, I want typed Percepts for my experience towards the next level and my spec points, so that I can tell progress and what I can train.
31. As a Mind developer, I want a typed Percept for my level and class, so that I can choose Mobs by level and know when I've reached a goal.
32. As a Mind developer, I want a typed Percept for my specs and their levels, so that I know what I've trained.
33. As a Mind developer, I want typed Percepts for death and revive, with who died, who killed them and who came back, so that I can react to my own death and see others'.
34. As a Mind developer, I want Self to hold my health, endurance, level, experience, specs, spec points and whether I'm sitting or dead, all from Percepts, so that Tactics read beliefs instead of parsing Percepts.
35. As a Mind developer, I want Surroundings to hold each NPC's level, realm, health, whether it's peaceful, a pet or untargetable, and whom it's attacking, so that a Tactic can tell a Mob from a merchant and see who it fights.
36. As a Mind developer, I want every Tactic to be an async function that gets the Agent (Primitives, Self, Surroundings and `waitFor`), so that I write "do this, then wait for that" as straight-line code (ADR 0008).
37. As a Mind developer, I want `waitFor` to take a condition on Self and Surroundings and a timeout, so that a Primitive the server silently ignores ends my Tactic instead of hanging it.
38. As a Mind developer, I want every Tactic to end as succeeded, or failed with a reason, so that the Tactic that ran it can decide what to do next.
39. As a Mind developer, I want Tactics to run other Tactics and stopping a Tactic to stop those too, so that `stop` and Ctrl+C reach whatever is running.
40. As a Mind developer, I want a `kill` Tactic that runs to a Mob, faces it, targets it and attacks until it dies, so that fighting is one call.
41. As a Mind developer, I want `kill` to follow a Mob that moves out of melee range by sending a new `moveTo`, so that a fleeing or wandering Mob doesn't end the fight.
42. As a Mind developer, I want `kill` to fail with a reason when I die, the Mob leaves view, the route is `unreachable`, or nobody takes damage for 30 s, so that I can tell why a fight ended.
43. As a Mind developer, I want a `rest` Tactic that sits until health reaches a given percentage and fails when something attacks me, so that resting is one call.
44. As a Mind developer, I want a `train` Tactic that types `/train <line> <level>` and succeeds once the spec's new level arrives as a Percept, so that training is one call and its failure is visible.
45. As a Mind developer, I want Come rewritten as a Tactic, so that there's one way to write Tactics.
46. As a Mind developer, I want the TypeScript types for the new Primitives and Percepts generated from the C# definitions, so that both sides always agree.
47. As a Mind developer, I want to test any Tactic against a fake Agent on a fake clock, so that I can check `kill` timeouts and the Grind's choices without a server or real waiting.

## Implementation Decisions

- **Primitives (ADR 0002).** All in the Harness area, each built as the 1.127 client sends it and handled by the real packet handler on the client tick.
  - `target { objectId }` sends the target packet. A real client sets the line-of-sight bits from its own geometry; the Harness sets them as the capture shows for a Mob in plain view. The server still does its own checks.
  - `attack { on }` sends the attack request as a user action.
  - `sit` and `stand` send the sit request.
  - `command { text }` takes the text as a Human types it, starting with `/`, and sends it through the command packet the way `quit` and `say` already do (`/` becomes `&` on the wire).
  - `quit`, `say`, `moveTo`, `stop` and `face` are unchanged.
  - Once something is targeted, the Harness's position and heading updates carry the target's object ID, and its action flags carry attack mode, as the capture shows. Running and sitting don't mix: a `moveTo` while sitting works as the server rules decide, as for a Human.
- **Capture first.** The Human records a capture with the real 1.127 client: targeting a Mob, attack on and off (with the keyboard and by the client stopping it), sitting and standing, and a few seconds of fighting. It sits next to the existing 1.127 fixtures with a section in their README. The builders are written against it.
- **Percepts (ADR 0001).** New typed projections of calls the server already makes on the packet library, reading what each call's 1.127 packet would carry:
  - `status`: health, endurance and concentration percentages, sitting, dead.
  - `points`: experience towards the next level in thousandths, spec points, realm points and realm spec points.
  - `player`: level, name, class name, base class name and maximum health.
  - `skills`: the specs and their levels. Abilities and styles only as far as the 1.127 packet lists them, by name.
  - `died`: the object that died and the object that killed it (0 when none).
  - `revived`: the object that came back.
  - Combat, experience and level-up text stay `message` Percepts.
- **Codegen.** The new Primitives and Percepts are C# types in the Harness area, and the generated TypeScript is regenerated, as in earlier milestones.
- **Self and Surroundings.**
  - Self adds health, endurance and concentration, sitting, dead, level, class, experience, spec points, and specs with their levels. Its object ID is already known from the server's position at world entry.
  - Surroundings keeps, per NPC, its level, realm, peaceful, pet, untargetable, its last health percentage and whom it attacks (from object updates), and drops an NPC removed as dead.
- **Tactics (ADR 0008).**
  - A Tactic is an async function given the Agent and its own arguments, ending as succeeded or failed with a reason. The Agent gives:
    - `send(primitive)`, which returns the dispatch result;
    - Self and Surroundings;
    - `waitFor(condition, timeout)`, which resolves when the condition on Self and Surroundings holds after a Percept, and rejects on timeout or when the Tactic is stopped;
    - a way to run another Tactic, which stops with its parent.
  - Stopping a Tactic ends it as failed with the reason `stopped`. It also ends every Tactic it is running, and leaves the Agent standing with attack off and no run in progress.
  - Time comes from the Agent, so tests can run on a fake clock.
- **Porcelain Tactics.**
  - `kill(mob)`:
    - It runs to the Mob with `moveTo` and a `within` inside melee range, faces it on `arrived`, targets it and turns attack on.
    - When the Mob is out of melee range, it sends a new `moveTo` to the Mob's latest known position, no more often than position news arrives.
    - It succeeds when the Mob's health reaches 0 or the Mob is removed as dead. It fails with `died`, `outOfView`, `unreachable` or `noDamage` (no health change on either side for 30 s), turning attack off in every case.
  - `rest(until)`: sits, then succeeds when health reaches `until` percent and stands. It fails with `attacked` as soon as an NPC attacks the Agent, or the Agent's health drops.
  - `train(line, level)`: types `/train <line> <level>` and succeeds once `skills` shows the spec at that level. It fails with `notTrained` after a timeout.
  - `come(speaker)`: as milestone 3b's Come. It sends a `moveTo` to the speaker with `within: 100` and faces them on `arrived`.
- **Grind Tactic.** The stand-in rules, meant to be dumb:
  - Home is where the Agent stands when the grind starts.
  - Each step, in order:
    - If dead, type `/release`, wait to be alive, then `moveTo` home. If that ends `unreachable`, the grind fails with `homeUnreachable`.
    - If an NPC is attacking the Agent, `kill` it.
    - If there are spec points, `train` Blades to the highest level they pay for (a spec's next level costs that level in points).
    - If health is below 50%, `rest` until 90%.
    - Otherwise `kill` the nearest Mob within 1500 units of home that is hostile (realm none, not peaceful, not a pet, not untargetable), from the Agent's level minus 1 to plus 1, alive and attacking nobody but the Agent.
    - With no such Mob, `moveTo` a random walkable spot within 1500 units of home, giving up the run as soon as one appears.
  - A failed `kill` or `rest` isn't the end: the loop just takes its next step. A Mob that fails with `unreachable` or `noDamage` is skipped for the rest of the grind.
  - It succeeds at level 5, then says `I'm level 5 now.` and the Mind prints the summary: time taken, kills, deaths, rests, and Tactic failures by reason.
- **Mind entry point.**
  - It listens to say lines as Come does. `come` and `grind` start that Tactic in place of whatever is running, and `stop` stops it.
  - It prints every Percept, every Primitive sent and every Tactic outcome.
  - Ctrl+C stops the running Tactic, then quits as before.
- **Code placement.** All new server code lives in the Harness's folder and namespace, with no core code changes. Tactics live in the Mind project, one module each, beside Self and Surroundings.

## Testing Decisions

- **Good tests** check behaviour seen from outside: Percepts and time in, Primitives and outcomes out. They never check how a Tactic or projection is built inside.
- **Packet builders (existing seam):** golden-byte tests for `target`, `attack`, `sit` and `stand` against the new capture, and for position and heading updates with a target and attack mode. Prior art: the movement builder tests and the capture fixture checksums.
- **Percept projection (existing seam):** each new Percept projected from server state matches what its 1.127 packet would carry. Prior art: the `maxSpeed` projection tests.
- **Protocol (existing seam):** the new Primitives and Percepts round-trip as JSON, and the codegen test fails until the TypeScript is regenerated.
- **Tactics (new seam, `bun test`):** one fake Agent that records the Primitives sent, takes Percepts from the test and runs on a fake clock. Every Tactic is tested only through it:
  - `kill`:
    - Runs, faces, targets and attacks.
    - Re-sends `moveTo` when the Mob moves away.
    - Succeeds on health 0 and on removal as dead.
    - Fails with each reason, turning attack off.
  - `rest`: sits and stands at the threshold, and fails when attacked.
  - `train`: types the command, succeeds when the spec's level arrives, and fails on timeout.
  - `come`: as the current Come tests.
  - Grind:
    - Prefers a Mob attacking the Agent.
    - Chooses only hostile Mobs in the level range and inside the leash, and skips ones fighting someone else.
    - Rests at 50% and trains when points arrive.
    - On death, releases and runs home, and fails when home is unreachable.
    - Wanders when nothing is in view.
    - Succeeds at level 5 with the summary.
  - Stopping: stopping any Tactic stops the ones it runs and leaves attack off, standing and no run.
- **Self and Surroundings (existing seams):** the new Percepts update them as described.
- **Not tested automatically:** the live server and the real client. They're covered by the manual acceptance test.
- **Manual acceptance test:**
  1. `/player level 1` on the Agent's Character, then start its Mind.
  2. Stand it in Hibernia's starting area with low-level Mobs around and say `grind`.
  3. Leave it alone until it says it's level 5. Deaths are fine, and nobody helps it.
  4. Check the Mind's summary, and that the server log has no speed-check entries.
  5. Say `grind` again and then `stop` mid-fight: it stands with attack off.
  6. Say `come`: it comes as in milestone 3b.

## Out of Scope

- Choosing a class at level 5, and any NPC interaction (trainers, merchants, quests).
- Styles, spells and abilities. A Guardian has no styles before level 5 in this database.
- Loot, inventory, equipment and money beyond what the server does by itself.
- LLM Planning, LLM-written Tactics, the Tactic Library and the Journal.
- Smarter rules for the Grind: anything beyond the stand-ins above is milestone 5's LLM's job.
- Grouping, other Agents and other realms or regions.
- Praying at the grave, healers and resurrection by others.
- Line of sight worked out by the Harness from zone geometry.

## Further Notes

- Vocabulary follows the root glossary, which now has **Mob**, and a **Tactic** that runs other Tactics and ends having succeeded or failed with a reason.
- Relevant decisions:
  - ADR 0001: Percepts only as a client would know them. The new Percepts are projections of typed calls.
  - ADR 0002: the new Primitives go through the real handlers.
  - ADR 0004: results report dispatch only, so every Tactic judges its effect from Percepts and timeouts.
  - ADR 0006 and 0007: `moveTo` routes, which `kill`, wandering and running home rely on.
  - ADR 0008 (new): Tactics are async functions over Primitives.
- Facts this spec relies on, from the server and its database as they stand:
  - `allow_train_anywhere` and `custom_train` are on, so `/train <line> <level>` works without a trainer.
  - A Guardian's spec lines are Blades, Blunt, Piercing and Parry. Styles are defined only for the classes a Guardian can become, and the fallback to shared styles finds none.
  - `/player level 1` resets a Character when it lowers the level.
  - The server's messages include "You get N experience points.", "You raise to level N!", "You get N more Specialization Points to spend at this level!" and "You release your corpse unto death."
  - The 1.127 position update carries the target's object ID at data offsets 22–23, and the heading update at 2–3.
  - The target packet carries two line-of-sight bits. Without them the server says "Target is not in view." and marks the target not in view.
  - An NPC removed from the world after dying arrives as `objectRemove` with object type 0.
- Greeen is a level 1 Celt Guardian with a training sword and shield, in Shannon Estuary (region 200).
- Risk: the Harness sets the target's line-of-sight bits without working out line of sight. In melee range that almost never differs from the truth, but it's a small advantage a real client doesn't have. If it matters, work it out from the Navmesh (ADR 0006) later.
- Risk: if the Agent's bind point is in another region, releasing changes region, which the Harness hasn't handled before. Bind the test Character in region 200.
- Risk: melee needs the Mob in front. A Mob that circles may need `kill` to face it again during the fight. Watch for it in the acceptance test.
- Milestone ladder: 1 login and logout (done) → 2 Mind connection (done) → 3 movement (done) → 3b terrain and pathing (done) → 4 hand-written grind (this spec) → 5 LLM Planning → 6 Reflection and Tactic Library → 7 Agent character creation → 8 multiple Agents and grouping.

## Comments

**2026-10-22 (agent):** Implemented, not yet tested in game. Where it differs from the spec above:

- **Attack and sit aren't captured yet.** The Human still has to record a real 1.127 capture of attack on and off, sitting and standing, and a short fight. Until then the attack and sit builders follow their handlers' read logic and are checked by round-trip tests. The target packet already matches `run-turn-stop.tsv`. Then add golden-byte tests and correct the builders if the bytes differ.
- **Attack mode isn't in the action flags.** The server has no attack-mode action flag. Position and heading updates carry the target's object ID and `TARGET_IN_VIEW` while something is targeted. The server reads `TARGET_IN_VIEW` from every update, so leaving it out would put the target out of view.
- **`status` has no dead flag.** The 1.127 packet carries none. Self counts the Agent as dead from its own `died`, or a status with no health, until its own `revived`.
- **`noDamage` counts only health falling on either side.** The server sends misses and refused attacks as `CT_YouHit` too, so combat text would keep a stalled fight going.
- **Tactic failure reasons are camel case.** `runTo` passes on a run's stop reason that way (`unreachable`, `movedByServer`).
- **Left as they are:**
  - A Mob chasing the Agent shows target 65535 until it's within reach, so it only counts as attacking once it gets there.
  - Wandering picks a spot at home's Z and lets the Harness move it to the nearest walkable point (ADR 0007).

**2026-10-23 (agent):** The Human recorded the fight capture with account `joshua`. It's checked in as `Tests/Fixtures/Agents/v1127/target-attack-sit.tsv`. What it changed:

- The attack request has 4 bytes of data. When the Mob it was attacking dies, the client stops attacking and clears its target by itself (flags `0001`), and the Harness now does the same.
- `/sit` and `/stand` go out as commands, so `sit` and `stand` send those. Nothing uses the sit request any more.
- While attacking, the client sets the top bit of the health byte in its position and heading updates, and the Harness now does the same. That's the attack mode the spec meant. It isn't an action flag.
- Golden-byte tests cover all of these.

**2026-10-23 (Human, recorded by agent):** In the first acceptance run, Greeen fought a blue Mob down to about 50% health, then took on a yellow one and died. The stand-in rest threshold is now below 80% (it was 50%), still resting until 90%. It may change again.

**2026-10-23 (Human and agent):** A second capture, of the sit key, is checked in as `sit-key.tsv`. The key sends the sit request as a toggle: the client picks sitting or standing from what it believes its state is. `sit` and `stand` stay on `/sit` and `/stand`, which say what they mean.

**2026-10-23 (Human, recorded by agent):** In the second run, level 1 Greeen killed a level 0 sand crab and came out at 84% health. That's above 80%, so he didn't rest, and a level 1 sand crab killed him in about 12 s. The stand-in rules now take only Mobs one level below the Agent's own (blue), because "yellows are too strong", and he rests until 100%.
