# Bindstones in the Wiki

Status: ready-for-agent

## Problem Statement

In milestone 5's acceptance run, Dalta hunted west of Ardagh for levels 4 and 5, and each death there sent it back to its Bind point in Howth, about 40,000 units away. It knew to bind: it ran to the merchant Moesen in Ardagh, sent `/bind`, and the server replied "You can't bind here!". It was about 1,300 units from Ardagh's Bindstone, and a Character must stand at the stone to bind.

A Human sees a Bindstone in the client's static scenery and walks up to it. An Agent has no such view. The server never sends a Bindstone as an object, the Wiki doesn't list them, and no Learning says how binding works. So an Agent can't find one except by luck.

Binding itself already works for an Agent. `/bind` goes through the `command` Primitive to the real command handler, and the reply comes back as a `message` Percept. What's missing is knowing where the Bindstones are.

## Solution

The Wiki lists each zone's Bindstones. A zone page gets a **Bindstones** section with the position of each Bindstone the realm's Characters can use. The fighting Learning gets a paragraph on binding: find the Bindstone of the town you hunt from on its zone page, run right up to it, `/bind`, and read the reply.

Nothing else changes: no new Primitive or Percept, and Self and Surroundings stay as they are.

Acceptance: an Agent in Ardagh finds Ardagh's Bindstone on the Silvermine Mts. page, binds there, dies nearby, releases, and comes back to life in Ardagh.

## User Stories

1. As the coding agent, I want each zone page in the Wiki to list the zone's Bindstones, so that I can find where to bind without walking around looking for one.
2. As the coding agent, I want each Bindstone's position in global coordinates, as `/gloc` gives them and as the page's Mobs and NPCs are listed, so that I can `runTo` it directly.
3. As the coding agent, I want only Bindstones my realm can use listed, so that I never run to one that refuses me.
4. As the coding agent, I want one entry per stone, even where the server has two bind points for it, so that I don't wonder which of two nearly identical positions to use.
5. As the coding agent, I want a zone page to say when the zone has no Bindstone, so that I know to look in a neighbouring zone rather than wonder whether the Wiki left it out.
6. As the coding agent, I want the fighting Learning to tell me to bind at the Bindstone of the town I hunt from, so that a death doesn't send me back to where I started.
7. As the coding agent, I want the Learning to tell me to run right up to the stone before `/bind`, so that I don't try from across town as Dalta did.
8. As the coding agent, I want the Learning to tell me which replies mean bound, not bound, and wait, so that I can check my snippet worked.
9. As the coding agent, I want the Learning to tell me that binding again has a short wait, so that a second `/bind` straight after the first doesn't confuse me.
10. As the coding agent, I want the Learning to tell me that my Bind point isn't tracked for me, so that I note it in my own files if I care where I'll wake up.
11. As the coding agent, I want the Learning's binding snippet to be one I can paste into `execute_code`, so that I don't have to work out the reply-checking myself.
12. As the Human, I want Hibernia's Wiki regenerated with the Bindstones and checked in, so that the next Agent reads them.
13. As the Human, I want Bindstones to come from the bind points the server has loaded, so that the Wiki lists the stones where `/bind` actually works.
14. As the Human, I want the Agent to learn nothing about the server's bind radius, so that it binds the way a Human does: by standing at the stone.
15. As the Human, I want binding to go on using the real `/bind` command, so that Agents bind under exactly the rules Humans do (ADR 0002).
16. As a Harness developer, I want the Bindstones section built by the same function from plain records that builds the rest of the Wiki, so that it's tested like the rest of the Wiki.
17. As a Harness developer, I want the Wiki's existing pages unchanged apart from the new section, so that the change is easy to review in the regenerated Wiki's diff.

## Implementation Decisions

- **Vocabulary.** The root glossary now has **Bindstone** (a fixed place where a Character can bind) and **Bind point** (where a Character comes back to life when it releases). The server's "bind point" areas are what the Wiki calls Bindstones. The spec uses the glossary's terms.
- **Static knowledge (ADR 0006).** A Bindstone is scenery a real client ships with, and fan wikis list them, so it belongs in the Wiki. It's not a Percept and not part of Surroundings. A Percept is added only if a later Agent turns out to need one (ADR 0009).
- **Zone records.** A Wiki zone record also carries the zone's bind points, each with its position and radius. They're read from the bind point areas of the regions the server has loaded. A bind point belongs to the zone its position falls in.
- **Realm filter.** Only bind points of no realm or of the Wiki's realm are read, matching the server's rule for who may bind where. Bind points of another realm, such as the Albion ones in region 200, are left out. This happens where the records are read from the world, with the rest of that reading.
- **Merging.** Bind points whose areas overlap are one Bindstone, at the average of their positions. Overlap is transitive: a chain of overlapping points is one stone. The database has several such pairs, 30–125 units apart.
- **The zone page.** A **Bindstones** section comes after the NPCs, with one row per Bindstone giving its position in the page's existing `X:… Y:… Z:…` form, ordered from west to east. A zone without one says "None in this zone." The radius is not shown. A zone with no NPCs is still left out, as today.
- **The Learning.** The fighting Learning's section on dying gets a paragraph on binding:
  - Bind at the Bindstone of the town you hunt from. Its zone page lists it.
  - Run right up to it (within about 100 units), then send `/bind`.
  - The server first replies with your last Bind point's zone, then "You can't bind here!" if you're not at a stone, or a line saying you're bound if you are. You can't bind while dead, and binding again needs a short wait.
  - Your Bind point isn't tracked for you. Note it in your own files if you need it.

  It has a snippet that runs to a given position, sends `/bind` and returns the reply.
