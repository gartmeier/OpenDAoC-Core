import { expect, test } from "bun:test";
import { Group } from "./group.ts";
import type { Percept } from "./protocol.ts";
import { groupMember, groupPositions, groupStatus, groupWindow, memberStatus, message } from "./test-percepts.ts";

function groupAfter(...percepts: Percept[]): Group {
  const known = new Group();

  for (const percept of percepts)
    known.perceive(message(percept));

  return known;
}

test("is empty before the server sends a group window", () => {
  expect(new Group().members).toEqual([]);
});

test("lists the members in the window's order, with their name, class, level and object ID", () => {
  const known = groupAfter(groupWindow(groupMember("Fearnan", 0x0456, "Druid", 8), groupMember("Orla", 17, "Eldritch", 7)));

  expect(known.members.map(({ name, className, level, objectId }) => ({ name, className, level, objectId }))).toEqual([
    { name: "Fearnan", className: "Druid", level: 8, objectId: 0x0456 },
    { name: "Orla", className: "Eldritch", level: 7, objectId: 17 },
  ]);
  expect(known.member(0x0456)?.name).toBe("Fearnan");
  expect(known.named("fearnan")?.objectId).toBe(0x0456);
});

test("knows how each member is doing, from the newest status the server sent for it", () => {
  const known = groupAfter(
    groupWindow(groupMember("Fearnan", 0x0456), groupMember("Orla", 17)),
    groupStatus(memberStatus(0x0456, { healthPercent: 80 }), memberStatus(17)),
    groupStatus(memberStatus(0x0456, { healthPercent: 0, powerPercent: 30, dead: true })),
  );

  expect(known.member(0x0456)).toMatchObject({ healthPercent: 0, powerPercent: 30, endurancePercent: 100, dead: true, mezzed: false });
  expect(known.member(17)).toMatchObject({ healthPercent: 100, dead: false });
});

test("keeps a status that came before the window naming its member, as when it joins", () => {
  const known = groupAfter(groupStatus(memberStatus(0x0456, { healthPercent: 60 })), groupWindow(groupMember("Fearnan", 0x0456)));

  expect(known.member(0x0456)?.healthPercent).toBe(60);
});

test("knows nothing of a member's health or place until the server says", () => {
  const known = groupAfter(groupWindow(groupMember("Fearnan", 0x0456)));

  expect(known.member(0x0456)).toMatchObject({ healthPercent: undefined, zone: undefined, position: undefined, dead: false });
});

test("knows where each other member is, and that one in another region is nowhere it can see", () => {
  const known = groupAfter(
    groupWindow(groupMember("Fearnan", 0x0456), groupMember("Dalta", 0x0789)),
    groupPositions({ objectId: 0x0456, zone: 202, x: 300000, y: 640000 }, { objectId: 0x0789 }),
  );

  expect(known.member(0x0456)).toMatchObject({ zone: 202, position: { x: 300000, y: 640000 } });
  expect(known.member(0x0789)).toMatchObject({ zone: undefined, position: undefined });
});

test("forgets a member who left, with how it was doing, and everyone once the Group is gone", () => {
  const twoLeft = groupAfter(
    groupWindow(groupMember("Fearnan", 0x0456), groupMember("Dalta", 0x0789), groupMember("Orla", 17)),
    groupStatus(memberStatus(0x0789, { healthPercent: 10 })),
    groupWindow(groupMember("Fearnan", 0x0456), groupMember("Orla", 17)),
  );

  expect(twoLeft.members.map((member) => member.name)).toEqual(["Fearnan", "Orla"]);
  expect(groupAfter(groupWindow(groupMember("Fearnan", 0x0456)), groupWindow()).members).toEqual([]);

  twoLeft.perceive(message(groupWindow(groupMember("Fearnan", 0x0456), groupMember("Dalta", 0x0789), groupMember("Orla", 17))));
  expect(twoLeft.member(0x0789)?.healthPercent).toBeUndefined();
});
