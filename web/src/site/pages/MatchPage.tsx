import { useEffect, useState } from 'react';
import { useParams } from 'react-router-dom';
import type { ReplayDocument } from '../../types';
import Viewer from '../../components/Viewer';
import { api } from '../api';

interface LiveState {
  status: string;
  presentationTicksPerSecond: number;
  presentationTick: number;
  totalTicks: number | null;
  broadcastComplete: boolean;
  countdownMs: number;
}

export default function MatchPage() {
  const { matchId } = useParams<{ matchId: string }>();
  const [live, setLive] = useState<LiveState | null>(null);
  const [replay, setReplay] = useState<ReplayDocument | null>(null);
  const [finished, setFinished] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let stopped = false;
    let timer: number | undefined;

    const poll = async () => {
      try {
        const state = await api.get<LiveState>(`/api/matches/${matchId}/live`);
        if (stopped) return;
        setLive(state);

        if (state.status === 'Failed') {
          const detail = await api.get<{ error: string | null }>(`/api/matches/${matchId}`);
          setError(detail.error ?? 'Match failed to execute.');
          return;
        }
        if (state.status !== 'Completed') {
          timer = window.setTimeout(poll, 1500);
          return;
        }
        if (state.broadcastComplete) {
          setReplay(await api.get<ReplayDocument>(`/api/matches/${matchId}/replay`));
          setFinished(true);
          return;
        }
        // Mid-broadcast: pick up the ticks revealed so far and keep following.
        setReplay(await api.get<ReplayDocument>(`/api/matches/${matchId}/replay`));
        timer = window.setTimeout(poll, 1500);
      } catch {
        if (!stopped) timer = window.setTimeout(poll, 3000);
      }
    };
    void poll();
    return () => {
      stopped = true;
      window.clearTimeout(timer);
    };
  }, [matchId]);

  if (error)
    return (
      <div className="rounded-xl border border-red-900 bg-arena-panel p-6">
        <p className="font-semibold text-red-400">Match failed to execute.</p>
        <pre className="mt-2 font-mono text-xs whitespace-pre-wrap text-arena-dim">{error}</pre>
      </div>
    );

  if (!live) return <p className="text-sm text-arena-dim">Loading…</p>;

  if (live.status !== 'Completed')
    return (
      <Waiting
        label={live.status === 'Pending' ? 'Match queued…' : 'Bots are fighting…'}
      />
    );

  if (!finished && (live.countdownMs > 0 || !replay || replay.ticks.length === 0))
    return <Waiting label="Broadcast starting…" countdown />;

  if (!replay) return <Waiting label="Loading replay…" />;

  return (
    <div className="h-[calc(100dvh-140px)] min-h-[560px]">
      <Viewer
        replay={replay}
        live={
          finished
            ? undefined
            : {
                tick: live.presentationTick,
                ticksPerSecond: live.presentationTicksPerSecond,
              }
        }
      />
    </div>
  );
}

function Waiting({ label, countdown }: { label: string; countdown?: boolean }) {
  return (
    <div className="flex flex-col items-center gap-3 py-24">
      <div
        className={
          'size-10 rounded-full border-2 border-arena-edge ' +
          (countdown ? 'animate-pulse border-t-red-500' : 'animate-spin border-t-arena-accent')
        }
      />
      <p className="font-mono text-sm text-arena-dim">{label}</p>
    </div>
  );
}
