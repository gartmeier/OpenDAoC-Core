# 05: Who gets in: takeover, link-death retry and refusals

**What to build:** The Mind endpoint decides which Mind drives an Agent, the way ADR 0005 says:
- A second Mind for an Agent that's online with a live Mind takes over the connection without a logout. The old Mind gets `replaced` and exits.
- A Mind for an Agent that's still linkdead is refused with retry set. The Mind says it's retrying, tries again every few seconds, and logs in once the server has removed the linkdead Character.
- While a Mind is connected, a real client logging into the Agent's Account is refused, as for any Account already online.
- A Mind whose first message isn't a valid `hello`, or that sends a wrong token or an unknown message, is refused with a clear reason. Nothing it sends can affect the Agent or the server.

See the spec: `.scratch/mind-connection/spec.md`.

**Blocked by:** 02

**Status:** ready-for-agent

- [ ] Starting a second Mind for an online Agent hands it the connection, the first Mind reports it was replaced and exits, and the Character stays in the world throughout
- [ ] Starting a Mind while its Agent is linkdead makes it retry until the Character is gone, then log in
- [ ] Logging into the Agent's Account with the real client while a Mind is connected is refused
- [ ] A wrong token, a missing or malformed `hello`, and unknown or malformed messages each get a clear refusal and a closed connection, with no effect on any Agent
- [ ] Refusals that shouldn't be retried make the Mind exit with the reason
