import { Link, useParams } from 'react-router-dom';
import Viewer from '../../components/Viewer';
import { ApiError, type MatchLive } from '../api';
import { useMatch, useMatchLive, useMatchReplay } from '../queries';

export default function MatchPage() {
  const { matchId } = useParams<{ matchId: string }>();
  // Two queries, not one loop: the clock says where the broadcast is, and the replay
  // follows it. Each stops on its own condition, so nothing keeps polling a finished
  // match.
  const { data: live, error: liveError } = useMatchLive(matchId);
  const { data: loadedReplay } = useMatchReplay(matchId, live);
  const replay = loadedReplay?.replay;
  // Only a failed match has an error worth showing in place of the arena; the detail
  // carries the message the worker recorded.
  const { data: detail } = useMatch(live?.status === 'Failed' ? matchId : undefined);

  const missing = liveError instanceof ApiError && liveError.status === 404;
  const error = live?.status === 'Failed' ? (detail?.error ?? 'Match failed to execute.') : null;
  const finished = live?.broadcastComplete ?? false;

  if (missing)
    return (
      <div className="rounded-xl border border-arena-edge bg-arena-panel p-6">
        <p className="font-semibold">No such match.</p>
        <p className="mt-1 text-sm text-arena-dim">
          This match id does not exist.{' '}
          <Link to="/" className="text-arena-accent hover:underline">
            Back to the arena
          </Link>
          .
        </p>
      </div>
    );

  if (error)
    return (
      <div className="flex flex-col gap-3">
        <RankedSetNavigation live={live} />
        <div className="rounded-xl border border-red-900 bg-arena-panel p-6">
          <p className="font-semibold text-red-400">Match failed to execute.</p>
          <pre className="mt-2 font-mono text-xs whitespace-pre-wrap text-arena-dim">{error}</pre>
        </div>
      </div>
    );

  if (!live) return <p className="text-sm text-arena-dim">Loading…</p>;

  if (live.status !== 'Completed')
    return (
      <div className="flex flex-col gap-3">
        <RankedSetNavigation live={live} />
        <Waiting
          label={live.status === 'Pending' ? 'Match queued…' : 'Bots are fighting…'}
        />
      </div>
    );

  if (!finished && (live.countdownMs > 0 || !replay || replay.ticks.length === 0))
    return (
      <div className="flex flex-col gap-3">
        <RankedSetNavigation live={live} />
        <Waiting label="Broadcast starting…" countdown />
      </div>
    );

  if (!replay)
    return (
      <div className="flex flex-col gap-3">
        <RankedSetNavigation live={live} />
        <Waiting label="Loading replay…" />
      </div>
    );

  return (
    <div className="flex h-[calc(100dvh-140px)] min-h-[560px] flex-col gap-2">
      <RankedSetNavigation live={live} />
      <div className="min-h-0 flex-1">
        <Viewer
          replay={replay}
          soundtrackPresentationId={matchId}
          live={
            finished
              ? undefined
              : {
                  tick: live.presentationTick,
                  ticksPerSecond: live.presentationTicksPerSecond,
                }
          }
        />
      </div>
    </div>
  );
}

function RankedSetNavigation({ live }: { live: MatchLive | undefined }) {
  if (!live?.matchSetId) return null;
  return (
    <Link
      to={`/sets/${live.matchSetId}`}
      className="w-fit rounded-md border border-arena-edge bg-arena-panel/60 px-3 py-1.5 font-mono text-xs text-arena-accent transition-colors hover:border-arena-accent focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-arena-accent/40"
    >
      ← Ranked set{live.setGame ? ` · game ${live.setGame} of 6` : ''}
    </Link>
  );
}

function Waiting({ label, countdown }: { label: string; countdown?: boolean }) {
  return (
    <div className="flex flex-col items-center gap-3 py-24">
      <div
        className={
          'size-10 rounded-full border-2 border-arena-edge ' +
          (countdown ? 'animate-pulse border-t-red-500' : 'animate-spin border-t-arena-accent')
        }
      />
      <p className="font-mono text-sm text-arena-dim">{label}</p>
    </div>
  );
}
