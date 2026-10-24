# Milestone 6: Styles

Status: ready-for-agent

## Problem Statement

In milestone 5 Dalta, a coding agent, took a fresh Guardian to a level 5 Blademaster. A Blademaster's weapon specs give it Styles: Shining Blade at Blades 2, Return Blade after a parry at 4, Taunting Blade at 6, and so on. Styles are a big part of how a fighter in this game wins fights from level 5 on.

Dalta can see its Styles in `agent.self.skills`, but it can't use one. A real client uses a Style through the skill packet, which names it by its place in the skill list the server last sent. No Primitive sends that packet, and `command` can't reach it, because no slash command uses a Style. Dalta also can't find out what a Style needs: an earlier parry, an attack from the side, another Style before it. A client shows that when the Human examines the Style.

## Solution

Two new Primitives, both naming a Style the way the coding agent sees it in `skills`:

- `useStyle { name, line? }` uses the Style on the next swing, as a click on its icon does.
- `examineStyle { name, line? }` asks for its description. The answer arrives as the `examined` Percept that examining an item already produces.

The Harness turns the name into the index a client would send, using the skill list it was last sent. `line` is the Style's spec, such as Blades or Blunt. It's only needed when two Styles share a name, which for a Blademaster happens once: Slam, in both Blunt and Shields. Without `line`, the first match in the list is used. Each Style's entry in `skills` now carries its line, as the real packet does, so the coding agent can see it.

Nothing else changes. Whether a Style was used, failed its requirement, or is still on cooldown, the server says in messages, which already arrive as `message` Percepts. A new Learning, "Styles", shows how to work with them.

Acceptance: Dalta carries on, as the same Agent with its Lessons and Tactics, in a new Claude Code session, and goes from level 5 to 10. It lands a Style in most fights, at least one of them a Style with an opening requirement.

## User Stories

1. As the Human, I want to record a capture with the real 1.127 client of Dalta using a Style from the quickbar in a fight and examining a Style in the skills window, so that the new packets are built byte for byte as the client sends them.
2. As the Human, I want Dalta to carry on as the same Agent, with its Character, folder, Lessons and Tactics, so that I see how it adapts what it already wrote.
3. As the Human, I want to start a new Claude Code session in Dalta's folder and tell it to play on, so that a run starts as milestone 5's did.
4. As the Human, I want to nudge Dalta only when it stops, so that I learn when and why it stops.
5. As the Human, I want the run to record how Dalta found out what each Style needs, so that I know whether examining is enough.
6. As the Human, I want the run to record its papercuts, so that the next Primitive or Percept comes from a need.
7. As the Human, I want no speed-check entries for Dalta in the server log, so that using Styles doesn't break the position the server trusts.
8. As the coding agent, I want to use a Style by the name I see in `agent.self.skills`, so that I don't work out indexes.
9. As the coding agent, I want `useStyle` to queue the Style for my next swing, as a click on its icon does, so that it works with the `kill` I already use.
10. As the coding agent, I want to name a Style's line when two of my Styles share a name, so that I can use either one.
11. As the coding agent, I want the first matching Style used when I give no line, so that the common case needs only a name.
12. As the coding agent, I want a Style I don't have, or a line it isn't in, refused at dispatch with a reason, so that a typo doesn't fail silently.
13. As the coding agent, I want each Style's line in `agent.self.skills`, so that I can tell two Styles of the same name apart.
14. As the coding agent, I want to examine a Style by name and read its description in `agent.inventory.examined`, so that I learn its opening requirement, position, follow-ups and cost as a Human would.
15. As the coding agent, I want the server's messages about a Style (used, missed its requirement, not enough endurance, the backup Style used) in the chat after each call, so that I can judge whether it worked.
16. As the coding agent, I want new Styles to appear in `agent.self.skills` as my specs rise, so that I know when I've gained one.
17. As the coding agent, I want a Learning with working snippets that use a Style inside a `kill`, react to a parry with a reactive Style, and examine a Style, so that I don't have to rediscover the pattern.
18. As the coding agent, I want the Learning to say that a Style queues for the next swing, that reactive Styles only work just after what they react to, and that Slam needs its line, so that I avoid the gotchas.
19. As a Mind developer, I want the TypeScript types for the new Primitives and the `skills` entry's line generated from C#, so that both sides agree.
20. As a Mind developer, I want the Learning's snippets typechecked by the existing Learnings test, so that it never teaches code that doesn't compile.
21. As a Harness developer, I want the name looked up in the skill list the server last sent the client, so that the index matches what the server reads it against.
22. As a Harness developer, I want the skill and detail packets to carry the client's position, heading and target-in-view bits as the captured client writes them, so that using a Style doesn't move the Character or put its target out of view.
23. As a Harness developer, I want both Primitives handled by the real packet handlers on the client tick, so that the server decides everything as it does for a Human (ADR 0002).

