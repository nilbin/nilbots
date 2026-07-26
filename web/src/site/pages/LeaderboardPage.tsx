import { useEffect, useMemo, useState } from 'react';
import { Link } from 'react-router-dom';
import clsx from 'clsx';
import BotIdentity from '../components/BotIdentity';
import { api, type Leaderboard } from '../api';
import { EmptyState, ErrorState, LoadingState } from '../components/StateView';

export default function LeaderboardPage() {
  const [board, setBoard] = useState<Leaderboard | null>(null);
  const [rules, setRules] = useState<string | null>(null); // null = server default
  const [error, setError] = useState<unknown>(null);
  const [reloads, setReloads] = useState(0);
  const [query, setQuery] = useState('');

  // Keep each bot's real rank while filtering: #7 is #7 whatever else is on screen.
  const shown = useMemo(() => {
    const needle = query.trim().toLowerCase();
    return (board?.entries ?? []).filter(
      (entry) =>
        needle === '' ||
        entry.name.toLowerCase().includes(needle) ||
        entry.owner.toLowerCase().includes(needle),
    );
  }, [board, query]);

  useEffect(() => {
    setError(null);
    const query = rules === null ? '' : `?rules=${encodeURIComponent(rules)}`;
    void api.get<Leaderboard>(`/api/leaderboard${query}`).then(setBoard).catch(setError);
  }, [rules, reloads]);

  return (
    <div className="mx-auto max-w-2xl">
      <h1 className="mb-1 text-2xl font-black tracking-wide">Leaderboard</h1>
      <p className="mb-3 text-sm text-arena-dim">
        Ratings move only through ranked sets: six games across three map/seed pairs,
        each played from both starting positions. Every rules version has its own
        ladder — a new era never erases old standings.
      </p>
      {board !== null && board.rulesVersion !== board.activeRulesVersion && (
        <p className="mb-3 text-sm text-arena-dim">
          This ladder is <b>closed</b>: rules {board.rulesVersion} no longer accepts new
          sets, so these standings are final. Ranked play happens on rules{' '}
          {board.activeRulesVersion}.
        </p>
      )}
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
      {error !== null && (
        <ErrorState error={error} onRetry={() => setReloads((n) => n + 1)} />
      )}
      {error === null && board === null && <LoadingState label="Loading the ladder…" />}
      {board !== null && board.entries.length === 0 && (
        <EmptyState
          title="No ranked sets yet"
          detail="Standings appear once bots have played a ranked set."
        />
      )}
      {board !== null && board.entries.length > 0 && (
        <input
          type="search"
          value={query}
          onChange={(e) => setQuery(e.target.value)}
          placeholder="Filter by bot or owner…"
          aria-label="Filter the ladder by bot or owner"
          className="mb-3 w-full rounded-md border border-arena-edge bg-arena-bg px-3 py-1.5 text-sm text-arena-text placeholder:text-arena-dim"
        />
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
          {shown.length === 0 && (
            <li className="text-sm text-arena-dim">No bot on this ladder matches “{query}”.</li>
          )}
          {shown.map((entry) => (
            <li key={entry.id}>
              <Link
                to={`/bots/${entry.slug}`}
                className="flex items-center gap-3 rounded-lg border border-arena-edge bg-arena-panel/60 px-4 py-3 transition-colors hover:border-arena-dim"
              >
                <span
                  className={
                    'w-8 text-center font-mono text-sm ' +
                    (entry.rank === 1
                      ? 'text-amber-300'
                      : entry.rank <= 3
                        ? 'text-arena-accent'
                        : 'text-arena-dim')
                  }
                >
                  #{entry.rank}
                </span>
                <BotIdentity
                  name={entry.name}
                  accent={entry.accent}
                  lookId={entry.lookId}
                  size="sm"
                  emphasized
                />
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
