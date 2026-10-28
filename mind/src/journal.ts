// The Journal's lines: one for every Percept the Agent gets and every Primitive it sends, naming players, NPCs, World objects and items from its beliefs.
import { GROUP_MEMBER_FLAGS } from "./group.ts";
import { merchantIndex } from "./inventory.ts";
import type { EffectEntry, InventoryItem, Percept, PerceptMessage, Primitive } from "./protocol.ts";
import type { Position } from "./surroundings.ts";
import { closeSync, openSync, writeSync } from "node:fs";
import type { Agent } from "./tactic.ts";

/** A Journal file, appended to line by line. */
export interface JournalFile {
  write(line: string): void;
  close(): void;
}

/**
 * Opens the Journal file at `path` for appending. Each line is written before `write` returns.
 * A snippet's module graph ends whatever it started once the snippet ends, so a line it wrote asynchronously could still be under way then.
 * That write would never finish, and every line after it would wait for it.
 */
export function openJournal(path: string): JournalFile {
  const fd = openSync(path, "a");
  return { write: (line) => void writeSync(fd, `${line}\n`), close: () => closeSync(fd) };
}

/** A Percept's line, after the Agent has taken it in. */
export function describePercept(agent: Agent, character: string, { sequence, elapsedMs, percept }: PerceptMessage): string {
  return `#${sequence}\t${(elapsedMs / 1000).toFixed(3)} s\t${describe(agent, character, percept)}`;
}

