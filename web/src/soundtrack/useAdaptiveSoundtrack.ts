import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import type {
  ArenaAudioGraph,
  ArenaAudioSession,
} from '../audio/ArenaAudioSession';
import type { ReplayModel } from '../replayModel';
import {
  buildAdaptiveTimeline,
  sampleAdaptiveTimeline,
} from './director.ts';
import {
  SoundtrackEngine,
  straightThroughPauseReasonForTransport,
  type StraightThroughPauseReason,
} from './SoundtrackEngine.ts';
import { loadSoundtrack } from './manifest.ts';
import type { SoundtrackController } from './SoundtrackControl';
import {
  collectCrossedSoundtrackTriggers,
  createSoundtrackTriggerCursor,
  resetSoundtrackTriggerCursor,
  soundtrackPresentationId,
} from './transport';
import type {
  SoundtrackPlaybackMode,
  SoundtrackStatus,
} from './types';

const VOLUME_KEY = 'nilbots.soundtrack.volume.v1';
const DEFAULT_VOLUME = 0.62;
const REPLAY_TICKS_PER_SECOND = 5;

interface SoundtrackPlanningGrid {
  bpm: number;
  beatsPerBar: number;
}

export interface AdaptiveSoundtrackInput {
  available: boolean;
  replay: ReplayModel;
  time: number;
  playing: boolean;
  session: ArenaAudioSession;
  /** Stable across live-prefix updates and the completed replay handoff. */
  presentationId?: string;
  /** The server clock owns presentation, so future replay ticks stay private. */
  followingLive?: boolean;
  /** Allow a finite resolution cue after transport reaches the end on its own. */
  playResolveTail?: boolean;
  /** Replay transport rate; straight-through comparison audio is 1x-only. */
  playbackSpeed?: number;
  soundtrackId?: string;
  /** Explicit A/B control; adaptive highlight alignment remains the default. */
  scoreMode?: SoundtrackPlaybackMode;
  /** Increments for every explicit seek/step/restart, including forward jumps. */
  transportRevision?: number;
}

/**
 * Presentation-only bridge from the authoritative replay timeline to the audio
 * engine. Loading starts only from the explicit music button, satisfying browser
 * autoplay policy and keeping standalone replay files self-contained.
 */
