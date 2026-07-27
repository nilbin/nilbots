import { useEffect, useRef, useState, useCallback } from 'react';
import type { ReplayModel } from './replayModel';

export interface PlaybackState {
  /** Continuous playhead: floor(t) is the tick being animated, frac(t) its progress. */
  time: number;
  playing: boolean;
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

/** Presentation timeline (plan §28.1): ~5 ticks/second at 1x, decoupled from simulation. */
const BASE_TICKS_PER_SECOND = 5;

/**
 * @param ready Hold at tick 0 until the arena's images have decoded. Without this the
 * clock runs behind a loading screen, and the match is already underway when it lifts.
 */
export function usePlayback(replay: ReplayModel, ready = true): PlaybackState {
  const tickCount = replay.ticks.length;
  const [time, setTime] = useState(0);
  const [playing, setPlaying] = useState(true);
  const [speed, setSpeed] = useState(1);
  const frame = useRef<number>(0);
  const lastStamp = useRef<number | null>(null);

  useEffect(() => {
    if (!playing || !ready) {
      lastStamp.current = null;
      return;
    }
    const advance = (stamp: number) => {
      const dt = lastStamp.current === null ? 0 : (stamp - lastStamp.current) / 1000;
      lastStamp.current = stamp;
      setTime((current) => {
        const next = current + dt * BASE_TICKS_PER_SECOND * speed;
        if (next >= tickCount) {
          setPlaying(false);
          return tickCount;
        }
        return next;
      });
      frame.current = requestAnimationFrame(advance);
    };
    frame.current = requestAnimationFrame(advance);
    return () => cancelAnimationFrame(frame.current);
  }, [playing, ready, speed, tickCount]);

  const pause = useCallback(() => setPlaying(false), []);
  const play = useCallback(() => {
    setTime((current) => (current >= tickCount ? 0 : current));
    setPlaying(true);
  }, [tickCount]);

  const tick = Math.min(Math.floor(time), tickCount - 1);
  return {
    time,
    playing,
    speed,
    tickCount,
    tick,
    atEnd: time >= tickCount,
    play,
    pause,
    toggle: playing ? pause : play,
    restart: useCallback(() => {
      setTime(0);
      setPlaying(true);
    }, []),
    step: useCallback(
      (delta: number) => {
        setPlaying(false);
        setTime((current) =>
          Math.max(0, Math.min(tickCount, Math.floor(current) + delta)),
        );
      },
      [tickCount],
    ),
    seek: useCallback(
      (value: number) => {
        setPlaying(false);
        setTime(Math.max(0, Math.min(tickCount, value)));
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
export function useLiveFollower(replay: ReplayModel, live?: LiveFollow): number {
  const [time, setTime] = useState(0);
  const anchor = useRef<{ tick: number; at: number } | null>(null);

  useEffect(() => {
    if (!live) return;
    anchor.current = { tick: Math.max(0, live.tick), at: performance.now() };
    let frame = 0;
    const advance = () => {
      const a = anchor.current!;
      const elapsed = (performance.now() - a.at) / 1000;
      setTime(Math.min(a.tick + elapsed * live.ticksPerSecond, replay.ticks.length));
      frame = requestAnimationFrame(advance);
    };
    frame = requestAnimationFrame(advance);
    return () => cancelAnimationFrame(frame);
  }, [live?.tick, live?.ticksPerSecond, live, replay.ticks.length]);

  return time;
}
