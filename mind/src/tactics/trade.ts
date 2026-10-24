import { merchantIndex } from "../inventory.ts";
import { saidBy } from "../say-line.ts";
import { distance } from "../surroundings.ts";
import { type Agent, failed, type Outcome, succeeded } from "../tactic.ts";
import { runTo } from "./run-to.ts";

/** How close, in game units, a player must stand to talk to an NPC. Buying and selling allow 256. Static knowledge, as a Human learns it. */
export const TALK_RANGE = 192;

/** How far short of the NPC a run to it ends, well inside talking range. */
export const TALK_CLOSE_IN = 100;

/** In milliseconds. A merchant answers with its window, another NPC with its words, and a trade with the changed slots and money, at once. */
export const ANSWER_TIMEOUT_MS = 5_000;
export const TRADE_TIMEOUT_MS = 5_000;

/**
 * Gets within talking range of the NPC and talks to it, unless the Agent is trading in its window already, and succeeds once it answers: a merchant with its window, another NPC, such as a trainer, with its words.
 * It fails with `outOfView` (the NPC isn't in view), `tooFar` (the Agent can't get within reach) or `timeout` (no answer came).
 */
export async function talkTo(agent: Agent, npcId: number): Promise<Outcome> {
  const { self, surroundings, inventory } = agent;
  const npc = surroundings.npc(npcId);

  if (!npc)
    return failed("outOfView");

  const near = () => self.position !== undefined && distance(self.position, npc.position) <= TALK_RANGE;

  if (!near()) {
    const ran = await runTo(agent, npc.position, TALK_CLOSE_IN);

    if (!ran.ok || !near())
      return failed("tooFar");
  }

  if (inventory.trading(npcId))
    return succeeded;

  const answer = await agent.sendAndWait({ name: "talkTo", objectId: npcId }, (percept) => percept?.kind === "merchantWindow" || saidBy(npc.name, percept), ANSWER_TIMEOUT_MS);
  return answer ? succeeded : failed("timeout");
}

/**
 * Buys one of the item at `index` in the merchant's window (see `merchantIndex`), talking to the merchant first as `talkTo` does, and succeeds once a new item lands in the backpack and the money drops.
 * It fails as `talkTo` does, with `notEnoughMoney` or `noRoom` from what the Inventory says before buying, or with `notBought`, also when the window has no item there.
 */
export async function buy(agent: Agent, merchantId: number, index: number): Promise<Outcome> {
  const { inventory } = agent;
  const talked = await talkTo(agent, merchantId);

  if (!talked.ok)
    return talked;

  const item = inventory.merchant?.items.find((sold) => merchantIndex(sold) === index);

  if (!item)
    return failed("notBought");

  if (inventory.money < item.price)
    return failed("notEnoughMoney");

  if (inventory.backpackFull)
    return failed("noRoom");

  const before = new Map(inventory.backpack.map(({ slot, item }) => [slot, item]));
  const money = inventory.money;
  const landed = () => inventory.backpack.some(({ slot, item }) => before.get(slot) !== item);
  const bought = await agent.sendAndWait({ name: "buy", index, count: 1 }, () => landed() && inventory.money < money, TRADE_TIMEOUT_MS);
  return bought ? succeeded : failed("notBought");
}

/**
 * Sells the item in backpack slot `slot` to the merchant, talking to it first as `talkTo` does, and succeeds once the slot empties and the money rises.
 * It fails as `talkTo` does, or with `notSold`, also when there's nothing in the slot.
 */
export async function sell(agent: Agent, merchantId: number, slot: number): Promise<Outcome> {
  const { inventory } = agent;

  if (!inventory.item(slot))
    return failed("notSold");

  const talked = await talkTo(agent, merchantId);

  if (!talked.ok)
    return talked;

  const money = inventory.money;
  const sold = await agent.sendAndWait({ name: "sell", slot }, () => !inventory.item(slot) && inventory.money > money, TRADE_TIMEOUT_MS);
  return sold ? succeeded : failed("notSold");
}
