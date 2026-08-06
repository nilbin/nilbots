import { useEffect, useRef, useState, useCallback } from 'react';
import type { ReplayModel } from './replayModel';
import {
  createArenaFramePacer,
  takeArenaFrame,
} from './render/arenaRenderProfile';

export interface PlaybackState {
  /** Continuous playhead: floor(t) is the tick being animated, frac(t) its progress. */
  time: number;
  playing: boolean;
  /** True only after playback, including a live handoff, reaches the revealed end. */
  endedNaturally: boolean;
  /** Increments for every explicit seek, step, restart, or play-from-end. */
  transportRevision: number;
  speed: number;
  tickCount: number;
  tick: number;
  atEnd: boolean;
  play: () => void;
  pause: () => void;
  toggle: () => void;
  restart: () => void;
  step: (delta: number) => void;
  seek: (tick: number) => void;
  setSpeed: (speed: number) => void;
}

/**
 * Presentation timeline, decoupled from simulation.
 *
 * The former five-tick cadence made a sixteen-body Arc Relay match legible to a probe
 * but not to a person. 1x is now deliberately cinematic: every authoritative tick gets
 * 400ms of screen time, while the speed control still offers faster review.
 */
export const PRESENTATION_TICKS_PER_SECOND = 2.5;

/**
 * Every mode starts at 1x (owner ruling — the half-speed Arc Relay first watch read as
 * sluggish); the speed control still offers 0.5x for a slow read.
 */
export function defaultPlaybackSpeed(replay: ReplayModel): number {
  void replay;
  return 1;
}

/**
 * @param ready Hold at tick 0 until the arena's assets have arrived. Without this the
 * clock runs behind a loading screen, and the match is already underway when it lifts.
 * @param active False while the server's live clock owns presentation time.
 * @param autoStart Whether the clock runs as soon as it is allowed to.
 * @param framesPerSecond Optional React update cap. Elapsed wall time remains authoritative.
 *
 * `autoStart` is false wherever a person is watching and a play button is offered: waiting
 * for assets and then starting anyway is the same missed opening as not waiting at all,
 * and only a real press can carry the audio activation a browser demands. It stays true
 * for an embedding host, which draws its own transport and asks for playback over the
 * bridge — a host that never sent `play` would otherwise sit on a still frame forever.
 */
