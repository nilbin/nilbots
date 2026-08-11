import type {
  ReplayActorState,
  ReplayModel,
  ReplayPosition,
} from '../../replayModel';
import type { SheetDraft } from './sheetAuthoring';

export const TRIAL_HISTORY_KEY = 'nilbots-sheet-trial-history-v1';

export interface TrialSnapshot {
  composition: string;
  doctrines: string;
  conditions: string;
  custody: string;
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
}

export function snapshotForTrial(draft: SheetDraft): TrialSnapshot {
  return {
    composition: JSON.stringify(draft.playbook.composition),
    doctrines: JSON.stringify(draft.playbook.doctrines),
    conditions: JSON.stringify(draft.playbook.conditions ?? {}),
    custody: JSON.stringify(draft.playbook.custodyPolicies),
    geography: JSON.stringify({
      zones: draft.layout.zones,
      routes: draft.layout.routes,
      anchors: draft.layout.anchors,
      bindings: draft.layout.bindings,
    }),
  };
}

export function describeTrialChanges(
  previous: TrialSnapshot | undefined,
  current: TrialSnapshot,
): string[] {
  if (!previous) return ['First recorded trial'];
  const labels: Array<[keyof TrialSnapshot, string]> = [
    ['composition', 'Composition'],
    ['doctrines', 'Doctrines'],
    ['conditions', 'Conditions'],
    ['custody', 'Core custody'],
    ['geography', 'Map geometry'],
  ];
  const changed = labels
    .filter(([key]) => previous[key] !== current[key])
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
  const collect = (actors: ReplayActorState[]) => {
    for (const actor of actors) {
      const key = actor.unitKey;
      const track = tracks.get(key) ?? {
        key,
        teamId: actor.identity.teamId,
        unitId: actor.identity.unitId,
        points: [],
      };
      const last = track.points.at(-1);
      if (!last || last.x !== actor.position.x || last.y !== actor.position.y)
        track.points.push({ ...actor.position });
      tracks.set(key, track);
    }
  };
  collect(replay.initialWorld.actors);
  replay.ticks.forEach((tick) => collect(tick.after.actors));
  return [...tracks.values()]
    .filter((track) => track.points.length > 0)
    .sort((a, b) => a.teamId - b.teamId || a.unitId - b.unitId || a.key.localeCompare(b.key));
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
