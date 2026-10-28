import { type Agent, failed, type Outcome, succeeded } from "../../../mind/src/tactic.ts";
import { answer } from "../../../mind/src/tactics/dialog.ts";
import { cast } from "../../../mind/src/tactics/cast.ts";
import { kill } from "../../../mind/src/tactics/kill.ts";
import { rest } from "../../../mind/src/tactics/rest.ts";
import { runTo } from "../../../mind/src/tactics/run-to.ts";
import { assistTarget } from "./assist.ts";

type Point = { x: number; y: number; z?: number };

const dist = (a: Point | undefined, b: Point | undefined) => (a && b ? Math.hypot(a.x - b.x, a.y - b.y) : Infinity);

const headingTo = (agent: Agent, p: Point) => {
  const at = agent.self.position!;
  return (Math.round((Math.atan2(-(p.x - at.x), p.y - at.y) * 4096) / (2 * Math.PI)) + 4096) % 4096;
};

const damageOf = (description = "") => Number(/Inflicts (\d+)/.exec(description)?.[1] ?? 0);

/**
 * The Shadow Control nuke (no recast) that inflicts the most. Only "Shadow…" Spells: they are my specced Light line, while the
 * baseline Ethereal Blast (29 on paper) hit for 11–21 with Mana at 1, against 37–41 for Shadowblast (26 on paper).
 */
const nuke = (agent: Agent) =>
  agent.self.skills
    .filter((s) => s.page === "Spells" && s.name.startsWith("Shadow") && s.details?.description.startsWith("Inflicts") && !(s.details?.recastSeconds))
    .sort((a, b) => damageOf(b.details?.description) - damageOf(a.details?.description))[0];

let partnerZ: number | undefined;

/**
 * Where the partner is, always with a real Z (the Human's rule for every run): in view, its own position; else the group map's x and y,
 * which carry no height, with the Z it last had in view (or mine, if it was never in view).
 */
export const partnerAt = (agent: Agent, name: string): { x: number; y: number; z: number } | undefined => {
  const seen = agent.surroundings.players.find((p) => p.name === name)?.position;
  if (seen) {
    partnerZ = seen.z;
    return seen;
  }
  const map = agent.group.named(name)?.position;
  return map && { ...map, z: partnerZ ?? agent.self.position!.z };
};

const mezzedAt = new Map<number, number>();

/**
 * Mesmerizes an add: a second Mob fighting one of us, me or the pet, while I burn `objectId`. Paralyzing Wind holds it 10 s
 * (radius 350, so it may catch the main Mob too; the next nuke wakes that one). Recast every ~9 s. Ends true if it cast.
 */
async function mezAdd(agent: Agent, objectId: number): Promise<boolean> {
  // Paralyzing Veil (Light 15) holds 15 s; Paralyzing Wind 10 s.
  const mez = agent.self.skills.find((s) => s.name === "Paralyzing Veil") ?? agent.self.skills.find((s) => s.name === "Paralyzing Wind");
  if (!mez) return false;
  const me = agent.self.objectId;
  const ours = new Set<number | undefined>([me, ...agent.group.members.map((m) => m.objectId),
    ...agent.surroundings.npcs.filter((p) => p.realm === "Hibernia").map((p) => p.objectId)]);
  // An NPC under 75% health resists every mez ("Your target is enraged", CCSpellHandler): only mez fresh adds.
  const add = agent.surroundings.npcs.find((n) => n.objectId !== objectId && n.realm === "None" && n.healthPercent >= 75 &&
    ours.has(n.targetId) && dist(n.position, agent.self.position) < 1400 && Date.now() - (mezzedAt.get(n.objectId) ?? 0) > 9000);
  if (!add || agent.self.power < (mez.details?.powerCost ?? 3)) return false;
  // Being hit interrupts every mez (5 in a row at 15): only mez while nothing is in melee on me.
  if (agent.surroundings.npcs.some((n) => n.targetId === me && n.healthPercent > 0 && dist(n.position, agent.self.position) < 300)) return false;
  await agent.send({ name: "face", heading: headingTo(agent, add.position) });
  const out = await cast(agent, mez.name, add.objectId);
  if (out.ok) mezzedAt.set(add.objectId, Date.now());
  console.log(`mez ${add.name}: ${out.ok ? "ok" : out.reason}`);
  return out.ok;
}

