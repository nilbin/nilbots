import type {
  ReplayActorState,
  ReplayCausalEvent,
  ReplayModel,
  ReplayPosition,
} from '../../replayModel';
import { parseCommandReason } from '../../presentation/commandReason';
import {
  conditionExpressionNames,
  type OrderNode,
  type SheetDraft,
} from './sheetAuthoring';
import type {
  LinkedPlay,
  PlayOpportunityBinding,
  PlayProjection,
} from './workshopTypes';

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

export interface PlayFormationEvidence {
  ready: number;
  required: number;
  tick: number | null;
}

export interface SignatureOpportunityPracticeSummary {
  evaluations: number;
  acceptedUses: number;
  signatureEvents: number;
  firstActivationTick: number | null;
  lastActivationTick: number | null;
  declineCauses: Array<{ cause: string; count: number }>;
  lastResolution: string | null;
  continuation: string | null;
  routeContinuity: {
    before: ReplayPosition;
    after: ReplayPosition;
    tick: number;
  } | null;
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

/** Best authoritative occupancy of one role-qualified formation Condition. */
export function bestPlayFormationReadiness(
  draft: SheetDraft,
  conditionId: string,
  tracks: UnitTrack[],
): PlayFormationEvidence {
  const condition = draft.orders.conditions[conditionId];
  const rect = condition?.zone ? draft.mapPlan.zones[condition.zone]?.rect : undefined;
  const required = condition?.fact === 'role-in-zone-count'
    ? Math.max(0, condition.value) : 0;
  if (!condition?.role || !rect || required === 0)
    return { ready: 0, required, tick: null };
  let firstUnit = 0;
  const role = draft.orders.roles.find((entry) => {
    if (entry.id === condition.role) return true;
    firstUnit += entry.classes.length;
    return false;
  });
  if (!role) return { ready: 0, required, tick: null };
  const unitIds = new Set(role.classes.map((_, index) => firstUnit + index));
  const byTick = new Map<number, Map<number, ReplayPosition>>();
  for (const track of tracks) {
    if (track.teamId !== 0 || !unitIds.has(track.unitId)) continue;
    for (const sample of track.samples) {
      const positions = byTick.get(sample.tick) ?? new Map<number, ReplayPosition>();
      positions.set(track.unitId, sample.position);
      byTick.set(sample.tick, positions);
    }
  }
  let best: PlayFormationEvidence = { ready: 0, required, tick: null };
  for (const [tick, positions] of [...byTick].sort(([a], [b]) => a - b)) {
    const ready = [...positions.values()].filter((point) =>
      point.x >= rect[0] && point.x <= rect[2]
      && point.y >= rect[1] && point.y <= rect[3]).length;
    if (ready > best.ready) best = { ready, required, tick };
  }
  return best;
}

/**
 * Correlate one linked opportunity back to canonical Practice evidence. The
 * replay still knows only ordinary role/Order paths and signature events; the
 * binding is applied here, after the match, and never becomes execution data.
 */
export function signatureOpportunityPracticeSummary(
  replay: ReplayModel | undefined,
  draft: SheetDraft,
  projection: PlayProjection,
  binding: PlayOpportunityBinding,
): SignatureOpportunityPracticeSummary | null {
  if (!replay?.initialWorld) return null;
  const use = findOrderById(projection.orders, binding.useOrderId);
  if (!use?.use) return null;
  const unitIds = unitIdsForRole(draft, projection.roleId);
  if (unitIds.size === 0) return null;
  const acceptedPath = `> ${binding.useOrderId}:`;
  const acceptedUse = `:use:${use.use.skill}:`;
  const declineMarker = `after-${binding.useOrderId}-`;
  const declines = new Map<string, number>();
  const acceptedTicks: Array<{ tick: number; unitId: number; reason: string }> = [];
  let evaluations = 0;
  let continuation: string | null = null;
  let lastResolution: string | null = null;

  for (const tick of replay.ticks) {
    for (const turn of tick.actorTurns) {
      if (turn.actor.teamId !== 0 || !unitIds.has(turn.actor.unitId)) continue;
      const reason = turn.runtimeReply.debugMessage
        ?? turn.acceptedDecision.debugMessage ?? '';
      if (reason.includes('orders:')
        && reason.includes(acceptedPath)
        && reason.includes(acceptedUse)) {
        evaluations++;
        acceptedTicks.push({ tick: tick.tick, unitId: turn.actor.unitId, reason });
        lastResolution = reasonAfter(reason, `:use:${use.use.skill}:`)
          ?? `accepted ${use.use.skill}`;
        continue;
      }
      const declinedAt = reason.indexOf(declineMarker);
      if (declinedAt < 0) continue;
      evaluations++;
      const cause = reason.slice(declinedAt + declineMarker.length)
        .split(':')[0] || 'declined';
      declines.set(cause, (declines.get(cause) ?? 0) + 1);
      const parsed = parseCommandReason(reason);
      if (parsed?.orderId) continuation = parsed.orderId;
      lastResolution = cause;
    }
  }

  const relevantEvents = replay.ticks.flatMap((tick) => tick.events)
    .filter((event) => signatureEventMatches(event, use.use!.skill, unitIds));
  const activation = opportunityActivationTicks(replay, draft, use, unitIds);
  const routeContinuity = acceptedTicks.length > 0
    ? continuityAround(replay, acceptedTicks.at(-1)!) : null;
  if (acceptedTicks.length > 0) {
    const last = acceptedTicks.at(-1)!;
    const next = nextBodyReason(replay, last.tick, last.unitId);
    if (next) continuation = parseCommandReason(next)?.orderId ?? continuation;
  }

  return {
    evaluations,
    acceptedUses: acceptedTicks.length,
    signatureEvents: relevantEvents.length,
    firstActivationTick: activation[0] ?? null,
    lastActivationTick: activation.at(-1) ?? null,
    declineCauses: [...declines]
      .map(([cause, count]) => ({ cause, count }))
      .sort((left, right) => right.count - left.count
        || left.cause.localeCompare(right.cause)),
    lastResolution,
    continuation,
    routeContinuity,
  };
}

function opportunityActivationTicks(
  replay: ReplayModel,
  draft: SheetDraft,
  use: OrderNode,
  unitIds: ReadonlySet<number>,
): number[] {
  const zone = conditionExpressionNames(use.when)
    .map((id) => draft.orders.conditions[id])
    .find((condition) => condition?.fact === 'self-in-zone')?.zone;
  const rect = zone ? draft.mapPlan.zones[zone]?.rect : undefined;
  if (!rect) return [];
  const result: number[] = [];
  const inspect = (tick: number, actors: ReplayActorState[]) => {
    if (actors.some((actor) => actor.identity.teamId === 0
      && unitIds.has(actor.identity.unitId)
      && actor.position.x >= rect[0] && actor.position.x <= rect[2]
      && actor.position.y >= rect[1] && actor.position.y <= rect[3])) {
      result.push(tick);
    }
  };
  inspect(0, replay.initialWorld?.actors ?? []);
  replay.ticks.forEach((tick) => inspect(tick.tick, tick.after.actors));
  return result;
}

function signatureEventMatches(
  event: ReplayCausalEvent,
  skill: string,
  unitIds: ReadonlySet<number>,
): boolean {
  const fact = event.arcRelayFact;
  return fact?.kind === 'signature-changed'
    && fact.signatureId === skill
    && fact.ownerActor.teamId === 0
    && unitIds.has(fact.ownerActor.unitId);
}

function continuityAround(
  replay: ReplayModel,
  accepted: { tick: number; unitId: number },
): SignatureOpportunityPracticeSummary['routeContinuity'] {
  const before = replay.ticks.find((tick) => tick.tick === accepted.tick)?.before
    .actors.find((actor) => actor.identity.teamId === 0
      && actor.identity.unitId === accepted.unitId)?.position;
  const after = replay.ticks.find((tick) => tick.tick > accepted.tick)?.after
    .actors.find((actor) => actor.identity.teamId === 0
      && actor.identity.unitId === accepted.unitId)?.position;
  return before && after
    ? { before: { ...before }, after: { ...after }, tick: accepted.tick }
    : null;
}

function nextBodyReason(
  replay: ReplayModel,
  tick: number,
  unitId: number,
): string | null {
  for (const frame of replay.ticks) {
    if (frame.tick <= tick) continue;
    const turn = frame.actorTurns.find((candidate) =>
      candidate.actor.teamId === 0 && candidate.actor.unitId === unitId);
    const reason = turn?.runtimeReply.debugMessage
      ?? turn?.acceptedDecision.debugMessage;
    if (reason) return reason;
  }
  return null;
}

function unitIdsForRole(draft: SheetDraft, roleId: string): Set<number> {
  const result = new Set<number>();
  let unitId = 0;
  for (const role of draft.orders.roles) {
    for (let offset = 0; offset < role.classes.length; offset++) {
      if (role.id === roleId) result.add(unitId + offset);
    }
    unitId += role.classes.length;
  }
  return result;
}

function findOrderById(orders: OrderNode[], id: string): OrderNode | undefined {
  for (const node of orders) {
    if (node.id === id) return node;
    const nested = node.orders && findOrderById(node.orders, id);
    if (nested) return nested;
  }
  return undefined;
}

function reasonAfter(reason: string, marker: string): string | null {
  const index = reason.indexOf(marker);
  if (index < 0) return null;
  return reason.slice(index + marker.length).split(' via ')[0]?.trim() || null;
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
