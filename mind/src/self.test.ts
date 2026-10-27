import { expect, test } from "bun:test";
import type { JumpPercept, MovedPercept, OwnPositionPercept, Percept, PerceptMessage } from "./protocol.ts";
import { ownPositionIn, Self } from "./self.ts";
import { FakeClock } from "./test-agent.ts";
import { castStarted, concentration, cooldowns, dialog, died, effects, interrupted, petWindow, player, points, revived, skills, spec, spellLanded, stats, status } from "./test-percepts.ts";

function message(percept: Percept): PerceptMessage {
  return { type: "percept", sequence: 1, elapsedMs: 1000, percept };
}

function ownPosition(x: number, y: number, z: number, heading: number): OwnPositionPercept {
  return { kind: "ownPosition", x, y, z, objectId: 17, heading, zoneXOffset: 0, zoneYOffset: 0, region: 200, divingEnabled: false, underwater: false };
}

function moved(x: number, y: number, z: number, heading: number): MovedPercept {
  return { kind: "moved", x, y, z, heading, speed: 191, targetX: 300000, targetY: 640000, targetZ: 4848 };
}

function jump(x: number, y: number, z: number, heading: number): JumpPercept {
  return { kind: "jump", x, y, z, objectId: 17, heading, houseNumber: 0 };
}

function selfAt(x: number, y: number, z: number): Self {
  const agentSelf = new Self(new FakeClock());
  agentSelf.perceive(message(ownPosition(x, y, z, 1523)));
  return agentSelf;
}

test("knows nothing about where it is before the server says", () => {
  const agentSelf = new Self(new FakeClock());

  expect(agentSelf.position).toBeUndefined();
  expect(agentSelf.maxSpeed).toBe(0);
  expect(agentSelf.movement).toEqual({ kind: "standing" });
});

test("takes its position and heading from the server at world entry, and stands", () => {
  const agentSelf = selfAt(296600, 642629, 4848);

  expect(agentSelf.position).toEqual({ x: 296600, y: 642629, z: 4848 });
  expect(agentSelf.heading).toBe(1523);
  expect(agentSelf.movement).toEqual({ kind: "standing" });
});

test("is moving to the target while it runs, wherever it has got to", () => {
  const agentSelf = selfAt(296600, 642629, 4848);

  agentSelf.perceive(message(moved(296591.7, 642621, 4848, 1523)));

  expect(agentSelf.position).toEqual({ x: 296591.7, y: 642621, z: 4848 });
  expect(agentSelf.heading).toBe(1523);
  expect(agentSelf.movement).toEqual({ kind: "moving", target: { x: 300000, y: 640000, z: 4848 } });
});

test("has arrived where the run ended", () => {
  const agentSelf = selfAt(296600, 642629, 4848);
  agentSelf.perceive(message(moved(296591.7, 642621, 4848, 1523)));

  agentSelf.perceive(message({ kind: "arrived", x: 300000, y: 640000, z: 4848 }));

  expect(agentSelf.position).toEqual({ x: 300000, y: 640000, z: 4848 });
  expect(agentSelf.movement).toEqual({ kind: "arrived" });
});

test("has stopped where the run ended, with the reason", () => {
  const agentSelf = selfAt(296600, 642629, 4848);
  agentSelf.perceive(message(moved(296591.7, 642621, 4848, 1523)));

  agentSelf.perceive(message({ kind: "stopped", x: 296580, y: 642610, z: 4848, reason: "Stop" }));

  expect(agentSelf.position).toEqual({ x: 296580, y: 642610, z: 4848 });
  expect(agentSelf.movement).toEqual({ kind: "stopped", reason: "Stop" });
});

