import { Fragment, useMemo } from 'react';
import { Link, useLocation, useSearchParams } from 'react-router-dom';
import clsx from 'clsx';
import BotIdentity from '../components/BotIdentity';
import type { BotSummary } from '../api';
import { ErrorState, LoadingState } from '../components/StateView';
import Th from '../components/TableHeader';
import Control from '../../components/ToggleButton';
import { useAuth } from '../auth';
import {
  useBots,
  useMyBots,
} from '../queries';
import { playerAccent } from '../../presentation/playerAccent';
import { styleVariables } from '../../presentation/styleVariables';

/**
 * Read-only index for the retired Duel roster. Historical names and standings remain
 * intact here; current Arc Relay concepts and actions deliberately do not leak into it.
 */

/*
 * Three things are deliberately *not* shaped here, and each is a refusal rather than an
 * omission:
 *
 * - **Win–loss per bot.** `/api/bots/{botId}/stats` is one bot per request, so a 214-row
 *   roster is 214 requests. No column, no seam. (Seam if it is ever wanted:
 *   `wins`/`losses` on `BotSummaryResponse`.)
 * - **Movement and trend.** Blocked identically to the ladder — no rank snapshot, no
 *   per-bot rating history — but also out of scope: movement is the ladder's subject, and
 *   a directory that grows a movement column becomes a second ladder.
 * - **A rules switcher.** `/api/bots` resolves `currentStanding` for the active rules
 *   version only, so a switcher could change the label and never re-rank a single row.
 *   A control that lies is worse than one that is missing. (Seam: `?rules=` on
 *   `/api/bots`.)
 */

type SortKey = 'rank' | 'new' | 'name';

/** Chassis (24px) + its ring border and padding + the chip's own gap. */
const SUB_LINE_INDENT = 'ml-[41px]';

