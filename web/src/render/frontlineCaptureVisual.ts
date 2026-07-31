import type {
  CaptureRevertPresentation,
  TickPresentation,
} from '../replayPresentation';

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
  /**
   * True when this ruleset captures by channelling. Renderers use it to decide
   * whether the meter is a *channel* — a bar filling toward a threshold that a
   * hit can knock back — rather than the older accumulating push. False on
   * every replay written before the arm, which keeps those exactly as they
   * were.
   */
  channel: boolean;
  /** The channel's capped surplus this tick; null while nobody controls. */
  channelGain: number | null;
  /** Accent of the team whose work a revert took, or null when none did. */
  revertAccent: string | null;
  /**
   * A revert this tick, with its beat resolved into a 0..1 strength so a
   * renderer can flash on it without keeping state between frames.
   *
   * The two kinds are drawn differently on purpose: an interrupt is a hit
   * reaction, and an erosion is a drain.
   */
  revert: FrontlineCaptureRevert | null;
}

export interface FrontlineCaptureRevert extends CaptureRevertPresentation {
  /** Where the meter stood before the revert, as a fraction of the arc. */
  ghostFraction: number;
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

  const revertReading = objective.captureRevert ?? null;
  const revert: FrontlineCaptureRevert | null =
    revertReading === null
      ? null
      : {
          ...revertReading,
          ghostFraction: clamp01(revertReading.fromFraction),
        };

  let progressDirection: FrontlineProgressDirection;
  if (revert !== null && revert.ticksSince === 0) {
    // The meter moved backwards this tick. Whatever the presence rule says
    // about who is standing where, that is the sentence to draw.
    progressDirection = revert.kind === 'interrupt' ? 'frozen' : 'eroding';
  } else if (objective.capturePaused || contested) {
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
    channel: objective.channel ?? false,
    channelGain: objective.channelGain ?? null,
    // The team that lost the work, which is not always the current claimant:
    // an erosion that reaches zero clears the claim outright, and the ghost of
    // what was taken still belongs to whoever built it.
    revertAccent: accentFor(revert?.teamId ?? null),
    revert,
  };
}

function clamp01(value: number): number {
  return Math.max(0, Math.min(1, value));
}