test("is moving again after a run was replaced, to the new target", () => {
  const agentSelf = selfAt(296600, 642629, 4848);
  agentSelf.perceive(message(moved(296591.7, 642621, 4848, 1523)));
  agentSelf.perceive(message({ kind: "stopped", x: 296580, y: 642610, z: 4848, reason: "Replaced" }));
  expect(agentSelf.movement).toEqual({ kind: "stopped", reason: "Replaced" });

  agentSelf.perceive(message({ ...moved(296570, 642620, 4848, 1000), targetX: 296000, targetY: 643000, targetZ: 4900 }));

  expect(agentSelf.movement).toEqual({ kind: "moving", target: { x: 296000, y: 643000, z: 4900 } });
});

test("takes the position and heading the server jumps it to, and stands", () => {
  const agentSelf = selfAt(296600, 642629, 4848);
  agentSelf.perceive(message({ kind: "arrived", x: 300000, y: 640000, z: 4848 }));

  agentSelf.perceive(message(jump(250000, 600000, 3000, 1024)));

  expect(agentSelf.position).toEqual({ x: 250000, y: 600000, z: 3000 });
  expect(agentSelf.heading).toBe(1024);
  expect(agentSelf.movement).toEqual({ kind: "standing" });
});

// The Harness ends the run with `stopped` right after the jump.
test("has stopped where the server jumped it to mid-run, moved by the server", () => {
  const agentSelf = selfAt(296600, 642629, 4848);
  agentSelf.perceive(message(moved(296591.7, 642621, 4848, 1523)));

  agentSelf.perceive(message(jump(250000, 600000, 3000, 1024)));
  agentSelf.perceive(message({ kind: "stopped", x: 250000, y: 600000, z: 3000, reason: "MovedByServer" }));

  expect(agentSelf.position).toEqual({ x: 250000, y: 600000, z: 3000 });
  expect(agentSelf.heading).toBe(1024);
  expect(agentSelf.movement).toEqual({ kind: "stopped", reason: "MovedByServer" });
});

// As `/faceloc` sends it.
test("takes only the heading from a jump without a position", () => {
  const agentSelf = selfAt(296600, 642629, 4848);
  agentSelf.perceive(message({ kind: "arrived", x: 300000, y: 640000, z: 4848 }));

  agentSelf.perceive(message(jump(0, 0, 0, 2048)));

  expect(agentSelf.position).toEqual({ x: 300000, y: 640000, z: 4848 });
  expect(agentSelf.heading).toBe(2048);
  expect(agentSelf.movement).toEqual({ kind: "arrived" });
});

test("has stopped where it stands when it can't move, for example while rooted", () => {
  const agentSelf = selfAt(296600, 642629, 4848);
  agentSelf.perceive(message(moved(296591.7, 642621, 4848, 1523)));

  agentSelf.perceive(message({ kind: "maxSpeed", maxSpeedPercent: 0, turningDisabled: false, waterSpeed: 0 }));
  agentSelf.perceive(message({ kind: "stopped", x: 296580, y: 642610, z: 4848, reason: "UnableToMove" }));

  expect(agentSelf.maxSpeed).toBe(0);
  expect(agentSelf.position).toEqual({ x: 296580, y: 642610, z: 4848 });
  expect(agentSelf.movement).toEqual({ kind: "stopped", reason: "UnableToMove" });
});

test("has stopped where the route ended when it can't get to the target", () => {
  const agentSelf = selfAt(296600, 642629, 4848);
  agentSelf.perceive(message(moved(296591.7, 642621, 4848, 1523)));

  agentSelf.perceive(message({ kind: "stopped", x: 296580, y: 642610, z: 4851, reason: "Unreachable" }));

  expect(agentSelf.position).toEqual({ x: 296580, y: 642610, z: 4851 });
  expect(agentSelf.movement).toEqual({ kind: "stopped", reason: "Unreachable" });
});

