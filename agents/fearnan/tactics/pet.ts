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

/**
 * Whether `buff` is kept up on `target` per the Concentration list. The list cuts names ("Strength of .." is both Strength of the Redwood
 * and Strength of the Tree), so the cost tells them apart.
 */
export function kept(agent: Agent, buff: Spell, target: string): boolean {
  return agent.self.concentrationBuffs.some((k) => k.spell === cut(buff.name) && k.cost === buff.details?.concentrationCost && k.target === cut(target));
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
    if (kept(agent, buff, pet.name)) continue;
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
    if (kept(agent, buff, "Fearnan")) continue;
    await castOn(agent, me, buff.name);
  }
}

/** Its best heal by hit points for the Power it has. */
function bestHeal(agent: Agent) {
  return spells(agent)
    .filter((spell) => spell.details?.description.startsWith("Heals") && (spell.details.powerCost ?? 0) <= agent.self.power)
    .sort((a, b) => b.level - a.level)[0];
}

/** Heals the Pet while it's below `below`% and there's Power. */
export async function healPet(agent: Agent, below = 80): Promise<void> {
  for (let i = 0; i < 6; i++) {
    const pet = petNpc(agent);
    const heal = bestHeal(agent);
    if (!pet || !heal || pet.healthPercent >= below) return;
    const reply = await castOn(agent, pet.objectId, heal.name);
    if (!reply?.startsWith("You cast")) return;
  }
}

/** Heals itself while below `below`% and there's Power. */
export async function healMe(agent: Agent, below = 70): Promise<void> {
  for (let i = 0; i < 6; i++) {
    const heal = bestHeal(agent);
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
