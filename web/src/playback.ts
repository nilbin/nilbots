import { useEffect, useRef, useState, useCallback } from 'react';
import type { ReplayDocument } from './types';

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

export function usePlayback(replay: ReplayDocument): PlaybackState {
  const tickCount = replay.ticks.length;
  const [time, setTime] = useState(0);
  const [playing, setPlaying] = useState(true);
  const [speed, setSpeed] = useState(1);
  const frame = useRef<number>(0);
  const lastStamp = useRef<number | null>(null);

  useEffect(() => {
    if (!playing) {
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
  }, [playing, speed, tickCount]);

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
