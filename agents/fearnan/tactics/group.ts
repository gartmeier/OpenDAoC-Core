import { type Agent, failed, type Outcome, succeeded } from "../../../mind/src/tactic.ts";
import { distance, headingTowards } from "../../../mind/src/surroundings.ts";
import { rest } from "../../../mind/src/tactics/rest.ts";
import { runTo } from "../../../mind/src/tactics/run-to.ts";
import { bardBuffs, bestHeal, buffSelf, castOn, dropBase, dropWeaker, emergencyHeal, ensurePet, healMe, healPet, kept, petNpc } from "./pet.ts";

const away = (agent: Agent, p: { x: number; y: number }) => {
  const at = agent.self.position;
  return at ? Math.hypot(p.x - at.x, p.y - at.y) : Infinity;
};

/** The other Group member called `name`, as a player in view, with how the group window says it's doing. */
function mate(agent: Agent, name: string) {
  const member = agent.group.named(name);
  const seen = member && agent.surroundings.players.find((p) => p.objectId === member.objectId);
  // After a death and release the server may never send the mate's playerCreate again (see papercuts), so it drops out of
  // Surroundings for good. Stand in with its object ID and the group map's position (my own Z): targeting and casting by ID still work.
  const player = seen ?? (member?.objectId !== undefined && member.position && !member.dead && away(agent, member.position) < 4000
    ? { objectId: member.objectId, position: { ...member.position, z: agent.self.position?.z ?? 0 } } : undefined);
  return member && { member, player };
}

/** The names of the other Group members. */
export function mateNames(agent: Agent): string[] {
  // Self has no name: tell myself apart by object ID (by name it let "Fearnan" in, and I buffed myself as a mate).
  return agent.group.members.filter((m) => m.objectId !== agent.self.objectId && m.name !== "Fearnan").map((m) => m.name).filter((n) => !!n);
}

/** Beyond this my heals say "That target is too far away!" (Dalta at 2247 got spammed with them). */
const HEAL_RANGE = 1900;

