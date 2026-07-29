import { Link, useLocation, useParams } from 'react-router-dom';
import BotIdentity from '../components/BotIdentity';
import Matchup from '../components/Matchup';
import LiveStatus from '../../components/LiveStatus';
import ArenaAction from '../components/ArenaAction';
import { ErrorState, LoadingState } from '../components/StateView';
import Movement from '../components/Movement';
import Th from '../components/TableHeader';
import { ApiError, type MatchSetDetail, type SetGame } from '../api';
import { useAuth } from '../auth';
import { useMatchSet, useMyBots } from '../queries';
import { internalReturnTarget } from '../returnTarget';

/**
 * A ranked set: authoritative standings and its exact ordered game schedule.
 *
 * Playlist versions can define different schedules. The response does not currently
 * project scheduler grouping or seeds, so this page deliberately renders games flat
 * rather than guessing which adjacent games form a mirrored pair.
 */
export default function MatchSetPage() {
  const { setId } = useParams<{ setId: string }>();
  const location = useLocation();
  const returnTarget = internalReturnTarget(location.state, {
    to: '/watch',
    label: 'Watch',
  });
  const { data: set, error, refetch } = useMatchSet(setId);
  const { user } = useAuth();
  const { data: myBots = [] } = useMyBots(Boolean(user));

  // A mistyped id is an answer, not an alarm.
  if (error instanceof ApiError && error.status === 404)
    return (
      <div className="mx-auto max-w-4xl py-10 text-center">
        <p className="t-body font-semibold text-arena-dim">No such set</p>
        <p className="t-micro mt-1">
          This set id does not exist.{' '}
          <Link to={returnTarget.to} className="text-link">
            Back to {returnTarget.label}
          </Link>
          .
        </p>
      </div>
    );
  if (error !== null) return <ErrorState error={error} onRetry={() => void refetch()} />;
  if (!set) return <LoadingState label="Loading the set…" />;

  const sides = setSides(set);
  const games = orderedSetGames(set);
  const standings = [...sides].sort((a, b) => (b.points ?? 0) - (a.points ?? 0));
  const drawn =
    set.revealed && set.status === 'Completed' && set.winnerBotId === null;
  const myIds = new Set(myBots.map((bot) => bot.id));
  const ownedSide = sides.find((side) => myIds.has(side.id));

  return (
    <div className="mx-auto flex max-w-4xl flex-col gap-3.5">
      <nav aria-label="Breadcrumb">
        <Link to={returnTarget.to} className="t-meta text-link">
          ← {returnTarget.label}
        </Link>
      </nav>
      <h1 className="sr-only">
        Ranked set: {set.botA.name ?? 'removed bot'} vs{' '}
        {set.botB.name ?? 'removed bot'}
      </h1>
      <header className="flex flex-col gap-2.5">
        <p className="lab">
          Ranked set · {shortDate(set.createdAt)}
        </p>
        {/* The matchup is the page title, walked rather than destructured — the same
            component the feed uses, so a set of three sides would render three chips. */}
        <Matchup
          participants={sides.map((side, index) => ({
            slot: index,
            nameSnapshot: side.name ?? 'A removed bot',
            ownerDisplayNameSnapshot: side.owner ?? '',
            accentSnapshot: side.accent ?? '',
            lookIdSnapshot: side.lookId ?? '',
          }))}
          size="lg"
          className="type-display"
        />

        <section className="panel pad flex flex-col gap-2">
          <p className="lab">Standings</p>
          {/* One row per side, ordered by set points — so the score is a column read down,
              which is what a standing is, rather than a `4 – 2` banner welded to exactly
              two competitors. */}
          {standings.map((side) => (
            <div key={side.id} className="flex flex-wrap items-center gap-x-3 gap-y-1">
              {side.name === null ? (
                <BotIdentity
                  name="A removed bot"
                  accent={side.accent}
                  lookId={side.lookId}
                  size="md"
                  emphasized={set.winnerBotId === side.id}
                  className="min-w-0"
                />
              ) : (
                <Link
                  to={`/bots/${side.id}`}
                  state={{
                    returnTo: `/sets/${set.id}`,
                    returnLabel: 'Ranked set',
                  }}
                  className="inline-flex min-w-0 transition-opacity hover:opacity-80"
                >
                  <BotIdentity
                    name={side.name}
                    accent={side.accent}
                    lookId={side.lookId}
                    size="md"
                    emphasized={set.winnerBotId === side.id}
                    className="min-w-0"
                  />
                </Link>
              )}
              {side.owner !== null && (
                <span className="t-micro hidden min-w-0 truncate sm:block">{side.owner}</span>
              )}
              {/* Points take the display cut, the same move the ladder's rank makes: a
                  standing is a position, not a value the engine computed. */}
              <span className="type-display tabular ml-auto shrink-0 text-[30px] text-arena-text">
                {side.points === null ? '—' : points(side.points)}
              </span>
              <RatingDelta change={side.ratingChange} before={side.ratingBefore} />
              {set.winnerBotId === side.id && <span className="pill">Takes the set</span>}
            </div>
          ))}
          {drawn && <p className="t-micro">Neither bot separated: the set is drawn.</p>}
        </section>
        <p className="t-micro">Ratings move only when a whole set completes.</p>
      </header>

      {set.status === 'Failed' && (
        <p className="panel-quiet t-body border-l-2 border-l-arena-hot px-3 py-2 text-arena-dim">
          A game in this set failed to execute, so no ratings changed. The games that did
          run are below — a failed set that rendered nothing would hide them.
        </p>
      )}

      {!set.revealed && set.status !== 'Failed' && (
        // The meaningful empty on this page is the *unrevealed* set, and it has to read as
        // intent rather than as a value that failed to arrive.
        <div className="panel-quiet pad flex flex-col gap-1.5">
          <p className="t-body">
            <b>No score yet — and not because it isn’t decided.</b>
          </p>
          <p className="t-body max-w-[62ch] text-arena-dim">
            The result is held until every game has finished broadcasting, so the first
            person to watch is not the first person to know. Open any game below; the
            schedule fills in as broadcasts finish.
          </p>
        </div>
      )}

      <section className="panel">
        <div className="pad pb-2.5">
          <h2 className="lab">
            Schedule · {set.games.length}{' '}
            {set.games.length === 1 ? 'game' : 'games'}
          </h2>
        </div>

        {games.length === 0 ? (
          <p className="pad t-body pt-0 text-arena-dim">
            The games for this set have not been created yet.
          </p>
        ) : (
          <>
            <div className="flex flex-col gap-2.5 px-3.5 pb-3.5 sm:hidden">
              {games.map((game, index) => (
                <GameCard
                  key={game.id}
                  game={game}
                  fallbackNumber={index + 1}
                  sides={sides}
                  setId={set.id}
                />
              ))}
            </div>

            <div className="hidden px-3.5 pb-3.5 sm:block">
              <table className="w-full border-collapse">
                <thead>
                  <tr>
                    <Th className="w-[64px]">Game</Th>
                    <Th>Matchup</Th>
                    <Th className="w-[120px]">Map</Th>
                    <Th className="w-[150px]">Result</Th>
                  </tr>
                </thead>
                <tbody>
                  {games.map((game, index) => (
                    <tr key={game.id} className="border-b border-arena-edge last:border-b-0">
                      <td className="val py-2.5 pr-2 align-top text-arena-text">
                        {game.game ?? index + 1}
                      </td>
                      <td className="py-2.5 pr-2 align-top">
                        <Matchup
                          participants={game.participants}
                          winnerSlot={gameWinnerSlot(game)}
                          size="xs"
                        />
                      </td>
                      <td className="val py-2.5 pr-2 align-top text-arena-text">
                        {game.mapId}
                      </td>
                      <td className="py-2.5 align-top">
                        <GameCell game={game} sides={sides} setId={set.id} />
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </>
        )}
      </section>

      <section className="panel-quiet pad flex flex-wrap items-center gap-2">
        <span className="t-meta mr-auto">
          {ownedSide
            ? 'The next ranked set is matchmade again from the current ladder.'
            : 'Follow another fight from the public feed.'}
        </span>
        <Link to="/watch" className="btn">
          Watch more
        </Link>
        <Link to="/" className="btn">
          View rankings
        </Link>
        {ownedSide && (
          <ArenaAction
            bot={{
              id: ownedSide.id,
              name: ownedSide.name ?? 'Your bot',
              accent: ownedSide.accent,
              lookId: ownedSide.lookId,
              isOwner: true,
            }}
            modes={['ranked']}
            initialMode="ranked"
            triggerLabel="Start another matchmade set"
          />
        )}
      </section>

      <details className="panel-quiet px-3 py-2">
        <summary className="lab cursor-pointer">Technical details</summary>
        <dl className="t-micro mt-2 grid grid-cols-[62px_minmax(0,1fr)] gap-x-2 gap-y-1">
          <dt>Set</dt>
          <dd className="val break-all">{set.id}</dd>
          <dt>Ruleset ID</dt>
          <dd className="val break-all">{set.rulesVersion}</dd>
          <dt>Created</dt>
          <dd>{shortDate(set.createdAt)}</dd>
        </dl>
      </details>
    </div>
  );
}

/* -------------------------------------------------------------------- schedule ----- */

function GameCard({
  game,
  fallbackNumber,
  sides,
  setId,
}: {
  game: SetGame;
  fallbackNumber: number;
  sides: readonly SetSide[];
  setId: string;
}) {
  const number = game.game ?? fallbackNumber;
  return (
    <div className="panel-quiet flex flex-col gap-2 px-3 py-2.5">
      <div className="flex flex-wrap items-baseline justify-between gap-x-3">
        <p className="lab">Game {number}</p>
        <span className="val">{game.mapId}</span>
      </div>
      <hr className="border-arena-edge" />
      <Matchup
        participants={game.participants}
        winnerSlot={gameWinnerSlot(game)}
        size="xs"
        layout="stack"
      />
      <GameCell game={game} sides={sides} setId={setId} />
    </div>
  );
}

/**
 * One scheduled game: its current outcome and a way into the broadcast or replay.
 *
 * The cell links out rather than expanding, which is the deliberate consequence of the set
 * projection carrying no `endReason`/`endTick` per game — the match page has them, and it
 * has the arena too.
 */
function GameCell({
  game,
  sides,
  setId,
}: {
  game: SetGame;
  sides: readonly SetSide[];
  setId: string;
}) {
  const number = game.game;
  return (
    <Link
      to={`/matches/${game.id}`}
      state={{
        returnTo: `/sets/${setId}`,
        returnLabel: 'Ranked set',
      }}
      aria-label={`Game ${number ?? ''} on ${game.mapId}`}
      className="group flex min-w-0 items-center gap-2 transition-opacity hover:opacity-80"
    >
      <GameOutcome game={game} sides={sides} />
      <span className="val ml-auto shrink-0 text-arena-text">
        {number === null ? 'open' : `g${number}`}{' '}
        <span aria-hidden className="inline-block transition-transform group-hover:translate-x-0.5">
          →
        </span>
      </span>
    </Link>
  );
}

function GameOutcome({ game, sides }: { game: SetGame; sides: readonly SetSide[] }) {
  if (game.status === 'Failed') return <span className="val text-arena-hot">did not run</span>;
  if (game.broadcasting)
    return <LiveStatus />;
  if (game.draw)
    return (
      <span className="flex items-baseline gap-1.5">
        <span className="val text-arena-text">—</span>
        <span className="t-micro">drawn</span>
      </span>
    );
  if (game.winnerBotId !== null) {
    const winner = sides.find((side) => side.id === game.winnerBotId);
    const participant = game.participants.find(
      (entry) => entry.botId === game.winnerBotId,
    );
    return (
      <span className="t-body text-arena-dim">
        <span className="font-semibold text-arena-text">
          {participant?.nameSnapshot ?? winner?.name ?? 'A removed bot'}
        </span>{' '}
        wins
      </span>
    );
  }
  return <span className="val">{game.status.toLowerCase()}</span>;
}

function RatingDelta({ change, before }: { change: number | null; before: number | null }) {
  return <Movement change={change} before={before} />;
}

/* ------------------------------------------------------------------ derivations ----- */

interface SetSide {
  id: string;
  name: string | null;
  accent: string | null;
  lookId: string | null;
  /** Derived, not invented: the owner as the first game that names this bot recorded it. */
  owner: string | null;
  points: number | null;
  ratingChange: number | null;
  ratingBefore: number | null;
}

/**
 * The sides of a set, in order.
 *
 * `MatchSetResponse` is `botA`/`botB` — the one place the wire asserts an arity, while
 * `participants` and `games` are already lists. The page must not inherit the assertion,
 * so it walks this instead. The day the field becomes `sides[]` this returns it and no
 * markup changes; the score and rating fields are the same assertion and move with it.
 */
function setSides(set: MatchSetDetail): SetSide[] {
  const wire = [set.botA, set.botB];
  const scores = [set.scoreA, set.scoreB];
  const changes = [set.ratingChangeA, set.ratingChangeB];
  return wire.map((bot, index) => ({
    id: bot.id,
    name: sideLabel(bot),
    accent: bot.accent,
    lookId: bot.lookId,
    owner:
      set.games
        .flatMap((game) => game.participants)
        .find((participant) => participant.botId === bot.id)
        ?.ownerDisplayNameSnapshot ?? null,
    points: scores[index] ?? null,
    ratingChange: changes[index] ?? null,
    ratingBefore: null,
  }));
}

function orderedSetGames(set: MatchSetDetail): SetGame[] {
  return [...set.games].sort(
    (left, right) => (left.game ?? Number.MAX_SAFE_INTEGER) -
      (right.game ?? Number.MAX_SAFE_INTEGER),
  );
}

function gameWinnerSlot(game: SetGame): number | null {
  if (game.winnerBotId === null) return null;
  return (
    game.participants.find(
      (participant) => participant.botId === game.winnerBotId,
    )?.slot ?? null
  );
}

function points(value: number): string {
  return Number.isInteger(value) ? String(value) : String(Math.round(value * 10) / 10);
}

function sideLabel(bot: SetSideWire): string | null {
  // Name and accent are null when a bot has been deleted and the set carries no participant
  // snapshot for it (`MatchPublicProjection.ToSetBot`).
  if (bot.name === null) return null;
  return bot.name;
}

function shortDate(iso: string): string {
  return new Date(iso).toLocaleDateString(undefined, {
    day: 'numeric',
    month: 'short',
    year: 'numeric',
  });
}

type SetSideWire = MatchSetDetail['botA'];
