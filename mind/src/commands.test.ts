import { expect, test } from "bun:test";
import { heardCommand } from "./commands.ts";
import { sayLine } from "./test-percepts.ts";

test("reads the speaker and the command from a say line, in any case", () => {
  expect(heardCommand(sayLine('Vue says, "come"'), "Greeen")).toEqual({ speaker: "Vue", word: "come" });
  expect(heardCommand(sayLine('Vue says, "GRIND"'), "Greeen")).toEqual({ speaker: "Vue", word: "grind" });
  expect(heardCommand(sayLine('Vue says, "shop"'), "Greeen")).toEqual({ speaker: "Vue", word: "shop" });
  expect(heardCommand(sayLine('the town guard says, "stop"'), "Greeen")).toEqual({ speaker: "the town guard", word: "stop" });
});

test("anything else said isn't a command", () => {
  expect(heardCommand(sayLine('Vue says, "hello"'), "Greeen")).toBeUndefined();
  expect(heardCommand(sayLine('Vue says, "come here"'), "Greeen")).toBeUndefined();
});

test("the Agent's own lines aren't commands", () => {
  expect(heardCommand(sayLine('Greeen says, "grind"'), "greeen")).toBeUndefined();
  expect(heardCommand(sayLine('You say, "grind"'), "Greeen")).toBeUndefined();
});

test("only say lines are commands", () => {
  expect(heardCommand({ kind: "message", text: 'Vue says, "come"', chatType: "CT_Send", chatLocation: "CL_ChatWindow" }, "Greeen")).toBeUndefined();
});
