import type {
  ReplayActorLifeKey,
  ReplayActorState,
  ReplayFormTransition,
  ReplayModel,
  ReplayProjectileHeading,
  ReplayStableUnitKey,
  ReplayUnitLifecycleStatus,
  ReplayWorldSnapshot,
} from './replayModel';
import { playerAccent } from './presentation/playerAccent';
import { unitAccent, unitLook } from './render/unitPresentation';
import { replayMaxHealth } from './replayMetadata';
import {
  legacySlotForUnit,
  participantForUnit,
  teamName,
  unitName,
} from './replayParticipants';

/**
 * Everything a panel or bridge needs to describe one normalized replay tick.
 * Rules-derived values live here so the canvas, web cards, and hosted bridge
 * cannot drift into three interpretations of the same match.
 */

export interface LegacyControlPresentation {
  kind: 'legacy-control';
  /** Signed toward the first team, matching replay-v1's historical meter. */
  pressure: number;
  limit: number;
  overtime: boolean;
  phase: string | null;
  names: [string, string];
}

export interface FrontlineControlPresentation {
  kind: 'frontline';
  activePositionIndex: number;
  positionCount: number;
  claimingTeamId: number | null;
  captureProgress: number;
  captureThreshold: number;
  controlResumesAtTick: number;
  winnerTeamId: number | null;
  phase: string;
}

export type ObjectivePresentation =
  | LegacyControlPresentation
  | FrontlineControlPresentation;

export interface UnitPresentation {
  unitKey: ReplayStableUnitKey;
  actorKey: ReplayActorLifeKey | null;
  teamId: number;
  unitId: number;
  lifeId: number | null;
  participantId: number;
  /** Replay-v1 compatibility identity; never populated for replay-v2. */
  legacySlot: number | null;
  name: string;
  accent: string;
  lookLabel: string;
  runtimeKind: string;
  formId: string;
  canMove: boolean;
  omnidirectionalVision: boolean;
  omnidirectionalShooting: boolean;
  status: ReplayUnitLifecycleStatus;
  respawnAtTick: number | null;
  unlockAtTick: number | null;
  rebuildReadyAtTick: number | null;
  fabricationAtTick: number | null;
  reservedSpawn: { x: number; y: number } | null;
  pendingSpawnReason: string | null;
  pendingFormTransition: ReplayFormTransition | null;
  health: number;
  maxHealth: number;
  cooldown: number;
  energy: number | null;
  zoneTicks: number | null;
  holdingObjective: boolean;
  actionId: string | null;
  actionLaunchHeading: ReplayProjectileHeading | null;
  actionResult: string | null;
  debug: string | null;
  visibleTiles: number;
  visibleEnemies: { x: number; y: number }[];
}

export interface TickPresentation {
  tick: number;
  objective: ObjectivePresentation | null;
  units: UnitPresentation[];
}

export interface ReplayPresenter {
  tickCount: number;
  maxHealth: number;
  at: (tick: number) => TickPresentation;
}

export function createPresenter(replay: ReplayModel): ReplayPresenter {
  const maxHealth = replayMaxHealth(replay);
  const tickCount = replay.ticks.length;
  const legacyZone = deriveLegacyZone(replay);

  const at = (rawTick: number): TickPresentation => {
    if (tickCount === 0) {
      return {
        tick: 0,
        objective: null,
        units: replay.units.map((unit) =>
          presentUnit(
            replay,
            unit.unitKey,
            replay.initialWorld,
            replay.initialWorld,
            null,
            legacyZone,
            0,
          ),
        ),
      };
    }

    const tickIndex = Math.max(0, Math.min(rawTick, tickCount - 1));
    const tick = replay.ticks[tickIndex];
    return {
      tick: tick.tick,
      objective: objectiveAt(replay, tickIndex, legacyZone),
      units: replay.units.map((unit) =>
        presentUnit(
          replay,
          unit.unitKey,
          tick.before,
          tick.after,
          tick.actorTurns.find(
            (turn) => turn.actor.unitKey === unit.unitKey,
          ) ?? null,
          legacyZone,
          tickIndex,
        ),
      ),
    };
  };

  return { tickCount, maxHealth, at };
}

