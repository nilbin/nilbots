import { useCallback, useEffect, useRef, useState } from 'react';
import type { ReplayDocument } from '../types';
import {
  audioCandidate,
  audioCandidates,
  type AudioCandidateId,
  type AudioCueId,
} from './audioCandidates';
import { replayAudioEventsAt } from './replayAudioEvents';
import { readLocalSetting, writeLocalSetting } from './localSettings';

const BASE_TICKS_PER_SECOND = 5;
const MAX_ACTIVE_VOICES = 8;
const MAX_CROSSED_TICKS = 3;
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
}: {
  replay: ReplayDocument;
  time: number;
  playing: boolean;
  speed: number;
  atEnd: boolean;
  following: boolean;
  reviewEnabled?: boolean;
}): ReplayAudioController {
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
    const context = new AudioContext({ latencyHint: 'interactive' });
    const master = context.createGain();
    const limiter = context.createDynamicsCompressor();
    limiter.threshold.value = -4;
    limiter.knee.value = 3;
    limiter.ratio.value = 14;
    limiter.attack.value = 0.002;
    limiter.release.value = 0.11;
    master.connect(limiter).connect(context.destination);
    graph.current = { context, master };
    return graph.current;
  }, []);

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

        const { context, master } = graph.current;
        const source = context.createBufferSource();
        const gain = context.createGain();
        source.buffer = buffer;
        gain.gain.value = cueGain[cue];
        source.connect(gain).connect(master);
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
    await current.context.resume();
    current.master.gain.setValueAtTime(
      muted ? 0 : volume,
      current.context.currentTime,
    );
    setEnabled(true);
    previousTick.current = currentTickRef.current;
    await preloadCandidate(candidateRef.current);
  }, [ensureGraph, muted, preloadCandidate, volume]);

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
      const context = graph.current?.context;
      graph.current = null;
      buffers.current.clear();
      if (context && context.state !== 'closed') void context.close();
    },
    [stopAll],
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
