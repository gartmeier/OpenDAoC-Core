// An Agent for Tactic tests: it records the Primitives sent, takes Percepts from the test, and runs on a clock the test moves on by hand (ADR 0008).
import type { Percept, Primitive } from "./protocol.ts";
import { Agent, type Clock } from "./tactic.ts";
import { message } from "./test-percepts.ts";

/** Time that passes only when the test says so. */
export class FakeClock implements Clock {
  #now = 0;
  #timers: { at: number; callback: () => void }[] = [];

  now(): number {
    return this.#now;
  }

  schedule(ms: number, callback: () => void): () => void {
    const timer = { at: this.#now + ms, callback };
    this.#timers.push(timer);
    return () => {
      this.#timers = this.#timers.filter((other) => other !== timer);
    };
  }

  /** Moves time on by `ms`, firing each timer due on the way, in order, and letting what it wakes up run before the next. */
  async advance(ms: number): Promise<void> {
    const end = this.#now + ms;

    while (true) {
      const next = this.#timers.filter((timer) => timer.at <= end).sort((a, b) => a.at - b.at)[0];

      if (!next)
        break;

      this.#timers = this.#timers.filter((timer) => timer !== next);
      this.#now = next.at;
      next.callback();
      await settle();
    }

    this.#now = end;
    await settle();
  }
}

/** Lets everything that's ready to run, run. */
export async function settle(): Promise<void> {
  await new Promise((resolve) => setImmediate(resolve));
}

export class TestAgent {
  readonly clock = new FakeClock();
  readonly sent: Primitive[] = [];
  readonly lines: string[] = [];
  readonly agent: Agent;
  /** While set, every Primitive comes back not sent, for this reason. */
  notSending: string | undefined;
  #elapsedMs = 0;

  /** `random` gives these numbers in turn, then 0.5. */
  constructor(random: number[] = []) {
    this.agent = new Agent({
      send: async (primitive) => {
        this.sent.push(primitive);
        return this.notSending === undefined
          ? { type: "primitiveResult", id: this.sent.length, queued: true }
          : { type: "primitiveResult", id: this.sent.length, queued: false, reason: this.notSending };
      },
      clock: this.clock,
      random: () => random.shift() ?? 0.5,
      log: (line) => this.lines.push(line),
    });
  }

  /** Each Percept in turn, then lets what it wakes up run. */
  async perceive(...percepts: Percept[]): Promise<void> {
    for (const percept of percepts) {
      this.agent.perceive(message(percept, (this.#elapsedMs += 100)));
      await settle();
    }
  }

  /** The Primitives sent since the last call. */
  take(): Primitive[] {
    return this.sent.splice(0);
  }
}
