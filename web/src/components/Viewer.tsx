import { useEffect, useRef, useState } from 'react';
import type { ReplayDocument } from '../types';
import { usePlayback } from '../playback';
import ArenaCanvas from './ArenaCanvas';
import Controls from './Controls';
import BotPanel from './BotPanel';
import EventFeed from './EventFeed';
import Logo from './Logo';

export interface LiveFollow {
  /** The shared presentation tick from the server clock. */
  tick: number;
  ticksPerSecond: number;
}

export default function Viewer({
  replay,
  live,
}: {
  replay: ReplayDocument;
  live?: LiveFollow;
}) {
  const playback = usePlayback(replay);
  const liveTime = useLiveFollower(replay, live);
  const [selectedSlot, setSelectedSlot] = useState<number | null>(null);
  const [showVisibility, setShowVisibility] = useState(true);

  const isLive = live !== undefined;
  const time = isLive ? liveTime : playback.time;
  const tick = Math.max(0, Math.min(Math.floor(time), replay.ticks.length - 1));

  useEffect(() => {
    if (isLive) return; // No seeking during a live broadcast — viewers stay synchronized.
    const onKey = (event: KeyboardEvent) => {
      if (event.target instanceof HTMLInputElement) return;
      switch (event.key) {
        case ' ':
          event.preventDefault();
          playback.toggle();
          break;
        case 'ArrowLeft':
          playback.step(-1);
          break;
        case 'ArrowRight':
          playback.step(1);
          break;
        case 'Home':
          playback.restart();
          break;
      }
    };
    window.addEventListener('keydown', onKey);
    return () => window.removeEventListener('keydown', onKey);
  }, [playback, isLive]);

  const { header, result } = replay;
  const winner =
    result && result.winnerSlot !== null ? header.participants[result.winnerSlot] : null;

  return (
    <div className="mx-auto flex h-full max-w-7xl flex-col gap-3 p-3 md:p-5">
      <header className="flex flex-wrap items-baseline gap-x-4 gap-y-1">
        <h1 className="text-xl"><Logo size={24} /></h1>
        <span className="font-mono text-xs text-arena-dim">
          {header.mapId} · seed {header.seed} · rules {header.gameRulesVersion} ·{' '}
          {header.participants.map((p) => p.name).join(' vs ')}
        </span>
        {isLive ? (
          <span className="ml-auto flex items-center gap-1.5 rounded bg-red-500/15 px-2 py-0.5 font-mono text-[11px] font-bold text-red-400">
            <span className="inline-block size-2 animate-pulse rounded-full bg-red-500" />
            LIVE
          </span>
        ) : (
          replay.replayHash && (
            <span
              className="ml-auto font-mono text-[11px] text-arena-dim"
              title={`replay ${replay.replayHash}`}
            >
              #{replay.replayHash.slice(0, 12)}
            </span>
          )
        )}
      </header>

      <div className="grid min-h-0 flex-1 grid-cols-1 gap-3 lg:grid-cols-[1fr_320px]">
        <main className="relative min-h-[320px] overflow-hidden rounded-lg border border-arena-edge bg-arena-bg">
          <ArenaCanvas
            replay={replay}
            time={time}
            selectedSlot={selectedSlot}
            showVisibility={showVisibility}
            onSelectSlot={setSelectedSlot}
          />
          {!isLive && playback.atEnd && result && (
            <div className="absolute inset-0 flex items-center justify-center bg-arena-bg/70">
              <div className="rounded-xl border border-arena-edge bg-arena-panel px-8 py-6 text-center shadow-2xl">
                <p className="font-mono text-xs tracking-widest text-arena-dim">
                  MATCH COMPLETE — {result.reason.toUpperCase()} · TICK {result.endTick}
                </p>
                <p className="mt-2 text-2xl font-black tracking-wide">
                  {winner ? (
                    <>
                      <span style={{ color: winner.accent }}>{winner.name}</span> WINS
                    </>
                  ) : (
                    'DRAW'
                  )}
                </p>
                {result.bots.some((b) => b.zoneTicks !== undefined) && (
                  <p className="mt-1 font-mono text-xs text-arena-dim">
                    zone{' '}
                    {[...result.bots]
                      .sort((a, b) => a.slot - b.slot)
                      .map((b) => `${header.participants[b.slot]?.name ?? `s${b.slot}`} ${b.zoneTicks ?? 0}`)
                      .join(' · ')}
                  </p>
                )}
                {result.controlPressure !== undefined && (
                  <p className="mt-1 font-mono text-xs text-arena-dim">
                    final control {result.controlPressure > 0 ? '+' : ''}
                    {result.controlPressure} / ±
                    {header.controlOvertimeStartTick !== undefined &&
                    result.endTick >= header.controlOvertimeStartTick
                      ? header.controlOvertimePressureLimit
                      : header.controlPressureLimit}
                  </p>
                )}
                <button
                  onClick={playback.restart}
                  className="mt-4 rounded-md border border-arena-accent px-4 py-1.5 font-mono text-sm text-arena-accent transition-colors hover:bg-arena-accent/15"
                >
                  ⟲ Watch again
                </button>
              </div>
            </div>
          )}
        </main>

        <aside className="flex min-h-0 flex-col gap-3">
          <BotPanel
            replay={replay}
            tick={tick}
            selectedSlot={selectedSlot}
            showVisibility={showVisibility}
            onSelectSlot={setSelectedSlot}
            onToggleVisibility={() => setShowVisibility((value) => !value)}
          />
          <EventFeed replay={replay} tick={tick} />
        </aside>
      </div>

      {isLive ? (
        <div className="flex items-center gap-3 rounded-lg border border-arena-edge bg-arena-panel p-4 font-mono text-xs text-arena-dim">
          <span className="inline-block size-2 animate-pulse rounded-full bg-red-500" />
          Broadcasting tick {String(tick).padStart(3, '0')} — every viewer sees this moment.
        </div>
      ) : (
        <Controls playback={playback} />
      )}
    </div>
  );
}

/// Follows the server's presentation clock: re-anchors on every update from the
/// server and advances smoothly between polls, never running past received ticks.
function useLiveFollower(replay: ReplayDocument, live?: LiveFollow): number {
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
