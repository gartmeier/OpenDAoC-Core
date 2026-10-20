// Generated from the C# types in GameServer/agents/MindProtocol.cs. Don't edit it by hand. Regenerate it with:
// dotnet test Tests/Tests.csproj --filter UT_MindProtocolTypeScript -e REGENERATE_MIND_PROTOCOL=1

export type MindMessage = HelloMessage | PrimitiveMessage;

export interface HelloMessage {
  type: "hello";
  token: string;
  character: string;
}

export interface PrimitiveMessage {
  type: "primitive";
  id: number;
  primitive: Primitive;
}

export type ServerMessage = RefusedMessage | EnteredMessage | PerceptMessage | PrimitiveResultMessage;

export interface RefusedMessage {
  type: "refused";
  reason: string;
  retry: boolean;
}

export interface EnteredMessage {
  type: "entered";
}

export interface PerceptMessage {
  type: "percept";
  sequence: number;
  elapsedMs: number;
  percept: Percept;
}

export interface PrimitiveResultMessage {
  type: "primitiveResult";
  id: number;
  queued: boolean;
  reason?: string;
}

export type Primitive = QuitPrimitive;

export interface QuitPrimitive {
  name: "quit";
}

export type Percept = BarePercept;

export interface BarePercept {
  kind: "bare";
  call: string;
}
