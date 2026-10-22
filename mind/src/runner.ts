import type { Agent, Outcome, Tactic } from "./tactic.ts";

/**
 * Runs one top-level Tactic at a time on the Agent. Starting another stops the one running first.
 * Stopping leaves the Agent standing, with attack off and no run in progress, however the Tactic was interrupted.
 */
export class Runner {
  readonly #agent: Agent;
  #running: Promise<Outcome> | undefined;

  constructor(agent: Agent) {
    this.#agent = agent;
  }

  /** Stops whatever runs, then runs `tactic`, logging how it ended. */
  async start<Args extends unknown[]>(name: string, tactic: Tactic<Args>, ...args: Args): Promise<Outcome> {
    await this.stop();
    this.#agent.log(`Starting ${name}.`);
    const running = this.#agent.run(tactic, ...args);
    this.#running = running;
    const outcome = await running;

    if (this.#running === running)
      this.#running = undefined;

    this.#agent.log(outcome.ok ? `${name} succeeded.` : `${name} failed: ${outcome.reason}.`);
    return outcome;
  }

  /** Stops whatever runs, and leaves the Agent standing still with attack off. Does nothing if nothing runs. */
  async stop(): Promise<void> {
    const running = this.#running;

    if (!running)
      return;

    this.#running = undefined;
    this.#agent.stop();
    await running;
    this.#agent.resume();
    await this.#agent.send({ name: "attack", on: false });
    await this.#agent.send({ name: "stop" });

    if (this.#agent.self.sitting)
      await this.#agent.send({ name: "stand" });
  }
}
