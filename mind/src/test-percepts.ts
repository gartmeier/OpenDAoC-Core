// Percepts as the Harness sends them, for tests. Every field a test doesn't pass is a plain default.
import type {
  ArrivedPercept,
  DiedPercept,
  EffectEntry,
  EffectsPercept,
  EncumbrancePercept,
  ExaminedPercept,
  InventoryItem,
  InventoryPercept,
  InventorySlot,
  MerchantItem,
  MerchantWindowPercept,
  MoneyPercept,
  NpcCreatePercept,
  ObjectCreatePercept,
  ObjectDeletePercept,
  ObjectRemovePercept,
  ObjectUpdatePercept,
  OwnPositionPercept,
  Percept,
  PerceptMessage,
  PlayerCreatePercept,
  PlayerPercept,
  PlayerPositionPercept,
  PointsPercept,
  RevivedPercept,
  SkillEntry,
  SkillsPercept,
  StatusPercept,
  StopReason,
  StoppedPercept,
} from "./protocol.ts";

export function message(percept: Percept, elapsedMs = 1000): PerceptMessage {
  return { type: "percept", sequence: 1, elapsedMs, percept };
}

/** The Agent's Character, object ID 17, as the server places it at world entry. */
export function ownPosition(x: number, y: number, z: number, objectId = 17): OwnPositionPercept {
  return { kind: "ownPosition", x, y, z, objectId, heading: 0, zoneXOffset: 0, zoneYOffset: 0, region: 200, divingEnabled: false, underwater: false };
}

export function sayLine(text: string): Percept {
  return { kind: "message", text, chatType: "CT_Say", chatLocation: "CL_ChatWindow" };
}

/** What an NPC says in a popup, as a trainer asks which class a Character chooses. */
export function popup(text: string): Percept {
  return { kind: "message", text, chatType: "CT_System", chatLocation: "CL_PopupWindow" };
}

export function playerCreate(objectId: number, name: string, x: number, y: number, z: number): PlayerCreatePercept {
  return {
    kind: "playerCreate",
    x,
    y,
    z,
    sessionId: objectId + 1000,
    objectId,
    heading: 0,
    model: 0x02be,
    level: 23,
    realm: "Hibernia",
    dead: false,
    swimming: false,
    stealthed: false,
    wireframe: false,
    vampiir: false,
    eyeSize: 0,
    lipSize: 0,
    moodType: 0,
    eyeColor: 0,
    hairColor: 0,
    faceType: 0,
    hairStyle: 0,
    name,
    guildName: "",
    lastName: "",
    prefixName: "",
    title: "",
  };
}

export function playerPosition(objectId: number, x: number, y: number, z: number): PlayerPositionPercept {
  return {
    kind: "playerPosition",
    x,
    y,
    z,
    speed: 0,
    fallSpeed: 0,
    sessionId: objectId + 1000,
    objectId,
    zoneId: 202,
    stateFlags: 0,
    steedSeat: 0,
    heading: 0,
    headingFlags: 0,
    actionFlags: 0,
    roleplay: false,
    healthPercent: 100,
    attacking: false,
    manaPercent: 100,
    endurancePercent: 100,
  };
}

/** A level 1 Mob with no realm, unless `fields` say otherwise. */
export function npcCreate(objectId: number, name: string, x: number, y: number, z: number, fields: Partial<NpcCreatePercept> = {}): NpcCreatePercept {
  return {
    kind: "npcCreate",
    x,
    y,
    z,
    objectId,
    speed: 0,
    zSpeed: 0,
    heading: 0,
    model: 0x190,
    size: 40,
    level: 1,
    statue: false,
    realm: "None",
    ghost: false,
    equipped: false,
    torch: false,
    peaceful: false,
    flying: false,
    pet: false,
    untargetable: false,
    nameHidden: false,
    stealthed: false,
    questIndicator: "None",
    name,
    guildName: "",
    ...fields,
  };
}

/** A World object, such as a Grave. */
export function objectCreate(objectId: number, name: string, x: number, y: number, z: number): ObjectCreatePercept {
  return { kind: "objectCreate", x, y, z, objectId, heading: 0, model: 637, realm: "Hibernia", emblem: 0, underwater: false, keepBanner: false, yours: false, name };
}

export function objectUpdate(objectId: number, x: number, y: number, z: number, fields: Partial<ObjectUpdatePercept> = {}): ObjectUpdatePercept {
  return {
    kind: "objectUpdate",
    x,
    y,
    z,
    objectId,
    speed: 0,
    zSpeed: 0,
    heading: 0,
    targetId: 0,
    healthPercent: 100,
    realm: "None",
    untargetable: false,
    nameHidden: false,
    underwater: false,
    flying: false,
    zoneId: 202,
    ...fields,
  };
}

