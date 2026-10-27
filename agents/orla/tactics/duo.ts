import { type Agent, failed, type Outcome, succeeded } from "../../../mind/src/tactic.ts";
import { cast } from "../../../mind/src/tactics/cast.ts";
import { rest } from "../../../mind/src/tactics/rest.ts";
import { runTo } from "../../../mind/src/tactics/run-to.ts";

type Point = { x: number; y: number };

const dist = (a: Point | undefined, b: Point | undefined) => (a && b ? Math.hypot(a.x - b.x, a.y - b.y) : Infinity);

const headingTo = (agent: Agent, p: Point) => {
  const at = agent.self.position!;
  return (Math.round((Math.atan2(-(p.x - at.x), p.y - at.y) * 4096) / (2 * Math.PI)) + 4096) % 4096;
};

const damageOf = (description = "") => Number(/Inflicts (\d+)/.exec(description)?.[1] ?? 0);

/** The Shadow Control nuke (no recast) that inflicts the most. */
const nuke = (agent: Agent) =>
  agent.self.skills
    .filter((s) => s.page === "Spells" && s.details?.description.startsWith("Inflicts") && !(s.details?.recastSeconds))
    .sort((a, b) => damageOf(b.details?.description) - damageOf(a.details?.description))[0];

/** Where the partner is: in view if possible, else from the group map. */
export const partnerAt = (agent: Agent, name: string): Point | undefined =>
  agent.surroundings.players.find((p) => p.name === name)?.position ?? agent.group.named(name)?.position;

/** Nukes `objectId` until it dies, from range. Keeps casting even when it's on me (the pet should take it back). */
export async function burn(agent: Agent, objectId: number): Promise<Outcome> {
  const spell = nuke(agent);
  if (!spell) return failed("noSpell");
  const range = (spell.details?.range ?? 1500) - 150;
  const cost = spell.details?.powerCost ?? 0;
  const mob = () => agent.surroundings.npcs.find((n) => n.objectId === objectId);

  for (let tries = 0; tries < 30; tries++) {
    if (agent.self.dead) return failed("died");
    const m = mob();
    if (!m || m.healthPercent <= 0) return succeeded;
    if (agent.self.power < cost) return failed("noPower");
    if (dist(agent.self.position, m.position) > range) {
      const ran = await runTo(agent, m.position, range - 200);
      if (!ran.ok) return ran;
    }
    await agent.send({ name: "face", heading: headingTo(agent, m.position) });
    const me = agent.self.objectId;
    const onMe = me !== undefined && m.targetId === me && dist(agent.self.position, m.position) < 300;
    const out = await cast(agent, spell.name, objectId, onMe && agent.self.power >= 2 * cost && !agent.self.cooldowns.has("Quickcast") ? { quickCast: true } : undefined);
    if (!out.ok && out.reason === "tooFar") await runTo(agent, m.position, range - 400);
    if (!out.ok && !["interrupted", "noEffect", "tooFar", "mustWait"].includes(out.reason)) {
      console.log(`cast: ${out.reason}`);
      await agent.waitFor(() => undefined, 1000);
    }
  }
  return failed("tooLong");
}

/**
 * Hunts with a partner: assists on whatever fights the group (or, with `lead`, pulls Mobs of `levels` near the partner, announces each pull in group chat,
 * burns them down, and rests (saying so) when low. Stops after `minutes` or at `untilLevel`.
 */
export async function duo(agent: Agent, partner: string, levels: (l: number) => number[], minutes: number, untilLevel = 99, avoid: string[] = [], lead = false): Promise<Outcome> {
  const end = Date.now() + minutes * 60_000;
  let kills = 0;
  const log: string[] = [];
  while (Date.now() < end && agent.self.level < untilLevel) {
    if (agent.self.dead) return failed("died");
    const me = agent.self.objectId;
    const home = partnerAt(agent, partner);

    // Anything fighting us (me, partner, or the pet) first.
    const party = new Set(agent.group.members.map((m) => m.objectId));
    const busy = agent.surroundings.npcs.find((n) => n.realm === "None" && n.healthPercent > 0 && n.targetId !== undefined && (n.targetId === me || party.has(n.targetId) || agent.surroundings.npcs.some((p) => p.objectId === n.targetId && p.realm === "Hibernia" && dist(p.position, home) < 1500)) && dist(n.position, agent.self.position) < 2000);
    if (busy) {
      const k = await burn(agent, busy.objectId);
      if (k.ok) kills++;
      else log.push(`${busy.name}: ${k.reason}`);
      continue;
    }

    // Rest when I'm low, and sit with the partner while it is: once, waiting on beliefs, not on a loop of sends.
    const f = agent.group.named(partner);
    const partnerLow = !!f && ((f.powerPercent ?? 100) < 40 || (f.healthPercent ?? 100) < 60);
    if (agent.self.powerPercent < 50 || agent.self.healthPercent < 70 || partnerLow) {
      if (!partnerLow) await agent.send({ name: "command", text: "/g rest" });
      await rest(agent, 100);
      if (!agent.self.sitting) await agent.send({ name: "sit" } as never);
      await agent.waitFor(() => {
        const g = agent.group.named(partner);
        return !g || ((g.powerPercent ?? 100) >= 90 && (g.healthPercent ?? 100) >= 90) || agent.surroundings.npcs.some((n) => n.targetId === agent.self.objectId) || undefined;
      }, 60_000);
      continue;
    }

    // Stay with the partner.
    if (home && dist(agent.self.position, home) > 600) {
      await runTo(agent, home, 250);
      continue;
    }

    if (!lead) {
      // The partner pulls with the pet: wait for a fight to join.
      await agent.waitFor(() => undefined, 1000);
      continue;
    }
    const wanted = levels(agent.self.level);
    const mob = agent.surroundings.npcs
      .filter((n) => n.realm === "None" && !n.peaceful && n.healthPercent > 0 && wanted.includes(n.level) && !avoid.includes(n.name) && dist(n.position, home ?? agent.self.position) < 1600)
      .sort((a, b) => dist(a.position, agent.self.position) - dist(b.position, agent.self.position))[0];
    if (!mob) {
      log.push("no Mob near");
      break;
    }
    await agent.send({ name: "command", text: `/g Pulling ${mob.name} (L${mob.level}).` });
    const k = await burn(agent, mob.objectId);
    if (k.ok) kills++;
    else log.push(`${mob.name}: ${k.reason}`);
  }
  console.log(`kills ${kills}, level ${agent.self.level} ${agent.self.levelPermill}‰\n${log.join("\n")}`);
  return succeeded;
}
