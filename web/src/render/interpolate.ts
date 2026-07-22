import type { Direction, ReplayBotState, ReplayDocument } from '../types';

export interface BotPose {
  slot: number;
  x: number;
  y: number;
  angle: number;
  health: number;
  cooldown: number;
  status: ReplayBotState['status'];
}

const DIRECTION_ANGLE: Record<Direction, number> = {
  North: -Math.PI / 2,
  East: 0,
  South: Math.PI / 2,
  West: Math.PI,
};

export function directionAngle(direction: Direction): number {
  return DIRECTION_ANGLE[direction];
}

/** Authoritative bot states *before* a given tick executes. */
export function stateBefore(replay: ReplayDocument, tick: number): ReplayBotState[] {
  if (tick <= 0) {
    return replay.header.participants.map((participant) => ({
      slot: participant.slot,
      x: participant.spawnX,
      y: participant.spawnY,
      facing: participant.spawnFacing,
      health: 3,
      cooldown: 0,
      status: 'Active' as const,
    }));
  }
  const index = Math.min(tick, replay.ticks.length) - 1;
  return replay.ticks[index].state;
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
export function posesAt(replay: ReplayDocument, time: number): BotPose[] {
  const tickCount = replay.ticks.length;
  const clamped = Math.max(0, Math.min(time, tickCount));
  const tick = Math.min(Math.floor(clamped), tickCount - 1);
  const fraction = easeInOut(Math.max(0, Math.min(clamped - tick, 1)));
  const before = stateBefore(replay, tick);
  const after = stateBefore(replay, tick + 1);

  return before.map((start) => {
    const end = after.find((s) => s.slot === start.slot) ?? start;
    const fromAngle = directionAngle(start.facing);
    const rotation = shortestRotation(fromAngle, directionAngle(end.facing));
    return {
      slot: start.slot,
      x: start.x + (end.x - start.x) * fraction,
      y: start.y + (end.y - start.y) * fraction,
      angle: fromAngle + rotation * fraction,
      health: fraction < 0.6 ? start.health : end.health,
      cooldown: end.cooldown,
      status: fraction < 0.9 ? start.status : end.status,
    };
  });
}
