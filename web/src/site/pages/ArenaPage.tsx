import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { api, type MatchSummary } from '../api';
import { useAuth } from '../auth';

/// Landing + public match browser: recent matches, refreshed while any are running.
export default function ArenaPage() {
  const [matches, setMatches] = useState<MatchSummary[] | null>(null);
  const { user } = useAuth();

  useEffect(() => {
    let timer: number | undefined;
    const load = async () => {
      const data = await api.get<MatchSummary[]>('/api/matches?take=30');
      setMatches(data);
      if (data.some((m) => m.status === 'Pending' || m.status === 'Running'))
        timer = window.setTimeout(load, 2500);
    };
    void load();
    return () => window.clearTimeout(timer);
  }, []);

  return (
    <div className="flex flex-col gap-8">
      <section className="rounded-xl border border-arena-edge bg-arena-panel p-8">
        <h1 className="text-3xl font-black tracking-wide">
          Write a bot. <span className="text-arena-accent">Watch it fight.</span>
        </h1>
        <p className="mt-2 max-w-xl text-sm text-arena-dim">
          Bot Arena is a programming game: autonomous C# bots compiled to
          WebAssembly, battling in a deterministic arena. Same seed, same bots —
          same match, every time.
        </p>
        <div className="mt-4 flex gap-3">
          <Link
            to={user ? '/garage' : '/login'}
            className="rounded-md bg-arena-accent px-4 py-2 font-semibold text-slate-950"
          >
            {user ? 'Open my garage' : 'Join the arena'}
          </Link>
          <Link
            to="/bots"
            className="rounded-md border border-arena-edge px-4 py-2 text-arena-text"
          >
            Browse bots
          </Link>
        </div>
      </section>

      <section>
        <h2 className="mb-3 font-mono text-xs tracking-widest text-arena-dim">RECENT MATCHES</h2>
        {matches === null ? (
          <p className="text-sm text-arena-dim">Loading…</p>
        ) : matches.length === 0 ? (
          <p className="text-sm text-arena-dim">
            No matches yet — be the first to throw down a challenge.
          </p>
        ) : (
          <ul className="flex flex-col gap-2">
            {matches.map((match) => (
              <MatchRow key={match.id} match={match} />
            ))}
          </ul>
        )}
      </section>
    </div>
  );
}

export function MatchRow({ match }: { match: MatchSummary }) {
  const [a, b] = match.participants;
  const winner = match.winnerSlot;
  return (
    <li>
      <Link
        to={`/matches/${match.id}`}
        className="flex flex-wrap items-center gap-x-3 gap-y-1 rounded-lg border border-arena-edge bg-arena-panel/60 px-4 py-3 transition-colors hover:border-arena-dim"
      >
        <Fighter name={a?.nameSnapshot} accent={a?.accentSnapshot} bold={winner === 0} />
        <span className="font-mono text-xs text-arena-dim">vs</span>
        <Fighter name={b?.nameSnapshot} accent={b?.accentSnapshot} bold={winner === 1} />
        <span className="ml-auto flex items-center gap-2 font-mono text-[11px] text-arena-dim">
          {match.setGame && <span className="rounded bg-arena-edge px-1.5 py-0.5">ranked g{match.setGame}</span>}
          {match.mapId} ·{' '}
          {match.broadcasting ? (
            <span className="flex items-center gap-1 font-bold text-red-400">
              <span className="inline-block size-1.5 animate-pulse rounded-full bg-red-500" />
              LIVE
            </span>
          ) : match.status === 'Completed' ? (
            winner === null || winner === undefined ? (
              'draw'
            ) : (
              `${match.participants[winner]?.nameSnapshot} wins`
            )
          ) : (
            match.status.toLowerCase()
          )}
        </span>
      </Link>
    </li>
  );
}

function Fighter({ name, accent, bold }: { name?: string; accent?: string; bold: boolean }) {
  return (
    <span className={'flex items-center gap-2 ' + (bold ? 'font-bold' : '')}>
      <span className="inline-block size-2.5 rounded-full" style={{ background: accent }} />
      {name ?? '?'}
    </span>
  );
}