export function useAdaptiveSoundtrack({
  available,
  replay,
  time,
  playing,
  session,
  presentationId,
  followingLive = false,
  playResolveTail = false,
  playbackSpeed = 1,
  soundtrackId,
  scoreMode = 'adaptive',
  transportRevision = 0,
}: AdaptiveSoundtrackInput): SoundtrackController {
  const [planningGrid, setPlanningGrid] =
    useState<SoundtrackPlanningGrid | null>(null);
  const timeline = useMemo(
    () =>
      buildAdaptiveTimeline(replay, {
        followingLive,
        ...(planningGrid === null
          ? {}
          : {
              planner: {
                ticksPerSecond: REPLAY_TICKS_PER_SECOND,
                bpm: planningGrid.bpm,
                beatsPerBar: planningGrid.beatsPerBar,
              },
            }),
      }),
    [followingLive, planningGrid, replay],
  );
  const frame = useMemo(
    () => sampleAdaptiveTimeline(timeline, time),
    [timeline, time],
  );
  const replayPresentationId = useMemo(
    () =>
      soundtrackPresentationId(
        replay,
        soundtrackId,
        presentationId,
      ),
    [presentationId, replay, soundtrackId],
  );
  const engineRef = useRef<SoundtrackEngine | null>(null);
  const readyEngineRef = useRef<SoundtrackEngine | null>(null);
  const abortRef = useRef<AbortController | null>(null);
  const activationRef = useRef(0);
  const transportRef = useRef(0);
  const faultedRef = useRef(false);
  const soundtrackIdRef = useRef(soundtrackId);
  const scoreModeRef = useRef(scoreMode);
  const triggerCursorRef = useRef(createSoundtrackTriggerCursor());
  const latestRef = useRef({
    replay,
    time,
    followingLive,
    frame,
    timeline,
    replayPresentationId,
    transportRevision,
    playing,
    playResolveTail,
    playbackSpeed,
    scoreMode,
    volume: DEFAULT_VOLUME,
  });
  const [status, setStatus] = useState<SoundtrackStatus>(
    available ? 'off' : 'unavailable',
  );
  const [enabled, setEnabled] = useState(false);
  const [title, setTitle] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [volume, setVolumeState] = useState(readStoredVolume);
  latestRef.current = {
    replay,
    time,
    followingLive,
    frame,
    timeline,
    replayPresentationId,
    transportRevision,
    playing,
    playResolveTail,
    playbackSpeed,
    scoreMode,
    volume,
  };

  const disable = useCallback(() => {
    activationRef.current += 1;
    transportRef.current += 1;
    faultedRef.current = false;
    abortRef.current?.abort();
    abortRef.current = null;
    const engine = engineRef.current;
    engineRef.current = null;
    readyEngineRef.current = null;
    triggerCursorRef.current = createSoundtrackTriggerCursor();
    setPlanningGrid(null);
    if (engine) void engine.dispose();
    setEnabled(false);
    setTitle(null);
    setError(null);
    setStatus(available ? 'off' : 'unavailable');
  }, [available]);

  const enable = useCallback(() => {
    if (!available || engineRef.current || abortRef.current) return;
    faultedRef.current = false;
    const serial = ++activationRef.current;
    let graph: ArenaAudioGraph;
    try {
      // Create and resume synchronously from the click gesture; downloads may
      // then take as long as needed without losing autoplay permission.
      graph = session.ensureGraph();
    } catch (reason: unknown) {
      setEnabled(false);
      setStatus('error');
      setError(
        reason instanceof Error
          ? reason.message
          : 'This browser could not create an audio player.',
      );
      return;
    }
    const resume = session.resume();
    const abort = new AbortController();
    abortRef.current = abort;
    setEnabled(true);
    setStatus('loading');
    setError(null);

    const publicBase = new URL(import.meta.env.BASE_URL, document.baseURI);
    const catalogUrl = new URL('soundtracks/index.json', publicBase);
    void Promise.all([
      loadSoundtrack(catalogUrl, soundtrackId, abort.signal),
      resume,
    ])
      .then(async ([loaded]) => {
        if (serial !== activationRef.current) return;
        const loadedPlanningGrid = {
          bpm: loaded.manifest.bpm,
          beatsPerBar: loaded.manifest.beatsPerBar,
        };
        setPlanningGrid(loadedPlanningGrid);
        const alignToLoadedGrid = (
          snapshot: typeof latestRef.current,
        ): typeof latestRef.current => {
          const alignedTimeline = buildAdaptiveTimeline(snapshot.replay, {
            followingLive: snapshot.followingLive,
            planner: {
              ticksPerSecond: REPLAY_TICKS_PER_SECOND,
              bpm: loadedPlanningGrid.bpm,
              beatsPerBar: loadedPlanningGrid.beatsPerBar,
            },
          });
          return {
            ...snapshot,
            timeline: alignedTimeline,
            frame: sampleAdaptiveTimeline(alignedTimeline, snapshot.time),
          };
        };
        const engine = new SoundtrackEngine(
          loaded,
          graph.context,
          (reason) => {
            if (
              serial !== activationRef.current ||
              engineRef.current !== engine
            ) {
              return;
            }
            faultedRef.current = true;
            engineRef.current = null;
            readyEngineRef.current = null;
            void engine.dispose();
            setEnabled(false);
            setTitle(null);
            setStatus('error');
            setError(reason.message);
          },
          graph.music,
        );
        if (abortRef.current === abort) abortRef.current = null;
        engineRef.current = engine;
        const current = alignToLoadedGrid(latestRef.current);
        engine.setVolume(current.volume);
        resetSoundtrackTriggerCursor(
          triggerCursorRef.current,
          current.replayPresentationId,
          current.transportRevision,
          current.frame.sourceTick,
        );
        const primaryHighlight =
          current.scoreMode === 'adaptive' &&
          current.timeline.mode === 'retrospective'
            ? current.timeline.highlights.find(
                (highlight) => highlight.primary,
              )
            : undefined;
        await engine.start(
          {
            state: current.frame.state,
            intensity: current.frame.intensity,
            targetIntensity: current.frame.targetIntensity,
            momentum: current.frame.momentum,
          },
          current.scoreMode === 'straight'
            ? {
                straightThrough: {
                  getReplaySeconds: () =>
                    latestRef.current.time /
                    REPLAY_TICKS_PER_SECOND,
                  getPauseReason: () =>
                    straightThroughPauseReason(latestRef.current),
                },
              }
            : primaryHighlight
            ? {
                retrospective: {
                  primaryPeakSeconds:
                    primaryHighlight.peakTick /
                    REPLAY_TICKS_PER_SECOND,
                  getReplaySeconds: () =>
                    latestRef.current.time /
                    REPLAY_TICKS_PER_SECOND,
                },
              }
            : {},
        );
        if (
          serial !== activationRef.current ||
          faultedRef.current ||
          engineRef.current !== engine
        ) {
          await engine.dispose();
          return;
        }
        const latest = alignToLoadedGrid(latestRef.current);
        const startupDiscontinuity =
          latest.replayPresentationId !== current.replayPresentationId ||
          latest.transportRevision !== current.transportRevision ||
          latest.frame.sourceTick < current.frame.sourceTick;
        if (startupDiscontinuity) engine.resetForDiscontinuity();
        resetSoundtrackTriggerCursor(
          triggerCursorRef.current,
          latest.replayPresentationId,
          latest.transportRevision,
          latest.frame.sourceTick,
        );
        readyEngineRef.current = engine;
        if (latest.scoreMode === 'adaptive') {
          engine.setDirection({
            state: latest.frame.state,
            intensity: latest.frame.intensity,
            targetIntensity: latest.frame.targetIntensity,
            momentum: latest.frame.momentum,
          });
        }
        const straightPauseReason = straightThroughPauseReason(latest);
        const shouldPause = shouldPauseScore(latest);
        await engine.setPaused(
          shouldPause,
          straightPauseReason ?? 'manual',
        );
        if (
          serial !== activationRef.current ||
          engineRef.current !== engine ||
          faultedRef.current
        ) {
          return;
        }
        setTitle(engine.title);
        setStatus(shouldPause ? 'paused' : 'playing');
      })
      .catch((reason: unknown) => {
        if (serial !== activationRef.current) return;
        if (abortRef.current === abort) abortRef.current = null;
        const engine = engineRef.current;
        engineRef.current = null;
        readyEngineRef.current = null;
        if (engine) void engine.dispose();
        if (reason instanceof DOMException && reason.name === 'AbortError') return;
        faultedRef.current = true;
        setEnabled(false);
        setStatus('error');
        setError(reason instanceof Error ? reason.message : 'Could not start soundtrack.');
      });
  }, [available, scoreMode, session, soundtrackId]);

  const toggle = useCallback(() => {
    if (enabled) disable();
    else enable();
  }, [disable, enable, enabled]);

  const setVolume = useCallback((value: number) => {
    const normalized = Math.max(0, Math.min(1, value));
    setVolumeState(normalized);
    engineRef.current?.setVolume(normalized);
    try {
      window.localStorage.setItem(VOLUME_KEY, String(normalized));
    } catch {
      // Storage can be unavailable in hardened/private browsing contexts.
    }
  }, []);

  useEffect(() => {
    const engine = readyEngineRef.current;
    if (!engine || faultedRef.current) return;
    const batch = collectCrossedSoundtrackTriggers(
      triggerCursorRef.current,
      timeline,
      replayPresentationId,
      transportRevision,
      frame.sourceTick,
    );
    if (batch.discontinuity) engine.resetForDiscontinuity();
    if (scoreMode === 'straight') return;
    engine.setDirection({
      state: frame.state,
      intensity: frame.intensity,
      targetIntensity: frame.targetIntensity,
      momentum: frame.momentum,
    }, batch.triggers);
  }, [
    frame.intensity,
    frame.momentum,
    frame.sourceTick,
    frame.state,
    frame.targetIntensity,
    replayPresentationId,
    scoreMode,
    timeline,
    transportRevision,
  ]);

  useEffect(() => {
    const engine = readyEngineRef.current;
    if (!engine || faultedRef.current) return;
    const shouldPause = shouldPauseScore({
      frame,
      followingLive,
      playing,
      playResolveTail,
      playbackSpeed,
      scoreMode,
    });
    const straightPauseReason = straightThroughPauseReason({
      followingLive,
      playing,
      playResolveTail,
      playbackSpeed,
      scoreMode,
    });
    const operation = ++transportRef.current;
    void engine
      .setPaused(shouldPause, straightPauseReason ?? 'manual')
      .then(() => {
        if (
          operation === transportRef.current &&
          engineRef.current === engine &&
          !faultedRef.current
        ) {
          setStatus(shouldPause ? 'paused' : 'playing');
        }
      })
      .catch((reason: unknown) => {
        if (
          operation !== transportRef.current ||
          engineRef.current !== engine
        ) {
          return;
        }
        faultedRef.current = true;
        setStatus('error');
        setError(reason instanceof Error ? reason.message : 'Audio playback failed.');
      });
  }, [
    followingLive,
    frame.state,
    playResolveTail,
    playbackSpeed,
    playing,
    scoreMode,
  ]);

  useEffect(() => {
    if (!available) {
      if (
        enabled ||
        engineRef.current !== null ||
        abortRef.current !== null
      ) {
        disable();
      } else {
        setStatus('unavailable');
      }
      return;
    }
    setStatus((current) => (current === 'unavailable' ? 'off' : current));
  }, [available, disable, enabled]);

  useEffect(() => {
    if (soundtrackIdRef.current === soundtrackId) return;
    soundtrackIdRef.current = soundtrackId;
    // Switching packs is explicit and silent until the user enables the new
    // selection, preserving autoplay-policy guarantees.
    disable();
  }, [disable, soundtrackId]);

  useEffect(() => {
    if (scoreModeRef.current === scoreMode) return;
    scoreModeRef.current = scoreMode;
    // A/B mode changes replace the playback graph and require a fresh gesture.
    disable();
  }, [disable, scoreMode]);

  useEffect(
    () => () => {
      activationRef.current += 1;
      abortRef.current?.abort();
      const engine = engineRef.current;
      engineRef.current = null;
      readyEngineRef.current = null;
      if (engine) void engine.dispose();
    },
    [],
  );

  return {
    status: available ? status : 'unavailable',
    enabled,
    title,
    volume,
    error,
    toggle,
    setVolume,
  };
}

