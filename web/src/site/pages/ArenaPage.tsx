import { Fragment, type ReactNode } from 'react';
import { Link, useLocation, useSearchParams } from 'react-router-dom';
import clsx from 'clsx';
import Matchup from '../components/Matchup';
import { ErrorState, LoadingState } from '../components/StateView';
import Th from '../components/TableHeader';
import LiveStatus, { LiveDot } from '../../components/LiveStatus';
import { type Me, type MatchLive, type MatchSummary } from '../api';
import { useMatchLive, useMatches, useMe, useMeta } from '../queries';
import { playerAccent } from '../../presentation/playerAccent';
import { styleVariables } from '../../presentation/styleVariables';

/**
 * Watch — the one screen a spectator reaches without already knowing what they came for.
 *
 * It exists so that somebody who wants *something to watch* is inside a fight within one
 * tap, and — when nothing is broadcasting — is handed the most recent replay instead. So
 * the live rail leads and the explanatory prose does not appear in the content state at
 * all: "why is there no winner on that row" is a question you only have while looking at
 * a row, so its answer is one caption on the rail. The full version lives in the empty
 * state, which is the one state where prose *is* the content.
 *
 * **Result secrecy is a server invariant, not a presentation choice.** `/api/matches`
 * withholds winner, end reason and final health until a match has finished broadcasting
 * (`MatchPublicProjection.BroadcastSafe`), so nothing here reconstructs an outcome from
 * what is left behind. Three rules follow, and every one of them is load-bearing:
 *
 * - `winnerSlot === null` is never read on its own — it means "drawn" on a revealed match
 *   and "not telling you" on a broadcasting one. Every read goes through
 *   `revealedOutcome()`.
 * - Nothing sorts, groups or counts by outcome. An ordering is a channel: a "wins" group
 *   with a live match in it announces the result the payload withheld.
 * - A live card carries no timestamp. `completedAt` is *not* redacted, so a broadcasting
 *   match will hand you a completion time — and "finished 40 s ago" beside a fight the
 *   reader is about to watch undercuts the broadcast for nothing.
 */
