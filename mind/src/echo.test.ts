import { expect, test } from "bun:test";
import { echo } from "./echo.ts";
import type { Percept } from "./protocol.ts";

function sayLine(text: string): Percept {
  return { kind: "message", text, chatType: "CT_Say", chatLocation: "CL_ChatWindow" };
}

test("another speaker's say line is said back", () => {
  expect(echo(sayLine('Vue says, "hello"'), "Greeen")).toEqual({ name: "say", text: "hello" });
});

test("the words are said back whole, quotes included", () => {
  expect(echo(sayLine('Vue says, "he said "hi", then left"'), "Greeen")).toEqual({ name: "say", text: 'he said "hi", then left' });
});

test("an NPC's say line is said back too", () => {
  expect(echo(sayLine('the town guard says, "Move along."'), "Greeen")).toEqual({ name: "say", text: "Move along." });
});

test("the Agent's own say line gives nothing", () => {
  expect(echo(sayLine('You say, "hello"'), "Greeen")).toBeUndefined();
});

test("a say line under the Agent's own name gives nothing, whatever the case it was typed in", () => {
  expect(echo(sayLine('Greeen says, "hello"'), "greeen")).toBeUndefined();
});

test("other chat types give nothing", () => {
  for (const chatType of ["CT_Send", "CT_Group", "CT_Emote", "CT_System", "CT_Staff"] as const)
    expect(echo({ kind: "message", text: 'Vue says, "hello"', chatType, chatLocation: "CL_ChatWindow" }, "Greeen")).toBeUndefined();
});

test("bare Percepts give nothing", () => {
  expect(echo({ kind: "bare", call: "SendUpdatePoints" }, "Greeen")).toBeUndefined();
});

test("say lines that don't read as someone saying words give nothing", () => {
  for (const text of [
    'Vue yells, "hello"',
    "Vue says something in a language you don't understand.",
    'Vue says, ""',
    'Vue says, "hello',
    "hello",
    "",
  ])
    expect(echo(sayLine(text), "Greeen")).toBeUndefined();
});
