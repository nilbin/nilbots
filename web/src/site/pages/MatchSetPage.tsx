import { Link, useParams } from 'react-router-dom';
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

/**
 * A ranked set: six games, three map/seed **pairs**, each pair played from both sides.
 *
 * The page's question is not "what was the score" — the `set-settled` notification already
 * carried `score`, `opponentScore` and `ratingChange`, so anyone arriving from it has been
 * told. Its question is **whether one bot is actually better than the other, or only better
 * on one side of one map**, which no notification can answer. So the *mirror* leads and the
 * score is a caption on it.
 *
 * That is why the games are not six cards. Six cards are a rendering of the storage layout;
 * the format is three pairs played twice, and a pair that one bot took from both sides says
 * something a pair that split does not. A player who sees three splits has learned that the
 * two bots are equal and the maps are unfair; a player who sees three sweeps has learned
 * they were beaten. **At no width does this page render a flat list of six.**
 *
 * Nothing here assumes two sides or two games to a pair: `setSides` returns an ordered
 * collection, the mirror's arrangement columns are derived from the games themselves, and
 * both are walked.
 */
export default function MatchSetPage() {
  const { setId } = useParams<{ setId: string }>();
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
          <Link to="/watch" className="text-link">
            Back to Watch
          </Link>
          .
        </p>
      </div>
    );
  if (error !== null) return <ErrorState error={error} onRetry={() => void refetch()} />;
  if (!set) return <LoadingState label="Loading the set…" />;

  const sides = setSides(set);
  const pairs = mirrorPairs(set);
  const arrangements = startingArrangements(pairs);
  const standings = [...sides].sort((a, b) => (b.points ?? 0) - (a.points ?? 0));
  const drawn =
    set.revealed && set.status === 'Completed' && set.winnerBotId === null;
  const myIds = new Set(myBots.map((bot) => bot.id));
  const ownedSide = sides.find((side) => myIds.has(side.id));

  return (
    <div className="mx-auto flex max-w-4xl flex-col gap-3.5">
      <nav aria-label="Breadcrumb">
        <Link to="/watch" className="t-meta text-link">
          ← Watch
        </Link>
      </nav>
      <h1 className="sr-only">
        Ranked set: {set.botA.name ?? 'removed bot'} vs{' '}
        {set.botB.name ?? 'removed bot'}
      </h1>
      <header className="flex flex-col gap-2.5">
        <p className="lab">
          Ranked set · Rules {set.rulesVersion} · {shortDate(set.createdAt)}
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
            person to watch is not the first person to know. Open any game below; the pairs
            fill in as they finish.
          </p>
        </div>
      )}

      <section className="panel">
        <div className="pad pb-2.5">
          <h2 className="lab">
            The mirror · {pairs.length} map/seed {pairs.length === 1 ? 'pair' : 'pairs'},
            each played from both sides
          </h2>
        </div>

        {pairs.length === 0 ? (
          <p className="pad t-body pt-0 text-arena-dim">
            The games for this set have not been created yet.
          </p>
        ) : (
          <>
            {/* Below sm the axis flips from columns to stacked rows and nothing is lost,
                because the pair — not the game — is the unit at every size. */}
            <div className="flex flex-col gap-2.5 px-3.5 pb-3.5 sm:hidden">
              {pairs.map((pair) => (
                <PairCard
                  key={pair.number}
                  pair={pair}
                  arrangements={arrangements}
                  sides={sides}
                />
              ))}
            </div>

            <div className="hidden px-3.5 pb-3.5 sm:block">
              <table className="w-full border-collapse">
                <thead>
                  <tr>
                    <Th className="w-[52px]">Pair</Th>
                    <Th>Map · seed</Th>
                    {arrangements.map((arrangement, column) => (
                      <Th key={arrangement.key}>
                        <span className="flex items-center gap-1.5">
                          {arrangement.name === null ? (
                            `Start ${column + 1}`
                          ) : (
                            <>
                              {/* The chip keeps its own name size; the uppercase and the
                                  tracking come from the `.lab` head it sits in. */}
                              <BotIdentity
                                name={arrangement.name}
                                accent={arrangement.accent}
                                lookId={arrangement.lookId}
                                size="xs"
                              />
                              first
                            </>
                          )}
                        </span>
                      </Th>
                    ))}
                    <Th className="w-[104px]">Verdict</Th>
                  </tr>
                </thead>
                <tbody>
                  {pairs.map((pair) => (
                    <tr key={pair.number} className="border-b border-arena-edge last:border-b-0">
                      <td className="val py-2.5 pr-2 align-top text-arena-text">
                        {pair.number}
                      </td>
                      <td className="py-2.5 pr-2 align-top">
                        {/* Named once per pair, because being shared is the entire point. */}
                        <span className="t-body block text-arena-text">{pair.mapId}</span>
                        <span className="val">seed {pair.seed ?? '—'}</span>
                      </td>
                      {arrangements.map((arrangement, column) => (
                        <td key={arrangement.key} className="py-2.5 pr-2 align-top">
                          <GameCell game={pair.games[column]} sides={sides} />
                        </td>
                      ))}
                      <td className="py-2.5 align-top">
                        <Verdict verdict={pairVerdict(pair, sides)} />
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>

              <p className="t-micro mt-2.5 max-w-[68ch]">
                <b className="text-arena-text">swept</b> — one bot took the pair from both
                sides. <b className="text-arena-text">first move</b> — each bot won the game
                it started, so the position decided it. That is what the mirror is for.
              </p>
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
        {ownedSide && (
          <ArenaAction
            bot={{
              id: ownedSide.id,
              name: ownedSide.name ?? 'Your bot',
              accent: ownedSide.accent,
              lookId: ownedSide.lookId,
              isOwner: true,
              ready: true,
            }}
            modes={['ranked']}
            initialMode="ranked"
            triggerLabel="Start another matchmade set"
          />
        )}
      </section>

      {/* A set's provenance is the union of its games', and each game page carries its own
          in full — so this is a footnote, not a panel. */}
      <p className="t-micro break-all">
        set {set.id} · rules {set.rulesVersion} · created {shortDate(set.createdAt)}
      </p>
    </div>
  );
}

/* ------------------------------------------------------------------- the mirror ---- */

function PairCard({
  pair,
  arrangements,
  sides,
}: {
  pair: MirrorPair;
  arrangements: readonly Arrangement[];
  sides: readonly SetSide[];
}) {
  const verdict = pairVerdict(pair, sides);
  return (
    <div className="panel-quiet flex flex-col gap-2 px-3 py-2.5">
      <div className="flex flex-wrap items-baseline justify-between gap-x-3">
        <p className="lab">
          Pair {pair.number} · {pair.mapId}
        </p>
        <Verdict verdict={verdict} />
      </div>
      <span className="val">seed {pair.seed ?? '—'}</span>
      <hr className="border-arena-edge" />
      {arrangements.map((arrangement, column) => (
        <div
          key={arrangement.key}
          className="grid min-w-0 grid-cols-[58px_minmax(0,1fr)] items-center gap-x-2 gap-y-1.5"
        >
          <span className="lab">Starter</span>
          <span className="flex min-w-0 items-center gap-1.5">
            {arrangement.name === null ? (
              <span className="t-body text-arena-dim">Start {column + 1}</span>
            ) : (
              <BotIdentity
                name={arrangement.name}
                accent={arrangement.accent}
                lookId={arrangement.lookId}
                size="xs"
                className="min-w-0"
                nameClassName="overflow-visible whitespace-normal text-clip"
              />
            )}
          </span>
          <span className="lab">Result</span>
          <GameCell game={pair.games[column]} sides={sides} />
        </div>
      ))}
    </div>
  );
}

/**
 * One game of a pair: who won it, and a way into it.
 *
 * The cell links out rather than expanding, which is the deliberate consequence of the set
 * projection carrying no `endReason`/`endTick` per game — the match page has them, and it
 * has the arena too.
 */
function GameCell({ game, sides }: { game: SetGame | undefined; sides: readonly SetSide[] }) {
  if (game === undefined) return <span className="val">—</span>;
  const number = game.game;
  return (
    <Link
      to={`/matches/${game.id}`}
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
    // A player's accent is their own pick, so it is legitimate chroma here: it is the one
    // thing on the page that means "this bot", not "this state".
    return (
      <BotIdentity
        name={participant?.nameSnapshot ?? winner?.name ?? 'A removed bot'}
        accent={participant?.accentSnapshot ?? winner?.accent}
        lookId={participant?.lookIdSnapshot ?? winner?.lookId}
        size="xs"
        className="min-w-0"
        nameClassName="overflow-visible whitespace-normal text-clip"
      />
    );
  }
  return <span className="val">{game.status.toLowerCase()}</span>;
}

function Verdict({ verdict }: { verdict: PairVerdict | null }) {
  if (verdict === null) return <span className="val">—</span>;
  return (
    <span className="flex flex-wrap items-baseline gap-x-1.5">
      <span className="val text-arena-text">{verdict.score}</span>
      {verdict.word !== null && <span className="t-micro">{verdict.word}</span>}
    </span>
  );
}

function RatingDelta({ change, before }: { change: number | null; before: number | null }) {
  return <Movement change={change} before={before} />;
}

/* ------------------------------------------------------------------ derivations ----- */

/**
 * How many games make a pair.
 *
 * `RankedEndpoints` draws three maps and plays each twice with mirrored starting slots, so
 * pairs are formed **by game index** — `pair = ceil(game / 2)` — and never by `mapId`.
 * Today the pool draws without replacement so grouping by map happens to work; the day it
 * draws with replacement, map-grouping silently collapses two pairs into one and renders
 * four columns of nonsense. Group by the index the server actually assigned.
 */
const GAMES_PER_PAIR = 2;

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

interface MirrorPair {
  number: number;
  mapId: string;
  seed: string | null;
  games: SetGame[];
}

interface Arrangement {
  key: string;
  name: string | null;
  accent: string | null;
  lookId: string | null;
}

interface PairVerdict {
  score: string;
  word: string | null;
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

function mirrorPairs(set: MatchSetDetail): MirrorPair[] {
  const byPair = new Map<number, SetGame[]>();
  set.games.forEach((game, index) => {
    // A game with no number is not a shape the server produces; ordering position is the
    // only honest stand-in, and it keeps a malformed payload rendering rather than empty.
    const number = game.game ?? index + 1;
    const pair = Math.ceil(number / GAMES_PER_PAIR);
    byPair.set(pair, [...(byPair.get(pair) ?? []), game]);
  });
  return [...byPair.entries()]
    .sort(([left], [right]) => left - right)
    .map(([number, games]) => {
      const ordered = [...games].sort(
        (left, right) => (left.game ?? 0) - (right.game ?? 0),
      );
      return {
        number,
        mapId: ordered[0]?.mapId ?? '—',
        seed: gameSeed(ordered[0]),
        games: ordered,
      };
    });
}

/**
 * Who moved first in each arrangement, derived from the games rather than assumed.
 *
 * Column `n` is the participant at `slot === 0` in every pair's `n`th game — which is what
 * "mirrored starts" means on the wire. There are as many columns as a pair has games:
 * today two, and nothing here says two.
 */
function startingArrangements(pairs: readonly MirrorPair[]): Arrangement[] {
  const width = pairs.reduce((widest, pair) => Math.max(widest, pair.games.length), 0);
  return Array.from({ length: width }, (_, column) => {
    const leader = pairs
      .map((pair) => pair.games[column]?.participants.find((entry) => entry.slot === 0))
      .find((entry) => entry !== undefined);
    return {
      key: `start-${column}`,
      name: leader?.nameSnapshot ?? null,
      accent: leader?.accentSnapshot ?? null,
      lookId: leader?.lookIdSnapshot ?? null,
    };
  });
}

/**
 * What the pair says, which is the only genuinely new fact on the page — and it is derived
 * entirely from data already on the wire (`winnerBotId` plus the slot-0 participant).
 *
 * "Swept" means one bot is better on that map and seed regardless of position. "First
 * move" means the position decided it, and the mirror earned its keep.
 */
function pairVerdict(pair: MirrorPair, sides: readonly SetSide[]): PairVerdict | null {
  const games = pair.games;
  if (games.length === 0) return null;
  const decided = games.every((game) => game.winnerBotId !== null || game.draw);
  if (!decided) return null;

  // The set's own scoring, applied to a pair: 1 for a win, 0.5 each for a draw.
  const tally = sides.map((side) =>
    games.reduce(
      (total, game) =>
        total + (game.winnerBotId === side.id ? 1 : game.draw ? 0.5 : 0),
      0,
    ),
  );
  const score = tally.map(points).join('–');

  // Every one of the words is a claim about *both* sides of the same map and seed, so a
  // half-built pair gets the score and no word rather than "swept" off a single game.
  if (games.length < 2) return { score, word: null };
  if (games.every((game) => game.draw)) return { score, word: 'drawn' };
  if (sides.some((side) => games.every((game) => game.winnerBotId === side.id)))
    return { score, word: 'swept' };
  const wonByStarter = (game: SetGame) =>
    game.winnerBotId !== null &&
    game.participants.find((entry) => entry.slot === 0)?.botId === game.winnerBotId;
  if (games.every(wonByStarter)) return { score, word: 'first move' };
  if (games.every((game) => game.winnerBotId !== null && !wonByStarter(game)))
    return { score, word: 'second move' };
  // A pair that mixes a win with a draw is none of the four; the score still says it.
  return { score, word: null };
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

/**
 * The generated set contract does not carry seeds, generation snapshots, starting
 * ratings, or a generalized sides collection yet.
 *
 * These presentation seams stay visibly empty rather than accepting arbitrary extra JSON
 * that bypasses the generated schema. When the contract grows, regenerate it and map the
 * typed fields in `setSides`, `sideLabel`, and `gameSeed`.
 */
function gameSeed(_game: SetGame | undefined): string | null {
  return null;
}
