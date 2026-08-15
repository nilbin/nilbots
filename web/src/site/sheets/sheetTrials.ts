import type {
  ReplayActorState,
  ReplayModel,
  ReplayPosition,
} from '../../replayModel';
import type { SheetDraft } from './sheetAuthoring';
import type { LinkedPlay } from './workshopTypes';

export const TRIAL_HISTORY_KEY = 'nilbots-sheet-trial-history-v1';

export interface TrialSnapshot {
  bodies: string;
  orders: string;
  sharedOrders: string;
  conditions: string;
  capacities: string;
  geography: string;
}

export interface SheetTrialRun {
  matchId: string;
  sheetId: string;
  sheetName: string;
  revision: number;
  contentHash: string;
  stockSheetId: string;
  stockName: string;
  seed: number | null;
  createdAt: string;
  changes: string[];
  snapshot: TrialSnapshot;
}

export interface UnitTrack {
  key: string;
  teamId: number;
  unitId: number;
  points: ReplayPosition[];
  samples: Array<{ tick: number; position: ReplayPosition }>;
}

export interface PlayReadinessEvidence {
  ready: number;
  total: number;
  tick: number | null;
  readySlotIds: string[];
}

export function snapshotForTrial(draft: SheetDraft): TrialSnapshot {
  return {
    bodies: JSON.stringify(draft.orders.roles.map(({ id, classes, upgradePath }) => ({
      id, classes, upgradePath,
    }))),
    orders: JSON.stringify(draft.orders.roles.map(({ id, orders }) => ({ id, orders }))),
    sharedOrders: JSON.stringify(draft.orders.sharedOrders),
    conditions: JSON.stringify(draft.orders.conditions),
    capacities: JSON.stringify(draft.orders.capacities),
    geography: JSON.stringify({
      zones: draft.mapPlan.zones,
      routes: draft.mapPlan.routes,
      anchors: draft.mapPlan.anchors,
    }),
  };
}

export function describeTrialChanges(
  previous: Partial<TrialSnapshot> | undefined,
  current: TrialSnapshot,
): string[] {
  if (!previous) return ['First recorded trial'];
  const labels: Array<[keyof TrialSnapshot, string]> = [
    ['bodies', 'Bodies'],
    ['orders', 'Orders'],
    ['sharedOrders', 'Shared orders'],
    ['conditions', 'Conditions'],
    ['capacities', 'Capacity rules'],
    ['geography', 'Map geometry'],
  ];
  const changed = labels
    // Older device histories predate some snapshot fields. Absence is unknown,
    // not evidence that the player changed that authoring plane.
    .filter(([key]) => previous[key] !== undefined && previous[key] !== current[key])
    .map(([, label]) => label);
  return changed.length > 0 ? changed : ['No tactical changes'];
}

export function loadTrialHistory(storage: Storage | undefined = browserStorage()): SheetTrialRun[] {
  if (!storage) return [];
  try {
    const value = JSON.parse(storage.getItem(TRIAL_HISTORY_KEY) ?? '[]') as unknown;
    if (!Array.isArray(value)) return [];
    return value.filter(isTrialRun).slice(0, 20);
  } catch {
    return [];
  }
}

export function saveTrialHistory(
  runs: SheetTrialRun[],
  storage: Storage | undefined = browserStorage(),
): SheetTrialRun[] {
  const retained = runs.slice(0, 20);
  storage?.setItem(TRIAL_HISTORY_KEY, JSON.stringify(retained));
  return retained;
}

/**
 * Reduce an authoritative replay to the tiles each stable body actually occupied.
 * Consecutive duplicates are removed, but no inferred intermediate tiles are added.
 */
export function replayUnitTracks(replay: ReplayModel | undefined): UnitTrack[] {
  if (!replay?.initialWorld) return [];
  const tracks = new Map<string, UnitTrack>();
  const collect = (tick: number, actors: ReplayActorState[]) => {
    for (const actor of actors) {
      const key = actor.unitKey;
      const track = tracks.get(key) ?? {
        key,
        teamId: actor.identity.teamId,
        unitId: actor.identity.unitId,
        points: [],
        samples: [],
      };
      const last = track.points.at(-1);
      if (!last || last.x !== actor.position.x || last.y !== actor.position.y)
        track.points.push({ ...actor.position });
      track.samples.push({ tick, position: { ...actor.position } });
      tracks.set(key, track);
    }
  };
  collect(0, replay.initialWorld.actors);
  replay.ticks.forEach((tick) => collect(tick.tick, tick.after.actors));
  return [...tracks.values()]
    .filter((track) => track.points.length > 0)
    .sort((a, b) => a.teamId - b.teamId || a.unitId - b.unitId || a.key.localeCompare(b.key));
}

/**
 * Find the authoritative replay frame that filled the most required slots.
 * Roles are resolved to unit ids with the same ordered composition mapping as
 * the compiler. A body counts only at a position the replay actually reports.
 */
export function bestPlayReadiness(
  draft: SheetDraft,
  play: LinkedPlay,
  tracks: UnitTrack[],
): PlayReadinessEvidence {
  const required = play.slots.filter((slot) => slot.required);
  if (required.length === 0)
    return { ready: 0, total: 0, tick: null, readySlotIds: [] };

  const roleUnits = new Map<string, Set<number>>();
  let unitId = 0;
  for (const role of draft.orders.roles) {
    roleUnits.set(role.id, new Set(role.classes.map((_, offset) => unitId + offset)));
    unitId += role.classes.length;
  }
  const samplesByTick = new Map<number, Map<number, ReplayPosition>>();
  for (const track of tracks) {
    if (track.teamId !== 0) continue;
    for (const sample of track.samples) {
      const positions = samplesByTick.get(sample.tick) ?? new Map<number, ReplayPosition>();
      positions.set(track.unitId, sample.position);
      samplesByTick.set(sample.tick, positions);
    }
  }

  let best: PlayReadinessEvidence = { ready: 0, total: required.length, tick: null, readySlotIds: [] };
  for (const [tick, positions] of [...samplesByTick].sort(([left], [right]) => left - right)) {
    const readySlotIds = required.filter((slot) => {
      const rect = draft.mapPlan.zones[slot.readinessZoneId]?.rect;
      if (!rect) return false;
      const units = roleUnits.get(slot.roleId) ?? new Set<number>();
      return [...units].some((candidate) => {
        const point = positions.get(candidate);
        return point && point.x >= rect[0] && point.x <= rect[2]
          && point.y >= rect[1] && point.y <= rect[3];
      });
    }).map((slot) => slot.id);
    if (readySlotIds.length > best.ready)
      best = { ready: readySlotIds.length, total: required.length, tick, readySlotIds };
  }
  return best;
}

function browserStorage(): Storage | undefined {
  return typeof localStorage === 'undefined' ? undefined : localStorage;
}

function isTrialRun(value: unknown): value is SheetTrialRun {
  if (!value || typeof value !== 'object') return false;
  const run = value as Partial<SheetTrialRun>;
  return typeof run.matchId === 'string'
    && typeof run.sheetId === 'string'
    && typeof run.sheetName === 'string'
    && typeof run.revision === 'number'
    && typeof run.contentHash === 'string'
    && typeof run.stockSheetId === 'string'
    && typeof run.stockName === 'string'
    && (run.seed === null || typeof run.seed === 'number')
    && typeof run.createdAt === 'string'
    && Array.isArray(run.changes)
    && Boolean(run.snapshot);
}
