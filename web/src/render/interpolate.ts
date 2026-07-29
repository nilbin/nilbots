import type {
  ReplayActorIdentity,
  ReplayActorLifeKey,
  ReplayActorSpawnReason,
  ReplayDirection,
  ReplayFormTransition,
  ReplayModel,
  ReplayPosition,
  ReplayProjectileHeading,
  ReplayStableUnitKey,
  ReplayUnitLifecycleStatus,
  ReplayWorldSnapshot,
} from '../replayModel';
import { isArrivalEvent } from '../replayModel';

export interface BotPose {
  actorKey: ReplayActorLifeKey;
  unitKey: ReplayStableUnitKey;
  teamId: number;
  unitId: number;
  lifeId: number;
  formId: string;
  x: number;
  y: number;
  angle: number;
  health: number;
  cooldown: number;
  pendingFormTransition: ReplayFormTransition | null;
  status: ReplayUnitLifecycleStatus;
}

const DIRECTION_ANGLE: Record<ReplayDirection, number> = {
  north: -Math.PI / 2,
  east: 0,
  south: Math.PI / 2,
  west: Math.PI,
};

export function directionAngle(direction: ReplayDirection): number {
  return DIRECTION_ANGLE[direction];
}

/** Where an absolute projectile heading points, in radians, in screen space. */
export const headingAngle: Record<ReplayProjectileHeading, number> = {
  ...DIRECTION_ANGLE,
  'north-east': -Math.PI / 4,
  'south-east': Math.PI / 4,
  'south-west': (3 * Math.PI) / 4,
  'north-west': (-3 * Math.PI) / 4,
};

/** One tile step along an absolute projectile heading. */
export const headingStep: Record<
  ReplayProjectileHeading,
  readonly [number, number]
> = {
  north: [0, -1],
  east: [1, 0],
  south: [0, 1],
  west: [-1, 0],
  'north-east': [1, -1],
  'south-east': [1, 1],
  'south-west': [-1, 1],
  'north-west': [-1, -1],
};

/** Authoritative world immediately before a given tick executes. */
export function stateBefore(
  replay: ReplayModel,
  tick: number,
): ReplayWorldSnapshot | null {
  if (tick <= 0) return replay.initialWorld;
  return replay.ticks[Math.min(tick, replay.ticks.length) - 1]?.after ?? null;
}

function shortestRotation(from: number, to: number): number {
  let delta = to - from;
  while (delta > Math.PI) delta -= 2 * Math.PI;
  while (delta < -Math.PI) delta += 2 * Math.PI;
  return delta;
}

function easeInOut(t: number): number {
  return t < 0.5 ? 2 * t * t : 1 - (-2 * t + 2) ** 2 / 2;
}

/**
 * Interpolated presentation poses at continuous playhead `time`.
 * The renderer animates between authoritative states; it never invents them (plan §32).
 */
export function posesAt(replay: ReplayModel, time: number): BotPose[] {
  const tickCount = replay.ticks.length;
  if (tickCount === 0) {
    return replay.initialWorld?.actors.map(poseFromState) ?? [];
  }
  const clamped = Math.max(0, Math.min(time, tickCount));
  const tick = Math.min(Math.floor(clamped), tickCount - 1);
  const fraction = easeInOut(Math.max(0, Math.min(clamped - tick, 1)));
  const before = replay.ticks[tick].before.actors;
  const after = replay.ticks[tick].after.actors;

  return before.map((start) => {
    // Actor life, never stable unit, is the interpolation identity. A destroyed
    // life can collapse where it stood, but it must never fly across a respawn
    // gap into the next life occupying the same unit slot.
    const end =
      after.find((candidate) => candidate.actorKey === start.actorKey) ?? {
        ...start,
        health: 0,
        status: 'destroyed' as const,
    };
    const fromAngle = directionAngle(start.facing);
    const rotation = shortestRotation(fromAngle, directionAngle(end.facing));
    const startedTransitionEvent = replay.ticks[tick].events.find(
      (event) =>
        event.type === 'form-transition-started' &&
        event.sourceActor?.actorKey === start.actorKey &&
        event.fromFormId !== null &&
        event.toFormId !== null &&
        event.formTransitionStartedAtTick !== null &&
        event.formTransitionCompletesAtTick !== null,
    );
    const startedTransition = startedTransitionEvent
      ? {
          fromFormId: startedTransitionEvent.fromFormId!,
          toFormId: startedTransitionEvent.toFormId!,
          startedAtTick:
            startedTransitionEvent.formTransitionStartedAtTick!,
          completesAtTick:
            startedTransitionEvent.formTransitionCompletesAtTick!,
        }
      : null;
    const pendingFormTransition =
      startedTransition !== null && fraction < 0.9
        ? startedTransition
        : start.pendingFormTransition !== null &&
            end.pendingFormTransition === null
        ? fraction < 0.9
          ? start.pendingFormTransition
          : null
        : (end.pendingFormTransition ?? start.pendingFormTransition);
    return {
      actorKey: start.actorKey,
      unitKey: start.unitKey,
      teamId: start.identity.teamId,
      unitId: start.identity.unitId,
      lifeId: start.identity.lifeId,
      formId: fraction < 0.9 ? start.formId : end.formId,
      x:
        start.position.x +
        (end.position.x - start.position.x) * fraction,
      y:
        start.position.y +
        (end.position.y - start.position.y) * fraction,
      angle: fromAngle + rotation * fraction,
      health: fraction < 0.6 ? start.health : end.health,
      cooldown: end.cooldown,
      pendingFormTransition,
      status: fraction < 0.9 ? start.status : end.status,
    };
  });
}

