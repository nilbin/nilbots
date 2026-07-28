import { Fragment, type ReactNode } from 'react';
import { Link, useSearchParams } from 'react-router-dom';
import clsx from 'clsx';
import Matchup from '../components/Matchup';
import { EmptyState, ErrorState, LoadingState } from '../components/StateView';
import { type MatchSummary } from '../api';
import { useBots, useMatches, useMeta } from '../queries';

/**
 * Watch — the one screen a spectator reaches without already knowing what they came for.
 *
 * So it opens with something to watch rather than with the controls for narrowing a list.
 * Whatever is broadcasting is lifted out of the feed into cards at the top; when nothing
 * is broadcasting, the match that finished most recently takes that place, because a
 * replay is the thing this product actually has. Filters sit below that, where somebody
 * who *does* know what they came for will go looking for them.
 *
 * Every row says who fought and what happened. A match is two or more recognisable bots —
 * chassis, accent, owner — and an ending, not a line of log with an id in it.
 *
 * **Result secrecy is a server invariant, not a presentation choice.** `/api/matches`
 * withholds winner, end reason and final health until a match has finished broadcasting
 * (`MatchPublicProjection.BroadcastSafe`), so nothing here reconstructs an outcome from
 * what is left behind. A match still broadcasting reads as in progress, which is all
 * anyone outside the arena is entitled to know yet.
 */
