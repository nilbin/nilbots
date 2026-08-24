import type { TacticalSheetCatalog } from '../api';
import { makePathfinder } from './pathing';
import {
  effectiveJobResolution, tilePlacementIssue,
  type Point, type Tactic, type TacticJob, type TacticsDocument,
  type WorkshopChoice,
} from './tacticsDocument';

/** The rules-owned training tile for a Workshop, straight from the catalog's
 * map export — the editor never invents it. The editor authors from west's
 * perspective, so own previews south and enemy previews north. `either`
 * previews north because the executor's match-time choice is unknowable. */
export function workshopTrainingTile(
  catalog: TacticalSheetCatalog,
  workshop: WorkshopChoice,
): Point | null {
  const order = workshop === 'own' ? ['south']
    : workshop === 'enemy' ? ['north']
      : ['north', 'south'];
  for (const id of order) {
    const tile = catalog.map.regions
      .find((region) => region.id === `veterancy-workshop-${id}-train`)?.tiles[0];
    if (tile) return [tile.x, tile.y];
  }
  return null;
}

/** A presentation-only tile for one stable squad slot. Exact Posts are
 * authoritative; every other entry is an explicitly labelled projection and
 * never enters the saved tactics document. */
export interface BotProjection {
  slot: number;
  groupId: string;
  phaseIndex: number;
  sourcePhaseIndex: number;
  at: Point;
  exact: boolean;
  carried: boolean;
  jobKind: TacticJob['kind'];
}

export interface PhaseBotProjection {
  bots: BotProjection[];
  /** Groups whose loaded/advanced assignment cannot honestly imply tiles. */
  unresolvedGroupIds: string[];
}

/** Stable slot selection for a map-drawn marquee. Exact and projected bots
 * share this presentation shape, so the gesture does not care how a phase's
 * positions were authored. Rect edges are inclusive in tile coordinates. */
export function slotsInMapRect(bots: readonly BotProjection[], rect: readonly Point[]): number[] {
  if (rect.length < 2) return [];
  const [a, b] = rect;
  const x0 = Math.min(a[0], b[0]);
  const y0 = Math.min(a[1], b[1]);
  const x1 = Math.max(a[0], b[0]);
  const y1 = Math.max(a[1], b[1]);
  return [...new Set(bots
    .filter(({ at }) => at[0] >= x0 && at[0] <= x1 && at[1] >= y0 && at[1] <= y1)
    .map(({ slot }) => slot))].sort((left, right) => left - right);
}

const key = ([x, y]: Point) => `${x},${y}`;

/** Only authored ground participates. Posture, recovery and current-phase
 * signatures may change while the group keeps precisely the same ground. */
export function groundFingerprint(job: TacticJob): string {
  return JSON.stringify({
    kind: job.kind,
    posts: job.posts?.map((post) => ({ at: post.at, slot: post.slot })) ?? null,
    rect: job.rect ?? null,
    path: job.path ?? null,
    point: job.point ?? null,
    workshop: job.workshop ?? null,
  });
}

/** FNV-1a with Math.imul: tiny, platform-stable and entirely deterministic. */
export function projectionHash(value: string): number {
  let hash = 0x811c9dc5;
  for (let index = 0; index < value.length; index++) {
    hash ^= value.charCodeAt(index);
    hash = Math.imul(hash, 0x01000193);
  }
  return hash >>> 0;
}

function chebyshev(a: Point, b: Point): number {
  return Math.max(Math.abs(a[0] - b[0]), Math.abs(a[1] - b[1]));
}

function unique(points: Point[]): Point[] {
  const seen = new Set<string>();
  return points.filter((point) => {
    const id = key(point);
    if (seen.has(id)) return false;
    seen.add(id);
    return true;
  });
}

function chooseSpaced(
  candidates: Point[], slots: number[], seed: string, occupied: Set<string>,
): (Point | null)[] {
  const chosen: Point[] = [];
  return slots.map((slot) => {
    const free = candidates.filter((point) =>
      !occupied.has(key(point)) && !chosen.some((other) => key(other) === key(point)));
    const pool = free.length ? free : candidates.filter((point) =>
      !chosen.some((other) => key(other) === key(point)));
    if (!pool.length) return null;
    const references = [...chosen];
    const ranked = [...pool].sort((a, b) => {
      const spacingA = references.length
        ? Math.min(...references.map((point) => chebyshev(a, point))) : 0;
      const spacingB = references.length
        ? Math.min(...references.map((point) => chebyshev(b, point))) : 0;
      if (spacingA !== spacingB) return spacingB - spacingA;
      return projectionHash(`${seed}:${slot}:${key(a)}`)
        - projectionHash(`${seed}:${slot}:${key(b)}`);
    });
    const point = ranked[0];
    chosen.push(point);
    occupied.add(key(point));
    return point;
  });
}

