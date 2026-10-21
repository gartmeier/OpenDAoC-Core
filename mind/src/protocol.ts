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

export type Primitive = QuitPrimitive | SayPrimitive | MoveToPrimitive | StopPrimitive;

export interface QuitPrimitive {
  name: "quit";
}

export interface SayPrimitive {
  name: "say";
  text: string;
}

export interface MoveToPrimitive {
  name: "moveTo";
  x: number;
  y: number;
  z: number;
}

export interface StopPrimitive {
  name: "stop";
}

export type Percept = BarePercept | MessagePercept | BarePacketPercept | PlayerCreatePercept | PlayerPositionPercept | ObjectDeletePercept | ObjectRemovePercept | OwnPositionPercept | MaxSpeedPercept | MovedPercept | ArrivedPercept | StoppedPercept;

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

export interface BarePacketPercept {
  kind: "barePacket";
  call: string;
  code: number;
}

export interface PlayerCreatePercept {
  kind: "playerCreate";
  x: number;
  y: number;
  z: number;
  sessionId: number;
  objectId: number;
  heading: number;
  model: number;
  level: number;
  realm: PacketRealm;
  dead: boolean;
  swimming: boolean;
  stealthed: boolean;
  wireframe: boolean;
  vampiir: boolean;
  eyeSize: number;
  lipSize: number;
  moodType: number;
  eyeColor: number;
  hairColor: number;
  faceType: number;
  hairStyle: number;
  name: string;
  guildName: string;
  lastName: string;
  prefixName: string;
  title: string;
  horse?: PlayerCreateHorse;
}

export interface PlayerPositionPercept {
  kind: "playerPosition";
  x: number;
  y: number;
  z: number;
  speed: number;
  fallSpeed: number;
  sessionId: number;
  objectId: number;
  zoneId: number;
  stateFlags: number;
  steedSeat: number;
  heading: number;
  headingFlags: number;
  actionFlags: number;
  roleplay: boolean;
  healthPercent: number;
  attacking: boolean;
  manaPercent: number;
  endurancePercent: number;
}

export interface ObjectDeletePercept {
  kind: "objectDelete";
  objectId: number;
}

export interface ObjectRemovePercept {
  kind: "objectRemove";
  objectId: number;
  objectType: number;
}

export interface OwnPositionPercept {
  kind: "ownPosition";
  x: number;
  y: number;
  z: number;
  objectId: number;
  heading: number;
  zoneXOffset: number;
  zoneYOffset: number;
  region: number;
  divingEnabled: boolean;
  underwater: boolean;
  serverName?: string;
}

export interface MaxSpeedPercept {
  kind: "maxSpeed";
  maxSpeedPercent: number;
  turningDisabled: boolean;
  waterSpeed: number;
}

export interface MovedPercept {
  kind: "moved";
  x: number;
  y: number;
  z: number;
  heading: number;
  speed: number;
  targetX: number;
  targetY: number;
  targetZ: number;
}

export interface ArrivedPercept {
  kind: "arrived";
  x: number;
  y: number;
  z: number;
}

export interface StoppedPercept {
  kind: "stopped";
  x: number;
  y: number;
  z: number;
  reason: StopReason;
}

export type eChatType = "CT_Say" | "CT_Send" | "CT_Group" | "CT_Guild" | "CT_Broadcast" | "CT_Emote" | "CT_Help" | "CT_Chat" | "CT_Advise" | "CT_Officer" | "CT_Alliance" | "CT_BattleGroup" | "CT_BattleGroupLeader" | "CT_Staff" | "CT_Spell" | "CT_YouHit" | "CT_System" | "CT_Items" | "CT_Merchant" | "CT_YourDeath" | "CT_OthersDeath" | "CT_OthersCombat" | "CT_ResistsChanged" | "CT_SpellExpires" | "CT_Loot" | "CT_SpellResisted" | "CT_Important" | "CT_Damaged" | "CT_Action" | "CT_SpellPulse" | "CT_KilledByAlb" | "CT_KilledByMid" | "CT_KilledByHib" | "CT_LookingForGroup" | "CT_Trade" | "CT_SocialInterface" | "CT_ScreenCenter" | "CT_ScreenCenterSmaller" | "CT_ScreenCenter_And_CT_System" | "CT_ScreenCenterSmaller_And_CT_System";

export type eChatLoc = "CL_ChatWindow" | "CL_PopupWindow" | "CL_SystemWindow";

export type PacketRealm = "None" | "Albion" | "Midgard" | "Hibernia";

export interface PlayerCreateHorse {
  id: number;
  barding: number;
  bardingColor: number;
  saddle: number;
  saddleColor: number;
}

export type StopReason = "Stop" | "Replaced";