export default function ArenaPage() {
  const { data: bots = [] } = useBots();
  const { data: meta = null } = useMeta();

  // Filters live in the URL so a filtered feed can be linked and reloaded.
  const [params, setParams] = useSearchParams();
  const bot = params.get('bot') ?? '';
  const map = params.get('map') ?? '';
  const ranked = params.get('ranked') ?? '';
  const filtered = bot !== '' || map !== '' || ranked !== '';

  const setFilter = (key: string, value: string) => {
    const next = new URLSearchParams(params);
    if (value === '') next.delete(key);
    else next.set(key, value);
    setParams(next, { replace: true });
  };

  // The filters are part of the key, so changing one is a different query rather than a
  // refetch — no manual reset of pages, and switching back finds the old feed cached.
  const feed = useMatches({ bot, map, ranked });
  const matches = feed.data?.pages.flat();

  // `broadcasting` is the server's own flag for the window where a match has run and its
  // result is still sealed — which is exactly the set worth putting first, and the set
  // whose outcome must not appear anywhere on this page.
  const live = (matches ?? []).filter((match) => match.broadcasting);
  const rest = (matches ?? []).filter((match) => !match.broadcasting);
  const starting = rest.filter(
    (match) => match.status === 'Pending' || match.status === 'Running',
  ).length;
  // With nothing broadcasting, the newest finished match leads instead. `/api/matches`
  // orders by `createdAt` descending, so the first one carrying a result is it.
  const lastFinished =
    live.length > 0 ? null : (rest.find((match) => revealedOutcome(match) !== null) ?? null);
  // Whatever is featured above is not repeated below: a match belongs in one place at a
  // time, and a live row would announce itself twice on the same screen.
  const rows = rest.filter((match) => match.id !== lastFinished?.id);

  return (
    <div className="mx-auto flex max-w-4xl flex-col gap-6">
      <header>
        <p className="type-label mb-2 text-[10.5px] text-arena-dim">Watch</p>
        <h1 className="type-display mb-2 text-[30px]">Every fight, as it happens</h1>
        <p className="max-w-[62ch] text-sm text-arena-dim">
          Join a match while it is broadcasting, or open a finished one and scrub to the
          moment it turned — every replay is a deterministic record you can check. Results
          stay sealed until a broadcast has played all the way out, so a match still
          running here has no winner yet, not a hidden one.
        </p>
      </header>

      {feed.isError ? (
        <ErrorState error={feed.error} onRetry={() => void feed.refetch()} />
      ) : feed.isPending || matches === undefined ? (
        <LoadingState label="Finding matches…" />
      ) : matches.length === 0 ? (
        <EmptyState
          title={filtered ? 'No match fits those filters' : 'No matches yet'}
          detail={
            filtered
              ? 'Clear a filter below to see the rest of the arena.'
              : 'Bots fight when someone throws a challenge. Open a bot and start one.'
          }
        />
      ) : (
        <>
          <NowPanel
            live={live}
            lastFinished={lastFinished}
            starting={starting}
            filtered={filtered}
          />

          <section>
            <div className="mb-3 flex flex-wrap items-center gap-2">
              <h2 className="type-label mr-auto text-[10.5px] text-arena-dim">
                {filtered ? 'Matches in this filter' : 'Every match'}
              </h2>
              <select
                value={bot}
                onChange={(event) => setFilter('bot', event.target.value)}
                aria-label="Filter by bot"
                className={filterClass}
              >
                <option value="">any bot</option>
                {bots.map((entry) => (
                  <option key={entry.id} value={entry.slug}>
                    {entry.name}
                  </option>
                ))}
              </select>
              <select
                value={map}
                onChange={(event) => setFilter('map', event.target.value)}
                aria-label="Filter by map"
                className={filterClass}
              >
                <option value="">any map</option>
                {(meta?.maps ?? []).map((entry) => (
                  <option key={entry.id} value={entry.id}>
                    {entry.id}
                  </option>
                ))}
              </select>
              <select
                value={ranked}
                onChange={(event) => setFilter('ranked', event.target.value)}
                aria-label="Filter by ranked or unranked"
                className={filterClass}
              >
                <option value="">ranked + unranked</option>
                <option value="true">ranked only</option>
                <option value="false">unranked only</option>
              </select>
              {filtered && (
                <button
                  type="button"
                  onClick={() => setParams(new URLSearchParams(), { replace: true })}
                  className="rounded-[3px] border border-arena-edge px-2 py-1 text-xs text-arena-dim transition-colors hover:border-arena-edge2 hover:text-arena-text"
                >
                  clear
                </button>
              )}
            </div>

            {rows.length === 0 ? (
              <p className="text-sm text-arena-dim">
                {filtered
                  ? 'Nothing else fits this filter.'
                  : 'Nothing else yet — what is above is the whole arena so far.'}
              </p>
            ) : (
              <ul className="flex flex-col gap-1.5">
                {rows.map((match) => (
                  <MatchRow key={match.id} match={match} />
                ))}
              </ul>
            )}

            {feed.hasNextPage && (
              <button
                type="button"
                onClick={() => void feed.fetchNextPage()}
                disabled={feed.isFetchingNextPage}
                className="mt-3 rounded-[3px] border border-arena-edge px-4 py-2 text-sm text-arena-dim transition-colors hover:border-arena-edge2 hover:text-arena-text disabled:opacity-50"
              >
                {feed.isFetchingNextPage ? 'Loading…' : 'Older matches'}
              </button>
            )}
          </section>
        </>
      )}
    </div>
  );
}

/**
 * `max-w-full` is load-bearing: a select is as wide as its longest option, and a bot with
 * a long name would otherwise widen the page past the phone it is being read on.
 */
const filterClass =
  'max-w-full rounded-[3px] border border-arena-edge bg-arena-bg px-2 py-1 text-xs text-arena-text';

/**
 * The lead: what there is to watch this second.
 *
 * When nothing is broadcasting this deliberately does not collapse to nothing. A
 * spectator who arrives at a quiet arena still needs a way in, so the last finished match
 * takes the same slot with the same shape — the difference is that it carries a result
 * and a live one cannot.
 */
