import type { JumpPercept, Percept, PerceptMessage, StopReason } from "./protocol.ts";
import type { Position } from "./surroundings.ts";

/** The speed, in game units per second, that a maximum speed of 100% stands for (`GamePlayer.PLAYER_BASE_SPEED` in the server). Static knowledge, as the client has it. */
export const BASE_SPEED = 191;

/** Whether the Agent's Character is on its way somewhere, as the last movement Percept said. */
export type MovementStatus =
  /** Not running since it entered the world or the server last moved it. */
  | { readonly kind: "standing" }
  /** Running to `target`, in global coordinates. */
  | { readonly kind: "moving"; readonly target: Position }
  /** Its last run reached its target. */
  | { readonly kind: "arrived" }
  /** Its last run ended short of its target, for `reason`. */
  | { readonly kind: "stopped"; readonly reason: StopReason };

/**
 * What the Agent currently believes about its own Character, built only from Percepts (see `CONTEXT.md`).
 * The server says where the Character is at world entry and whenever it moves it itself (a jump), and how fast it may run. The Harness says where it is while it runs, and how each run ended, as the Agent's client works that out itself.
 * It doesn't track the Primitives the Mind sent, so there's one answer to "where am I and am I there yet".
 */
export class Self {
  #position: Position | undefined;
  #heading = 0;
  #maxSpeed = 0;
  #movement: MovementStatus = { kind: "standing" };

  perceive({ percept }: PerceptMessage): void {
    this.#position = ownPositionIn(percept) ?? this.#position;

    switch (percept.kind) {
      case "ownPosition":
        this.#heading = percept.heading;
        this.#movement = { kind: "standing" };
        break;
      case "jump":
        this.#heading = percept.heading;

        // A run in progress ended where the server put the Character, and the Harness says so next.
        if (!setsOnlyHeading(percept))
          this.#movement = { kind: "standing" };

        break;
      case "maxSpeed":
        this.#maxSpeed = (percept.maxSpeedPercent * BASE_SPEED) / 100;
        break;
      case "moved":
        this.#heading = percept.heading;
        this.#movement = { kind: "moving", target: { x: percept.targetX, y: percept.targetY, z: percept.targetZ } };
        break;
      case "arrived":
        this.#movement = { kind: "arrived" };
        break;
      case "stopped":
        this.#movement = { kind: "stopped", reason: percept.reason };
        break;
    }
  }

  /** Where the Character is, in global coordinates. Unknown until the server has said, at world entry. */
  get position(): Position | undefined {
    return this.#position;
  }

  /** 0 to 4095. */
  get heading(): number {
    return this.#heading;
  }

  /** In game units per second. 0 until the server has said. */
  get maxSpeed(): number {
    return this.#maxSpeed;
  }

  get movement(): MovementStatus {
    return this.#movement;
  }
}

/** Where `percept` says the Agent's own Character is, in global coordinates, or `undefined` if it doesn't say. */
export function ownPositionIn(percept: Percept): Position | undefined {
  switch (percept.kind) {
    case "jump":
      return setsOnlyHeading(percept) ? undefined : { x: percept.x, y: percept.y, z: percept.z };
    case "ownPosition":
    case "moved":
    case "arrived":
    case "stopped":
      return { x: percept.x, y: percept.y, z: percept.z };
    default:
      return undefined;
  }
}

/** A jump without a position, as for `/faceloc`: the client keeps its position and takes only the heading. */
function setsOnlyHeading(jump: JumpPercept): boolean {
  return jump.x === 0 && jump.y === 0;
}
