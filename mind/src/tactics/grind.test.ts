import { expect, test } from "bun:test";
import type { Percept } from "../protocol.ts";
import { settle, TestAgent } from "../test-agent.ts";
import { BACKPACK_SLOTS, SLOT } from "../inventory.ts";
import { arrived, died, encumbrance, inventory, item, merchantWindow, money, npcCreate, objectRemove, objectUpdate, ownPosition, player, points, revived, skills, spec, status, stopped } from "../test-percepts.ts";
import { grind, LEASH } from "./grind.ts";
import { BLADES } from "./shop.ts";
import { TRADE_TIMEOUT_MS } from "./trade.ts";

const HOME = { x: 296000, y: 642000, z: 4848 };

// A level 2 Greeen (object ID 17) at home with full health, Blades 1 and no spec points, and whatever else is around. Level 1 Mobs, the default, are blue to him.
async function grinding(around: Percept[] = [], random: number[] = []) {
  const t = new TestAgent(random);
  await t.perceive(ownPosition(HOME.x, HOME.y, HOME.z), player(2), status(100), points(0), skills(spec("Blades", 1)), ...around);
  const outcome = t.agent.run(grind);
  await settle();
  return { t, outcome };
}

function targeted(t: TestAgent): number | undefined {
  const target = t.take().find((primitive) => primitive.name === "target");
  return target?.name === "target" ? target.objectId : undefined;
}

test("goes for the nearest Mob it can take on", async () => {
  const { t } = await grinding([npcCreate(0x101, "far wolf", HOME.x + 900, HOME.y, HOME.z), npcCreate(0x102, "near wolf", HOME.x + 300, HOME.y, HOME.z)]);

  expect(targeted(t)).toBe(0x102);
});

test("leaves alone what it shouldn't fight: peaceful, pets, other realms, the untargetable, Mobs that aren't blue to it, Mobs fighting someone else, and Mobs outside the leash", async () => {
  const { t } = await grinding([
    npcCreate(0x101, "merchant", HOME.x + 100, HOME.y, HOME.z, { peaceful: true }),
    npcCreate(0x102, "pet", HOME.x + 110, HOME.y, HOME.z, { pet: true }),
    npcCreate(0x103, "guard", HOME.x + 120, HOME.y, HOME.z, { realm: "Hibernia" }),
    npcCreate(0x104, "statue", HOME.x + 130, HOME.y, HOME.z, { untargetable: true }),
    npcCreate(0x105, "bear", HOME.x + 140, HOME.y, HOME.z, { level: 3 }),
    npcCreate(0x109, "boar", HOME.x + 141, HOME.y, HOME.z, { level: 2 }),
    npcCreate(0x10a, "rat", HOME.x + 142, HOME.y, HOME.z, { level: 0 }),
    npcCreate(0x106, "busy wolf", HOME.x + 150, HOME.y, HOME.z),
    objectUpdate(0x106, HOME.x + 150, HOME.y, HOME.z, { targetId: 999 }),
    npcCreate(0x107, "distant wolf", HOME.x + LEASH + 10, HOME.y, HOME.z),
    npcCreate(0x108, "wolf", HOME.x + 1000, HOME.y, HOME.z),
  ]);

  expect(targeted(t)).toBe(0x108);
});

test("fights back first against a Mob attacking it, however far and whatever it is", async () => {
  const { t } = await grinding([
    npcCreate(0x101, "near wolf", HOME.x + 100, HOME.y, HOME.z),
    npcCreate(0x102, "angry bear", HOME.x + 120, HOME.y, HOME.z, { level: 4 }),
    objectUpdate(0x102, HOME.x + 120, HOME.y, HOME.z, { targetId: 17 }),
  ]);

  expect(targeted(t)).toBe(0x102);
});

test("rests when its health is low and nothing attacks it", async () => {
  const { t } = await grinding([status(40)]);

  expect(t.take()).toEqual([{ name: "sit" }]);
});

test("rests below 80% health, and only stands at full health", async () => {
  const { t } = await grinding([status(79)]);
  expect(t.take()).toEqual([{ name: "sit" }]);

  await t.perceive(status(99, { sitting: true }));
  expect(t.take()).toEqual([]);

  await t.perceive(status(100, { sitting: true }));
  expect(t.take()[0]).toEqual({ name: "stand" });
});

test("doesn't rest at 80% health", async () => {
  const { t } = await grinding([status(80)], [0, 0]);

  expect(t.take().map((primitive) => primitive.name)).toEqual(["moveTo"]);
});

test("trains its spec points into Blades as far as they go", async () => {
  const { t } = await grinding([points(5)]);

  expect(t.take()).toEqual([{ name: "command", text: "/train Blades 3" }]);
});

test("doesn't try the same training again once it failed", async () => {
  const { t } = await grinding([points(2)], [0, 0]);
  t.take();

  await t.clock.advance(10_000);

  expect(t.take().map((primitive) => primitive.name)).toEqual(["moveTo"]);
});

test("releases when it dies, then runs home once it's alive again", async () => {
  const { t } = await grinding([npcCreate(0x101, "wolf", HOME.x + 100, HOME.y, HOME.z)]);
  t.take();

  await t.perceive(died(17, 0x101));
  expect(t.take()).toEqual([{ name: "attack", on: false }, { name: "command", text: "/release" }]);

  await t.perceive({ kind: "jump", x: 290000, y: 640000, z: 4800, objectId: 17, heading: 0, houseNumber: 0 }, revived(17));
  expect(t.take()).toEqual([{ name: "moveTo", ...HOME }]);
});