function NowPanel({
  live,
  lastFinished,
  starting,
  filtered,
}: {
  live: MatchSummary[];
  lastFinished: MatchSummary | null;
  starting: number;
  filtered: boolean;
}) {
  const featured = live.length > 0 ? live : lastFinished === null ? [] : [lastFinished];
  if (featured.length === 0 && starting === 0) return null;

  return (
    <section className="rounded-[3px] border border-arena-edge bg-arena-panel">
      <div className="flex flex-wrap items-baseline justify-between gap-x-3 gap-y-1 px-3.5 pt-3 pb-2">
        <h2 className="type-label text-[10.5px] text-arena-dim">
          {live.length > 0
            ? `Live now · ${live.length} ${live.length === 1 ? 'match' : 'matches'}`
            : // Never claim the whole arena is quiet when only this filter is.
              filtered
              ? 'Nothing live in this filter'
              : 'Nothing live right now'}
        </h2>
        {starting > 0 && (
          <p className="type-label text-[10px] text-arena-dim">
            {starting} {starting === 1 ? 'match' : 'matches'} starting
          </p>
        )}
      </div>
      {featured.length > 0 && (
        <div className="grid grid-cols-[repeat(auto-fit,minmax(260px,1fr))] gap-2.5 px-3.5 pb-3.5">
          {featured.map((match) => (
            <FeatureCard key={match.id} match={match} live={match.broadcasting} />
          ))}
        </div>
      )}
      {live.length === 0 && (
        <p className="px-3.5 pb-3.5 text-sm text-arena-dim">
          This page follows the arena on its own — a broadcast appears here the moment one
          starts.{' '}
          <Link to="/bots" className="text-arena-accent hover:underline">
            Any bot can be challenged
          </Link>
          .
        </p>
      )}
    </section>
  );
}

/**
 * One match, big enough to choose from: the matchup with owners, where it is being
 * fought, and either the state of the broadcast or how it ended.
 */
function FeatureCard({ match, live }: { match: MatchSummary; live: boolean }) {
  const outcome = revealedOutcome(match);
  const when = match.completedAt ?? match.createdAt;
  return (
    <Link
      to={`/matches/${match.id}`}
      className="group flex flex-col gap-3 rounded-[3px] border border-arena-edge bg-arena-raise px-3 py-3 transition-colors hover:border-arena-edge2"
    >
      <div className="flex flex-wrap items-center justify-between gap-2">
        {live ? (
          <LiveTag />
        ) : (
          <span className="type-label text-[10px] text-arena-dim">Last match</span>
        )}
        <span className="font-mono text-[11px] text-arena-dim">{match.mapId}</span>
      </div>

      <Matchup
        participants={match.participants}
        winnerSlot={outcome?.winnerSlot ?? null}
        size="sm"
        layout="stack"
        showOwners
      />

      <div className="flex flex-wrap items-baseline justify-between gap-x-3 gap-y-1">
        {live ? (
          <span className="flex items-baseline gap-2">
            <span className="type-label text-[10px] text-arena-dim">tick</span>
            <span className="tabular font-mono text-[11px] text-arena-dim">
              {broadcastProgress(match) ?? '—'}
            </span>
          </span>
        ) : (
          <Verdict match={match} outcome={outcome} />
        )}
        <span className="text-[11.5px] text-arena-dim transition-colors group-hover:text-arena-text">
          {live ? 'watch →' : 'replay →'}
        </span>
      </div>

      {!live && (
        <MetaLine
          items={[
            rankedItem(match),
            <time key="when" dateTime={when}>
              {agoLabel(when)}
            </time>,
          ]}
        />
      )}
    </Link>
  );
}

