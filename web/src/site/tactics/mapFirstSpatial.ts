import type { TacticalSheetCatalog } from '../api';
import { projectionHash, projectPhaseBots } from './botProjection';
import {
  effectiveJobResolution, postFacingIssue, tilePlacementIssue,
  type Facing, type Point, type Tactic, type TacticJob, type TacticPost,
  type TacticsDocument,
} from './tacticsDocument';

const pointKey = ([x, y]: Point) => `${x},${y}`;

/** Paint quiet phase context first and the selected phase last. SVG has no
 * z-index for ordinary children, so DOM order is the honest stacking rule. */
export function phasePaintOrder(
  phaseCount: number,
  focus: number,
  showAllPhases: boolean,
  showAdjacentPhases: boolean,
): number[] {
  if (phaseCount <= 0) return [];
  const selected = Math.min(Math.max(focus, 0), phaseCount - 1);
  const visible = Array.from({ length: phaseCount }, (_, index) => index)
    .filter((index) => showAllPhases
      || Math.abs(index - selected) <= (showAdjacentPhases ? 1 : 0));
  return [
    ...visible.filter((index) => index !== selected),
    ...(visible.includes(selected) ? [selected] : []),
  ];
}

/**
 * Copy-on-write boundary for inherited ground. A `none` job remains the saved
 * inheritance spelling until the first spatial edit. At that moment only the
 * effective ground is cloned; current-phase behavior and signatures continue
 * to win exactly as they did before materialization.
 */
export function materializeSpatialJob(
  tactic: Tactic,
  phaseIndex: number,
  groupId: string,
): TacticJob | null {
  const raw = tactic.phases[phaseIndex]?.jobs[groupId];
  if (!raw) return null;
  if (raw.kind !== 'none') return raw;
  const resolution = effectiveJobResolution(tactic, phaseIndex, groupId);
  if (!resolution?.inherited) return raw;
  const materialized = structuredClone(resolution.job);
  materialized.posture = raw.posture;
  materialized.recovery = raw.recovery;
  if (raw.travel === undefined) delete materialized.travel;
  else materialized.travel = raw.travel;
  if (raw.sigs === undefined) delete materialized.sigs;
  else materialized.sigs = structuredClone(raw.sigs);
  tactic.phases[phaseIndex].jobs[groupId] = materialized;
  return materialized;
}
function orderedSnapDeltas(desired: Point, limit: number, seed: string): Point[] {
  const deltas: Point[] = [];
  for (let radius = 0; radius <= limit; radius++) {
    for (let y = desired[1] - radius; y <= desired[1] + radius; y++) {
      for (let x = desired[0] - radius; x <= desired[0] + radius; x++) {
        if (Math.max(Math.abs(x - desired[0]), Math.abs(y - desired[1])) !== radius) continue;
        deltas.push([x, y]);
      }
    }
  }
  return deltas.sort((a, b) => {
    const distanceA = Math.max(Math.abs(a[0] - desired[0]), Math.abs(a[1] - desired[1]));
    const distanceB = Math.max(Math.abs(b[0] - desired[0]), Math.abs(b[1] - desired[1]));
    return distanceA - distanceB
      || projectionHash(`${seed}:${pointKey(a)}`) - projectionHash(`${seed}:${pointKey(b)}`);
  });
}

export interface PostTranslation {
  positions: Map<number, Point>;
  facings: Map<number, Facing>;
  snappedAnchor: Point;
}

const ROTATION_ORDER: Record<Facing, Facing[]> = {
  n: ['n', 'e', 'w', 's'],
  e: ['e', 's', 'n', 'w'],
  s: ['s', 'w', 'e', 'n'],
  w: ['w', 'n', 's', 'e'],
};

/**
 * Translate a bot selection as one rigid formation. The closest legal delta
 * to the pointer wins deterministically; unselected Posts are reservations.
 */
export function translatePostSelection(
  catalog: TacticalSheetCatalog,
  posts: readonly TacticPost[],
  slots: readonly number[],
  anchorSlot: number,
  destination: Point,
): PostTranslation | null {
  const selected = new Set(slots);
  const anchor = posts.find((post) => post.slot === anchorSlot);
  if (!anchor || !selected.has(anchorSlot)) return null;
  const moving = posts.filter((post) => post.slot != null && selected.has(post.slot));
  if (!moving.length) return null;
  const reserved = new Set(posts
    .filter((post) => post.slot == null || !selected.has(post.slot))
    .map((post) => pointKey(post.at)));
  const desired: Point = [destination[0] - anchor.at[0], destination[1] - anchor.at[1]];
  const limit = Math.max(catalog.map.width, catalog.map.height);
  for (const [dx, dy] of orderedSnapDeltas(
    desired, limit, `${catalog.map.id}:${[...selected].sort((a, b) => a - b).join(',')}`,
  )) {
    const positions = new Map<number, Point>();
    const facings = new Map<number, Facing>();
    let legal = true;
    for (const post of moving) {
      const at: Point = [post.at[0] + dx, post.at[1] + dy];
      const facing = ROTATION_ORDER[post.facing].find((candidate) =>
        postFacingIssue(catalog, { ...post, at, facing: candidate }) === null);
      if (tilePlacementIssue(catalog, at, 'anchor') !== null
        || !facing
        || reserved.has(pointKey(at))
        || [...positions.values()].some((point) => pointKey(point) === pointKey(at))) {
        legal = false;
        break;
      }
      positions.set(post.slot!, at);
      facings.set(post.slot!, facing);
    }
    if (legal) {
      return {
        positions,
        facings,
        snappedAnchor: positions.get(anchorSlot)!,
      };
    }
  }
  return null;
}

