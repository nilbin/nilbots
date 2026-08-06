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
  ReplayTick,
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
  /** Authoritative A-to-B displacement for this tick; never inferred from facing. */
  motionX: number;
  /** Authoritative A-to-B displacement for this tick; never inferred from facing. */
  motionY: number;
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
 * Monotone cubic interpolation through one authoritative axis segment.
 *
 * A same-direction run retains its boundary velocity, a step out of rest accelerates once,
 * and a hold remains exactly still. The incoming tangent uses only already-revealed motion;
 * the outgoing tangent is the current displacement, so smoothing never peeks at a future
 * action. Tangents are capped to the current step, so the curve cannot overshoot the
 * recorded segment or make tile occupancy ambiguous.
 */
function smoothAxis(
  start: number,
  end: number,
  previousDelta: number,
  fraction: number,
): number {
  const delta = end - start;
  if (delta === 0) return start;
  const tangent = (neighbour: number) =>
    Math.sign(neighbour) === Math.sign(delta)
      ? Math.sign(delta) * Math.min(Math.abs(delta), Math.abs(neighbour))
      : 0;
  const fromTangent = tangent(previousDelta);
  const toTangent = delta;
  const squared = fraction * fraction;
  const cubed = squared * fraction;
  return (
    (2 * cubed - 3 * squared + 1) * start +
    (cubed - 2 * squared + fraction) * fromTangent +
    (-2 * cubed + 3 * squared) * end +
    (cubed - squared) * toTangent
  );
}

/**
 * One causal path segment with speed continuity through an already-revealed corner.
 *
 * Positions remain on the authoritative A→B segment. The preceding displacement may
 * contribute speed, but never direction: when a new tick reveals a right-angle turn the
 * body enters the new segment at its carried speed instead of stopping on the tile centre.
 * A true reversal still brakes because carrying its old velocity through the new segment
 * would point outside the authoritative path.
 */
function smoothSegment(
  start: ReplayPosition,
  end: ReplayPosition,
  previousDeltaX: number,
  previousDeltaY: number,
  fraction: number,
): { x: number; y: number } {
  const deltaX = end.x - start.x;
  const deltaY = end.y - start.y;
  const distance = Math.hypot(deltaX, deltaY);
  if (distance === 0) return { x: start.x, y: start.y };
  const previousDistance = Math.hypot(previousDeltaX, previousDeltaY);
  const dot = deltaX * previousDeltaX + deltaY * previousDeltaY;
  const incomingSpeed =
    previousDistance > 0 && dot >= 0
      ? Math.min(distance, previousDistance)
      : 0;
  const progress = smoothAxis(
    0,
    1,
    incomingSpeed / distance,
    fraction,
  );
  return {
    x: start.x + deltaX * progress,
    y: start.y + deltaY * progress,
  };
}

type CarrierGlide = {
  x: number;
  y: number;
  motionX: number;
  motionY: number;
};

/**
 * Spread an already-resolved Arc carrier relocation across its rules-declared cadence.
 *
 * The move tick reaches the edge between the two tiles exactly at its authoritative
 * boundary. Relocation-locked ticks continue at the same visual speed from that edge to
 * the recorded centre. Only the revealed move behind the playhead is used; a later
 * direction is never read.
 */
