import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { api, type LeaderboardEntry } from '../api';

export default function LeaderboardPage() {
  const [entries, setEntries] = useState<LeaderboardEntry[] | null>(null);

  useEffect(() => {
    void api.get<LeaderboardEntry[]>('/api/leaderboard').then(setEntries);
  }, []);

  return (
    <div className="mx-auto max-w-2xl">
      <h1 className="mb-1 text-2xl font-black tracking-wide">Leaderboard</h1>
      <p className="mb-5 text-sm text-arena-dim">
        Ratings move only through ranked sets: six games across three map/seed pairs,
        each played from both starting positions.
      </p>
      {entries === null ? (
        <p className="text-sm text-arena-dim">Loading…</p>
      ) : entries.length === 0 ? (
        <p className="text-sm text-arena-dim">
          Nobody has fought for rating yet. Open a bot page and start a ranked set.
        </p>
      ) : (
        <ol className="flex flex-col gap-2">
          {entries.map((entry, index) => (
            <li key={entry.id}>
              <Link
                to={`/bots/${entry.id}`}
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
