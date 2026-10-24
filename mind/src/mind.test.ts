import { expect, test } from "bun:test";
import { mkdtemp } from "node:fs/promises";
import { tmpdir } from "node:os";
import { join } from "node:path";
import { Mind, type MindOptions } from "./mind.ts";
import type { Percept, Primitive } from "./protocol.ts";
import { FakeClock, settle } from "./test-agent.ts";
import { encumbrance, inventory, item, message, money, npcCreate, ownPosition, player, playerCreate, points, sayLine, status } from "./test-percepts.ts";

// A Mind on a clock the test moves on by hand, recording the Primitives it sends and the Journal it writes.
class TestMind {
  readonly clock = new FakeClock();
  readonly sent: Primitive[] = [];
  readonly journal: string[] = [];
  readonly mind: Mind;
  #elapsedMs = 0;

  constructor(options: Partial<MindOptions> = {}) {
    this.mind = new Mind({
      character: "Greeen",
      folder: import.meta.dir,
      send: async (primitive) => {
        this.sent.push(primitive);
        return { type: "primitiveResult", id: this.sent.length, queued: true };
      },
      clock: this.clock,
      journal: (line) => this.journal.push(line),
      ...options,
    });
  }

  async perceive(...percepts: Percept[]): Promise<void> {
    for (const percept of percepts) {
      this.mind.perceive(message(percept, (this.#elapsedMs += 100)));
      await settle();
    }
  }

  take(): Primitive[] {
    return this.sent.splice(0);
  }
}

test("a snippet's console output and result come back", async () => {
  const t = new TestMind();

  const { text, isError } = await t.mind.executeCode({ code: `console.log("hello", 1 + 2);\nreturn { answer: 42 };` });

  expect(isError).toBe(false);
  expect(text).toContain("Console:\nhello 3\n");
  expect(text).toContain('Result:\n{\n  "answer": 42\n}\n');
});

test("a snippet that throws comes back as an error, with its message and how long it ran", async () => {
  const t = new TestMind();

  const running = t.mind.executeCode({ code: `await agent.waitFor(() => undefined, 1500);\nthrow new Error("no mudmen here");` });
  await settle();
  await t.clock.advance(1500);
  const { text, isError } = await running;

  expect(isError).toBe(true);
  expect(text).toContain("Error: no mudmen here");
  expect(text).toContain("after 1.5 s");
  expect(text).not.toContain("Result:");
});

test("a snippet that doesn't compile comes back as an error", async () => {
  const t = new TestMind();

  const { text, isError } = await t.mind.executeCode({ code: `const = 3;` });

  expect(isError).toBe(true);
  expect(text).toContain("Error:");
});

test("the porcelain Tactics and the slot names are at hand in every snippet", async () => {
  const t = new TestMind();
  await t.perceive(inventory([40, item("short sword")]));

  const running = t.mind.executeCode({ code: `return await equip(agent, 40, SLOT.rightHand);` });
  await settle();
  expect(t.take()).toEqual([{ name: "moveItem", from: 40, to: 10 }]);
  await t.perceive(inventory([40], [10, item("short sword")]));
  const { text, isError } = await running;

  expect(isError).toBe(false);
  expect(text).toContain('"ok": true');
});

test("a new call aborts the snippet still running, which says so, and leaves the Agent standing with attack off", async () => {
  const t = new TestMind();
  const first = t.mind.executeCode({ code: `await agent.send({ name: "sit" });\nawait agent.waitFor(() => undefined, 600_000);` });
  await settle();

  const second = await t.mind.executeCode({ code: `return 1;` });
  const { text, isError } = await first;

  expect(isError).toBe(true);
  expect(text).toContain("Aborted by a new call after 0 s.");
  expect(second.isError).toBe(false);
  expect(t.take()).toEqual([{ name: "sit" }, { name: "attack", on: false }, { name: "stop" }]);
});

test("an aborted snippet that swallows every error sends nothing more", async () => {
  const t = new TestMind();
  const first = t.mind.executeCode({ code: `while (true) {\n  try {\n    await agent.send({ name: "sit" });\n    await agent.waitFor(() => undefined, 1000);\n  } catch {}\n}` });
  await settle();
  await t.clock.advance(2500);

  await t.mind.executeCode({ code: `return 1;` });
  await first;
  t.take();
  await t.clock.advance(5000);

  expect(t.take()).toEqual([]);
});

const WAIT_FOR_EVER = `await agent.waitFor(() => undefined, Number.MAX_SAFE_INTEGER);`;

test("a snippet is aborted after two minutes unless the call says otherwise", async () => {
  const t = new TestMind();
  const running = t.mind.executeCode({ code: WAIT_FOR_EVER });
  await settle();

  await t.clock.advance(120_000);
  const { text, isError } = await running;

  expect(isError).toBe(true);
  expect(text).toContain("Aborted by its timeout of 2 min after 120 s.");
});

test("a call's timeout is clamped to between 0.1 and 25 minutes, under the 30 an MCP call gets", async () => {
  const t = new TestMind();
  const short = t.mind.executeCode({ code: WAIT_FOR_EVER, timeout: 0.01 });
  await settle();
  await t.clock.advance(6000);
  expect((await short).text).toContain("Aborted by its timeout of 0.1 min after 6 s.");

  const long = t.mind.executeCode({ code: WAIT_FOR_EVER, timeout: 600 });
  await settle();
  await t.clock.advance(1_500_000);
  expect((await long).text).toContain("Aborted by its timeout of 25 min after 1500 s.");
});

test("a stop said next to the Agent aborts the snippet running, in any case", async () => {
  const t = new TestMind();
  const running = t.mind.executeCode({ code: `await agent.send({ name: "attack", on: true });\n${WAIT_FOR_EVER}` });
  await settle();

  await t.perceive(sayLine('Vue says, "STOP"'));
  const { text, isError } = await running;

  expect(isError).toBe(true);
  expect(text).toContain("Aborted by a stop Vue said after 0 s.");
  expect(t.take()).toEqual([{ name: "attack", on: true }, { name: "attack", on: false }, { name: "stop" }]);
});

test("the Agent's own stop, or anything else said, doesn't abort a snippet", async () => {
  const t = new TestMind();
  const running = t.mind.executeCode({ code: `await agent.waitFor(() => undefined, 1000);\nreturn "done";` });
  await settle();

  await t.perceive(sayLine('Greeen says, "stop"'), sayLine('You say, "stop"'), sayLine('Vue says, "stop that"'));
  await t.clock.advance(1000);

  expect((await running).isError).toBe(false);
});

test("every result ends with what the Agent believes about itself, what's near it and what it carries", async () => {
  const t = new TestMind();
  await t.perceive(
    ownPosition(296000, 642000, 4848),
    player(3),
    points(1, 512),
    status(80, { health: 40, maxHealth: 50, endurancePercent: 90 }),
    npcCreate(100, "mudman", 296300, 642400, 4848, { level: 2 }),
    playerCreate(200, "Vue", 296030, 642040, 4848),
    inventory([10, item("short sword")], [40, item("bone")]),
    money(182),
    encumbrance(50, 12),
  );

  const { text } = await t.mind.executeCode({ code: `return 1;` });

  expect(text).toContain(
    [
      "State:",
      "Greeen, level 3 Guardian, 512‰ of the way to level 4, 1 spec point.",
      "Health 80% (40/50), endurance 90%, standing at X:296000 Y:642000 Z:4848.",
      "Near (2):",
      "  #200 Vue, a player, 50 away",
      "  #100 mudman, level 2 NPC, health 100%, 500 away",
      "Equipped: rightHand short sword (level 1).",
      "Backpack: 1 of 40 slots used. Money: 182 copper. Weight: 12 of 50 lb.",
    ].join("\n"),
  );
});

test("each result has the chat and game messages since the last call, newest 50", async () => {
  const t = new TestMind();
  await t.perceive(sayLine('Vue says, "hello"'));
  expect((await t.mind.executeCode({ code: `return 1;` })).text).toContain('Chat:\nCT_Say\tVue says, "hello"');

  for (let i = 1; i <= 51; i++)
    await t.perceive({ kind: "message", text: `You get ${i} experience points.`, chatType: "CT_Important", chatLocation: "CL_SystemWindow" });

  const { text } = await t.mind.executeCode({ code: `return 1;` });

  expect(text).not.toContain("hello");
  expect(text).not.toContain("You get 1 experience points.");
  expect(text).toContain("Chat (newest 50 of 51):\nCT_Important\tYou get 2 experience points.");
  expect(text).toContain("You get 51 experience points.");
});

test("the Journal gets every Percept, every Primitive and every snippet with how it ended", async () => {
  const t = new TestMind();
  await t.perceive(ownPosition(296000, 642000, 4848));

  await t.mind.executeCode({ code: `await agent.send({ name: "sit" });\nreturn 1;` });
  await t.mind.executeCode({ code: `throw new Error("no mudmen here");` });

  expect(t.journal).toEqual([
    "#1\t0.100 s\townPosition #17\tX:296000 Y:642000 Z:4848 heading 0 region 200",
    'Snippet, with a timeout of 2 min:\n  await agent.send({ name: "sit" });\n  return 1;',
    "Starting snippet.",
    "Sending sit.",
    "snippet succeeded.",
    'Snippet, with a timeout of 2 min:\n  throw new Error("no mudmen here");',
    "Starting snippet.",
    "snippet failed: Error: no mudmen here.",
  ]);
});

test("a call while the Agent is out of the world enters it first, or says why it can't", async () => {
  let inWorld = false;
  let refusal: string | undefined = "the server is unreachable";
  const t = new TestMind({
    world: {
      inWorld: () => inWorld,
      enter: async () => {
        inWorld = refusal === undefined;
        return refusal;
      },
    },
  });

  const refused = await t.mind.executeCode({ code: `await agent.send({ name: "sit" });` });

  expect(refused.isError).toBe(true);
  expect(refused.text).toContain("Greeen isn't in the world, and entering it failed: the server is unreachable.");
  expect(t.take()).toEqual([]);

  refusal = undefined;
  const entered = await t.mind.executeCode({ code: `await agent.send({ name: "sit" });` });

  expect(entered.isError).toBe(false);
  expect(entered.text).toContain("Greeen wasn't in the world, and has entered it first.");
  expect(t.take()).toEqual([{ name: "sit" }]);
});

test("a snippet imports from the Agent's folder", async () => {
  const folder = await mkdtemp(join(tmpdir(), "agent-"));
  await Bun.write(join(folder, "tactics", "greet.ts"), `export const greeting: string = "hello from my folder";\n`);
  const t = new TestMind({ folder });

  const { text, isError } = await t.mind.executeCode({ code: `const { greeting } = await import("./tactics/greet.ts");\nreturn greeting;` });

  expect(isError).toBe(false);
  expect(text).toContain('Result:\n"hello from my folder"');
});

test("a snippet's console output is bounded to its first and last 300 lines of 2000 characters", async () => {
  const t = new TestMind();

  const { text } = await t.mind.executeCode({ code: `for (let i = 1; i <= 1000; i++) console.log(i === 1 ? "x".repeat(3000) : \`line \${i}\`);` });

  expect(text).toContain(`Console:\n${"x".repeat(2000)}…\nline 2\n`);
  expect(text).toContain("line 300\n… 400 lines left out …\nline 701\n");
  expect(text).toContain("line 1000\n");
  expect(text).not.toContain("line 301\n");
});

test("a snippet imports the Agent's files as they are now, not as they were when first imported", async () => {
  const folder = await mkdtemp(join(tmpdir(), "agent-"));
  await Bun.write(join(folder, "tactics", "level.ts"), `export const level = 1;\n`);
  await Bun.write(join(folder, "tactics", "hunt.ts"), `import { level } from "./level.ts";\nexport const hunt = () => level;\n`);
  const t = new TestMind({ folder });
  const code = `const { hunt } = await import("./tactics/hunt.ts");\nreturn hunt();`;
  expect((await t.mind.executeCode({ code })).text).toContain("Result:\n1\n");

  await Bun.write(join(folder, "tactics", "level.ts"), `export const level = 2;\n`);

  expect((await t.mind.executeCode({ code })).text).toContain("Result:\n2\n");
});

test("a result that JSON can't hold comes back readable, maps included", async () => {
  const t = new TestMind();

  const map = await t.mind.executeCode({ code: `return new Map([["Blades", 3]]);` });
  const circular = await t.mind.executeCode({ code: `const a: any = { name: "loop" };\na.self = a;\nreturn a;` });

  expect(map.text).toContain('Result:\n{\n  "Blades": 3\n}\n');
  expect(circular.isError).toBe(false);
  expect(circular.text).toContain("Result:\n");
  expect(circular.text).toContain("loop");
  expect(circular.text).toContain("State:\n");
});

test("forgetting starts the Agent's beliefs afresh, as after logging in again", async () => {
  const t = new TestMind();
  await t.perceive(ownPosition(296000, 642000, 4848), npcCreate(100, "mudman", 296300, 642400, 4848));

  t.mind.forget();
  const { text } = await t.mind.executeCode({ code: `return agent.surroundings.npcs.length;` });

  expect(text).toContain("Result:\n0\n");
  expect(text).toContain("Near: nothing in view.");
});

test("what an aborted snippet logs later stays out of the next snippet's console", async () => {
  const t = new TestMind();
  const first = t.mind.executeCode({ code: `await new Promise((resolve) => setTimeout(resolve, 20));\nconsole.log("from the first");` });
  await settle();
  const second = t.mind.executeCode({ code: `await new Promise((resolve) => setTimeout(resolve, 50));\nreturn 2;` });
  await first;

  expect((await second).text).not.toContain("from the first");
});