## Implementation Decisions

- **Vocabulary.** The root glossary now has **Style** and **Spell**, and keeps "skill" out as an umbrella term. "The skill list" means only the server's list of specs, abilities and Styles. No ADR changes.
- **Capture first.** The Human records a capture with the real 1.127 client, with markers said before each part:
  - Dalta in a fight, using Shining Blade from the quickbar, at least twice;
  - examining Shining Blade, and one other Style, in the skills window.

  It's checked in with the other 1.127 fixtures, with a section in their README, and the builders are written against it. It settles what the reading of the handlers leaves open:
  - which of the two numberings the client's skill packet uses: the whole list, or from the first entry after the specs;
  - how the detail request numbers a Style;
  - what the client writes in the fields the server doesn't read.
- **Primitives (ADR 0002).** In the Harness area, built as the captured client sends them, handled by the real handlers on the client tick:
  - `useStyle { name, line? }` sends the skill packet for the Style.
  - `examineStyle { name, line? }` sends the detail request for a Style, as `examineMerchantItem` does for a merchant's item. `examine { slot }` stays for inventory items only.
  - The skill packet starts with the client's position, speed and heading, then flags with the strafing and target-in-view bits, which the server takes from it as it does from a position update. The Harness writes these as its own position updates would at that moment.
- **Looking a Style up.**
  - The Harness looks the name up among the Styles in the skill list the server last sent the Agent's client. That's the list the handlers read the index against, as a client's icons are.
  - Only Styles match, never specs or abilities of the same name.
  - Without `line`, the first matching Style is used. With `line`, the first matching Style in that spec is used.
  - No match is refused at dispatch: `Dalta has no Style "X".`, or `Dalta has no Style "X" in Y.` Whether the server then accepts the Style isn't known at dispatch (ADR 0004).
- **`skills` Percept.**
  - Each entry gains `line`. For a Style it's the name of its spec, which the 1.127 packet carries as an index the client knows the name of. For anything else it's absent.
  - Self keeps the entries as they come, so the line reaches `agent.self.skills` with nothing else to change.
- **Feedback.** No new Percepts. The server's messages about Styles (prepared, performed, requirement not met, too tired, backup Style) already come as `message` Percepts and appear in `execute_code`'s chat.
- **Examined text.** A Style's description lands in `agent.inventory.examined`, where an item's already does. It's the last detail window the client opened, whatever it described.
- **Codegen.** As before: C# types in the Harness area, regenerated TypeScript.
- **Learning.** A new `styles` Learning beside fighting, trading and moving, written as `execute_code` snippets:
  - examining a Style and reading its requirement;
  - using an anytime Style each swing inside a `kill`;
  - using a reactive Style just after the message that says the Mob was parried, blocked or evaded, or that the Character parried, blocked or evaded;
  - the gotchas: a Style queues for the next swing, a reactive Style must be used before the next swing after what it reacts to, and Slam needs its `line`.
- **Not changed.** Porcelain Tactics, Self apart from the line, Surroundings, Inventory, the Wiki, the guide and the `execute_code` summary.
- **Code placement.** As before: the Harness folder and namespace, no core code changes. Only the Learning and the regenerated types change on the Mind side.

## Testing Decisions

- **Good tests** check behaviour seen from outside: a Primitive and a skill list in, a queued packet or a refusal out; server state in, a Percept out; packets matching what the real client sent. They don't check how the lookup is built.
- **Harness dispatch (existing seam, the main one).** On a Character double whose skill list has specs, abilities and Styles, two of them named Slam in Blunt and Shields:
  - `useStyle` and `examineStyle` by name queue the packet with the index the handlers read back as that Style;
  - without `line`, the first Slam is used, and with `line` the other one;
  - a name that isn't a Style, a line it isn't in, and an ability's name are each refused with a reason;
  - the index follows the list as the server last sent it, after a new Style is learned.
  - Prior art: the `talkTo`, `buy` and `pickUp` dispatch tests in the Harness client tests.
- **Packet builders (existing seam).** Golden bytes for the skill packet and the Style detail request against the new capture, masking only fields the README documents as varying. Prior art: the item and merchant builder tests.
- **Percept projection (existing seam).** A Style's `skills` entry carries its spec as `line`, and specs and abilities carry none. Prior art: the existing `skills` projection test.
- **Protocol (existing seam).** The new Primitives and the entry's `line` round-trip as JSON, and the codegen test fails until the TypeScript is regenerated.
- **Learnings (existing seam).** The new Learning's snippets typecheck as `execute_code` snippets.
- **Not tested automatically:** the live server and the coding agent, covered by the manual acceptance test.
- **Manual acceptance test:**
  1. Record the capture with the real client, as above.
  2. Start a new Claude Code session in `agents/dalta` and tell it to play on to level 10, using its Styles.
  3. Leave it, nudging it only when it stops.
  4. Dalta reaches level 10 and lands a Style in most fights, at least one of them with an opening requirement, such as Return Blade after a parry or Glowing Blade from the side at Blades 10.
  5. Record on this spec how far it got, how long it took, how it found out what each Style needs, why it stopped each time, and its papercuts.
  6. Check the server log has no speed-check entries for Dalta.

