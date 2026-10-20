import type { PerceptMessage } from "./protocol.ts";

/** A point in global coordinates. */
export interface Position {
  readonly x: number;
  readonly y: number;
  readonly z: number;
}

/** Another player the Agent knows of, as the last Percept about it said. */
export interface KnownPlayer {
  readonly objectId: number;
  readonly name: string;
  readonly position: Position;
  /** 0 to 4095. */
  readonly heading: number;
  /** 0 until a position Percept says otherwise: the create packet carries no speed. */
  readonly speed: number;
  /** The `elapsedMs` of the last Percept about it. */
  readonly lastHeardMs: number;
}

/**
 * What the Agent currently believes is around it, built only from Percepts (see `CONTEXT.md`).
 * A player is known from its create Percept until a delete Percept (it went out of view) or a remove Percept (it left the world, for example by quitting). Nothing is forgotten by time, because the server doesn't send a player who stands still again.
 * A position Percept for a player it doesn't know is ignored: without its create, the Agent doesn't know who it is.
 */
export class Surroundings {
  readonly #players = new Map<number, KnownPlayer>();

  perceive({ elapsedMs, percept }: PerceptMessage): void {
    switch (percept.kind) {
      case "playerCreate":
        this.#players.set(percept.objectId, {
          objectId: percept.objectId,
          name: percept.name,
          position: { x: percept.x, y: percept.y, z: percept.z },
          heading: percept.heading,
          speed: 0,
          lastHeardMs: elapsedMs,
        });
        break;
      case "playerPosition": {
        const player = this.#players.get(percept.objectId);

        if (!player)
          break;

        this.#players.set(percept.objectId, {
          ...player,
          position: { x: percept.x, y: percept.y, z: percept.z },
          heading: percept.heading,
          speed: percept.speed,
          lastHeardMs: elapsedMs,
        });
        break;
      }
      case "objectDelete":
      case "objectRemove":
        this.#players.delete(percept.objectId);
        break;
    }
  }

  /** Every player the Agent knows of. */
  get players(): readonly KnownPlayer[] {
    return [...this.#players.values()];
  }

  player(objectId: number): KnownPlayer | undefined {
    return this.#players.get(objectId);
  }

  /** The player called `name`, in any case. */
  named(name: string): KnownPlayer | undefined {
    const wanted = name.toLowerCase();
    return this.players.find((player) => player.name.toLowerCase() === wanted);
  }
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