export default function ArenaPage() {
  const { data: meta = null } = useMeta();
  const { data: me = null } = useMe();

  // Filters live in the URL so a filtered feed can be linked and reloaded.
  const [params, setParams] = useSearchParams();
  const map = params.get('map') ?? '';
  const ranked = params.get('ranked') ?? '';
  const filtered = map !== '' || ranked !== '';

  const setFilter = (key: string, value: string) => {
    const next = new URLSearchParams(params);
    if (value === '') next.delete(key);
    else next.set(key, value);
    setParams(next, { replace: true });
  };
  const clearFilters = () => setParams(new URLSearchParams(), { replace: true });

  // The filters are part of the key, so changing one is a different query rather than a
  // refetch — no manual reset of pages, and switching back finds the old feed cached.
  const feed = useMatches({ bot: '', map, ranked });
  const matches =
    feed.data === undefined ? undefined : dedupeById(feed.data.pages.flat());

  // `broadcasting` is the server's own flag for the window where a match has run and its
  // result is still sealed — which is exactly the set worth putting first, and the set
  // whose outcome must not appear anywhere on this page.
  const live = (matches ?? []).filter((match) => match.broadcasting);
  const rest = (matches ?? []).filter((match) => !match.broadcasting);
  // Queued and running matches get a count, never a card: there is nothing to watch yet,
  // and a card promises there is. `broadcasting` is `Completed && !revealed`, so these two
  // sets never overlap with the rail's.
  const fighting = rest.filter((match) => match.status === 'Running').length;
  const queued = rest.filter((match) => match.status === 'Pending').length;
  // With nothing broadcasting, the newest finished match leads instead. `/api/matches`
  // orders by `createdAt` descending, so the first one carrying a result is it.
  const lastFinished =
    live.length > 0 ? null : (rest.find((match) => revealedOutcome(match) !== null) ?? null);
  // Whatever is featured above is not repeated below: a match belongs in one place at a
  // time, and a live row would announce itself twice on the same screen.
  const rows = rest.filter((match) => match.id !== lastFinished?.id);

  return (
    <div className="mx-auto flex max-w-4xl flex-col gap-3.5">
      {/* The title block renders in all four states, so the page has an identity while it
          loads and while it fails. "The arena" is a place, not a state, and does not
          rewrite itself on a 2.5 s poll — the count beside it is the part that moves,
          which is exactly what a `.pill` is for. */}
      <header>
        <p className="lab mb-2">Watch</p>
        <div className="flex flex-wrap items-baseline gap-x-3 gap-y-1.5">
          <h1 className="type-display text-[30px]">The arena</h1>
          {matches !== undefined && (
            <span className="pill">
              {live.length === 0 ? 'quiet' : `${live.length} live`}
            </span>
          )}
        </div>
      </header>

      {feed.isError ? (
        <ErrorState error={feed.error} onRetry={() => void feed.refetch()} />
      ) : feed.isPending || matches === undefined ? (
        // No filters while there is nothing to filter.
        <LoadingState label="Finding matches…" />
      ) : matches.length === 0 && !filtered ? (
        <NeverFought />
      ) : (
        <>
          <LiveRail
            live={live}
            lastFinished={lastFinished}
            fighting={fighting}
            queued={queued}
            filtered={filtered}
          />

          <section className="panel">
            <div className="pad flex flex-wrap items-center gap-x-2.5 gap-y-2">
              <p className="lab mr-auto">
                {filtered ? 'Matches in this filter' : 'Every match'}
              </p>
              {/* Controls sit in the panel head, right of the label, where somebody who
                  *does* know what they came for will look. */}
              <select
                value={map}
                onChange={(event) => setFilter('map', event.target.value)}
                aria-label="Filter by map"
                className={selectClass}
              >
                <option value="">any map</option>
                {/* `/api/meta` is read for this and nothing else. It would also give a
                    map's theme and size, and neither is a fact about the fight. */}
                {(meta?.maps ?? []).map((entry) => (
                  <option key={entry.id} value={entry.id}>
                    {entry.id}
                  </option>
                ))}
              </select>
              <div
                className="flex flex-wrap items-center gap-1.5"
                role="group"
                aria-label="Ranked or unranked"
              >
                {RANKED_CHOICES.map((choice) => (
                  <button
                    key={choice.value}
                    type="button"
                    onClick={() => setFilter('ranked', choice.value)}
                    aria-pressed={ranked === choice.value}
                    className={clsx('btn', ranked === choice.value && 'btn-on')}
                  >
                    {choice.label}
                  </button>
                ))}
              </div>
              {filtered && (
                <button type="button" onClick={clearFilters} className="btn">
                  clear
                </button>
              )}
            </div>

            <div className="px-3.5 pb-3.5">
              {/* A table, not a list of cards: a feed of homogeneous records is scanned by
                  column — who, what happened, when — and cards put every value in a
                  different place on every row. `table-fixed` is what makes the names
                  truncate instead of widening the page. */}
              <table className="t-body w-full table-fixed border-collapse">
                <thead>
                  <tr>
                    <Th className="w-[54%] sm:w-[36%]">Matchup</Th>
                    <Th className="w-[46%] sm:w-[22%]">Result</Th>
                    <Th className="hidden sm:table-cell sm:w-[16%]">Ended</Th>
                    <Th className="hidden sm:table-cell sm:w-[7%]">Set</Th>
                    <Th className="hidden sm:table-cell sm:w-[11%]">Map</Th>
                    <Th className="hidden sm:table-cell sm:w-[8%]">When</Th>
                  </tr>
                </thead>
                <tbody>
                  {rows.length === 0 ? (
                    <tr>
                      <td colSpan={6} className="py-4 text-arena-dim">
                        {matches.length === 0 ? (
                          <span className="flex flex-wrap items-center gap-2.5">
                            No match fits those filters.
                            <button
                              type="button"
                              onClick={clearFilters}
                              className="btn"
                            >
                              clear
                            </button>
                          </span>
                        ) : lastFinished !== null ? (
                          filtered
                            ? 'The featured replay is the only match in this filter.'
                            : 'The featured replay is the whole arena so far.'
                        ) : filtered ? (
                          'Nothing in this filter has finished yet — it is all still playing out.'
                        ) : (
                          'Nothing has finished yet — everything in the arena is still playing out.'
                        )}
                      </td>
                    </tr>
                  ) : (
                    rows.map((match) => (
                      <FeedRow key={match.id} match={match} me={me} />
                    ))
                  )}
                </tbody>
              </table>

              {feed.hasNextPage ? (
                <button
                  type="button"
                  onClick={() => void feed.fetchNextPage()}
                  disabled={feed.isFetchingNextPage}
                  className="btn mt-3"
                >
                  {feed.isFetchingNextPage ? 'Loading…' : 'Older matches'}
                </button>
              ) : (
                rows.length > 0 && (
                  <p className="t-micro mt-3">That is the whole arena so far.</p>
                )
              )}
            </div>
          </section>
        </>
      )}
    </div>
  );
}

