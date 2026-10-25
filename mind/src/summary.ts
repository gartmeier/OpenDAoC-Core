import { BACKPACK_SLOTS, SLOT, type SlotName } from "./inventory.ts";
import { at } from "./journal.ts";
import type { MovementStatus } from "./self.ts";
import { distance, type KnownObject } from "./surroundings.ts";
import type { Agent } from "./tactic.ts";

/** How many of the nearest objects the summary lists. */
export const NEAR_LISTED = 10;

/** What the Agent believes about itself, what's near it and what it carries, in a few lines, as every `execute_code` result ends. */
export function summarize({ self, surroundings, inventory }: Agent, character: string): string {
  const position = self.position;
  const where = position ? `at ${at(position)}` : "somewhere the server hasn't said yet";
  const specPoints = self.specPoints === 1 ? "1 spec point" : `${self.specPoints} spec points`;
  const near = position
    ? surroundings.objects.map((object) => ({ object, away: distance(position, object.position) })).sort((a, b) => a.away - b.away)
    : surroundings.objects.map((object) => ({ object, away: undefined }));
  const nearHeading = near.length === 0 ? "Near: nothing in view." : near.length > NEAR_LISTED ? `Near (nearest ${NEAR_LISTED} of ${near.length}):` : `Near (${near.length}):`;
  const equipped = (Object.keys(SLOT) as SlotName[]).flatMap((slot) => {
    const item = inventory.equipped(slot);
    return item ? [`${slot} ${item.name} (level ${item.level})`] : [];
  });

  return [
    `${character}, level ${self.level} ${self.className}, ${self.levelPermill}‰ of the way to level ${self.level + 1}, ${specPoints}.`,
    `Health ${self.healthPercent}% (${self.health}/${self.maxHealth}), endurance ${self.endurancePercent}%, ${self.dead ? "dead" : self.sitting ? "sitting" : describeMovement(self.movement)} ${where}.`,
    nearHeading,
    ...near.slice(0, NEAR_LISTED).map(({ object, away }) => `  ${describeObject(object, self.objectId)}${away === undefined ? "" : `, ${Math.round(away)} away`}`),
    `Equipped: ${equipped.join("; ") || "nothing"}.`,
    `Backpack: ${BACKPACK_SLOTS - inventory.freeBackpackSlots} of ${BACKPACK_SLOTS} slots used. Money: ${inventory.money} copper. Weight: ${inventory.weight} of ${inventory.capacity} lb.`,
  ].join("\n");
}

function describeMovement(movement: MovementStatus): string {
  switch (movement.kind) {
    case "standing":
      return "standing";
    case "moving":
      return `running to ${at(movement.target)}, now`;
    case "arrived":
      return "arrived, standing";
    case "stopped":
      return `stopped short (${movement.reason}), standing`;
  }
}

function describeObject(object: KnownObject, selfId: number | undefined): string {
  if (object.kind === "player")
    return `#${object.objectId} ${object.name}, a player`;

  if (object.kind === "object")
    return `#${object.objectId} ${object.name}, a World object`;

  const flags = [object.peaceful ? "peaceful" : "", object.realm === "None" ? "" : `realm ${object.realm}`, object.targetId !== 0 && object.targetId === selfId ? "attacking you" : ""].filter(Boolean);
  return `#${object.objectId} ${object.name}, level ${object.level} NPC${flags.length ? ` (${flags.join(", ")})` : ""}, health ${object.healthPercent}%`;
}
