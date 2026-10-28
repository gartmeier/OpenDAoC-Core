import type { SkillEntry } from "../../../mind/src/protocol.ts";
import { type Agent, failed, type Outcome, succeeded } from "../../../mind/src/tactic.ts";
import { kill } from "../../../mind/src/tactics/kill.ts";
import { rest } from "../../../mind/src/tactics/rest.ts";
import { runTo } from "../../../mind/src/tactics/run-to.ts";

type Spell = SkillEntry;

const spells = (agent: Agent) => agent.self.skills.filter((skill) => skill.page === "Spells");
const cut = (name: string) => (name.length > 14 ? `${name.slice(0, 12)}..` : name);
const away = (agent: Agent, p: { x: number; y: number }) => {
  const at = agent.self.position;
  return at ? Math.hypot(p.x - at.x, p.y - at.y) : Infinity;
};

/** A base (single-stat or armour) buff: what a Bard's Nurture also gives. Spec buffs (str/con, haste, damage shield) aren't. */
export const isBase = (spell: Spell) => /^Increases the target's (armor factor|strength|constitution|dexterity) by/.test(spell.details?.description ?? "");

/** A living Bard in the Group (Ceol): it gives everyone the base buffs, so I keep only my spec ones (Ceol asked, and so did the Human). */
export const bardBuffs = (agent: Agent) => agent.group.members.some((m) => m.name !== "Fearnan" && /Bard/.test(m.className ?? "") && !m.dead);

/** Same kind of buff, ignoring the numbers (Ceol's "Oak's Stamina" and my "Oak's Girth" are both "constitution by #"). */
const kindOf = (description: string) => description.replace(/[\d.]+/g, "#");

/** Whether it should leave `buff` off: a base buff while a Bard covers those. */
const leaveOff = (agent: Agent, buff: Spell) => isBase(buff) && bardBuffs(agent);

/** Cancels my base buffs on myself and the Pet while a Bard gives those, so the Bard's can land (and my Concentration is free). */
export async function dropBase(agent: Agent): Promise<number> {
  if (!bardBuffs(agent)) return 0;
  const pet = agent.self.pet && agent.surroundings.npc(agent.self.pet.objectId);
  let dropped = 0;
  for (const k of agent.self.concentrationBuffs.filter((k) => k.target === cut("Fearnan") || (pet && k.target === cut(pet.name)))) {
    const spell = spells(agent).find((s) => cut(s.name) === k.spell && s.details?.concentrationCost === k.cost);
    if (!spell || !isBase(spell)) continue;
    await agent.sendAndWait({ name: "cancelConcentration", spell: k.spell, target: k.target },
      () => !agent.self.concentrationBuffs.some((c) => c.spell === k.spell && c.target === k.target && c.cost === k.cost) || undefined, 3_000);
    dropped++;
  }
  return dropped;
}

/**
 * Whether `buff` is kept up on `target` per the Concentration list. The list cuts names ("Strength of .." is both Strength of the Redwood
 * and Strength of the Tree), so the cost tells them apart.
 */
export function kept(agent: Agent, buff: Spell, target: string): boolean {
  return agent.self.concentrationBuffs.some((k) => k.spell === cut(buff.name) && k.cost === buff.details?.concentrationCost && k.target === cut(target));
}

/**
 * Cancels weaker Concentration buffs of the same kind as `buff` that it keeps on `target`: the server refuses a stronger one
 * ("already has that effect") while the weaker is on.
 */
export async function dropWeaker(agent: Agent, buff: Spell, target: string): Promise<void> {
  const kind = (description: string) => description.replace(/[\d.]+/g, "#");
  for (const k of agent.self.concentrationBuffs.filter((k) => k.target === cut(target))) {
    const spell = spells(agent).find((s) => cut(s.name) === k.spell && s.details?.concentrationCost === k.cost);
    if (!spell || spell.name === buff.name || kind(spell.details!.description) !== kind(buff.details!.description)) continue;
    await agent.sendAndWait({ name: "cancelConcentration", spell: k.spell, target: k.target },
      () => !agent.self.concentrationBuffs.some((c) => c.spell === k.spell && c.target === k.target && c.cost === k.cost) || undefined, 3_000);
  }
}