/**
 * A select wearing the shared control, so the filter row is one family with the ranked
 * buttons beside it rather than two. `bg-arena-bg` overrides `.btn`'s transparency —
 * a select's popup takes its ground from the element on most platforms — and `max-w-full`
 * is what stops one long bot name widening the page past the phone.
 */
const selectClass = 'btn max-w-full bg-arena-bg';

const RANKED_CHOICES = [
  { value: '', label: 'all' },
  { value: 'true', label: 'ranked' },
  { value: 'false', label: 'unranked' },
] as const;

/**
 * How many broadcasts may hold a presentation clock at once.
 *
 * `presentationTick` lives only on `/api/matches/{id}/live` — one request per match, every
 * 1.5 s — so only rail cards ever subscribe, never feed rows. Past this many concurrent
 * broadcasts the tick drops from *every* card rather than some, because a rail where three
 * cards count and four do not reads as four broken cards.
 */
const LIVE_CLOCK_BUDGET = 6;

/**
 * The lead: what there is to watch this second.
 *
 * When nothing is broadcasting this deliberately does not collapse. A spectator who
 * arrives at a quiet arena still needs a way in, so the newest match carrying a revealed
 * result takes the same slot in the same card shape — the verdict where the tick was, and
 * `replay →` instead of `watch →`. A replay is the thing this product actually has.
 *
 * The rail is *live within what is loaded*, which is exact only because the feed is
 * `createdAt DESC` and a broadcast begins seconds after a match is created. The day
 * matches sit queued for minutes that stops being true, and the fix is a
 * `?broadcasting=true` filter on the endpoint rather than a deeper scan on this side.
 */