function around(
  catalog: TacticalSheetCatalog, centre: Point, occupied: Set<string>,
  slots: number[], seed: string,
): (Point | null)[] {
  const candidates: Point[] = [];
  const limit = Math.max(catalog.map.width, catalog.map.height);
  for (let radius = 0; radius <= limit; radius++) {
    const shell: Point[] = [];
    for (let y = centre[1] - radius; y <= centre[1] + radius; y++) {
      for (let x = centre[0] - radius; x <= centre[0] + radius; x++) {
        const point: Point = [x, y];
        if (chebyshev(point, centre) !== radius) continue;
        if (tilePlacementIssue(catalog, point, 'anchor') === null) shell.push(point);
      }
    }
    shell.sort((a, b) => projectionHash(`${seed}:${key(a)}`)
      - projectionHash(`${seed}:${key(b)}`));
    candidates.push(...shell);
    const available = candidates.filter((point) => !occupied.has(key(point))).length;
    if (available >= slots.length) break;
  }
  const chosen: Point[] = [];
  return slots.map((slot) => {
    // Radius remains the primary truth; the bot slot supplies stable noise
    // only within a shell.
    const ranked = [...candidates].sort((a, b) => chebyshev(a, centre) - chebyshev(b, centre)
      || projectionHash(`${seed}:${slot}:${key(a)}`)
        - projectionHash(`${seed}:${slot}:${key(b)}`));
    const point = ranked.find((candidate) =>
      !occupied.has(key(candidate)) && !chosen.some((other) => key(other) === key(candidate)))
      ?? ranked.find((candidate) => !chosen.some((other) => key(other) === key(candidate)))
      ?? null;
    if (point) {
      chosen.push(point);
      occupied.add(key(point));
    }
    return point;
  });
}

function alongRoute(
  route: Point[], slots: number[], seed: string, occupied: Set<string>,
): (Point | null)[] {
  const candidates = unique(route);
  if (!candidates.length) return slots.map(() => null);
  const offset = projectionHash(`${seed}:${slots.join(',')}`) % candidates.length;
  const ordered = Array.from({ length: candidates.length }, (_, index) =>
    candidates[(offset + index) % candidates.length]);
  const chosen: Point[] = [];
  return slots.map((slot, index) => {
    const ideal = Math.floor(index * ordered.length / Math.max(1, slots.length));
    let point: Point | null = null;
    for (let delta = 0; delta < ordered.length; delta++) {
      const candidate = ordered[(ideal + delta) % ordered.length];
      if (!occupied.has(key(candidate))
        && !chosen.some((other) => key(other) === key(candidate))) {
        point = candidate;
        break;
      }
    }
    point ??= ordered.find((candidate) =>
      !chosen.some((other) => key(other) === key(candidate))) ?? null;
    if (point) {
      chosen.push(point);
      occupied.add(key(point));
    }
    void slot;
    return point;
  });
}

/** Project every group together so distinctness is phase-wide. Recursing one
 * phase lets inherited jobs retain the exact same slot positions before new
 * current-phase assignments claim the remaining ground. */
