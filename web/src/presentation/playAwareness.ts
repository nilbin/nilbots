import type {
  ReplayActorLifeKey,
  ReplayCausalEvent,
  ReplayModel,
  ReplayStableUnitKey,
} from '../replayModel';
import { isDestructionEvent } from '../replayModel';

/**
 * Presentation-only reading of the bounded operation role tags emitted by a
 * MIND. The engine does not consume this vocabulary and this module never
 * feeds a decision or canonical replay field back into the match.
 *
 * Stock operations publish `g-<operation>-<phase>-<task>`. Two operation
 * codes were deliberately made semantic by the frozen interpreter. The older
 * generic `op` code is kept honest: it proves coordinated execution, but not
 * which authored operation it was, so the viewer says exactly that instead of
 * guessing from movement.
 */

export type PlayPhase = 'preparing' | 'committed' | 'recovery';

export interface ParsedPlayRole {
  operationCode: string;
  name: string;
  named: boolean;
  phase: PlayPhase;
  taskCode: string;
  task: string;
}

export interface PlayParticipantFrame {
  actorKey: ReplayActorLifeKey;
  unitKey: ReplayStableUnitKey;
  taskCode: string;
  task: string;
}

export interface PlayContact {
  tick: number;
  eventId: string;
  kind: 'combat' | 'core' | 'casualty';
  summary: string;
}

export interface PlayPhaseTransition {
  tick: number;
  phase: PlayPhase | 'released';
}

export interface PlayActivation {
  key: string;
  teamId: number;
  operationCode: string;
  name: string;
  named: boolean;
  ordinal: number;
  startedTick: number;
  committedTick: number | null;
  recoveryTick: number | null;
  releaseTick: number | null;
  contacts: readonly PlayContact[];
  transitions: readonly PlayPhaseTransition[];
  participantUnitKeys: readonly ReplayStableUnitKey[];
  participantActorKeys: readonly ReplayActorLifeKey[];
  taskLabels: readonly string[];
}

export interface ActivePlayFrame {
  activationKey: string;
  teamId: number;
  operationCode: string;
  name: string;
  named: boolean;
  ordinal: number;
  phase: PlayPhase;
  phaseStartedTick: number;
  participants: readonly PlayParticipantFrame[];
  contact: PlayContact | null;
  salience: number;
}

export interface PlayAwarenessTimeline {
  activations: readonly PlayActivation[];
  frames: readonly (readonly ActivePlayFrame[])[];
}

const OPERATION_NAMES: Readonly<Record<string, string>> = {
  rh: 'Rear Hook',
  ls: 'Lantern Sweep',
};

const PHASES: Readonly<Record<string, PlayPhase>> = {
  p: 'preparing',
  c: 'committed',
  r: 'recovery',
};

const TASK_NAMES: Readonly<Record<string, string>> = {
  car: 'carrier',
  lan: 'route probe',
  scr: 'screen',
  nh: 'north hook',
  sh: 'south hook',
  ext: 'extraction',
  task: 'assigned task',
};

const cache = new WeakMap<ReplayModel, PlayAwarenessTimeline>();

export function parsePlayRoleTag(tag: string | null): ParsedPlayRole | null {
  if (!tag) return null;
  const match = /^g-([a-z0-9]+)-([pcr])-([a-z0-9]+)$/.exec(tag);
  if (!match) return null;
  const operationCode = match[1]!;
  const phase = PHASES[match[2]!];
  if (!phase) return null;
  return {
    operationCode,
    name: OPERATION_NAMES[operationCode] ?? 'Unlabelled coordination',
    named: operationCode in OPERATION_NAMES,
    phase,
    taskCode: match[3]!,
    task: TASK_NAMES[match[3]!] ?? humanize(match[3]!),
  };
}

export function playRoleSummary(tag: string | null): string | null {
  const parsed = parsePlayRoleTag(tag);
  return parsed
    ? `${parsed.name} · ${parsed.task} · ${parsed.phase}`
    : null;
}

export function playAwarenessTimeline(
  replay: ReplayModel,
): PlayAwarenessTimeline {
  const prior = cache.get(replay);
  if (prior) return prior;
  const built = buildTimeline(replay);
  cache.set(replay, built);
  return built;
}

export function playsAt(
  replay: ReplayModel,
  tickIndex: number,
): readonly ActivePlayFrame[] {
  const timeline = playAwarenessTimeline(replay);
  if (timeline.frames.length === 0) return [];
  return timeline.frames[
    Math.max(0, Math.min(tickIndex, timeline.frames.length - 1))
  ] ?? [];
}

export function playForUnit(
  replay: ReplayModel,
  tickIndex: number,
  unitKey: ReplayStableUnitKey | null,
): ActivePlayFrame | null {
  if (unitKey === null) return null;
  return playsAt(replay, tickIndex).find((play) =>
    play.participants.some((participant) => participant.unitKey === unitKey),
  ) ?? null;
}

