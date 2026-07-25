import { useEffect, useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import type { ReplayDocument } from '../../types';
import Viewer from '../../components/Viewer';
import { ApiError, api } from '../api';

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
  const [missing, setMissing] = useState(false);

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
      } catch (e) {
        if (stopped) return;
        // A 404 is an answer, not a hiccup: retrying it forever left the page on
        // "Loading…" for any mistyped match id (UI audit). Everything else is
        // transient — a restarting server, a dropped connection — so keep polling.
        if (e instanceof ApiError && e.status === 404) setMissing(true);
        else timer = window.setTimeout(poll, 3000);
      }
    };
    void poll();
    return () => {
      stopped = true;
      window.clearTimeout(timer);
    };
  }, [matchId]);

  if (missing)
    return (
      <div className="rounded-xl border border-arena-edge bg-arena-panel p-6">
        <p className="font-semibold">No such match.</p>
        <p className="mt-1 text-sm text-arena-dim">
          This match id does not exist.{' '}
          <Link to="/" className="text-arena-accent hover:underline">
            Back to the arena
          </Link>
          .
        </p>
      </div>
    );

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