function presentUnit(
  replay: ReplayModel,
  unitKey: ReplayStableUnitKey,
  before: ReplayWorldSnapshot | null,
  after: ReplayWorldSnapshot | null,
  turn: ReplayModel['ticks'][number]['actorTurns'][number] | null,
  legacyZone: LegacyZone | null,
  tickIndex: number,
): UnitPresentation {
  const unit = replay.units.find(
    (candidate) => candidate.unitKey === unitKey,
  );
  if (!unit) throw new Error(`Replay unit ${unitKey} is missing.`);
  const afterUnit = after?.units.find(
    (candidate) => candidate.unitKey === unitKey,
  );
  const actor =
    after?.actors.find((candidate) => candidate.unitKey === unitKey) ??
    before?.actors.find((candidate) => candidate.unitKey === unitKey) ??
    null;
  const activeActor =
    after?.actors.find(
      (candidate) =>
        candidate.unitKey === unitKey && candidate.status === 'active',
    ) ??
    null;
  const formId =
    activeActor?.formId ??
    actor?.formId ??
    afterUnit?.formId ??
    unit.initialFormId ??
    '';
  const form = replay.forms.find(
    (candidate) => candidate.formId === formId,
  );
  const participant = participantForUnit(replay, unitKey);
  // Resolved through the same place the arena resolves it, for the effective form: a card
  // showing a different chassis or a different colour from the bot it names is worse than
  // no card.
  const look = unitLook(replay, unitKey, formId);
  const status =
    afterUnit?.lifecycleStatus ?? actor?.status ?? 'respawning';
  const position = activeActor?.position ?? actor?.position ?? null;
  const frontlineObjective =
    after?.objective.kind === 'frontline' ? after.objective : null;
  const objectiveTiles =
    replay.map.frontline && frontlineObjective
      ? replay.map.frontline.positions.find(
          (candidate) =>
            candidate.positionIndex ===
            frontlineObjective.activePositionIndex,
        )?.tiles ?? []
      : replay.map.objectiveTiles;
  const onObjective =
    position !== null &&
    objectiveTiles.some(
      (point) => point.x === position.x && point.y === position.y,
    );
  const zoneTicks =
    legacyZone?.cumulative?.[tickIndex]?.get(unitKey) ??
    (after?.objective.kind === 'legacy'
      ? after.objective.zoneTicks.find(
          (entry) => entry.unitKey === unitKey,
        )?.ticks
      : undefined) ??
    null;

  return {
    unitKey,
    actorKey: activeActor?.actorKey ?? null,
    teamId: unit.teamId,
    unitId: unit.unitId,
    lifeId: activeActor?.identity.lifeId ?? null,
    participantId: unit.controllerParticipantId,
    legacySlot: legacySlotForUnit(replay, unitKey),
    name: unitName(replay, unitKey),
    accent: playerAccent(unitAccent(replay, unitKey, formId)),
    lookLabel: look.label,
    runtimeKind: participant?.runtimeKind ?? 'unknown',
    formId,
    canMove: form?.canMove ?? true,
    omnidirectionalVision: form?.omnidirectionalVision ?? false,
    omnidirectionalShooting: form?.omnidirectionalShooting ?? false,
    status,
    respawnAtTick: afterUnit?.respawnAtTick ?? null,
    unlockAtTick: afterUnit?.unlockAtTick ?? null,
    rebuildReadyAtTick: afterUnit?.rebuildReadyAtTick ?? null,
    fabricationAtTick: afterUnit?.fabricationAtTick ?? null,
    reservedSpawn: afterUnit?.reservedSpawn
      ? { ...afterUnit.reservedSpawn }
      : null,
    pendingSpawnReason: afterUnit?.pendingSpawnReason ?? null,
    pendingFormTransition: activeActor?.pendingFormTransition
      ? { ...activeActor.pendingFormTransition }
      : null,
    health: activeActor?.health ?? (status === 'active' ? (actor?.health ?? 0) : 0),
    maxHealth: form?.maxHealth ?? Math.max(1, actor?.health ?? maxHealthFallback(replay)),
    cooldown: activeActor?.cooldown ?? 0,
    energy: activeActor?.energy ?? null,
    zoneTicks,
    holdingObjective:
      onObjective &&
      status === 'active' &&
      (form?.objectiveWeight ?? 1) > 0 &&
      (after?.objective.kind === 'frontline' ||
        replay.contract.rules.objective.sharedPressureEnabled ||
        (turn?.actionResolution.validatedActionId === 'wait' &&
          turn.actionResolution.result === 'success')),
    actionId: turn?.actionResolution.chosenActionId ?? null,
    actionLaunchHeading:
      turn?.actionResolution.chosenPayload?.launchHeading ?? null,
    actionResult: turn?.actionResolution.result ?? null,
    debug: turn?.runtimeReply.debugMessage ?? null,
    visibleTiles: turn?.observation.visibleTiles.length ?? 0,
    visibleEnemies:
      turn?.observation.enemies.map((enemy) => ({
        x: enemy.position.x,
        y: enemy.position.y,
      })) ?? [],
  };
}

