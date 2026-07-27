import type { ArenaAudioSession } from '../audio/ArenaAudioSession';
import type { ReplayModel } from '../replayModel';
import { soundtrackPlaybackMode } from './preferences';
import SoundtrackControl from './SoundtrackControl';
import { useAdaptiveSoundtrack } from './useAdaptiveSoundtrack';

export interface AdaptiveSoundtrackProps {
  replay: ReplayModel;
  time: number;
  playing: boolean;
  playResolveTail: boolean;
  playbackSpeed: number;
  transportRevision: number;
  session: ArenaAudioSession;
  /** The viewer has already resumed the shared audio session from a gesture. */
  activationGranted?: boolean;
  presentationId?: string;
  followingLive?: boolean;
}

/**
 * HTTP-only score extension for the full viewer. The CLI build replaces this
 * dynamic module with a no-op, and HostedViewer never imports it.
 */
export default function AdaptiveSoundtrack({
  replay,
  time,
  playing,
  playResolveTail,
  playbackSpeed,
  transportRevision,
  session,
  activationGranted = false,
  presentationId,
  followingLive = false,
}: AdaptiveSoundtrackProps) {
  const query = new URLSearchParams(window.location.search);
  const requestedId =
    query.get('soundtrack') ?? undefined;
  const scoreMode = soundtrackPlaybackMode(window.location.search);
  const controller = useAdaptiveSoundtrack({
    available: true,
    replay,
    time,
    playing,
    playResolveTail,
    playbackSpeed,
    soundtrackId: requestedId,
    scoreMode,
    activationGranted,
    transportRevision,
    session,
    presentationId,
    followingLive,
  });

  return <SoundtrackControl controller={controller} />;
}
