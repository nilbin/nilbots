import { useCallback, useEffect, useRef, useState } from 'react';
import type { ReplayModel } from '../replayModel';
import {
  audioCandidate,
  audioCandidates,
  type AudioCandidateId,
  type AudioCueId,
} from './audioCandidates';
import { ArenaAudioSession } from './ArenaAudioSession';
import { createArenaImpulse, ROOM_MIX } from './arenaRoom';
import { replayAudioEventsAt } from './replayAudioEvents';
import { readLocalSetting, writeLocalSetting } from './localSettings';

const BASE_TICKS_PER_SECOND = 5;
const MAX_ACTIVE_VOICES = 8;
const MAX_CROSSED_TICKS = 3;
/**
 * How far cues are allowed to move off centre. Full width puts an edge-of-map shot
 * entirely in one ear, which is disorienting on headphones for a view you are looking
 * straight at; this keeps the arena wide but coherent.
 */
const PAN_WIDTH = 0.7;
const candidateStorageKey = 'nilbots.audio.review.candidate';
const volumeStorageKey = 'nilbots.audio.review.volume';
const muteStorageKey = 'nilbots.audio.review.muted';

const cueGain: Record<AudioCueId, number> = {
  projectile: 0.36,
  impact: 0.48,
  destroyed: 0.56,
  unlock: 0.52,
};
const cueVoiceLimit: Record<AudioCueId, number> = {
  projectile: 3,
  impact: 2,
  destroyed: 1,
  unlock: 1,
};

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
  cue: AudioCueId;
  priority: number;
  startedAt: number;
}

export interface ReplayAudioController {
  candidateId: AudioCandidateId;
  enabled: boolean;
  muted: boolean;
  volume: number;
  suspendedForSpeed: boolean;
  enable: () => Promise<void>;
  setCandidate: (candidate: AudioCandidateId) => void;
  setMuted: (muted: boolean) => void;
  setVolume: (volume: number) => void;
  previewUnlock: () => Promise<void>;
}

export function useReplayAudio({
  replay,
  time,
  playing,
  speed,
  atEnd,
  following,
  reviewEnabled = true,
  session,
}: {
  replay: ReplayModel;
  time: number;
  playing: boolean;
  speed: number;
  atEnd: boolean;
  following: boolean;
  reviewEnabled?: boolean;
  /**
   * Stable, viewer-owned session shared with other arena audio. Omitted only
   * for compatibility with callers that have not yet moved ownership upward.
   */
  session?: ArenaAudioSession;
}): ReplayAudioController {
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
  const [candidateId, setCandidateState] = useState<AudioCandidateId>(
    () => (reviewEnabled ? readCandidate() : 'nilbots-signature'),
  );
  const [volume, setVolumeState] = useState(() =>
    reviewEnabled ? readVolume() : 0.72,
  );
  const [muted, setMutedState] = useState(
    () => reviewEnabled && readLocalSetting(muteStorageKey) === 'true',
  );
  const [enabled, setEnabled] = useState(false);
  const graph = useRef<AudioGraph | null>(null);
  const buffers = useRef(new Map<string, Promise<AudioBuffer>>());
  const voices = useRef<Voice[]>([]);
  /** Panner per source, so a finished voice disconnects the node it created. */
  const panners = useRef(new Map<AudioBufferSourceNode, StereoPannerNode>());
  const generation = useRef(0);
  const previousTick = useRef<number | null>(null);
  const candidateRef = useRef(candidateId);
  const currentTick = Math.max(
    0,
    Math.min(Math.floor(time), replay.ticks.length - 1),
  );
  const currentTickRef = useRef(currentTick);
  currentTickRef.current = currentTick;
  candidateRef.current = candidateId;

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
    (selected: AudioCandidateId, cue: AudioCueId): Promise<AudioBuffer> => {
      const key = `${selected}/${cue}`;
      const cached = buffers.current.get(key);
      if (cached) return cached;
      const { context } = ensureGraph();
      const url = audioCandidate(selected).cues[cue];
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

  const preloadCandidate = useCallback(
    (selected: AudioCandidateId) =>
      Promise.all(
        (['projectile', 'impact', 'destroyed', 'unlock'] as const).map((cue) =>
          loadCue(selected, cue),
        ),
      ),
    [loadCue],
  );

  const scheduleCue = useCallback(
    (
      cue: AudioCueId,
      delayMilliseconds: number,
      priority: number,
      pan: number | null = null,
      expectedGeneration = generation.current,
    ) => {
      const selected = candidateRef.current;
      const requestedAt = performance.now();
      void loadCue(selected, cue).then((buffer) => {
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

  const enable = useCallback(async () => {
    const current = ensureGraph();
    await audioSession.resume();
    current.master.gain.setValueAtTime(
      muted ? 0 : volume,
      current.context.currentTime,
    );
    setEnabled(true);
    previousTick.current = currentTickRef.current;
    await preloadCandidate(candidateRef.current);
  }, [audioSession, ensureGraph, muted, preloadCandidate, volume]);

  const previewUnlock = useCallback(async () => {
    await enable();
    scheduleCue('unlock', 0, 3);
  }, [enable, scheduleCue]);

  const setCandidate = useCallback(
    (selected: AudioCandidateId) => {
      setCandidateState(selected);
      writeLocalSetting(candidateStorageKey, selected);
      stopAll();
      previousTick.current = currentTickRef.current;
      if (enabled) void preloadCandidate(selected);
    },
    [enabled, preloadCandidate, stopAll],
  );

  const setVolume = useCallback((next: number) => {
    const clamped = Math.max(0, Math.min(1, next));
    setVolumeState(clamped);
    writeLocalSetting(volumeStorageKey, String(clamped));
  }, []);

  const setMuted = useCallback((next: boolean) => {
    setMutedState(next);
    writeLocalSetting(muteStorageKey, String(next));
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
    if (!reviewEnabled || !enabled) {
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
    reviewEnabled,
    scheduleCue,
    speed,
    stopAll,
    time,
  ]);

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
    candidateId,
    enabled,
    muted,
    volume,
    suspendedForSpeed: speed > 2,
    enable,
    setCandidate,
    setMuted,
    setVolume,
    previewUnlock,
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

function readCandidate(): AudioCandidateId {
  const stored = readLocalSetting(candidateStorageKey);
  const match = audioCandidates.find((candidate) => candidate.id === stored);
  return match?.id ?? 'nilbots-signature';
}

function readVolume(): number {
  const value = readLocalSetting(volumeStorageKey);
  if (value === null) return 0.72;
  const stored = Number(value);
  return Number.isFinite(stored) && stored >= 0 && stored <= 1 ? stored : 0.72;
}
