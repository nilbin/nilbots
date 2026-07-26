import type { ReplayDocument } from '../types';
import type { AudioCueId } from './audioCandidates';

export interface ReplayAudioEvent {
  cue: AudioCueId;
  /** Position within the visual tick; values above one intentionally trail it. */
  tickOffset: number;
  priority: number;
  /**
   * Where it happened across the arena, -1 (left edge) to 1 (right edge), or null when
   * the event has no place — a UI reward rather than something in the world.
   *
   * Panned rather than positioned in 3D: the arena is a flat plan view on a phone
   * speaker or a laptop, so left/right is the only axis a listener can actually resolve.
   */
  pan: number | null;
}

/**
 * Maps authoritative replay events onto presentation audio.
 *
 * Match completion uses the unlock cue only in this review build. The final
 * product needs a distinct match-result cue; the mapping lets reviewers hear
 * the fourth candidate sample against real UI without pretending it is the
 * eventual match-win contract.
 */
export function replayAudioEventsAt(
  replay: ReplayDocument,
  tickIndex: number,
): ReplayAudioEvent[] {
  const tick = replay.ticks[tickIndex];
  if (!tick) return [];

  const width = replay.header.mapWidth;
  /** Tile column to a pan position, with the map's centre column at dead centre. */
  const panAt = (x: number | undefined): number | null => {
    if (typeof x !== 'number' || width <= 1) return null;
    return Math.max(-1, Math.min(1, (x / (width - 1)) * 2 - 1));
  };

  const scheduled: ReplayAudioEvent[] = [];
  for (const event of tick.events) {
    // Events carry their origin tile; a shot is placed where it was fired from.
    const pan = panAt((event as { fromX?: number; x?: number }).fromX ?? (event as { x?: number }).x);
    switch (event.type) {
      case 'Shot':
        scheduled.push({ cue: 'projectile', tickOffset: 0.46, priority: 1, pan });
        break;
      case 'Damage':
        scheduled.push({ cue: 'impact', tickOffset: 0.56, priority: 2, pan });
        break;
      case 'Destroyed':
      case 'Disqualified':
        scheduled.push({ cue: 'destroyed', tickOffset: 0.68, priority: 4, pan });
        break;
    }
  }

  if (
    replay.result &&
    tick.tick === replay.result.endTick &&
    replay.result.winnerSlot !== null
  ) {
    // A reward, not a thing in the arena — it belongs dead centre.
    scheduled.push({ cue: 'unlock', tickOffset: 2.35, priority: 3, pan: null });
  }
  return scheduled;
}