function describe(agent: Agent, character: string, percept: Percept): string {
  const { self, surroundings, inventory } = agent;
  const nameOf = (objectId: number) => nameIn(agent, character, objectId);

  switch (percept.kind) {
    case "bare":
      return percept.call;
    case "message":
      return `${percept.chatType} ${percept.chatLocation}\t${percept.text}`;
    case "barePacket":
      return `${percept.call} 0x${percept.code.toString(16).toUpperCase().padStart(2, "0")}`;
    case "playerCreate":
      return `playerCreate #${percept.objectId} ${percept.name}\t${at(percept)} heading ${percept.heading}`;
    case "playerPosition":
      return `playerPosition #${percept.objectId} ${surroundings.object(percept.objectId)?.name ?? "(unknown)"}\t${at(percept)} heading ${percept.heading} speed ${percept.speed} zone ${percept.zoneId}`;
    case "npcCreate":
      return `npcCreate #${percept.objectId} ${percept.name}\t${at(percept)} heading ${percept.heading} speed ${percept.speed} level ${percept.level}`;
    case "objectCreate":
      return `objectCreate #${percept.objectId} ${percept.name}\t${at(percept)} heading ${percept.heading} model ${percept.model}`;
    case "objectUpdate": {
      const target = percept.targetId ? ` target #${percept.targetId}` : "";
      const destination = percept.destination ? ` to ${at(percept.destination)}` : "";
      return `objectUpdate #${percept.objectId} ${surroundings.object(percept.objectId)?.name ?? "(unknown)"}\t${at(percept)} heading ${percept.heading} speed ${percept.speed} zone ${percept.zoneId} health ${percept.healthPercent}%${target}${destination}`;
    }
    case "objectDelete":
      return `objectDelete #${percept.objectId}`;
    case "objectRemove":
      return `objectRemove #${percept.objectId} type ${percept.objectType}`;
    case "ownPosition":
      return `ownPosition #${percept.objectId}\t${at(percept)} heading ${percept.heading} region ${percept.region}`;
    case "jump":
      return percept.x === 0 && percept.y === 0 ? `jump #${percept.objectId}\theading ${percept.heading}` : `jump #${percept.objectId}\t${at(percept)} heading ${percept.heading}`;
    case "maxSpeed":
      return `maxSpeed ${percept.maxSpeedPercent}% (${self.maxSpeed.toFixed(1)} per second)`;
    case "routed":
      // A Swim leg is joined with `~`.
      return `routed (${percept.reachesTarget ? "reaches the target" : "can't reach the target"})\t${percept.corners.map((corner, i) => (i === 0 ? "" : percept.corners[i - 1]?.swim ? " ~ " : " → ") + at(corner)).join("")}`;
    case "moved":
      return `moved\t${at(percept)} heading ${percept.heading} speed ${percept.speed.toFixed(1)} to ${at({ x: percept.targetX, y: percept.targetY, z: percept.targetZ })}`;
    case "arrived":
      return `arrived\t${at(percept)}`;
    case "stopped":
      return `stopped (${percept.reason})\t${at(percept)}`;
    case "status":
      return `status\thealth ${percept.healthPercent}% (${percept.health}/${percept.maxHealth})${percept.maxPower ? ` power ${percept.powerPercent}% (${percept.power}/${percept.maxPower})` : ""} endurance ${percept.endurancePercent}%${percept.maxConcentration ? ` concentration ${percept.concentration}/${percept.maxConcentration}` : ""}${percept.sitting ? " sitting" : ""}`;
    case "points":
      return `points\texperience ${percept.levelPermill}‰ spec points ${percept.specPoints}`;
    case "player":
      return `player\tlevel ${percept.level} ${percept.name} the ${percept.className}`;
    case "skills":
      return `skills\t${percept.skills.map((skill) => `${skill.name} ${skill.level}`).join(", ")}`;
    case "effects":
      return `effects\t${percept.effects.map(describeEffect).join(", ") || "none"}`;
    case "concentration":
      return `concentration\t${percept.buffs.map((buff) => `${buff.spell} on ${buff.target} (${buff.cost})`).join(", ") || "none"}`;
    case "petWindow":
      return percept.action === "Close" ? "petWindow Close" : `petWindow ${percept.action}\t#${percept.objectId} ${nameOf(percept.objectId)} ${percept.aggression} ${percept.walk}`;
    case "castStarted":
      return `castStarted #${percept.casterId} ${nameOf(percept.casterId)}\t${(percept.castingMs / 1000).toFixed(1)} s${percept.casterId === self.objectId && self.casting ? ` ${self.casting.spell}` : ""}`;
    case "spellLanded":
      return `spellLanded #${percept.casterId} ${nameOf(percept.casterId)} on #${percept.targetId} ${nameOf(percept.targetId)}${percept.success ? "" : " (didn't take)"}`;
    case "interrupted":
      return `interrupted #${percept.objectId} ${nameOf(percept.objectId)}`;
    case "cooldowns":
      return `cooldowns\t${percept.skills.map((skill) => `${skill.name} ${skill.seconds} s`).join(", ")}`;
    case "died":
      return `died #${percept.objectId} ${nameOf(percept.objectId)}, killed by #${percept.killerId} ${nameOf(percept.killerId)}`;
    case "revived":
      return `revived #${percept.objectId} ${nameOf(percept.objectId)}`;
    case "inventory":
      return `inventory${percept.window === "Update" ? "" : ` (${percept.window})`}\t${percept.slots.map(({ slot, item }) => `${slot}: ${item ? describeItem(item) : "empty"}`).join(", ") || "nothing"}`;
    case "encumbrance":
      return `encumbrance\t${percept.weight} of ${percept.capacity} lb`;
    case "money":
      return `money\t${inventory.money} copper`;
    case "examined":
      return `examined ${percept.caption}\t${percept.lines.join(" | ")}`;
    case "merchantWindow":
      return `merchantWindow${inventory.merchant?.objectId === undefined ? "" : ` #${inventory.merchant.objectId} ${nameOf(inventory.merchant.objectId)}`}\t${percept.items.map((sold) => `${merchantIndex(sold)}: ${sold.name} level ${sold.level} ${sold.price} copper${sold.usable ? "" : " (unusable)"}`).join(", ") || "nothing"}`;
    case "dialog":
      return `dialog ${percept.code} (${percept.yesNo ? "yes or no" : "OK"})\t${percept.text}`;
    case "stats":
      return `stats\tconstitution lost ${percept.constitutionLost} max health ${percept.maxHealth}`;
    case "targetChanged":
      return `targetChanged #${percept.objectId} ${nameOf(percept.objectId)}`;
    case "groupWindow":
      return `groupWindow\t${percept.members.map((member) => `#${member.objectId} ${member.name} (level ${member.level} ${member.className})`).join(", ") || "no one"}`;
    case "groupStatus":
      return `groupStatus\t${percept.members
        .map((member) => {
          const flags = GROUP_MEMBER_FLAGS.filter((flag) => member[flag]);
          const doing = member.inAnotherRegion ? "in another region" : [`health ${member.healthPercent}% power ${member.powerPercent}% endurance ${member.endurancePercent}%`, ...flags].join(" ");
          return `#${member.objectId} ${nameOf(member.objectId)} ${doing}`;
        })
        .join(", ")}`;
    case "groupPositions":
      return `groupPositions\t${percept.members.map((member) => `#${member.objectId} ${nameOf(member.objectId)} ${member.x === undefined || member.y === undefined ? "in another region" : `zone ${member.zone} X:${member.x} Y:${member.y}`}`).join(", ")}`;
  }
}

/** A Primitive's line, before it's sent. */
export function describePrimitive(agent: Agent, character: string, primitive: Primitive): string {
  const { inventory } = agent;

  switch (primitive.name) {
    case "moveTo":
      return primitive.within === undefined ? `moveTo ${at(primitive)}` : `moveTo ${at(primitive)} within ${primitive.within}`;
    case "say":
      return `say "${primitive.text}"`;
    case "face":
      return `face heading ${primitive.heading}`;
    case "target":
      return `target #${primitive.objectId} ${nameIn(agent, character, primitive.objectId)}`;
    case "attack":
      return `attack ${primitive.on ? "on" : "off"}`;
    case "command":
      return `command ${primitive.text}`;
    case "moveItem":
      return `moveItem ${primitive.from} (${inventory.item(primitive.from)?.name ?? "empty"}) to ${primitive.to}`;
    case "examine":
    case "sell":
      return `${primitive.name} ${primitive.slot} (${inventory.item(primitive.slot)?.name ?? "empty"})`;
    case "examineMerchantItem":
      return `examineMerchantItem ${primitive.index} (${merchantItemName(agent, primitive.index)})`;
    case "buy":
      return `buy ${primitive.count} of ${primitive.index} (${merchantItemName(agent, primitive.index)})`;
    case "pickUp":
    case "talkTo":
      return `${primitive.name} #${primitive.objectId} ${nameIn(agent, character, primitive.objectId)}`;
    case "answerDialog":
      return `answerDialog ${primitive.code} ${primitive.accept ? "accept" : "decline"}`;
    case "castSpell":
      return `castSpell ${primitive.spell}`;
    case "useAbility":
      return `useAbility ${primitive.ability}`;
    case "commandPet":
      return `commandPet ${primitive.order}`;
    case "cancelConcentration":
      return primitive.target === undefined ? `cancelConcentration ${primitive.spell}` : `cancelConcentration ${primitive.spell} on ${primitive.target}`;
    default:
      return primitive.name;
  }
}

// The Agent's own Character isn't in its Surroundings, and a Group member far away is only in its Group.
function nameIn({ self, surroundings, group }: Agent, character: string, objectId: number): string {
  return objectId === self.objectId ? character : (surroundings.object(objectId)?.name ?? group.member(objectId)?.name ?? "(unknown)");
}

function describeItem(item: InventoryItem): string {
  return `${item.name} level ${item.level}${item.dpsAf ? ` dps/af ${item.dpsAf}` : ""}`;
}

function describeEffect(effect: EffectEntry): string {
  const description = effect.details ? `: ${effect.details.description.trim().replaceAll("\n", " ")}` : "";
  return `${effect.name} (${effect.positive ? "buff" : "debuff"}${effect.secondsLeft === undefined ? "" : `, ${effect.secondsLeft} s`}${effect.disabled ? ", disabled" : ""})${description}`;
}

function merchantItemName({ inventory }: Agent, index: number): string {
  return inventory.merchant?.items.find((sold) => merchantIndex(sold) === index)?.name ?? "unknown";
}

/** As `/gloc` prints a position, in whole units. */
export function at({ x, y, z }: Position): string {
  return `X:${Math.round(x)} Y:${Math.round(y)} Z:${Math.round(z)}`;
}
