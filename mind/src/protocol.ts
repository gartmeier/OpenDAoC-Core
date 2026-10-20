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

export type ServerMessage = RefusedMessage | EnteredMessage | PerceptMessage | PrimitiveResultMessage | ReplacedMessage;

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

export interface ReplacedMessage {
  type: "replaced";
}

export type Primitive = QuitPrimitive | SayPrimitive;

export interface QuitPrimitive {
  name: "quit";
}

export interface SayPrimitive {
  name: "say";
  text: string;
}

export type Percept = BarePercept | MessagePercept;

export interface BarePercept {
  kind: "bare";
  call: string;
}

export interface MessagePercept {
  kind: "message";
  text: string;
  chatType: eChatType;
  chatLocation: eChatLoc;
}

export type eChatType = "CT_Say" | "CT_Send" | "CT_Group" | "CT_Guild" | "CT_Broadcast" | "CT_Emote" | "CT_Help" | "CT_Chat" | "CT_Advise" | "CT_Officer" | "CT_Alliance" | "CT_BattleGroup" | "CT_BattleGroupLeader" | "CT_Staff" | "CT_Spell" | "CT_YouHit" | "CT_System" | "CT_Items" | "CT_Merchant" | "CT_YourDeath" | "CT_OthersDeath" | "CT_OthersCombat" | "CT_ResistsChanged" | "CT_SpellExpires" | "CT_Loot" | "CT_SpellResisted" | "CT_Important" | "CT_Damaged" | "CT_Action" | "CT_SpellPulse" | "CT_KilledByAlb" | "CT_KilledByMid" | "CT_KilledByHib" | "CT_LookingForGroup" | "CT_Trade" | "CT_SocialInterface" | "CT_ScreenCenter" | "CT_ScreenCenterSmaller" | "CT_ScreenCenter_And_CT_System" | "CT_ScreenCenterSmaller_And_CT_System";

export type eChatLoc = "CL_ChatWindow" | "CL_PopupWindow" | "CL_SystemWindow";
