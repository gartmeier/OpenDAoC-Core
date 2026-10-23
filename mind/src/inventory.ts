import type { InventoryItem, MerchantItem, PerceptMessage, Primitive } from "./protocol.ts";

/** The client's equipment slots by what they hold (`eInventorySlot` in the server). */
export const SLOT = {
  rightHand: 10,
  leftHand: 11,
  twoHanded: 12,
  ranged: 13,
  head: 21,
  hands: 22,
  feet: 23,
  jewel: 24,
  torso: 25,
  cloak: 26,
  legs: 27,
  arms: 28,
  neck: 29,
  waist: 32,
  leftWrist: 33,
  rightWrist: 34,
  leftRing: 35,
  rightRing: 36,
  mythical: 37,
} as const;

export type SlotName = keyof typeof SLOT;

/** The backpack's slots are 40 to 79. */
export const FIRST_BACKPACK_SLOT = 40;
export const BACKPACK_SLOTS = 40;

/** A merchant window's pages hold 30 items each. */
export const MERCHANT_PAGE_SIZE = 30;

/** Windows whose slots are another inventory's, such as a vault's. */
const OTHER_INVENTORIES = new Set(["PlayerVault", "HouseVault", "ConsignmentOwner", "ConsignmentViewer"]);

/** The last merchant window the Agent saw, and the merchant it talked to for it: the window's packet doesn't say. */
export interface MerchantWindow {
  readonly objectId: number | undefined;
  readonly items: readonly MerchantItem[];
}

export interface Examined {
  readonly caption: string;
  readonly lines: readonly string[];
}

/** An item and the slot it's in. */
export interface SlotItem {
  readonly slot: number;
  readonly item: InventoryItem;
}

/**
 * What the Agent currently believes it carries, built only from Percepts (see `CONTEXT.md`): what's in each equipment and backpack slot the server has told it about, its money, and its weight against its capacity.
 * It also keeps the last merchant window and the last examined text. Which merchant a window is from, and whether the client still targets it, is the one thing it learns from the Primitives sent, as a client knows what it clicked.
 */
export class Inventory {
  readonly #slots = new Map<number, InventoryItem>();
  #money = 0;
  #weight = 0;
  #capacity = 0;
  #merchant: MerchantWindow | undefined;
  #examined: Examined | undefined;
  #targetId: number | undefined;
  #talkingTo: number | undefined;

  perceive({ percept }: PerceptMessage): void {
    switch (percept.kind) {
      case "inventory":
        if (OTHER_INVENTORIES.has(percept.window))
          break;

        for (const { slot, item } of percept.slots) {
          if (item)
            this.#slots.set(slot, item);
          else
            this.#slots.delete(slot);
        }

        break;
      case "money":
        this.#money = percept.copper + percept.silver * 100 + percept.gold * 10_000 + percept.mithril * 10_000_000 + percept.platinum * 10_000_000_000;
        break;
      case "encumbrance":
        this.#weight = percept.weight;
        this.#capacity = percept.capacity;
        break;
      case "merchantWindow":
        this.#merchant = { objectId: this.#talkingTo, items: percept.items };
        break;
      case "examined":
        this.#examined = { caption: percept.caption, lines: percept.lines };
        break;
      // The client forgets its target once it can't see it.
      case "objectDelete":
      case "objectRemove":
        if (percept.objectId === this.#targetId)
          this.#targetId = undefined;

        break;
    }
  }

  /** Takes in a Primitive as it's sent: talking to an NPC, or picking something up, targets it as a click does. */
  sent(primitive: Primitive): void {
    switch (primitive.name) {
      case "talkTo":
        this.#targetId = primitive.objectId;
        this.#talkingTo = primitive.objectId;
        break;
      case "target":
      case "pickUp":
        this.#targetId = primitive.objectId || undefined;
        break;
    }
  }

  /** The item in the client slot, if any. */
  item(slot: number): InventoryItem | undefined {
    return this.#slots.get(slot);
  }

  equipped(slot: SlotName): InventoryItem | undefined {
    return this.item(SLOT[slot]);
  }

  /** What's in the backpack, in slot order. */
  get backpack(): readonly SlotItem[] {
    return [...this.#slots]
      .filter(([slot]) => isBackpackSlot(slot))
      .sort(([a], [b]) => a - b)
      .map(([slot, item]) => ({ slot, item }));
  }

  get freeBackpackSlots(): number {
    return BACKPACK_SLOTS - this.backpack.length;
  }

  get backpackFull(): boolean {
    return this.freeBackpackSlots === 0;
  }

  /** In copper. 0 until the server has said. */
  get money(): number {
    return this.#money;
  }

  /** In pounds. 0 until the server has said. */
  get weight(): number {
    return this.#weight;
  }

  /** In pounds: above it, the Character slows down. 0 until the server has said. */
  get capacity(): number {
    return this.#capacity;
  }

  get merchant(): MerchantWindow | undefined {
    return this.#merchant;
  }

  get examined(): Examined | undefined {
    return this.#examined;
  }

  /** Whether the last merchant window is this merchant's and the client still targets it, so that buying and selling go to it. */
  trading(merchantId: number): boolean {
    return this.#merchant?.objectId === merchantId && this.#targetId === merchantId;
  }
}

export function isBackpackSlot(slot: number): boolean {
  return slot >= FIRST_BACKPACK_SLOT && slot < FIRST_BACKPACK_SLOT + BACKPACK_SLOTS;
}

/** Where `buy` and `examineMerchantItem` find a merchant's item. */
export function merchantIndex(item: MerchantItem): number {
  return item.page * MERCHANT_PAGE_SIZE + item.place;
}