function LiveRail({
  live,
  lastFinished,
  fighting,
  queued,
  filtered,
}: {
  live: MatchSummary[];
  lastFinished: MatchSummary | null;
  fighting: number;
  queued: number;
  filtered: boolean;
}) {
  const featured = live.length > 0 ? live : lastFinished === null ? [] : [lastFinished];
  const activity = [
    fighting > 0 ? `${fighting} fighting` : null,
    queued > 0 ? `${queued} queued` : null,
  ].filter((part) => part !== null);
  if (featured.length === 0 && activity.length === 0) return null;

  return (
    <section className="panel pad">
      <div className="mb-2.5 flex flex-wrap items-baseline justify-between gap-x-3 gap-y-1">
        <p className="lab">
          {live.length > 0
            ? `Live now · ${live.length} ${live.length === 1 ? 'match' : 'matches'}`
            : lastFinished !== null
              ? 'Nothing live · last match'
              : // Never claim the whole arena is quiet when only this filter is.
                filtered
                ? 'Nothing live in this filter'
                : 'Nothing live right now'}
        </p>
        {activity.length > 0 && <p className="t-micro">{activity.join(' · ')}</p>}
      </div>

      {featured.length > 0 && (
        <div className="grid gap-2.5 [grid-template-columns:repeat(auto-fit,minmax(260px,1fr))]">
          {featured.map((match) => (
            <FeatureCard
              key={match.id}
              match={match}
              followClock={match.broadcasting && live.length <= LIVE_CLOCK_BUDGET}
            />
          ))}
        </div>
      )}

      {/* One sentence, attached to the place the question arises. */}
      <p className="t-micro mt-2.5">
        {live.length > 0
          ? 'No result is shown until a broadcast has played all the way out.'
          : lastFinished !== null
            ? 'The newest completed match leads here whenever there is nothing live.'
            : 'Queued and fighting matches appear here once their broadcast begins.'}
      </p>
    </section>
  );
}

/**
 * One match, big enough to choose from: who is fighting, where, and how far in.
 *
 * The tick is a counting number and never a proportion. `MatchLiveResponse.totalTicks` is
 * `endTick + 1` and is withheld while broadcasting, correctly — "39 ticks total" is a
 * partial spoiler, because a 12-tick match is a rout. A progress bar here would need the
 * server to break its own invariant, so there is no denominator and there must not be one.
 */
function FeatureCard({
  match,
  followClock,
}: {
  match: MatchSummary;
  followClock: boolean;
}) {
  const location = useLocation();
  const live = match.broadcasting;
  const outcome = revealedOutcome(match);
  const clock = useMatchLive(followClock ? match.id : undefined).data;
  const ranked = rankedLabel(match);

  return (
    <Link
      to={`/matches/${match.id}`}
      state={{
        returnTo: `${location.pathname}${location.search}`,
        returnLabel: 'Watch',
      }}
      className="panel group flex flex-col gap-3 bg-arena-raise px-3 py-3 transition-colors hover:border-arena-edge2"
    >
      <div className="flex flex-wrap items-center justify-between gap-2">
        {live ? <LiveStatus /> : <span className="lab">Last match</span>}
        {/* A map id is a machine identifier, so it is set the way every other one is. */}
        <span className="val">{match.mapId}</span>
      </div>

      <Matchup
        participants={match.participants}
        winnerSlot={outcome?.winnerSlot ?? null}
        size="sm"
        layout="stack"
        showOwners
      />

      <div className="flex flex-wrap items-baseline gap-x-3 gap-y-1">
        {live ? (
          <span className="flex items-baseline gap-2">
            <span className="lab">Tick</span>
            <span className="val">{broadcastProgress(clock) ?? '—'}</span>
          </span>
        ) : (
          <Verdict match={match} outcome={outcome} />
        )}
        {ranked !== null && <span className="t-micro">{ranked}</span>}
        <span className="t-micro ml-auto transition-colors group-hover:text-arena-text">
          {live ? 'watch →' : 'replay →'}
        </span>
      </div>

      {/* Only ever on the finished card: a live one carries no timestamp. */}
      {!live && (
        <MetaLine
          items={[
            ...endedItems(outcome),
            <time key="when" dateTime={match.completedAt ?? match.createdAt}>
              {agoLabel(match.completedAt ?? match.createdAt)}
            </time>,
          ]}
        />
      )}
    </Link>
  );
}

/**
 * One line of the feed.
 *
 * The row is the link — the whole row, through an `::after` over a positioned `<tr>` — so
 * there is one tab stop and one target well over 44 px, and no second interactive element
 * inside a row to be swallowed by it. The identity chips cannot be links themselves; see
 * `Matchup`.
 *
 * No win/loss glyph and no colour on the verdict. The design colours win and loss on the
 * *bot page*, where every row is one bot's rise or fall; here a match between two
 * strangers has a winner, not a rise, and the winner is already marked by weight in the
 * chip beside it.
 */
