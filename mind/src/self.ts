import type { JumpPercept, Percept, PerceptMessage, SkillEntry, StopReason } from "./protocol.ts";
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
 * The server also says how healthy it is, whether it sits, its level, experience and spec points, its skills, and when it dies and comes back to life.
 * It doesn't track the Primitives the Mind sent, so there's one answer to "where am I and am I there yet".
 */
export class Self {
  #position: Position | undefined;
  #heading = 0;
  #maxSpeed = 0;
  #movement: MovementStatus = { kind: "standing" };
  #objectId: number | undefined;
  #healthPercent = 100;
  #health = 0;
  #maxHealth = 0;
  #endurancePercent = 100;
  #sitting = false;
  #dead = false;
  #level = 0;
  #className = "";
  #levelPermill = 0;
  #specPoints = 0;
  #skills: readonly SkillEntry[] = [];
  #specs: ReadonlyMap<string, number> = new Map();

  perceive({ percept }: PerceptMessage): void {
    this.#position = ownPositionIn(percept) ?? this.#position;

    switch (percept.kind) {
      case "ownPosition":
        this.#objectId = percept.objectId;
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
      case "status":
        this.#healthPercent = percept.healthPercent;
        this.#health = percept.health;
        this.#maxHealth = percept.maxHealth;
        this.#endurancePercent = percept.endurancePercent;
        this.#sitting = percept.sitting;

        // Only the dead have no health, for example when the Mind starts while its Character lies dead.
        if (percept.health === 0)
          this.#dead = true;

        break;
      case "player":
        this.#level = percept.level;
        this.#className = percept.className;
        break;
      case "points":
        this.#levelPermill = percept.levelPermill;
        this.#specPoints = percept.specPoints;
        break;
      case "skills":
        this.#skills = percept.skills;
        this.#specs = new Map(percept.skills.filter((skill) => skill.page === "Specialization").map((skill) => [skill.name, skill.level]));
        break;
      case "died":
        if (percept.objectId === this.#objectId)
          this.#dead = true;

        break;
      case "revived":
        if (percept.objectId === this.#objectId)
          this.#dead = false;

        break;
    }
  }

  /** The Character's object ID, which other objects' Percepts name it by. Unknown until the server has said, at world entry. */
  get objectId(): number | undefined {
    return this.#objectId;
  }

  /** 0 to 100. 100 until the server has said. */
  get healthPercent(): number {
    return this.#healthPercent;
  }

  get health(): number {
    return this.#health;
  }

  get maxHealth(): number {
    return this.#maxHealth;
  }

  /** 0 to 100. 100 until the server has said. */
  get endurancePercent(): number {
    return this.#endurancePercent;
  }

  get sitting(): boolean {
    return this.#sitting;
  }

  /** From its own death until it comes back to life, for example by releasing. */
  get dead(): boolean {
    return this.#dead;
  }

  /** 0 until the server has said. */
  get level(): number {
    return this.#level;
  }

  get className(): string {
    return this.#className;
  }

  /** The experience towards the next level, in thousandths. */
  get levelPermill(): number {
    return this.#levelPermill;
  }

  /** The specialization points it has to train with. */
  get specPoints(): number {
    return this.#specPoints;
  }

  /** Everything on its skill pages, in the order the client lists them. */
  get skills(): readonly SkillEntry[] {
    return this.#skills;
  }

  /** Each specialization by name, with its trained level. */
  get specs(): ReadonlyMap<string, number> {
    return this.#specs;
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
