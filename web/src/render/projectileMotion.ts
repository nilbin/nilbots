import type { ProjectileFlightCue } from './arenaThemes';

export interface ProjectileFlightMotion {
  /** Local Three.js head rotations; the parent rig retains authoritative heading. */
  rotationX: number;
  rotationY: number;
  rotationZ: number;
  /** Canvas approximation applied after the authoritative heading rotation. */
  canvasRotation: number;
  canvasScaleX: number;
  canvasScaleY: number;
}

const STILL: ProjectileFlightMotion = {
  rotationX: 0,
  rotationY: 0,
  rotationZ: 0,
  canvasRotation: 0,
  canvasScaleX: 1,
  canvasScaleY: 1,
};

/**
 * Authored projectile-head motion derived only from replay truth.
 *
 * The exact stable identity offsets simultaneous projectiles without narrowing legacy
 * decimal-string IDs. The returned transforms affect only the projectile head: its parent
 * still owns heading and position, while the renderer continues to own trail, glow and
 * impact timing.
 */
export function projectileFlightMotionAt(
  cue: ProjectileFlightCue | null,
  time: number,
  stableIdentity: string,
): ProjectileFlightMotion {
  if (cue === null) return STILL;
  const phase = phaseForIdentity(stableIdentity);

  if (cue === 'axial-spin') {
    const roll = time * 8.2 + phase;
    return {
      rotationX: roll,
      rotationY: 0,
      rotationZ: 0,
      canvasRotation: 0,
      canvasScaleX: 1,
      // A top-down sprite cannot expose a roll axis. A bounded thickness pulse conveys
      // it without ever reducing the head to an unreadable line.
      canvasScaleY: 0.58 + Math.abs(Math.cos(roll)) * 0.42,
    };
  }

  if (cue === 'flat-spin') {
    const spin = time * 5.4 + phase;
    return {
      rotationX: 0,
      rotationY: spin,
      rotationZ: 0,
      canvasRotation: spin,
      canvasScaleX: 1,
      canvasScaleY: 1,
    };
  }

  const tumble = time * 4.35 + phase;
  const pitch = tumble;
  const yaw = Math.sin(time * 2.15 + phase * 1.3) * 0.62;
  const roll = time * 3.2 + phase * 0.7;
  return {
    rotationX: pitch,
    rotationY: yaw,
    rotationZ: roll,
    canvasRotation: roll * 0.55,
    canvasScaleX: 0.78 + Math.abs(Math.cos(yaw)) * 0.22,
    canvasScaleY: 0.66 + Math.abs(Math.cos(pitch)) * 0.34,
  };
}

/** Stable FNV-1a phase; deliberately duplicated from actor idle motion to keep modules pure. */
function phaseForIdentity(identity: string): number {
  let hash = 2_166_136_261;
  for (let index = 0; index < identity.length; index++) {
    hash ^= identity.charCodeAt(index);
    hash = Math.imul(hash, 16_777_619);
  }
  return ((hash >>> 0) / 0xffff_ffff) * Math.PI * 2;
}
