# Percepts come from a custom IPacketLib

An Agent must only know what a real game client would be told, but parsing the server's outbound byte stream means reimplementing a whole client (about 20 `PacketLib` versions, encoding, crypto). The Harness therefore assigns its own `IPacketLib` implementation to the Agent's `GameClient.Out`, so every `Send*` call the server makes arrives as a typed call and is turned into Percepts. That makes it fair by construction and removes byte parsing entirely.

## Considered Options

- **Parse outbound bytes**: the most faithful option, but this is the "build a whole client" effort that sank earlier attempts.
- **Query the world directly** (e.g. objects in radius) and apply our own visibility rules: cheap, but fairness then depends on rules we would have to write and keep correct, and system messages ("You are too far away") would be lost.

## Consequences

- Any server code that queues packets on the `PacketProcessor` directly instead of going through `Out` goes around this and is invisible to the Agent.
- The `IPacketLib` implementation is called on the game loop thread and must never block.
