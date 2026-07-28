import { Fragment, useMemo, useState } from 'react';
import { Link } from 'react-router-dom';
import clsx from 'clsx';
import BotIdentity from '../components/BotIdentity';
import type { BotSummary } from '../api';
import { ErrorState, LoadingState } from '../components/StateView';
import { useAuth } from '../auth';
import { useBots, useMeta, useMyBots } from '../queries';

/**
 * The census of the game: every bot anyone has submitted, and the screen you pick one
 * worth fighting from.
 *
 * It is a **table**, and that is a reversal of the card grid this page used to be. The
 * reading task is the ladder's — is that rating near mine, has that thing actually been
 * played, who owns it — and cards put every value in a different place on every row, so
 * the eye has to re-find each one. A column of 24px chassis in their owners' accents is
 * recognisable before a single name is read; the grid made identity large and standing
 * invisible, which is backwards for a directory.
 *
 * Two things separate it from the ladder and are the reason it exists:
 *
 * - **It contains the unranked.** `/api/leaderboard` is the top 100 and only ever holds
 *   bots with a completed ranked set. `/api/bots` returns everything and attaches a
 *   whole-ladder `currentStanding` per bot, so rank #137 is correct here and unreachable
 *   on the ladder.
 * - **It knows what can be fought.** `activeVersion === null` means no built artifact,
 *   and `ChallengePanel` already refuses those. Fightability is a first-class column here
 *   and appears nowhere else in the product.
 *
 * Colour: the only saturated colour on this screen is `bot.accent`, in exactly two places
 * — the chip ring and the 2px rule on your rows. Ratings used to print in
 * `text-arena-accent`; burnt orange is material, never a signal, so a rating is now a
 * `.val` in `text-arena-text`. `--color-arena-ok`/`--color-arena-hot` appear nowhere:
 * there is no rise or fall in a directory, so the outcome exception is not spent.
 */

/**
 * Fields the design needs that `/api/bots` does not carry yet.
 *
 * Read off the response optionally rather than added to `schema.d.ts`, which is generated
 * from the server and must not be hand-edited. Production sends none of them, so every
 * read here is null and the screen says so instead of inventing a value. The day the
 * endpoint grows them, regenerating the client deletes this type and nothing else moves.
 */
interface RosterSeamFields {
  /** Seam: `lastMatchAt` on `BotSummaryResponse`. */
  lastMatchAt?: string;
}

/**
 * When this bot last fought — the real gap for "worth challenging".
 *
 * Nothing on the roster says it. `createdAt` is when the bot was *made*, which is why the
 * `new` sort means newest-created and why an abandoned month-one experiment is
 * indistinguishable here from something fighting hourly. Until `lastMatchAt` lands on
 * `BotSummaryResponse` this returns null on every row, the footnote admits it, and there
 * is no "active" pill pretending otherwise.
 */
