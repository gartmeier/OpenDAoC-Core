import { merchantIndex, SLOT, type SlotName } from "../inventory.ts";
import type { InventoryItem, MerchantItem } from "../protocol.ts";
import { type Agent, failed, type Outcome, succeeded } from "../tactic.ts";
import { equip } from "./equip.ts";
import { buy, sell, talkTo } from "./trade.ts";

// The stand-in rules: deliberately simple, for the coding agent to replace in milestone 5.

/** Connla's merchants, by name, as a Human remembers them: Eira sells blades and Erskine leather armour. */
export const BLADESMITH = "Eira";
export const LEATHERWORKER = "Erskine";

/** Object types, as the server's `eObjectType` numbers them. Static knowledge, as the client has it. */
export const BLADES = 19;
export const LEATHER = 33;

/** An item's `hand` when it takes both hands. */
const TWO_HANDED = 1;

/** The armour slot a leather piece goes in, by the last word of its name. */
export const ARMOUR_PIECES: Readonly<Record<string, SlotName>> = {
  helm: "head",
  gloves: "hands",
  boots: "feet",
  jerkin: "torso",
  leggings: "legs",
  sleeves: "arms",
};

/**
 * Gears the Agent up in Connla. It sells everything in the backpack to Erskine. Then, if the right hand holds no blade at least the Agent's level,
 * it buys from Eira the lowest-level blade at least its level that its class can use with one hand and that it can afford, and puts it in the right hand.
 * Then, for each armour slot whose piece is missing or below its level, it buys from Erskine the cheapest leather piece for that slot at least its level, cheapest slots first, as far as the money goes, and puts each on.
 * It succeeds even when it could afford nothing. It fails with `outOfView` when either merchant isn't in view, as `talkTo` does when it can't talk to one, and with `attacked` before its next trade once something attacks the Agent.
 * A sale, purchase or equip that fails is logged and skipped.
 */
export async function shop(agent: Agent): Promise<Outcome> {
  const { self, surroundings, inventory } = agent;
  const bladesmith = surroundings.named(BLADESMITH);
  const leatherworker = surroundings.named(LEATHERWORKER);

  if (bladesmith?.kind !== "npc" || leatherworker?.kind !== "npc")
    return failed("outOfView");

  const attacked = () => self.objectId !== undefined && surroundings.attacking(self.objectId).length > 0;

  for (const { slot, item } of inventory.backpack) {
    if (attacked())
      return failed("attacked");

    const sold = await sell(agent, leatherworker.objectId, slot);

    if (!sold.ok && sold.reason !== "notSold")
      return sold;

    if (!sold.ok)
      agent.log(`${LEATHERWORKER} didn't buy the ${item.name}.`);
  }

  if (attacked())
    return failed("attacked");

  if (wantsReplacing(inventory.equipped("rightHand"), self.level, BLADES)) {
    const talked = await talkTo(agent, bladesmith.objectId);

    if (!talked.ok)
      return talked;

    const blade = (inventory.merchant?.items ?? [])
      .filter((sold) => sold.objectType === BLADES && sold.hand !== TWO_HANDED && sold.usable && sold.level >= self.level && sold.price <= inventory.money)
      .sort((a, b) => a.level - b.level || a.price - b.price)[0];

    if (blade)
      await buyAndEquip(agent, bladesmith.objectId, blade, "rightHand");
  }

  const outgrown = Object.values(ARMOUR_PIECES).filter((slot) => wantsReplacing(inventory.equipped(slot), self.level));

  if (outgrown.length > 0) {
    const talked = await talkTo(agent, leatherworker.objectId);

    if (!talked.ok)
      return talked;

    const pieces = outgrown
      .map((slot) => cheapestPiece(inventory.merchant?.items ?? [], slot, self.level))
      .filter((piece) => piece !== undefined)
      .sort((a, b) => a.price - b.price);

    for (const piece of pieces) {
      if (attacked())
        return failed("attacked");

      if (piece.price > inventory.money)
        break;

      await buyAndEquip(agent, leatherworker.objectId, piece, ARMOUR_PIECES[lastWord(piece.name)]!);
    }
  }

  return succeeded;
}

/** Whether the piece in a slot wants replacing: there's none, it's below `level`, or it isn't of `objectType`. */
function wantsReplacing(worn: InventoryItem | undefined, level: number, objectType?: number): boolean {
  return !worn || worn.level < level || (objectType !== undefined && worn.objectType !== objectType);
}

function cheapestPiece(items: readonly MerchantItem[], slot: SlotName, level: number): MerchantItem | undefined {
  return items
    .filter((sold) => sold.objectType === LEATHER && sold.usable && sold.level >= level && ARMOUR_PIECES[lastWord(sold.name)] === slot)
    .sort((a, b) => a.price - b.price)[0];
}

function lastWord(name: string): string {
  return name.trim().split(/\s+/).at(-1)!.toLowerCase();
}

/** Buys the item and moves it from wherever it landed to `slot`. */
async function buyAndEquip(agent: Agent, merchantId: number, sold: MerchantItem, slot: SlotName): Promise<void> {
  const { inventory } = agent;
  const before = new Set(inventory.backpack.map(({ slot }) => slot));
  const bought = await buy(agent, merchantId, merchantIndex(sold));

  if (!bought.ok) {
    agent.log(`Couldn't buy the ${sold.name}: ${bought.reason}.`);
    return;
  }

  const landed = inventory.backpack.find(({ slot, item }) => !before.has(slot) && item.name === sold.name) ?? inventory.backpack.find(({ item }) => item.name === sold.name);
  const equipped = landed ? await equip(agent, landed.slot, SLOT[slot]) : failed("notEquipped");

  if (!equipped.ok)
    agent.log(`Couldn't equip the ${sold.name}: ${equipped.reason}.`);
}
