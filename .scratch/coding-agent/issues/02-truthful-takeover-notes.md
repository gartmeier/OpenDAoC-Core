# 02: Truthful Takeover notes

**What to build:** Notes that say a Takeover happened, instead of saying the Character left the world. A Takeover never takes the Character out of the world (ADR 0005), but both Minds' notes say it does:

- The new Mind's first call waits for its login and then says "Dalta wasn't in the world, and has entered it first", because the server sends `entered` both for a login and for a Takeover.
- The old Mind's next call says "Dalta isn't in the world, and entering it failed: another Mind took over the Agent, and this one won't take it back."

A Takeover is also the only sign the coding agent gets that its MCP server restarted, as Dalta's did mid-run. See the spec's acceptance comments: `.scratch/coding-agent/spec.md`.

- **The protocol:** the server's `entered` message says whether this Mind took over. It's false for a login, and true when the Harness hands an online Agent's connection to a new Mind. The generated TypeScript protocol types follow.
- **The new Mind** notes in the Console, after a Takeover: "Dalta was in the world under another Mind, and this Mind has taken it over first." A login keeps today's note.
- **The old Mind** answers a call after it was replaced with "Another Mind has taken Dalta over, and this one won't take it back." It still runs nothing, and it's still an error.

**Blocked by:** None (can start immediately)

**Status:** resolved

- [x] The Harness's `entered` says it took over after a Takeover, and doesn't after a login (C# unit tests, beside the existing Takeover tests in `UT_HarnessClient`)
- [x] The TypeScript protocol types have the new field
- [x] A call that waits for a Takeover gives the Takeover note, and a call that waits for a login still gives the login note (Mind tests)
- [x] A call on a replaced Mind gives the new note, runs nothing and is an error (Mind tests)

## Comments

**2026-10-24 (agent):** Done. Not yet tried on the live server. Where it differs from or adds to the ticket above:

- **`entered` after a login isn't unit-tested in `UT_HarnessClient`,** since world entry has no unit seam there. The protocol test writes both `tookOver: false` and `true`, and the record's field has no default, so every sender says which.
- **A Takeover while the Character is still entering the world counts as a login:** the new Mind hears `tookOver: false` once world entry ends, and says it "wasn't in the world", which is true then.
- **The note shows only on a call that waits for the entry,** as the login's always has. A Takeover the Mind started at launch that's done before the first call shows none.
- **`World.enter` gives an `Entry`** (entered and whether by Takeover, failed and why, or replaced) in place of a refusal string, and `Connection`'s `onEntered` gets `tookOver` with the connection.
