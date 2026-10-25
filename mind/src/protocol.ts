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
  tookOver: boolean;
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

export type Primitive = QuitPrimitive | SayPrimitive | MoveToPrimitive | StopPrimitive | FacePrimitive | TargetPrimitive | AttackPrimitive | SitPrimitive | StandPrimitive | CommandPrimitive | MoveItemPrimitive | ExaminePrimitive | ExamineMerchantItemPrimitive | PickUpPrimitive | TalkToPrimitive | BuyPrimitive | SellPrimitive | UseStylePrimitive | AnswerDialogPrimitive;

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
  within?: number;
}

export interface StopPrimitive {
  name: "stop";
}

export interface FacePrimitive {
  name: "face";
  heading: number;
}

export interface TargetPrimitive {
  name: "target";
  objectId: number;
}

export interface AttackPrimitive {
  name: "attack";
  on: boolean;
}

export interface SitPrimitive {
  name: "sit";
}

export interface StandPrimitive {
  name: "stand";
}

export interface CommandPrimitive {
  name: "command";
  text: string;
}

export interface MoveItemPrimitive {
  name: "moveItem";
  from: number;
  to: number;
}

export interface ExaminePrimitive {
  name: "examine";
  slot: number;
}

export interface ExamineMerchantItemPrimitive {
  name: "examineMerchantItem";
  index: number;
}

export interface PickUpPrimitive {
  name: "pickUp";
  objectId: number;
}

export interface TalkToPrimitive {
  name: "talkTo";
  objectId: number;
}

export interface BuyPrimitive {
  name: "buy";
  index: number;
  count: number;
}

export interface SellPrimitive {
  name: "sell";
  slot: number;
}

export interface UseStylePrimitive {
  name: "useStyle";
  style: string;
  line?: string;
}

export interface AnswerDialogPrimitive {
  name: "answerDialog";
  code: eDialogCode;
  data1: number;
  data2: number;
  data3: number;
  accept: boolean;
}

export type Percept = BarePercept | MessagePercept | BarePacketPercept | PlayerCreatePercept | PlayerPositionPercept | NpcCreatePercept | ObjectUpdatePercept | ObjectDeletePercept | ObjectRemovePercept | OwnPositionPercept | JumpPercept | MaxSpeedPercept | StatusPercept | PointsPercept | PlayerPercept | SkillsPercept | EffectsPercept | DiedPercept | RevivedPercept | InventoryPercept | EncumbrancePercept | MoneyPercept | ExaminedPercept | MerchantWindowPercept | DialogPercept | StatsPercept | RoutedPercept | MovedPercept | ArrivedPercept | StoppedPercept;

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

export interface NpcCreatePercept {
  kind: "npcCreate";
  x: number;
  y: number;
  z: number;
  objectId: number;
  speed: number;
  zSpeed: number;
  heading: number;
  model: number;
  size: number;
  level: number;
  statue: boolean;
  realm: PacketRealm;
  ghost: boolean;
  equipped: boolean;
  torch: boolean;
  peaceful: boolean;
  flying: boolean;
  pet: boolean;
  untargetable: boolean;
  nameHidden: boolean;
  stealthed: boolean;
  questIndicator: eQuestIndicator;
  name: string;
  guildName: string;
}

