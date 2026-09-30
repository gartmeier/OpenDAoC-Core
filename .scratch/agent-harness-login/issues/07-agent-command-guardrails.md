# 07: Guardrails on `/agent`

**What to build:** `/agent` refuses unsafe or nonsensical requests with clear messages, so mistakes never fail silently or hijack a Human's Character. See the spec.

**Blocked by:** 04

**Status:** ready-for-agent

- [ ] `/agent login` with an unknown Character name fails with a clear message
- [ ] `/agent login` on a Character whose Account isn't an Agent Account (no `agent_` prefix) is refused
- [ ] `/agent login` on a Character that's already online (Possession by a Human, or another Harness) is refused
- [ ] `/agent logout` on a Character that isn't online as an Agent fails with a clear message and changes nothing
- [ ] A login that doesn't reach in-world play within a time limit is reported as a failure, and the half-connected Harness client is cleaned up
- [ ] Non-admins can't use any `/agent` subcommand
