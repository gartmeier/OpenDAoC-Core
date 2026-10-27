import { expect, test } from "bun:test";
import type { Percept } from "../protocol.ts";
import { settle, TestAgent } from "../test-agent.ts";
import { castStarted, cooldowns, effects, gameMessage, interrupted, npcCreate, ownPosition, skills, spell, spellLanded, status } from "../test-percepts.ts";
import { cast, START_TIMEOUT_MS } from "./cast.ts";

const ME = 17;
const MOB = 0x1f0b;

/** Orla at full Power, with a bolt, a heal, a buff and Quick Cast, and a Mob in view, casting `name` at `targetId`. */
async function casting(name = "Gleam Ray", targetId = MOB, options: { quickCast?: boolean } = {}, ...percepts: Percept[]) {
  const t = new TestAgent();
  await t.perceive(
    ownPosition(296000, 642000, 4848),
    status(100, { power: 20, maxPower: 20, powerPercent: 100 }),
    skills(
      spell("Gleam Ray"),
      spell("Minor Revigoration", { target: "Realm", description: "Heals 20 health." }),
      spell("Lesser Powerward", { target: "SELF", castingSeconds: 0, description: "Buffs armor." }),
      { page: "Abilities", name: "Quickcast", level: 1 },
    ),
    npcCreate(MOB, "wolf pup", 296500, 642000, 4848),
    ...percepts,
  );
  const outcome = t.agent.run(cast, name, targetId, options);
  await settle();
  return { t, outcome };
}

test("targets and casts, and succeeds once its own start and landing come", async () => {
  const { t, outcome } = await casting();
  expect(t.take()).toEqual([{ name: "target", objectId: MOB }, { name: "castSpell", spell: "Gleam Ray" }]);

  await t.perceive(castStarted(ME, 2500));
  await t.clock.advance(2500);
  await t.perceive(spellLanded(ME, MOB));

  expect(await outcome).toEqual({ ok: true });
});

test("succeeds on an instant harmful Spell's start of 0 and its landing", async () => {
  const { t, outcome } = await casting();

  await t.perceive(castStarted(ME, 0), spellLanded(ME, MOB));

  expect(await outcome).toEqual({ ok: true });
  expect(t.take().at(-1)).toEqual({ name: "castSpell", spell: "Gleam Ray" });
});

test("succeeds on an instant friendly Spell's landing alone, which the server sends no start for", async () => {
  const { t, outcome } = await casting("Lesser Powerward", ME);

  await t.perceive(spellLanded(ME, ME));

  expect(await outcome).toEqual({ ok: true });
});

test("fails with noEffect when the Spell lands but doesn't take", async () => {
  const { t, outcome } = await casting();

  await t.perceive(castStarted(ME, 2500), spellLanded(ME, MOB, false));

  expect(await outcome).toEqual({ ok: false, reason: "noEffect" });
});

test("fails with noEffect when the target resists, from the landing rather than the resist line", async () => {
  const { t, outcome } = await casting();

  await t.perceive(castStarted(ME, 2500), gameMessage("The wolf pup resists the effect! (12.0%)"), spellLanded(ME, MOB, false));

  expect(await outcome).toEqual({ ok: false, reason: "noEffect" });
});

test("fails with interrupted when its own cast is interrupted, and not when another caster's is", async () => {
  const { t, outcome } = await casting();

  await t.perceive(castStarted(ME, 2500), interrupted(MOB), spellLanded(MOB, ME));
  await t.perceive(interrupted(ME));

  expect(await outcome).toEqual({ ok: false, reason: "interrupted" });
});

// The server sends the hit's line, as a refusal, just before the interrupt itself.
test("fails with interrupted, not refused, on the line a hit sends as it interrupts the cast", async () => {
  const { t, outcome } = await casting();

  await t.perceive(castStarted(ME, 2500), gameMessage("The feccan attacks you and your spell is interrupted!"), interrupted(ME));

  expect(await outcome).toEqual({ ok: false, reason: "interrupted" });
});

