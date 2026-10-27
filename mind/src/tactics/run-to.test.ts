import { expect, test } from "bun:test";
import type { Outcome } from "../tactic.ts";
import { settle, TestAgent } from "../test-agent.ts";
import { runTo } from "./run-to.ts";

test("fails at once when its moveTo isn't sent, rather than waiting out the run", async () => {
  const t = new TestAgent();
  t.notSending = "The connection closed first.";
  let outcome: Outcome | undefined;

  void t.agent.run(runTo, { x: 296400, y: 637000, z: 5600 }).then((ended) => (outcome = ended));
  await settle();

  expect(outcome).toEqual({ ok: false, reason: "timeout" });
  expect(t.lines).toEqual(["The moveTo wasn't sent: The connection closed first."]);
});
