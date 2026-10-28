import { type Agent, type Outcome, succeeded } from "../../../mind/src/tactic.ts";
import { sell, talkTo } from "../../../mind/src/tactics/trade.ts";
import { runTo } from "../../../mind/src/tactics/run-to.ts";

// Slots for jewellery, cloak and belt: an item for one I have empty is worth wearing, whatever it is.
const JEWELLERY = [24, 26, 29, 32, 33, 34, 35, 36];
const CLOTH = 32;

/**
 * What in my backpack I'd rather keep: the Recall Stone, anything for an empty jewellery, cloak or belt slot,
 * and cloth armour with a higher armour factor than what I wear there. (I sold a Thrustbender Belt and a
 * Matterbender Choker with those slots empty.)
 */
export const keep = (agent: Agent) =>
  agent.inventory.backpack.filter(({ item }) => {
    if (item.name.includes("Recall")) return true;
    const worn = agent.inventory.item(item.itemType);
    if (JEWELLERY.includes(item.itemType)) return !worn;
    if (item.objectType === CLOTH) return !worn || item.dpsAf > worn.dpsAf;
    // A Focus Staff of a higher level than mine may focus more levels: I sold a +21 Insightful one while wearing a +20.
    if (/Focus Staff/.test(item.name)) return item.level > (agent.inventory.item(12)?.level ?? 0);
    return false;
  });

/** Runs to the merchant and sells everything in the backpack but what `keep` keeps. */
export async function sellJunk(agent: Agent, merchant: string): Promise<Outcome & { sold: string[]; kept: string[] }> {
  const npc = agent.surroundings.npcs.find((n) => n.name === merchant);
  if (!npc) return { ok: false, reason: "noMerchant", sold: [], kept: [] };
  await runTo(agent, npc.position, 150);
  await talkTo(agent, npc.objectId);
  const kept = new Set(keep(agent).map((b) => b.slot));
  const sold: string[] = [];
  for (const b of [...agent.inventory.backpack]) {
    if (kept.has(b.slot)) continue;
    const r = await sell(agent, npc.objectId, b.slot);
    sold.push(`${b.item.name}${r.ok ? "" : `: ${r.reason}`}`);
  }
  return { ...succeeded, sold, kept: keep(agent).map((b) => b.item.name) };
}
