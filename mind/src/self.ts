import type { ConcentrationBuff, DialogPercept, EffectEntry, JumpPercept, Percept, PerceptMessage, PetWindowPercept, Primitive, SkillEntry, SpellDetails, StopReason } from "./protocol.ts";
import type { Position } from "./surroundings.ts";
import type { Clock } from "./tactic.ts";

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

/** A buff or debuff on the Character, as its buff bar shows it (see `CONTEXT.md`). */
export interface Effect {
  readonly name: string;
  /** A buff rather than a debuff. */
  readonly positive: boolean;
  /** Whole seconds until it ends, counting down as the client's buff bar does. 0 once its time is up, until the server takes it off. `undefined` for an Effect without a timer. */
  readonly secondsLeft: number | undefined;
  /** A spell's, as its tooltip window shows them. `undefined` for an ability's Effect, such as Sprint. */
  readonly details: SpellDetails | undefined;
  /** Shown but doing nothing: switched off by the server, such as a weaker buff a better one of its kind replaced, or an immunity. */
  readonly disabled: boolean;
}

/** A question the server put to the Character in a window (see `CONTEXT.md`): its text, whether it asks yes or no or only says OK, and what an `answerDialog` echoes back. */
export type Dialog = Omit<DialogPercept, "kind">;

/** The Character's Pet (see `CONTEXT.md`), as its pet window shows it: the Pet's object ID, which its Surroundings know it by, and its aggression and walk settings. */
export type Pet = Omit<PetWindowPercept, "kind" | "action">;

/**
 * What the Agent currently believes about its own Character, built from Percepts (see `CONTEXT.md`).
 * The server says where the Character is at world entry and whenever it moves it itself (a jump), and how fast it may run. The Harness says where it is while it runs, and how each run ended, as the Agent's client works that out itself.
 * The server also says how healthy it is, how much Power and concentration it has, whether it sits, its level, experience and spec points, its skills, which Effects are on it, which Concentration buffs it keeps up,
 * how much Constitution it has lost, when it dies and comes back to life, which Pet it has and which Dialog it opened.
 * Of the Primitives the Mind sent it tracks only answering the Dialog, which closes it as a click does in the client, so there's one answer to "where am I and am I there yet".
 */
export class Self {
  readonly #clock: Clock;
  #position: Position | undefined;
  #heading = 0;
  #maxSpeed = 0;
  #movement: MovementStatus = { kind: "standing" };
  #objectId: number | undefined;
  #healthPercent = 100;
  #health = 0;
  #maxHealth = 0;
  #endurancePercent = 100;
  #powerPercent = 100;
  #power = 0;
  #maxPower = 0;
  #concentration = 0;
  #maxConcentration = 0;
  #concentrationBuffs: readonly ConcentrationBuff[] = [];
  #sitting = false;
  #dead = false;
  #level = 0;
  #className = "";
  #levelPermill = 0;
  #specPoints = 0;
  #skills: readonly SkillEntry[] = [];
  #specs: ReadonlyMap<string, number> = new Map();
  #effects: readonly Effect[] = [];
  #constitutionLost: number | undefined;
  #pet: Pet | undefined;
  #dialog: Dialog | undefined;

  /** `clock` is the Agent's, which an Effect's time left counts down on. */
  constructor(clock: Clock) {
    this.#clock = clock;
  }

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
        this.#powerPercent = percept.powerPercent;
        this.#power = percept.power;
        this.#maxPower = percept.maxPower;
        this.#concentration = percept.concentration;
        this.#maxConcentration = percept.maxConcentration;
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
      case "effects":
        this.#effects = percept.effects.map((entry) => effectFrom(entry, this.#clock));
        break;
      case "concentration":
        this.#concentrationBuffs = percept.buffs;
        break;
      case "stats":
        this.#constitutionLost = percept.constitutionLost;
        this.#maxHealth = percept.maxHealth;
        break;
      case "petWindow": {
        const { kind, action, ...pet } = percept;
        this.#pet = action === "Close" ? undefined : pet;
        break;
      }
      case "dialog": {
        const { kind, ...dialog } = percept;
        this.#dialog = dialog;
        break;
      }
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

  /** Takes in a Primitive as it's sent: answering the Dialog closes it. */
  sent(primitive: Primitive): void {
    if (primitive.name === "answerDialog")
      this.#dialog = undefined;
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

  /** 0 to 100. 100 until the server has said. A Character without Power, such as a pure fighter, has a `maxPower` of 0. */
  get powerPercent(): number {
    return this.#powerPercent;
  }

  get power(): number {
    return this.#power;
  }

  get maxPower(): number {
    return this.#maxPower;
  }

  /** The concentration it has left for more Concentration buffs, out of `maxConcentration`. */
  get concentration(): number {
    return this.#concentration;
  }

  get maxConcentration(): number {
    return this.#maxConcentration;
  }

  /** Every Concentration buff it keeps up, in the order it cast them, with who wears it, as its concentration window shows them: names longer than 14 characters cut to their first 12 and "..". */
  get concentrationBuffs(): readonly ConcentrationBuff[] {
    return this.#concentrationBuffs;
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

  /** Every Effect on it, in the order its buff bar shows them. */
  get effects(): readonly Effect[] {
    return this.#effects;
  }

  /** The Constitution it has lost by dying, which a Healer restores. Unknown until the server has said, at world entry. */
  get constitutionLost(): number | undefined {
    return this.#constitutionLost;
  }

  /** Its Pet, from when the server opens the pet window until it closes it, when the Pet dies or is released. */
  get pet(): Pet | undefined {
    return this.#pet;
  }

  /** The Dialog the server opened, until it's answered or a newer one takes its place. */
  get dialog(): Dialog | undefined {
    return this.#dialog;
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

/** The Effect `entry` names, ending on `clock` as many seconds from now as it has left. */
function effectFrom(entry: EffectEntry, clock: Clock): Effect {
  const endsAt = entry.secondsLeft === undefined ? undefined : clock.now() + entry.secondsLeft * 1000;

  return {
    name: entry.name,
    positive: entry.positive,
    details: entry.details,
    disabled: entry.disabled ?? false,
    get secondsLeft() {
      return endsAt === undefined ? undefined : Math.max(0, Math.ceil((endsAt - clock.now()) / 1000));
    },
  };
}

/** A jump without a position, as for `/faceloc`: the client keeps its position and takes only the heading. */
function setsOnlyHeading(jump: JumpPercept): boolean {
  return jump.x === 0 && jump.y === 0;
}