function poseFromState(
  state: NonNullable<ReplayModel['initialWorld']>['actors'][number],
): BotPose {
  return {
    actorKey: state.actorKey,
    unitKey: state.unitKey,
    teamId: state.identity.teamId,
    unitId: state.identity.unitId,
    lifeId: state.identity.lifeId,
    formId: state.formId,
    x: state.position.x,
    y: state.position.y,
    angle: directionAngle(state.facing),
    health: state.health,
    cooldown: state.cooldown,
    pendingFormTransition: state.pendingFormTransition,
    status: state.status,
  };
}

export interface BoltPose {
  /** Replay-local projectile identity, kept as exact decimal text for replay-v2. */
  id: string;
  ownerActor: ReplayActorIdentity;
  /** Tile coordinates, interpolated along the authoritative substep path. */
  x: number;
  y: number;
  heading: ReplayProjectileHeading;
  /** True when the projectile advances on the next tick. */
  imminent: boolean;
  /** Substeps the projectile takes per advance, for its lane warning. */
  tilesPerAdvance: number;
  /** The authoritative locked future arc, when the shot has one. */
  programmedPath: readonly ReplayPosition[] | null;
}

/**
 * Interpolated projectile poses at continuous playhead `time`.
 *
 * Replay traversals are authoritative ordered substeps. A speed-two path A→B→C is
 * shown as A→B in the first half and B→C in the second; straight interpolation from
 * A to C could put a projectile somewhere the engine never did. This consumes only
 * the normalized ReplayModel, so replay-v1 and replay-v2 share the same derivation.
 */
export function boltsAt(replay: ReplayModel, time: number): BoltPose[] {
  const tickCount = replay.ticks.length;
  if (tickCount === 0) return [];
  const clamped = Math.max(0, Math.min(time, tickCount));
  const tick = Math.min(Math.floor(clamped), tickCount - 1);
  const fraction = Math.max(0, Math.min(clamped - tick, 1));
  const current = replay.ticks[tick];
  const traversals = current.projectileTraversals;
  const bolts = current.after.projectiles ?? [];
  const moving = new Set(
    traversals.map((traversal) => traversal.projectileId),
  );
  const poses: BoltPose[] = [];

  for (const traversal of traversals) {
    if (traversal.path.length === 0) continue;
    const points = [traversal.from, ...traversal.path];
    const progress = fraction * traversal.path.length;
    const segment = Math.min(
      Math.floor(progress),
      traversal.path.length - 1,
    );
    const local = Math.min(1, progress - segment);
    const from = points[segment];
    const to = points[segment + 1];
    poses.push({
      id: traversal.projectileId,
      ownerActor: traversal.ownerActor,
      x: from.x + (to.x - from.x) * local,
      y: from.y + (to.y - from.y) * local,
      heading: headingBetween(from.x, from.y, to.x, to.y),
      imminent: false,
      tilesPerAdvance: 1,
      programmedPath: traversal.programmedPath,
    });
  }

  for (const bolt of bolts) {
    if (moving.has(bolt.projectileId)) continue;
    poses.push({
      id: bolt.projectileId,
      ownerActor: bolt.ownerActor,
      x: bolt.position.x,
      y: bolt.position.y,
      heading: bolt.heading ?? bolt.launchDirection,
      imminent: bolt.ticksUntilAdvance === 1,
      tilesPerAdvance: bolt.tilesPerAdvance ?? 1,
      programmedPath: bolt.programmedPath,
    });
  }
  return poses;
}

export interface SpentBolt {
  id: string;
  ownerActor: ReplayActorIdentity;
  /** Where the projectile was when it stopped existing. */
  x: number;
  y: number;
  /** 0 at disappearance, 1 when the dissipation is over. */
  age: number;
}

/**
 * Projectiles that disappeared in the preceding authoritative tick.
 *
 * This is derived from normalized before/after state plus that tick's traversal,
 * never accumulated by the renderer, so seeking cannot strand cosmetic effects.
 */
