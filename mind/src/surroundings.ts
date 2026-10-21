import type { PerceptMessage } from "./protocol.ts";
import { ownPositionIn } from "./self.ts";

/** How far, in game units, the Agent's Character sees objects (`WorldMgr.VISIBILITY_DISTANCE` in the server). Static knowledge, as the client has it. */
export const VISIBILITY_DISTANCE = 6000;

/** A point in global coordinates. */
export interface Position {
  readonly x: number;
  readonly y: number;
  readonly z: number;
}

/** What the Agent knows of any object around it, as the last Percept about it said. */
interface KnownObjectFields {
  readonly objectId: number;
  readonly name: string;
  readonly position: Position;
  /** 0 to 4095. */
  readonly heading: number;
  readonly speed: number;
  /** The `elapsedMs` of the last Percept about it. */
  readonly lastHeardMs: number;
}

/** Another player the Agent knows of. Its speed is 0 until a position Percept says otherwise: the create packet carries none. */
export interface KnownPlayer extends KnownObjectFields {
  readonly kind: "player";
}

/** An NPC the Agent knows of. */
export interface KnownNpc extends KnownObjectFields {
  readonly kind: "npc";
}

/** Everything the Agent knows of around it. `kind` tells players and NPCs apart. */
export type KnownObject = KnownPlayer | KnownNpc;

/**
 * What the Agent currently believes is around it, built only from Percepts (see `CONTEXT.md`).
 * One collection of known objects by object ID, since the server gives players and NPCs IDs from the same range and a delete or remove Percept doesn't say which it was.
 * An object is known from its create Percept until a delete Percept (it went out of view) or a remove Percept (it left the world, for example by quitting or despawning). Position and object update Percepts move it.
 * Whenever the Agent's own Character moves, by running or by the server moving it, everything farther than the visibility distance from it is forgotten too, as the client forgets it: the server doesn't always say, and never does for an NPC that goes out of view.
 * Nothing is forgotten by time, because the server doesn't send a player who stands still again.
 * An update for an object it doesn't know is ignored: without its create, the Agent doesn't know what it is.
 */
export class Surroundings {
  readonly #objects = new Map<number, KnownObject>();
  // Where the Agent's own Character is, from the same Percepts Self takes it from.
  #ownPosition: Position | undefined;

  perceive({ elapsedMs, percept }: PerceptMessage): void {
    const ownPosition = ownPositionIn(percept);

    if (ownPosition && !samePosition(ownPosition, this.#ownPosition)) {
      this.#ownPosition = ownPosition;
      this.#forgetOutOfView(ownPosition);
    }

    switch (percept.kind) {
      case "playerCreate":
      case "npcCreate":
        this.#objects.set(percept.objectId, {
          kind: percept.kind === "playerCreate" ? "player" : "npc",
          objectId: percept.objectId,
          name: percept.name,
          position: { x: percept.x, y: percept.y, z: percept.z },
          heading: percept.heading,
          speed: percept.kind === "npcCreate" ? percept.speed : 0,
          lastHeardMs: elapsedMs,
        });
        break;
      case "playerPosition":
      case "objectUpdate": {
        const known = this.#objects.get(percept.objectId);

        if (!known)
          break;

        this.#objects.set(percept.objectId, {
          ...known,
          position: { x: percept.x, y: percept.y, z: percept.z },
          heading: percept.heading,
          speed: percept.speed,
          lastHeardMs: elapsedMs,
        });
        break;
      }
      case "objectDelete":
      case "objectRemove":
        this.#objects.delete(percept.objectId);
        break;
    }
  }

  /** Every object the Agent knows of, players and NPCs. */
  get objects(): readonly KnownObject[] {
    return [...this.#objects.values()];
  }

  get players(): readonly KnownPlayer[] {
    return this.objects.filter((known) => known.kind === "player");
  }

  get npcs(): readonly KnownNpc[] {
    return this.objects.filter((known) => known.kind === "npc");
  }

  object(objectId: number): KnownObject | undefined {
    return this.#objects.get(objectId);
  }

  player(objectId: number): KnownPlayer | undefined {
    const known = this.#objects.get(objectId);
    return known?.kind === "player" ? known : undefined;
  }

  npc(objectId: number): KnownNpc | undefined {
    const known = this.#objects.get(objectId);
    return known?.kind === "npc" ? known : undefined;
  }

  /** The object called `name`, player or NPC, in any case. */
  named(name: string): KnownObject | undefined {
    const wanted = name.toLowerCase();
    return this.objects.find((known) => known.name.toLowerCase() === wanted);
  }

  #forgetOutOfView(from: Position): void {
    for (const [objectId, known] of this.#objects) {
      if (distance(from, known.position) > VISIBILITY_DISTANCE)
        this.#objects.delete(objectId);
    }
  }
}

function samePosition(a: Position, b: Position | undefined): boolean {
  return b !== undefined && a.x === b.x && a.y === b.y && a.z === b.z;
}

/** How far apart two points are, in game units, Z included. Worked out when asked, so it's never stale. */
export function distance(from: Position, to: Position): number {
  return Math.hypot(to.x - from.x, to.y - from.y, to.z - from.z);
}

/**
 * The heading, 0 to 4095, that faces `to` from `from`, as the server measures headings: heading `h` points along (−sin θ, cos θ) with θ = `h` × 2π / 4096, so 0 points towards +Y.
 * Only X and Y count. Nothing faces a point straight above, below or at `from`.
 */
export function headingTowards(from: Position, to: Position): number | undefined {
  const dx = to.x - from.x;
  const dy = to.y - from.y;

  if (dx === 0 && dy === 0)
    return undefined;

  return (Math.round((Math.atan2(-dx, dy) * 4096) / (2 * Math.PI)) + 4096) % 4096;
}