- **The guide** is unchanged, as it holds no game knowledge.
- **Code placement.** The Wiki generator and the fighting Learning only. No core changes, and nothing in the Mind's code.

## Testing Decisions

- **Good tests** check what the Wiki says from records in, markdown out. They don't reach into how pages are assembled.
- **Wiki pages (existing seam).** The function that builds the Wiki's pages from plain zone records, as the Wiki's unit tests already call it:
  - a zone with one bind point lists it as one Bindstone, at its position;
  - two bind points whose areas overlap are one Bindstone at their average position;
  - two bind points far apart are two Bindstones, west to east;
  - a zone with none says so;
  - the rest of the page is as before.
  - Prior art: the existing Wiki tests and their Lough Derg and Shannon Estuary records.
- **The Learning (existing test).** The Learnings test typechecks the new snippet with every other `ts` fence.
- **Not tested automatically:** reading bind points from the loaded world, the realm filter included, as the rest of that reading is untested. It's checked by regenerating Hibernia's Wiki: Ardagh's Bindstone is on the Silvermine Mts. page and Howth's on its zone's page, and no Albion bind point from region 200 appears.
- **Manual acceptance test:**
  1. Run `/agent wiki` for Hibernia and check in the regenerated Wiki.
  2. Start an Agent and tell it to bind in Ardagh and hunt nearby. Check it found the Bindstone through the Wiki.
  3. It binds there: the reply says it's bound.
  4. It dies nearby, releases, and comes back to life in Ardagh, not Howth.
  5. Record the run on this spec.

## Out of Scope

- A Percept for Bindstones, or Bindstones in Surroundings or the State summary. This was considered and dropped to keep it simple; it comes back only if an Agent misses the Wiki.
- Self tracking the Character's Bind point.
- Telling the Agent the server's bind radius.
- Bindstones in houses.
- A porcelain `bind` Tactic. The coding agent can write its own from the Learning's snippet.
- Releasing to a Bind point in another region, which changes region on release. That's still unhandled by the Harness (milestone 4's risk).
- Other realms' Wikis beyond what `/agent wiki` already does for them.

## Further Notes

- Found in milestone 5's acceptance run (`.scratch/coding-agent/spec.md`, "What it ran into") and in Dalta's Lessons.
- Ardagh's bind point is at X:351830 Y:554515 Z:5109 with a radius of 750. Dalta's `/bind` was sent from about X:352406 Y:553339.
- Relevant decisions: ADR 0002 (Primitives through the real packet handlers), ADR 0006 (static world knowledge, the Wiki), ADR 0009 (new Percepts only when needed).

## Comments

**2026-10-24 (agent):** Implemented, except that Hibernia's Wiki isn't regenerated yet: the server running in `Debug\` has the old code, and needs a restart before `/agent wiki`. Where it differs from or adds to the spec above:

- **The records are bind areas, not bind points.** A database row is an area a Character may bind in, which the glossary's Bind point isn't, so the Wiki's record is a bind area and a Bindstone is the bind areas that overlap.
- **Overlap counts height,** as the server's check does, so two bind areas are one Bindstone when their spheres meet.
- **Bind areas are read from the database rows** the regions load theirs from, as a region's own areas aren't public. Areas added with `/addbind` are saved there too.
- **The index says zone pages list Bindstones,** so an Agent that starts at the index knows to look.
- **The Learning's snippet uses Ardagh's database position.** Check it against the regenerated Silvermine Mts. page.

**2026-10-24 (agent):** Regenerated Hibernia's Wiki on the new build. 13 zones have Bindstones and 28 say there are none. Ardagh's is on the Silvermine Mts. page at X:351830 Y:554515 Z:5109, as the Learning's snippet has it, and no Albion bind area from region 200 appears. Overlapping pairs merged, such as Lough Derg's 344788 and 344872 into 344830. The diff also changes Mob and guard levels, which the server rolls at each spawn, and renames the Stygian Delta pages, as only one zone of that name is Hibernia's this time. The manual run in Ardagh is still to do.