function shouldPauseScore({
  frame,
  followingLive,
  playing,
  playResolveTail,
  playbackSpeed,
  scoreMode,
}: {
  frame: { state: string };
  followingLive: boolean;
  playing: boolean;
  playResolveTail: boolean;
  playbackSpeed: number;
  scoreMode: SoundtrackPlaybackMode;
}): boolean {
  if (scoreMode === 'straight') {
    return (
      straightThroughPauseReason({
        followingLive,
        playing,
        playResolveTail,
        playbackSpeed,
        scoreMode,
      }) !== null
    );
  }
  return !playing && !(playResolveTail && frame.state === 'resolve');
}

function straightThroughPauseReason({
  followingLive,
  playing,
  playResolveTail,
  playbackSpeed,
  scoreMode,
}: {
  followingLive: boolean;
  playing: boolean;
  playResolveTail: boolean;
  playbackSpeed: number;
  scoreMode: SoundtrackPlaybackMode;
}): StraightThroughPauseReason | null {
  return straightThroughPauseReasonForTransport({
    enabled: scoreMode === 'straight',
    followingLive,
    playing,
    playResolveTail,
    playbackSpeed,
  });
}

function readStoredVolume(): number {
  try {
    const stored = Number(window.localStorage.getItem(VOLUME_KEY));
    return Number.isFinite(stored) && stored >= 0 && stored <= 1
      ? stored
      : DEFAULT_VOLUME;
  } catch {
    return DEFAULT_VOLUME;
  }
}
