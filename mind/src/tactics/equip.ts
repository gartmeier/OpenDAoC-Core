import { type Agent, failed, type Outcome, succeeded } from "../tactic.ts";

/** In milliseconds. The server answers a move with the slots it changed at once. */
export const EQUIP_TIMEOUT_MS = 10_000;

/**
 * Moves the item in slot `from` to the equipment slot `to`, as dragging it does, and succeeds once the Inventory shows it there. What was in `to` swaps into `from`.
 * It fails with `notEquipped` if it doesn't get there, for example because the Character can't use it, or there's nothing in `from`.
 */
export async function equip(agent: Agent, from: number, to: number): Promise<Outcome> {
  const { inventory } = agent;
  const item = inventory.item(from);

  if (!item)
    return failed("notEquipped");

  // The server sends both slots anew, so what was in `to` before, even an item of the same name, is never the moved item.
  const was = inventory.item(to);
  const there = await agent.sendAndWait({ name: "moveItem", from, to }, () => inventory.item(to) !== was && inventory.item(to)?.name === item.name, EQUIP_TIMEOUT_MS);
  return there ? succeeded : failed("notEquipped");
}