export function facingFromPointer(origin: Point, pointer: Point): Facing | null {
  const dx = pointer[0] - origin[0];
  const dy = pointer[1] - origin[1];
  if (dx === 0 && dy === 0) return null;
  return Math.abs(dx) >= Math.abs(dy)
    ? (dx > 0 ? 'e' : 'w')
    : (dy > 0 ? 's' : 'n');
}

/** A selection rotates together only when every resulting facing is legal. */
export function rotatePostSelection(
  catalog: TacticalSheetCatalog,
  posts: readonly TacticPost[],
  slots: readonly number[],
  facing: Facing,
): boolean {
  const selected = new Set(slots);
  const moving = posts.filter((post) => post.slot != null && selected.has(post.slot));
  return moving.length > 0
    && moving.every((post) => postFacingIssue(catalog, { ...post, facing }) === null);
}

function candidateTiles(catalog: TacticalSheetCatalog, objective: Point): Point[] {
  const candidates: Point[] = [];
  const limit = Math.max(catalog.map.width, catalog.map.height);
  for (let radius = 0; radius <= limit; radius++) {
    for (let y = objective[1] - radius; y <= objective[1] + radius; y++) {
      for (let x = objective[0] - radius; x <= objective[0] + radius; x++) {
        const point: Point = [x, y];
        if (Math.max(Math.abs(x - objective[0]), Math.abs(y - objective[1])) !== radius) continue;
        if (tilePlacementIssue(catalog, point, 'anchor') === null) candidates.push(point);
      }
    }
  }
  return candidates;
}

function cardinalToward(catalog: TacticalSheetCatalog, at: Point, objective: Point): Facing {
  const dx = objective[0] - at[0];
  const dy = objective[1] - at[1];
  const preferred: Facing[] = Math.abs(dx) >= Math.abs(dy)
    ? [dx >= 0 ? 'e' : 'w', dy >= 0 ? 's' : 'n', dy >= 0 ? 'n' : 's', dx >= 0 ? 'w' : 'e']
    : [dy >= 0 ? 's' : 'n', dx >= 0 ? 'e' : 'w', dx >= 0 ? 'w' : 'e', dy >= 0 ? 'n' : 's'];
  return preferred.find((facing) =>
    postFacingIssue(catalog, { at, facing }) === null) ?? preferred[0];
}

/**
 * Modest deterministic editor sugar: choose ordinary explicit Posts around a
 * target. Distance to the objective is primary, spacing is secondary, and no
 * runtime concept survives the returned array.
 */
export function suggestFormationPosts(
  catalog: TacticalSheetCatalog,
  slots: readonly number[],
  objective: Point,
  reserved: readonly Point[] = [],
  seed = 'formation',
): TacticPost[] {
  const occupied = new Set(reserved.map(pointKey));
  const candidates = candidateTiles(catalog, objective);
  const chosen: Point[] = [];
  const result: TacticPost[] = [];
  for (const slot of slots) {
    const available = candidates.filter((point) =>
      !occupied.has(pointKey(point)) && !chosen.some((other) => pointKey(other) === pointKey(point)));
    if (!available.length) break;
    available.sort((a, b) => {
      const distanceA = Math.max(Math.abs(a[0] - objective[0]), Math.abs(a[1] - objective[1]));
      const distanceB = Math.max(Math.abs(b[0] - objective[0]), Math.abs(b[1] - objective[1]));
      const spacingA = chosen.length
        ? Math.min(...chosen.map((point) => Math.max(Math.abs(a[0] - point[0]), Math.abs(a[1] - point[1])))) : 0;
      const spacingB = chosen.length
        ? Math.min(...chosen.map((point) => Math.max(Math.abs(b[0] - point[0]), Math.abs(b[1] - point[1])))) : 0;
      return distanceA - distanceB || spacingB - spacingA
        || projectionHash(`${seed}:${slot}:${pointKey(a)}`)
          - projectionHash(`${seed}:${slot}:${pointKey(b)}`);
    });
    const at = available[0];
    chosen.push(at);
    occupied.add(pointKey(at));
    result.push({ slot, at, facing: cardinalToward(catalog, at, objective) });
  }
  return result;
}

export type FormationObjective = 'mid' | 'well-own' | 'well-centre' | 'well-enemy';

export function formationObjectivePoint(
  catalog: TacticalSheetCatalog,
  objective: FormationObjective,
): Point | null {
  if (objective === 'mid') {
    return [Math.floor(catalog.map.width / 2), Math.floor(catalog.map.height / 2)];
  }
  const id = objective === 'well-own' ? 'well-north'
    : objective === 'well-centre' ? 'well-centre' : 'well-south';
  const region = catalog.map.regions.find((value) => value.id === id);
  const tile = region?.tiles[0];
  return tile ? [tile.x, tile.y] : null;
}

/** Best-effort explicit conversion for loaded geometry; returns null rather
 * than claiming a conversion when every real slot cannot be positioned. */
export function projectedPostsForLegacyJob(
  catalog: TacticalSheetCatalog,
  document: TacticsDocument,
  tactic: Tactic,
  phaseIndex: number,
  groupId: string,
): TacticPost[] | null {
  const group = tactic.groups.find((value) => value.id === groupId);
  if (!group) return null;
  const projection = projectPhaseBots(catalog, document, tactic, phaseIndex);
  const positions = projection.bots.filter((bot) => bot.groupId === groupId);
  if (positions.length !== group.slots.length) return null;
  const centre: Point = [Math.floor(catalog.map.width / 2), Math.floor(catalog.map.height / 2)];
  return group.slots.map((slot) => {
    const bot = positions.find((value) => value.slot === slot)!;
    return { slot, at: [...bot.at], facing: cardinalToward(catalog, bot.at, centre) };
  });
}