function arcCarrierGlide(
  replay: ReplayModel,
  tickIndex: number,
  actorKey: ReplayActorLifeKey,
  fraction: number,
): CarrierGlide | null {
  if (
    replay.contract.kind !== 'v3-generic' ||
    replay.contract.mode.kind !== 'arc-relay'
  )
    return null;
  const cadence = Math.max(
    1,
    Math.floor(replay.contract.mode.coreRelocationIntervalTicks),
  );
  if (cadence <= 1) return null;

  for (let offset = 0; offset < cadence; offset += 1) {
    const sourceIndex = tickIndex - offset;
    if (sourceIndex < 0) break;
    const source = replay.ticks[sourceIndex]!;
    const start = source.before.actors.find(
      (actor) => actor.actorKey === actorKey,
    );
    const end = source.after.actors.find(
      (actor) => actor.actorKey === actorKey,
    );
    if (!start || !end) continue;
    const deltaX = end.position.x - start.position.x;
    const deltaY = end.position.y - start.position.y;
    if (deltaX === 0 && deltaY === 0) continue;

    const carriedAfter = carriedCoreFor(source.after, actorKey);
    if (
      !carriedAfter ||
      carriedAfter.nextRelocationTick !== source.tick + cadence
    )
      continue;

    let uninterrupted = true;
    for (let index = sourceIndex + 1; index <= tickIndex; index += 1) {
      const tick = replay.ticks[index]!;
      const before = tick.before.actors.find(
        (actor) => actor.actorKey === actorKey,
      );
      const after = tick.after.actors.find(
        (actor) => actor.actorKey === actorKey,
      );
      if (
        !before ||
        !after ||
        before.position.x !== after.position.x ||
        before.position.y !== after.position.y
      ) {
        uninterrupted = false;
        break;
      }
    }
    if (!uninterrupted) continue;

    const progressAt = (localFraction: number) =>
      offset === 0
        ? localFraction * 0.5
        : 0.5 +
          ((offset - 1 + localFraction) / Math.max(1, cadence - 1)) * 0.5;
    // The cadence is the movement duration, not a fresh ease-in/ease-out cycle. A linear
    // phase keeps a carrier rolling through its forced relocation hold and into the next
    // revealed relocation instead of visibly settling on every tile centre.
    const progress = progressAt(fraction);
    const tickStart = progressAt(0);
    const tickEnd = progressAt(1);
    return {
      x: start.position.x + deltaX * progress,
      y: start.position.y + deltaY * progress,
      motionX: deltaX * (tickEnd - tickStart),
      motionY: deltaY * (tickEnd - tickStart),
    };
  }
  return null;
}

