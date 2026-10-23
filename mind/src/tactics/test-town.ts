// A stand-in for the server in Connla, for shopping tests: it answers each Primitive the Agent sends with the Percepts the server would send, until the Tactic ends.
import { BACKPACK_SLOTS, FIRST_BACKPACK_SLOT, merchantIndex } from "../inventory.ts";
import type { InventoryItem, MerchantItem, Percept, Primitive } from "../protocol.ts";
import type { Outcome } from "../tactic.ts";
import type { TestAgent } from "../test-agent.ts";
import { BLADES, LEATHER } from "./shop.ts";
import { arrived, inventory, item, merchantItem, merchantWindow, money, npcCreate, ownPosition, player } from "../test-percepts.ts";

export const EIRA = { objectId: 0x2c54, x: 294899, y: 641231, z: 4848 };
export const ERSKINE = { objectId: 0x2c55, x: 295981, y: 641115, z: 4848 };

/** Eira's blades, one per level, as the facts in the spec give them. */
export const BLADES_FOR_SALE: MerchantItem[] = [
  merchantItem(0, "short sword", 150, { level: 1, objectType: BLADES, hand: 2 }),
  merchantItem(1, "falcata", 250, { level: 2, objectType: BLADES, hand: 2 }),
  merchantItem(2, "broadsword", 350, { level: 3, objectType: BLADES }),
  merchantItem(3, "long sword", 400, { level: 4, objectType: BLADES }),
  merchantItem(4, "bastard sword", 450, { level: 5, objectType: BLADES }),
  merchantItem(5, "great falcata", 500, { level: 5, objectType: BLADES, hand: 1 }),
  merchantItem(6, "ferrite sword", 2400, { level: 6, objectType: BLADES }),
];

/** Erskine's rawhide and tanned leather. */
export const LEATHER_FOR_SALE: MerchantItem[] = [
  ["helm", 60],
  ["gloves", 50],
  ["boots", 55],
  ["jerkin", 90],
  ["leggings", 70],
  ["sleeves", 50],
].flatMap(([piece, price], i): MerchantItem[] => [
  merchantItem(i, `rawhide ${piece}`, price as number, { level: 2, objectType: LEATHER, dpsAf: 4 }),
  merchantItem(i, `tanned ${piece}`, (price as number) * 16, { page: 1, level: 5, objectType: LEATHER, dpsAf: 10 }),
]);

export class Town {
  readonly sent: Primitive[] = [];
  /** The names of what Greeen bought, in order. */
  readonly bought: string[] = [];
  readonly slots = new Map<number, InventoryItem>();
  money: number;
  /** What a sale pays. */
  salePrice = 10;
  readonly #windows = new Map<number, MerchantItem[]>([
    [EIRA.objectId, BLADES_FOR_SALE],
    [ERSKINE.objectId, LEATHER_FOR_SALE],
  ]);
  #trading: number | undefined;

  constructor(copper: number, slots: [number, InventoryItem][] = []) {
    this.money = copper;

    for (const [slot, carried] of slots)
      this.slots.set(slot, carried);
  }

  /** Greeen, at `level`, standing between the two merchants with what he carries. */
  welcome(level: number): Percept[] {
    return [
      ownPosition(295500, 641200, 4848),
      player(level),
      npcCreate(EIRA.objectId, "Eira", EIRA.x, EIRA.y, EIRA.z, { peaceful: true, realm: "Hibernia" }),
      npcCreate(ERSKINE.objectId, "Erskine", ERSKINE.x, ERSKINE.y, ERSKINE.z, { peaceful: true, realm: "Hibernia" }),
      money(this.money),
      inventory(...this.slots),
    ];
  }

  answer(primitive: Primitive): Percept[] {
    this.sent.push(primitive);

    switch (primitive.name) {
      case "moveTo":
        return [arrived(primitive.x + (primitive.within ?? 0), primitive.y, primitive.z)];
      case "talkTo":
        this.#trading = primitive.objectId;
        return [merchantWindow(...(this.#windows.get(primitive.objectId) ?? []))];
      case "sell": {
        if (!this.slots.delete(primitive.slot))
          return [];

        this.money += this.salePrice;
        return [inventory([primitive.slot]), money(this.money)];
      }
      case "buy": {
        const sold = this.#windows.get(this.#trading!)?.find((offered) => merchantIndex(offered) === primitive.index);
        const free = Array.from({ length: BACKPACK_SLOTS }, (_, i) => FIRST_BACKPACK_SLOT + i).find((slot) => !this.slots.has(slot));

        if (!sold || free === undefined || sold.price > this.money)
          return [];

        const bought = item(sold.name, { level: sold.level, objectType: sold.objectType, dpsAf: sold.dpsAf, hand: sold.hand });
        this.slots.set(free, bought);
        this.bought.push(sold.name);
        this.money -= sold.price;
        return [inventory([free, bought]), money(this.money)];
      }
      case "moveItem": {
        const moving = this.slots.get(primitive.from);
        const there = this.slots.get(primitive.to);
        this.#put(primitive.to, moving);
        this.#put(primitive.from, there);
        return [inventory([primitive.to, moving], [primitive.from, there])];
      }
      default:
        return [];
    }
  }

  #put(slot: number, carried: InventoryItem | undefined): void {
    if (carried)
      this.slots.set(slot, carried);
    else
      this.slots.delete(slot);
  }
}

/** Runs `running` to its end, answering every Primitive the Agent sends with what the town says. */
export async function inTown(t: TestAgent, town: Town, running: Promise<Outcome>): Promise<Outcome> {
  let outcome: Outcome | undefined;
  void running.then((ended) => (outcome = ended));

  while (outcome === undefined) {
    await t.perceive();
    const sent = t.take();

    for (const primitive of sent)
      await t.perceive(...town.answer(primitive));

    // Something the town doesn't answer is left to time out.
    if (sent.length === 0 && outcome === undefined)
      await t.clock.advance(1000);
  }

  return outcome;
}
