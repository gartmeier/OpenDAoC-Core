import { type Agent, failed, type Outcome, succeeded } from "../../../mind/src/tactic.ts";
import { distance, headingTowards } from "../../../mind/src/surroundings.ts";
import { rest } from "../../../mind/src/tactics/rest.ts";
import { runTo } from "../../../mind/src/tactics/run-to.ts";
import { buffSelf, castOn, dropWeaker, ensurePet, healMe, healPet, kept, petNpc } from "./pet.ts";

const away = (agent: Agent, p: { x: number; y: number }) => {
  const at = agent.self.position;
  return at ? Math.hypot(p.x - at.x, p.y - at.y) : Infinity;
};

/** The other Group member called `name`, as a player in view, with how the group window says it's doing. */
function mate(agent: Agent, name: string) {
  const member = agent.group.named(name);
  const player = member && agent.surroundings.players.find((p) => p.objectId === member.objectId);
  return member && { member, player };
}

/** Its best heal it has the Power for. */
function bestHeal(agent: Agent) {
  return agent.self.skills
    .filter((s) => s.page === "Spells" && s.details?.description.startsWith("Heals") && (s.details.powerCost ?? 0) <= agent.self.power)
    .sort((a, b) => b.level - a.level)[0];
}

/** Its best damage over time Spell it has the Power for. */
function bestDot(agent: Agent) {
  return agent.self.skills
    .filter((s) => s.page === "Spells" && /damage every/.test(s.details?.description ?? "") && (s.details?.powerCost ?? 0) <= agent.self.power)
    .sort((a, b) => b.level - a.level)[0];
}

/** Heals the Group member `name` while below `below`% and in view. */
export async function healMate(agent: Agent, name: string, below = 60, casts = 4): Promise<void> {
  for (let i = 0; i < casts; i++) {
    const m = mate(agent, name);
    const heal = bestHeal(agent);
    if (!m?.player || !heal || (m.member.healthPercent ?? 100) >= below || m.member.dead) return;
    const before = m.member.healthPercent;
    const reply = await castOn(agent, m.player.objectId, heal.name);
    if (!reply?.startsWith("You cast")) return;
    // The group window lags the heal: wait for it (or "fully healed") so it doesn't overheal on a stale percentage.
    const full = await agent.waitFor((percept) =>
      percept?.kind === "message" && /is fully healed/.test(percept.text) ? "full" :
      mate(agent, name)?.member.healthPercent !== before ? "updated" : undefined, 2_000);
    if (full === "full") return;
  }
}

/** The buffs worth keeping on a caster mate: armour and constitution, the best of each it has. */
const MATE_BUFFS = [/^Increases the target's armor factor/, /^Increases the target's constitution by/];

/** Puts its armour and constitution buffs on the mate when it's close and alive and lacks them. */
export async function buffMate(agent: Agent, name: string): Promise<void> {
  const m = mate(agent, name);
  if (!m?.player || m.member.dead || !m.member.position || away(agent, m.member.position) > 1200) return;
  for (const kind of MATE_BUFFS) {
    const buff = agent.self.skills.filter((s) => s.page === "Spells" && kind.test(s.details?.description ?? "") && s.details?.concentrationCost)
      .sort((a, b) => b.level - a.level)[0];
    if (!buff || kept(agent, buff, name)) continue;
    await dropWeaker(agent, buff, name);
    if ((buff.details?.concentrationCost ?? 0) > agent.self.concentration) continue;
    await castOn(agent, m.player.objectId, buff.name);
  }
}

/** Mobs attacking it, its Pet or a Group member. */
export function hostile(agent: Agent) {
  const ours = [agent.self.objectId, agent.self.pet?.objectId, ...agent.group.members.map((m) => m.objectId)].filter((id) => id !== undefined);
  return ours.flatMap((id) => agent.surroundings.attacking(id!)).filter((npc) => npc.realm === "None" && npc.healthPercent > 0);
}

/** Melee on `mobId` without spamming the server: it sends only what changed since the last step. */
class Striker {
  #swinging = false;
  #runningTo: { x: number; y: number } | undefined;
  #facing: number | undefined;
  constructor(readonly agent: Agent, readonly mobId: number) {}

