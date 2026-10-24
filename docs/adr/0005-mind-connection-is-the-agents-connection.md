# The Mind's connection is the Agent's network connection

An Agent is online exactly while its Mind is connected, the way a Human is online while their client is connected. Starting a Mind logs its Agent in. Stopping it cleanly sends the real `/quit`. Losing it (a crash, or a WebSocket that stops answering) drops the Harness client like a closed socket, so the server's normal link-death rules apply: the Character shows as linkdead, stays 60 s, then quits and is saved. While connected, the Harness does what a client does on its own (pings, position repeats), so the LLM never handles them. A new Mind for an Agent that's already online takes over the connection. A Mind for an Agent that's still linkdead is refused, as a Human would be, until the link-death window ends.

## Considered Options

- **Server first**: `/agent login` puts the Character in the world, and Minds attach and detach freely. Mind restarts would then be invisible in game, but Agents without a Mind would stand around idle indefinitely.

## Consequences

- Restarting a Mind is a logout and login, or a link death if it crashed.
- The Character can't be online as an Agent without a running Mind for longer than the link-death window.
- A Mind that takes over knows nothing the old one was told. So the Harness has the server tell the client again what it tells one entering the world, and forgets which objects and players the server sent it, so that they're sent anew. A real client never needs this, but nothing reaches the Mind except through the server's own calls (ADR 0001).
