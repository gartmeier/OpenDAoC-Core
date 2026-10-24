import { expect, test } from "bun:test";
import { settle, TestAgent } from "../test-agent.ts";
import { arrived, npcCreate, ownPosition, popup, stopped } from "../test-percepts.ts";
import { WHISPER_CLOSE_IN, WHISPER_TIMEOUT_MS, whisper } from "./whisper.ts";

const EA = 0x2c60;
const AT = { x: 351072, y: 552741, z: 5109 };

// Greeen 300 units from Ea, the Blademaster trainer, unless he's elsewhere.
async function nearEa(at = { x: AT.x + 300, y: AT.y, z: AT.z }) {
  const t = new TestAgent();
  await t.perceive(ownPosition(at.x, at.y, at.z), npcCreate(EA, "Ea", AT.x, AT.y, AT.z, { peaceful: true, realm: "Hibernia" }));
  return t;
}

async function started<T>(t: TestAgent, running: Promise<T>) {
  await settle();
  return { t, outcome: running };
}

test("whisper targets the NPC, whispers the keyword and succeeds once the NPC answers", async () => {
  const t = await nearEa();
  const { outcome } = await started(t, t.agent.run(whisper, EA, "Blademaster"));
  expect(t.take()).toEqual([{ name: "target", objectId: EA }, { name: "command", text: "/whisper Blademaster" }]);

  await t.perceive(popup("Ea says, \"Very well, Greeen. Congratulations, from this day forth, you are a Blademaster.\""));

  expect(await outcome).toEqual({ ok: true });
});

test("whisper fails with outOfView, whispering nothing, to an NPC it doesn't see", async () => {
  const t = await nearEa();

  expect(await t.agent.run(whisper, 0x999, "Blademaster")).toEqual({ ok: false, reason: "outOfView" });
  expect(t.take()).toEqual([]);
});

test("whisper runs within whispering range of an NPC that's too far, then whispers", async () => {
  const t = await nearEa({ x: AT.x + 1000, y: AT.y, z: AT.z });
  await started(t, t.agent.run(whisper, EA, "Blademaster"));
  expect(t.take()).toEqual([{ name: "moveTo", ...AT, within: WHISPER_CLOSE_IN }]);

  await t.perceive(arrived(AT.x + WHISPER_CLOSE_IN, AT.y, AT.z));

  expect(t.take()).toEqual([{ name: "target", objectId: EA }, { name: "command", text: "/whisper Blademaster" }]);
});

test("whisper fails with tooFar, whispering nothing, when it can't get within range", async () => {
  const t = await nearEa({ x: AT.x + 1000, y: AT.y, z: AT.z });
  const { outcome } = await started(t, t.agent.run(whisper, EA, "Blademaster"));
  t.take();

  await t.perceive(stopped(AT.x + 900, AT.y, AT.z, "Unreachable"));

  expect(await outcome).toEqual({ ok: false, reason: "tooFar" });
  expect(t.take()).toEqual([]);
});

test("whisper fails with timeout when the NPC doesn't answer", async () => {
  const t = await nearEa();
  const { outcome } = await started(t, t.agent.run(whisper, EA, "Mentalist"));

  await t.perceive(popup("Eadan says, \"Well met, Greeen.\""));
  await t.clock.advance(WHISPER_TIMEOUT_MS);

  expect(await outcome).toEqual({ ok: false, reason: "timeout" });
});
