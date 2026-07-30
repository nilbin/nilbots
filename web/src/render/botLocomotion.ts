import type { BotLocomotionCue } from './arenaThemes';

export interface BotLocomotionSample {
  /** Presentation-only gap above the authoritative ground layer. */
  hover: number;
  /** Multiplier for lateral idle/drift motion; zero beside a wall for new hover families. */
  lateralScale: number;
  /** Multiplier for the existing turn/idle yaw. */
  yawScale: number;
  /** Multiplier and hard limit for the existing turn/idle bank. */
  rollScale: number;
  maxRoll: number;
  /** Bulwark settles onto the floor while entering a planted form. */
  settlesWhenPlanted: boolean;
}

const NO_HOVER: BotLocomotionSample = {
  hover: 0,
  lateralScale: 1,
  yawScale: 1,
  rollScale: 1,
  maxRoll: Number.POSITIVE_INFINITY,
  settlesWhenPlanted: false,
};

/**
 * Pure replay-time locomotion language for one look.
 *
 * The identity phase is derived by the caller from the stable unit key. Nothing here
 * retains frame state or uses randomness, so scrubbing to a timestamp always produces the
 * same pose. `low-hover` deliberately preserves Striker's original frequencies and
 * amplitude byte-for-behaviour; the other profiles are slower and more restrained.
 */
export function botLocomotionAt(
  cue: BotLocomotionCue | null,
  time: number,
  phase: number,
  boxedIn: boolean,
): BotLocomotionSample {
  if (cue === null) return NO_HOVER;

  if (cue === 'low-hover') {
    const rise =
      Math.sin(time * 2.3 + phase * 1.7) +
      Math.sin(time * 1.3 + phase) * 0.5;
    return {
      hover: 0.075 + rise * 0.014,
      lateralScale: 1,
      yawScale: 1,
      rollScale: 1,
      maxRoll: Number.POSITIVE_INFINITY,
      settlesWhenPlanted: false,
    };
  }

  if (cue === 'heavy-hover') {
    const settle =
      Math.sin(time * 1.15 + phase) * 0.75 +
      Math.sin(time * 0.63 + phase * 1.7) * 0.25;
    return {
      hover: 0.045 + settle * 0.004,
      lateralScale: boxedIn ? 0 : 0.42,
      yawScale: 0.35,
      rollScale: 0.3,
      maxRoll: Math.PI / 60,
      settlesWhenPlanted: true,
    };
  }

  const float =
    Math.sin(time * 0.85 + phase) * 0.7 +
    Math.sin(time * 0.47 + phase * 1.9) * 0.3;
  return {
    hover: 0.065 + float * 0.007,
    lateralScale: boxedIn ? 0 : 0.28,
    yawScale: 0.16,
    rollScale: 0.16,
    maxRoll: Math.PI / 80,
    settlesWhenPlanted: false,
  };
}

export function clampLocomotionRoll(
  roll: number,
  sample: BotLocomotionSample,
): number {
  return Math.max(
    -sample.maxRoll,
    Math.min(roll * sample.rollScale, sample.maxRoll),
  );
}