export default function BotsPage() {
  const { data: bots = null, error, refetch } = useBots();
  const { user, loading: authLoading } = useAuth();
  const { data: myBots = null } = useMyBots(Boolean(user));
  const location = useLocation();
  const [params, setParams] = useSearchParams();
  const query = params.get('q') ?? '';
  const sortValue = params.get('sort');
  const sort: SortKey =
    sortValue === 'new' || sortValue === 'name' ? sortValue : 'rank';
  const mineOnly = params.get('mine') === '1';
  const setParam = (key: string, value: string | null) => {
    const next = new URLSearchParams(params);
    if (value === null || value === '') next.delete(key);
    else next.set(key, value);
    setParams(next, { replace: true });
  };
  const setQuery = (value: string) => setParam('q', value);
  const setSort = (value: SortKey) =>
    setParam('sort', value === 'rank' ? null : value);
  const setMineOnly = (value: boolean) =>
    setParam('mine', value ? '1' : null);
  const returnTo = `${location.pathname}${location.search}`;

  // Ownership joins on ids, never display names.
  const myIds = useMemo(
    () => new Set((myBots ?? []).map((bot) => bot.id)),
    [myBots],
  );
  const ownedOnRoster = useMemo(
    () => (bots ?? []).filter((bot) => myIds.has(bot.id)).length,
    [bots, myIds],
  );
  // A filter left pressed over an empty selection is a table nobody can get back out of.
  // It stops applying with the control that sets it (the ladder's guard).
  const mineActive = mineOnly && ownedOnRoster > 0;

  const shown = useMemo(() => {
    const needle = query.trim().toLowerCase();
    return (bots ?? []).filter((bot) => {
      if (mineActive && !myIds.has(bot.id)) return false;
      if (needle === '') return true;
      return (
        bot.name.toLowerCase().includes(needle) ||
        bot.owner.toLowerCase().includes(needle)
      );
    });
  }, [
    bots,
    mineActive,
    myIds,
    query,
  ]);

  // `rank` is standings ascending, then the unranked by newest-created. Ties break by
  // name in every mode: competition ranking means equal ratings share a rank, so payload
  // order would otherwise wobble between fetches and rows would swap under the reader.
  const ordered = useMemo(() => {
    const list = [...shown];
    if (sort === 'name') return list.sort((a, b) => a.name.localeCompare(b.name));
    if (sort === 'new') return list.sort(byNewest);
    return list.sort((a, b) => {
      const left = a.currentStanding ?? null;
      const right = b.currentStanding ?? null;
      if (left && right) return left.rank - right.rank || a.name.localeCompare(b.name);
      if (left) return -1;
      if (right) return 1;
      return byNewest(a, b);
    });
  }, [shown, sort]);

  // The band partitions ranked from unranked, and that partition only means anything
  // under `rank` — under `new` and `name` the two groups interleave, so it must not
  // render. Nor when everything on screen is unranked: a band above every row reads as a
  // lie about the rows above it, of which there are none.
  const bandAt = useMemo(() => {
    if (sort !== 'rank') return -1;
    const first = ordered.findIndex((bot) => !bot.currentStanding);
    return first > 0 ? first : -1;
  }, [ordered, sort]);

  const ladderEmpty =
    bots !== null && bots.length > 0 && !bots.some((bot) => bot.currentStanding);

  return (
    <div className="mx-auto max-w-4xl">
      {/* The header block renders in every state, loading included: the title and the
          lede say nothing they cannot already know. The census line and the controls do
          not — a count of nothing is noise and a pressed filter over no data is a dead
          control. */}
      <p className="lab mb-2">Bot directory</p>
      <p className="lab mb-2 text-arena-material">Legacy archive · read only</p>
      <h1 className="type-display mb-2 text-[30px]">Historical Duel records</h1>
      <p className="t-body mb-4 max-w-[62ch] text-arena-dim">
        Inspect the retired Duel roster, generations, standings and match records exactly
        as history recorded them. Creation, submission and admission are no longer offered.
      </p>

      {bots !== null && bots.length > 0 && (
        <p className="t-meta mb-4">
          <span className="val text-arena-text">{bots.length}</span> bots ·{' '}
          <span className="val text-arena-text">
            {bots.filter((bot) => bot.currentStanding).length}
          </span>{' '}
          ranked
          <span className="hidden sm:inline">
            {' · '}
            <span className="val text-arena-text">
              {new Set(bots.map((bot) => bot.owner)).size}
            </span>{' '}
            owners
          </span>
        </p>
      )}

      {error !== null && <ErrorState error={error} onRetry={() => void refetch()} />}
      {error === null && bots === null && <LoadingState label="Loading the roster…" />}
      {/* Which of the two empty sentences is true depends on the session, so it waits for
          it: showing the visitor's copy for a beat and then swapping it is a page that
          appears to change its mind about who you are. */}
      {bots !== null && bots.length === 0 && !authLoading && (
        <EmptyRoster />
      )}

      {bots !== null && bots.length > 0 && (
        <section className="panel">
          <div className="pad flex flex-wrap items-center justify-between gap-2.5 pb-0">
            <p className="lab">Roster · {bots.length} bots</p>
            <div className="flex flex-wrap items-center gap-1.5">
              <div className="flex flex-wrap items-center gap-1.5" role="group" aria-label="Sort the roster">
                <Control pressed={sort === 'rank'} onClick={() => setSort('rank')}>
                  rank
                </Control>
                <Control pressed={sort === 'new'} onClick={() => setSort('new')}>
                  new
                </Control>
                <Control pressed={sort === 'name'} onClick={() => setSort('name')}>
                  name
                </Control>
              </div>
              <div className="flex flex-wrap items-center gap-1.5" role="group" aria-label="Filter the roster">
                {/* Only when you are signed in and own something on this roster: a
                    filter that can only ever empty the table is not a control. */}
                {ownedOnRoster > 0 && (
                  <Control pressed={mineActive} onClick={() => setMineOnly(!mineActive)}>
                    mine
                  </Control>
                )}
              </div>
            </div>
          </div>

          <div className="pad pt-2.5">
            <div className="mb-2.5 flex flex-wrap items-center gap-2">
              <input
                type="search"
                value={query}
                onChange={(event) => setQuery(event.target.value)}
                placeholder="Filter by bot or owner…"
                aria-label="Filter the roster by bot or owner"
                className="field min-w-0 grow basis-full sm:basis-0"
              />
              <span className="val ml-auto">
                {ordered.length}/{bots.length}
              </span>
            </div>

            {/* A phone drops columns; the table never scrolls sideways, because a table
                that has to be dragged is a table nobody reads. What survives is what a
                directory entry *is* — rank, identity, rating — and owner, gen and sets
                fold into the sub-line under the name. `table-fixed` is what makes the
                identity column truncate instead of widening the page. */}
            <table className="t-body w-full table-fixed border-collapse">
              <thead>
                <tr>
                  <Th className="w-[42px]">#</Th>
                  <Th>Bot</Th>
                  <Th className="hidden w-32 sm:table-cell">Owner</Th>
                  {/* The brief's 70px is the width of `gen-10`; the `no build` pill is
                      wider than that and `.pill` does not wrap, so the column is sized to
                      the widest thing it holds rather than clipping it into Rating. */}
                  <Th className="hidden w-[92px] md:table-cell">Gen</Th>
                  <Th className="w-20" numeric>
                    Rating
                  </Th>
                  <Th className="hidden w-14 sm:table-cell" numeric>
                    Sets
                  </Th>
                  <Th className="hidden w-[86px] sm:table-cell">
                    <span className="sr-only">Fight</span>
                  </Th>
                </tr>
              </thead>
              <tbody>
                {/* Filtered to nothing is not empty, and it names the control that did
                    it. Replacing the panel with a page-level empty state would take the
                    controls away with it, stranding the reader behind their own filter. */}
                {ordered.length === 0 && (
                  <tr>
                    <td colSpan={7} className="py-4 text-arena-dim">
                      {noMatchLine(query, mineActive)}
                    </td>
                  </tr>
                )}
                {ordered.map((bot, index) => {
                  const mine = myIds.has(bot.id);
                  const standing = bot.currentStanding ?? null;
                  const gen = bot.activeVersion
                    ? `gen-${bot.activeVersion.versionNumber}`
                    : null;
                  const owner = mine ? 'you' : bot.owner;
                  const rail =
                    mine && bot.accent
                      ? playerAccent(bot.accent, 'panel')
                      : null;
                  return (
                    <Fragment key={bot.id}>
                      {index === bandAt && (
                        <tr>
                          {/* The mock's own band: a dashed rule and a condensed
                              uppercase line at full colspan, carrying a fact the
                              payload actually has rather than an invented rule. */}
                          <td
                            colSpan={7}
                            className="lab border-t border-dashed border-arena-edge pt-2.5 pb-1.5"
                          >
                            Unranked · {ordered.length - bandAt} bots
                          </td>
                        </tr>
                      )}
                      <tr
                        className={clsx(
                          'border-b border-arena-edge last:border-b-0',
                          mine && 'bg-arena-text/[0.028]',
                        )}
                      >
                        {/* A rank is display type rather than mono: it is the bot's
                            position, not a value the engine computed. The rule down the
                            left is the ladder's marking and this bot's own accent — the
                            only chroma the table spends. */}
                        <td
                          className={clsx(
                            'type-display tabular p-2 align-middle text-[22px]',
                            standing ? 'text-arena-text' : 'text-arena-dim',
                            rail && 'player-accent-rail',
                          )}
                          style={
                            rail
                              ? styleVariables({ '--player-accent': rail })
                              : undefined
                          }
                        >
                          {standing ? standing.rank : '—'}
                        </td>
                        <td className="p-2 align-middle">
                          <Link
                            to={`/archive/bots/${bot.slug}`}
                            state={{
                              returnTo,
                              returnLabel: 'All bots',
                            }}
                            className="block min-w-0 transition-opacity hover:opacity-80"
                          >
                            <BotIdentity
                              name={bot.name}
                              accent={bot.accent}
                              lookId={bot.lookId}
                              size="sm"
                              className="w-full"
                            />
                            {/* The dropped columns, folded. Same treatment as
                                `IdentityChip`'s own `sub` slot, so if this line ever
                                becomes unconditional it moves there and looks identical. */}
                            <span
                              className={clsx(
                                't-micro block truncate tracking-[0.04em] [font-stretch:84%] sm:hidden',
                                SUB_LINE_INDENT,
                              )}
                            >
                              {owner} · {gen ?? 'no build'} ·{' '}
                              {standing ? standing.rankedSets : 'no sets'}
                            </span>
                          </Link>
                        </td>
                        <td className="hidden p-2 align-middle sm:table-cell">
                          {/* The cheapest possible "show me this player's stable", and
                              it needs nothing the payload lacks. It filters by the real
                              display name even when the cell reads "you" — `owner` is a
                              string with no id and no profile route, so it can be
                              filtered by and not linked to. */}
                          <button
                            type="button"
                            onClick={() => setQuery(bot.owner)}
                            title={`Filter the roster to ${bot.owner}`}
                            className="block w-full truncate text-left text-arena-dim transition-colors hover:text-arena-text"
                          >
                            {owner}
                          </button>
                        </td>
                        <td className="hidden p-2 align-middle md:table-cell">
                          {gen === null ? (
                            // `activeVersion === null` conflates never built, building
                            // now and every build failed — `latestVersion.status` exists
                            // for your own bots only. So the pill says this much and
                            // nothing more precise.
                            <span className="pill">no build</span>
                          ) : (
                            <span className="val">{gen}</span>
                          )}
                        </td>
                        <td
                          className={clsx(
                            'val p-2 text-right align-middle whitespace-nowrap',
                            standing && 'text-arena-text',
                          )}
                        >
                          {/* The server already rounds (`LadderStandings` calls
                              `Math.Round`), so this is the ladder's own number rather
                              than a second opinion about it. */}
                          {standing ? standing.rating : '—'}
                        </td>
                        <td className="val hidden p-2 text-right align-middle whitespace-nowrap sm:table-cell">
                          {standing ? standing.rankedSets : 0}
                        </td>
                        <td className="hidden p-2 text-right align-middle sm:table-cell">
                          {/* The directory opens the shared Arena overlay; the composer
                              exists once rather than being copied into every row. */}
                          <span className="pill">archived</span>
                        </td>
                      </tr>
                    </Fragment>
                  );
                })}
              </tbody>
            </table>

            {ladderEmpty && (
              <p className="t-micro mt-2.5">
                No historical ranked records are available.
              </p>
            )}
          </div>
        </section>
      )}
    </div>
  );
}

