import type {
  ReplayActorState,
  ReplayCausalEvent,
  ReplayModel,
  ReplayPosition,
  ReplayTick,
} from '../../replayModel';
import { parseCommandReason } from '../../presentation/commandReason';
import {
  conditionExpressionNames,
  type OrderNode,
  type SheetDraft,
} from './sheetAuthoring';
import { resolveProjectionOrders } from './linkedPlays';
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

export interface PracticeProgramBody {
  unitId: number;
  roleId: string;
  classId: string;
}

export interface PracticeProgramGate {
  programId: string;
  programName: string;
  status: 'accepted' | 'ineligible' | 'yielded' | 'not-participating';
  reason: string;
}

export interface PracticeProgramAttribution {
  tick: number;
  body: PracticeProgramBody;
  programId: string | null;
  programName: string | null;
  orderId: string | null;
  action: string;
  reason: string | null;
  higherPrograms: PracticeProgramGate[];
}

export interface PracticeProgramTimelineSegment {
  fromTick: number;
  toTick: number;
  programId: string | null;
  programName: string | null;
  orderId: string | null;
  action: string;
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

/** The exact deterministic role-to-body assignment used by the compiler. */
export function practiceProgramBodies(draft: SheetDraft): PracticeProgramBody[] {
  const result: PracticeProgramBody[] = [];
  let unitId = 0;
  for (const role of draft.orders.roles) {
    for (const classId of role.classes)
      result.push({ unitId: unitId++, roleId: role.id, classId });
  }
  return result;
}

/**
 * Explain one authored body's accepted Instruction through the immutable
 * Program sidecar captured with the Practice run. No Program state is inferred:
 * the accepted order path and any rejection clauses come from the mind's own
 * authoritative reason string, then map back to generated source ids.
 */
export function practiceProgramAttributionAt(
  replay: ReplayModel | undefined,
  draft: SheetDraft,
  unitId: number,
  requestedTick: number,
): PracticeProgramAttribution | null {
  const body = practiceProgramBodies(draft).find((entry) => entry.unitId === unitId);
  if (!body || !replay) return null;
  const frame = closestReplayFrame(replay, requestedTick);
  if (!frame) return null;
  return practiceProgramAttributionForFrame(draft, body, frame);
}

function practiceProgramAttributionForFrame(
  draft: SheetDraft,
  body: PracticeProgramBody,
  frame: ReplayTick,
): PracticeProgramAttribution {
  const turn = frame.actorTurns.find((candidate) =>
    candidate.actor.teamId === 0 && candidate.actor.unitId === body.unitId);
  const published = frame.publishedUnitOrders?.find((candidate) =>
    candidate.teamId === 0 && candidate.unitId === body.unitId);
  const reason = turn?.runtimeReply.debugMessage
    ?? turn?.acceptedDecision.debugMessage
    ?? null;
  const parsed = reason ? parseCommandReason(reason) : null;
  const orderId = parsed?.orderId ?? published?.orderId ?? null;
  const action = parsed?.action ?? published?.action ?? 'No body action recorded';
  const matched = findProgramForOrder(draft, body.roleId, orderId, reason);
  const acceptedIndex = matched
    ? draft.workshop.plays.findIndex((entry) => entry.id === matched.id)
    : draft.workshop.plays.length;
  const rejections = parseEligibilityReasons(reason);

  const higherPrograms = draft.workshop.plays
    .slice(0, Math.max(0, acceptedIndex))
    .filter((program) => program.projections.some((projection) =>
      projection.roleId === body.roleId))
    .map((program): PracticeProgramGate => {
      const recorded = rejectionForProgram(draft, program, body.roleId, reason, rejections);
      return {
        programId: program.id,
        programName: program.name,
        status: recorded ? 'ineligible' : 'yielded',
        reason: recorded ?? 'No accepted Instruction from this Program at this tick; the replay records no more specific gate result.',
      };
    });

  return {
    tick: frame.tick,
    body,
    programId: matched?.id ?? null,
    programName: matched?.name ?? null,
    orderId,
    action,
    reason,
    higherPrograms,
  };
}

/** Group consecutive ticks so a player can scan Program handoffs, not 600 rows. */
export function practiceProgramTimeline(
  replay: ReplayModel | undefined,
  draft: SheetDraft,
  unitId: number,
): PracticeProgramTimelineSegment[] {
  if (!replay) return [];
  const body = practiceProgramBodies(draft).find((entry) => entry.unitId === unitId);
  if (!body) return [];
  const result: PracticeProgramTimelineSegment[] = [];
  for (const frame of replay.ticks) {
    const point = practiceProgramAttributionForFrame(draft, body, frame);
    const action = practiceActionLabel(point.action);
    const previous = result.at(-1);
    if (previous
      && previous.programId === point.programId
      && previous.orderId === point.orderId
      && previous.action === action
      && previous.toTick + 1 === point.tick) {
      previous.toTick = point.tick;
      continue;
    }
    result.push({
      fromTick: point.tick,
      toTick: point.tick,
      programId: point.programId,
      programName: point.programName,
      orderId: point.orderId,
      action,
    });
  }
  return result;
}

/** Reduce executor diagnostics to the player-facing action clause. */
export function practiceActionLabel(action: string): string {
  const withoutPlanner = action.split(':urgency-')[0] ?? action;
  const semantic = /(?:^|:)(march|use|handoff|drop|repair|focus|prepare focus|duel-stand|close-on-focus|flank-approach|hold|wait|no-eligible-order)(?::|$)/
    .exec(withoutPlanner);
  if (!semantic) return humanizeReason(withoutPlanner.split(':')[0] ?? withoutPlanner);
  const verb = semantic[1]!;
  if (verb === 'march') return 'Move';
  if (verb === 'use') {
    const skill = withoutPlanner.slice(semantic.index + semantic[0].length)
      .split(':')[0];
    return skill ? `Use ${humanizeReason(skill)}` : 'Use signature';
  }
  if (verb === 'focus' || verb === 'prepare focus'
    || verb === 'duel-stand' || verb === 'close-on-focus'
    || verb === 'flank-approach') return 'Fight';
  if (verb === 'handoff') return 'Handoff Core';
  if (verb === 'drop') return 'Drop Core';
  if (verb === 'repair') return 'Repair';
  if (verb === 'hold' || verb === 'wait') return 'Hold';
  return humanizeReason(verb);
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

/** Required Post occupancy at one authoritative Practice frame. */
export function playReadinessAtTick(
  replay: ReplayModel | undefined,
  draft: SheetDraft,
  play: LinkedPlay,
  requestedTick: number,
): PlayReadinessEvidence {
  const required = play.slots.filter((slot) => slot.required);
  const frame = replay ? closestReplayFrame(replay, requestedTick) : undefined;
  if (!frame || required.length === 0)
    return { ready: 0, total: required.length, tick: frame?.tick ?? null, readySlotIds: [] };
  const positions = new Map(frame.after.actors
    .filter((actor) => actor.identity.teamId === 0)
    .map((actor) => [actor.identity.unitId, actor.position] as const));
  const readySlotIds = required.filter((slot) => {
    const rect = draft.mapPlan.zones[slot.readinessZoneId]?.rect;
    if (!rect) return false;
    return [...unitIdsForRole(draft, slot.roleId)].some((unitId) => {
      const point = positions.get(unitId);
      return point && point.x >= rect[0] && point.x <= rect[2]
        && point.y >= rect[1] && point.y <= rect[3];
    });
  }).map((slot) => slot.id);
  return {
    ready: readySlotIds.length,
    total: required.length,
    tick: frame.tick,
    readySlotIds,
  };
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

function closestReplayFrame(replay: ReplayModel, requestedTick: number) {
  let result = replay.ticks[0];
  for (const frame of replay.ticks) {
    if (frame.tick > requestedTick) break;
    result = frame;
  }
  return result;
}

function findProgramForOrder(
  draft: SheetDraft,
  roleId: string,
  orderId: string | null,
  reason: string | null,
): LinkedPlay | null {
  if (!orderId && !reason) return null;
  for (const program of draft.workshop.plays) {
    for (const projection of program.projections) {
      if (projection.roleId !== roleId) continue;
      const orders = resolveProjectionOrders(program, projection);
      if (orderId && findOrderById(orders, orderId)) return program;
      if (reason && projection.lastGenerated.some((root) =>
        reason.includes(`> ${root.orderId}`))) return program;
    }
  }
  return null;
}

function parseEligibilityReasons(reason: string | null): Map<string, string> {
  const result = new Map<string, string>();
  if (!reason) return result;
  const match = /(?:^|:)eligibility\[([^\]]+)\]/.exec(reason);
  if (!match?.[1]) return result;
  for (const clause of match[1].split('|')) {
    const separator = clause.indexOf('~');
    if (separator <= 0) continue;
    result.set(clause.slice(0, separator), clause.slice(separator + 1));
  }
  return result;
}

function rejectionForProgram(
  draft: SheetDraft,
  program: LinkedPlay,
  roleId: string,
  reason: string | null,
  eligibility: ReadonlyMap<string, string>,
): string | null {
  const projection = program.projections.find((entry) => entry.roleId === roleId);
  if (!projection) return null;
  for (const root of projection.lastGenerated) {
    const direct = eligibility.get(root.orderId);
    if (direct) return describeEligibilityReason(draft, direct);
  }
  if (!reason) return null;
  for (const order of flattenOrders(resolveProjectionOrders(program, projection))) {
    const marker = `after-${order.id}-`;
    const index = reason.indexOf(marker);
    if (index < 0) continue;
    const cause = reason.slice(index + marker.length).split(':')[0] ?? 'yielded';
    return humanizeReason(cause);
  }
  return null;
}

function describeEligibilityReason(draft: SheetDraft, reason: string): string {
  if (reason.startsWith('cooldown-until-'))
    return `Cooling down until tick ${reason.slice('cooldown-until-'.length)}`;
  if (reason === 'timeout') return 'Its authored timeout ended this response.';
  if (reason === 'no-child-eligible') return 'No child Instruction was eligible.';
  const prefix = ['when-ended-', 'condition-', 'when-', 'until-']
    .find((candidate) => reason.startsWith(candidate));
  const expression = prefix ? reason.slice(prefix.length) : reason;
  const clauses = expression.split('+').map((clause) => {
    const conditionId = Object.keys(draft.orders.conditions)
      .filter((id) => clause.startsWith(`${id}-`))
      .sort((left, right) => right.length - left.length)[0];
    if (!conditionId) return humanizeReason(clause);
    const condition = draft.orders.conditions[conditionId];
    const suffix = `-${condition.operator}-${condition.value}`;
    const actualPart = clause.slice(conditionId.length + 1);
    const actual = actualPart.endsWith(suffix)
      ? actualPart.slice(0, -suffix.length) : '?';
    return `${humanizeReason(conditionId)} was ${actual}; needs ${humanizeReason(condition.operator)} ${condition.value}`;
  });
  const lead = prefix?.startsWith('until') ? 'Exit condition met' : 'Gate not met';
  return `${lead}: ${clauses.join('; ')}`;
}

function flattenOrders(orders: OrderNode[]): OrderNode[] {
  const result: OrderNode[] = [];
  for (const order of orders) {
    result.push(order);
    if (order.orders) result.push(...flattenOrders(order.orders));
  }
  return result;
}

function humanizeReason(value: string): string {
  return value.replace(/[-_]+/g, ' ').replace(/\b\w/g, (letter) => letter.toUpperCase());
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