export function objectDelete(objectId: number): ObjectDeletePercept {
  return { kind: "objectDelete", objectId };
}

/** Object type 0 is anything that isn't a player or a living NPC, a dead NPC included. */
export function objectRemove(objectId: number, objectType = 0): ObjectRemovePercept {
  return { kind: "objectRemove", objectId, objectType };
}

export function status(healthPercent: number, fields: Partial<StatusPercept> = {}): StatusPercept {
  return {
    kind: "status",
    healthPercent,
    manaPercent: 0,
    sitting: false,
    endurancePercent: 100,
    concentrationPercent: 0,
    maxMana: 0,
    maxEndurance: 100,
    maxConcentration: 0,
    maxHealth: 100,
    health: healthPercent,
    endurance: 100,
    mana: 0,
    concentration: 0,
    ...fields,
  };
}

export function points(specPoints: number, levelPermill = 0): PointsPercept {
  return { kind: "points", realmPoints: 0, levelPermill, specPoints, bountyPoints: 0, realmSpecPoints: 0, championLevelPermill: 0, experience: 0, experienceForNextLevel: 0 };
}

export function player(level: number): PlayerPercept {
  return { kind: "player", level, name: "Greeen", className: "Guardian", baseClassName: "Guardian", maxHealth: 100 };
}

export function skills(...entries: SkillEntry[]): SkillsPercept {
  return { kind: "skills", skills: entries };
}

/** A specialization at its trained level. */
export function spec(name: string, level: number): SkillEntry {
  return { name, level, page: "Specialization" };
}

export function effects(...entries: EffectEntry[]): EffectsPercept {
  return { kind: "effects", effects: entries };
}

export function died(objectId: number, killerId = 0): DiedPercept {
  return { kind: "died", objectId, killerId };
}

export function revived(objectId: number): RevivedPercept {
  return { kind: "revived", objectId };
}

export function arrived(x: number, y: number, z: number): ArrivedPercept {
  return { kind: "arrived", x, y, z };
}

export function stopped(x: number, y: number, z: number, reason: StopReason): StoppedPercept {
  return { kind: "stopped", x, y, z, reason };
}

/** The server's `eObjectType` for an item that's none of the others. */
export const GENERIC_ITEM = 0;

/** A level 1 generic item of quality 100 in good condition, unless `fields` say otherwise. */
export function item(name: string, fields: Partial<InventoryItem> = {}): InventoryItem {
  return {
    name,
    level: 1,
    dpsAf: 0,
    speedAbs: 0,
    count: 1,
    hand: 0,
    damageType: 0,
    objectType: GENERIC_ITEM,
    weight: 10,
    condition: 100,
    durability: 100,
    quality: 100,
    bonus: 0,
    bonusLevel: 0,
    model: 0,
    color: 0,
    effect: 0,
    spellNames: [],
    ...fields,
  };
}

/** Each slot with the item now in it, or empty without one. */
export function inventory(...slots: [number, InventoryItem?][]): InventoryPercept {
  return { kind: "inventory", window: "Update", slots: slots.map(([slot, item]): InventorySlot => (item ? { slot, item } : { slot })) };
}

/** `copper` split into coins, as the server sends it. */
export function money(copper: number): MoneyPercept {
  return {
    kind: "money",
    copper: copper % 100,
    silver: Math.floor(copper / 100) % 100,
    gold: Math.floor(copper / 10_000) % 1000,
    mithril: Math.floor(copper / 10_000_000) % 1000,
    platinum: Math.floor(copper / 10_000_000_000),
  };
}

export function encumbrance(capacity: number, weight: number): EncumbrancePercept {
  return { kind: "encumbrance", capacity, weight };
}

export function examined(caption: string, ...lines: string[]): ExaminedPercept {
  return { kind: "examined", caption, lines };
}

export function merchantWindow(...items: MerchantItem[]): MerchantWindowPercept {
  return { kind: "merchantWindow", window: "Normal", items };
}

/** A level 1 item the Agent's class can use, on the first page, unless `fields` say otherwise. */
export function merchantItem(place: number, name: string, price: number, fields: Partial<MerchantItem> = {}): MerchantItem {
  return { page: 0, place, level: 1, dpsAf: 0, speedAbs: 0, count: 1, hand: 0, damageType: 0, objectType: GENERIC_ITEM, usable: true, weight: 10, price, model: 0, name, ...fields };
}