function maxHealthFallback(replay: ReplayModel): number {
  return Math.max(1, ...replay.forms.map((form) => form.maxHealth));
}

function objectiveAt(
  replay: ReplayModel,
  tickIndex: number,
  legacyZone: LegacyZone | null,
): ObjectivePresentation | null {
  const tick = replay.ticks[tickIndex];
  const objective = tick.after.objective;
  if (objective.kind === 'frontline') {
    const legacyDefinition =
      replay.contract.kind === 'v2-full'
        ? replay.contract.rules.frontlineDefinition
        : null;
    const genericDefinition =
      replay.contract.kind === 'v3-generic' &&
      replay.contract.mode.kind === 'frontline'
        ? replay.contract.mode
        : null;
    const threshold =
      legacyDefinition?.capture.threshold ??
      genericDefinition?.capture.threshold ??
      1;
    const terminalWinner =
      tickIndex === replay.ticks.length - 1 &&
      replay.result?.mode?.kind === 'frontline' &&
      replay.result.mode.reason === 'base-breach'
        ? replay.result.winnerTeamId
        : null;
    const winnerTeamId = objective.winnerTeamId ?? terminalWinner;
    let phase: string;
    if (winnerTeamId !== null) {
      phase = `${teamName(replay, winnerTeamId)} BREACHES`;
    } else if (objective.controlResumesAtTick > objective.nextTick) {
      phase = `REDEPLOYMENT · RESUMES TICK ${objective.controlResumesAtTick}`;
    } else if (objective.claimingTeamId === null) {
      phase = 'FRONTLINE NEUTRAL';
    } else {
      phase =
        `${teamName(replay, objective.claimingTeamId)} PUSHING · ` +
        `${objective.captureProgress}/${threshold}`;
    }
    return {
      kind: 'frontline',
      activePositionIndex: objective.activePositionIndex,
      positionCount:
        legacyDefinition?.frontlinePositionCount ??
        genericDefinition?.frontlinePositionCount ??
        replay.map.frontline?.positions.length ??
        0,
      claimingTeamId: objective.claimingTeamId,
      captureProgress: objective.captureProgress,
      captureThreshold: threshold,
      controlResumesAtTick: objective.controlResumesAtTick,
      winnerTeamId,
      phase,
    };
  }

  const rules = replay.contract.rules.objective;
  if (
    objective.mode !== 'shared-pressure' ||
    rules.controlPressureLimit === null ||
    legacyZone === null
  ) {
    return null;
  }
  const overtime =
    rules.overtime.startTick !== null &&
    tick.tick >= rules.overtime.startTick;
  const limit =
    overtime && rules.overtime.pressureLimit !== null
      ? rules.overtime.pressureLimit
      : rules.controlPressureLimit;
  const occupants = objectiveOccupants(tick.after, legacyZone);
  const previous =
    tickIndex > 0
      ? objectiveOccupants(
          replay.ticks[tickIndex - 1].after,
          legacyZone,
        )
      : [];
  let phase: string;
  if (rules.controlBySoleOccupancy) {
    if (occupants.length === 1) {
      const holder = unitName(replay, occupants[0]);
      phase =
        previous.length > 1
          ? `CONTEST BROKEN · ${holder} GAINS`
          : `SOLE OCCUPANT · ${holder} GAINS`;
    } else {
      phase =
        occupants.length > 1
          ? 'CONTESTED · PRESSURE DECAYS'
          : 'EMPTY · PRESSURE DECAYS';
    }
  } else {
    const holders = tick.actorTurns
      .filter(
        (turn) =>
          turn.actionResolution.validatedActionId === 'wait' &&
          turn.actionResolution.result === 'success' &&
          occupants.includes(turn.actor.unitKey),
      )
      .map((turn) => turn.actor.unitKey);
    phase =
      holders.length === 1
        ? `HOLDING · ${unitName(replay, holders[0])} GAINS`
        : holders.length > 1
          ? 'BOTH HOLD · PRESSURE FROZEN'
          : 'NO ACTIVE HOLD · PRESSURE DECAYS';
  }
  const orderedTeams = [...replay.teams].sort(
    (left, right) => left.teamId - right.teamId,
  );
  return {
    kind: 'legacy-control',
    pressure: objective.controlPressure ?? 0,
    limit,
    overtime,
    phase,
    names: [
      teamName(replay, orderedTeams[0]?.teamId ?? 0),
      teamName(replay, orderedTeams[1]?.teamId ?? 1),
    ],
  };
}