test("the Percepts that say where its own Character is give that position, and no others", () => {
  expect(ownPositionIn(ownPosition(1, 2, 3, 0))).toEqual({ x: 1, y: 2, z: 3 });
  expect(ownPositionIn(jump(4, 5, 6, 0))).toEqual({ x: 4, y: 5, z: 6 });
  expect(ownPositionIn(moved(7, 8, 9, 0))).toEqual({ x: 7, y: 8, z: 9 });
  expect(ownPositionIn({ kind: "arrived", x: 10, y: 11, z: 12 })).toEqual({ x: 10, y: 11, z: 12 });
  expect(ownPositionIn({ kind: "stopped", x: 13, y: 14, z: 15, reason: "Stop" })).toEqual({ x: 13, y: 14, z: 15 });

  expect(ownPositionIn(jump(0, 0, 0, 2048))).toBeUndefined();
  expect(ownPositionIn({ kind: "objectDelete", objectId: 291 })).toBeUndefined();
  expect(ownPositionIn({ kind: "maxSpeed", maxSpeedPercent: 100, turningDisabled: false, waterSpeed: 0 })).toBeUndefined();
});

test("works out its maximum speed from the percentage the server sends", () => {
  const agentSelf = new Self(new FakeClock());

  agentSelf.perceive(message({ kind: "maxSpeed", maxSpeedPercent: 100, turningDisabled: false, waterSpeed: 0 }));
  expect(agentSelf.maxSpeed).toBe(191);

  agentSelf.perceive(message({ kind: "maxSpeed", maxSpeedPercent: 117, turningDisabled: false, waterSpeed: 0 }));
  expect(agentSelf.maxSpeed).toBeCloseTo(223.47);
});

test("other Percepts change nothing", () => {
  const agentSelf = selfAt(296600, 642629, 4848);

  agentSelf.perceive(message({ kind: "bare", call: "SendUpdatePoints" }));
  agentSelf.perceive(message({ kind: "message", text: 'Vue says, "come"', chatType: "CT_Say", chatLocation: "CL_ChatWindow" }));
  // The route starts where the Character is, but `moved` says so once it runs.
  agentSelf.perceive(message({ kind: "routed", corners: [{ x: 296000, y: 642000, z: 4848, swim: false }, { x: 299000, y: 642000, z: 4850, swim: false }], reachesTarget: true }));

  expect(agentSelf.position).toEqual({ x: 296600, y: 642629, z: 4848 });
  expect(agentSelf.movement).toEqual({ kind: "standing" });
});

test("knows its object ID from the server at world entry", () => {
  expect(selfAt(296600, 642629, 4848).objectId).toBe(17);
  expect(new Self(new FakeClock()).objectId).toBeUndefined();
});

test("takes its health, endurance and whether it sits from the status Percept", () => {
  const agentSelf = selfAt(296600, 642629, 4848);

  agentSelf.perceive(message(status(45, { endurancePercent: 80, health: 54, maxHealth: 120, sitting: true })));

  expect(agentSelf.healthPercent).toBe(45);
  expect(agentSelf.health).toBe(54);
  expect(agentSelf.maxHealth).toBe(120);
  expect(agentSelf.endurancePercent).toBe(80);
  expect(agentSelf.sitting).toBe(true);
});

test("takes its Power and the concentration it has left from the status Percept", () => {
  const agentSelf = selfAt(296600, 642629, 4848);

  agentSelf.perceive(message(status(100, { powerPercent: 25, power: 3, maxPower: 12, concentration: 16, maxConcentration: 18 })));

  expect(agentSelf.powerPercent).toBe(25);
  expect(agentSelf.power).toBe(3);
  expect(agentSelf.maxPower).toBe(12);
  expect(agentSelf.concentration).toBe(16);
  expect(agentSelf.maxConcentration).toBe(18);
});