test("waits out \"You must wait\" and casts again, then succeeds", async () => {
  const { t, outcome } = await casting();
  t.take();

  await t.perceive(gameMessage("You must wait 2 seconds to cast a spell!"));
  await t.clock.advance(2299);
  expect(t.take()).toEqual([]);

  await t.clock.advance(1);
  expect(t.take()).toEqual([{ name: "castSpell", spell: "Gleam Ray" }]);

  await t.perceive(castStarted(ME, 2500), spellLanded(ME, MOB));
  expect(await outcome).toEqual({ ok: true });
});

test("waits out a Cooldown's and a potion's wait the same way", async () => {
  const { t, outcome } = await casting();
  t.take();

  await t.perceive(gameMessage("You must wait 1 second to recast this type of spell!"));
  await t.clock.advance(1300);
  await t.perceive(gameMessage("You need to wait 3 more seconds before casting a spell!", "CT_System"));
  await t.clock.advance(3300);

  expect(t.take()).toEqual([{ name: "castSpell", spell: "Gleam Ray" }, { name: "castSpell", spell: "Gleam Ray" }]);
  await t.perceive(spellLanded(ME, MOB));
  expect(await outcome).toEqual({ ok: true });
});

test("fails with mustWait once the server says to wait three times", async () => {
  const { t, outcome } = await casting();

  for (let i = 0; i < 3; i++) {
    await t.perceive(gameMessage("You must wait 2 seconds to cast a spell!"));
    await t.clock.advance(2300);
  }

  expect(await outcome).toEqual({ ok: false, reason: "mustWait" });
  expect(t.take().filter((primitive) => primitive.name === "castSpell")).toHaveLength(3);
});

for (const [line, reason] of [
  ["That target is too far away!", "tooFar"],
  ["You can't see your target from here!", "notInView"],
  ["Your target is not visible!", "notInView"],
  ["You must select a target for this spell!", "noTarget"],
  ["You don't have enough power to cast that!", "noPower"],
] as const) {
  test(`fails with ${reason} on "${line}", before the cast starts or as it ends`, async () => {
    const before = await casting();
    await before.t.perceive(gameMessage(line));
    expect(await before.outcome).toEqual({ ok: false, reason });

    const ending = await casting();
    await ending.t.perceive(castStarted(ME, 2500));
    await ending.t.clock.advance(2500);
    await ending.t.perceive(gameMessage(line));
    expect(await ending.outcome).toEqual({ ok: false, reason });
  });
}

test("fails with refused on any other refusal, and logs its words", async () => {
  const { t, outcome } = await casting();

  await t.perceive(gameMessage("You are already casting a spell!"));

  expect(await outcome).toEqual({ ok: false, reason: "refused" });
  expect(t.lines).toContain("The server refused Gleam Ray: You are already casting a spell!");
});

test("takes no other chat for an answer, nor resisting another caster's Spell", async () => {
  const { t, outcome } = await casting();

  await t.perceive(gameMessage("The wolf pup attacks you!", "CT_Damaged"), gameMessage("You resist the effect!"), gameMessage("Your wolf sage resists the effect!"), castStarted(ME, 2500), spellLanded(ME, MOB));

  expect(await outcome).toEqual({ ok: true });
});

test("fails with timeout when the server doesn't answer at all", async () => {
  const { t, outcome } = await casting();

  await t.clock.advance(START_TIMEOUT_MS);

  expect(await outcome).toEqual({ ok: false, reason: "timeout" });
});

test("waits for a started cast's whole casting time and a margin before it fails with timeout", async () => {
  const { t, outcome } = await casting();
  await t.perceive(castStarted(ME, 6000));

  await t.clock.advance(7000);
  await t.perceive(spellLanded(ME, MOB));
  expect(await outcome).toEqual({ ok: true });

  const silent = await casting();
  await silent.t.perceive(castStarted(ME, 6000));
  await silent.t.clock.advance(8000);
  expect(await silent.outcome).toEqual({ ok: false, reason: "timeout" });
});

test("fails with noSpell, sending nothing, for a Spell it doesn't have", async () => {
  const { t, outcome } = await casting("Gleam Rey");

  expect(await outcome).toEqual({ ok: false, reason: "noSpell" });
  expect(t.take()).toEqual([]);
});

test("fails with cooldown, sending nothing, for a Spell on Cooldown", async () => {
  const { t, outcome } = await casting("Gleam Ray", MOB, {}, cooldowns({ "Gleam Ray": 10 }));

  expect(await outcome).toEqual({ ok: false, reason: "cooldown" });
  expect(t.take()).toEqual([]);
});

