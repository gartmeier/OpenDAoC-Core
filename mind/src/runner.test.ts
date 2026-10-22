import { expect, test } from "bun:test";
import { Runner } from "./runner.ts";
import { failed, succeeded, type Agent } from "./tactic.ts";
import { settle, TestAgent } from "./test-agent.ts";
import { ownPosition, status } from "./test-percepts.ts";

// Sits, then waits for ever.
async function sitForEver(agent: Agent) {
  await agent.send({ name: "sit" });
  await agent.waitFor(() => undefined, Number.MAX_SAFE_INTEGER);
  return succeeded;
}

test("stopping leaves the Agent standing still with attack off, and ends the Tactic as stopped", async () => {
  const t = new TestAgent();
  await t.perceive(ownPosition(296000, 642000, 4848));
  const runner = new Runner(t.agent);
  const outcome = runner.start("sitting", sitForEver);
  await t.perceive(status(100, { sitting: true }));
  t.take();

  await runner.stop();

  expect(await outcome).toEqual({ ok: false, reason: "stopped" });
  expect(t.take()).toEqual([{ name: "attack", on: false }, { name: "stop" }, { name: "stand" }]);
  expect(t.lines).toEqual(["Starting sitting.", "sitting failed: stopped."]);
});

test("starting a Tactic stops the one running first", async () => {
  const t = new TestAgent();
  const runner = new Runner(t.agent);
  const first = runner.start("sitting", sitForEver);
  await settle();

  const second = await runner.start("nothing", async () => failed("nothingToDo"));

  expect(await first).toEqual({ ok: false, reason: "stopped" });
  expect(second).toEqual({ ok: false, reason: "nothingToDo" });
  expect(t.take()).toEqual([{ name: "sit" }, { name: "attack", on: false }, { name: "stop" }]);
});

test("stopping with nothing running sends nothing", async () => {
  const t = new TestAgent();

  await new Runner(t.agent).stop();

  expect(t.take()).toEqual([]);
});
