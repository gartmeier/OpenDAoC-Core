import { type Agent } from "../../../mind/src/tactic.ts";
import { sell } from "../../../mind/src/tactics/trade.ts";
import { equip } from "../../../mind/src/tactics/equip.ts";
import { SLOT } from "../../../mind/src/inventory.ts";

/** Never sold or dropped. */
const KEEP = ["Personal Bind Recall Stone"];
/** Merchants won't take these: drop them (moveItem to the ground, slot 1). */
const DROP = ["training shield", "training sword", "Blademaster Initiate Gloves", "Protector's Wrap"];

/**
 * Sells everything in the backpack but `keep` to the merchant `merchantName` (who must be in view), and drops what no merchant takes.
 * Returns one line per item. Look at drops before calling it: it sells upgrades too.
 */
export async function sellJunk(agent: Agent, merchantName: string, keep: string[] = [], log: string[] = []): Promise<string[]> {
  const merchant = agent.surroundings.npcs.find((n) => n.name === merchantName);
  if (!merchant) { log.push(`no ${merchantName} in view`); return log; }
  for (const { slot, item } of [...agent.inventory.backpack]) {
    if (KEEP.includes(item.name) || keep.includes(item.name)) continue;
    if (DROP.includes(item.name)) {
      await agent.sendAndWait({ name: "moveItem", from: slot, to: 1 }, () => !agent.inventory.item(slot), 3_000);
      log.push(`dropped ${item.name}`);
      continue;
    }
    const r = await sell(agent, merchant.objectId, slot);
    log.push(`${item.name}: ${r.ok ? "sold" : r.reason}`);
  }
  return log;
}

/** The armour slots: an armour piece's `itemType` is the slot it goes in. */
const ARMOUR: number[] = [SLOT.head, SLOT.hands, SLOT.feet, SLOT.torso, SLOT.legs, SLOT.arms];

/** The effective armour factor of the item in `slot`, from examining it, or 0. */
async function armourFactor(agent: Agent, slot: number): Promise<number> {
  if (!agent.inventory.item(slot)) return 0;
  await agent.send({ name: "examine", slot });
  await agent.waitFor(() => false, 800);
  const lines = agent.inventory.examined?.lines ?? [];
  const i = lines.findIndex((l) => /Effective Armor/.test(l));
  return Number(/(\d+) Factor/.exec(lines[i + 1] ?? "")?.[1] ?? 0);
}

/** Wears any armour piece in the backpack with a higher effective armour factor than what's worn in its slot. */
export async function equipBetterArmour(agent: Agent, log: string[] = []): Promise<string[]> {
  for (const { slot, item } of [...agent.inventory.backpack]) {
    const to = ARMOUR.includes(item.itemType) ? item.itemType : undefined;
    if (to === undefined) continue;
    const mine = await armourFactor(agent, slot);
    const worn = await armourFactor(agent, to);
    if (mine > worn) {
      const r = await equip(agent, slot, to);
      log.push(`wore ${item.name} (AF ${mine} over ${worn}): ${r.ok ? "ok" : r.reason}`);
    }
  }
  return log;
}
