/** Small shape shared by the generated response and cadence tests. */
export interface BroadcastClockReading {
  presentationTicksPerSecond: number;
  presentationTick: number;
  broadcastComplete: boolean;
}

/**
 * Sample the authoritative clock at least twice during one presented tick.
 *
 * The replay payload remains causal, so polling slower than a tick necessarily makes the
 * client exhaust its revealed prefix and visibly stop. This endpoint is tiny; the much
 * larger replay document is fetched separately and only when this clock says a new tick
 * exists.
 */
export function liveClockPollMilliseconds(
  reading?: Pick<BroadcastClockReading, 'presentationTicksPerSecond' | 'broadcastComplete'>,
): number | false {
  if (reading?.broadcastComplete) return false;
  const ticksPerSecond = Math.max(0.1, reading?.presentationTicksPerSecond ?? 1);
  return Math.max(200, Math.min(500, Math.floor(500 / ticksPerSecond)));
}

/**
 * Fetch a causal replay prefix only while it trails the clock.
 *
 * This turns frequent cheap clock reads into at most one expensive object-store read,
 * parse and normalization per newly revealed tick. A short retry closes the interval
 * between seeing the clock advance and receiving the matching prefix.
 */
export function replayPrefixPollMilliseconds(
  reading: BroadcastClockReading | undefined,
  loadedTickCount: number,
): number | false {
  if (!reading || reading.broadcastComplete) return false;
  const visibleTickCount = Math.max(0, reading.presentationTick + 1);
  return loadedTickCount >= visibleTickCount ? false : 100;
}