/** Newest-created first, ties by name so the order is stable across fetches. */
function byNewest(a: BotSummary, b: BotSummary) {
  return (
    new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime() ||
    a.name.localeCompare(b.name)
  );
}

/**
 * Why the table came back empty, naming the control responsible.
 *
 * "No results" leaves the reader to work out which of three things they pressed did it.
 */
function noMatchLine(query: string, mine: boolean) {
  const needle = query.trim();
  const active: string[] = [];
  if (mine) active.push('mine');
  const filters = active.join(' and ');
  if (active.length === 0)
    return needle === '' ? 'No bot to show.' : `No bot matches “${needle}”.`;
  if (needle === '') return `No bot is left with ${filters} on.`;
  return `No bot matches “${needle}” with ${filters} on.`;
}

/**
 * A roster with nothing in it — two different true sentences, so two states.
 *
 * Signed in, this is First Run's territory arriving through a side door, and it should
 * read as an invitation rather than as absence. It borrows First Run's **first step
 * only**: one command and a link, never a second copy of that screen. The command is the
 * one the CLI actually answers to — `nilbots new` makes the name a C# type, so `my-bot`
 * fails before a file is written.
 */
function EmptyRoster() {
  return (
    <section className="panel pad">
      <p className="lab mb-2">Empty roster</p>
      <h2 className="type-display mb-2 max-w-[34ch] text-[21px] text-arena-text">
        No historical Duel records were retained.
      </h2>
      <p className="t-meta">Arc Relay entrants live in the current product, not in this archive.</p>
      <Link to="/relay" className="btn mt-3 inline-flex">Open Arc Relay</Link>
    </section>
  );
}

/**
 * A sort or filter control.
 *
 * `.btn`/`.btn-on` are the shared control pair, so pressed, disabled and hover are one
 * implementation rather than four utilities re-typed per page.
 */
