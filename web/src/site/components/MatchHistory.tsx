import { Link } from 'react-router-dom';
import BotIdentity from './BotIdentity';
import { errorMessage } from '../errorMessage';
import { useBotMatches } from '../queries';

/** A bot's recent games, newest first. Absent entirely when it has never fought. */
export default function MatchHistory({
  botId,
  botSlug,
  botName,
}: {
  botId: string;
  botSlug: string;
  botName: string;
}) {
  const { data, error, refetch } = useBotMatches(botId);

  if (error)
    return (
      <section className="panel pad" role="alert">
        <h2 className="lab mb-2">Latest games</h2>
        <p className="t-meta text-arena-hot">
          {errorMessage(error, 'Match history could not be loaded.')}
        </p>
        <button type="button" onClick={() => void refetch()} className="btn mt-3">
          Try again
        </button>
      </section>
    );

  if (!data || data.matches.length === 0) return null;

  return (
    <section className="panel">
      <div className="pad flex items-baseline justify-between gap-2 pb-2">
        <h2 className="lab">Latest games</h2>
        <Link
          to={`/watch?bot=${botSlug}`}
          className="t-meta transition-colors hover:text-arena-text"
        >
          every match →
        </Link>
      </div>
      <ul className="pad flex flex-col gap-1.5 pt-1.5">
        {data.matches.map((match) => (
          <li key={match.id}>
            <Link
              to={`/matches/${match.id}`}
              state={{
                returnTo: `/bots/${botSlug}`,
                returnLabel: botName,
              }}
              className="panel-quiet flex min-w-0 items-center gap-2.5 px-3 py-2 transition-colors hover:border-arena-edge2"
            >
              <span className="val w-12 shrink-0 uppercase">
                {match.broadcasting ? 'LIVE' : (match.outcome ?? match.status.toLowerCase())}
              </span>
              <span className="flex min-w-0 items-center gap-2">
                <span className="t-micro">vs</span>
                <BotIdentity
                  name={match.opponent?.nameSnapshot}
                  accent={match.opponent?.accentSnapshot}
                  lookId={match.opponent?.lookIdSnapshot}
                  size="xs"
                />
              </span>
              <span className="val ml-auto hidden shrink-0 text-right sm:block">
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