test("knows no pet before the server opens the pet window, then its pet and settings from each one, and none once it closes", () => {
  const agentSelf = new Self(new FakeClock());
  expect(agentSelf.pet).toBeUndefined();

  agentSelf.perceive(message(petWindow("Open", 0x0456)));
  expect(agentSelf.pet).toEqual({ objectId: 0x0456, aggression: "Defensive", walk: "Follow" });

  agentSelf.perceive(message(petWindow("Update", 0x0456, "Passive", "Stay")));
  expect(agentSelf.pet).toEqual({ objectId: 0x0456, aggression: "Passive", walk: "Stay" });

  agentSelf.perceive(message(petWindow("Close", 0, "Aggressive", "Follow")));
  expect(agentSelf.pet).toBeUndefined();
});

// As when a Pet dies: the server updates the window with no Pet just before it closes it.
test("knows no pet from a pet window update with object ID 0", () => {
  const agentSelf = new Self(new FakeClock());
  agentSelf.perceive(message(petWindow("Open", 0x0456)));

  agentSelf.perceive(message(petWindow("Update", 0)));

  expect(agentSelf.pet).toBeUndefined();
});

test("knows no Concentration buffs before the server says, then each it keeps up from the latest concentration Percept", () => {
  const agentSelf = new Self(new FakeClock());
  expect(agentSelf.concentrationBuffs).toEqual([]);

  agentSelf.perceive(message(concentration({ spell: "Minor Bark S..", target: "Fearnan", cost: 1 }, { spell: "Strength of ..", target: "Fearnan", cost: 1 })));
  agentSelf.perceive(message(concentration({ spell: "Strength of ..", target: "Fearnan", cost: 1 })));

  expect(agentSelf.concentrationBuffs).toEqual([{ spell: "Strength of ..", target: "Fearnan", cost: 1 }]);
});

test("takes its level and class from the player Percept, and its experience and spec points from the points Percept", () => {
  const agentSelf = selfAt(296600, 642629, 4848);

  agentSelf.perceive(message(player(3)));
  agentSelf.perceive(message(points(2, 512)));

  expect(agentSelf.level).toBe(3);
  expect(agentSelf.className).toBe("Guardian");
  expect(agentSelf.levelPermill).toBe(512);
  expect(agentSelf.specPoints).toBe(2);
});

test("knows its specializations and their levels from the skills Percept, and nothing else from it as a spec", () => {
  const agentSelf = selfAt(296600, 642629, 4848);

  agentSelf.perceive(message(skills(spec("Blades", 2), spec("Parry", 1), { name: "Sprint", level: 1, page: "Abilities" })));

  expect(agentSelf.specs).toEqual(new Map([["Blades", 2], ["Parry", 1]]));
  expect(agentSelf.skills.map((skill) => skill.name)).toEqual(["Blades", "Parry", "Sprint"]);
});

test("is dead from its own death until it comes back to life, and others' deaths change nothing", () => {
  const agentSelf = selfAt(296600, 642629, 4848);

  agentSelf.perceive(message(died(291, 17)));
  expect(agentSelf.dead).toBe(false);

  agentSelf.perceive(message(died(17, 0x1f0b)));
  expect(agentSelf.dead).toBe(true);

  agentSelf.perceive(message(revived(291)));
  expect(agentSelf.dead).toBe(true);

  agentSelf.perceive(message(revived(17)));
  expect(agentSelf.dead).toBe(false);
});

test("is dead with no health, as when the Mind starts while its Character lies dead", () => {
  const agentSelf = selfAt(296600, 642629, 4848);

  agentSelf.perceive(message(status(0, { health: 0 })));

  expect(agentSelf.dead).toBe(true);
});

test("knows no Effects before the server says, then every Effect in the buff bar with its time left", () => {
  const agentSelf = new Self(new FakeClock());
  expect(agentSelf.effects).toEqual([]);

  agentSelf.perceive(message(effects({ name: "speed", positive: true, secondsLeft: 600 }, { name: "slow", positive: false })));

  expect(agentSelf.effects.map(({ name, positive, secondsLeft }) => ({ name, positive, secondsLeft }))).toEqual([
    { name: "speed", positive: true, secondsLeft: 600 },
    { name: "slow", positive: false, secondsLeft: undefined },
  ]);
});

