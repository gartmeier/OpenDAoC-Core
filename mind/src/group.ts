import type { GroupMember, GroupMemberPosition, GroupMemberStatus, PerceptMessage } from "./protocol.ts";

/** The status flags the group window shows for a member, in the order the packet's bits have them. */
export const GROUP_MEMBER_FLAGS = ["dead", "mezzed", "diseased", "poisoned", "linkdead", "nearsighted"] as const;

/** One Group member as the client's group window and map show it. What the server hasn't sent yet is undefined, and the status flags false. */
export interface KnownGroupMember {
  readonly name: string;
  readonly className: string;
  readonly level: number;
  /** As its `playerCreate` has it, so Surroundings know it by this ID while it's in view. */
  readonly objectId: number;
  readonly healthPercent: number | undefined;
  readonly powerPercent: number | undefined;
  readonly endurancePercent: number | undefined;
  readonly dead: boolean;
  readonly mezzed: boolean;
  readonly diseased: boolean;
  readonly poisoned: boolean;
  readonly linkdead: boolean;
  /** Under a spell or archery range debuff. The server sends no update when only this changes. */
  readonly nearsighted: boolean;
  /** In another region, where the server tells nothing else about it. */
  readonly inAnotherRegion: boolean;
  /** The zone it's in, as the map shows it. Never known for the Agent's own Character, whose place is in Self. */
  readonly zone: number | undefined;
  /** Where it was in global coordinates, from the map: updated every 2.5 s, and with no height. */
  readonly position: { readonly x: number; readonly y: number } | undefined;
}

/**
 * What the Agent currently believes about its Group, built only from Percepts: the members as its group window lists them, its own Character too,
 * how each is doing, and where the others are. Empty while it isn't in a Group.
 */
export class Group {
  #window: readonly GroupMember[] = [];
  readonly #status = new Map<number, GroupMemberStatus>();
  readonly #positions = new Map<number, GroupMemberPosition>();

  perceive({ percept }: PerceptMessage): void {
    switch (percept.kind) {
      // The status of a member who joins comes before the window that names it, so only what belongs to someone no longer in the window goes.
      case "groupWindow": {
        this.#window = percept.members;
        const members = new Set(percept.members.map((member) => member.objectId));

        for (const objectId of [...this.#status.keys()].filter((objectId) => !members.has(objectId)))
          this.#status.delete(objectId);

        for (const objectId of [...this.#positions.keys()].filter((objectId) => !members.has(objectId)))
          this.#positions.delete(objectId);

        break;
      }
      case "groupStatus":
        for (const status of percept.members)
          this.#status.set(status.objectId, status);

        break;
      case "groupPositions":
        for (const position of percept.members)
          this.#positions.set(position.objectId, position);

        break;
    }
  }

  /** The members in the window's order, the Agent's own Character among them. */
  get members(): readonly KnownGroupMember[] {
    return this.#window.map((member) => this.#known(member));
  }

  member(objectId: number): KnownGroupMember | undefined {
    const member = this.#window.find((member) => member.objectId === objectId);
    return member && this.#known(member);
  }

  /** The member with this name, in any case. */
  named(name: string): KnownGroupMember | undefined {
    const member = this.#window.find((member) => member.name.toLowerCase() === name.toLowerCase());
    return member && this.#known(member);
  }

  #known({ name, className, level, objectId }: GroupMember): KnownGroupMember {
    const status = this.#status.get(objectId);
    const position = this.#positions.get(objectId);

    return {
      name,
      className,
      level,
      objectId,
      healthPercent: status?.healthPercent,
      powerPercent: status?.powerPercent,
      endurancePercent: status?.endurancePercent,
      dead: status?.dead ?? false,
      mezzed: status?.mezzed ?? false,
      diseased: status?.diseased ?? false,
      poisoned: status?.poisoned ?? false,
      linkdead: status?.linkdead ?? false,
      nearsighted: status?.nearsighted ?? false,
      inAnotherRegion: status?.inAnotherRegion ?? false,
      zone: position?.zone,
      position: position?.x === undefined || position.y === undefined ? undefined : { x: position.x, y: position.y },
    };
  }
}
