import type { TickPresentation } from '../replayPresentation';

export type FrontlineProgressDirection =
  | 'none'
  | 'steady'
  | 'building'
  | 'eroding'
  | 'frozen';

export type FrontlineCaptureState =
  | 'neutral'
  | 'claim'
  | 'building'
  | 'eroding'
  | 'contested'
  | 'holding';

/**
 * One renderer-neutral reading of the exact Frontline control state.
 *
 * The claimant owns the progress already in the meter. A different
 * rules-resolved pressure team is therefore eroding that incumbent claim, not
 * building its own; only after the authoritative claimant flips may the
 * challenger fill the meter in its colour. Ratchet ownership is carried
 * separately because it can outlive redeployment and coexist with a new claim.
 */
export interface FrontlineCaptureVisual {
  state: FrontlineCaptureState;
  progressDirection: FrontlineProgressDirection;
  claimantTeamId: number | null;
  captureTeamId: number | null;
  challengerTeamId: number | null;
  contested: boolean;
  capturePaused: boolean;
  progressFraction: number;
  holdOwnerTeamId: number | null;
  holdEndsAtTick: number | null;
  holdRemainingTicks: number | null;
  holdFraction: number;
  claimantAccent: string | null;
  challengerAccent: string | null;
  holdAccent: string | null;
}

export function frontlineCaptureVisual(
  presentation: TickPresentation,
): FrontlineCaptureVisual | null {
  const objective = presentation.objective;
  if (objective?.kind !== 'frontline') return null;

  const claimantTeamId = objective.claimingTeamId;
  const captureTeamId = objective.captureTeamId;
  const contested = objective.captureContested;
  const progressFraction = clamp01(
    objective.captureProgress / Math.max(1, objective.captureThreshold),
  );
  const holdLive =
    objective.holdOwnerTeamId !== null &&
    objective.holdEndsAtTick !== null &&
    objective.holdRemainingTicks !== null &&
    objective.holdRemainingTicks > 0;

  let progressDirection: FrontlineProgressDirection;
  if (objective.capturePaused || contested) {
    progressDirection = 'frozen';
  } else if (claimantTeamId === null || progressFraction === 0) {
    progressDirection = 'none';
  } else if (captureTeamId === claimantTeamId) {
    progressDirection = 'building';
  } else if (captureTeamId !== null) {
    progressDirection = 'eroding';
  } else {
    progressDirection = 'steady';
  }

  const state: FrontlineCaptureState = holdLive
    ? 'holding'
    : contested
      ? 'contested'
      : progressDirection === 'building'
        ? 'building'
        : progressDirection === 'eroding'
          ? 'eroding'
          : claimantTeamId !== null
            ? 'claim'
            : 'neutral';

  const accentFor = (teamId: number | null): string | null =>
    teamId === null
      ? null
      : presentation.units.find((unit) => unit.teamId === teamId)
          ?.accent ?? null;
  const challengerTeamId =
    progressDirection === 'eroding' ? captureTeamId : null;

  return {
    state,
    progressDirection,
    claimantTeamId,
    captureTeamId,
    challengerTeamId,
    contested,
    capturePaused: objective.capturePaused,
    progressFraction,
    holdOwnerTeamId: holdLive ? objective.holdOwnerTeamId : null,
    holdEndsAtTick: holdLive ? objective.holdEndsAtTick : null,
    holdRemainingTicks: holdLive
      ? objective.holdRemainingTicks
      : null,
    holdFraction: holdLive
      ? clamp01(
          objective.holdRemainingTicks! /
            Math.max(
              1,
              objective.holdDurationTicks ??
                objective.holdRemainingTicks!,
            ),
        )
      : 0,
    claimantAccent: accentFor(claimantTeamId),
    challengerAccent: accentFor(challengerTeamId),
    holdAccent: accentFor(
      holdLive ? objective.holdOwnerTeamId : null,
    ),
  };
}

function clamp01(value: number): number {
  return Math.max(0, Math.min(1, value));
}
