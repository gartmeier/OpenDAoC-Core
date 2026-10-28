import { type Agent, failed, type Outcome } from "../../../mind/src/tactic.ts";
import { rest } from "../../../mind/src/tactics/rest.ts";
import { runTo } from "../../../mind/src/tactics/run-to.ts";
import { fight } from "./fight.ts";
import { trainBlades } from "./grind.ts";

export interface TrioOptions {
  minutes?: number;
  /** The Mob I pull, and its levels. */
  mob?: string;
  band?: [number, number];
  /** Mobs I keep my pull this far away from, so they don't join in. */
  avoid?: string[];
  avoidRange?: number;
  /** How far from the caster I pull, so I bring the Mob back into its range. */
  leash?: number;
  log?: string[];
}

/**
 * I pull and tank, as Orla set it up: a lone Mob at a time, once Orla has 60% power and nobody is low.
 * Orla assists me and nukes, Fearnan heals. Anything attacking the group is fought first.
 */
export async function trio(agent: Agent, { minutes = 20, mob = "large red wolfhound", band = [19, 21], avoid = ["mist sheerie"], avoidRange = 900, leash = 2500, log = [] }: TrioOptions = {}): Promise<Outcome> {
  const deadline = Date.now() + minutes * 60_000;
  const others = () => agent.group.members.filter((m) => m.objectId !== agent.self.objectId);
  const caster = () => agent.group.named("Orla");

  while (Date.now() < deadline) {
    if (agent.self.dead) {
      log.push("died");
      return failed("died");
    }
    await trainBlades(agent);

    // Defend the group first: a Mob on any of us, me or a member.
    const ids = [agent.self.objectId, ...others().map((m) => m.objectId)].filter((id): id is number => id !== undefined);
    const attacker = ids.flatMap((id) => agent.surroundings.attacking(id))[0];
    if (attacker) {
      const stats = await fight(agent, attacker.objectId);
      log.push(`defended vs ${attacker.name} L${attacker.level}: ${stats.outcome.ok ? "ok" : stats.outcome.reason}, hp ${agent.self.healthPercent}%`);
      continue;
    }

    // Wait while anyone is low: sit if it's me, stand by if it's them.
    if (agent.self.healthPercent < 75) {
      await rest(agent, 100);
      continue;
    }
    // Pull only with both of them here and alive: alone I'm the one holding everything.
    const away = (m: (typeof agent.group.members)[number]) => m.dead || !m.position || !agent.self.position || Math.hypot(m.position.x - agent.self.position.x, m.position.y - agent.self.position.y) > 2000;
    const low = others().find((m) => away(m) || (m.healthPercent ?? 100) < 75 || (m.name === "Orla" && (m.powerPercent ?? 100) < 60));
    if (low) {
      await agent.waitFor(() => false, 5_000);
      continue;
    }

    const home = caster()?.position ?? agent.self.position;
    const pos = agent.self.position;
    if (!home || !pos) return failed("noPosition");
    const dist = (a: { x: number; y: number }, b: { x: number; y: number }) => Math.hypot(a.x - b.x, a.y - b.y);
    const avoided = agent.surroundings.npcs.filter((n) => avoid.includes(n.name) && n.healthPercent > 0);
    const target = agent.surroundings.npcs
      .filter((n) => n.name === mob && n.healthPercent > 0 && n.level >= band[0] && n.level <= band[1] && dist(n.position, home) < leash)
      .filter((n) => avoided.every((a) => dist(a.position, n.position) > avoidRange))
      .sort((a, b) => dist(a.position, pos) - dist(b.position, pos))[0];
    if (!target) {
      // Back to the caster and wait for a spawn.
      if (dist(home, pos) > 600) await runTo(agent, { ...home, z: agent.self.position!.z }, 300);
      else await agent.waitFor(() => false, 10_000);
      continue;
    }

    const xp = agent.self.level * 1000 + agent.self.levelPermill;
    const stats = await fight(agent, target.objectId);
    log.push(`${target.name} L${target.level}: ${stats.outcome.ok ? "ok" : stats.outcome.reason} in ${Math.round(stats.seconds)}s, -${stats.hpLost}% +${agent.self.level * 1000 + agent.self.levelPermill - xp}‰, hp ${agent.self.healthPercent}%`);
    // Back to the group after the fight, so the next pull starts beside the healer.
    if (agent.self.position && dist(home, agent.self.position) > 800) await runTo(agent, { ...home, z: agent.self.position.z }, 300);
  }
  return failed("outOfTime");
}