test("fails with noPower, sending nothing, when the Spell costs more Power than it has", async () => {
  const { t, outcome } = await casting("Gleam Ray", MOB, {}, status(100, { power: 1, maxPower: 20, powerPercent: 5 }));

  expect(await outcome).toEqual({ ok: false, reason: "noPower" });
  expect(t.take()).toEqual([]);
});

test("fails with wrongTarget, sending nothing, for a friendly Spell at a Mob", async () => {
  const { t, outcome } = await casting("Minor Revigoration", MOB);

  expect(await outcome).toEqual({ ok: false, reason: "wrongTarget" });
  expect(t.take()).toEqual([]);
});

const SITTING = status(100, { power: 20, maxPower: 20, powerPercent: 100, sitting: true });
const STANDING = status(100, { power: 20, maxPower: 20, powerPercent: 100 });

test("stands up first when sitting, and casts once the server says it stands", async () => {
  const { t } = await casting("Minor Revigoration", ME, {}, SITTING);
  expect(t.take()).toEqual([{ name: "stand" }]);

  await t.perceive(STANDING);

  expect(t.take()).toEqual([{ name: "target", objectId: ME }, { name: "castSpell", spell: "Minor Revigoration" }]);
});

// The server drops a /stand within 1.5 s of the /sit before it, without a word.
test("stands again when the server drops the first stand, and fails with sitting when it drops the second", async () => {
  const { t, outcome } = await casting("Minor Revigoration", ME, {}, SITTING);
  expect(t.take()).toEqual([{ name: "stand" }]);

  await t.clock.advance(1799);
  expect(t.take()).toEqual([]);
  await t.clock.advance(1);
  expect(t.take()).toEqual([{ name: "stand" }]);

  await t.clock.advance(1800);
  expect(await outcome).toEqual({ ok: false, reason: "sitting" });
  expect(t.take()).toEqual([]);
});

test("stops a run in progress first", async () => {
  const { t } = await casting("Gleam Ray", MOB, {}, { kind: "moved", x: 296000, y: 642000, z: 4848, heading: 0, speed: 191, targetX: 297000, targetY: 642000, targetZ: 4848 });

  expect(t.take()).toEqual([{ name: "stop" }, { name: "target", objectId: MOB }, { name: "castSpell", spell: "Gleam Ray" }]);
});

test("with quickCast, turns attack off and uses Quick Cast, then casts once its Effect is up", async () => {
  const { t, outcome } = await casting("Gleam Ray", MOB, { quickCast: true });
  expect(t.take()).toEqual([{ name: "target", objectId: MOB }, { name: "attack", on: false }, { name: "useAbility", ability: "Quickcast" }]);

  await t.perceive(effects({ name: "Quick Cast", positive: true }));
  expect(t.take()).toEqual([{ name: "castSpell", spell: "Gleam Ray" }]);

  await t.perceive(castStarted(ME, 2500), spellLanded(ME, MOB));
  expect(await outcome).toEqual({ ok: true });
});

test("with quickCast, doesn't use Quick Cast again while it's already up, which would turn it off", async () => {
  const { t } = await casting("Gleam Ray", MOB, { quickCast: true }, effects({ name: "Quick Cast", positive: true }));

  expect(t.take()).toEqual([{ name: "target", objectId: MOB }, { name: "castSpell", spell: "Gleam Ray" }]);
});

test("with quickCast, fails with cooldown, sending nothing, while Quick Cast is on Cooldown", async () => {
  const { t, outcome } = await casting("Gleam Ray", MOB, { quickCast: true }, cooldowns({ Quickcast: 30 }));

  expect(await outcome).toEqual({ ok: false, reason: "cooldown" });
  expect(t.take()).toEqual([]);
});

test("with quickCast, fails with noQuickCast when its Effect doesn't come up", async () => {
  const { t, outcome } = await casting("Gleam Ray", MOB, { quickCast: true });

  await t.perceive(gameMessage("You can't prepare a quickcast while casting a spell!"));
  await t.clock.advance(START_TIMEOUT_MS);

  expect(await outcome).toEqual({ ok: false, reason: "noQuickCast" });
  expect(t.take().some((primitive) => primitive.name === "castSpell")).toBe(false);
});
