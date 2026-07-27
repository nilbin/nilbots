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
import { SoundtrackEngine } from './SoundtrackEngine.ts';
import { loadSoundtrack } from './manifest.ts';
import type { SoundtrackController } from './SoundtrackControl';
import {
  collectCrossedSoundtrackTriggers,
  createSoundtrackTriggerCursor,
  resetSoundtrackTriggerCursor,
  soundtrackPresentationId,
} from './transport';
import type { SoundtrackStatus } from './types';

const VOLUME_KEY = 'nilbots.soundtrack.volume.v1';
const DEFAULT_VOLUME = 0.62;

export interface AdaptiveSoundtrackInput {
  available: boolean;
  replay: ReplayModel;
  time: number;
  playing: boolean;
  session: ArenaAudioSession;
  /** Stable across live-prefix updates and the completed replay handoff. */
  presentationId?: string;
  /** Allow a finite resolution cue after transport reaches the end on its own. */
  playResolveTail?: boolean;
  soundtrackId?: string;
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
  playResolveTail = false,
  soundtrackId,
  transportRevision = 0,
}: AdaptiveSoundtrackInput): SoundtrackController {
  const timeline = useMemo(() => buildAdaptiveTimeline(replay), [replay]);
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
  const triggerCursorRef = useRef(createSoundtrackTriggerCursor());
  const latestRef = useRef({
    frame,
    timeline,
    replayPresentationId,
    transportRevision,
    playing,
    playResolveTail,
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
    frame,
    timeline,
    replayPresentationId,
    transportRevision,
    playing,
    playResolveTail,
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
        const current = latestRef.current;
        engine.setVolume(current.volume);
        resetSoundtrackTriggerCursor(
          triggerCursorRef.current,
          current.replayPresentationId,
          current.transportRevision,
          current.frame.sourceTick,
        );
        await engine.start({
          state: current.frame.state,
          intensity: current.frame.intensity,
          targetIntensity: current.frame.targetIntensity,
          momentum: current.frame.momentum,
        });
        if (
          serial !== activationRef.current ||
          faultedRef.current ||
          engineRef.current !== engine
        ) {
          await engine.dispose();
          return;
        }
        const latest = latestRef.current;
        engine.resetForDiscontinuity();
        resetSoundtrackTriggerCursor(
          triggerCursorRef.current,
          latest.replayPresentationId,
          latest.transportRevision,
          latest.frame.sourceTick,
        );
        readyEngineRef.current = engine;
        engine.setDirection({
          state: latest.frame.state,
          intensity: latest.frame.intensity,
          targetIntensity: latest.frame.targetIntensity,
          momentum: latest.frame.momentum,
        });
        const shouldPause = shouldPauseScore(latest);
        await engine.setPaused(shouldPause);
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
  }, [available, session, soundtrackId]);

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
    timeline,
    transportRevision,
  ]);

  useEffect(() => {
    const engine = readyEngineRef.current;
    if (!engine || faultedRef.current) return;
    const shouldPause = shouldPauseScore({
      frame,
      playing,
      playResolveTail,
    });
    const operation = ++transportRef.current;
    void engine
      .setPaused(shouldPause)
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
  }, [frame.state, playResolveTail, playing]);

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
  playing,
  playResolveTail,
}: {
  frame: { state: string };
  playing: boolean;
  playResolveTail: boolean;
}): boolean {
  return !playing && !(playResolveTail && frame.state === 'resolve');
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