/** The other Group member most in need of a heal (lowest health, alive, in reach), else `name`'s. */
function neediest(agent: Agent, name: string) {
  return mateNames(agent).map((n) => mate(agent, n)).filter((m) => m?.player && !m.member.dead && away(agent, m.player.position) < HEAL_RANGE)
    .sort((a, b) => (a!.member.healthPercent ?? 100) - (b!.member.healthPercent ?? 100))[0] ?? mate(agent, name);
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
    const heal = bestHeal(agent, 100 - (m?.member.healthPercent ?? 0));
    if (!m?.player || !heal || (m.member.healthPercent ?? 100) >= below || m.member.dead || away(agent, m.player.position) > HEAL_RANGE) return;
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

/** The buffs I keep on mates: only the spec strength/constitution. Ceol (Bard) gives the base buffs (AF, str, con, dex) and asked me to drop mine. */
const MATE_BUFFS = [/^Increases the target's strength and constitution/];
/** With no living Bard grouped, the base armour and constitution buffs go on mates again (Ceol logged off and they went bare). */
const BASE_MATE_BUFFS = [/^Increases the target's armor factor by/, /^Increases the target's constitution by/];
/** Buffs a mate refused ("already has that effect"), by "spell|mate", until when: don't spend Power on them every upkeep. */
const refused = new Map<string, number>();

/** Puts its armour and constitution buffs on the mate when it's close and alive and lacks them. */
export async function buffMate(agent: Agent, name: string): Promise<void> {
  const m = mate(agent, name);
  if (!m?.player || m.member.dead || !m.member.position || away(agent, m.member.position) > 1200) return;
  for (const kind of bardBuffs(agent) ? MATE_BUFFS : [...MATE_BUFFS, ...BASE_MATE_BUFFS]) {
    const buff = agent.self.skills.filter((s) => s.page === "Spells" && kind.test(s.details?.description ?? "") && s.details?.concentrationCost)
      .sort((a, b) => b.level - a.level)[0];
    if (!buff || kept(agent, buff, name)) continue;
    await dropWeaker(agent, buff, name);
    if ((buff.details?.concentrationCost ?? 0) > agent.self.concentration) continue;
    const key = `${buff.name}|${name}`;
    if ((refused.get(key) ?? 0) > agent.clock.now()) continue;
    await castOn(agent, m.player.objectId, buff.name);
    const said = await agent.waitFor((p) => p?.kind === "message" && /already has that effect/.test(p.text) ? true : undefined, 800);
    if (said) refused.set(key, agent.clock.now() + 300_000);
  }
}

let lastBackOff = 0;
let lastToTank = 0;

/** Classes with no Power bar (it shows 0%): never wait on their Power. */
const NO_POWER = /Blademaster|Hero|Warrior|Armsman|Mercenary|Berserker|Savage|Infiltrator|Shadowblade|Scout/;

/** The nearest Mob within `within` that could fight us (not peaceful, not far below its level), if any. */
export function idleNear(agent: Agent, within: number) {
  return agent.surroundings.npcs
    .filter((n) => n.realm === "None" && !n.peaceful && !n.pet && n.healthPercent > 0 && n.level >= agent.self.level - 3)
    .map((n) => ({ n, d: away(agent, n.position) }))
    .filter((x) => x.d < within)
    .sort((a, b) => a.d - b.d)[0]?.n;
}

/** Mobs attacking it, its Pet or a Group member. */
export function hostile(agent: Agent) {
  const ours = [agent.self.objectId, agent.self.pet?.objectId, ...agent.group.members.map((m) => m.objectId)].filter((id) => id !== undefined);
  // The most hurt first: the one we're already fighting, not an add Orla has mezzed (hitting that breaks the mez).
  return ours.flatMap((id) => agent.surroundings.attacking(id!)).filter((npc) => npc.realm === "None" && npc.healthPercent > 0)
    .sort((a, b) => a.healthPercent - b.healthPercent);
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
export async function groupFight(agent: Agent, mobId: number, name: string, dots = false): Promise<Outcome> {
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

      // Orla /assists me: keep the Mob targeted while it lives, back on it at once after a heal on someone else.
      if (agent.self.target !== mobId) await agent.send({ name: "target", objectId: mobId });

      // With a tank in the group, the Pet joins only once the tank has hit the Mob (else the Pet takes it and costs heals).
      const hasTank = mateNames(agent).some((n) => NO_POWER.test(agent.group.named(n)?.className ?? "") && !agent.group.named(n)?.dead);
      if (agent.self.pet && !sent && (!hasTank || mob.healthPercent < 95)) {
        await agent.send({ name: "target", objectId: mobId });
        await agent.send({ name: "commandPet", order: "Attack" });
        sent = true;
      }

      const me = agent.self.objectId!;
      // In a trio: whoever is hurt most, so the tank and the nuker both get heals.
      const isOn = (x: ReturnType<typeof mate>) => !!x?.player && agent.surroundings.attacking(x.player.objectId).some((npc) => npc.objectId === mobId);
      // A non-tank mate the Mob turned on comes first; else whoever is hurt most.
      const victim = mateNames(agent).map((n) => mate(agent, n)).find((x) => isOn(x) && !x!.member.dead && !NO_POWER.test(x!.member.className));
      const m = victim ?? neediest(agent, name);
      const onMe = agent.surroundings.attacking(me).some((npc) => npc.objectId === mobId);
      const onMate = isOn(m);
      // Not on a tank (no Power bar): it holds the Mob, and I stay back to heal it.
      // With a tank I never melee: chasing a Mob that turned on Orla dragged me off camp. The tank taunts it; I heal.
      const melee = !hasTank && (onMe || !!victim || !agent.self.pet);

      // Any Mob on me interrupts every heal (an add sat on me 60 s and killed me): run to the tank so it can taunt it off me.
      const onMeAny = agent.surroundings.attacking(me).some((npc) => npc.healthPercent > 0);
      const tank = mateNames(agent).map((n) => mate(agent, n)).find((x) => x?.player && !x.member.dead && NO_POWER.test(x.member.className));
      if (onMeAny && tank && agent.self.position && distance(agent.self.position, tank.player!.position) > 200) {
        if (agent.clock.now() - lastToTank > 2_000) {
          lastToTank = agent.clock.now();
          await striker.halt();
          const p = tank.player!.position;
          await agent.send({ name: "moveTo", x: p.x, y: p.y, z: p.z, within: 100 });
        }
        await agent.waitFor(() => undefined, 500);
        continue;
      }
      // The tank fighting out of heal range (it chased the Mob off the camp): close in just enough (1500), or it dies unhealed.
      // Not further: groupGrind runs back to the camp after the fight, and Orla follows me if I wander.
      if (tank && agent.self.position && distance(agent.self.position, tank.player!.position) > HEAL_RANGE - 200 && agent.clock.now() - lastToTank > 2_000) {
        lastToTank = agent.clock.now();
        await striker.halt();
        const p = tank.player!.position;
        await agent.send({ name: "moveTo", x: p.x, y: p.y, z: p.z, within: HEAL_RANGE - 400 });
        await agent.waitFor(() => undefined, 500);
        continue;
      }

      // Heals: the mate first, then itself, then the Pet, one cast at a time so a mate the Mob turns on comes first at once.
      // Out of melee they don't get interrupted.
      // Only with a heal it has the Power for: else the `continue`s would spin without awaiting anything, starve the connection's
      // heartbeat and get the Character dropped linkdead.
      const canHeal = !!bestHeal(agent);
      // Nature's Appreciation: instant, so a Mob on me can't interrupt it. For me or the mate in real danger only (15 min reuse).
      const instant = emergencyHeal(agent);
      if (instant && !agent.self.cooldowns.has(instant.name)) {
        const mateLow = m?.player && away(agent, m.player.position) < HEAL_RANGE && !m.member.dead && (m.member.healthPercent ?? 100) < 30;
        const meLow = agent.self.healthPercent < 30;
        if (mateLow || meLow) {
          await striker.halt();
          const reply = await castOn(agent, mateLow ? m!.player!.objectId : me, instant.name);
          if (reply?.startsWith("You cast")) continue;
        }
      }
      if (canHeal && m?.player && away(agent, m.player.position) < HEAL_RANGE && !m.member.dead && (m.member.healthPercent ?? 100) < (onMate ? 85 : 50)) { await striker.halt(); await healMate(agent, m.member.name, onMate ? 95 : 70, 1); continue; }
      if (canHeal && agent.self.healthPercent < 45 && !onMe) { await striker.halt(); await healMe(agent, 70, 1); continue; }
      // Even with it on me: a maiden took me to 13% with 93% Power while I only meleed. A cast may be interrupted, but try.
      if (canHeal && agent.self.healthPercent < 40 && onMe) { await striker.halt(); await healMe(agent, 60, 1); continue; }
      const pet = petNpc(agent);
      if (canHeal && pet && pet.healthPercent < 45 && !onMe) { await striker.halt(); await healPet(agent, 70, 1); continue; }

      // Its damage over time on the Mob (if `dots`), renewed as it runs out, while there's Power to spare for heals.
      // Off by default: on a L10 sheerie it took the Mob off the L6 Pet and onto me (hits of 31-41).
      const dot = dots ? bestDot(agent) : undefined;
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
export async function upkeep(agent: Agent, name: string, stay = false): Promise<Outcome | "rested"> {
  // A dead mate in reach: resurrect it (Regrowth 16 brings Minor Reconstitution), so it needn't release and run back.
  const rez = agent.self.skills.filter((sp) => sp.page === "Spells" && /Reconstitution/.test(sp.name) && (sp.details?.powerCost ?? 0) <= agent.self.power)
    .sort((a, b) => b.level - a.level)[0];
  for (const n of mateNames(agent)) {
    const member = agent.group.named(n);
    if (!rez || !member?.dead || member.objectId === undefined || !member.position || away(agent, member.position) > 3000) continue;
    const corpse = agent.surroundings.players.find((pl) => pl.objectId === member.objectId);
    if (!corpse) continue;
    if (away(agent, corpse.position) > 1300) { await runTo(agent, corpse.position, 1000); }
    const reply = await castOn(agent, member.objectId, rez.name);
    if (reply?.startsWith("You cast")) await agent.waitFor(() => !agent.group.named(n)?.dead || undefined, 15_000);
  }
  const pet = await ensurePet(agent);
  if (!pet.ok && pet.reason !== "petOutOfView") return pet;
  // Ceol (Bard) gives the base buffs: take mine off me and the Pet so hers land.
  await dropBase(agent);
  await buffSelf(agent);
  for (const n of mateNames(agent)) await buffMate(agent, n);
  for (const n of mateNames(agent)) await healMate(agent, n, 90);
  await healPet(agent, 85);
  await healMe(agent, 85);

  const m = mate(agent, name);
  if (!m) return failed("notGrouped");
  if (m.member.dead) return failed("mateDead");

  // Rest until it is full, and give the mate time to get its Power back.
  const others = mateNames(agent).map((n) => agent.group.named(n)).filter((x) => x && !x.dead && x.position && away(agent, x.position) < 3000);
  if (agent.self.healthPercent < 90 || agent.self.powerPercent < 70 || others.some((x) => (!NO_POWER.test(x!.className) && (x!.powerPercent ?? 100) < 60) || (x!.healthPercent ?? 100) < 80)) {
    // Not next to an idle Mob: one that wandered up to us resting 150 from the siabra camp killed us both. Back off 1500 from it first.
    const near = idleNear(agent, 1500);
    // Once per 30 s at most, so it doesn't bounce between Mobs for ever: else it rests where it is.
    // Not at a camp the tank pulls to (`stay`): there I keep the spot (backing off walked me off camp, and Orla followed).
    if (!stay && near && agent.clock.now() - lastBackOff > 30_000) {
      lastBackOff = agent.clock.now();
      const at = agent.self.position!, d = Math.max(1, away(agent, near.position));
      await runTo(agent, { x: Math.round(at.x + (at.x - near.position.x) / d * (1600 - d)), y: Math.round(at.y + (at.y - near.position.y) / d * (1600 - d)), z: at.z }, 100);
      return "rested";
    }
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
 * With a `camp`, it goes there first and pulls only Mobs within 3500 of it. With `until`, it stops once that holds between fights.
 */
export async function groupGrind(agent: Agent, name: string, minutes: number, stopAtLevel: number, range = [-1, 2], avoid: string[] = ["mudman"], lead = true, camp?: { x: number; y: number; z: number }, until?: () => boolean): Promise<Outcome & { kills?: number }> {
  const end = agent.clock.now() + minutes * 60_000;
  let kills = 0, idle = 0;
  const shunned = new Map<number, number>();
  while (agent.clock.now() < end && agent.self.level < stopAtLevel) {
    // A timer, not a microtask: a loop that awaits nothing else must still let the connection breathe.
    await agent.waitFor(() => undefined, 50);
    // Stops early once `until` holds and no fight is going on (e.g. a task Mob got killed).
    if (until?.() && !hostile(agent)[0]) return { ...succeeded, kills };
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

    // Following with a camp: the puller brings the Mobs, so I never leave the camp spot except to heal a fight,
    // and go back to it first thing after one (before resting: I sat 1000 off camp for 3 min resting where a fight ended).
    if (!lead && camp && away(agent, camp) > 250) { await runTo(agent, camp, 100); continue; }
    const ready = await upkeep(agent, name, !lead && !!camp);
    if (ready === "rested") continue;
    if (!ready.ok) return { ...ready, kills };
    const m = mate(agent, name)!;

    // Following with a `camp`: the puller brings Mobs back to it, so wait there instead of trailing the puller onto the Mobs.
    if (!lead && camp) {
      if (away(agent, camp) > 250) { await runTo(agent, camp, 100); continue; }
      await agent.waitFor(() => hostile(agent)[0], 15_000);
      continue;
    }

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

    // A camp keeps it where the Mobs pay: run there first, and pull only Mobs near it (else it drifts onto low Mobs on the way).
    // In legs of at most 2000, so the leash above sees a mate that doesn't follow (one long run left Orla 26000 behind).
    if (camp && away(agent, camp) > 2500) {
      const at = agent.self.position!, d = away(agent, camp);
      const leg = d > 2300 ? { x: Math.round(at.x + (camp.x - at.x) / d * 2000), y: Math.round(at.y + (camp.y - at.y) / d * 2000), z: camp.z } : camp;
      await runTo(agent, leg, 300);
      continue;
    }
    const level = agent.self.level;
    const fit = agent.surroundings.npcs
      .filter((npc) => npc.realm === "None" && !npc.peaceful && !npc.pet && npc.healthPercent > 0 && !avoid.includes(npc.name)
        && npc.level >= level + range[0] && npc.level <= level + range[1] && (shunned.get(npc.objectId) ?? 0) < 2
        && (!camp || Math.hypot(npc.position.x - camp.x, npc.position.y - camp.y) < 3500))
      .map((npc) => ({ npc, away: away(agent, npc.position) }));
    const mob = fit
      // The Pet attacks only within 2000 units; Orla asked for 1500 so her assist is in range at once.
      .filter((x) => x.away < 1500)
      // Alone first: a Mob with another within 600 tends to bring it (an add killed us both at the siabras).
      .map((x) => ({ ...x, crowded: agent.surroundings.npcs.some((o) => o.objectId !== x.npc.objectId && o.realm === "None" && !o.peaceful && !o.pet && o.healthPercent > 0 && o.level >= level - 3 && Math.hypot(o.position.x - x.npc.position.x, o.position.y - x.npc.position.y) < 600) }))
      // Higher Mobs pay about twice as much (L8 rowdy ~1000 XP, L5 ~520), so the highest first, then the nearest.
      .sort((a, b) => Number(a.crowded) - Number(b.crowded) || b.npc.level - a.npc.level || a.away - b.away)[0];
    // The camp is picked clean nearby: move up to 1000 short of the nearest fitting Mob farther off (the mate follows).
    const farther = !mob && fit.filter((x) => x.away < 5000).sort((a, b) => a.away - b.away)[0];
    if (farther) {
      const at = agent.self.position!, p = farther.npc.position;
      await runTo(agent, { x: Math.round(p.x - (p.x - at.x) / farther.away * 1000), y: Math.round(p.y - (p.y - at.y) / farther.away * 1000), z: p.z }, 150);
      continue;
    }
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
