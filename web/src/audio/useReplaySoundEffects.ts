import { useCallback, useEffect, useRef, useState } from 'react';
import type { ReplayModel } from '../replayModel';
import {
  soundEffectPack,
  type SoundEffectCueId,
} from './soundEffects';
import { ArenaAudioSession } from './ArenaAudioSession';
import { beginAsset } from '../render/assetReadiness';
import { createArenaImpulse, ROOM_MIX } from './arenaRoom';
import { replayAudioEventsAt } from './replayAudioEvents';
import {
  readSoundEffectsMutedPreference,
  readSoundEffectsVolumePreference,
  writeSoundEffectsMutedPreference,
  writeSoundEffectsVolumePreference,
} from './soundEffectsPreferences';

const BASE_TICKS_PER_SECOND = 5;
const MAX_ACTIVE_VOICES = 8;
const MAX_CROSSED_TICKS = 3;
/**
 * How far cues are allowed to move off centre. Full width puts an edge-of-map shot
 * entirely in one ear, which is disorienting on headphones for a view you are looking
 * straight at; this keeps the arena wide but coherent.
 */
const PAN_WIDTH = 0.7;

const cueGain: Record<SoundEffectCueId, number> = {
  projectile: 0.36,
  impact: 0.48,
  destroyed: 0.56,
};
const cueVoiceLimit: Record<SoundEffectCueId, number> = {
  projectile: 3,
  impact: 2,
  destroyed: 1,
};

/** The cues a match cannot start without. Same set `preloadEffects` decodes. */
const PRELOADED_CUES = ['projectile', 'impact', 'destroyed'] as const;

interface AudioGraph {
  context: AudioContext;
  master: GainNode;
  /**
   * The reverb send. Cues connect here *in addition to* the master, so the dry signal
   * keeps its timing and the room sits behind it — a cue routed only through the
   * convolver would arrive smeared and late.
   */
  room: GainNode | null;
  convolver: ConvolverNode | null;
}

interface Voice {
  source: AudioBufferSourceNode;
  cue: SoundEffectCueId;
  priority: number;
  startedAt: number;
}

export interface ReplaySoundEffectsController {
  packLabel: string;
  enabled: boolean;
  activating: boolean;
  muted: boolean;
  volume: number;
  error: string | null;
  suspendedForSpeed: boolean;
  enable: () => Promise<void>;
  setMuted: (muted: boolean) => void;
  setVolume: (volume: number) => void;
}

