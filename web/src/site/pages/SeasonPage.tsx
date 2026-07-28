import { useMemo, useState } from 'react';
import { Link } from 'react-router-dom';
import clsx from 'clsx';
import BotIdentity from '../components/BotIdentity';
import type { LeaderboardEntry } from '../api';
import { useLeaderboard, useMe } from '../queries';
import { EmptyState, ErrorState, LoadingState } from '../components/StateView';
import Th from '../components/TableHeader';
import Movement from '../components/Movement';
import Control from '../../components/ToggleButton';
import { playerAccent } from '../../presentation/playerAccent';
import { styleVariables } from '../../presentation/styleVariables';

/**
 * The season screen: your fleet, your trajectory, and the ladder underneath them.
 *
 * A player owns several bots at once, so "your standing" is not one number — which is
 * why the strip walks every bot of yours on this ladder rather than pinning a favourite,
 * and why the table marks all of your rows.
 *
 * The table is a table, not a list of cards. Standings are read by comparing rows — is
 * that rating close to mine, who moved — and cards put every value in a different place
 * on every row, so the eye has to re-find each one. Tabular figures let a column be
 * scanned instead of read.
 *
 * Three things the design asks for cannot be built yet and are deliberately absent rather
 * than faked: **the season itself** (there is no season number, start or end anywhere in
 * the API), **movement** (`▲14 places` needs each bot's rank at season open, which nothing
 * records) and the **season trend and record** (per-bot rating history and a ranked W–L,
 * neither of which this endpoint carries). Each has a named seam below and a column that
 * renders the day the data lands.
 */

/**
 * Per-row season data, when there is a season.
 *
 * Today every field is empty. `movement` needs a rank snapshot at season open, `history`
 * needs rating over time per bot, and `record` needs a ranked W–L on the ladder response
 * — `/api/bots/{id}/stats` has one but only a bot at a time, and a row of the table is
 * not worth a request. The table is already shaped for all three, so the day an endpoint
 * appears the change is this function rather than the markup.
 */
interface SeasonEntryView {
  movement: number | null;
  history: readonly number[] | null;
  record: { wins: number; losses: number } | null;
}

/**
 * One stable object keeps the page honest and avoids allocating a placeholder for every
 * row. The generated leaderboard contract carries none of these values today.
 *
 * A review response with made-up properties must not activate production UI. When the
 * backend owns these fields, regenerate the API client and map those typed fields here.
 */
const unavailableSeasonEntry: SeasonEntryView = {
  movement: null,
  history: null,
  record: null,
};

function seasonView(_entry: LeaderboardEntry): SeasonEntryView {
  return unavailableSeasonEntry;
}

type SortKey = 'rating' | 'movement';

