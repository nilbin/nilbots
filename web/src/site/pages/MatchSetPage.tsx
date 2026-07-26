
import { Link, useParams } from 'react-router-dom';
import BotIdentity from '../components/BotIdentity';
import type { MatchSetDetail, SetGame } from '../api';
import { useMatchSet } from '../queries';
import { ErrorState, LoadingState } from '../components/StateView';

/// A ranked set: six games, three map/seed pairs with mirrored starts. Outcomes and
/// the rating swing reveal only as broadcasts finish — no spoilers from this page.
export default function MatchSetPage() {
  const { setId } = useParams<{ setId: string }>();
  const { data: set, error, refetch } = useMatchSet(setId);

  if (error !== null)
    return <ErrorState error={error} onRetry={() => void refetch()} />;
  if (!set) return <LoadingState label="Loading the set…" />;

  const winner =
    set.winnerBotId === set.botA.id ? set.botA : set.winnerBotId === set.botB.id ? set.botB : null;

  return (
    <div className="flex flex-col gap-6">
      <header className="rounded-xl border border-arena-edge bg-arena-panel p-6">
        <p className="mb-2 font-mono text-xs tracking-widest text-arena-dim">
          RANKED MATCH SET · 6 GAMES · MIRRORED STARTS
        </p>
        <div className="flex flex-wrap items-center gap-4 text-2xl font-black">
          <BotIdentity
            name={set.botA.name}
            accent={set.botA.accent}
            lookId={set.botA.lookId}
            size="lg"
            nameClassName="font-black"
          />
          <span className="font-mono text-lg text-arena-dim">
            {set.scoreA !== null ? `${set.scoreA} : ${set.scoreB}` : 'vs'}
          </span>
          <BotIdentity
            name={set.botB.name}
            accent={set.botB.accent}
            lookId={set.botB.lookId}
            size="lg"
            nameClassName="font-black"
          />
        </div>
        {set.revealed && set.status === 'Completed' && (
          <p className="mt-3 text-sm">
            {winner ? (
              <>
                {/* Name and accent are null when a bot has been deleted and the set
                    carries no participant snapshot for it (MatchPublicProjection.ToSetBot). */}
                <span style={{ color: winner.accent ?? '#38bdf8' }} className="font-bold">
                  {winner.name ?? 'A removed bot'}
                </span>{' '}
                takes the set.
              </>
            ) : (
              'The set is drawn.'
            )}{' '}
            <span className="font-mono text-xs text-arena-dim">
              rules {set.rulesVersion} ladder: {set.botA.name} {formatDelta(set.ratingChangeA)} ·{' '}
              {set.botB.name} {formatDelta(set.ratingChangeB)}
            </span>
          </p>
        )}
        {set.status === 'Failed' && (
          <p className="mt-3 text-sm text-red-400">
            A game in this set failed to execute; no ratings were changed.
          </p>
        )}
        {!set.revealed && set.status !== 'Failed' && (
          <p className="mt-3 flex items-center gap-2 font-mono text-xs text-arena-dim">
            <span className="inline-block size-2 animate-pulse rounded-full bg-red-500" />
            Games are still broadcasting — results reveal as you watch them.
          </p>
        )}
      </header>

      <section>
        <div className="mb-3 flex flex-wrap items-baseline justify-between gap-2">
          <h2 className="font-mono text-xs tracking-widest text-arena-dim">
            GAMES
          </h2>
          <p className="text-xs text-arena-dim">
            Open any game to watch its live broadcast or replay.
          </p>
        </div>
        <ul className="grid grid-cols-1 gap-3 sm:grid-cols-2 lg:grid-cols-3">
          {set.games.map((game) => (
            <GameCard key={game.id} game={game} set={set} />
          ))}
        </ul>
      </section>
    </div>
  );
}

function GameCard({ game, set }: { game: SetGame; set: MatchSetDetail }) {
  const winnerName =
    game.winnerBotId === set.botA.id
      ? set.botA.name
      : game.winnerBotId === set.botB.id
        ? set.botB.name
        : null;
  const [first, second] = game.participants;
  const action =
    game.status === 'Failed'
      ? 'View details'
      : game.broadcasting
        ? 'Watch live'
        : game.status === 'Completed'
          ? 'Watch replay'
          : 'Open game';

  return (
    <li>
      <Link
        to={`/matches/${game.id}`}
        aria-label={`${action}: game ${game.game} on ${game.mapId}`}
        className="group flex h-full flex-col gap-2 rounded-lg border border-arena-edge bg-arena-panel/60 p-4 transition-colors hover:border-arena-accent/70 hover:bg-arena-panel focus-visible:border-arena-accent focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-arena-accent/40"
      >
        <div className="flex items-center gap-2 font-mono text-[11px] text-arena-dim">
          GAME {game.game} · {game.mapId}
          <span className="ml-auto">
            {game.broadcasting ? (
              <span className="flex items-center gap-1 font-bold text-red-400">
                <span className="inline-block size-1.5 animate-pulse rounded-full bg-red-500" />
                LIVE
              </span>
            ) : game.status !== 'Completed' ? (
              game.status.toLowerCase()
            ) : null}
          </span>
        </div>
        <div className="flex flex-wrap items-center gap-2">
          <BotIdentity
            name={first?.nameSnapshot}
            accent={first?.accentSnapshot}
            lookId={first?.lookIdSnapshot}
            size="xs"
            emphasized={winnerName === first?.nameSnapshot}
          />
          <span className="font-mono text-xs text-arena-dim"> vs </span>
          <BotIdentity
            name={second?.nameSnapshot}
            accent={second?.accentSnapshot}
            lookId={second?.lookIdSnapshot}
            size="xs"
            emphasized={winnerName === second?.nameSnapshot}
          />
        </div>
        <div className="mt-auto flex items-center justify-between gap-3 font-mono text-xs">
          <span className={winnerName ? 'text-arena-text' : 'text-arena-dim'}>
            {winnerName
              ? `${winnerName} wins`
              : game.draw
                ? 'draw'
                : game.broadcasting
                  ? 'result pending'
                  : game.status.toLowerCase()}
          </span>
          <span className="whitespace-nowrap text-arena-accent">
            {action}{' '}
            <span
              aria-hidden
              className="inline-block transition-transform group-hover:translate-x-0.5"
            >
              →
            </span>
          </span>
        </div>
      </Link>
    </li>
  );
}

function formatDelta(value: number | null): string {
  if (value === null) return '';
  const rounded = Math.round(value * 10) / 10;
  return rounded >= 0 ? `+${rounded}` : `${rounded}`;
}