export function useReplaySoundEffects({
  replay,
  time,
  playing,
  speed,
  atEnd,
  following,
  available = true,
  activationGranted = false,
  session,
}: {
  replay: ReplayModel;
  time: number;
  playing: boolean;
  speed: number;
  atEnd: boolean;
  following: boolean;
  available?: boolean;
  /** The shared arena audio session has resumed from a trusted interaction. */
  activationGranted?: boolean;
  /**
   * Stable, viewer-owned session shared with other arena audio. Omitted only
   * for compatibility with callers that have not yet moved ownership upward.
   */
  session?: ArenaAudioSession;
}): ReplaySoundEffectsController {
  const fallbackSession = useRef<ArenaAudioSession | null>(null);
  if (!session && fallbackSession.current === null) {
    fallbackSession.current = new ArenaAudioSession();
  }
  const audioSession = session ?? fallbackSession.current!;
  const ownsSession = session === undefined;
  useEffect(
    () => (ownsSession ? audioSession.retainOwner() : undefined),
    [audioSession, ownsSession],
  );
  // Warm the cue bytes while the arena is still building, and hold the loading gate open
  // until they land.
  //
  // Decoding cannot happen here: `decodeAudioData` needs the graph, the graph needs a
  // resumed context, and a context resumes only from a trusted gesture — which is the play
  // button itself. Gating that button on a decode would be a deadlock. Fetching is not
  // gesture-bound, though, so the bytes can be in the HTTP cache before the click, and the
  // decode `enable()` does afterwards is then local work rather than a network round trip.
  // That is the difference between a match whose first shot is silent and one that is not.
  useEffect(() => {
    if (!available || typeof fetch !== 'function') return;
    let cancelled = false;
    const release = beginAsset();
    void Promise.all(
      PRELOADED_CUES.map((cue) =>
        // A cue that will not download is not a reason to hold the viewer shut; the
        // arena plays silent and the control says so.
        fetch(soundEffectPack.cues[cue]).catch(() => null),
      ),
    ).finally(() => {
      if (!cancelled) release();
    });
    return () => {
      cancelled = true;
      release();
    };
  }, [available]);

  const [volume, setVolumeState] = useState(
    readSoundEffectsVolumePreference,
  );
  const [muted, setMutedState] = useState(
    readSoundEffectsMutedPreference,
  );
  const [enabled, setEnabled] = useState(false);
  const [activating, setActivating] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const graph = useRef<AudioGraph | null>(null);
  const buffers = useRef(new Map<string, Promise<AudioBuffer>>());
  const voices = useRef<Voice[]>([]);
  /** Panner per source, so a finished voice disconnects the node it created. */
  const panners = useRef(new Map<AudioBufferSourceNode, StereoPannerNode>());
  const generation = useRef(0);
  const previousTick = useRef<number | null>(null);
  const activation = useRef<Promise<void> | null>(null);
  const enabledRef = useRef(enabled);
  const currentTick = Math.max(
    0,
    Math.min(Math.floor(time), replay.ticks.length - 1),
  );
  const currentTickRef = useRef(currentTick);
  currentTickRef.current = currentTick;
  enabledRef.current = enabled;

  const ensureGraph = useCallback((): AudioGraph => {
    if (graph.current && graph.current.context.state !== 'closed') {
      return graph.current;
    }
    const { context, effects } = audioSession.ensureGraph();
    const master = context.createGain();
    master.connect(effects);

    // The room. Optional on purpose: ConvolverNode is universally supported but building
    // the response allocates a couple of seconds of stereo float, and a browser that
    // refuses for any reason should lose the reverb rather than the audio.
    let room: GainNode | null = null;
    let convolver: ConvolverNode | null = null;
    try {
      convolver = context.createConvolver();
      convolver.buffer = createArenaImpulse(context);
      room = context.createGain();
      room.gain.value = ROOM_MIX;
      // Into master, not the limiter, so the volume control governs wet and dry together
      // and muting actually mutes.
      room.connect(convolver).connect(master);
    } catch {
      room?.disconnect();
      convolver?.disconnect();
      room = null;
      convolver = null;
    }

    graph.current = { context, master, room, convolver };
    return graph.current;
  }, [audioSession]);

  const stopAll = useCallback(() => {
    generation.current++;
    for (const voice of voices.current) {
      try {
        voice.source.stop();
      } catch {
        // A source that ended between the read and stop is already harmless.
      }
    }
    voices.current = [];
  }, []);

  const loadCue = useCallback(
    (cue: SoundEffectCueId): Promise<AudioBuffer> => {
      const key = `${soundEffectPack.id}/${cue}`;
      const cached = buffers.current.get(key);
      if (cached) return cached;
      const { context } = ensureGraph();
      const url = soundEffectPack.cues[cue];
      const loading = fetch(url)
        .then((response) => {
          if (!response.ok) throw new Error(`Audio asset returned ${response.status}.`);
          return response.arrayBuffer();
        })
        .then((bytes) => context.decodeAudioData(bytes));
      buffers.current.set(key, loading);
      loading.catch(() => buffers.current.delete(key));
      return loading;
    },
    [ensureGraph],
  );

  const preloadEffects = useCallback(
    () =>
      Promise.all(
        (['projectile', 'impact', 'destroyed'] as const).map((cue) =>
          loadCue(cue),
        ),
      ),
    [loadCue],
  );

  const scheduleCue = useCallback(
    (
      cue: SoundEffectCueId,
      delayMilliseconds: number,
      priority: number,
      pan: number | null = null,
      expectedGeneration = generation.current,
    ) => {
      const requestedAt = performance.now();
      void loadCue(cue).then((buffer) => {
        if (
          expectedGeneration !== generation.current ||
          !graph.current ||
          graph.current.context.state !== 'running'
        ) {
          return;
        }
        const activeForCue = voices.current
          .filter((voice) => voice.cue === cue)
          .sort((left, right) => left.startedAt - right.startedAt);
        while (activeForCue.length >= cueVoiceLimit[cue]) {
          stopVoice(activeForCue.shift()!);
        }
        while (voices.current.length >= MAX_ACTIVE_VOICES) {
          const lowest = [...voices.current].sort(
            (left, right) =>
              left.priority - right.priority || left.startedAt - right.startedAt,
          )[0];
          stopVoice(lowest);
        }

        const { context, master, room } = graph.current;
        const source = context.createBufferSource();
        const gain = context.createGain();
        source.buffer = buffer;
        gain.gain.value = cueGain[cue];

        // Place the cue across the arena. Stereo panning rather than a 3D panner: this is
        // a flat plan view heard on a phone speaker or laptop, where left/right is the
        // only axis a listener can actually resolve, and StereoPannerNode costs far less.
        // Centre-panned cues skip the node entirely rather than routing through a no-op.
        if (pan !== null && pan !== 0 && typeof context.createStereoPanner === 'function') {
          const panner = context.createStereoPanner();
          panner.pan.value = Math.max(-1, Math.min(1, pan)) * PAN_WIDTH;
          source.connect(gain).connect(panner).connect(master);
          // Sent post-pan so the tail inherits the cue's position rather than collapsing
          // every reflection to the centre.
          if (room) panner.connect(room);
          panners.current.set(source, panner);
        } else {
          source.connect(gain).connect(master);
          if (room) gain.connect(room);
        }
        const voice: Voice = {
          source,
          cue,
          priority,
          startedAt: context.currentTime,
        };
        source.addEventListener(
          'ended',
          () => {
            voices.current = voices.current.filter(
              (candidate) => candidate !== voice,
            );
            source.disconnect();
            gain.disconnect();
            panners.current.get(source)?.disconnect();
            panners.current.delete(source);
          },
          { once: true },
        );
        voices.current.push(voice);
        const elapsed = performance.now() - requestedAt;
        source.start(
          context.currentTime +
            Math.max(0, delayMilliseconds - elapsed) / 1_000,
        );
      });
    },
    [loadCue],
  );

  const enable = useCallback((): Promise<void> => {
    if (!available || enabledRef.current) return Promise.resolve();
    if (activation.current) return activation.current;

    setActivating(true);
    setError(null);
    let operation: Promise<void>;
    operation = (async () => {
      const current = ensureGraph();
      await audioSession.resume();
      current.master.gain.setValueAtTime(
        muted ? 0 : volume,
        current.context.currentTime,
      );
      await preloadEffects();
      previousTick.current = currentTickRef.current;
      enabledRef.current = true;
      setEnabled(true);
    })()
      .catch((reason: unknown) => {
        setError(
          reason instanceof Error
            ? reason.message
            : 'Sound effects could not start in this browser.',
        );
        throw reason;
      })
      .finally(() => {
        if (activation.current === operation) activation.current = null;
        setActivating(false);
      });
    activation.current = operation;
    return operation;
  }, [
    audioSession,
    available,
    ensureGraph,
    muted,
    preloadEffects,
    volume,
  ]);

  const setVolume = useCallback((next: number) => {
    const clamped = Math.max(0, Math.min(1, next));
    setVolumeState(clamped);
    writeSoundEffectsVolumePreference(clamped);
  }, []);

  const setMuted = useCallback((next: boolean) => {
    setMutedState(next);
    writeSoundEffectsMutedPreference(next);
  }, []);

  useEffect(() => {
    if (!graph.current) return;
    graph.current.master.gain.setTargetAtTime(
      muted ? 0 : volume,
      graph.current.context.currentTime,
      0.015,
    );
  }, [muted, volume]);

  useEffect(() => {
    if (!available || !enabled) {
      previousTick.current = currentTick;
      return;
    }
    if (document.hidden) {
      stopAll();
      previousTick.current = currentTick;
      return;
    }

    const active = following || playing;
    if (!active) {
      if (!atEnd) stopAll();
      previousTick.current = currentTick;
      return;
    }
    if (speed > 2) {
      if (previousTick.current !== currentTick || voices.current.length > 0) {
        stopAll();
      }
      previousTick.current = currentTick;
      return;
    }

    const previous = previousTick.current;
    if (previous === null) {
      previousTick.current = currentTick;
      return;
    }
    if (currentTick === previous) return;

    const fraction = Math.max(0, time - currentTick);
    if (currentTick < previous) {
      stopAll();
      previousTick.current = currentTick;
      if (currentTick !== 0) return;
      scheduleTick(currentTick, fraction);
      return;
    }
    if (currentTick - previous > MAX_CROSSED_TICKS) {
      stopAll();
      previousTick.current = currentTick;
      return;
    }
    for (let tick = previous + 1; tick <= currentTick; tick++) {
      scheduleTick(tick, tick === currentTick ? fraction : 1);
    }
    previousTick.current = currentTick;

    function scheduleTick(tick: number, elapsedTicks: number) {
      const tickDuration = 1_000 / (BASE_TICKS_PER_SECOND * speed);
      const expectedGeneration = generation.current;
      for (const event of replayAudioEventsAt(replay, tick)) {
        scheduleCue(
          event.cue,
          Math.max(0, event.tickOffset - elapsedTicks) * tickDuration,
          event.priority,
          event.pan,
          expectedGeneration,
        );
      }
    }
  }, [
    atEnd,
    currentTick,
    enabled,
    following,
    playing,
    replay,
    available,
    scheduleCue,
    speed,
    stopAll,
    time,
  ]);

  useEffect(() => {
    if (!activationGranted || !available || enabled) return;
    void enable().catch(() => {
      // The compact control exposes the error and gives the user an explicit retry.
    });
  }, [activationGranted, available, enable, enabled]);

  useEffect(() => {
    const onVisibility = () => {
      if (document.hidden) stopAll();
    };
    document.addEventListener('visibilitychange', onVisibility);
    return () => document.removeEventListener('visibilitychange', onVisibility);
  }, [stopAll]);

  useEffect(
    () => () => {
      stopAll();
      const current = graph.current;
      graph.current = null;
      buffers.current.clear();
      current?.room?.disconnect();
      current?.convolver?.disconnect();
      current?.master.disconnect();
    },
    [audioSession, stopAll],
  );

  return {
    packLabel: soundEffectPack.label,
    enabled,
    activating,
    muted,
    volume,
    error,
    suspendedForSpeed: speed > 2,
    enable,
    setMuted,
    setVolume,
  };

  function stopVoice(voice: Voice) {
    voices.current = voices.current.filter((candidate) => candidate !== voice);
    try {
      voice.source.stop();
    } catch {
      // The voice ended while being evicted.
    }
  }
}
