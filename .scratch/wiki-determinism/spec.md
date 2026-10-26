# Wiki determinism

Status: needs-triage
Priority: low

## Problem Statement

Writing the Wiki again with `/agent wiki` rewrites most zone pages, the index and a few merchant pages even when no game data changed. Two runs in the same server start match. Two server starts don't, because the Wiki reads the world as it spawned this time. So every regeneration, such as one for new class pages, comes with a diff of noise that a Human has to throw away by hand (`git checkout` the zone and merchant pages) before committing.

What varies between server starts, found on 2026-10-26:

- **Order**: NPCs of the same name, such as a town's Hasteners or Guardians, are listed in the order the server spawned them.
- **Template levels**: an NPC with a template rolls its level from the template's list or range (Fallen One 18–24), and a template ID with several rows is picked at random (Ciarda the Druid Trainer is 40 or 50).
- **Template roles**: an NPC whose template replaces its own values takes the template's guild name. Eachann in Tir na Nog is an armor merchant at level 1 or a Mentalist Trainer at 40, because its row points at the trainer's template.
- **Keep guards**: realm guards (`GameKeepGuard`: Guardians, Sentinels, Lookouts, keep lords) get a random level, and a lord a random Chieftain or Chieftess name. Their realm follows whoever holds the keep.
- **Script-spawned Mobs**: a boss's adds, such as Cronwort's Breanworts and the Fire Blights, spawn at random offsets and have no database row.
- **Region realm ties**: a zone with no realm of its own belongs to the realm most of its region's realm NPCs belong to. Stygian Delta's hub has one Master Trainer per realm, so which realm wins changes, and its zone 138 comes and goes. That also renames `stygian-delta.md` to `stygian-delta-81.md`.
- **Epic bosses**: some named Mobs' scripts roll their own level as they spawn and save it into their database row, such as Zytka (`Util.Random(65, 70)`), Melancholic Fairy Queen (64–68) and Giant Caithor (62–65). The range is only in their C# code.

## Solution

Not decided. The simplest option, and the one in use: do nothing, and keep only the intended pages when committing a regeneration.

A tried fix, reverted on 2026-10-26: the Wiki wrote only what's the same each start. It ordered NPCs of the same name west to east, gave each NPC every level and role its templates allow, left out keep guards and Mobs with no database row (both dynamic, per ADR 0006), and gave a region tied between realms to none. All of that removed the noise except the epic bosses. A `GameNPC.RollLevel(min, max)` helper that remembered the range, with all 76 `Level = (byte)Util.Random(a, b)` lines in 60 scripts switched to it, covered those too, but it was reverted as too broad for the gain.

Options without changing C#, for the epic bosses: a post-processing script with their ranges, or a hand-written page beside the generated ones. Neither stops the zone page itself from changing.

## User Stories

1. As a Human regenerating the Wiki for one reason, I want only the pages that reason touches to change, so the diff shows what changed in the game.
2. As an Agent reading a zone page, I want an NPC's level to be every level it can spawn at, not one roll, so I don't misjudge a Mob.

## Implementation Decisions

- Left open for triage: whether this is worth C# changes at all, and if so, which of the reverted pieces to bring back.
- A keep guard's state is dynamic knowledge (ADR 0006), which argues for leaving keep guards out whatever else is decided.

## Testing Decisions

- Page order and level ranges are pure and testable through `Wiki.Pages`, as `UT_Wiki` does.
- What `FromWorld` reads can only be checked against a running server: write the Wiki from two separate server starts into two folders and `diff -r` them.

## Out of Scope

- Class pages, which come from static data and don't change between starts.
- Fixing the data itself, such as Eachann's template or the epic bosses saving their rolled level.
