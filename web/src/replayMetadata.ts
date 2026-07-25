import type { ReplayDocument } from './types';

const historicalMaxHealth = 3;
const legacyCache = new WeakMap<ReplayDocument, number>();

/**
 * New non-default rules snapshot maxHealth in the replay header. Historical
 * replays omit it; scan their recorded states so synthetic/custom legacy
 * replays above the original three-health default still render honestly.
 */
export function replayMaxHealth(replay: ReplayDocument): number {
  const snapshot = replay.header.maxHealth;
  if (snapshot !== undefined && Number.isInteger(snapshot) && snapshot > 0)
    return snapshot;

  const cached = legacyCache.get(replay);
  if (cached !== undefined) return cached;

  let observed = historicalMaxHealth;
  for (const tick of replay.ticks) {
    for (const state of tick.state)
      observed = Math.max(observed, state.health);
    for (const bot of tick.bots)
      for (const enemy of bot.visibleEnemies)
        observed = Math.max(observed, enemy.health);
  }
  legacyCache.set(replay, observed);
  return observed;
}
