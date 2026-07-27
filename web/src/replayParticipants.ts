import type {
  ReplayActorIdentity,
  ReplayModel,
  ReplayParticipantController,
  ReplayStableUnit,
  ReplayStableUnitKey,
} from './replayModel';

/**
 * Participant, stable-unit, and actor-life identities are deliberately
 * separate. These lookups are the only viewer boundary that resolves one into
 * another; renderers never use collection positions as identity.
 */
export function participantsById(
  participants: readonly ReplayParticipantController[],
): ReadonlyMap<number, ReplayParticipantController> {
  return new Map(
    participants.map((participant) => [
      participant.participantId,
      participant,
    ]),
  );
}

export function unitsByKey(
  units: readonly ReplayStableUnit[],
): ReadonlyMap<ReplayStableUnitKey, ReplayStableUnit> {
  return new Map(units.map((unit) => [unit.unitKey, unit]));
}

export function participantForUnit(
  replay: ReplayModel,
  unitKey: ReplayStableUnitKey,
): ReplayParticipantController | null {
  const unit = replay.units.find((candidate) => candidate.unitKey === unitKey);
  if (!unit) return null;
  return (
    replay.participants.find(
      (participant) =>
        participant.participantId === unit.controllerParticipantId,
    ) ?? null
  );
}

export function participantForActor(
  replay: ReplayModel,
  actor: ReplayActorIdentity,
): ReplayParticipantController | null {
  return participantForUnit(replay, actor.unitKey);
}

export function legacySlotForUnit(
  replay: ReplayModel,
  unitKey: ReplayStableUnitKey,
): number | null {
  if (replay.sourceVersion !== 1) return null;
  return (
    replay.units.find((candidate) => candidate.unitKey === unitKey)?.teamId ??
    null
  );
}

/** Stable fallback only for choosing legacy artwork when no look ID exists. */
export function visualIndexForUnit(
  replay: ReplayModel,
  unitKey: ReplayStableUnitKey,
): number {
  return (
    legacySlotForUnit(replay, unitKey) ??
    Math.max(
      0,
      replay.units.findIndex((unit) => unit.unitKey === unitKey),
    )
  );
}

export function teamName(replay: ReplayModel, teamId: number): string {
  const names = replay.participants
    .filter((participant) => participant.teamId === teamId)
    .map((participant) => participant.name);
  return names.length === 1
    ? names[0]
    : names.length > 1
      ? names.join(' + ')
      : `team ${teamId}`;
}

export function unitName(
  replay: ReplayModel,
  unitKey: ReplayStableUnitKey,
): string {
  const unit = replay.units.find((candidate) => candidate.unitKey === unitKey);
  if (!unit) return unitKey;
  const participant = participantForUnit(replay, unitKey);
  const teamUnits = replay.units.filter(
    (candidate) => candidate.teamId === unit.teamId,
  );
  if (teamUnits.length === 1) {
    return participant?.name ?? teamName(replay, unit.teamId);
  }
  return `${participant?.name ?? teamName(replay, unit.teamId)} · unit ${unit.unitId}`;
}

export function actorName(
  replay: ReplayModel,
  actor: ReplayActorIdentity | null,
): string {
  if (!actor) return '?';
  const base = unitName(replay, actor.unitKey);
  return replay.sourceVersion === 1 ? base : `${base} · life ${actor.lifeId}`;
}