export function projectPhaseBots(
  catalog: TacticalSheetCatalog,
  document: Pick<TacticsDocument, 'mapId'>,
  tactic: Tactic,
  phaseIndex: number,
  cache = new Map<number, PhaseBotProjection>(),
): PhaseBotProjection {
  const cached = cache.get(phaseIndex);
  if (cached) return cached;
  const pathfinder = makePathfinder(catalog);
  const bots: BotProjection[] = [];
  const unresolved = new Set<string>();
  const occupied = new Set<string>();
  const previous = phaseIndex > 0
    ? projectPhaseBots(catalog, document, tactic, phaseIndex - 1, cache)
    : null;

  // Exact Posts always win reservations. A malformed overlap stays visible
  // and remains the validator's error rather than silently moving a post.
  for (const group of tactic.groups) {
    const resolution = effectiveJobResolution(tactic, phaseIndex, group.id);
    if (!resolution || resolution.job.kind !== 'posts') continue;
    const posts = resolution.job.posts ?? [];
    posts.forEach((post, index) => {
      const slot = post.slot ?? group.slots[index];
      if (slot == null || !group.slots.includes(slot)) return;
      bots.push({
        slot, groupId: group.id, phaseIndex,
        sourcePhaseIndex: resolution.sourcePhaseIndex,
        at: [...post.at], exact: true, carried: resolution.inherited,
        jobKind: 'posts',
      });
      occupied.add(key(post.at));
    });
  }

  // Preserve inherited non-Post projections by stable slot before placing
  // newly authored ground in this phase.
  for (const group of tactic.groups) {
    const resolution = effectiveJobResolution(tactic, phaseIndex, group.id);
    if (!resolution || !resolution.inherited || resolution.job.kind === 'posts') continue;
    const priorResolution = effectiveJobResolution(tactic, phaseIndex - 1, group.id);
    if (!priorResolution
      || groundFingerprint(priorResolution.job) !== groundFingerprint(resolution.job)) continue;
    for (const slot of group.slots) {
      const prior = previous?.bots.find((bot) => bot.groupId === group.id && bot.slot === slot);
      if (!prior || occupied.has(key(prior.at))) continue;
      bots.push({
        ...prior, phaseIndex, sourcePhaseIndex: resolution.sourcePhaseIndex,
        carried: true, exact: false,
      });
      occupied.add(key(prior.at));
    }
  }

  for (const group of tactic.groups) {
    const resolution = effectiveJobResolution(tactic, phaseIndex, group.id);
    if (!resolution || resolution.job.kind === 'posts') {
      if (!resolution) unresolved.add(group.id);
      continue;
    }
    const pending = group.slots.filter((slot) =>
      !bots.some((bot) => bot.groupId === group.id && bot.slot === slot));
    if (!pending.length) continue;
    const job = resolution.job;
    const seed = [document.mapId, tactic.id,
      tactic.phases[resolution.sourcePhaseIndex]?.id ?? resolution.sourcePhaseIndex,
      group.id, groundFingerprint(job)].join(':');
    let points: (Point | null)[] | null = null;
    if (job.kind === 'area' && job.rect) {
      const candidates: Point[] = [];
      for (let y = job.rect[1]; y <= job.rect[3]; y++) {
        for (let x = job.rect[0]; x <= job.rect[2]; x++) {
          const point: Point = [x, y];
          if (tilePlacementIssue(catalog, point, 'anchor') === null) candidates.push(point);
        }
      }
      points = chooseSpaced(candidates, pending, seed, occupied);
    } else if ((job.kind === 'converge' || job.kind === 'approach') && job.point) {
      points = around(catalog, job.point, occupied, pending, seed);
    } else if ((job.kind === 'patrol' || job.kind === 'advance' || job.kind === 'line')
      && job.path && job.path.length > 1) {
      const waypoints = job.kind === 'patrol' ? [...job.path, job.path[0]] : job.path;
      const route = pathfinder.connectTiles(waypoints);
      if (route) {
        const legal = route.filter((point) =>
          tilePlacementIssue(catalog, point, 'anchor') === null);
        points = alongRoute(legal, pending, seed, occupied);
      }
    } else if (job.kind === 'return') {
      // Home is the map's own west reactor, not a remembered coordinate.
      const reactor = catalog.map.regions
        .find((region) => region.id === 'reactor-west')?.tiles[0];
      points = around(
        catalog, reactor ? [reactor.x, reactor.y] : [3, 13],
        occupied, pending, seed);
    } else if (job.kind === 'train' && job.workshop) {
      const tile = workshopTrainingTile(catalog, job.workshop);
      if (tile) points = around(catalog, tile, occupied, pending, seed);
    }

    if (!points) {
      unresolved.add(group.id);
      continue;
    }
    pending.forEach((slot, index) => {
      const at = points![index];
      if (!at) return;
      bots.push({
        slot, groupId: group.id, phaseIndex,
        sourcePhaseIndex: resolution.sourcePhaseIndex,
        at, exact: false, carried: resolution.inherited,
        jobKind: job.kind,
      });
    });
    if (points.some((point) => point === null)) unresolved.add(group.id);
  }

  bots.sort((a, b) => a.slot - b.slot || a.groupId.localeCompare(b.groupId));
  const result = { bots, unresolvedGroupIds: [...unresolved] };
  cache.set(phaseIndex, result);
  return result;
}
