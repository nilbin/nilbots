import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import clsx from 'clsx';
import { api, type Leaderboard } from '../api';

export default function LeaderboardPage() {
  const [board, setBoard] = useState<Leaderboard | null>(null);
  const [rules, setRules] = useState<string | null>(null); // null = server default

  useEffect(() => {
    const query = rules === null ? '' : `?rules=${encodeURIComponent(rules)}`;
    void api.get<Leaderboard>(`/api/leaderboard${query}`).then(setBoard);
  }, [rules]);

  return (
    <div className="mx-auto max-w-2xl">
      <h1 className="mb-1 text-2xl font-black tracking-wide">Leaderboard</h1>
      <p className="mb-3 text-sm text-arena-dim">
        Ratings move only through ranked sets: six games across three map/seed pairs,
        each played from both starting positions. Every rules version has its own
        ladder — a new era never erases old standings.
      </p>
      {board !== null && board.ladders.length > 1 && (
        <div className="mb-4 flex flex-wrap items-center gap-1.5" role="group" aria-label="Ruleset ladder">
          {board.ladders.map((ladder) => (
            <button
              key={ladder}
              onClick={() => setRules(ladder)}
              className={clsx(
                'rounded px-2.5 py-1 font-mono text-xs transition-colors',
                ladder === board.rulesVersion
                  ? 'bg-arena-accent text-slate-950'
                  : 'border border-arena-edge text-arena-dim hover:border-arena-dim hover:text-arena-text',
              )}
            >
              rules {ladder}
            </button>
          ))}
        </div>
      )}
      {board === null ? (
        <p className="text-sm text-arena-dim">Loading…</p>
      ) : board.entries.length === 0 ? (
        <p className="text-sm text-arena-dim">
          Nobody has fought on the rules {board.rulesVersion} ladder yet. Open a bot
          page and start a ranked set.
        </p>
      ) : (
        <ol className="flex flex-col gap-2">
          {board.entries.map((entry, index) => (
            <li key={entry.id}>
              <Link
                to={`/bots/${entry.slug}`}
                className="flex items-center gap-3 rounded-lg border border-arena-edge bg-arena-panel/60 px-4 py-3 transition-colors hover:border-arena-dim"
              >
                <span
                  className={
                    'w-8 text-center font-mono text-sm ' +
                    (index === 0
                      ? 'text-amber-300'
                      : index < 3
                        ? 'text-arena-accent'
                        : 'text-arena-dim')
                  }
                >
                  #{index + 1}
                </span>
                <span className="inline-block size-3 rounded-full" style={{ background: entry.accent }} />
                <span className="font-semibold">{entry.name}</span>
                <span className="text-xs text-arena-dim">by {entry.owner}</span>
                <span className="ml-auto font-mono text-sm text-arena-text">{entry.rating}</span>
                <span className="font-mono text-[11px] text-arena-dim">
                  {entry.rankedSets} set{entry.rankedSets === 1 ? '' : 's'}
                </span>
              </Link>
            </li>
          ))}
        </ol>
      )}
    </div>
  );
}