  async step(): Promise<void> {
    const { agent, mobId } = this;
    const mob = agent.surroundings.npc(mobId);
    const at = agent.self.position;
    if (!mob || !at) return;
    if (agent.self.target !== mobId) { await agent.send({ name: "target", objectId: mobId }); this.#swinging = false; }
    if (!this.#swinging) { await agent.send({ name: "attack", on: true }); this.#swinging = true; }
    if (distance(at, mob.position) > 120) {
      if (!this.#runningTo || distance(this.#runningTo, mob.position) > 80) {
        this.#runningTo = mob.position;
        await agent.send({ name: "moveTo", x: mob.position.x, y: mob.position.y, z: mob.position.z, within: 80 });
      }
      this.#facing = undefined;
    } else {
      this.#runningTo = undefined;
      const heading = headingTowards(at, mob.position);
      if (heading !== undefined && (this.#facing === undefined || Math.abs(((heading - this.#facing + 6144) % 4096) - 2048) > 300)) {
        this.#facing = heading;
        await agent.send({ name: "face", heading });
      }
    }
  }

  async halt(): Promise<void> {
    if (this.#swinging) await this.agent.send({ name: "attack", on: false });
    this.#swinging = false;
    this.#runningTo = undefined;
  }
}

/**
 * Fights `mobId` with the Pet as the tank: it heals the Pet and the Group, and melees only what attacks itself or the mate
 * (to take it off the mate) or when the Pet is gone. Ends when the Mob is gone.
 */
export async function groupFight(agent: Agent, mobId: number, name: string): Promise<Outcome> {
  const end = agent.clock.now() + 120_000;
  let sent = false, dotUntil = 0;
  const striker = new Striker(agent, mobId);
  try {
    while (agent.clock.now() < end) {
      // A timer, not a microtask: a loop that awaits nothing else must still let the connection breathe.
      await agent.waitFor(() => undefined, 50);
      if (agent.self.dead) return failed("died");
      const mob = agent.surroundings.npc(mobId);
      if (!mob || mob.healthPercent <= 0) return succeeded;

      if (agent.self.pet && !sent) {
        await agent.send({ name: "target", objectId: mobId });
        await agent.send({ name: "commandPet", order: "Attack" });
        sent = true;
      }

      const me = agent.self.objectId!;
      const m = mate(agent, name);
      const onMe = agent.surroundings.attacking(me).some((npc) => npc.objectId === mobId);
      const onMate = m?.player && agent.surroundings.attacking(m.player.objectId).some((npc) => npc.objectId === mobId);
      const melee = onMe || onMate || !agent.self.pet;

      // Heals: the mate first, then itself, then the Pet, one cast at a time so a mate the Mob turns on comes first at once.
      // Out of melee they don't get interrupted.
      // Only with a heal it has the Power for: else the `continue`s would spin without awaiting anything, starve the connection's
      // heartbeat and get the Character dropped linkdead.
      const canHeal = !!bestHeal(agent);
      if (canHeal && m && !m.member.dead && (m.member.healthPercent ?? 100) < (onMate ? 85 : 50)) { await striker.halt(); await healMate(agent, name, onMate ? 95 : 70, 1); continue; }
      if (canHeal && agent.self.healthPercent < 45 && !onMe) { await striker.halt(); await healMe(agent, 70, 1); continue; }
      const pet = petNpc(agent);
      if (canHeal && pet && pet.healthPercent < 45 && !onMe) { await striker.halt(); await healPet(agent, 70, 1); continue; }

      // Its damage over time on the Mob, renewed as it runs out, while there's Power to spare for heals.
      const dot = bestDot(agent);
      if (!melee && dot && agent.clock.now() >= dotUntil && agent.self.powerPercent >= 40) {
        await striker.halt();
        const reply = await castOn(agent, mobId, dot.name);
        dotUntil = agent.clock.now() + (reply?.startsWith("You cast") ? (dot.details?.durationSeconds ?? 24) * 1000 : 5_000);
        continue;
      }

      if (melee) await striker.step();
      else await striker.halt();
      await agent.waitFor(() => undefined, 1_000);
    }
    return failed("timeout");
  } finally {
    await striker.halt();
  }
}

/**
 * Between fights: a Pet, buffs on itself and the mate, heals, then a rest while it or the mate is low.
 * "rested" when it rested (look around again), else whether both are ready to pull.
 */
export async function upkeep(agent: Agent, name: string): Promise<Outcome | "rested"> {
  const pet = await ensurePet(agent);
  if (!pet.ok && pet.reason !== "petOutOfView") return pet;
  await buffSelf(agent);
  await buffMate(agent, name);
  await healMate(agent, name, 90);
  await healPet(agent, 85);
  await healMe(agent, 85);

  const m = mate(agent, name);
  if (!m) return failed("notGrouped");
  if (m.member.dead) return failed("mateDead");

  // Rest until it is full, and give the mate time to get its Power back.
  if (agent.self.healthPercent < 90 || agent.self.powerPercent < 70 || (m.member.powerPercent ?? 100) < 60 || (m.member.healthPercent ?? 100) < 80) {
    await Promise.race([
      rest(agent, 100),
      agent.waitFor(() => (agent.self.powerPercent >= 90 && agent.self.healthPercent >= 95 && (mate(agent, name)?.member.powerPercent ?? 100) >= 80) || undefined, 120_000),
      agent.waitFor(() => hostile(agent)[0], 120_000),
    ]);
    await agent.send({ name: "stand" });
    return "rested";
  }
  return succeeded;
}

/**
 * Grinds with the Group member `name` for `minutes`: rests until both are ready, waits for them to be close, pulls the nearest Mob
 * of `below` levels relative to its own (e.g. [-2, 1] = two above to one below) with the Pet, and heals everyone.
 */
export async function groupGrind(agent: Agent, name: string, minutes: number, stopAtLevel: number, range = [-1, 2], avoid: string[] = ["mudman"], lead = true): Promise<Outcome & { kills?: number }> {
  const end = agent.clock.now() + minutes * 60_000;
  let kills = 0, idle = 0;
  const shunned = new Map<number, number>();
  while (agent.clock.now() < end && agent.self.level < stopAtLevel) {
    // A timer, not a microtask: a loop that awaits nothing else must still let the connection breathe.
    await agent.waitFor(() => undefined, 50);
    if (agent.self.dead) {
      await agent.sendAndWait({ name: "command", text: "/release" }, () => !agent.self.dead, 30_000);
      return { ...failed("died"), kills };
    }

    const foe = hostile(agent)[0];
    if (foe) {
      const o = await groupFight(agent, foe.objectId, name);
      if (o.ok) kills++;
      continue;
    }

    const ready = await upkeep(agent, name);
    if (ready === "rested") continue;
    if (!ready.ok) return { ...ready, kills };
    const m = mate(agent, name)!;

    // Stay close to the mate.
    // The group map's position, not the player in view, which stays stale after a release.
    const where = m.member.position;
    // Following, it keeps closer, and runs to where the mate is seen, not the map's 2.5 s old point.
    const leash = lead ? 1200 : 500;
    const there = m.player?.position ?? (where && { ...where, z: agent.self.position!.z });
    if (!m.player || !where || away(agent, where) > leash) {
      // Not in view though the map says it's close: the map is stale (it lags, or the mate is gone), so wait rather than spin.
      if (there && away(agent, there) < 8000 && away(agent, there) > (lead ? 400 : 200)) await runTo(agent, there, lead ? 400 : 200);
      else await agent.waitFor(() => undefined, 5_000);
      continue;
    }

    if (!lead) {
      // The mate pulls: wait for a Mob to attack one of us, or for the mate to move off.
      await agent.waitFor(() => hostile(agent)[0] || (away(agent, mate(agent, name)?.player?.position ?? where) > leash ? "moved" : undefined), 15_000);
      continue;
    }

    const level = agent.self.level;
    const mob = agent.surroundings.npcs
      .filter((npc) => npc.realm === "None" && !npc.peaceful && !npc.pet && npc.healthPercent > 0 && !avoid.includes(npc.name)
        && npc.level >= level + range[0] && npc.level <= level + range[1] && (shunned.get(npc.objectId) ?? 0) < 2)
      .map((npc) => ({ npc, away: away(agent, npc.position) }))
      .filter((x) => x.away < 2500)
      // Higher Mobs pay about twice as much (L8 rowdy ~1000 XP, L5 ~520), so the highest first, then the nearest.
      .sort((a, b) => b.npc.level - a.npc.level || a.away - b.away)[0];
    if (!mob) {
      if (++idle > 6) return { ...failed("noMob"), kills };
      await agent.waitFor(() => undefined, 10_000);
      continue;
    }
    idle = 0;

    const o = await groupFight(agent, mob.npc.objectId, name);
    if (o.ok) kills++;
    else if (o.reason === "died") continue;
    else shunned.set(mob.npc.objectId, (shunned.get(mob.npc.objectId) ?? 0) + 1);
  }
  return { ...succeeded, kills };
}
