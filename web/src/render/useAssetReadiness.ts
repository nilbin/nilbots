import { useEffect, useState } from 'react';
import { pendingAssets, subscribeToAssets } from './assetReadiness';

/**
 * How many images the arena is still waiting on.
 *
 * Seeded from the current count rather than zero: images are requested during render, so
 * a subscriber mounting afterwards would otherwise believe everything was ready.
 */
export function useAssetReadiness(): { pending: number; ready: boolean } {
  const [pending, setPending] = useState(pendingAssets);
  useEffect(() => subscribeToAssets(setPending), []);
  return { pending, ready: pending === 0 };
}
