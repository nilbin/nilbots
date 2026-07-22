import { useEffect, useState } from 'react';
import type { ReplayDocument } from './types';
import { usePlayback } from './playback';
import ArenaCanvas from './components/ArenaCanvas';
import Controls from './components/Controls';
import BotPanel from './components/BotPanel';
import EventFeed from './components/EventFeed';

export default function App() {
  const [replay, setReplay] = useState<ReplayDocument | null>(
    window.__BOTARENA_REPLAY__ ?? null,
  );
  const [loadError, setLoadError] = useState<string | null>(null);

  useEffect(() => {
    if (replay) return;
    // Dev mode: serve a replay from web/public/replay.json (see scripts/dev-viewer.sh).
    fetch('replay.json')
      .then((response) => (response.ok ? response.json() : Promise.reject(response.status)))
      .then((data: ReplayDocument) => setReplay(data))
      .catch(() =>
        setLoadError(
          'No replay embedded and no replay.json found. Generate one with: botarena play',
        ),
      );
  }, [replay]);

  if (loadError) {
    return (
      <div className="flex h-screen items-center justify-center">
        <p className="max-w-md font-mono text-sm text-arena-dim">{loadError}</p>
      </div>
    );
  }
  if (!replay) {
    return (
      <div className="flex h-screen items-center justify-center">
        <p className="font-mono text-sm text-arena-dim">Loading replay…</p>
      </div>
    );
  }
  return <Viewer replay={replay} />;
}

function Viewer({ replay }: { replay: ReplayDocument }) {
  const playback = usePlayback(replay);
  const [selectedSlot, setSelectedSlot] = useState<number | null>(null);
  const [showVisibility, setShowVisibility] = useState(true);

  useEffect(() => {
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
  }, [playback]);

  const { header, result } = replay;
  const winner =
    result.winnerSlot === null ? null : header.participants[result.winnerSlot];

  return (
    <div className="mx-auto flex h-screen max-w-7xl flex-col gap-3 p-3 md:p-5">
      <header className="flex flex-wrap items-baseline gap-x-4 gap-y-1">
        <h1 className="text-xl font-black tracking-[0.2em] text-arena-text uppercase">
          Bot<span className="text-arena-accent">Arena</span>
        </h1>
        <span className="font-mono text-xs text-arena-dim">
          {header.mapId} · seed {header.seed} · rules {header.gameRulesVersion} ·{' '}
          {header.participants.map((p) => p.name).join(' vs ')}
        </span>
        <span
          className="ml-auto font-mono text-[11px] text-arena-dim"
          title={`replay ${replay.replayHash}`}
        >
          #{replay.replayHash.slice(0, 12)}
        </span>
      </header>

      <div className="grid min-h-0 flex-1 grid-cols-1 gap-3 lg:grid-cols-[1fr_320px]">
        <main className="relative min-h-[320px] overflow-hidden rounded-lg border border-arena-edge bg-arena-bg">
          <ArenaCanvas
            replay={replay}
            time={playback.time}
            selectedSlot={selectedSlot}
            showVisibility={showVisibility}
            onSelectSlot={setSelectedSlot}
          />
          {playback.atEnd && (
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
            tick={playback.tick}
            selectedSlot={selectedSlot}
            showVisibility={showVisibility}
            onSelectSlot={setSelectedSlot}
            onToggleVisibility={() => setShowVisibility((value) => !value)}
          />
          <EventFeed replay={replay} tick={playback.tick} />
        </aside>
      </div>

      <Controls playback={playback} />
    </div>
  );
}
