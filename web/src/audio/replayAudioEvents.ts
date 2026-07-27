import type { ReplayModel } from '../replayModel';
import type { SoundEffectCueId } from './soundEffects';

export interface ReplayAudioEvent {
  cue: SoundEffectCueId;
  /** Position within the visual tick; values above one intentionally trail it. */
  tickOffset: number;
  priority: number;
  /**
   * Where it happened across the arena, -1 (left edge) to 1 (right edge), or null when
   * the authoritative event has no usable presentation position.
   *
   * Panned rather than positioned in 3D: the arena is a flat plan view on a phone
   * speaker or a laptop, so left/right is the only axis a listener can actually resolve.
   */
  pan: number | null;
}

/**
 * Maps authoritative replay events onto presentation audio.
 */
export function replayAudioEventsAt(
  replay: ReplayModel,
  tickIndex: number,
): ReplayAudioEvent[] {
  const tick = replay.ticks[tickIndex];
  if (!tick) return [];

  const width = replay.map.width;
  /** Tile column to a pan position, with the map's centre column at dead centre. */
  const panAt = (x: number | undefined): number | null => {
    if (typeof x !== 'number' || width <= 1) return null;
    return Math.max(-1, Math.min(1, (x / (width - 1)) * 2 - 1));
  };

  const scheduled: ReplayAudioEvent[] = [];
  for (const event of tick.events) {
    // Events carry their origin tile; a shot is placed where it was fired from.
    const pan = panAt(event.from?.x ?? event.to?.x);
    switch (event.type) {
      case 'shot':
        scheduled.push({ cue: 'projectile', tickOffset: 0.46, priority: 1, pan });
        break;
      case 'damage':
        scheduled.push({ cue: 'impact', tickOffset: 0.56, priority: 2, pan });
        break;
      case 'destroyed':
      case 'disqualified':
        scheduled.push({ cue: 'destroyed', tickOffset: 0.68, priority: 4, pan });
        break;
    }
  }

  return scheduled;
}