function FeedRow({ match, me }: { match: MatchSummary; me: Me | null }) {
  const location = useLocation();
  const outcome = revealedOutcome(match);
  const when = match.completedAt ?? match.createdAt;
  const ended = endedItems(outcome);
  const set = setLabel(match);
  // Yours is marked by a rule in that bot's own accent — the only saturated colour this
  // page spends, and always somebody's choice rather than a system opinion. The join is
  // the display-name snapshot, which is the only one the summary offers and which
  // mis-marks on a rename or a duplicate name; the ladder already accepts the same trade.
  const mine =
    me === null
      ? null
      : (match.participants.find(
          (participant) => participant.ownerDisplayNameSnapshot === me.displayName,
        ) ?? null);
  const rail = mine?.accentSnapshot
    ? playerAccent(mine.accentSnapshot, 'panel')
    : null;

  return (
    <tr
      className={clsx(
        'relative border-b border-arena-edge transition-colors last:border-b-0 hover:bg-arena-raise/60',
        mine !== null && 'bg-arena-text/[0.028]',
      )}
    >
      <td
        className={clsx('p-2 align-middle', rail && 'player-accent-rail')}
        style={
          rail
            ? styleVariables({ '--player-accent': rail })
            : undefined
        }
      >
        {/* Deliberately not `relative`: the overlay has to resolve against the row. */}
        <Link
          to={`/matches/${match.id}`}
          state={{
            returnTo: `${location.pathname}${location.search}`,
            returnLabel: 'Watch',
          }}
          className="flex min-w-0 after:absolute after:inset-0 after:content-['']"
        >
          <Matchup
            participants={match.participants}
            winnerSlot={outcome?.winnerSlot ?? null}
          />
        </Link>
      </td>

      <td className="p-2 text-right align-middle sm:text-left">
        <Verdict match={match} outcome={outcome} />
        {/* A phone drops columns rather than scrolling sideways, so Ended and When fold in
            here as a second line instead of becoming a horizontal drag. */}
        <MetaLine
          className="justify-end sm:hidden"
          items={[
            ...ended,
            <time key="when" dateTime={when}>
              {agoLabel(when)}
            </time>,
          ]}
        />
      </td>

      <td className="hidden p-2 align-middle sm:table-cell">
        {ended.length === 0 ? (
          <span className="t-micro">—</span>
        ) : (
          <MetaLine items={ended} />
        )}
      </td>

      <td className="hidden p-2 align-middle sm:table-cell">
        {set !== null && <span className="t-micro">{set}</span>}
      </td>

      <td className="val hidden truncate p-2 align-middle sm:table-cell">
        {match.mapId}
      </td>

      {/* "6m ago" is a phrase rather than a machine value, so it is not mono. */}
      <td className="t-micro hidden p-2 align-middle sm:table-cell">
        <time dateTime={when}>{agoLabel(when)}</time>
      </td>
    </tr>
  );
}

/**
 * The arena with nothing in it — this is the state where prose *is* the content, so it
 * carries the explanation the content state gives up.
 *
 * Written out rather than passed to `EmptyState`, which is a title and a line: this one
 * has an argument, a way in and an honest alternative to signing up. It must not compete
 * with `FirstRun`, which owns "you have no bots"; this owns "the arena has no matches".
 */
function NeverFought() {
  return (
    <section className="panel pad">
      <p className="lab mb-2">No Arc Relay broadcasts yet</p>
      <p className="t-body mb-3 max-w-[52ch]">
        The arena fills when the passive ladder pairs entrants or a player starts
        an unrated scrimmage.
      </p>
      <p className="t-meta mb-4 max-w-[62ch]">
        Every match here is a deterministic record — same entrant revisions, map and seed,
        same replay hash every time. A broadcast appears on its causal clock, and you can
        scrub it afterwards to the tick it turned.
      </p>
      <Link to="/relay" className="btn inline-block">
        Create an entrant →
      </Link>
    </section>
  );
}