/** The Pet as an NPC in view, if it has one. */
export function petNpc(agent: Agent) {
  return agent.self.pet && agent.surroundings.npc(agent.self.pet.objectId);
}

/** Casts `spell` on `objectId`, waiting out "must wait" holds. Returns the server's answer. */
export async function castOn(agent: Agent, objectId: number, spell: string): Promise<string | undefined> {
  // A cast fails while sitting and is interrupted by a run still going.
  if (agent.self.sitting) await agent.send({ name: "stand" });
  if (agent.self.movement.kind === "moving") await agent.send({ name: "stop" });
  for (let tries = 0; tries < 3; tries++) {
    await agent.send({ name: "target", objectId });
    const reply = await agent.sendAndWait({ name: "castSpell", spell }, (percept) =>
      percept?.kind === "message" && /You cast a|interrupt|must wait|requires|can't cast|enough power|too far|can't see/.test(percept.text) ? percept.text : undefined, 10_000);
    const wait = reply?.match(/must wait (\d+) seconds?/);
    if (!wait) return reply;
    await agent.waitFor(() => undefined, Number(wait[1]) * 1000 + 300);
  }
}

/**
 * Its best Concentration buff of each kind (only those it can cast on others if `others`).
 * A single-stat buff (baseline) and a two-stat one (spec) of the same stat both count, so both stay.
 */
export function bestBuffs(agent: Agent, others: boolean): Spell[] {
  const kind = (description: string) => description.replace(/[\d.]+/g, "#");
  const best = new Map<string, Spell>();
  for (const spell of spells(agent)) {
    if (!spell.details?.concentrationCost || (others && spell.details.range === 0)) continue;
    const other = best.get(kind(spell.details.description));
    if (!other || spell.level > other.level) best.set(kind(spell.details.description), spell);
  }
  return [...best.values()];
}

