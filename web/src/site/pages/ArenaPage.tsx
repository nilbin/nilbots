import { Link, useSearchParams } from 'react-router-dom';
import BotIdentity from '../components/BotIdentity';
import { type MatchSummary } from '../api';
import { ErrorState, LoadingState } from '../components/StateView';
import { useBots, useMatches, useMeta } from '../queries';
import { useAuth } from '../auth';

/// Landing + public match browser: recent matches, refreshed while any are running.
export default function ArenaPage() {
  const { data: bots = [] } = useBots();
  const { data: meta = null } = useMeta();
  const { user } = useAuth();

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

  return (
    <div className="flex flex-col gap-8">
      <section className="rounded-xl border border-arena-edge bg-arena-panel p-8">
        <h1 className="text-3xl font-black tracking-wide">
          Write a bot. <span className="text-arena-accent">Watch it fight.</span>
        </h1>
        <p className="mt-2 max-w-xl text-sm text-arena-dim">
          nilbots is a programming game: autonomous C# bots compiled to
          WebAssembly, battling in a deterministic arena. Same seed, same bots —
          same match, every time.
        </p>
        <div className="mt-4 flex gap-3">
          <Link
            to={user ? '/garage' : '/login'}
            className="rounded-md bg-arena-accent px-4 py-2 font-semibold text-arena-bg"
          >
            {user ? 'Open my garage' : 'Join the arena'}
          </Link>
          <Link
            to="/bots"
            className="rounded-md border border-arena-edge px-4 py-2 text-arena-text"
          >
            Browse bots
          </Link>
        </div>
      </section>

      <section>
        <div className="mb-3 flex flex-wrap items-center gap-2">
          <h2 className="font-mono text-xs tracking-widest text-arena-dim">
            {filtered ? 'MATCHES' : 'RECENT MATCHES'}
          </h2>
          <select
            value={bot}
            onChange={(e) => setFilter('bot', e.target.value)}
            aria-label="Filter by bot"
            className="rounded-md border border-arena-edge bg-arena-bg px-2 py-1 text-xs text-arena-text"
          >
            <option value="">any bot</option>
            {bots.map((b) => (
              <option key={b.id} value={b.slug}>
                {b.name}
              </option>
            ))}
          </select>
          <select
            value={map}
            onChange={(e) => setFilter('map', e.target.value)}
            aria-label="Filter by map"
            className="rounded-md border border-arena-edge bg-arena-bg px-2 py-1 text-xs text-arena-text"
          >
            <option value="">any map</option>
            {(meta?.maps ?? []).map((m) => (
              <option key={m.id} value={m.id}>
                {m.id}
              </option>
            ))}
          </select>
          <select
            value={ranked}
            onChange={(e) => setFilter('ranked', e.target.value)}
            aria-label="Filter by ranked or unranked"
            className="rounded-md border border-arena-edge bg-arena-bg px-2 py-1 text-xs text-arena-text"
          >
            <option value="">ranked + unranked</option>
            <option value="true">ranked only</option>
            <option value="false">unranked only</option>
          </select>
          {filtered && (
            <button
              onClick={() => setParams(new URLSearchParams(), { replace: true })}
              className="rounded-md border border-arena-edge px-2 py-1 text-xs text-arena-dim hover:text-arena-text"
            >
              clear
            </button>
          )}
        </div>
        {feed.isError ? (
          <ErrorState error={feed.error} onRetry={() => void feed.refetch()} />
        ) : feed.isPending || matches === undefined ? (
          <LoadingState label="Loading the arena…" />
        ) : matches.length === 0 ? (
          <p className="text-sm text-arena-dim">
            {filtered
              ? 'No match fits those filters.'
              : 'No matches yet — be the first to throw down a challenge.'}
          </p>
        ) : (
          <>
            <ul className="flex flex-col gap-2">
              {matches.map((match) => (
                <MatchRow key={match.id} match={match} />
              ))}
            </ul>
            {feed.hasNextPage && (
              <button
                onClick={() => void feed.fetchNextPage()}
                disabled={feed.isFetchingNextPage}
                className="mt-3 rounded-md border border-arena-edge px-4 py-2 text-sm text-arena-dim transition-colors hover:border-arena-dim hover:text-arena-text disabled:opacity-50"
              >
                {feed.isFetchingNextPage ? 'Loading…' : 'Load more'}
              </button>
            )}
          </>
        )}
      </section>
    </div>
  );
}

function MatchRow({ match }: { match: MatchSummary }) {
  const [a, b] = match.participants;
  const winner = match.winnerSlot;
  return (
    <li>
      <Link
        to={`/matches/${match.id}`}
        className="flex flex-wrap items-center gap-x-3 gap-y-1 rounded-lg border border-arena-edge bg-arena-panel/60 px-4 py-3 transition-colors hover:border-arena-dim"
      >
        <BotIdentity
          name={a?.nameSnapshot}
          accent={a?.accentSnapshot}
          lookId={a?.lookIdSnapshot}
          size="xs"
          emphasized={winner === 0}
        />
        <span className="font-mono text-xs text-arena-dim">vs</span>
        <BotIdentity
          name={b?.nameSnapshot}
          accent={b?.accentSnapshot}
          lookId={b?.lookIdSnapshot}
          size="xs"
          emphasized={winner === 1}
        />
        <span className="ml-auto flex items-center gap-2 font-mono text-[11px] text-arena-dim">
          {match.setGame && <span className="rounded bg-arena-edge px-1.5 py-0.5">ranked g{match.setGame}</span>}
          {match.mapId} ·{' '}
          {match.broadcasting ? (
            <span className="flex items-center gap-1 font-bold text-arena-hot">
              <span className="inline-block size-1.5 animate-pulse rounded-full bg-arena-hot" />
              LIVE
            </span>
          ) : match.status === 'Completed' ? (
            winner === null || winner === undefined ? (
              'draw'
            ) : (
              `${match.participants[winner]?.nameSnapshot} wins`
            )
          ) : (
            match.status.toLowerCase()
          )}
        </span>
      </Link>
    </li>
  );
}