function lastPlayed(bot: BotSummary): string | null {
  return (bot as BotSummary & RosterSeamFields).lastMatchAt ?? null;
}

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
  const { data: meta = null } = useMeta();
  const { user, loading: authLoading } = useAuth();
  const { data: myBots = null } = useMyBots(Boolean(user));
  const [query, setQuery] = useState('');
  const [sort, setSort] = useState<SortKey>('rank');
  const [fightableOnly, setFightableOnly] = useState(false);
  const [mineOnly, setMineOnly] = useState(false);

  const rules = meta?.gameRulesVersion ?? null;

  // Ownership joins on ids, not display names: `/api/bots` has no `isOwner`, and two
  // accounts can share a display name — so a name match would mark somebody else's rows
  // as yours. `useMyBots` is the only exact answer the API offers.
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
      if (fightableOnly && bot.activeVersion === null) return false;
      if (mineActive && !myIds.has(bot.id)) return false;
      if (needle === '') return true;
      return (
        bot.name.toLowerCase().includes(needle) ||
        bot.owner.toLowerCase().includes(needle)
      );
    });
  }, [bots, fightableOnly, mineActive, myIds, query]);

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
  const hasLastPlayed = (bots ?? []).some((bot) => lastPlayed(bot) !== null);

  return (
    <div className="mx-auto max-w-4xl">
      {/* The header block renders in every state, loading included: the title and the
          lede say nothing they cannot already know. The census line and the controls do
          not — a count of nothing is noise and a pressed filter over no data is a dead
          control. */}
      <p className="lab mb-2">Directory{rules === null ? '' : ` · rules ${rules}`}</p>
      <h1 className="type-display mb-2 text-[30px]">Every bot</h1>
      <p className="t-body mb-4 max-w-[62ch] text-arena-dim">
        Every bot anyone has submitted. Rank, rating and sets belong to the active
        ladder; a bot with no built version cannot be fought.
      </p>

      {bots !== null && bots.length > 0 && (
        <p className="t-meta mb-4">
          <span className="val text-arena-text">{bots.length}</span> bots ·{' '}
          <span className="val text-arena-text">
            {bots.filter((bot) => bot.currentStanding).length}
          </span>{' '}
          ranked ·{' '}
          <span className="val text-arena-text">
            {bots.filter((bot) => bot.activeVersion).length}
          </span>
          <span className="hidden sm:inline"> with a built version</span>
          <span className="sm:hidden"> built</span>
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
        <EmptyRoster signedIn={user !== null} />
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
                <Control
                  pressed={fightableOnly}
                  onClick={() => setFightableOnly(!fightableOnly)}
                  title="A bot with no built artifact cannot be fought."
                >
                  can fight
                </Control>
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
                className="t-body min-w-0 grow basis-full rounded-[3px] border border-arena-edge bg-arena-bg px-3 py-1.5 text-arena-text placeholder:text-arena-dim focus:border-arena-edge2 focus:outline-none sm:basis-0"
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
                      {noMatchLine(query, fightableOnly, mineActive)}
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
                            {rules !== null && (
                              <span className="hidden sm:inline">
                                {' '}
                                · no set on rules {rules}
                              </span>
                            )}
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
                          )}
                          style={
                            mine && bot.accent
                              ? { boxShadow: `inset 2px 0 0 ${bot.accent}` }
                              : undefined
                          }
                        >
                          {standing ? standing.rank : '—'}
                        </td>
                        <td className="p-2 align-middle">
                          <Link
                            to={`/bots/${bot.slug}`}
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
                          {/* A link, not a form. Composing a match needs your bot, a map
                              and a ranked/unranked choice, and that composer already
                              exists on the bot page — reproducing it per row would fork
                              it, and would bake a 1v1 shape into a directory. The
                              directory hands off; the bot page composes. */}
                          {bot.activeVersion && (
                            <Link className="btn inline-block" to={`/bots/${bot.slug}#challenge`}>
                              challenge
                            </Link>
                          )}
                        </td>
                      </tr>
                    </Fragment>
                  );
                })}
              </tbody>
            </table>

            {/* The honest account of the blank columns. A directory that shows a dormant
                bot and a busy one identically should say so rather than let the reader
                diagnose it. */}
            <p className="t-micro mt-2.5">
              {ladderEmpty &&
                `No bot has completed a ranked set${rules === null ? '' : ` on rules ${rules}`} yet. `}
              {hasLastPlayed
                ? 'No win–loss record reaches this endpoint: it is one request per bot.'
                : 'No record and no last-played date reach this endpoint, so a dormant bot and a busy one read the same.'}
            </p>
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
function noMatchLine(query: string, fightable: boolean, mine: boolean) {
  const needle = query.trim();
  const active: string[] = [];
  if (fightable) active.push('can fight');
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
function EmptyRoster({ signedIn }: { signedIn: boolean }) {
  if (!signedIn) {
    return (
      <section className="panel pad">
        <p className="lab mb-2">Empty roster</p>
        <h2 className="type-display mb-2 max-w-[34ch] text-[21px] text-arena-text">
          Nobody has submitted a bot here yet.
        </h2>
        <p className="t-meta">
          The roster fills as players ship their first generation.{' '}
          <Link to="/login" className="text-arena-accent underline">
            Sign in
          </Link>
        </p>
      </section>
    );
  }
  return (
    <section className="panel pad">
      <p className="lab mb-2">Empty roster</p>
      <h2 className="type-display mb-3 max-w-[34ch] text-[21px] text-arena-text">
        No bots yet — the roster starts with yours.
      </h2>
      <pre className="term max-w-[46ch] text-arena-text">
        <span className="text-arena-dim">$</span> nilbots new MyBot
      </pre>
      <p className="t-meta mt-[11px]">
        Your{' '}
        <Link to="/garage" className="text-arena-accent underline">
          garage
        </Link>{' '}
        walks the whole sequence.
      </p>
    </section>
  );
}

/**
 * A sort or filter control.
 *
 * `.btn`/`.btn-on` are the shared control pair, so pressed, disabled and hover are one
 * implementation rather than four utilities re-typed per page.
 */
function Control({
  children,
  pressed,
  title,
  onClick,
}: {
  children: React.ReactNode;
  pressed: boolean;
  title?: string;
  onClick: () => void;
}) {
  return (
    <button
      type="button"
      onClick={onClick}
      title={title}
      aria-pressed={pressed}
      className={clsx('btn', pressed && 'btn-on')}
    >
      {children}
    </button>
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
        'lab border-b border-arena-edge px-2 pb-2',
        numeric ? 'text-right' : 'text-left',
        className,
      )}
    >
      {children}
    </th>
  );
}