/** Summons a Pet if it has none, then puts its best Concentration buffs of each kind on it. */
export async function ensurePet(agent: Agent): Promise<Outcome> {
  if (!agent.self.pet) {
    const summoning = spells(agent).filter((spell) => spell.details?.description.startsWith("Summon a pet")).sort((a, b) => b.level - a.level)[0];
    if (!summoning) return failed("noSummoning");
    if (agent.self.powerPercent < 26) {
      const rested = await rest(agent, 100);
      if (!rested.ok) return rested;
    }
    await agent.send({ name: "stop" });
    const reply = await agent.sendAndWait({ name: "castSpell", spell: summoning.name }, (percept) =>
      percept?.kind === "petWindow" && percept.action === "Open" ? "summoned" :
      percept?.kind === "message" && /interrupt|must wait|enough power|can't cast|release it first/.test(percept.text) ? percept.text : undefined, 10_000);
    if (reply !== "summoned") return failed("notSummoned");
    await agent.waitFor(() => petNpc(agent), 3_000);
  }

  const pet = petNpc(agent);
  if (!pet) return failed("petOutOfView");

  for (const buff of bestBuffs(agent, true)) {
    if (kept(agent, buff, pet.name) || leaveOff(agent, buff)) continue;
    await dropWeaker(agent, buff, pet.name);
    if (away(agent, pet.position) > 900) await runTo(agent, pet.position, 300);
    await castOn(agent, pet.objectId, buff.name);
  }
  return succeeded;
}

/** Puts its best Concentration buffs of each kind on itself (self-only ones too). */
export async function buffSelf(agent: Agent): Promise<void> {
  const me = agent.self.objectId;
  if (me === undefined) return;
  for (const buff of bestBuffs(agent, false)) {
    // After a relog the buffs stay on the buff bar but leave the Concentration list, and recasting them fails.
    if (kept(agent, buff, "Fearnan") || agent.self.effects.some((e) => e.name === buff.name)) continue;
    // What's actually on me: a buff of the same kind from someone else (Ceol's Oak's Stamina) refuses mine anyway.
    if (leaveOff(agent, buff) || agent.self.effects.some((e) => e.details && !agent.self.concentrationBuffs.some((k) => k.spell === cut(e.name) && k.target === cut("Fearnan")) && kindOf(e.details.description) === kindOf(buff.details?.description ?? ""))) continue;
    await dropWeaker(agent, buff, "Fearnan");
    await castOn(agent, me, buff.name);
  }
}

/** Hit points a heal Spell heals, from its description. */
const healed = (spell: { details?: { description: string } }) => Number(/for (\d+) hit points/.exec(spell.details?.description ?? "")?.[1] ?? 0);

/**
 * Its biggest heal by hit points for the Power it has. Not by Spell level: at 18 that picked Minor Regrowth (48 HP), the newest.
 * Not the group heal, and not one with a reuse timer (Nature's Appreciation, kept for emergencies: see `emergencyHeal`).
 */
export function bestHeal(agent: Agent, missingPercent = 100) {
  const heals = spells(agent)
    .filter((spell) => healed(spell) > 0 && !spell.name.startsWith("Group") && !spell.details?.recastSeconds && (spell.details?.powerCost ?? 0) <= agent.self.power);
  // A top-off (a quarter or less missing) takes the cheapest heal: a 29-Power Greater Revival on someone at 90% mostly overheals
  // ("is fully healed"), and the Power it wastes is rest time.
  if (missingPercent <= 25) return heals.sort((a, b) => (a.details?.powerCost ?? 0) - (b.details?.powerCost ?? 0))[0];
  return heals.sort((a, b) => healed(b) - healed(a))[0];
}

/** Its instant heal with a reuse timer (Nature's Appreciation, 33%), if it has one: a Mob on me can't interrupt it. */
export function emergencyHeal(agent: Agent) {
  return spells(agent).find((spell) => spell.details?.description.startsWith("Heals") && spell.details.recastSeconds && spell.details.castingSeconds === 0);
}

/** Heals the Pet while it's below `below`% and there's Power. */
export async function healPet(agent: Agent, below = 80, casts = 6): Promise<void> {
  for (let i = 0; i < casts; i++) {
    const pet = petNpc(agent);
    const heal = bestHeal(agent, 100 - (pet?.healthPercent ?? 0));
    if (!pet || !heal || pet.healthPercent >= below) return;
    const before = pet.healthPercent;
    const reply = await castOn(agent, pet.objectId, heal.name);
    if (!reply?.startsWith("You cast")) return;
    // Its health lags the heal: wait for it (or "fully healed"), else it casts again on a full Pet.
    const full = await agent.waitFor((p) => p?.kind === "message" && /is fully healed/.test(p.text) ? "full"
      : petNpc(agent)?.healthPercent !== before ? "updated" : undefined, 2_000);
    if (full === "full") return;
  }
}

/** Heals itself while below `below`% and there's Power. */
export async function healMe(agent: Agent, below = 70, casts = 6): Promise<void> {
  for (let i = 0; i < casts; i++) {
    const heal = bestHeal(agent, 100 - agent.self.healthPercent);
    if (!heal || agent.self.healthPercent >= below || agent.self.objectId === undefined) return;
    const reply = await castOn(agent, agent.self.objectId, heal.name);
    if (!reply?.startsWith("You cast")) return;
  }
}

/**
 * Sends the Pet at the Mob and helps it: in melee too if `melee`, else from where it stands, healing the Pet below 50%.
 * Whatever attacks the Agent itself it fights in melee. Ends when the Mob dies.
 */
export async function petFight(agent: Agent, mobId: number, melee: boolean): Promise<Outcome> {
  const mob = agent.surroundings.npc(mobId);
  if (!mob) return failed("outOfView");
  if (!agent.self.pet) return failed("noPet");

  if (away(agent, mob.position) > 1500) {
    const at = agent.self.position!;
    const dx = mob.position.x - at.x, dy = mob.position.y - at.y, d = Math.hypot(dx, dy);
    await runTo(agent, { x: Math.round(mob.position.x - dx / d * 1000), y: Math.round(mob.position.y - dy / d * 1000), z: mob.position.z }, 150);
  }

  await agent.send({ name: "target", objectId: mobId });
  const ordered = await agent.sendAndWait({ name: "commandPet", order: "Attack" }, (percept) =>
    percept?.kind === "message" && /You command|too far away/.test(percept.text) ? percept.text : undefined, 5_000);
  if (!ordered?.startsWith("You command")) return failed("petNotSent");

  const end = agent.clock.now() + 120_000;
  while (agent.clock.now() < end) {
    const target = agent.surroundings.npc(mobId);
    if (!target) return succeeded;
    if (agent.self.dead) return failed("died");

    const me = agent.self.objectId!;
    const onMe = agent.surroundings.attacking(me)[0];
    if (melee || onMe) {
      // Let the Pet take the Mob's attention first.
      if (melee && !onMe) await agent.waitFor(() => undefined, 3_000);
      const outcome = await kill(agent, (onMe ?? target).objectId);
      if (!outcome.ok) return outcome;
      if (!onMe || onMe.objectId === mobId) return succeeded;
      continue;
    }

    if (!agent.self.pet) {
      // The Pet died: finish the Mob itself.
      return await kill(agent, mobId);
    }

    const pet = petNpc(agent);
    if (pet && pet.healthPercent < 50) await healPet(agent, 70);
    await agent.waitFor(() => undefined, 1_000);
  }
  return failed("timeout");
}

/**
 * Grinds with its Pet for `minutes` until `stopAtLevel`: Mobs `below` levels under its own (default 1 below to its level), nearest first,
 * skipping `avoid`. Rests itself and heals its Pet between fights, and summons and buffs a new Pet when it has none.
 */
export async function petGrind(agent: Agent, minutes: number, stopAtLevel: number, below = [0, 1], avoid: string[] = [], melee = false): Promise<Outcome & { kills?: number; deaths?: number; petDeaths?: number }> {
  const end = agent.clock.now() + minutes * 60_000;
  let kills = 0, deaths = 0, petDeaths = 0, idle = 0;
  const shunned = new Map<number, number>();

  while (agent.clock.now() < end && agent.self.level < stopAtLevel) {
    // A timer, not a microtask: a loop that awaits nothing else must still let the connection breathe.
    await agent.waitFor(() => undefined, 50);
    if (agent.self.dead) {
      deaths++;
      await agent.sendAndWait({ name: "command", text: "/release" }, () => !agent.self.dead, 30_000);
      return { ...failed("died"), kills, deaths, petDeaths };
    }

    const me = agent.self.objectId;
    const attacker = me === undefined ? undefined : agent.surroundings.attacking(me)[0];
    if (attacker) {
      if (agent.self.pet) await petFight(agent, attacker.objectId, true);
      else await kill(agent, attacker.objectId);
      continue;
    }

    const hadPet = !!agent.self.pet;
    const pet = await ensurePet(agent);
    if (!pet.ok) return { ...pet, kills, deaths, petDeaths };
    if (!hadPet) petDeaths++;
    await buffSelf(agent);

    await healPet(agent, 90);
    await healMe(agent, 80);
    if (agent.self.healthPercent < 100 || agent.self.powerPercent < 100) {
      const rested = await rest(agent, 100);
      if (!rested.ok && rested.reason === "attacked") continue;
    }

    const level = agent.self.level;
    const mob = agent.surroundings.npcs
      .filter((npc) => npc.realm === "None" && !npc.peaceful && !npc.pet && npc.healthPercent > 0 && !avoid.includes(npc.name)
        && npc.level >= level - Math.max(...below) && npc.level <= level - Math.min(...below) && (shunned.get(npc.objectId) ?? 0) < 2)
      .map((npc) => ({ npc, away: away(agent, npc.position) }))
      .sort((a, b) => a.away - b.away)[0];
    if (!mob) {
      // Wait a while for one to come into view or respawn.
      if (++idle > 6) return { ...failed("noMob"), kills, deaths, petDeaths };
      await agent.waitFor(() => undefined, 10_000);
      continue;
    }
    idle = 0;

    const outcome = await petFight(agent, mob.npc.objectId, melee);
    if (outcome.ok) kills++;
    else if (outcome.reason !== "died") shunned.set(mob.npc.objectId, (shunned.get(mob.npc.objectId) ?? 0) + 1);
  }
  return { ...succeeded, kills, deaths, petDeaths };
}
