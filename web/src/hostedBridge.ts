import type {
  ReplayModel,
  ReplayStableUnitKey,
  ReplayWorldSnapshot,
} from './replayModel';
import type {
  TickPresentation,
  UnitPresentation,
} from './replayPresentation';

export type HostedBridgeVersion = 1 | 2 | 3;

export interface HostedTransportState {
  playing: boolean;
  speed: number;
  tick: number;
  tickCount: number;
  atEnd: boolean;
  following: boolean;
  loading: boolean;
  pendingAssets: number;
}

export function hostedBridgeVersion(search: string): HostedBridgeVersion {
  const requested = new URLSearchParams(search).get('bridge');
  return requested === '3' ? 3 : requested === '2' ? 2 : 1;
}

export function bridgeSupportsReplay(
  bridgeVersion: HostedBridgeVersion,
  replay: ReplayModel,
): boolean {
  return (
    bridgeVersion === 3 ||
    (bridgeVersion === 2 && replay.sourceVersion <= 2) ||
    replay.sourceVersion === 1
  );
}

export function readyBridgeMessage(version: HostedBridgeVersion) {
  return version > 1
    ? { type: 'ready' as const, bridgeVersion: version }
    : { type: 'ready' as const };
}

export function loadedBridgeMessage(version: HostedBridgeVersion) {
  return version > 1
    ? { type: 'loaded' as const, bridgeVersion: version }
    : { type: 'loaded' as const };
}

export function errorBridgeMessage(
  version: HostedBridgeVersion,
  reason: string,
) {
  return version > 1
    ? {
        type: 'error' as const,
        bridgeVersion: version,
        reason,
      }
    : { type: 'error' as const, reason };
}

export function replayBridgeMessage(
  version: HostedBridgeVersion,
  replay: ReplayModel,
  tickCount: number,
  maxHealth: number,
) {
  if (version === 1) {
    if (replay.sourceVersion !== 1) {
      return errorBridgeMessage(1, 'unsupported-replay-version');
    }
    return {
      type: 'replay' as const,
      header: {
        mapId: replay.map.mapId,
        seed: replay.seed,
        rulesVersion: replay.versions.gameRulesVersion,
        replayHash: replay.replayHash,
        tickCount,
        maxHealth,
        partial: replay.partial,
        participants: replay.participants.map((participant) => ({
          slot: participant.teamId,
          name: participant.name,
          accent: participant.accent,
          ...(participant.lookId === null
            ? {}
            : { lookId: participant.lookId }),
        })),
      },
      result: replay.result
        ? {
            winnerSlot: replay.result.winnerTeamId,
            reason: legacyReason(replay.result.reason),
            endTick: replay.result.endTick,
          }
        : null,
    };
  }

  const stable = {
    type: 'replay' as const,
    header: {
      replayVersion: replay.sourceVersion,
      mapId: replay.map.mapId,
      seed: replay.seed,
      seedExact: replay.seedExact,
      rulesVersion: replay.versions.gameRulesVersion,
      replayHash: replay.replayHash,
      tickCount,
      partial: replay.partial,
      participants: replay.participants.map((participant) => ({
        participantId: participant.participantId,
        teamId: participant.teamId,
        name: participant.name,
        accent: participant.accent,
        lookId: participant.lookId,
      })),
      teams: replay.teams.map((team) => ({
        teamId: team.teamId,
        unitKeys: [...team.unitKeys],
      })),
      units: replay.units.map((unit) => ({
        unitKey: unit.unitKey,
        teamId: unit.teamId,
        unitId: unit.unitId,
        controllerParticipantId: unit.controllerParticipantId,
        initialLifeId: unit.initialLifeId,
        initialFormId: unit.initialFormId,
      })),
      forms: replay.forms.map((form) => ({
        formId: form.formId,
        maxHealth: form.maxHealth,
        canMove: form.canMove,
        canShoot: form.canShoot,
        omnidirectionalVision: form.omnidirectionalVision,
        omnidirectionalShooting: form.omnidirectionalShooting,
        objectiveWeight: form.objectiveWeight,
        allowedActionIds: form.allowedActionIds
          ? [...form.allowedActionIds]
          : [],
      })),
    },
    result: replay.result
      ? {
          winnerTeamId: replay.result.winnerTeamId,
          reason: replay.result.reason,
          endTick: replay.result.endTick,
          territorialScore: replay.result.territorialScore,
          teams: replay.result.teams.map((team) => ({
            teamId: team.teamId,
            outcome: team.outcome,
            activeHealth: team.activeHealth,
            damageDealt: team.damageDealt,
            units: team.units.map((unit) => ({
              unitKey: unit.unitKey,
              teamId: unit.teamId,
              unitId: unit.unitId,
              defaultFormId: unit.defaultFormId,
              formId: unit.formId,
              pendingFormTransition: unit.pendingFormTransition
                ? { ...unit.pendingFormTransition }
                : null,
              lifecycleStatus: unit.lifecycleStatus,
              activeActorKey: unit.activeActorKey,
              health: unit.health,
              damageDealt: unit.damageDealt,
            })),
          })),
        }
      : null,
  };
  if (version === 2) {
    return {
      ...stable,
      bridgeVersion: 2 as const,
    };
  }
  return {
    ...stable,
    bridgeVersion: 3 as const,
    header: {
      ...stable.header,
      mode: {
        kind:
          replay.contract.kind === 'v3-generic'
            ? replay.contract.modeKind
            : replay.contract.rules.objective.mode,
        id:
          replay.contract.kind === 'v3-generic'
            ? replay.contract.modeId
            : replay.contract.rules.rulesetId,
      },
    },
    result: replay.result
      ? {
          ...stable.result!,
          reportedEndTick:
            replay.result.reportedEndTick === undefined
              ? replay.result.endTick
              : replay.result.reportedEndTick,
          eligibleTeamIds: replay.result.eligibleTeamIds ?? [],
          mode: replay.result.mode ?? null,
          teams: replay.result.teams.map((team) => ({
            ...stable.result!.teams.find(
              (candidate) => candidate.teamId === team.teamId,
            )!,
            rank: team.rank ?? null,
            scores: team.scores ?? [],
          })),
        }
      : null,
  };
}

