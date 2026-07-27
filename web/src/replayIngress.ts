import type { ReplayModel } from './replayModel';
import {
  decodeReplay,
  decodeReplayJson,
  type ReplayWireDocument,
} from './replayNormalize';

/**
 * Ingress evidence retained beside the normalized model. Raw text is available
 * whenever the replay arrived as text, so hash verification can consume the
 * original bytes instead of a lossy JSON reserialization.
 */
export interface LoadedReplay {
  replay: ReplayModel;
  wire: ReplayWireDocument;
  replayVersion: 1 | 2;
  rawJson: string | null;
}

export function loadReplayJson(rawJson: string): LoadedReplay {
  const decoded = decodeReplayJson(rawJson);
  return {
    replay: decoded.replay,
    wire: decoded.wire,
    replayVersion: decoded.replayVersion,
    rawJson: decoded.rawJson,
  };
}

export function loadReplayObject(input: unknown): LoadedReplay {
  const decoded = decodeReplay(input);
  return {
    replay: decoded.replay,
    wire: decoded.wire,
    replayVersion: decoded.replayVersion,
    rawJson: null,
  };
}