test("fails with homeUnreachable when it can't get home after dying", async () => {
  const { t, outcome } = await grinding();
  t.take();
  await t.perceive(died(17));
  expect(t.take()).toEqual([{ name: "stop" }, { name: "command", text: "/release" }]);
  await t.perceive(revived(17));

  await t.perceive(stopped(290000, 640000, 4800, "Unreachable"));

  expect(await outcome).toEqual({ ok: false, reason: "homeUnreachable" });
});

test("with nothing to fight, runs to a random spot near home, and stops there as soon as a Mob shows up", async () => {
  // Angle 0, and a quarter of the leash's area: half its radius.
  const { t } = await grinding([], [0, 0.25]);
  expect(t.take()).toEqual([{ name: "moveTo", x: HOME.x + LEASH / 2, y: HOME.y, z: HOME.z }]);

  await t.perceive(npcCreate(0x101, "wolf", HOME.x + 700, HOME.y, HOME.z));

  expect(t.take().slice(0, 2)).toEqual([{ name: "stop" }, { name: "target", objectId: 0x101 }]);
});

test("doesn't go for a Mob again once it couldn't get to it", async () => {
  const { t } = await grinding([npcCreate(0x101, "wolf on a roof", HOME.x + 300, HOME.y, HOME.z), npcCreate(0x102, "wolf", HOME.x + 600, HOME.y, HOME.z)]);
  expect(targeted(t)).toBe(0x101);

  await t.perceive(stopped(HOME.x + 100, HOME.y, HOME.z, "Unreachable"));

  expect(targeted(t)).toBe(0x102);
});

test("at level 5, says so, logs the summary and succeeds", async () => {
  const { t, outcome } = await grinding([npcCreate(0x101, "wolf", HOME.x + 300, HOME.y, HOME.z)]);
  t.take();
  await t.perceive(arrived(HOME.x + 220, HOME.y, HOME.z));
  await t.clock.advance(25_000);

  await t.perceive(player(5), objectRemove(0x101, 0));

  expect(await outcome).toEqual({ ok: true });
  expect(t.take().at(-1)).toEqual({ name: "say", text: "I'm level 5 now." });
  expect(t.lines.at(-1)).toBe("Reached level 5 in 0 min 25 s: 1 kills, 0 deaths, 0 rests, 0 shops. Tactic failures: none.");
});

test("ends as stopped when the Agent is stopped", async () => {
  const { t, outcome } = await grinding([npcCreate(0x101, "wolf", HOME.x + 300, HOME.y, HOME.z)]);

  t.agent.stop();

  expect(await outcome).toEqual({ ok: false, reason: "stopped" });
});

// Connla's merchants, a step from home.
const MERCHANTS = [
  npcCreate(0x201, "Eira", HOME.x + 50, HOME.y, HOME.z, { peaceful: true, realm: "Hibernia" }),
  npcCreate(0x202, "Erskine", HOME.x, HOME.y + 50, HOME.z, { peaceful: true, realm: "Hibernia" }),
];

test("shops after a level-up, then runs home", async () => {
  const { t } = await grinding([...MERCHANTS, money(0), inventory([SLOT.rightHand, item("training sword", { objectType: BLADES })])], [0, 0]);
  expect(t.take().map((primitive) => primitive.name)).toEqual(["moveTo"]);

  await t.perceive(player(3));
  expect(t.take()).toEqual([{ name: "stop" }, { name: "talkTo", objectId: 0x201 }]);

  await t.perceive(merchantWindow());
  expect(t.take()).toEqual([{ name: "talkTo", objectId: 0x202 }]);

  await t.perceive(merchantWindow());
  expect(t.take()).toEqual([{ name: "moveTo", ...HOME }]);
});

test("trains before it shops after a level-up", async () => {
  const { t } = await grinding([...MERCHANTS, money(0)], [0, 0]);
  t.take();

  await t.perceive(points(2), player(3));

  expect(t.take()).toEqual([{ name: "stop" }, { name: "command", text: "/train Blades 2" }]);
});

test("shops when it carries 80% of what it can", async () => {
  const { t } = await grinding([...MERCHANTS, money(0), inventory([40, item("bear claw")]), encumbrance(100, 79)], [0, 0]);
  t.take();

  await t.perceive(encumbrance(100, 80));

  expect(t.take()).toEqual([{ name: "stop" }, { name: "talkTo", objectId: 0x202 }]);
});

test("shops when its backpack is full", async () => {
  const full = inventory(...Array.from({ length: BACKPACK_SLOTS }, (_, i): [number, ReturnType<typeof item>] => [40 + i, item(`claw ${i}`)]));
  const { t } = await grinding([...MERCHANTS, money(0), full]);

  expect(t.take()).toEqual([{ name: "talkTo", objectId: 0x202 }]);
});

// The stand-in can't empty a backpack the merchant won't buy from, so it goes back to grinding until it has picked up more.
test("doesn't shop again for its load until it has picked up more", async () => {
  const { t } = await grinding([...MERCHANTS, money(0), inventory([SLOT.rightHand, item("falcata", { level: 2, objectType: BLADES })], [40, item("bear claw")]), encumbrance(100, 90)], [0, 0, 0, 0]);
  expect(t.take()).toEqual([{ name: "talkTo", objectId: 0x202 }]);
  await t.perceive(merchantWindow());
  expect(t.take()).toEqual([{ name: "sell", slot: 40 }]);
  await t.clock.advance(TRADE_TIMEOUT_MS);
  await t.perceive(arrived(HOME.x, HOME.y, HOME.z));
  expect(t.take().map((primitive) => primitive.name)).toEqual(["moveTo", "moveTo"]);

  await t.perceive(inventory([41, item("wolf pelt")]), encumbrance(100, 92));

  expect(t.take()).toEqual([{ name: "stop" }, { name: "sell", slot: 40 }]);
});
