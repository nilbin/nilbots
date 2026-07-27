import { useMemo, useState } from 'react';
import { Link } from 'react-router-dom';
import clsx from 'clsx';
import BotIdentity from '../components/BotIdentity';
import { useLeaderboard, useMe } from '../queries';
import { EmptyState, ErrorState, LoadingState } from '../components/StateView';

/**
 * The ladder.
 *
 * A table, not a list of cards. Standings are read by comparing rows — is that rating
 * close to mine, who moved — and cards put every value in a different place on every
 * row, so the eye has to re-find each one. A table with tabular figures lets a column
 * be scanned instead of read.
 *
 * Two things the design asks for cannot be built yet, and are deliberately absent rather
 * than faked: **movement** (`▲14 places`) needs each bot's rank at season open, which
 * nothing records, and the **season sparkline** needs rating history per bot. Both are
 * columns in this table the day the data exists; see `seasonView` below, which is the
 * seam they arrive through.
 */

/**
 * Per-row season data, when there is a season.
 *
 * Today this is always empty — there are no seasons and no rank snapshots. It exists so
 * the table is already shaped for them, and so the day an endpoint appears the change is
 * this function rather than the markup.
 */
function seasonView(_entryId: string): {
  movement: number | null;
  history: readonly number[] | null;
} {
  return { movement: null, history: null };
}

export default function LeaderboardPage() {
  const [rules, setRules] = useState<string | null>(null); // null = server default
  const { data: board = null, error, refetch } = useLeaderboard(rules);
  const { data: me = null } = useMe();
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

  const closed =
    board !== null && board.rulesVersion !== board.activeRulesVersion;

  return (
    <div className="mx-auto max-w-3xl">
      <p className="type-label mb-2 text-[10.5px] text-arena-dim">
        Ranked ladder{board === null ? '' : ` · rules ${board.rulesVersion}`}
      </p>
      <h1 className="type-display mb-2 text-[30px]">Standings</h1>
      <p className="mb-4 max-w-[62ch] text-sm text-arena-dim">
        Ratings move only through ranked sets: six games across three map/seed pairs,
        each played from both starting positions. Every rules version has its own
        ladder — a new era never erases old standings.
      </p>

      {closed && (
        <p className="mb-4 max-w-[62ch] rounded-md border border-arena-edge border-l-2 border-l-arena-accent bg-arena-panel/60 px-3 py-2 text-sm text-arena-dim">
          This ladder is <b className="text-arena-text">closed</b>: rules{' '}
          {board.rulesVersion} no longer accepts new sets, so these standings are
          final. Ranked play happens on rules {board.activeRulesVersion}.
        </p>
      )}

      {board !== null && board.ladders.length > 1 && (
        <div
          className="mb-4 flex flex-wrap items-center gap-1.5"
          role="group"
          aria-label="Ruleset ladder"
        >
          {board.ladders.map((ladder) => (
            <button
              key={ladder}
              onClick={() => setRules(ladder)}
              aria-pressed={ladder === board.rulesVersion}
              className={clsx(
                'type-label rounded-full border px-2.5 py-1 text-[10px] transition-colors',
                ladder === board.rulesVersion
                  ? 'border-arena-edge2 bg-arena-raise text-arena-text'
                  : 'border-arena-edge text-arena-dim hover:text-arena-text',
              )}
            >
              rules {ladder}
            </button>
          ))}
        </div>
      )}

      {error !== null && (
        <ErrorState error={error} onRetry={() => void refetch()} />
      )}
      {error === null && board === null && (
        <LoadingState label="Loading the ladder…" />
      )}
      {board !== null && board.entries.length === 0 && (
        <EmptyState
          title="No ranked sets yet"
          detail="Standings appear once bots have played a ranked set. Open a bot page and start one."
        />
      )}

      {board !== null && board.entries.length > 0 && (
        <>
          <input
            type="search"
            value={query}
            onChange={(event) => setQuery(event.target.value)}
            placeholder="Filter by bot or owner…"
            aria-label="Filter the ladder by bot or owner"
            className="mb-3 w-full rounded-md border border-arena-edge bg-arena-bg px-3 py-1.5 text-sm text-arena-text placeholder:text-arena-dim focus:border-arena-edge2 focus:outline-none"
          />
          {/* A phone drops columns rather than scrolling sideways. Rank, bot and rating
              are what a standing *is*; owner and set count are context you can open the
              bot page for. A table that has to be dragged is a table nobody reads. */}
          <div>
            <table className="w-full border-collapse text-[13px]">
              <thead>
                <tr>
                  <Th className="w-10">#</Th>
                  <Th>Bot</Th>
                  <Th className="hidden w-28 sm:table-cell">Owner</Th>
                  <Th className="w-20" numeric>
                    Rating
                  </Th>
                  <Th className="hidden w-16 sm:table-cell" numeric>
                    Sets
                  </Th>
                </tr>
              </thead>
              <tbody>
                {shown.length === 0 && (
                  <tr>
                    <td colSpan={5} className="py-4 text-sm text-arena-dim">
                      No bot on this ladder matches “{query}”.
                    </td>
                  </tr>
                )}
                {shown.map((entry) => {
                  // Yours is marked by a rule in that bot's own accent — the only
                  // chroma the table spends, and always somebody's choice rather than
                  // a system opinion.
                  const mine =
                    me !== null && entry.owner === me.displayName;
                  const season = seasonView(entry.id);
                  return (
                    <tr
                      key={entry.id}
                      className={clsx(
                        'border-b border-arena-edge last:border-b-0',
                        mine && 'bg-arena-text/[0.028]',
                      )}
                    >
                      <td
                        className="tabular py-2 pr-2 pl-2 font-mono text-arena-dim"
                        style={
                          mine && entry.accent
                            ? { boxShadow: `inset 2px 0 0 ${entry.accent}` }
                            : undefined
                        }
                      >
                        {entry.rank}
                      </td>
                      <td className="py-2 pr-2">
                        <Link
                          to={`/bots/${entry.slug}`}
                          className="inline-flex min-w-0 transition-opacity hover:opacity-80"
                        >
                          <BotIdentity
                            name={entry.name}
                            accent={entry.accent}
                            lookId={entry.lookId}
                            size="sm"
                          />
                        </Link>
                      </td>
                      <td className="hidden truncate py-2 pr-2 text-arena-dim sm:table-cell">
                        {entry.owner}
                      </td>
                      <td className="tabular py-2 pr-2 text-right font-mono text-arena-text">
                        {entry.rating}
                        {season.movement !== null && (
                          <span className="ml-1.5 text-arena-dim">
                            {season.movement > 0 ? '▲' : '▼'}
                            {Math.abs(season.movement)}
                          </span>
                        )}
                      </td>
                      <td className="tabular hidden py-2 pr-2 text-right font-mono text-arena-dim sm:table-cell">
                        {entry.rankedSets}
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        </>
      )}
    </div>
  );
}

function Th({
  children,
  className,
  numeric,
}: {
  children: React.ReactNode;
  className?: string;
  numeric?: boolean;
}) {
  return (
    <th
      scope="col"
      className={clsx(
        'type-label border-b border-arena-edge px-2 pb-2 text-[10px] font-normal text-arena-dim',
        numeric ? 'text-right' : 'text-left',
        className,
      )}
    >
      {children}
    </th>
  );
}