export default function SeasonPage() {
  const [rules, setRules] = useState<string | null>(null); // null = server default
  const { data: board = null, error, refetch } = useLeaderboard(rules);
  const { data: me = null } = useMe();
  const [query, setQuery] = useState('');
  const [sort, setSort] = useState<SortKey>('rating');
  const [mineOnly, setMineOnly] = useState(false);

  const entries = useMemo(() => board?.entries ?? [], [board]);

  // Your bots on this ladder. Ownership is the display name the ladder reports, which is
  // the only join the response offers. Note the endpoint returns the top of the board, so
  // a bot of yours below the cut is on neither this strip nor the table.
  const fleet = useMemo(
    () => (me === null ? [] : entries.filter((entry) => entry.owner === me.displayName)),
    [entries, me],
  );

  // Switching ladders can take your last bot off the board, and a filter left pressed
  // over an empty fleet is a table nobody can get back. It stops applying with the
  // control that sets it.
  const mineActive = mineOnly && fleet.length > 0;

  // Keep each bot's real rank while filtering: #7 is #7 whatever else is on screen.
  const shown = useMemo(() => {
    const needle = query.trim().toLowerCase();
    const mine = new Set(fleet.map((entry) => entry.id));
    return entries.filter(
      (entry) =>
        (!mineActive || mine.has(entry.id)) &&
        (needle === '' ||
          entry.name.toLowerCase().includes(needle) ||
          entry.owner.toLowerCase().includes(needle)),
    );
  }, [entries, fleet, mineActive, query]);

  // The endpoint already returns rating order, so that sort is the identity; movement is
  // a real comparator over a seam that is empty today, which is why its control is off.
  const ordered = useMemo(() => {
    if (sort === 'rating') return shown;
    return [...shown].sort((a, b) => {
      const left = seasonView(a).movement;
      const right = seasonView(b).movement;
      if (left === null) return right === null ? 0 : 1;
      if (right === null) return -1;
      return right - left;
    });
  }, [shown, sort]);

  const hasMovement = entries.some((entry) => seasonView(entry).movement !== null);
  const hasHistory = entries.some((entry) => seasonView(entry).history !== null);
  const hasRecord = entries.some((entry) => seasonView(entry).record !== null);
  const ladderColumnCount =
    5 + Number(hasMovement) + Number(hasHistory) + Number(hasRecord);

  const closed =
    board !== null && board.rulesVersion !== board.activeRulesVersion;

  return (
    <div className="mx-auto max-w-4xl">
      <p className="lab mb-2">
        Ranked ladder{board === null ? '' : ` · rules ${board.rulesVersion}`}
      </p>
      <h1 className="type-display mb-2 text-[30px]">Season</h1>
      <p className="t-body mb-4 max-w-[62ch] text-arena-dim">
        Ratings move only through ranked sets: six games across three map/seed pairs,
        each played from both starting positions. Every rules version has its own
        ladder — a new era never erases old standings. There is no season boundary yet,
        so this one runs continuously and the rules version is the only era the game
        keeps.
      </p>

      {closed && (
        <p className="panel-quiet t-body mb-4 max-w-[62ch] border-l-2 border-l-arena-edge2 px-3 py-2 text-arena-dim">
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
              type="button"
              onClick={() => setRules(ladder)}
              aria-pressed={ladder === board.rulesVersion}
              className={clsx('btn', ladder === board.rulesVersion && 'btn-on')}
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
      {board !== null && entries.length === 0 && (
        <EmptyState
          title="No ranked sets yet"
          detail="Standings appear once bots have played a ranked set. Open a bot page and start one."
        />
      )}

      {board !== null && entries.length > 0 && (
        <div className="flex flex-col gap-3.5">
          {/* The header and the fleet are one panel, and neither renders for a visitor
              with nothing on this ladder: every line in it is about *your* bots, so
              signed out it would say nothing the page above has not already said. */}
          {fleet.length > 0 && (
            <section className="panel pad">
              <div className="mb-2.5 flex flex-wrap items-center justify-between gap-2.5">
                <p className="lab">
                  {headerLine(board.rulesVersion, fleet.length)}
                </p>
                <span className="pill">
                  best rank {Math.min(...fleet.map((entry) => entry.rank))}
                </span>
              </div>
              <div className="grid gap-2.5 [grid-template-columns:repeat(auto-fit,minmax(190px,1fr))]">
                {fleet.map((entry) => (
                  <Link
                    key={entry.id}
                    to={`/bots/${entry.slug}`}
                    className="panel px-3 py-[11px] transition-colors hover:border-arena-edge2"
                  >
                    <div className="mb-2 flex items-center justify-between gap-2">
                      <BotIdentity
                        name={entry.name}
                        accent={entry.accent}
                        lookId={entry.lookId}
                        size="md"
                        className="min-w-0"
                      />
                      {/* A rank is the loudest number on the screen, and it is display
                          type rather than mono: it is the bot's position, not a value
                          the engine computed. */}
                      <span className="type-display tabular shrink-0 text-[22px] text-arena-text">
                        {entry.rank}
                      </span>
                    </div>
                    <div className="flex items-center justify-between gap-2.5">
                      <Movement
                        change={seasonView(entry).movement}
                        suffix="places"
                      />
                      <span className="val ml-auto text-arena-text">{entry.rating}</span>
                    </div>
                  </Link>
                ))}
              </div>
            </section>
          )}

          {fleet.length > 0 && (
            <section className="panel pad">
              <p className="lab mb-2">Your season · one line per bot</p>
              {/* This is where player accents earn their keep, and it is the design's own
                  argument: several overlaid trajectories are unreadable in one colour and
                  instantly separable in six — precisely because the grid and the labels
                  are achromatic, so the only thing colour distinguishes is which bot. */}
              <SeasonChart
                lines={fleet
                  .map((entry) => ({
                    name: entry.name,
                    accent: entry.accent,
                    history: seasonView(entry).history,
                  }))
                  .filter((line) => line.history !== null)}
              />
            </section>
          )}

          <section className="panel">
            <div className="pad flex flex-wrap items-center justify-between gap-2.5 pb-0">
              <p className="lab">Ranked ladder · {entries.length} bots</p>
              <div className="flex flex-wrap items-center gap-1.5" role="group" aria-label="Sort and filter the ladder">
                <Control pressed={sort === 'rating'} onClick={() => setSort('rating')}>
                  rating
                </Control>
                {hasMovement && (
                  <Control
                    pressed={sort === 'movement'}
                    onClick={() => setSort('movement')}
                  >
                    movement
                  </Control>
                )}
                {fleet.length > 0 && (
                  <Control pressed={mineActive} onClick={() => setMineOnly(!mineActive)}>
                    mine only
                  </Control>
                )}
              </div>
            </div>
            <div className="pad pt-2.5">
              <input
                type="search"
                value={query}
                onChange={(event) => setQuery(event.target.value)}
                placeholder="Filter by bot or owner…"
                aria-label="Filter the ladder by bot or owner"
                className="field mb-2.5"
              />
              {/* A phone drops secondary columns rather than scrolling sideways. Columns
                  the generated response cannot fill stay out of the visible table entirely;
                  their named seams remain in `seasonView` until the contract owns them. */}
              <table className="t-body w-full border-collapse">
                <thead>
                  <tr>
                    <Th className="w-[30px]">#</Th>
                    {hasMovement && (
                      <Th className="hidden w-[78px] sm:table-cell">Movement</Th>
                    )}
                    <Th>Bot</Th>
                    <Th className="hidden w-28 sm:table-cell">Owner</Th>
                    {hasHistory && (
                      <Th className="hidden w-[80px] md:table-cell">Trend</Th>
                    )}
                    <Th className="w-20" numeric>
                      Rating
                    </Th>
                    {hasRecord && (
                      <Th className="hidden w-16 md:table-cell" numeric>
                        W–L
                      </Th>
                    )}
                    <Th className="hidden w-16 sm:table-cell" numeric>
                      Sets
                    </Th>
                  </tr>
                </thead>
                <tbody>
                  {ordered.length === 0 && (
                    <tr>
                      <td colSpan={ladderColumnCount} className="py-4 text-arena-dim">
                        {query.trim() === ''
                          ? 'No bot of yours is on this ladder.'
                          : `No bot on this ladder matches “${query}”.`}
                      </td>
                    </tr>
                  )}
                  {ordered.map((entry) => {
                    // Yours is marked by a rule in that bot's own accent — the only
                    // chroma the table spends, and always somebody's choice rather than
                    // a system opinion.
                    const mine = me !== null && entry.owner === me.displayName;
                    const season = seasonView(entry);
                    const rail =
                      mine && entry.accent
                        ? playerAccent(entry.accent, 'panel')
                        : null;
                    return (
                      <tr
                        key={entry.id}
                        className={clsx(
                          'border-b border-arena-edge last:border-b-0',
                          mine && 'bg-arena-text/[0.028]',
                        )}
                      >
                        <td
                          className={clsx(
                            'type-display tabular p-2 align-middle text-[22px] text-arena-text',
                            rail && 'player-accent-rail',
                          )}
                          style={
                            rail
                              ? styleVariables({ '--player-accent': rail })
                              : undefined
                          }
                        >
                          {entry.rank}
                        </td>
                        {hasMovement && (
                          <td className="hidden p-2 align-middle sm:table-cell">
                            <Movement change={season.movement} />
                          </td>
                        )}
                        <td className="p-2 align-middle">
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
                        <td className="hidden truncate p-2 align-middle text-arena-dim sm:table-cell">
                          {entry.owner}
                        </td>
                        {hasHistory && (
                          <td className="hidden p-2 align-middle md:table-cell">
                            <Sparkline
                              history={season.history}
                              accent={mine ? entry.accent : null}
                            />
                          </td>
                        )}
                        <td className="val p-2 text-right align-middle whitespace-nowrap text-arena-text">
                          {entry.rating}
                        </td>
                        {hasRecord && (
                          <td className="val hidden p-2 text-right align-middle whitespace-nowrap text-arena-text md:table-cell">
                            {season.record &&
                              `${season.record.wins}–${season.record.losses}`}
                          </td>
                        )}
                        <td className="val hidden p-2 text-right align-middle whitespace-nowrap sm:table-cell">
                          {entry.rankedSets}
                        </td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
              {/* Say why the mock's secondary measures are absent rather than leaving a
                  disabled control or a run of blank columns looking unfinished. */}
              {!hasMovement && !hasHistory && !hasRecord && (
                <p id="season-data-note" className="t-micro mt-2.5">
                  Movement, trend and W–L are not shown: no rank snapshot, rating history
                  or ranked record reaches this endpoint yet.
                </p>
              )}
            </div>
          </section>
        </div>
      )}
    </div>
  );
}

/**
 * The line above the fleet.
 *
 * The mock reads "Season 3 · 11 days left · your 3 ranked bots". Two thirds of that is a
 * season, so the era the game does have stands in its place — one substitution rather
 * than an invented countdown.
 */
function headerLine(
  rulesVersion: string,
  owned: number,
) {
  const yours = `your ${owned} ranked ${owned === 1 ? 'bot' : 'bots'}`;
  return `Rules ${rulesVersion} · ${yours}`;
}

/**
 * Every one of your bots' seasons on one axis.
 *
 * A player is a stable rather than a competitor, so "how is my season going" is not one
 * number. The lines share a rating axis so they can be compared against each other, and
 * the gridlines are achromatic so the only thing a colour says is which bot it belongs to.
 */
function SeasonChart({
  lines,
}: {
  lines: readonly {
    name: string;
    accent: string | null;
    history: readonly number[] | null;
  }[];
}) {
  const values = lines.flatMap((line) => [...(line.history ?? [])]);
  if (values.length === 0) {
    return (
      <EmptyState
        title="No rating history yet"
        detail="One line per bot, each in that bot's own accent, lands here when the server keeps a rating over time."
      />
    );
  }

  const low = Math.min(...values);
  const high = Math.max(...values);
  const span = high - low || 1;
  // Round the axis outward to whole tens so the labels are numbers a person would say.
  const floor = Math.floor(low / 10) * 10;
  const ceiling = Math.ceil(high / 10) * 10;
  const W = 600;
  const H = 132;
  const PAD_L = 42;
  const PAD_B = 16;
  const y = (value: number) =>
    12 + ((ceiling - value) / (ceiling - floor || span)) * (H - 12 - PAD_B);

  return (
    <>
      <svg
        viewBox={`0 0 ${W} ${H}`}
        width="100%"
        className="block h-auto"
        role="img"
        aria-label={lines
          .map((line) =>
            line.history === null || line.history.length === 0
              ? `${line.name}: no history`
              : `${line.name}: ${line.history[0]} to ${line.history.at(-1)}`,
          )
          .join('; ')}
      >
        {[ceiling, floor].map((mark) => (
          <g key={mark}>
            <line
              x1={PAD_L}
              y1={y(mark)}
              x2={W}
              y2={y(mark)}
              className="stroke-arena-edge"
              strokeWidth={1}
            />
            <text
              x={4}
              y={y(mark) + 4}
              className="fill-arena-dim font-mono text-[9px]"
            >
              {mark}
            </text>
          </g>
        ))}
        {lines.map((line, index) => {
          const history = line.history ?? [];
          const drawn = line.accent
            ? playerAccent(line.accent, 'panel')
            : 'currentColor';
          const points = history
            .map(
              (value, index) =>
                `${PAD_L + (index / Math.max(1, history.length - 1)) * (W - PAD_L)},${y(value).toFixed(1)}`,
            )
            .join(' ');
          const last = points.split(' ').at(-1)?.split(',') ?? ['0', '0'];
          return (
            <g
              key={line.name}
              style={styleVariables({ '--player-accent': drawn })}
            >
              <polyline
                className="player-accent-stroke"
                points={points}
                fill="none"
                strokeWidth={2}
                strokeLinejoin="round"
                strokeDasharray={linePattern(index)}
              />
              <circle
                className="fill-arena-bg player-accent-stroke"
                cx={last[0]}
                cy={last[1]}
                r={4}
                strokeWidth={2.5}
              />
            </g>
          );
        })}
      </svg>
      <ul className="mt-1.5 flex flex-wrap gap-x-3.5 gap-y-1">
        {lines.map((line, index) => {
          const drawn = line.accent
            ? playerAccent(line.accent, 'panel')
            : 'currentColor';
          return (
            <li key={line.name} className="t-meta flex items-center gap-1.5">
              <svg
                aria-hidden
                width="14"
                height="4"
                viewBox="0 0 14 4"
                style={styleVariables({ '--player-accent': drawn })}
              >
                <line
                  className="player-accent-stroke"
                  x1="0"
                  y1="2"
                  x2="14"
                  y2="2"
                  strokeWidth="2"
                  strokeDasharray={linePattern(index)}
                />
              </svg>
              {line.name}
              <span className="val">{line.history?.at(-1)}</span>
            </li>
          );
        })}
      </ul>
    </>
  );
}

/**
 * One row's shape over the season.
 *
 * Achromatic for everybody else and in the bot's own accent for your rows, which is the
 * same rule the rule down the left of the row follows: the only colour is one a player
 * picked. Renders nothing without a history, which is every row today.
 */
function Sparkline({
  history,
  accent,
}: {
  history: readonly number[] | null;
  accent: string | null;
}) {
  if (history === null || history.length < 2) return null;
  const low = Math.min(...history);
  const high = Math.max(...history);
  // A flat history has no range to divide by; draw it down the middle rather than at the
  // top, which is where a zero-span normalisation would put it.
  const span = high - low;
  const points = history
    .map((value, index) => {
      const x = (index / (history.length - 1)) * 72;
      const y = span === 0 ? 10 : 18 - ((value - low) / span) * 16;
      return `${x.toFixed(1)},${y.toFixed(1)}`;
    })
    .join(' ');
  const drawn = accent
    ? playerAccent(accent, 'panel')
    : 'var(--color-arena-dim)';
  return (
    <svg
      className="block"
      width="72"
      height="20"
      viewBox="0 0 72 20"
      aria-hidden="true"
      style={styleVariables({ '--player-accent': drawn })}
    >
      <polyline
        className="player-accent-stroke"
        points={points}
        fill="none"
        strokeWidth="1.5"
        strokeLinejoin="round"
      />
    </svg>
  );
}

/** Colour is not identity: repeatable dash patterns keep equal accents distinguishable. */
function linePattern(index: number): string | undefined {
  return [undefined, '7 4', '2 3', '10 3 2 3'][index % 4];
}
