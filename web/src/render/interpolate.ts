import type {
  ReplayActorLifeKey,
  ReplayDirection,
  ReplayFormTransition,
  ReplayModel,
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
