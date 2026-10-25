# World objects in Surroundings

Status: ready-for-agent

## Problem Statement

Dalta, a coding agent playing a Blademaster, died and was told "Return to /pray at your grave to regain experience." It ran back to where it died and sent `/pray`, and the server answered "You need to target a grave at which to pray!". Its Grave never showed up in Surroundings, so it had no object ID to `target`, and the experience was lost (papercut "The grave can't be targeted", 2026-10-25).

A Human sees the Grave standing there, clicks it and prays. The server does tell the Agent's Character about the Grave: it sends it the way it sends every World object, through the object create call. But the Harness has no projection for that call, so the Mind gets only a bare Percept that names the call, and Surroundings knows only players and NPCs. The Agent is just as blind to every other World object: doors, loot and money on the ground, vaults, keep banners, relics.

Praying itself already works for an Agent. `target` goes through the real target handler, which takes any object by its ID, and `/pray` goes through the `command` Primitive to the real command handler. What's missing is the World object in Surroundings.

## Solution

The Harness projects the object create call into an `objectCreate` Percept that carries what the 1.127 packet carries. Surroundings gets a third kind of known object, the World object, built from that Percept and forgotten like any other object. The Agent finds its Grave by name ("Dalta's Grave"), runs to it, targets it and prays.

The State summary at the end of every `execute_code` result lists World objects among what's near, as it lists players and NPCs. The Journal writes a line for each `objectCreate`. The fighting Learning gets a paragraph on praying at your Grave, after the one on dying.

Acceptance: Dalta dies, releases, runs back, finds its Grave in Surroundings, targets it, sends `/pray`, stands still, and gains experience back.

## User Stories

1. As an Agent, I want my Grave in Surroundings after I die, so that I can target it and pray.
2. As an Agent, I want to find my Grave by its name, so that I don't mistake another Character's Grave for mine.
3. As an Agent, I want the Grave's position, so that I can run within praying range of it.
4. As an Agent, I want my Grave forgotten once I've prayed at it, so that I don't run back to a Grave that's gone.
5. As an Agent, I want a World object forgotten once it's out of view, as a client forgets it, so that Surroundings doesn't keep stale objects.
6. As an Agent, I want doors, loot on the ground and other World objects in Surroundings too, since a client sees them, so that I can notice them when they matter.
7. As an Agent, I want World objects listed apart from players and NPCs, so that code that looks for Mobs or players never picks up a Grave or a door.
8. As an Agent, I want the State summary to list a World object near me, so that I notice my Grave without asking for it.
9. As an Agent, I want a Learning that shows how to pray at my Grave, so that I don't have to work out the steps from failures.
10. As the Human studying an Agent, I want each `objectCreate` in the Journal, so that I can see which World objects the Agent knew of.
11. As the Human, I want the Percept to carry only what the 1.127 packet carries, so that the Agent knows only what a real client would.
12. As the Human, I want a World object the Agent's Character can't see to give no Percept, as the packet library sends nothing for it.
13. As a coding agent writing Tactics, I want the Percept and the World object's fields in the generated protocol, so that I can read what exists from the source.

## Implementation Decisions

- **Glossary.** **World object** and **Grave** are in `CONTEXT.md`. Code and docs say World object, never item or static item, and Grave, never gravestone.
- **Percept: `objectCreate`.** The projection mirrors the object create call of the 1.127 packet library, which is `PacketLib176`'s override; no later library overrides it. Like the other projections, it names the override it mirrors.
  - It gives nothing for an object the Agent's Character can't see, as the library sends nothing.
  - It carries the object ID, name, position, heading, model, realm, emblem, whether it's underwater, whether it's a keep banner, whether it's loot the Agent's Character owns, and the door ID for a door (none otherwise).
  - The name is the one the library writes: translated for the Agent's language where the library translates it, and cut to 48 characters.
  - The model is the object's model; for a World object the library marks being underwater in the flags, not in the model.
- **Protocol.** The Percept is a C# record in the Mind protocol, and the TypeScript protocol is regenerated from it.
- **Surroundings.** A third kind of known object, `object`, with the object ID, name, position, heading and model, and a speed of 0. Realm, emblem, door ID and the loot flag stay in the Percept only, until an Agent needs them.
  - `worldObjects` lists them, next to `players` and `npcs`. `objects`, `object(id)` and `named(name)` cover them as they cover the other kinds.
  - Object update, delete and remove Percepts and the out-of-view rule apply to World objects as to any known object. An object update moves it but changes nothing else about it.
- **State summary.** A World object is listed among what's near by its object ID and name, as a World object, with its distance.
- **Journal.** An `objectCreate` line with the object ID, name, position and model.
- **Learning.** A paragraph in the fighting Learning, after Dying: after release, the Grave stands where the Character died and is named after it; run within 2000 units, target it, send `/pray` standing still, and wait about 5 s for the message that experience was gained back. A snippet shows it.
- **Papercut.** Dalta's papercut gets a "Fixed:" line saying where the Grave now is, and stays in the file.

## Testing Decisions

- **Good tests** check what goes in and what comes out at an existing seam: a server call in, a Percept out; Percepts in, beliefs out. They don't reach into how either is built.
- **Projection (existing seam).** `UT_PerceptProjection`, calling the projection with the object create call as the Harness's packet library gets it:
  - a Grave gives an `objectCreate` with its object ID, name, position, heading, model and realm;
  - a door also gives its door ID;
  - an object the Agent's Character can't see gives nothing;
  - a name longer than 48 characters is cut.
  - Prior art: the NPC create and player create cases in the same test.
- **Protocol (existing test).** `UT_MindProtocolTypeScript` checks the regenerated TypeScript protocol matches the C# types.
- **Surroundings (existing seam).** `surroundings.test.ts`, feeding Percepts in:
  - an `objectCreate` gives a World object in `worldObjects` and in `objects`, but not in `npcs` or `players`;
  - `named` finds a Grave by its name;
  - an `objectRemove` forgets it;
  - it's forgotten when the Agent's Character moves more than the visibility distance from it;
  - an `objectUpdate` moves it.
  - Prior art: the NPC cases in the same test.
- **State summary and Journal (existing seams).** Wherever their current cases are tested: a World object near the Agent is listed, and an `objectCreate` gets its Journal line.
- **The Learning (existing test).** The Learnings test typechecks the new snippet with every other `ts` fence.
- **Not tested automatically:** that `target` accepts a World object. The real target handler looks up any object in the region by its ID.
- **Manual acceptance test:**
  1. Start Dalta and let it die to a Mob.
  2. It releases and runs back. Its Grave is in `agent.surroundings.worldObjects` and in the State summary.
  3. It targets the Grave and sends `/pray` standing still: the server says it begins to pray, then that it gains back experience.
  4. The Grave is gone from Surroundings.
  5. Record the run on this spec.

## Out of Scope

- A porcelain `pray` Tactic. The coding agent can write its own from the Learning's snippet.
- Realm, emblem, door ID and the loot flag in Surroundings.
- Doors opening and closing, picking up loot, and using vaults, banners or relics.
- Self knowing where its Grave is, or how much experience it holds.
- Praying at another Character's Grave, and resurrection by others.

## Further Notes

- Relevant decisions:
  - ADR 0001: Percepts come from a custom `IPacketLib`. The object create call already reaches the Harness as a typed call; it only lacks a projection.
  - ADR 0002: Primitives go through the real handlers. `target` and `/pray` need no change.
- Removing a Grave by praying sends the object remove call, which the Harness already projects.
- The server keeps one Grave per Character: dying again replaces the old one, and its experience with it. The Learning should say so, since praying before the next death is what saves it.
