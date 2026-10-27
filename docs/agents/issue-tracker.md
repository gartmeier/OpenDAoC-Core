# Issue tracker: Forgejo

Issues and specs for this repo live as issues on the Forgejo instance at https://git.gartmeier.dev/joshua/OpenDAoC-Core. Use the [`fj`](https://codeberg.org/forgejo-contrib/forgejo-cli) CLI for all operations. `fj` infers the repo from `git remote -v` when run inside a clone.

## Conventions

- **Spec**: an issue labelled `spec`. Its body ends with a `## Tickets` task list (`- [ ] #<ticket>`).
- **Ticket**: an issue whose body starts with `Part of #<spec>`. It carries no `spec` label.
- **Research**: a Markdown file in `docs/research/<topic>.md`, linked from the spec it informs.
- **Create an issue**: `fj issue create "<title>" --body-file <file> --no-template`. Write multi-line bodies to a file first. Short bodies can use `--body "..."`.
- **Read an issue**: `fj issue view <n>` for the body, `fj issue view <n> comments` for the comments.
- **List issues**: `fj issue search --all -s open`, filtered with `-l <label>` or `-m <milestone>`. Use `-s all` or `-s closed` for other states.
- **Comment on an issue**: `fj issue comment <n> --body-file <file>`, or `fj issue comment <n> "<body>"` for a short one.
- **Edit an issue's body**: `fj issue edit <n> body "<new body>"`.
- **Apply / remove labels**: `fj issue edit <n> labels --add <label>` / `--rm <label>`. Repeat the flag for each label; a comma-separated list is taken as one label name.
- **Close**: `fj issue close <n> --with-msg "..."`.

## Milestones

Each milestone ends with an Agent playing. Its spec is an issue in that milestone, and the spec's tickets go in the same milestone. Smaller specs come up between milestones whenever an Agent turns out to need something (ADR 0009). They get no milestone.

- **List**: `fj milestone search --state all`.
- **Create**: `fj milestone create "<title>" --state open --description "..."`. A milestone's description starts with `Spec: #<spec>` once its spec exists.
- **Put an issue in a milestone**: `fj issue edit <n> milestone "<title>"`. The `--milestone` flag on `fj issue create` is silently ignored, so set it afterwards. The title is matched loosely: `Spells` matched `Damage spells`. Check with `fj issue search -s all -m "<title>"`.
- **Close a milestone** once its Agent has played: `fj milestone edit "<title>" --state closed`. Add what happened to its description.

## Pull requests as a triage surface

**PRs as a request surface: no.** _(Set to `yes` if this repo treats external PRs as feature requests; `/triage` reads this flag.)_

Forgejo shares one number space across issues and PRs, so a bare `#42` may be either: resolve with `fj pr view 42` and fall back to `fj issue view 42`.

## When a skill says "publish to the issue tracker"

Create a Forgejo issue. A spec gets the `spec` label. A ticket starts with `Part of #<spec>`, and the spec's `## Tickets` task list gets a line for it.

## When a skill says "fetch the relevant ticket"

Run `fj issue view <n>` and `fj issue view <n> comments`.

## Wayfinding operations

Used by `/wayfinder`. The **map** is a single issue with **child** issues as tickets.

- **Map**: a single issue labelled `wayfinder:map`, holding the Notes / Decisions-so-far / Fog body.
- **Child ticket**: an issue with `Part of #<map>` at the top of its body, listed in a task list in the map body. Labels: `wayfinder:<type>` (`research`/`prototype`/`grilling`/`task`). Once claimed, the ticket is assigned to the driving dev.
- **Blocking**: Forgejo's native issue dependencies. Add an edge with `fj issue depend add <child> <blocker>...` and read them with `fj issue depend list <child>`, which shows each blocker as open or closed. A ticket is unblocked when every blocker is closed.
- **Frontier query**: list the map's open children (`fj issue search -s open`, scoped to the map's task list), drop any with an open blocker or an assignee; first in map order wins.
- **Claim**: `fj issue assign <n> joshua`, the session's first write.
- **Resolve**: `fj issue comment <n> "<answer>"`, then `fj issue close <n>`, then append a context pointer (gist + link) to the map's Decisions-so-far.
