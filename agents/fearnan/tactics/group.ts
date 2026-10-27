import { type Agent, failed, type Outcome, succeeded } from "../../../mind/src/tactic.ts";
import { distance, headingTowards } from "../../../mind/src/surroundings.ts";
import { rest } from "../../../mind/src/tactics/rest.ts";
import { runTo } from "../../../mind/src/tactics/run-to.ts";
import { buffSelf, castOn, ensurePet, healMe, healPet, petNpc } from "./pet.ts";

const away = (agent: Agent, p: { x: number; y: number }) => {
  const at = agent.self.position;
  return at ? Math.hypot(p.x - at.x, p.y - at.y) : Infinity;
};

/** The other Group member called `name`, as a player in view, with how the group window says it's doing. */
function mate(agent: Agent, name: string) {
  const member = agent.group.named(name);
  const player = agent.surroundings.players.find((p) => p.name === name);
  return member && { member, player };
}

/** Its best heal it has the Power for. */
function bestHeal(agent: Agent) {
  return agent.self.skills
    .filter((s) => s.page === "Spells" && s.details?.description.startsWith("Heals") && (s.details.powerCost ?? 0) <= agent.self.power)
    .sort((a, b) => b.level - a.level)[0];
}

/** Heals the Group member `name` while below `below`% and in view. */
export async function healMate(agent: Agent, name: string, below = 60): Promise<void> {
  for (let i = 0; i < 4; i++) {
    const m = mate(agent, name);
    const heal = bestHeal(agent);
    if (!m?.player || !heal || (m.member.healthPercent ?? 100) >= below || m.member.dead) return;
    const reply = await castOn(agent, m.player.objectId, heal.name);
    if (!reply?.startsWith("You cast")) return;
  }
}

/** Mobs attacking it, its Pet or a Group member. */
function hostile(agent: Agent) {
  const ours = [agent.self.objectId, agent.self.pet?.objectId, ...agent.group.members.map((m) => m.objectId)].filter((id) => id !== undefined);
  return ours.flatMap((id) => agent.surroundings.attacking(id!)).filter((npc) => npc.realm === "None");
}

/** One step of melee on `mobId`: runs in range, faces it and keeps attack on. */
async function strike(agent: Agent, mobId: number): Promise<void> {
  const mob = agent.surroundings.npc(mobId);
  const at = agent.self.position;
  if (!mob || !at) return;
  if (agent.self.target !== mobId) await agent.send({ name: "target", objectId: mobId });
  if (distance(at, mob.position) > 120) {
    await agent.send({ name: "moveTo", x: mob.position.x, y: mob.position.y, z: mob.position.z, within: 80 });
  } else {
    const heading = headingTowards(at, mob.position);
    if (heading !== undefined) await agent.send({ name: "face", heading });
  }
  await agent.send({ name: "attack", on: true });
}

/**
 * Fights `mobId` with the Pet as the tank: it heals the Pet and the Group, and melees only what attacks itself or the mate
 * (to take it off the mate) or when the Pet is gone. Ends when the Mob is gone.
 */
export async function groupFight(agent: Agent, mobId: number, name: string): Promise<Outcome> {
  const end = agent.clock.now() + 120_000;
  let sent = false;
  try {
    while (agent.clock.now() < end) {
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

      // Heals: the mate first, then itself, then the Pet. Out of melee they don't get interrupted.
      if (m && !m.member.dead && (m.member.healthPercent ?? 100) < 50) { await agent.send({ name: "attack", on: false }); await healMate(agent, name, 70); continue; }
      if (agent.self.healthPercent < 45 && !onMe) { await healMe(agent, 70); continue; }
      const pet = petNpc(agent);
      if (pet && pet.healthPercent < 45 && !onMe) { await agent.send({ name: "attack", on: false }); await healPet(agent, 70); continue; }

      if (melee) await strike(agent, mobId);
      else await agent.send({ name: "attack", on: false });
      await agent.waitFor(() => undefined, 1_000);
    }
    return failed("timeout");
  } finally {
    await agent.send({ name: "attack", on: false });
  }
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

    const pet = await ensurePet(agent);
    if (!pet.ok && pet.reason !== "petOutOfView") return { ...pet, kills };
    await buffSelf(agent);
    await healMate(agent, name, 90);
    await healPet(agent, 85);
    await healMe(agent, 85);

    const m = mate(agent, name);
    if (!m) return { ...failed("notGrouped"), kills };
    if (m.member.dead) return { ...failed("mateDead"), kills };

    // Rest until it is full, and give the mate time to get its Power back.
    if (agent.self.healthPercent < 90 || agent.self.powerPercent < 70 || (m.member.powerPercent ?? 100) < 60 || (m.member.healthPercent ?? 100) < 80) {
      const rested = await Promise.race([
        rest(agent, 100),
        agent.waitFor(() => (agent.self.powerPercent >= 90 && agent.self.healthPercent >= 95 && (mate(agent, name)?.member.powerPercent ?? 100) >= 80) || undefined, 120_000).then(() => succeeded),
        agent.waitFor(() => hostile(agent)[0], 120_000).then((foe) => (foe ? failed("attacked") : succeeded)),
      ]);
      await agent.send({ name: "stand" });
      if (!rested.ok && rested.reason === "attacked") continue;
      continue;
    }

    // Stay close to the mate.
    // The group map's position, not the player in view, which stays stale after a release.
    const where = m.member.position;
    if (!m.player || !where || away(agent, where) > 1200) {
      if (where && away(agent, where) < 3000) await runTo(agent, { ...where, z: agent.self.position!.z }, 400);
      else await agent.waitFor(() => undefined, 5_000);
      continue;
    }

    if (!lead) {
      // The mate pulls: wait for a Mob to attack one of us.
      await agent.waitFor(() => hostile(agent)[0], 15_000);
      continue;
    }

    const level = agent.self.level;
    const mob = agent.surroundings.npcs
      .filter((npc) => npc.realm === "None" && !npc.peaceful && !npc.pet && npc.healthPercent > 0 && !avoid.includes(npc.name)
        && npc.level >= level + range[0] && npc.level <= level + range[1] && (shunned.get(npc.objectId) ?? 0) < 2)
      .map((npc) => ({ npc, away: away(agent, npc.position) }))
      .filter((x) => x.away < 2500)
      .sort((a, b) => a.away - b.away)[0];
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
