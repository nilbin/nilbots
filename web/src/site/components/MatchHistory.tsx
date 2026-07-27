import { Link } from 'react-router-dom';
import BotIdentity from './BotIdentity';
import { useBotMatches } from '../queries';

/** A bot's recent games, newest first. Absent entirely when it has never fought. */
export default function MatchHistory({ botId, botSlug }: { botId: string; botSlug: string }) {
  const { data } = useBotMatches(botId);

  if (!data || data.matches.length === 0) return null;

  return (
    <section>
      <h2 className="mb-3 type-label text-[10.5px] text-arena-dim">
        LATEST GAMES
        <Link to={`/?bot=${botSlug}`} className="ml-2 font-normal text-arena-accent hover:underline">
          every match →
        </Link>
      </h2>
      <ul className="flex flex-col gap-1.5">
        {data.matches.map((match) => (
          <li key={match.id}>
            <Link
              to={`/matches/${match.id}`}
              className="flex items-center gap-3 rounded-lg border border-arena-edge bg-arena-panel/60 px-4 py-2 text-sm transition-colors hover:border-arena-dim"
            >
              <span
                className={
                  'w-10 font-mono text-[11px] ' +
                  (match.outcome === 'Win'
                    ? 'text-arena-ok'
                    : match.outcome === 'Loss'
                      ? 'text-arena-hot'
                      : 'text-arena-dim')
                }
              >
                {match.broadcasting ? 'LIVE' : (match.outcome ?? match.status.toLowerCase())}
              </span>
              <span className="flex items-center gap-2">
                <span className="text-arena-dim">vs</span>
                <BotIdentity
                  name={match.opponent?.nameSnapshot}
                  accent={match.opponent?.accentSnapshot}
                  lookId={match.opponent?.lookIdSnapshot}
                  size="xs"
                />
              </span>
              <span className="ml-auto font-mono text-[11px] text-arena-dim">
                {match.setGame ? `ranked g${match.setGame} · ` : ''}
                {match.mapId} · {new Date(match.createdAt).toLocaleString()}
              </span>
            </Link>
          </li>
        ))}
      </ul>
    </section>
  );
}