/**
 * What happened, in the vocabulary the CLI prints: `<bot> wins`, `Drawn`, and what the
 * match is doing instead before a result exists — never a blank, which would read as a
 * match that ended in nothing.
 */
function Verdict({
  match,
  outcome,
}: {
  match: MatchSummary;
  outcome: RevealedOutcome | null;
}) {
  if (match.broadcasting) return <LiveStatus />;
  // `error` is on the detail response only, so a failed match says this and no more — and
  // is offered no `replay →` anywhere, because it has no replay.
  if (match.status === 'Failed')
    return <span className="t-body text-arena-hot">Failed to run</span>;
  if (outcome === null) {
    // A status this build has never heard of is repeated rather than guessed at — better
    // an unfamiliar word than confidently calling a queued match a fight.
    const running = match.status === 'Running';
    return (
      <span className="t-body inline-flex items-center gap-2 text-arena-dim">
        {running && <LiveDot />}
        {match.status === 'Pending'
          ? 'Queued'
          : running
            ? 'Fighting'
            : match.status.toLowerCase()}
      </span>
    );
  }
  if (outcome.winnerSlot === null)
    return <span className="t-body text-arena-text">Drawn</span>;
  return (
    <span className="t-body text-arena-dim">
      <span className="font-semibold text-arena-text">
        {outcome.winnerName ?? `Slot ${outcome.winnerSlot}`}
      </span>{' '}
      wins
    </span>
  );
}

/** A dot-separated line of context. Absent items drop out with their separator. */
function MetaLine({ items, className }: { items: ReactNode[]; className?: string }) {
  const shown = items.filter((item) => item !== null && item !== false);
  if (shown.length === 0) return null;
  return (
    <p
      className={clsx(
        't-micro flex flex-wrap items-baseline gap-x-2 gap-y-1',
        className,
      )}
    >
      {shown.map((item, index) => (
        <Fragment key={index}>
          {index > 0 && (
            <span aria-hidden className="text-arena-edge2">
              ·
            </span>
          )}
          {item}
        </Fragment>
      ))}
    </p>
  );
}

/** `elimination` and `t39` — how it ended and when, once the result is public. */
function endedItems(outcome: RevealedOutcome | null): ReactNode[] {
  if (outcome === null) return [];
  const ending = endingWords(outcome.reason);
  return [
    ending === null ? null : <span key="ending">{ending}</span>,
    outcome.endTick === null ? null : (
      <span key="tick" className="val">
        t{outcome.endTick}
      </span>
    ),
  ].filter((item) => item !== null);
}

/**
 * Where a match sits in its ranked set — the feed's own Set column, which is empty on
 * every unranked row.
 *
 * `game 3` with no denominator: nothing on the summary carries the length of a set, and
 * the only thing that knows one is `/api/matchsets/{id}` — a second request per row, on a
 * page that already refuses one per row for the broadcast clock. `setLength` is where the
 * ` of 6` lands the day the summary carries it.
 */
function setLabel(match: MatchSummary): string | null {
  if (match.matchSetId === null) return null;
  if (match.setGame === null) return 'ranked';
  const length = setLength(match);
  return length === null ? `game ${match.setGame}` : `game ${match.setGame} of ${length}`;
}

/** Always null today. See `setLabel`. */
function setLength(_match: MatchSummary): number | null {
  return null;
}

/**
 * The same fact on a rail card, which has no column above it to say what `game 3` is.
 * Ranked play is worth naming; unranked is the default and says nothing.
 */
function rankedLabel(match: MatchSummary): string | null {
  const set = setLabel(match);
  if (set === null) return null;
  return set === 'ranked' ? 'ranked' : `ranked · ${set}`;
}

interface RevealedOutcome {
  /** Null on a draw — only ever read once the payload has revealed the result. */
  winnerSlot: number | null;
  winnerName: string | null;
  reason: string | null;
  endTick: number | null;
}