/** Nukes `objectId` until it dies, from range. Keeps casting even when it's on me (the pet should take it back). */
export async function burn(agent: Agent, objectId: number): Promise<Outcome> {
  const spell = nuke(agent);
  if (!spell) return failed("noSpell");
  const range = (spell.details?.range ?? 1500) - 150;
  const cost = spell.details?.powerCost ?? 0;
  const mob = () => agent.surroundings.npcs.find((n) => n.objectId === objectId);

  let interrupted = 0;
  for (let tries = 0; tries < 30; tries++) {
    if (agent.self.dead) return failed("died");
    const m = mob();
    if (!m || m.healthPercent <= 0) return succeeded;
    if (agent.self.power < cost) return failed("noPower");
    if (dist(agent.self.position, m.position) > range) {
      const ran = await runTo(agent, m.position, range - 200);
      if (!ran.ok) return ran;
    }
    if (await mezAdd(agent, objectId)) continue;
    await agent.send({ name: "face", heading: headingTo(agent, m.position) });
    const me = agent.self.objectId;
    const onMe = me !== undefined && m.targetId === me && dist(agent.self.position, m.position) < 300;
    // In melee on me, a nearly dead Mob interrupts every cast: finish it with the staff instead (a L11 sheerie killed me at 2%).
    // But not when it hits hard: a L14 siabra at 9% hit me for 70 a swing while the staff did 12, and killed me. Then keep casting.
    // And never against a Mob above my level: a L18 parthanan at 13% killed me while the staff did 10 a swing.
    if (onMe && interrupted >= 1 && m.healthPercent <= 25 && agent.self.healthPercent > 60 && m.level < agent.self.level) return await kill(agent, objectId);
    const out = await cast(agent, spell.name, objectId, onMe && agent.self.power >= 2 * cost && !agent.self.cooldowns.has("Quickcast") ? { quickCast: true } : undefined);
    if (!out.ok && out.reason === "interrupted") interrupted++;
    if (!out.ok && out.reason === "tooFar") await runTo(agent, m.position, range - 400);
    if (!out.ok && !["interrupted", "noEffect", "tooFar", "mustWait", "noTarget"].includes(out.reason)) {
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
export async function duo(agent: Agent, partner: string, levels: (l: number) => number[], minutes: number, untilLevel = 99, avoid: string[] = [], lead = false, joinBelow = 75, until: () => boolean = () => false, puller?: string): Promise<Outcome> {
  const end = Date.now() + minutes * 60_000;
  let kills = 0;
  const picks: Record<string, number> = {};
  let assistedId: number | undefined;
  let assistedAt = 0;
  const log: string[] = [];
  while (Date.now() < end && agent.self.level < untilLevel && !until()) {
    if (agent.self.dead) return failed("died");
    // A relog drops me from the Group: take the partner's invite again.
    const d = agent.self.dialog;
    if (d?.code === "GroupInvite" && d.text.startsWith(partner)) await answer(agent, true);
    const me = agent.self.objectId;
    const home = partnerAt(agent, partner);
    const party = new Set(agent.group.members.map((m) => m.objectId));

    // Join a fight on the bear or the partner only once the bear has hit it a while, so the bear keeps the aggro.
    // Anything on me I fight at once.
    const near = (n: { position: Point }) => dist(n.position, agent.self.position) < 2000;
    const live = agent.surroundings.npcs.filter((n) => n.realm === "None" && n.healthPercent > 0 && n.targetId !== undefined && near(n));
    const onMe = live.find((n) => n.targetId === me);
    // A Mob counts as ours when it fights one of us or the pet, or when the pet fights it (the Mob's own target can lag).
    // The partner's pet: any controlled Hibernian NPC (I have none). No radius: the pet may pull from 2000+ away of us,
    // and a fight it is in is ours wherever it is, within reach once I run up (a L14 siabra died to the lynx 2300 away unhelped).
    const pets = agent.surroundings.npcs.filter((p) => p.realm === "Hibernia" && (p.pet || dist(p.position, home) < 1500));
    const petTargets = new Set(pets.map((p) => p.targetId));
    // After a death the server may never send the partner and its pet into my view again: then a hurt Mob fighting
    // something I can't see, next to the partner's map position, is the pet's.
    const known = (id?: number) => !!id && (agent.surroundings.npcs.some((o) => o.objectId === id) || agent.surroundings.players.some((o) => o.objectId === id));
    const unseenFight = (n: { targetId?: number; position: Point }) => !!n.targetId && !known(n.targetId) && dist(n.position, home) < 1200;
    const reach = (n: { position: Point }) => dist(n.position, agent.self.position) < 4000;
    const onUs = agent.surroundings.npcs.find((n) => n.realm === "None" && n.healthPercent > 0 && reach(n) && n.healthPercent <= joinBelow &&
      (party.has(n.targetId!) || pets.some((p) => p.objectId === n.targetId) || petTargets.has(n.objectId) || unseenFight(n)));
    // With a puller: assist the puller's target (/assist), once the pet or the puller has it engaged (or it's hurt).
    // Anything in melee on me comes first: being hit interrupts every cast anyway. Fallback: the pet's target, then whatever is on one of us.
    let assisted: typeof onUs;
    const inMelee = onMe && dist(onMe.position, agent.self.position) < 300;
    if (puller && !inMelee) {
      // Send /assist only when a fight starts or my assisted Mob died, at most every 3 s: not on every pass (it spammed 400 a session).
      const fighting = agent.surroundings.npcs.some((n) => n.realm === "None" && n.healthPercent > 0 && reach(n) &&
        (party.has(n.targetId) || pets.some((p) => p.objectId === n.targetId) || petTargets.has(n.objectId)));
      const last = agent.surroundings.npcs.find((n) => n.objectId === assistedId && n.healthPercent > 0);
      if (!last && fighting && Date.now() - assistedAt > 3000) {
        assistedAt = Date.now();
        assistedId = await assistTarget(agent, puller);
      }
      const id = last ? last.objectId : assistedId;
      const m = id === undefined ? undefined : agent.surroundings.npcs.find((n) => n.objectId === id && n.healthPercent > 0);
      const engaged = !!m && (m.healthPercent <= joinBelow || party.has(m.targetId) || pets.some((p) => p.objectId === m.targetId));
      if (m && engaged && reach(m)) assisted = m;
    }
    const busy = assisted ?? onMe ?? onUs;
    if (busy) {
      const how = assisted ? "assist" : busy === onMe ? "onMe" : "fallback";
      picks[how] = (picks[how] ?? 0) + 1;
      const k = await burn(agent, busy.objectId);
      if (k.ok) kills++;
      else log.push(`${busy.name}: ${k.reason}`);
      continue;
    }

    // Rest when I'm low, and sit with the partner while it is: once, waiting on beliefs, not on a loop of sends.
    const f = agent.group.named(partner);
    const partnerLow = !!f && ((f.powerPercent ?? 100) < 40 || (f.healthPercent ?? 100) < 60);
    if (agent.self.powerPercent < 50 || agent.self.healthPercent < 70 || partnerLow) {
      // No "/g rest": the partner sees my Power in its group window, and every /g line aborts its snippet.
      await rest(agent, 100);
      if (!agent.self.sitting) await agent.send({ name: "sit" } as never);
      await agent.waitFor(() => {
        const g = agent.group.named(partner);
        return !g || ((g.powerPercent ?? 100) >= 90 && (g.healthPercent ?? 100) >= 90) || agent.surroundings.npcs.some((n) => n.targetId === agent.self.objectId) || undefined;
      }, 60_000);
      continue;
    }

    // Keep my own AF buff up (+71 AF, 20 min); it beats the partner's Bark Skin.
    const field = agent.self.skills.find((s) => s.page === "Spells" && /Powerfield/.test(s.name));
    if (field && !agent.self.effects.some((e: any) => /Powerfield|energy shield/i.test(e.name ?? String(e))) && agent.self.power >= (field.details?.powerCost ?? 8)) {
      const out = await cast(agent, field.name, agent.self.objectId!);
      if (out.ok) await agent.waitFor(() => undefined, 2000);
      else console.log(`powerfield: ${out.reason}`);
    }

    // Stay with the partner.
    if (home && dist(agent.self.position, home) > 300) {
      await runTo(agent, home, 150);
      continue;
    }

    if (!lead) {
      // The partner pulls with the pet: wait for a fight to join.
      await agent.waitFor(() => undefined, 1000);
      continue;
    }
    const wanted = levels(agent.self.level);
    const mob = agent.surroundings.npcs
      .filter((n) => n.realm === "None" && !n.peaceful && n.healthPercent > 0 && wanted.includes(n.level) && !avoid.includes(n.name) && dist(n.position, home ?? agent.self.position) < 2600)
      .sort((a, b) => dist(a.position, agent.self.position) - dist(b.position, agent.self.position))[0];
    if (!mob) {
      log.push("no Mob near");
      break;
    }
    // No /g per pull: each line aborts the partner's snippet. Agree once that I pull, then the pet picks up what reaches us.
    const k = await burn(agent, mob.objectId);
    if (k.ok) kills++;
    else log.push(`${mob.name}: ${k.reason}`);
  }
  console.log(`picks ${JSON.stringify(picks)}`);
  console.log(`kills ${kills}, level ${agent.self.level} ${agent.self.levelPermill}‰\n${log.join("\n")}`);
  return succeeded;
}