export function tickBridgeMessage(
  version: HostedBridgeVersion,
  presentation: TickPresentation,
  world?: ReplayWorldSnapshot,
) {
  if (version === 1) {
    return {
      type: 'tick' as const,
      tick: presentation.tick,
      control:
        presentation.objective?.kind === 'legacy-control'
          ? {
              pressure: presentation.objective.pressure,
              limit: presentation.objective.limit,
              overtime: presentation.objective.overtime,
              phase: presentation.objective.phase,
              names: presentation.objective.names,
            }
          : null,
      bots: presentation.units.map(legacyBotPresentation),
    };
  }
  return {
    type: 'tick' as const,
    bridgeVersion: version,
    tick: presentation.tick,
    objective: presentation.objective,
    units: presentation.units,
    ...(version === 3
      ? {
          scoreboard: world?.scoreboard ?? null,
          mode: world?.mode ?? null,
        }
      : {}),
  };
}

export function transportBridgeMessage(
  version: HostedBridgeVersion,
  state: HostedTransportState,
) {
  return version > 1
    ? {
        type: 'transport' as const,
        bridgeVersion: version,
        ...state,
      }
    : { type: 'transport' as const, ...state };
}

export function selectedBridgeMessage(
  version: HostedBridgeVersion,
  replay: ReplayModel,
  unitKey: ReplayStableUnitKey | null,
) {
  if (version > 1) {
    return {
      type: 'selected' as const,
      bridgeVersion: version,
      unitKey,
    };
  }
  const unit =
    unitKey === null
      ? null
      : replay.units.find((candidate) => candidate.unitKey === unitKey) ??
        null;
  return {
    type: 'selected' as const,
    slot: unit?.teamId ?? null,
  };
}

function legacyBotPresentation(unit: UnitPresentation) {
  return {
    slot: unit.legacySlot ?? unit.teamId,
    name: unit.name,
    accent: unit.accent,
    lookLabel: unit.lookLabel,
    runtimeKind: unit.runtimeKind,
    status: legacyStatus(unit.status),
    health: unit.health,
    maxHealth: unit.maxHealth,
    cooldown: unit.cooldown,
    ...(unit.energy === null ? {} : { energy: unit.energy }),
    zoneTicks: unit.zoneTicks,
    holdingZone: unit.holdingObjective,
    ...(unit.actionId === null
      ? {}
      : { action: legacyAction(unit.actionId) }),
    ...(unit.actionResult === null
      ? {}
      : { actionResult: legacyActionResult(unit.actionResult) }),
    ...(unit.debug === null ? {} : { debug: unit.debug }),
    visibleTiles: unit.visibleTiles,
    visibleEnemies: unit.visibleEnemies,
  };
}

function legacyStatus(status: UnitPresentation['status']): string {
  switch (status) {
    case 'active':
      return 'Active';
    case 'destroyed':
    case 'respawning':
      return 'Destroyed';
    case 'disqualified':
      return 'Disqualified';
    default:
      return status;
  }
}

function legacyAction(actionId: string): string {
  switch (actionId) {
    case 'wait':
      return 'Wait';
    case 'move-forward':
      return 'MoveForward';
    case 'turn-left':
      return 'TurnLeft';
    case 'turn-right':
      return 'TurnRight';
    case 'shoot':
      return 'Shoot';
    case 'strafe-left':
      return 'StrafeLeft';
    case 'strafe-right':
      return 'StrafeRight';
    default:
      return actionId;
  }
}

function legacyActionResult(result: string): string {
  switch (result) {
    case 'none':
      return 'None';
    case 'success':
      return 'Success';
    case 'blocked':
      return 'Blocked';
    case 'on-cooldown':
      return 'OnCooldown';
    case 'faulted':
      return 'Faulted';
    default:
      return result;
  }
}

function legacyReason(reason: string): string {
  switch (reason) {
    case 'elimination':
      return 'Elimination';
    case 'disqualification':
      return 'Disqualification';
    case 'max-ticks':
      return 'MaxTicks';
    case 'domination':
      return 'Domination';
    default:
      return reason;
  }
}
