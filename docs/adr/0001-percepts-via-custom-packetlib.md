# Percepts come from a custom IPacketLib

An Agent must only know what a real game client would be told, but parsing the server's outbound byte stream means reimplementing a whole client (about 20 `PacketLib` versions, encoding, crypto). The Harness therefore assigns its own `IPacketLib` implementation to the Agent's `GameClient.Out`, so every `Send*` call the server makes arrives as a typed call and is turned into Percepts. That makes it fair by construction and removes byte parsing entirely.

## Considered Options

- **Parse outbound bytes**: the most faithful option, but this is the "build a whole client" effort that sank earlier attempts.
- **Query the world directly** (e.g. objects in radius) and apply our own visibility rules: cheap, but fairness then depends on rules we would have to write and keep correct, and system messages ("You are too far away") would be lost.

## Consequences

- Any server code that queues packets on the `PacketProcessor` directly instead of going through `Out` goes around this and is invisible to the Agent.
- The `IPacketLib` implementation is called on the game loop thread and must never block.
- It's a runtime proxy that hands every call's arguments over as an `object[]`, so no `IPacketLib` method can take a span or any other `ref struct`, which can't be boxed: the proxy's method throws the first time it's called. The group updates take a list of members for that reason.
- Some server code builds a packet itself and hands it to `Out.SendUDP` or `Out.SendTCP`, notably other players' position broadcasts. Those packets arrive whole rather than as typed calls. One layout is decoded so far, the 1.127 player position, mirroring the server code that builds it with a test that feeds that code's output back through the decoder. Every other raw packet stays a bare Percept naming its code. Decode another layout only when an Agent needs it, never as a general byte parser.
