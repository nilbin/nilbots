/**
 * Optional look-authored motion for a projectile head.
 *
 * The renderer's local projectile contract is +X forward and +Y up. These
 * transforms are applied to the head only, below the heading rig, so they
 * work unchanged for the SVG-derived fallback and a genuine GLB. Position,
 * tracer, floor glow, hitbox, and authoritative heading remain untouched.
 */
export type ProjectileFlightCue =
  | 'stable'
  | 'axial-spin'
  | 'flat-spin'
  | 'tumble';

export const projectileFlightCues: ReadonlySet<ProjectileFlightCue> =
  new Set([
    'stable',
    'axial-spin',
    'flat-spin',
    'tumble',
  ]);

export interface ProjectileFlightMotion {
  /** Rotation around +X, the authored forward/travel axis. */
  rotationX: number;
  /** Rotation around +Y, the authored up axis. */
  rotationY: number;
  /** Rotation around +Z, the authored lateral axis. */
  rotationZ: number;
  /** Canvas equivalent of rotation around +Y/+Z as seen from above. */
  canvasRotation: number;
  /** Canvas silhouette scale along the authored forward axis. */
  canvasScaleX: number;
  /** Canvas silhouette scale across the authored forward axis. */
  canvasScaleY: number;
}

export function isProjectileFlightCue(
  value: unknown,
): value is ProjectileFlightCue {
  return (
    typeof value === 'string' &&
    projectileFlightCues.has(value as ProjectileFlightCue)
  );
}

/**
 * Deterministic cosmetic phase without narrowing replay-v2 decimal IDs.
 *
 * Re-deriving this from the identity makes seeking and isolated-frame review
 * stable. It must never use Math.random() or retained frame state.
 */
export function projectileMotionPhase(identity: string): number {
  let hash = 2_166_136_261;
  for (let index = 0; index < identity.length; index++) {
    hash ^= identity.charCodeAt(index);
    hash = Math.imul(hash, 16_777_619);
  }
  return ((hash >>> 0) / 0xffff_ffff) * Math.PI * 2;
}

/**
 * Resolve one cue at replay time measured in ticks.
 *
 * Rates are deliberately moderate at the gameplay camera. Axial spin is the
 * quickest because its directional silhouette stays aligned with travel;
 * flat spin is slower because the whole planform turns; tumble advances by
 * roughly a quarter turn per tick on its primary axis.
 */
export function projectileFlightMotionAt(
  cue: ProjectileFlightCue,
  time: number,
  identity: string,
): ProjectileFlightMotion {
  const phase = projectileMotionPhase(identity);

  if (cue === 'stable')
    return {
      rotationX: 0,
      rotationY: 0,
      rotationZ: 0,
      canvasRotation: 0,
      canvasScaleX: 1,
      canvasScaleY: 1,
    };

  if (cue === 'axial-spin') {
    const roll = phase + time * 5.4;
    return {
      rotationX: roll,
      rotationY: 0,
      rotationZ: Math.sin(time * 2.1 + phase) * 0.06,
      canvasRotation: 0,
      canvasScaleX: 1,
      canvasScaleY: edgeOnScale(roll, 0.38),
    };
  }

  if (cue === 'flat-spin') {
    const spin = phase + time * 3.2;
    return {
      rotationX: 0,
      rotationY: spin,
      rotationZ: 0,
      canvasRotation: spin,
      canvasScaleX: 1,
      canvasScaleY: 1,
    };
  }

  const pitch = phase + time * 1.55;
  const roll = phase * 0.53 + time * 1.05;
  return {
    rotationX: pitch,
    rotationY: Math.sin(time * 0.9 + phase) * 0.24,
    rotationZ: roll,
    canvasRotation: roll,
    canvasScaleX: 0.92 + Math.abs(Math.cos(roll)) * 0.08,
    canvasScaleY: edgeOnScale(pitch, 0.48),
  };
}

/**
 * A Canvas sprite has no depth axis. Signed cross-axis compression is the
 * orthographic equivalent of a solid rolling through edge-on: it never
 * disappears completely, and the sign preserves the half-turn.
 */
function edgeOnScale(angle: number, minimum: number): number {
  const cosine = Math.cos(angle);
  const magnitude = minimum + (1 - minimum) * Math.abs(cosine);
  return cosine < 0 ? -magnitude : magnitude;
}
