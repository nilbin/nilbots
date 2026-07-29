export interface ReturnTarget {
  to: string;
  label: string;
}

/**
 * Resolve router state into a safe same-site return link.
 *
 * Entry pages can preserve filters and context without turning a browser-history guess
 * into navigation. Direct links and refreshes retain a useful route-specific fallback.
 */
export function internalReturnTarget(
  state: unknown,
  fallback: ReturnTarget,
): ReturnTarget {
  if (state === null || typeof state !== 'object') return fallback;
  const candidate = state as {
    returnTo?: unknown;
    returnLabel?: unknown;
  };
  if (
    typeof candidate.returnTo !== 'string' ||
    !candidate.returnTo.startsWith('/') ||
    candidate.returnTo.startsWith('//')
  )
    return fallback;
  return {
    to: candidate.returnTo,
    label:
      typeof candidate.returnLabel === 'string' &&
      candidate.returnLabel.trim() !== ''
        ? candidate.returnLabel
        : fallback.label,
  };
}