/** One line of the feed: who fought, what happened, and where. */
function MatchRow({ match }: { match: MatchSummary }) {
  const outcome = revealedOutcome(match);
  const when = match.completedAt ?? match.createdAt;
  const ending = endingWords(outcome?.reason ?? null);
  const endTick = outcome?.endTick ?? null;
  return (
    <li>
      <Link
        to={`/matches/${match.id}`}
        className="grid items-center gap-x-4 gap-y-2 rounded-[3px] border border-arena-edge bg-arena-panel px-3.5 py-3 transition-colors hover:border-arena-edge2 sm:grid-cols-[minmax(0,1fr)_auto]"
      >
        <Matchup
          participants={match.participants}
          winnerSlot={outcome?.winnerSlot ?? null}
        />
        {/* The verdict and its detail stack to the right on a wide row and fall under the
            matchup on a phone — dropping to a second line rather than to a scrollbar. */}
        <div className="flex flex-col gap-0.5 sm:items-end">
          <Verdict match={match} outcome={outcome} />
          <MetaLine
            className="sm:justify-end"
            items={[
              ending === null ? null : <span key="ending">{ending}</span>,
              endTick === null ? null : (
                <span key="tick" className="tabular font-mono">
                  t{endTick}
                </span>
              ),
              <span key="map" className="font-mono">
                {match.mapId}
              </span>,
              rankedItem(match),
              <time key="when" dateTime={when}>
                {agoLabel(when)}
              </time>,
            ]}
          />
        </div>
      </Link>
    </li>
  );
}

/**
 * What happened, in the vocabulary the CLI prints: `<bot> wins`, `draw`, and the reason
 * beside it. Before a result exists it says what the match is doing instead — never a
 * blank, which would read as a match that ended in nothing.
 */
function Verdict({
  match,
  outcome,
}: {
  match: MatchSummary;
  outcome: RevealedOutcome | null;
}) {
  if (match.broadcasting) return <LiveTag />;
  if (match.status === 'Failed')
    return <span className="text-sm text-arena-hot">Failed to run</span>;
  if (outcome === null) {
    // A status this build has never heard of is repeated rather than guessed at — better
    // an unfamiliar word than confidently calling a queued match a fight.
    const running = match.status === 'Running';
    return (
      <span className="flex items-center gap-2 text-sm text-arena-dim">
        {running && <PulseDot />}
        {match.status === 'Pending'
          ? 'Queued'
          : running
            ? 'Fighting'
            : match.status.toLowerCase()}
      </span>
    );
  }
  if (outcome.winnerSlot === null)
    return <span className="text-sm text-arena-text">Drawn</span>;
  return (
    <span className="text-sm text-arena-dim">
      <span className="font-semibold text-arena-text">
        {outcome.winnerName ?? `Slot ${outcome.winnerSlot}`}
      </span>{' '}
      wins
    </span>
  );
}

/**
 * Live is expressed by being brighter and by moving, not by hue: state is the system
 * talking, and saturated colour on this design belongs to the players.
 */
function LiveTag() {
  return (
    <span className="type-label inline-flex items-center gap-1.5 rounded-full border border-current px-2 py-px text-[10.5px] text-arena-text">
      <PulseDot />
      Live
    </span>
  );
}

function PulseDot() {
  return (
    <span
      aria-hidden
      className="inline-block size-1.5 shrink-0 animate-pulse rounded-full bg-current"
    />
  );
}

/** A dot-separated line of context. Absent items drop out with their separator. */
function MetaLine({ items, className }: { items: ReactNode[]; className?: string }) {
  const shown = items.filter((item) => item !== null && item !== false);
  if (shown.length === 0) return null;
  return (
    <p
      className={clsx(
        'flex flex-wrap items-baseline gap-x-2 gap-y-1 text-[11.5px] text-arena-dim',
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

/** Ranked play is worth naming; unranked is the default and says nothing. */
function rankedItem(match: MatchSummary): ReactNode {
  if (match.matchSetId === null) return null;
  return (
    <span key="ranked" className="type-label text-[10px]">
      {match.setGame === null ? 'ranked' : `ranked game ${match.setGame}`}
    </span>
  );
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
 * having to remember it.
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
 * How far into its broadcast a live match is.
 *
 * Always absent today, and shown as `—` rather than guessed. The presentation clock lives
 * on `/api/matches/{id}/live`, one request per match, and the feed's own payload carries
 * no tick at all; deriving one from `createdAt` would be a number that drifts from what
 * the viewer shows the moment a broadcast starts late. The day the feed carries the
 * presentation tick, this function is the only thing that changes.
 */
function broadcastProgress(_match: MatchSummary): string | null {
  return null;
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