function carriedCoreFor(
  state: ReplayWorldSnapshot,
  actorKey: ReplayActorLifeKey,
): { nextRelocationTick: number } | null {
  if (
    state.mode?.kind !== 'arc-relay' ||
    !('visibleCores' in state.mode)
  )
    return null;
  return state.mode.visibleCores.find(
    (core) =>
      core.disposition === 'carried' &&
      core.carrierActor?.actorKey === actorKey,
  ) ?? null;
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
  const fraction = Math.max(0, Math.min(clamped - tick, 1));
  // Position uses a monotone cubic inside the authoritative A→B segment. Its endpoint
  // tangents come only from the current and already-recorded previous displacement: a
  // continuous run glides through tile boundaries, a move out of rest accelerates once,
  // and a hold never drifts in anticipation of a later action. Facing and discrete
  // presentation changes keep their independent ease so a turn does not snap.
  const actionFraction = easeInOut(fraction);
  const before = replay.ticks[tick].before.actors;
  const after = replay.ticks[tick].after.actors;
  const previous = replay.ticks[tick - 1]?.before.actors ?? [];

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
    const prior = previous.find(
      (candidate) => candidate.actorKey === start.actorKey,
    );
    const previousMotionX = prior ? start.position.x - prior.position.x : 0;
    const previousMotionY = prior ? start.position.y - prior.position.y : 0;
    const previousAngle = prior ? directionAngle(prior.facing) : fromAngle;
    const previousRotation = shortestRotation(previousAngle, fromAngle);
    const carrierGlide = arcCarrierGlide(
      replay,
      tick,
      start.actorKey,
      fraction,
    );
    const position = carrierGlide ?? smoothSegment(
      start.position,
      end.position,
      previousMotionX,
      previousMotionY,
      fraction,
    );
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
      formId: actionFraction < 0.9 ? start.formId : end.formId,
      x: position.x,
      y: position.y,
      motionX: carrierGlide?.motionX ?? end.position.x - start.position.x,
      motionY: carrierGlide?.motionY ?? end.position.y - start.position.y,
      angle:
        fromAngle +
        smoothAxis(0, rotation, previousRotation, fraction),
      health: actionFraction < 0.6 ? start.health : end.health,
      cooldown: end.cooldown,
      pendingFormTransition,
      status: actionFraction < 0.9 ? start.status : end.status,
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
    motionX: 0,
    motionY: 0,
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
  const strikes = strikeAttackProfileIds(replay);
  const moving = new Set(
    traversals.map((traversal) => traversal.projectileId),
  );
  const poses: BoltPose[] = [];

  for (const traversal of traversals) {
    if (traversal.path.length === 0) continue;
    // A declared strike's resolution is drawn as a slash by
    // strikeSlashesAt, never as a traveling bolt — a strike is a hit on
    // the board, not a dodgeable object in flight.
    if (
      traversal.attackProfileId !== undefined &&
      strikes.has(traversal.attackProfileId)
    ) {
      continue;
    }
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

const HEADING_VECTORS: Record<ReplayProjectileHeading, [number, number]> = {
  north: [0, -1],
  'north-east': [1, -1],
  east: [1, 0],
  'south-east': [1, 1],
  south: [0, 1],
  'south-west': [-1, 1],
  west: [-1, 0],
  'north-west': [-1, -1],
};

export interface StrikeAim {
  /** The strike's frozen apex (tile coordinates). */
  origin: ReplayPosition;
  /** 0..1: how close the resolve tick is. */
  urgency: number;
  /**
   * The anchored victim's CURRENT interpolated pose — the body the strike
   * was for at declare, followed as it moves. Null when the wedge was
   * empty at declare (a pending whiff draws no ray).
   */
  target: { actorKey: ReplayActorLifeKey; x: number; y: number } | null;
  /** True once the anchored victim no longer stands on a wedge tile. */
  escaped: boolean;
}

/**
 * Tracking rays for declared strikes (owner direction 2026-08): the ray is
 * ANCHORED at declare — whoever the resolution rule would pick when the
 * cone lights — and follows that body through the windup. It does not
 * re-pick per frame; if the anchor steps off the wedge the ray reports
 * escaped (the renderers fade it) and the slash then shows who actually
 * ate the strike. The anchor mirrors the engine's single-target rule:
 * nearest body in the wedge by Chebyshev, most-central on integer-exact
 * angle ties, canonical (y, x) order last.
 */
export function strikeAimsAt(replay: ReplayModel, time: number): StrikeAim[] {
  const tickCount = replay.ticks.length;
  if (tickCount === 0) return [];
  const clamped = Math.max(0, Math.min(time, tickCount));
  const tick = Math.min(Math.floor(clamped), tickCount - 1);
  const current = replay.ticks[tick];
  const strikes = arcPendingStrikes(current);
  if (strikes.length === 0) return [];
  const poses = posesAt(replay, time);
  const aims: StrikeAim[] = [];
  for (const strike of strikes) {
    let declare = tick;
    while (
      declare > 0 &&
      arcPendingStrikes(replay.ticks[declare - 1]).some(
        (candidate) =>
          sameIdentity(candidate.shooter, strike.shooter) &&
          candidate.resolveAtTick === strike.resolveAtTick,
      )
    ) {
      declare -= 1;
    }
    const declared = replay.ticks[declare].after;
    const shooter = declared.actors.find((actor) =>
      sameIdentity(actor.identity, strike.shooter),
    );
    const origin = strike.origin ?? shooter?.position ?? null;
    if (!origin) continue;
    const urgency =
      strike.resolveAtTick - current.tick <= 1
        ? 1
        : 1 / Math.max(1, strike.resolveAtTick - current.tick);
    const wedge = new Set(
      strike.tiles.map((tile) => `${tile.x},${tile.y}`),
    );
    const heading = strike.centralHeading
      ? HEADING_VECTORS[strike.centralHeading]
      : null;
    const anchor = (strike.target
      ? declared.actors.filter((actor) =>
          sameIdentity(actor.identity, strike.target!),
        )
      : declared.actors.filter(
          (actor) =>
            !sameIdentity(actor.identity, strike.shooter) &&
            wedge.has(`${actor.position.x},${actor.position.y}`),
        )
    )
      .sort((a, b) => {
        const ringA = Math.max(
          Math.abs(a.position.x - origin.x),
          Math.abs(a.position.y - origin.y),
        );
        const ringB = Math.max(
          Math.abs(b.position.x - origin.x),
          Math.abs(b.position.y - origin.y),
        );
        if (ringA !== ringB) return ringA - ringB;
        if (heading) {
          const [ux, uy] = heading;
          const crossA = Math.abs(
            (a.position.x - origin.x) * uy - (a.position.y - origin.y) * ux,
          );
          const dotA =
            (a.position.x - origin.x) * ux + (a.position.y - origin.y) * uy;
          const crossB = Math.abs(
            (b.position.x - origin.x) * uy - (b.position.y - origin.y) * ux,
          );
          const dotB =
            (b.position.x - origin.x) * ux + (b.position.y - origin.y) * uy;
          const angle = crossA * dotB - crossB * dotA;
          if (angle !== 0) return angle;
        }
        if (a.position.y !== b.position.y) return a.position.y - b.position.y;
        return a.position.x - b.position.x;
      })[0];
    if (!anchor) {
      aims.push({ origin, urgency, target: null, escaped: false });
      continue;
    }
    const now = current.after.actors.find(
      (actor) => actor.actorKey === anchor.actorKey,
    );
    const escaped =
      !now || !wedge.has(`${now.position.x},${now.position.y}`);
    const pose = poses.find((value) => value.actorKey === anchor.actorKey);
    aims.push({
      origin,
      urgency,
      target: pose
        ? { actorKey: anchor.actorKey, x: pose.x, y: pose.y }
        : null,
      escaped,
    });
  }
  return aims;
}

type ArcPendingStrikeState = Extract<
  NonNullable<ReplayTick['after']['mode']>,
  { kind: 'arc-relay' }
>['pendingStrikes'][number];

function arcPendingStrikes(tick: ReplayTick): ArcPendingStrikeState[] {
  const mode = tick.after.mode;
  return mode && mode.kind === 'arc-relay' && 'pendingStrikes' in mode
    ? mode.pendingStrikes
    : [];
}

function sameIdentity(
  a: ReplayActorIdentity,
  b: ReplayActorIdentity,
): boolean {
  return (
    a.teamId === b.teamId && a.unitId === b.unitId && a.lifeId === b.lifeId
  );
}

const strikeProfileCache = new WeakMap<ReplayModel, ReadonlySet<string>>();

/**
 * Attack profiles whose successful attack is a declared strike (positive
 * windup): the cone telegraphs during the windup and the resolution lands
 * in-place on the resolve tick. Their traversals must never be drawn as
 * traveling bolts — a strike is a hit on the board, not a dodgeable
 * object in flight — so boltsAt skips them and strikeSlashesAt owns the
 * visual. Only the v3 generic contract declares windup; every other
 * source yields the empty set and changes nothing.
 */
export function strikeAttackProfileIds(
  replay: ReplayModel,
): ReadonlySet<string> {
  const cached = strikeProfileCache.get(replay);
  if (cached) return cached;
  const ids = new Set<string>();
  if (replay.contract.kind === 'v3-generic') {
    for (const profile of replay.contract.rawContract.rules
      .attackProfiles) {
      const windup = (
        profile.projectile as { strikeWindupTicks?: unknown }
      ).strikeWindupTicks;
      if (typeof windup === 'number' && windup > 0) ids.add(profile.id);
    }
  }
  strikeProfileCache.set(replay, ids);
  return ids;
}

export interface StrikeSlash {
  ownerActor: ReplayActorIdentity;
  /** The landed line: shooter's tile first, impact tile last. */
  points: readonly ReplayPosition[];
  /** 0 at the resolve instant, 1 when the flash has fully faded. */
  age: number;
}

/**
 * Matured strike resolutions flashing at continuous playhead `time`.
 *
 * The whole line exists at once for the resolve tick and fades over it —
 * the visual is the cone collapsing into the one line that landed, not a
 * projectile traveling. Same tick/fraction derivation as boltsAt so the
 * two grammars can never overlap on a projectile.
 */
export function strikeSlashesAt(
  replay: ReplayModel,
  time: number,
): StrikeSlash[] {
  const tickCount = replay.ticks.length;
  if (tickCount === 0) return [];
  const clamped = Math.max(0, Math.min(time, tickCount));
  const tick = Math.min(Math.floor(clamped), tickCount - 1);
  const fraction = Math.max(0, Math.min(clamped - tick, 1));
  const strikes = strikeAttackProfileIds(replay);
  if (strikes.size === 0) return [];
  const slashes: StrikeSlash[] = [];
  for (const traversal of replay.ticks[tick].projectileTraversals) {
    if (
      traversal.attackProfileId === undefined ||
      !strikes.has(traversal.attackProfileId) ||
      traversal.path.length === 0
    ) {
      continue;
    }
    slashes.push({
      ownerActor: traversal.ownerActor,
      points: [traversal.from, ...traversal.path],
      age: fraction,
    });
  }
  return slashes;
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
  const strikes = strikeAttackProfileIds(replay);
  for (const traversal of deathTick.projectileTraversals) {
    // Strike resolutions end in their slash, not in a bolt's
    // dissipation ring — the two grammars must stay unmistakable.
    if (
      traversal.attackProfileId !== undefined &&
      strikes.has(traversal.attackProfileId)
    ) {
      continue;
    }
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
