import { useEffect, useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import clsx from 'clsx';
import { api, type MatchSetDetail, type SetGame } from '../api';

/// A ranked set: six games, three map/seed pairs with mirrored starts. Outcomes and
/// the rating swing reveal only as broadcasts finish — no spoilers from this page.
export default function MatchSetPage() {
  const { setId } = useParams<{ setId: string }>();
  const [set, setSet] = useState<MatchSetDetail | null>(null);

  useEffect(() => {
    let stopped = false;
    let timer: number | undefined;
    const poll = async () => {
      const data = await api.get<MatchSetDetail>(`/api/matchsets/${setId}`);
      if (stopped) return;
      setSet(data);
      if (!data.revealed && data.status !== 'Failed')
        timer = window.setTimeout(poll, 2000);
    };
    void poll();
    return () => {
      stopped = true;
      window.clearTimeout(timer);
    };
  }, [setId]);

  if (!set) return <p className="text-sm text-arena-dim">Loading…</p>;

  const winner =
    set.winnerBotId === set.botA.id ? set.botA : set.winnerBotId === set.botB.id ? set.botB : null;

  return (
    <div className="flex flex-col gap-6">
      <header className="rounded-xl border border-arena-edge bg-arena-panel p-6">
        <p className="mb-2 font-mono text-xs tracking-widest text-arena-dim">
          RANKED MATCH SET · 6 GAMES · MIRRORED STARTS
        </p>
        <div className="flex flex-wrap items-center gap-4 text-2xl font-black">
          <span className="flex items-center gap-2">
            <span className="inline-block size-4 rounded-full" style={{ background: set.botA.accent }} />
            {set.botA.name}
          </span>
          <span className="font-mono text-lg text-arena-dim">
            {set.scoreA !== null ? `${set.scoreA} : ${set.scoreB}` : 'vs'}
          </span>
          <span className="flex items-center gap-2">
            <span className="inline-block size-4 rounded-full" style={{ background: set.botB.accent }} />
            {set.botB.name}
          </span>
        </div>
        {set.revealed && set.status === 'Completed' && (
          <p className="mt-3 text-sm">
            {winner ? (
              <>
                <span style={{ color: winner.accent }} className="font-bold">
                  {winner.name}
                </span>{' '}
                takes the set.
              </>
            ) : (
              'The set is drawn.'
            )}{' '}
            <span className="font-mono text-xs text-arena-dim">
              elo {set.botA.name} {formatDelta(set.ratingChangeA)} · {set.botB.name}{' '}
              {formatDelta(set.ratingChangeB)}
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

      <ul className="grid grid-cols-1 gap-3 sm:grid-cols-2 lg:grid-cols-3">
        {set.games.map((game) => (
          <GameCard key={game.id} game={game} set={set} />
        ))}
      </ul>
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

  return (
    <li>
      <Link
        to={`/matches/${game.id}`}
        className="flex flex-col gap-2 rounded-lg border border-arena-edge bg-arena-panel/60 p-4 transition-colors hover:border-arena-dim"
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
        <div className="text-sm">
          <span className={clsx(winnerName === first?.nameSnapshot && 'font-bold')}>
            {first?.nameSnapshot}
          </span>
          <span className="font-mono text-xs text-arena-dim"> vs </span>
          <span className={clsx(winnerName === second?.nameSnapshot && 'font-bold')}>
            {second?.nameSnapshot}
          </span>
        </div>
        <div className="font-mono text-xs">
          {winnerName ? (
            <span className="text-arena-text">{winnerName} wins</span>
          ) : game.draw ? (
            <span className="text-arena-dim">draw</span>
          ) : (
            <span className="text-arena-accent">▶ watch</span>
          )}
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