export function spentBoltsAt(
  replay: ReplayModel,
  time: number,
): SpentBolt[] {
  const tickCount = replay.ticks.length;
  if (tickCount === 0) return [];
  const clamped = Math.max(0, Math.min(time, tickCount));
  const tick = Math.min(Math.floor(clamped), tickCount - 1);
  const fraction = Math.max(0, Math.min(clamped - tick, 1));
  const deathTick = replay.ticks[tick - 1];
  if (!deathTick) return [];

  const survived = new Set(
    (deathTick.after.projectiles ?? []).map(
      (projectile) => projectile.projectileId,
    ),
  );
  const candidates = new Map<
    string,
    {
      ownerActor: ReplayActorIdentity;
      position: ReplayPosition;
    }
  >();
  for (const projectile of deathTick.before.projectiles ?? []) {
    candidates.set(projectile.projectileId, {
      ownerActor: projectile.ownerActor,
      position: projectile.position,
    });
  }
  for (const traversal of deathTick.projectileTraversals) {
    candidates.set(traversal.projectileId, {
      ownerActor: traversal.ownerActor,
      position:
        traversal.path[traversal.path.length - 1] ?? traversal.from,
    });
  }

  const spent: SpentBolt[] = [];
  for (const [id, candidate] of candidates) {
    if (survived.has(id)) continue;
    spent.push({
      id,
      ownerActor: candidate.ownerActor,
      x: candidate.position.x,
      y: candidate.position.y,
      age: fraction,
    });
  }
  return spent;
}

export interface Arrival {
  actorKey: ReplayActorLifeKey;
  unitKey: ReplayStableUnitKey;
  teamId: number;
  /** Tile the life materialized on. */
  x: number;
  y: number;
  /** Why it arrived, in the source document's own vocabulary. Never keyed off. */
  reason: ReplayActorSpawnReason | null;
  /** 0 the instant it appears, 1 when the materialization is over. */
  age: number;
}

/**
 * How far the materialization runs through the tick that carries it.
 *
 * It has to be over well before the tick is, because a life that arrives may act on its
 * creation tick: a child fabricated at the front can be shooting by the time this
 * finishes, and a body still condensing while it fires reads as a rendering bug.
 */
const ARRIVAL_SPAN = 0.75;

/**
 * Lives that materialized at the start of this tick.
 *
 * The mirror of `spentBoltsAt`, and derived the same way — from the normalized document,
 * never accumulated — so seeking backwards into a tick before a fabrication cannot leave a
 * spawn ring hanging in the air.
 *
 * Lifecycle is applied at *tick start*, so an arriving life is already in this tick's
 * opening state and its position comes from there rather than from the event: replay-v2
 * carries the pad in `to` and generation-3 carries it in the payload's `position`, and the
 * authoritative state is the one thing that says the same thing in both. The event is
 * still what decides *that* it arrived, through the model's predicate, because a life
 * appearing between two snapshots is an inference and a lifecycle event is a fact.
 */
export function arrivalsAt(replay: ReplayModel, time: number): Arrival[] {
  const tickCount = replay.ticks.length;
  if (tickCount === 0) return [];
  const clamped = Math.max(0, Math.min(time, tickCount));
  const index = Math.min(Math.floor(clamped), tickCount - 1);
  const fraction = Math.max(0, Math.min(clamped - index, 1));
  const age = fraction / ARRIVAL_SPAN;
  if (age > 1) return [];

  const tick = replay.ticks[index];
  const arrivals: Arrival[] = [];
  const seen = new Set<ReplayActorLifeKey>();
  for (const event of [...tick.lifecycleEvents, ...tick.events]) {
    if (!isArrivalEvent(event.type)) continue;
    const actor = event.sourceActor ?? event.targetActor;
    if (!actor || seen.has(actor.actorKey)) continue;
    const state = tick.before.actors.find(
      (candidate) => candidate.actorKey === actor.actorKey,
    );
    const position = state?.position ?? event.to ?? event.from;
    if (!position) continue;
    seen.add(actor.actorKey);
    arrivals.push({
      actorKey: actor.actorKey,
      unitKey: actor.unitKey,
      teamId: actor.teamId,
      x: position.x,
      y: position.y,
      reason: event.spawnReason,
      age,
    });
  }
  return arrivals;
}

export function headingBetween(
  fromX: number,
  fromY: number,
  toX: number,
  toY: number,
): ReplayProjectileHeading {
  const dx = Math.sign(toX - fromX);
  const dy = Math.sign(toY - fromY);
  if (dx === 0 && dy < 0) return 'north';
  if (dx > 0 && dy < 0) return 'north-east';
  if (dx > 0 && dy === 0) return 'east';
  if (dx > 0 && dy > 0) return 'south-east';
  if (dx === 0 && dy > 0) return 'south';
  if (dx < 0 && dy > 0) return 'south-west';
  if (dx < 0 && dy === 0) return 'west';
  return 'north-west';
}
