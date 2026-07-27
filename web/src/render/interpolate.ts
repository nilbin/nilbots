import type {
  ReplayActorLifeKey,
  ReplayDirection,
  ReplayFormTransition,
  ReplayModel,
  ReplayPosition,
  ReplayProjectileHeading,
  ReplayStableUnitKey,
  ReplayUnitLifecycleStatus,
  ReplayWorldSnapshot,
} from '../replayModel';

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

/** Where a bolt on this heading points, in radians, shared by both renderers. */
export const headingAngle: Record<ReplayProjectileHeading, number> = {
  ...DIRECTION_ANGLE,
  'north-east': -Math.PI / 4,
  'south-east': Math.PI / 4,
  'south-west': (3 * Math.PI) / 4,
  'north-west': (-3 * Math.PI) / 4,
};

/** One tile step along a heading. */
export const headingStep: Record<ReplayProjectileHeading, readonly [number, number]> = {
  north: [0, -1],
  east: [1, 0],
  south: [0, 1],
  west: [-1, 0],
  'north-east': [1, -1],
  'south-east': [1, 1],
  'south-west': [-1, 1],
  'north-west': [-1, -1],
};

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

export interface BoltPose {
  projectileId: string;
  /** The exact firing life, which outlives the unit that fired it. */
  ownerActorKey: ReplayActorLifeKey;
  /** The slot that life belonged to, which is what carries the look and accent. */
  ownerUnitKey: ReplayStableUnitKey;
  /** Tile coordinates, interpolated along the authoritative substep path. */
  x: number;
  y: number;
  heading: ReplayProjectileHeading;
  /** True when the bolt advances on the *next* tick — the lane ahead is about to be hit. */
  imminent: boolean;
  /** Substeps this bolt takes per advance, so a warning can show its whole reach. */
  tilesPerAdvance: number;
  /** The locked future arc, for replays with programmed shots. */
  programmedPath: ReplayPosition[] | null;
}

/**
 * Interpolated projectile poses at continuous playhead `time` — the bolt half of `posesAt`.
 *
 * **Replay traversals are authoritative ordered substeps**, not a start and an end. A
 * speed-two bolt is recorded as A→B→C, and interpolating across the whole path is what
 * makes it read A→B in the first half of the visual tick and B→C in the second, so a hit
 * on the first substep ends at B rather than somewhere the engine never put it. Treating
 * the tick as one straight A→C slide would put the bolt inside a wall it went around.
 *
 * Motion here is deliberately **not eased**, unlike a bot's. A bot accelerating out of a
 * tile and settling into the next reads as a machine deciding to move; a projectile doing
 * it reads as a bug, because a bolt in flight has no reason to slow down at a tile edge.
 */
export function boltsAt(replay: ReplayModel, time: number): BoltPose[] {
  const tickCount = replay.ticks.length;
  if (tickCount === 0) return [];
  const tick = Math.min(Math.floor(Math.max(0, time)), tickCount - 1);
  const fraction = Math.max(0, Math.min(time - tick, 1));
  const current = replay.ticks[tick];
  if (!current) return [];

  const traversals = current.projectileTraversals;
  const bolts = current.after.projectiles ?? [];
  const moving = new Set(traversals.map((move) => move.projectileId));
  const poses: BoltPose[] = [];

  for (const move of traversals) {
    if (move.path.length === 0) continue;
    const points = [move.from, ...move.path];
    const progress = fraction * move.path.length;
    const segment = Math.min(Math.floor(progress), move.path.length - 1);
    const local = Math.min(1, progress - segment);
    const from = points[segment];
    const to = points[segment + 1];
    poses.push({
      projectileId: move.projectileId,
      ownerActorKey: move.ownerActorKey,
      ownerUnitKey: move.ownerActor.unitKey,
      x: from.x + (to.x - from.x) * local,
      y: from.y + (to.y - from.y) * local,
      // Derived from the substep actually being travelled rather than the bolt's recorded
      // heading, so a programmed arc points along the leg it is on, not where it started.
      heading: headingBetween(from.x, from.y, to.x, to.y),
      imminent: false,
      tilesPerAdvance: 1,
      programmedPath: move.programmedPath,
    });
  }

  // A bolt with no traversal this tick is holding position — mid-flight between advances,
  // which is most of a slow projectile's life.
  for (const bolt of bolts) {
    if (moving.has(bolt.projectileId)) continue;
    poses.push({
      projectileId: bolt.projectileId,
      ownerActorKey: bolt.ownerActorKey,
      ownerUnitKey: bolt.ownerActor.unitKey,
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
  projectileId: string;
  ownerActorKey: ReplayActorLifeKey;
  ownerUnitKey: ReplayStableUnitKey;
  /** Where it was when it stopped existing. */
  x: number;
  y: number;
  /** 0 at the instant it went, 1 when the dissipation is over. */
  age: number;
}

/**
 * Bolts that stopped existing, and where they were when they did.
 *
 * A projectile reaching the end of its range simply left the replay, and a renderer simply
 * stopped drawing it — a bolt in flight one frame and nothing the next. Whatever it ran out
 * of, it did not run out of it instantaneously.
 *
 * Derived rather than watched for: a bolt is **dead in tick D** if the engine listed it
 * alive after D−1 and not after D. That is a fact about the document, so scrubbing
 * backwards past a despawn does not strand a puff of light in mid-air, and scrubbing
 * forwards over one does not skip it.
 *
 * The burst is drawn in the tick *after* the death, because the bolt itself is still being
 * drawn during D — it travels its last leg and expires at the end of it.
 */
export function spentBoltsAt(replay: ReplayModel, time: number): SpentBolt[] {
  const tickCount = replay.ticks.length;
  if (tickCount === 0) return [];
  const tick = Math.min(Math.floor(Math.max(0, time)), tickCount - 1);
  const fraction = Math.max(0, Math.min(time - tick, 1));
  const before = replay.ticks[tick - 2];
  const died = replay.ticks[tick - 1];
  if (!before || !died) return [];

  const survived = new Set(
    (died.after.projectiles ?? []).map((bolt) => bolt.projectileId),
  );
  const spent: SpentBolt[] = [];
  for (const bolt of before.after.projectiles ?? []) {
    if (survived.has(bolt.projectileId)) continue;
    // Its last leg, if it moved before expiring; otherwise it died where it sat.
    const move = died.projectileTraversals.find(
      (each) => each.projectileId === bolt.projectileId,
    );
    const last = move?.path[move.path.length - 1];
    spent.push({
      projectileId: bolt.projectileId,
      ownerActorKey: bolt.ownerActorKey,
      ownerUnitKey: bolt.ownerActor.unitKey,
      x: last ? last.x : bolt.position.x,
      y: last ? last.y : bolt.position.y,
      age: fraction,
    });
  }
  return spent;
}