export interface ObjectUpdatePercept {
  kind: "objectUpdate";
  x: number;
  y: number;
  z: number;
  objectId: number;
  speed: number;
  zSpeed: number;
  heading: number;
  targetId: number;
  healthPercent: number;
  realm: PacketRealm;
  untargetable: boolean;
  nameHidden: boolean;
  underwater: boolean;
  flying: boolean;
  zoneId: number;
  destination?: ObjectUpdateDestination;
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

export interface JumpPercept {
  kind: "jump";
  x: number;
  y: number;
  z: number;
  objectId: number;
  heading: number;
  houseNumber: number;
}

export interface MaxSpeedPercept {
  kind: "maxSpeed";
  maxSpeedPercent: number;
  turningDisabled: boolean;
  waterSpeed: number;
}

export interface StatusPercept {
  kind: "status";
  healthPercent: number;
  manaPercent: number;
  sitting: boolean;
  endurancePercent: number;
  concentrationPercent: number;
  maxMana: number;
  maxEndurance: number;
  maxConcentration: number;
  maxHealth: number;
  health: number;
  endurance: number;
  mana: number;
  concentration: number;
}

export interface PointsPercept {
  kind: "points";
  realmPoints: number;
  levelPermill: number;
  specPoints: number;
  bountyPoints: number;
  realmSpecPoints: number;
  championLevelPermill: number;
  experience: number;
  experienceForNextLevel: number;
}

export interface PlayerPercept {
  kind: "player";
  level: number;
  name: string;
  className: string;
  baseClassName: string;
  maxHealth: number;
}

export interface SkillsPercept {
  kind: "skills";
  skills: SkillEntry[];
}

export interface EffectsPercept {
  kind: "effects";
  effects: EffectEntry[];
}

export interface DiedPercept {
  kind: "died";
  objectId: number;
  killerId: number;
}

export interface RevivedPercept {
  kind: "revived";
  objectId: number;
}

export interface InventoryPercept {
  kind: "inventory";
  window: eInventoryWindowType;
  slots: InventorySlot[];
}

export interface EncumbrancePercept {
  kind: "encumbrance";
  capacity: number;
  weight: number;
}

export interface MoneyPercept {
  kind: "money";
  copper: number;
  silver: number;
  gold: number;
  mithril: number;
  platinum: number;
}

export interface ExaminedPercept {
  kind: "examined";
  caption: string;
  lines: string[];
}

export interface MerchantWindowPercept {
  kind: "merchantWindow";
  window: eMerchantWindowType;
  items: MerchantItem[];
}

export interface DialogPercept {
  kind: "dialog";
  code: eDialogCode;
  data1: number;
  data2: number;
  data3: number;
  yesNo: boolean;
  text: string;
}

export interface StatsPercept {
  kind: "stats";
  constitutionLost: number;
}

export interface RoutedPercept {
  kind: "routed";
  corners: RouteCorner[];
  reachesTarget: boolean;
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

export type eDialogCode = "SimpleWarning" | "GuildInvite" | "GroupInvite" | "CustomDialog" | "GuildLeave" | "HousePayRent" | "MasterLevelWindow" | "KeepClaim" | "BuyRespec" | "WarmapWindowHibernia" | "WarmapWindowAlbion" | "WarmapWindowMidgard" | "QuestSubscribe";

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

export type eQuestIndicator = "None" | "Available" | "Finish" | "Lesson" | "Lore" | "Pending";

export interface ObjectUpdateDestination {
  x: number;
  y: number;
  z: number;
  zoneId: number;
}

export interface SkillEntry {
  name: string;
  level: number;
  page: SkillPage;
  line?: string;
  details?: StyleDetails;
}

export interface EffectEntry {
  name: string;
  positive: boolean;
  secondsLeft?: number;
  details?: SpellDetails;
}

export type eInventoryWindowType = "Update" | "Equipment" | "Inventory" | "PlayerVault" | "HouseVault" | "ConsignmentOwner" | "ConsignmentViewer" | "HorseBags";

export interface InventorySlot {
  slot: number;
  item?: InventoryItem;
}

export type eMerchantWindowType = "Normal" | "Bp" | "Count" | "HousingDeedMenu" | "HousingOutsideMenu" | "HousingNPCHookpoint" | "HousingInsideShop" | "HousingOutsideShop" | "HousingVaultHookpoint" | "HousingCraftingHookpoint" | "HousingBindstoneHookpoint" | "HousingInsideMenu" | "HousingTicket" | "HousingGuildTicket";

export interface MerchantItem {
  page: number;
  place: number;
  level: number;
  dpsAf: number;
  speedAbs: number;
  count: number;
  hand: number;
  damageType: number;
  objectType: number;
  usable: boolean;
  weight: number;
  price: number;
  model: number;
  name: string;
}

export interface RouteCorner {
  x: number;
  y: number;
  z: number;
}

export type StopReason = "Stop" | "Replaced" | "MovedByServer" | "UnableToMove" | "Unreachable";

export type SkillPage = "Specialization" | "Abilities" | "Styles" | "Spells" | "Songs" | "AbilitiesSpell" | "RealmAbilities";

export interface StyleDetails {
  opening: eOpening;
  openingResult: eAttackResultRequirement;
  openingPosition?: eOpeningPosition;
  openingStyle?: string;
  followupStyles: string[];
  fatigue: number;
  attackMod: number;
  defensiveMod: number;
  openingDamage: number;
  hidden: boolean;
  weapon?: string;
}

export interface SpellDetails {
  level: number;
  durationSeconds?: number;
  powerCost?: number;
  target: string;
  range?: number;
  castingSeconds: number;
  magicType?: string;
  radius?: number;
  description: string;
}

export interface InventoryItem {
  name: string;
  level: number;
  dpsAf: number;
  speedAbs: number;
  count: number;
  hand: number;
  damageType: number;
  objectType: number;
  weight: number;
  condition: number;
  durability: number;
  quality: number;
  bonus: number;
  bonusLevel: number;
  model: number;
  color: number;
  effect: number;
  spellNames: string[];
}

export type eOpening = "Offensive" | "Defensive" | "Positional";

export type eAttackResultRequirement = "Any" | "Miss" | "Hit" | "Parry" | "Block" | "Evade" | "Fumble" | "Style";

export type eOpeningPosition = "Back" | "Side" | "Front";
