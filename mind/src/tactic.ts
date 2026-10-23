import { Inventory } from "./inventory.ts";
import type { Percept, PerceptMessage, Primitive, PrimitiveResultMessage } from "./protocol.ts";
import { Self } from "./self.ts";
import { Surroundings } from "./surroundings.ts";

/** How a Tactic ended: it succeeded, or it failed for a reason. */
export type Outcome = { readonly ok: true } | { readonly ok: false; readonly reason: string };

export const succeeded: Outcome = { ok: true };

export function failed(reason: string): Outcome {
  return { ok: false, reason };
}

/** A unit of Mind-side code that pursues an aim by issuing Primitives and running other Tactics (see `CONTEXT.md` and ADR 0008). */
export type Tactic<Args extends unknown[] = []> = (agent: Agent, ...args: Args) => Promise<Outcome>;

/** Time as the Agent sees it. The Mind's is the system clock, and tests use one they move on by hand. */
export interface Clock {
  /** In milliseconds. */
  now(): number;
  /** Calls `callback` once `ms` have passed. Returns what cancels it. */
  schedule(ms: number, callback: () => void): () => void;
}

export const systemClock: Clock = {
  now: () => Date.now(),
  schedule(ms, callback) {
    const timer = setTimeout(callback, ms);
    return () => clearTimeout(timer);
  },
};

/** Thrown out of whatever a Tactic is waiting on once the Agent is stopped. Running a Tactic turns it into a failure with the reason `stopped`. */
export class Stopped extends Error {
  constructor() {
    super("stopped");
  }
}

export interface AgentOptions {
  /** Sends a Primitive. Its result says only whether it was queued (ADR 0004). */
  send: (primitive: Primitive) => Promise<PrimitiveResultMessage>;
  clock?: Clock;
  /** 0 up to but not including 1. */
  random?: () => number;
  /** Where lines for the Human go. */
  log?: (line: string) => void;
}

interface Waiter {
  check(percept: Percept | undefined): boolean;
  stop(): void;
}

/**
 * What a Tactic gets: the Agent's Primitives, its beliefs (Self, Surroundings and Inventory), and a way to wait for its beliefs to change (ADR 0008).
 * Every Percept goes to Self, Surroundings and Inventory first, then to whatever is waiting.
 * Stopping it ends every Tactic running on it: whatever they wait on or send next throws `Stopped`.
 */
export class Agent {
  readonly self = new Self();
  readonly surroundings = new Surroundings();
  readonly inventory = new Inventory();
  readonly clock: Clock;
  readonly random: () => number;
  readonly log: (line: string) => void;
  readonly #send: (primitive: Primitive) => Promise<PrimitiveResultMessage>;
  readonly #waiters = new Set<Waiter>();
  #stopped = false;

  constructor({ send, clock = systemClock, random = Math.random, log = () => {} }: AgentOptions) {
    this.#send = send;
    this.clock = clock;
    this.random = random;
    this.log = log;
  }

  /** Whether the Agent has been stopped since its last `resume`. */
  get stopped(): boolean {
    return this.#stopped;
  }

  /** Takes in a Percept: beliefs first, then whatever is waiting on them. */
  perceive(message: PerceptMessage): void {
    this.self.perceive(message);
    this.surroundings.perceive(message);
    this.inventory.perceive(message);

    for (const waiter of [...this.#waiters])
      waiter.check(message.percept);
  }

  /** Sends a Primitive. Throws `Stopped` once the Agent is stopped. */
  async send(primitive: Primitive): Promise<PrimitiveResultMessage> {
    if (this.#stopped)
      throw new Stopped();

    // Before it goes: the Harness can answer with Percepts before the Primitive's result. A refused one only makes the Inventory think it isn't trading, which a Tactic gets over by talking to the merchant again.
    this.inventory.sent(primitive);
    const result = await this.#send(primitive);

    if (!result.queued)
      this.log(`The ${primitive.name} wasn't sent: ${result.reason}`);

    return result;
  }

  /**
   * Waits until `condition` gives something other than `undefined` or `false`, and gives that, or `undefined` once `timeoutMs` have passed.
   * It's asked at once, with no Percept, then after every Percept, with the Percept, once Self, Surroundings and Inventory have taken it in.
   * Throws `Stopped` once the Agent is stopped.
   */
  waitFor<T>(condition: (percept: Percept | undefined) => T | undefined | false, timeoutMs: number): Promise<T | undefined> {
    if (this.#stopped)
      return Promise.reject(new Stopped());

    return new Promise((resolve, reject) => {
      const finish = (end: () => void) => {
        cancel();
        this.#waiters.delete(waiter);
        end();
      };
      const waiter: Waiter = {
        check: (percept) => {
          const value = condition(percept);

          if (value === undefined || value === false)
            return false;

          finish(() => resolve(value));
          return true;
        },
        stop: () => finish(() => reject(new Stopped())),
      };
      const cancel = this.clock.schedule(timeoutMs, () => finish(() => resolve(undefined)));

      if (!waiter.check(undefined))
        this.#waiters.add(waiter);
    });
  }

  /**
   * Sends a Primitive and waits for what it should bring about, as `waitFor` does.
   * The wait starts before the Primitive goes, because the Harness can answer with Percepts before the Primitive's result.
   */
  async sendAndWait<T>(primitive: Primitive, condition: (percept: Percept | undefined) => T | undefined | false, timeoutMs: number): Promise<T | undefined> {
    const waiting = this.waitFor(condition, timeoutMs);
    // Stopping rejects the wait too, and the send's own `Stopped` is the one that's thrown.
    waiting.catch(() => {});
    await this.send(primitive);
    return waiting;
  }

  /** Runs a Tactic to its end. A Tactic stopped on the way ends failed with the reason `stopped`. */
  async run<Args extends unknown[]>(tactic: Tactic<Args>, ...args: Args): Promise<Outcome> {
    try {
      return await tactic(this, ...args);
    } catch (error) {
      if (error instanceof Stopped)
        return failed("stopped");

      throw error;
    }
  }

  /** Ends every Tactic running on the Agent. */
  stop(): void {
    this.#stopped = true;

    for (const waiter of [...this.#waiters])
      waiter.stop();
  }

  /** Lets Tactics run again after a `stop`. */
  resume(): void {
    this.#stopped = false;
  }
}
