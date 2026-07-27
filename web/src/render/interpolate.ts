import type {
  Direction,
  ProjectileHeading,
  ReplayBotState,
  ReplayDocument,
} from '../types';
import { replayMaxHealth } from '../replayMetadata';

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

/** Where a bolt on this heading points, in radians, screen-space and shared by both renderers. */
export const headingAngle: Record<ProjectileHeading, number> = {
  ...DIRECTION_ANGLE,
  NorthEast: -Math.PI / 4,
  SouthEast: Math.PI / 4,
  SouthWest: (3 * Math.PI) / 4,
  NorthWest: (-3 * Math.PI) / 4,
};

/** One tile step along a heading. */
export const headingStep: Record<ProjectileHeading, readonly [number, number]> = {
  North: [0, -1],
  East: [1, 0],
  South: [0, 1],
  West: [-1, 0],
  NorthEast: [1, -1],
  SouthEast: [1, 1],
  SouthWest: [-1, 1],
  NorthWest: [-1, -1],
};

/** Authoritative bot states *before* a given tick executes. */
export function stateBefore(replay: ReplayDocument, tick: number): ReplayBotState[] {
  if (tick <= 0) {
    return replay.header.participants.map((participant) => ({
      slot: participant.slot,
      x: participant.spawnX,
      y: participant.spawnY,
      facing: participant.spawnFacing,
      health: replayMaxHealth(replay),
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

export interface BoltPose {
  /** Replay-local bolt identity, stable across ticks — 0 on replays that predate ids. */
  id: number;
  ownerSlot: number;
  /** Tile coordinates, interpolated along the authoritative substep path. */
  x: number;
  y: number;
  heading: ProjectileHeading;
  /** True when the bolt advances on the *next* tick — the lane ahead is about to be hit. */
  imminent: boolean;
  /** Substeps this bolt takes per advance, so a warning can show its whole reach. */
  tilesPerAdvance: number;
  /** The locked future arc, for replays with programmed shots. */
  programmedPath?: number[][];
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
export function boltsAt(replay: ReplayDocument, time: number): BoltPose[] {
  const tickCount = replay.ticks.length;
  const tick = Math.min(Math.floor(Math.max(0, time)), tickCount - 1);
  const fraction = Math.max(0, Math.min(time - tick, 1));
  const current = replay.ticks[tick];
  if (!current) return [];

  const traversals = current.projectileTraversals ?? [];
  const bolts = current.projectiles ?? [];
  const moving = new Set(traversals.map((move) => move.id));
  const poses: BoltPose[] = [];

  for (const move of traversals) {
    if (move.path.length === 0) continue;
    const points = [[move.fromX, move.fromY], ...move.path];
    const progress = fraction * move.path.length;
    const segment = Math.min(Math.floor(progress), move.path.length - 1);
    const local = Math.min(1, progress - segment);
    const [fromX, fromY] = points[segment];
    const [toX, toY] = points[segment + 1];
    poses.push({
      id: move.id,
      ownerSlot: move.ownerSlot,
      x: fromX + (toX - fromX) * local,
      y: fromY + (toY - fromY) * local,
      // Derived from the substep actually being travelled rather than the bolt's recorded
      // heading, so a programmed arc points along the leg it is on, not where it started.
      heading: headingBetween(fromX, fromY, toX, toY),
      imminent: false,
      tilesPerAdvance: 1,
      programmedPath: move.programmedPath,
    });
  }

  // A bolt with no traversal this tick is holding position — mid-flight between advances,
  // which is most of a slow projectile's life.
  for (const bolt of bolts) {
    if (moving.has(bolt.id ?? 0)) continue;
    poses.push({
      id: bolt.id ?? 0,
      ownerSlot: bolt.ownerSlot,
      x: bolt.x,
      y: bolt.y,
      heading: bolt.heading ?? bolt.direction,
      imminent: bolt.ticksUntilAdvance === 1,
      tilesPerAdvance: bolt.tilesPerAdvance ?? 1,
      programmedPath: bolt.programmedPath,
    });
  }
  return poses;
}

export function headingBetween(
  fromX: number,
  fromY: number,
  toX: number,
  toY: number,
): ProjectileHeading {
  const dx = Math.sign(toX - fromX);
  const dy = Math.sign(toY - fromY);
  if (dx === 0 && dy < 0) return 'North';
  if (dx > 0 && dy < 0) return 'NorthEast';
  if (dx > 0 && dy === 0) return 'East';
  if (dx > 0 && dy > 0) return 'SouthEast';
  if (dx === 0 && dy > 0) return 'South';
  if (dx < 0 && dy > 0) return 'SouthWest';
  if (dx < 0 && dy === 0) return 'West';
  return 'NorthWest';
}