interface MutableActivation {
  key: string;
  teamId: number;
  operationCode: string;
  name: string;
  named: boolean;
  ordinal: number;
  startedTick: number;
  committedTick: number | null;
  recoveryTick: number | null;
  releaseTick: number | null;
  contacts: PlayContact[];
  transitions: PlayPhaseTransition[];
  participantUnitKeys: Set<ReplayStableUnitKey>;
  participantActorKeys: Set<ReplayActorLifeKey>;
  taskLabels: Set<string>;
  phase: PlayPhase;
  phaseStartedTick: number;
}

interface GroupAtTick {
  teamId: number;
  role: ParsedPlayRole;
  participants: PlayParticipantFrame[];
}

function buildTimeline(replay: ReplayModel): PlayAwarenessTimeline {
  const frames: ActivePlayFrame[][] = [];
  const complete: MutableActivation[] = [];
  const active = new Map<string, MutableActivation>();
  const ordinals = new Map<string, number>();

  for (const tick of replay.ticks) {
    const groups = groupsAtTick(tick.actorTurns);
    const present = new Set(groups.keys());
    for (const [groupKey, activation] of [...active]) {
      if (present.has(groupKey)) continue;
      activation.releaseTick = tick.tick;
      activation.transitions.push({ tick: tick.tick, phase: 'released' });
      complete.push(activation);
      active.delete(groupKey);
    }

    const frame: ActivePlayFrame[] = [];
    for (const [groupKey, group] of groups) {
      let activation = active.get(groupKey);
      if (!activation) {
        const ordinal = (ordinals.get(groupKey) ?? 0) + 1;
        ordinals.set(groupKey, ordinal);
        activation = {
          key: `${groupKey}:${ordinal}`,
          teamId: group.teamId,
          operationCode: group.role.operationCode,
          name: group.role.name,
          named: group.role.named,
          ordinal,
          startedTick: tick.tick,
          committedTick: group.role.phase === 'committed' ? tick.tick : null,
          recoveryTick: group.role.phase === 'recovery' ? tick.tick : null,
          releaseTick: null,
          contacts: [],
          transitions: [{ tick: tick.tick, phase: group.role.phase }],
          participantUnitKeys: new Set(),
          participantActorKeys: new Set(),
          taskLabels: new Set(),
          phase: group.role.phase,
          phaseStartedTick: tick.tick,
        };
        active.set(groupKey, activation);
      } else if (activation.phase !== group.role.phase) {
        activation.phase = group.role.phase;
        activation.phaseStartedTick = tick.tick;
        activation.transitions.push({ tick: tick.tick, phase: group.role.phase });
        if (group.role.phase === 'committed' && activation.committedTick === null)
          activation.committedTick = tick.tick;
        if (group.role.phase === 'recovery' && activation.recoveryTick === null)
          activation.recoveryTick = tick.tick;
      }

      for (const participant of group.participants) {
        activation.participantUnitKeys.add(participant.unitKey);
        activation.participantActorKeys.add(participant.actorKey);
        activation.taskLabels.add(participant.task);
      }
      const contacts = contactsAtTick(
        replay,
        tick.tick,
        new Set(group.participants.map((participant) => participant.actorKey)),
      );
      for (const contact of contacts) {
        if (!activation.contacts.some((candidate) => candidate.eventId === contact.eventId))
          activation.contacts.push(contact);
      }
      const contact = contacts[0] ?? null;
      frame.push({
        activationKey: activation.key,
        teamId: activation.teamId,
        operationCode: activation.operationCode,
        name: activation.name,
        named: activation.named,
        ordinal: activation.ordinal,
        phase: activation.phase,
        phaseStartedTick: activation.phaseStartedTick,
        participants: group.participants,
        contact,
        salience: (contact ? 100 : 0) + phaseSalience(activation.phase),
      });
    }
    frame.sort(
      (left, right) =>
        right.salience - left.salience ||
        left.teamId - right.teamId ||
        left.activationKey.localeCompare(right.activationKey),
    );
    frames.push(frame);
  }

  for (const activation of active.values()) {
    // The match ending is not a baseline release. Leave the tick absent so the
    // card cannot imply a command the replay never contained.
    activation.releaseTick = null;
    complete.push(activation);
  }
  complete.sort(
    (left, right) =>
      left.startedTick - right.startedTick ||
      left.teamId - right.teamId ||
      left.key.localeCompare(right.key),
  );

  return {
    frames,
    activations: complete.map((activation) => ({
      key: activation.key,
      teamId: activation.teamId,
      operationCode: activation.operationCode,
      name: activation.name,
      named: activation.named,
      ordinal: activation.ordinal,
      startedTick: activation.startedTick,
      committedTick: activation.committedTick,
      recoveryTick: activation.recoveryTick,
      releaseTick: activation.releaseTick,
      contacts: activation.contacts,
      transitions: activation.transitions,
      participantUnitKeys: [...activation.participantUnitKeys].sort(),
      participantActorKeys: [...activation.participantActorKeys].sort(),
      taskLabels: [...activation.taskLabels].sort(),
    })),
  };
}

