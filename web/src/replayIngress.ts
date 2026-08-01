import type { ReplayModel } from './replayModel';
import {
  decodeReplay,
  decodeReplayJson,
  type ReplayWireDocument,
} from './replayNormalize';
import {
  expandArcRelayBroadcastV1,
  isArcRelayBroadcastV1,
} from './replayBroadcastV1';
import { normalizeReplayV3 } from './replayV3Normalize';

/**
 * Ingress evidence retained beside the normalized model. Raw text is available
 * whenever the replay arrived as text, so hash verification can consume the
 * original bytes instead of a lossy JSON reserialization.
 */
export interface LoadedReplay {
  replay: ReplayModel;
  wire: ReplayWireDocument;
  replayVersion: 1 | 2 | 3;
  rawJson: string | null;
}

export function loadReplayJson(rawJson: string): LoadedReplay {
  // Broadcast documents put their discriminator first. Avoid parsing ordinary
  // canonical replays here because decodeReplayJson must parse them once
  // already, and evaluation replays can be hundreds of megabytes.
  if (/^\s*\{\s*"broadcastVersion"\s*:\s*1\b/.test(rawJson)) {
    const parsed = JSON.parse(rawJson) as unknown;
    if (isArcRelayBroadcastV1(parsed)) {
      return loadArcRelayBroadcast(parsed);
    }
  }
  const decoded = decodeReplayJson(rawJson);
  return {
    replay: decoded.replay,
    wire: decoded.wire,
    replayVersion: decoded.replayVersion,
    rawJson: decoded.rawJson,
  };
}

export function loadReplayObject(input: unknown): LoadedReplay {
  if (isArcRelayBroadcastV1(input)) return loadArcRelayBroadcast(input);
  const decoded = decodeReplay(input);
  return {
    replay: decoded.replay,
    wire: decoded.wire,
    replayVersion: decoded.replayVersion,
    rawJson: null,
  };
}

function loadArcRelayBroadcast(
  input: Parameters<typeof expandArcRelayBroadcastV1>[0],
): LoadedReplay {
  const wire = expandArcRelayBroadcastV1(input);
  return {
    replay: normalizeReplayV3(wire),
    wire,
    replayVersion: 3,
    // A broadcast carries the canonical replay hash as an address, but it is
    // not the canonical replay bytes. Never hand its transport text to the
    // canonical hash verifier.
    rawJson: null,
  };
}