/**
 * The result, when the server has actually sent one.
 *
 * `winnerSlot === null` cannot be read on its own: it means "drawn" on a revealed match
 * and "not telling you yet" on one still broadcasting, and rendering those the same way
 * would announce a draw for every fight in progress. `BroadcastSafe` sets `broadcasting`
 * for exactly the window where the outcome is withheld, so that flag decides whether
 * there is anything to say — and returning null here is what stops every caller from
 * having to remember it. `outcome` and `finalHealth` on the participants are redacted the
 * same way and are gated by the same function.
 */
function revealedOutcome(match: MatchSummary): RevealedOutcome | null {
  if (match.status !== 'Completed' || match.broadcasting) return null;
  const winner =
    match.winnerSlot === null
      ? null
      : (match.participants.find(
          (participant) => participant.slot === match.winnerSlot,
        ) ?? null);
  return {
    winnerSlot: match.winnerSlot,
    winnerName: winner?.nameSnapshot ?? null,
    reason: match.endReason,
    endTick: match.endTick,
  };
}

/**
 * How a match ended, in the words the player guide uses — "elimination or domination can
 * end it sooner", and a tick limit the clock runs into. An unrecognised reason passes
 * through rather than being dropped, so a rules version that adds one reads oddly here
 * instead of silently rendering nothing.
 */
function endingWords(reason: string | null): string | null {
  switch (reason) {
    case null:
      return null;
    case 'Elimination':
      return 'elimination';
    case 'Domination':
      return 'domination';
    case 'Disqualification':
      return 'disqualification';
    case 'MaxTicks':
      return 'tick limit';
    default:
      return reason.toLowerCase();
  }
}

/**
 * How far into its broadcast a live match is — a counting number, zero-padded so the
 * column does not twitch as it crosses a power of ten.
 *
 * The seam for the tick reaching the feed payload. Until then only rail cards subscribe to
 * `/api/matches/{id}/live`, and `—` stands in for a clock that has not answered yet rather
 * than a number derived from `createdAt`, which would drift from the viewer's the moment a
 * broadcast started late. `totalTicks` is deliberately not read: it is `endTick + 1` and
 * withheld while broadcasting, so there is no denominator to print here and must not be.
 */
function broadcastProgress(clock: MatchLive | undefined): string | null {
  // Guard the field, not just the response. A live payload that answers without a tick —
  // an older server, a match that has not started broadcasting — otherwise renders the
  // literal word "undefined" where a number belongs.
  if (typeof clock?.presentationTick !== 'number') return null;
  return String(clock.presentationTick).padStart(3, '0');
}

/**
 * The feed has no cursor.
 *
 * `skip`/`take` over a feed that grows at the front repeats a row across a page boundary
 * the moment a match is created mid-scroll — a duplicate React key and the same fight
 * twice. Until the endpoint offers a keyset, the flattened pages are de-duped here.
 */
function dedupeById(matches: readonly MatchSummary[]): MatchSummary[] {
  const seen = new Set<string>();
  return matches.filter((match) => {
    if (seen.has(match.id)) return false;
    seen.add(match.id);
    return true;
  });
}

/**
 * "6m ago". The feed is scanned rather than read, and a wall-clock timestamp makes the
 * reader do the subtraction on every row; the exact time is on the match page.
 */
function agoLabel(iso: string): string {
  const seconds = Math.max(0, (Date.now() - new Date(iso).getTime()) / 1000);
  if (seconds < 60) return 'just now';
  const minutes = Math.floor(seconds / 60);
  if (minutes < 60) return `${minutes}m ago`;
  const hours = Math.floor(minutes / 60);
  if (hours < 24) return `${hours}h ago`;
  const days = Math.floor(hours / 24);
  if (days < 7) return `${days}d ago`;
  return new Date(iso).toLocaleDateString();
}