export function usePlayback(
  replay: ReplayModel,
  ready = true,
  active = true,
  autoStart = true,
  framesPerSecond?: number,
): PlaybackState {
  const tickCount = replay.ticks.length;
  const [time, setTime] = useState(0);
  const [playing, setPlaying] = useState(active && autoStart);
  const [endedNaturally, setEndedNaturally] = useState(false);
  const [transportRevision, setTransportRevision] = useState(0);
  const [speed, setSpeed] = useState(() => defaultPlaybackSpeed(replay));
  const timeRef = useRef(0);
  const frame = useRef<number>(0);
  const lastStamp = useRef<number | null>(null);
  const framePacer = useRef(createArenaFramePacer());
  const wasActive = useRef(active);

  useEffect(() => {
    if (!active) {
      setPlaying(false);
      timeRef.current = tickCount;
      setTime(tickCount);
    } else if (!wasActive.current) {
      // A completed live broadcast hands its already-revealed end to the local
      // transport. Preserve that position and treat it like a natural finish,
      // without inventing a seek revision that would suppress the resolve cue.
      setPlaying(false);
      timeRef.current = tickCount;
      setTime(tickCount);
      setEndedNaturally(true);
    } else if (endedNaturally && timeRef.current < tickCount) {
      // The live clock and replay document are independent requests. Its final
      // full-document fetch can land after the handoff; follow that newly
      // revealed end instead of leaving the playhead on a stale partial prefix.
      timeRef.current = tickCount;
      setTime(tickCount);
    }
    wasActive.current = active;
  }, [active, endedNaturally, tickCount]);

  useEffect(() => {
    if (!active || !playing || !ready) {
      lastStamp.current = null;
      framePacer.current = createArenaFramePacer();
      return;
    }
    const advance = (stamp: number) => {
      if (
        framesPerSecond !== undefined &&
        !takeArenaFrame(framePacer.current, stamp, framesPerSecond)
      ) {
        frame.current = requestAnimationFrame(advance);
        return;
      }
      const dt = lastStamp.current === null ? 0 : (stamp - lastStamp.current) / 1000;
      lastStamp.current = stamp;
      setTime((current) => {
        const next = current + dt * PRESENTATION_TICKS_PER_SECOND * speed;
        if (next >= tickCount) {
          setPlaying(false);
          setEndedNaturally(true);
          timeRef.current = tickCount;
          return tickCount;
        }
        timeRef.current = next;
        return next;
      });
      frame.current = requestAnimationFrame(advance);
    };
    frame.current = requestAnimationFrame(advance);
    return () => cancelAnimationFrame(frame.current);
  }, [active, playing, ready, speed, tickCount, framesPerSecond]);

  const pause = useCallback(() => setPlaying(false), []);
  const play = useCallback(() => {
    setEndedNaturally(false);
    if (timeRef.current >= tickCount) {
      timeRef.current = 0;
      setTime(0);
      setTransportRevision((revision) => revision + 1);
    }
    setPlaying(true);
  }, [tickCount]);

  const completedLiveHandoff = active && !wasActive.current;
  const presentedTime = completedLiveHandoff ? tickCount : time;
  const tick = Math.max(
    0,
    Math.min(Math.floor(presentedTime), tickCount - 1),
  );
  return {
    time: presentedTime,
    playing: active && playing,
    endedNaturally: endedNaturally || completedLiveHandoff,
    transportRevision,
    speed,
    tickCount,
    tick,
    atEnd: presentedTime >= tickCount,
    play,
    pause,
    toggle: playing ? pause : play,
    restart: useCallback(() => {
      setEndedNaturally(false);
      timeRef.current = 0;
      setTime(0);
      setTransportRevision((revision) => revision + 1);
      setPlaying(true);
    }, []),
    step: useCallback(
      (delta: number) => {
        setPlaying(false);
        setEndedNaturally(false);
        const next = Math.max(
          0,
          Math.min(tickCount, Math.floor(timeRef.current) + delta),
        );
        timeRef.current = next;
        setTime(next);
        setTransportRevision((revision) => revision + 1);
      },
      [tickCount],
    ),
    seek: useCallback(
      (value: number) => {
        setPlaying(false);
        setEndedNaturally(false);
        const next = Math.max(0, Math.min(tickCount, value));
        timeRef.current = next;
        setTime(next);
        setTransportRevision((revision) => revision + 1);
      },
      [tickCount],
    ),
    setSpeed,
  };
}

export interface LiveFollow {
  /** The shared presentation tick from the server clock. */
  tick: number;
  ticksPerSecond: number;
}

/// Follows the server's presentation clock: re-anchors on every update from the
/// server and advances smoothly between polls, never running past received ticks.
export function useLiveFollower(
  replay: ReplayModel,
  live?: LiveFollow,
  framesPerSecond?: number,
): number {
  const [time, setTime] = useState(0);
  const anchor = useRef<{ tick: number; at: number } | null>(null);

  useEffect(() => {
    if (!live) return;
    anchor.current = { tick: Math.max(0, live.tick), at: performance.now() };
    const framePacer = createArenaFramePacer();
    let frame = 0;
    const advance = (stamp: number) => {
      if (
        framesPerSecond !== undefined &&
        !takeArenaFrame(framePacer, stamp, framesPerSecond)
      ) {
        frame = requestAnimationFrame(advance);
        return;
      }
      const a = anchor.current!;
      const elapsed = (stamp - a.at) / 1000;
      setTime(Math.min(a.tick + elapsed * live.ticksPerSecond, replay.ticks.length));
      frame = requestAnimationFrame(advance);
    };
    frame = requestAnimationFrame(advance);
    return () => cancelAnimationFrame(frame);
  }, [live?.tick, live?.ticksPerSecond, live, replay.ticks.length, framesPerSecond]);

  return time;
}
