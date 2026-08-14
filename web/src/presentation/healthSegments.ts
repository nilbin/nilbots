/**
 * One health vocabulary for bodies and damageable constructs.
 *
 * Historical 1..10 hull values keep one segment per point. Larger combat-scale
 * values are proportionalized into ten segments: the game still exposes the
 * exact current/maximum pair on inspection, while the field stays readable at
 * phone scale instead of allocating one tiny mark per hit point.
 */
export const MAX_HEALTH_SEGMENTS = 10;

export interface HealthSegments {
  count: number;
  filled: number;
}

export function healthSegments(
  currentHealth: number,
  maximumHealth: number,
): HealthSegments {
  const maximum = Math.max(1, Math.floor(maximumHealth));
  const current = Math.max(0, Math.min(maximum, Math.floor(currentHealth)));
  const count = Math.min(MAX_HEALTH_SEGMENTS, maximum);
  // Preserve a visible sliver for a living body even when one segment spans
  // several health points. Zero health remains unambiguously empty.
  const filled = current === 0
    ? 0
    : Math.min(count, Math.ceil((current * count) / maximum));
  return { count, filled };
}
