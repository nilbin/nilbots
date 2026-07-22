import { useEffect, useState } from 'react';
import { useParams } from 'react-router-dom';
import type { ReplayDocument } from '../../types';
import Viewer from '../../components/Viewer';
import { api, type MatchDetail } from '../api';

export default function MatchPage() {
  const { matchId } = useParams<{ matchId: string }>();
  const [match, setMatch] = useState<MatchDetail | null>(null);
  const [replay, setReplay] = useState<ReplayDocument | null>(null);

  useEffect(() => {
    let timer: number | undefined;
    const poll = async () => {
      const data = await api.get<MatchDetail>(`/api/matches/${matchId}`);
      setMatch(data);
      if (data.status === 'Completed') {
        setReplay(await api.get<ReplayDocument>(`/api/matches/${matchId}/replay`));
      } else if (data.status !== 'Failed') {
        timer = window.setTimeout(poll, 1500);
      }
    };
    void poll();
    return () => window.clearTimeout(timer);
  }, [matchId]);

  if (!match) return <p className="text-sm text-arena-dim">Loading…</p>;

  if (match.status === 'Failed')
    return (
      <div className="rounded-xl border border-red-900 bg-arena-panel p-6">
        <p className="font-semibold text-red-400">Match failed to execute.</p>
        {match.error && (
          <pre className="mt-2 font-mono text-xs whitespace-pre-wrap text-arena-dim">{match.error}</pre>
        )}
      </div>
    );

  if (!replay)
    return (
      <div className="flex flex-col items-center gap-3 py-24">
        <div className="size-10 animate-spin rounded-full border-2 border-arena-edge border-t-arena-accent" />
        <p className="font-mono text-sm text-arena-dim">
          {match.status === 'Pending' ? 'Match queued…' : 'Bots are fighting…'}
        </p>
      </div>
    );

  // The viewer is designed for a full viewport; give it a fixed-height stage here.
  return (
    <div className="h-[85vh] min-h-[560px]">
      <Viewer replay={replay} />
    </div>
  );
}