## Out of Scope

- Spells, the spell lines a pure caster is sent, casting, interrupts and a caster Character. They become their own small milestone when a Magician plays, reusing the name lookup.
- Abilities used from the skill list, such as sprint, and the backup-Style setting.
- New Percepts for Styles: cooldowns, the prepared Style, and effects or icons on the Character. They come when a papercut asks.
- New porcelain, such as a `kill` that takes a Style.
- Wiki pages for Styles. Examining a Style tells the coding agent what a fan site would.
- A fresh Agent: Dalta carries on.

## Further Notes

- Relevant decisions:
  - ADR 0001: Percepts only as a client knows them. The line is what the 1.127 skills packet carries.
  - ADR 0002: Primitives through the real handlers.
  - ADR 0004: dispatch-only results, so whether a Style landed comes from messages.
  - ADR 0009: Primitives and Percepts are added when the coding agent needs them. That's why this milestone is Styles only, with no new Percepts.
- Facts this spec relies on, from the server and its database as they stand:
  - **Dalta** is a level 5 Blademaster in Ardagh (region 200), with Blades 5 and every other spec at 1.
  - **Blades Styles for a Blademaster:** Shining Blade at 2 (anytime), Return Blade at 4 (after Dalta parries), Taunting Blade at 6, Enervating Blade at 8, Glowing Blade at 10 (from the side), Lunging Blade at 12 (after Return Blade), Auroric Blade at 15 (after Glowing Blade).
  - **The one name clash:** Slam, in Blunt (at 8) and in Shields, both open to a Blademaster.
  - **The skill handler** reads an index into the list the Character's skills last gave the client. With a type byte above 0, the index counts from the first entry after the specs. With 0, it counts from the start.
  - **The detail handler** numbers a Style by 100 plus its place after the specs.
  - **The 1.124+ skill packet** starts with the client's position as floats, its speed and heading, then flags whose target-in-view bits the server takes as the truth.
  - **The real skills packet** writes, for each Style, its opening requirement and an index for its spec.
- The takeover papercut from milestone 5 (a Mind that takes over started with empty beliefs) was already fixed: the server now tells a taking-over Mind what it tells one entering the world.
- **Milestone ladder:** 1 login and logout → 2 Mind connection → 3 movement → 3b terrain and pathing → 4 hand-written grind → 4b inventory and gear → 5 a coding agent plays (all done) → **6 Styles (this spec)** → Spells when a Magician plays, and other Primitives, Percepts and Learnings as the coding agent turns out to need them.

## Comments

**2026-10-24 (agent):** Implemented, not yet tested in game. Where it differs from or adds to the spec above:

- **Captured on 2026-10-24** in `styles.tsv`: Dalta, possessed by the Human, using Shining Blade (index 9) and Return Blade (10) from the quickbar. The skill packet matches it byte for byte, after one correction: it's 24 bytes, ending in 2 bytes of 0. Its type is 1 and its parameter 0x0000, as built.
- **Examining a Style isn't captured yet.** `examineStyle` follows what `DetailDisplayHandler` reads, and its test reads it back as the handler does. A later capture should settle:
  - whether the client examines a Style as kind 6 (100 plus its index, answered with a text window, which is what `examineStyle` sends) or kind 25 (its internal ID, answered with delve info, which isn't projected as a Percept yet);
  - what the client writes in the detail request's extra ID for a Style (the item one here).
- **The field is `style`, not `name`:** `useStyle { style, line? }` and `examineStyle { style, line? }`. `name` is already the Primitive's discriminator.
- **The list is the one the Harness was last sent:** it keeps the entries of the last `skills` Percept and looks the Style up there.
- **The skill packet's position is where the server has the Character,** with its speed and heading, so the server sees no move. Its flags carry the target-in-view bits exactly while something is targeted, as the position updates do.
- **Two refusals beyond the spec:** before the server has sent a skill list, every Style is unknown; and a Style past the 255th entry is refused, because the server's packet stops there and the client never got its icon.
- **A Style of a spec the packet has no index for has no `line`,** since the client couldn't name it. No Hibernian spec is like that.
- **The Learning hasn't been run in a fight.** Its snippets typecheck and its message texts are the server's, but no Agent has used a Style yet. Dalta's run is the test.
