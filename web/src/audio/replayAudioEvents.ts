import type { ReplayDocument } from '../types';
import type { AudioCueId } from './audioCandidates';

export interface ReplayAudioEvent {
  cue: AudioCueId;
  /** Position within the visual tick; values above one intentionally trail it. */
  tickOffset: number;
  priority: number;
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

  const scheduled: ReplayAudioEvent[] = [];
  for (const event of tick.events) {
    switch (event.type) {
      case 'Shot':
        scheduled.push({ cue: 'projectile', tickOffset: 0.46, priority: 1 });
        break;
      case 'Damage':
        scheduled.push({ cue: 'impact', tickOffset: 0.56, priority: 2 });
        break;
      case 'Destroyed':
      case 'Disqualified':
        scheduled.push({ cue: 'destroyed', tickOffset: 0.68, priority: 4 });
        break;
    }
  }

  if (
    replay.result &&
    tick.tick === replay.result.endTick &&
    replay.result.winnerSlot !== null
  ) {
    scheduled.push({ cue: 'unlock', tickOffset: 2.35, priority: 3 });
  }
  return scheduled;
}