function groupsAtTick(
  turns: ReplayModel['ticks'][number]['actorTurns'],
): Map<string, GroupAtTick> {
  const groups = new Map<string, GroupAtTick>();
  for (const turn of turns) {
    const role = parsePlayRoleTag(turn.observation.self?.roleTag ?? null);
    if (!role) continue;
    const key = `${turn.actor.teamId}:${role.operationCode}`;
    const existing = groups.get(key);
    const participant: PlayParticipantFrame = {
      actorKey: turn.actorKey,
      unitKey: turn.actor.unitKey,
      taskCode: role.taskCode,
      task: role.task,
    };
    if (existing) {
      existing.participants.push(participant);
      if (phaseSalience(role.phase) > phaseSalience(existing.role.phase))
        existing.role = role;
    } else {
      groups.set(key, {
        teamId: turn.actor.teamId,
        role,
        participants: [participant],
      });
    }
  }
  for (const group of groups.values())
    group.participants.sort((left, right) => left.unitKey.localeCompare(right.unitKey));
  return groups;
}

function contactsAtTick(
  replay: ReplayModel,
  tickIndex: number,
  actors: ReadonlySet<ReplayActorLifeKey>,
): PlayContact[] {
  const tick = replay.ticks[tickIndex];
  if (!tick) return [];
  const contacts: PlayContact[] = [];
  for (const event of tick.events) {
    const source = event.sourceActor?.actorKey;
    const target = event.targetActor?.actorKey;
    const involved = (source !== undefined && actors.has(source)) ||
      (target !== undefined && actors.has(target)) ||
      arcFactInvolves(event, actors);
    if (!involved) continue;
    if (isDestructionEvent(event.type)) {
      const participantLost = target !== undefined && actors.has(target);
      contacts.push({
        tick: tick.tick,
        eventId: event.eventId,
        kind: 'casualty',
        summary: participantLost
          ? 'claimed participant destroyed'
          : 'opponent destroyed at the play',
      });
    } else if (event.type === 'damage' || event.type === 'body-relocated') {
      contacts.push({
        tick: tick.tick,
        eventId: event.eventId,
        kind: 'combat',
        summary: event.type === 'damage' ? 'hostile damage at the play' : 'participant displaced',
      });
    } else if (event.arcRelayFact && coreFactIsContact(event)) {
      contacts.push({
        tick: tick.tick,
        eventId: event.eventId,
        kind: 'core',
        summary: coreContactSummary(event),
      });
    }
  }
  return contacts.sort((left, right) =>
    contactPriority(right.kind) - contactPriority(left.kind) ||
    left.eventId.localeCompare(right.eventId),
  );
}

function arcFactInvolves(
  event: ReplayCausalEvent,
  actors: ReadonlySet<ReplayActorLifeKey>,
): boolean {
  const fact = event.arcRelayFact;
  if (!fact) return false;
  const exact = 'carrierActor' in fact && fact.carrierActor
    ? fact.carrierActor.actorKey
    : 'sourceActor' in fact && fact.sourceActor
      ? fact.sourceActor.actorKey
      : null;
  if (exact && actors.has(exact)) return true;
  if ('targetActor' in fact && fact.targetActor && actors.has(fact.targetActor.actorKey))
    return true;
  return false;
}

function coreFactIsContact(event: ReplayCausalEvent): boolean {
  const kind = event.arcRelayFact?.kind;
  return kind === 'core-picked-up' || kind === 'core-dropped' ||
    kind === 'core-banked' || kind === 'core-handed-off';
}

function coreContactSummary(event: ReplayCausalEvent): string {
  switch (event.arcRelayFact?.kind) {
    case 'core-picked-up': return 'Core possession changed';
    case 'core-dropped': return 'Core forced loose';
    case 'core-banked': return 'Core banked';
    case 'core-handed-off': return 'Core handed off';
    default: return 'Core contested';
  }
}

function contactPriority(kind: PlayContact['kind']): number {
  return kind === 'casualty' ? 3 : kind === 'core' ? 2 : 1;
}

function phaseSalience(phase: PlayPhase): number {
  return phase === 'committed' ? 30 : phase === 'preparing' ? 20 : 10;
}

function humanize(value: string): string {
  return value.replace(/-/g, ' ');
}