type LegacyZone = {
  tiles: Set<string>;
  cumulative: Map<ReplayStableUnitKey, number>[] | null;
};

function deriveLegacyZone(replay: ReplayModel): LegacyZone | null {
  if (replay.map.objectiveTiles.length === 0) return null;
  const tiles = new Set(
    replay.map.objectiveTiles.map((point) => `${point.x},${point.y}`),
  );
  if (replay.contract.rules.objective.sharedPressureEnabled) {
    return { tiles, cumulative: null };
  }

  const authoritative = replay.ticks.map((tick) =>
    tick.after.objective.kind === 'legacy'
      ? new Map(
          tick.after.objective.zoneTicks.map((entry) => [
            entry.unitKey,
            entry.ticks,
          ]),
        )
      : new Map<ReplayStableUnitKey, number>(),
  );
  if (authoritative.some((tally) => tally.size > 0)) {
    return { tiles, cumulative: authoritative };
  }

  // Historical replay-v1 omitted per-tick tallies. Preserve its viewer
  // compatibility derivation, selecting the accrual mode that agrees with the
  // authoritative terminal result.
  const sharedRun = new Map<ReplayStableUnitKey, number>();
  const exclusiveRun = new Map<ReplayStableUnitKey, number>();
  const shared: Map<ReplayStableUnitKey, number>[] = [];
  const exclusive: Map<ReplayStableUnitKey, number>[] = [];
  for (const tick of replay.ticks) {
    const on = objectiveOccupants(tick.after, { tiles, cumulative: null });
    for (const unitKey of on) {
      sharedRun.set(unitKey, (sharedRun.get(unitKey) ?? 0) + 1);
    }
    if (on.length === 1) {
      exclusiveRun.set(
        on[0],
        (exclusiveRun.get(on[0]) ?? 0) + 1,
      );
    }
    shared.push(new Map(sharedRun));
    exclusive.push(new Map(exclusiveRun));
  }
  const matchesResult = (
    series: Map<ReplayStableUnitKey, number>[],
  ): boolean =>
    replay.result !== null &&
    replay.result.teams.every((team) => {
      const unitKey = replay.units.find(
        (unit) => unit.teamId === team.teamId,
      )?.unitKey;
      return (
        unitKey !== undefined &&
        (series.at(-1)?.get(unitKey) ?? 0) === (team.zoneTicks ?? 0)
      );
    });
  return {
    tiles,
    cumulative: matchesResult(exclusive)
      ? exclusive
      : matchesResult(shared)
        ? shared
        : exclusive,
  };
}

function objectiveOccupants(
  world: ReplayWorldSnapshot,
  zone: LegacyZone,
): ReplayStableUnitKey[] {
  return world.actors
    .filter(
      (actor: ReplayActorState) =>
        actor.status === 'active' &&
        zone.tiles.has(`${actor.position.x},${actor.position.y}`),
    )
    .map((actor) => actor.unitKey);
}