test("counts an Effect's time left down as the client's buff bar does, and keeps it at 0 until the server takes it off", async () => {
  const clock = new FakeClock();
  const agentSelf = new Self(clock);
  agentSelf.perceive(message(effects({ name: "speed", positive: true, secondsLeft: 600 })));

  await clock.advance(90_500);
  expect(agentSelf.effects[0]?.secondsLeft).toBe(510);

  await clock.advance(600_000);
  expect(agentSelf.effects.map(({ name, secondsLeft }) => ({ name, secondsLeft }))).toEqual([{ name: "speed", secondsLeft: 0 }]);
});

test("takes the whole buff bar from each Effects Percept, so an Effect the server took off is gone", () => {
  const agentSelf = new Self(new FakeClock());
  agentSelf.perceive(message(effects({ name: "speed", positive: true, secondsLeft: 600 }, { name: "slow", positive: false, secondsLeft: 20 })));

  agentSelf.perceive(message(effects({ name: "slow", positive: false, secondsLeft: 19 })));

  expect(agentSelf.effects.map(({ name, secondsLeft }) => ({ name, secondsLeft }))).toEqual([{ name: "slow", secondsLeft: 19 }]);
});

test("keeps a spell Effect's details, and none for an ability's", () => {
  const agentSelf = new Self(new FakeClock());
  const details = { level: 1, durationSeconds: 600, target: "Realm area effect", range: 1500, castingSeconds: 0, radius: 350, description: "The target's speed is increased to 147% of normal for 10 minutes." };

  agentSelf.perceive(message(effects({ name: "Speed of the Realm", positive: true, secondsLeft: 600, details }, { name: "Sprint", positive: true })));

  expect(agentSelf.effects.map(({ name, details }) => ({ name, details }))).toEqual([
    { name: "Speed of the Realm", details },
    { name: "Sprint", details: undefined },
  ]);
});

test("knows which Effects the server switched off, as a weaker buff a better one replaced", () => {
  const agentSelf = new Self(new FakeClock());

  agentSelf.perceive(message(effects({ name: "Minor Bark Skin", positive: true, disabled: true }, { name: "Lesser Bark Skin", positive: true })));

  expect(agentSelf.effects.map(({ name, disabled }) => ({ name, disabled }))).toEqual([
    { name: "Minor Bark Skin", disabled: true },
    { name: "Lesser Bark Skin", disabled: false },
  ]);
});

test("knows of no Dialog and no lost Constitution before the server says", () => {
  const agentSelf = new Self(new FakeClock());

  expect(agentSelf.dialog).toBeUndefined();
  expect(agentSelf.constitutionLost).toBeUndefined();
});

test("keeps the Dialog the server opened, and a newer one in its place", () => {
  const agentSelf = new Self(new FakeClock());

  agentSelf.perceive(message(dialog("It will cost 2 silver. Do you accept?")));
  expect(agentSelf.dialog).toEqual({ code: "CustomDialog", data1: 0x0101, data2: 1, data3: 0, yesNo: true, text: "It will cost 2 silver. Do you accept?" });

  agentSelf.perceive(message(dialog("Done.", false)));
  expect(agentSelf.dialog).toEqual({ code: "CustomDialog", data1: 0x0101, data2: 1, data3: 0, yesNo: false, text: "Done." });
});

test("forgets the Dialog once it's answered, as the client closes the window on the click", () => {
  const agentSelf = new Self(new FakeClock());
  agentSelf.perceive(message(dialog("Do you accept?")));

  agentSelf.sent({ name: "say", text: "hello" });
  expect(agentSelf.dialog?.text).toBe("Do you accept?");

  agentSelf.sent({ name: "answerDialog", code: "CustomDialog", data1: 0x0101, data2: 1, data3: 0, accept: true });
  expect(agentSelf.dialog).toBeUndefined();
});

