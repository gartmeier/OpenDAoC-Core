import { type Agent, type Outcome } from "../../../mind/src/tactic.ts";
import { kill } from "../../../mind/src/tactics/kill.ts";

/** What one fight cost and earned, for judging Tactics. */
export interface FightStats {
  outcome: Outcome;
  seconds: number;
  hpLost: number;
  performed: Record<string, number>;
  failed: Record<string, number>;
  fatigued: number;
  parries: number;
}

/**
 * Kills the Mob with `kill`, using Styles: answers each parry with Return Blade, and keeps the best filler Style queued
 * while endurance is above `fillerEndurance` percent (it's the backup when Return Blade comes up).
 * The server forgets a queued Style after each swing, so it queues again after each one.
 */
export async function fight(agent: Agent, mobId: number, fillerEndurance = 25): Promise<FightStats> {
  const started = Date.now();
  const hpBefore = agent.self.healthPercent;
  const stats: FightStats = { outcome: { ok: true }, seconds: 0, hpLost: 0, performed: {}, failed: {}, fatigued: 0, parries: 0 };
  const has = (name: string) => agent.self.skills.some((s) => s.page === "Styles" && s.name === name);
  const reactive = has("Return Blade") ? "Return Blade" : undefined;
  // The filler is the Style that needs nothing to open and hits hardest: Taunting Blade (116) beats Shining Blade (8) at Blades 6.
  const filler = agent.self.skills
    .filter((s) => s.page === "Styles" && s.details?.opening === "Offensive" && s.details.openingResult === "Any")
    .sort((a, b) => (b.details!.openingDamage ?? 0) - (a.details!.openingDamage ?? 0))[0]?.name;

  // A follow-up that opens after the reactive Style lands, such as Lunging Blade (174) after Return Blade.
  const followup = reactive && agent.self.skills.find((s) => s.page === "Styles" && s.details?.openingStyle === reactive)?.name;

  let fighting = true;
  const killing = kill(agent, mobId).finally(() => { fighting = false; });
  let fillerQueued = false;

  while (fighting) {
    if (filler && !fillerQueued && agent.self.endurancePercent > fillerEndurance) {
      await agent.send({ name: "useStyle", style: filler });
      fillerQueued = true;
    }
    const text = await agent.waitFor((p) => {
      if (!fighting) return "done";
      if (p?.kind !== "message") return undefined;
      return /attacks you and you parry the blow|You perform your|You fail to execute your|too fatigued|^You (attack|miss|enter combat mode)/.test(p.text) ? p.text : undefined;
    }, 5_000);
    if (typeof text !== "string" || text === "done") continue;

    if (/attacks you and you parry/.test(text)) {
      stats.parries++;
      if (reactive) await agent.send({ name: "useStyle", style: reactive });
    } else if (/^You (attack|miss|enter combat mode)/.test(text)) {
      // The server forgets a queued Style after every swing, a miss too, and whenever attack stops.
      fillerQueued = false;
    } else if (/too fatigued/.test(text)) {
      stats.fatigued++;
      fillerQueued = false;
    } else {
      const m = /You (perform|fail to execute) your (.+?) perfectly/.exec(text);
      if (m) {
        const bucket = m[1] === "perform" ? stats.performed : stats.failed;
        bucket[m[2]] = (bucket[m[2]] ?? 0) + 1;
        if (m[2] === filler) fillerQueued = false;
        if (m[1] === "perform" && m[2] === reactive && followup) await agent.send({ name: "useStyle", style: followup });
      }
    }
  }

  stats.outcome = await killing;
  // A Style queued as the Mob dies turns attack on again with no target, and then the next NPC I target (a guard, a merchant) gets swung at.
  if (!agent.stopped) await agent.send({ name: "attack", on: false });
  stats.seconds = Math.round((Date.now() - started) / 1000);
  stats.hpLost = hpBefore - agent.self.healthPercent;
  return stats;
}

/** One line for a log. */
export function describe(stats: FightStats): string {
  const styles = Object.entries(stats.performed).map(([k, v]) => `${k}×${v}`).join(" ");
  const fails = Object.entries(stats.failed).map(([k, v]) => `${k}×${v}`).join(" ");
  return `${stats.outcome.ok ? "ok" : stats.outcome.reason} ${stats.seconds}s hp-${stats.hpLost}% parries ${stats.parries} styles[${styles}]${fails ? ` failed[${fails}]` : ""}${stats.fatigued ? ` fatigued ${stats.fatigued}` : ""}`;
}
