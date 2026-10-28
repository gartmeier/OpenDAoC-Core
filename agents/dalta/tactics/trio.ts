import { type Agent, failed, type Outcome } from "../../../mind/src/tactic.ts";
import { answer } from "../../../mind/src/tactics/dialog.ts";
import { rest } from "../../../mind/src/tactics/rest.ts";
import { runTo } from "../../../mind/src/tactics/run-to.ts";
import { fight } from "./fight.ts";
import { trainBlades } from "./grind.ts";
import { sitForEndurance } from "./task.ts";

export interface TrioOptions {
  minutes?: number;
  /** The Mob I pull, and its levels. */
  mob?: string;
  band?: [number, number];
  /** Mobs I keep my pull this far away from, so they don't join in. */
  avoid?: string[];
  avoidRange?: number;
  /** Where the group sits: I pull only Mobs this close to it, and come back to it after each fight. */
  camp?: { x: number; y: number };
  leash?: number;
  /** A spot to clear first, such as a grave a Mob stands by. */
  first?: { x: number; y: number };
  log?: string[];
}

/**
 * I pull and tank, as Orla set it up: a lone Mob at a time, once Orla has 60% power and nobody is low.
 * Orla assists me and nukes, Fearnan heals. Anything attacking the group is fought first.
 */
export async function trio(agent: Agent, { minutes = 20, mob = "large red wolfhound", band = [19, 21], avoid = ["mist sheerie"], avoidRange = 900, camp, leash = 1500, first, log = [] }: TrioOptions = {}): Promise<Outcome> {
  const deadline = Date.now() + minutes * 60_000;
  const others = () => agent.group.members.filter((m) => m.objectId !== agent.self.objectId);
  const caster = () => agent.group.named("Orla");
  // The healer anchors the group: I pull Mobs back to Fearnan and tank there, where Orla stays too.
  const healer = () => agent.group.named("Fearnan")?.position;

  while (Date.now() < deadline) {
    // The group's rule: stay dead, say so once in /g, and wait for Fearnan to rez once the fight is safe.
    if (agent.self.dead) {
      await agent.send({ name: "command", text: "/g Dalta dead, need a rez when it's safe." });
      const offer = await agent.waitFor(() => agent.self.dialog || undefined, 180_000);
      if (offer) await answer(agent, true);
      const alive = await agent.waitFor(() => !agent.self.dead || undefined, 10_000);
      log.push(`died: ${offer ? `offered "${offer.text}"` : "no rez offer"}, ${alive ? "alive" : "still dead"}`);
      if (!alive) return failed("died");
      continue;
    }
    await trainBlades(agent);

    // Defend the group first: a Mob on any of us, me or a member.
    const ids = [agent.self.objectId, ...others().map((m) => m.objectId)].filter((id): id is number => id !== undefined);
    // A Mob near me is still in the fight even when it attacks nobody I can see: it's on a Pet, between swings,
    // or an add Orla mezzed at full health. Finish it before sitting, or the mez breaks on whoever sits.
    const me = agent.self.position;
    const near = (n: { position: { x: number; y: number } }, r: number) => me !== undefined && Math.hypot(n.position.x - me.x, n.position.y - me.y) < r;
    const wounded = agent.surroundings.npcs.find((n) => n.name === mob && n.healthPercent > 0 && ((n.healthPercent < 100 && near(n, 1500)) || near(n, 800)));
    const attacker = ids.flatMap((id) => agent.surroundings.attacking(id))[0] ?? wounded;
    if (attacker) {
      const stats = await fight(agent, attacker.objectId);
      log.push(`defended vs ${attacker.name} L${attacker.level}: ${stats.outcome.ok ? "ok" : stats.outcome.reason}, hp ${agent.self.healthPercent}%`);
      continue;
    }

    // Wait while anyone is low: sit if it's me, stand by if it's them.
    if (agent.self.healthPercent < 80) {
      await rest(agent, 100);
      continue;
    }
    // Pull only with both of them here and alive: alone I'm the one holding everything.
    const away = (m: (typeof agent.group.members)[number]) => m.dead || !m.position || !agent.self.position || Math.hypot(m.position.x - agent.self.position.x, m.position.y - agent.self.position.y) > 2000;
    const low = others().find((m) => away(m) || (m.healthPercent ?? 100) < 80 || (m.name === "Orla" && (m.powerPercent ?? 100) < 60));
    if (low) {
      await agent.waitFor(() => false, 5_000);
      continue;
    }

    const home = first ?? healer() ?? camp ?? agent.self.position;
    const pos = agent.self.position;
    if (!home || !pos) return failed("noPosition");
    const dist = (a: { x: number; y: number }, b: { x: number; y: number }) => Math.hypot(a.x - b.x, a.y - b.y);
    const avoided = agent.surroundings.npcs.filter((n) => avoid.includes(n.name) && n.healthPercent > 0);
    const target = agent.surroundings.npcs
      .filter((n) => n.name === mob && n.healthPercent > 0 && n.level >= band[0] && n.level <= band[1] && dist(n.position, home) < leash)
      .filter((n) => avoided.every((a) => dist(a.position, n.position) > avoidRange))
      .sort((a, b) => dist(a.position, pos) - dist(b.position, pos))[0];
    if (!target && first) {
      first = undefined;
      continue;
    }
    if (!target) {
      // Back to the caster and wait for a spawn.
      if (dist(home, pos) > 600) await runTo(agent, { ...home, z: agent.self.position!.z }, 300);
      else await agent.waitFor(() => false, 10_000);
      continue;
    }

    // Taunting Blade is what holds the Mob on me instead of Orla: start each pull with endurance for it.
    if (agent.self.endurancePercent < 50) {
      await sitForEndurance(agent);
      continue;
    }
    const xp = agent.self.level * 1000 + agent.self.levelPermill;
    const pulled = await pull(agent, target.objectId, healer());
    const stats = await fight(agent, target.objectId);
    log.push(`${target.name} L${target.level}: ${stats.outcome.ok ? "ok" : stats.outcome.reason} in ${Math.round(stats.seconds)}s, -${stats.hpLost}% (pull ${pulled}) +${agent.self.level * 1000 + agent.self.levelPermill - xp}‰, hp ${agent.self.healthPercent}%`);
    // Back to the group after the fight, so the next pull starts beside the healer.
    const back = healer() ?? camp;
    if (back && agent.self.position && dist(back, agent.self.position) > 500) await runTo(agent, { ...back, z: agent.self.position.z }, 200);
  }
  return failed("outOfTime");
}


/**
 * Hits the Mob once to make it chase me, then runs back to `to`, so the fight happens beside the healer.
 * Says how it went: `near` (no need), `pulled`, or `noAggro` (it never came).
 */
async function pull(agent: Agent, mobId: number, to: { x: number; y: number } | undefined): Promise<string> {
  const mob = agent.surroundings.npc(mobId);
  const me = agent.self.objectId;
  if (!mob || !to || me === undefined || Math.hypot(mob.position.x - to.x, mob.position.y - to.y) < 600) return "near";
  await runTo(agent, mob.position, 100);
  await agent.send({ name: "target", objectId: mobId });
  await agent.send({ name: "attack", on: true });
  const aggro = await agent.waitFor(() => agent.surroundings.attacking(me).some((n) => n.objectId === mobId) || undefined, 8_000);
  await agent.send({ name: "attack", on: false });
  if (!aggro) return "noAggro";
  await runTo(agent, { ...to, z: agent.self.position!.z }, 150);
  return "pulled";
}