test("takes the lost Constitution from each stats Percept", () => {
  const agentSelf = new Self(new FakeClock());

  agentSelf.perceive(message(stats(7)));
  expect(agentSelf.constitutionLost).toBe(7);

  agentSelf.perceive(message(stats(0)));
  expect(agentSelf.constitutionLost).toBe(0);
});

test("takes its max health from a stats Percept too, as a Healer restoring Constitution sends no status", () => {
  const agentSelf = new Self(new FakeClock());
  agentSelf.perceive(message(status(100, { health: 488, maxHealth: 488 })));

  agentSelf.perceive(message(stats(0, 496)));

  expect(agentSelf.maxHealth).toBe(496);
  expect(agentSelf.health).toBe(488);
});

test("is casting the Spell it last sent from its own cast start until it lands", () => {
  const clock = new FakeClock();
  const agentSelf = new Self(clock);
  agentSelf.perceive(message(ownPosition(296600, 642629, 4848, 0)));
  expect(agentSelf.casting).toBeUndefined();

  agentSelf.sent({ name: "castSpell", spell: "Gleam Ray" });
  expect(agentSelf.casting).toBeUndefined();

  agentSelf.perceive(message(castStarted(17, 2600)));
  expect(agentSelf.casting).toEqual({ spell: "Gleam Ray", landsAt: 2600 });

  agentSelf.perceive(message(spellLanded(17, 0x0456)));
  expect(agentSelf.casting).toBeUndefined();
});

test("is no longer casting once its own cast is interrupted, and another caster's start, landing or interrupt changes nothing", () => {
  const agentSelf = new Self(new FakeClock());
  agentSelf.perceive(message(ownPosition(296600, 642629, 4848, 0)));
  agentSelf.sent({ name: "castSpell", spell: "Gleam Ray" });

  agentSelf.perceive(message(castStarted(0x0456, 3000)));
  expect(agentSelf.casting).toBeUndefined();

  agentSelf.perceive(message(castStarted(17, 2600)));
  agentSelf.perceive(message(spellLanded(0x0456, 17)));
  agentSelf.perceive(message(interrupted(0x0456)));
  expect(agentSelf.casting?.spell).toBe("Gleam Ray");

  agentSelf.perceive(message(interrupted(17)));
  expect(agentSelf.casting).toBeUndefined();
});

test("is no longer casting once its cast is well past when it should have landed, as when it failed as it ended", async () => {
  const clock = new FakeClock();
  const agentSelf = new Self(clock);
  agentSelf.perceive(message(ownPosition(296600, 642629, 4848, 0)));
  agentSelf.sent({ name: "castSpell", spell: "Gleam Ray" });
  agentSelf.perceive(message(castStarted(17, 2600)));

  await clock.advance(3000);
  expect(agentSelf.casting?.spell).toBe("Gleam Ray");

  await clock.advance(2000);
  expect(agentSelf.casting).toBeUndefined();
});

test("knows each skill on Cooldown until it's ready again, from every cooldowns Percept", async () => {
  const clock = new FakeClock();
  const agentSelf = new Self(clock);
  expect(agentSelf.cooldowns.size).toBe(0);

  agentSelf.perceive(message(cooldowns({ Quickcast: 30 })));
  await clock.advance(5000);
  agentSelf.perceive(message(cooldowns({ "Major Ethereal Detonation": 10, Sprint: 0 })));

  expect(agentSelf.cooldowns).toEqual(new Map([["Quickcast", 30_000], ["Major Ethereal Detonation", 15_000]]));

  await clock.advance(10_000);
  expect([...agentSelf.cooldowns.keys()]).toEqual(["Quickcast"]);

  await clock.advance(15_000);
  expect(agentSelf.cooldowns.size).toBe(0);
});
