import type { ReplayModel } from '../replayModel';
import type { AdaptiveMusicTimeline } from './director';
import type { SoundtrackTriggerEvent } from './types';

export interface SoundtrackTriggerCursor {
  initialized: boolean;
  presentationId: string;
  transportRevision: number;
  sourceTick: number;
  seen: Set<string>;
}

export interface SoundtrackTriggerBatch {
  triggers: SoundtrackTriggerEvent[];
  discontinuity: boolean;
}

export function createSoundtrackTriggerCursor(): SoundtrackTriggerCursor {
  return {
    initialized: false,
    presentationId: '',
    transportRevision: 0,
    sourceTick: -1,
    seen: new Set(),
  };
}

export function resetSoundtrackTriggerCursor(
  cursor: SoundtrackTriggerCursor,
  presentationId: string,
  transportRevision: number,
  sourceTick: number,
): void {
  cursor.initialized = true;
  cursor.presentationId = presentationId;
  cursor.transportRevision = transportRevision;
  cursor.sourceTick = sourceTick;
  cursor.seen.clear();
}

/**
 * Deliver every newly crossed authoritative tick exactly once. Explicit
 * transport changes and backwards jumps establish their destination silently
 * instead of replaying historical accents.
 */
export function collectCrossedSoundtrackTriggers(
  cursor: SoundtrackTriggerCursor,
  timeline: AdaptiveMusicTimeline,
  presentationId: string,
  transportRevision: number,
  sourceTick: number,
): SoundtrackTriggerBatch {
  const discontinuity =
    !cursor.initialized ||
    cursor.presentationId !== presentationId ||
    cursor.transportRevision !== transportRevision ||
    sourceTick < cursor.sourceTick;
  if (discontinuity) {
    resetSoundtrackTriggerCursor(
      cursor,
      presentationId,
      transportRevision,
      sourceTick,
    );
    return { triggers: [], discontinuity: true };
  }

  const triggers: SoundtrackTriggerEvent[] = [];
  if (sourceTick > cursor.sourceTick) {
    for (const frame of timeline.frames) {
      if (frame.tick <= cursor.sourceTick || frame.tick > sourceTick) continue;
      for (const type of frame.triggers) {
        const key = `${frame.tick}:${type}`;
        if (cursor.seen.has(key)) continue;
        cursor.seen.add(key);
        triggers.push({ type, sourceTick: frame.tick });
      }
    }
  }
  cursor.sourceTick = sourceTick;
  return { triggers, discontinuity: false };
}

export function soundtrackPresentationId(
  replay: ReplayModel,
  soundtrackId: string | undefined,
  explicitId?: string,
): string {
  // Live documents gain ticks, result, initialWorld, and replayHash. Use only
  // immutable match inputs so a prefix refresh is not mistaken for a seek.
  const replayId =
    explicitId ??
    JSON.stringify({
      sourceVersion: replay.sourceVersion,
      versions: replay.versions,
      seed: replay.seed,
      matchContractFingerprint: replay.matchContractFingerprint,
      rulesetId: replay.contract.rules.rulesetId,
      rulesFingerprint: replay.contract.rules.rulesFingerprint,
      map: [replay.map.mapId, replay.map.mapVersion],
      participants: replay.participants.map((participant) => [
        participant.participantKey,
        participant.teamKey,
        participant.artifactHash,
      ]),
      units: replay.units.map((unit) => unit.unitKey),
    });
  return `${soundtrackId ?? ''}\0${replayId}`;
}
