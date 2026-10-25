import { expect, test } from "bun:test";
import { TestAgent } from "../test-agent.ts";
import { dialog } from "../test-percepts.ts";
import { answer } from "./dialog.ts";

test("answer accepts the open Dialog, echoing what the server sent with it, and the Dialog is gone", async () => {
  const t = new TestAgent();
  await t.perceive(dialog("It will cost 2 silver to have your lost constitution restored. Do you accept?"));

  expect(await t.agent.run(answer, true)).toEqual({ ok: true });

  expect(t.take()).toEqual([{ name: "answerDialog", code: "CustomDialog", data1: 0x0101, data2: 1, data3: 0, accept: true }]);
  expect(t.agent.self.dialog).toBeUndefined();
});

test("answer declines the open Dialog", async () => {
  const t = new TestAgent();
  await t.perceive(dialog("Do you accept?"));

  expect(await t.agent.run(answer, false)).toEqual({ ok: true });

  expect(t.take()).toEqual([{ name: "answerDialog", code: "CustomDialog", data1: 0x0101, data2: 1, data3: 0, accept: false }]);
});

test("answer fails with noDialog when no Dialog is open, and sends nothing", async () => {
  const t = new TestAgent();

  expect(await t.agent.run(answer, true)).toEqual({ ok: false, reason: "noDialog" });

  expect(t.take()).toEqual([]);
});
