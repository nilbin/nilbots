/**
 * One proportional health vocabulary for bodies and damageable constructs.
 *
 * The bar always occupies the same physical width. Maximum health changes how
 * quickly it drains, never how much screen space a body is allowed to claim.
 */
export type HealthBarTone = 'healthy' | 'wounded' | 'critical';

export const HEALTH_BAR_COLORS: Readonly<Record<HealthBarTone, string>> = {
  healthy: '#4ade80',
  wounded: '#fbbf24',
  critical: '#f87171',
};

/** One world-tile-relative width, shared with the renderer's cooldown tracks. */
export const HEALTH_BAR_RELATIVE_WIDTH = 0.52;

export interface HealthBarState {
  current: number;
  maximum: number;
  fraction: number;
  tone: HealthBarTone;
  color: string;
}

export function healthBarState(
  currentHealth: number,
  maximumHealth: number,
): HealthBarState {
  const maximum = Math.max(1, Math.floor(maximumHealth));
  const current = Math.max(0, Math.min(maximum, Math.floor(currentHealth)));
  const fraction = current / maximum;
  const tone: HealthBarTone = fraction > 0.5
    ? 'healthy'
    : fraction > 0.25
      ? 'wounded'
      : 'critical';
  return {
    current,
    maximum,
    fraction,
    tone,
    color: HEALTH_BAR_COLORS[tone],
  };
}
