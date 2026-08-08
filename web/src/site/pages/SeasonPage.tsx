import { Link } from 'react-router-dom';
import ClassIcon from '../../components/ClassIcon';
import EntrantCrest from '../../components/EntrantCrest';
import type { ArcRelaySeasonStanding } from '../api';
import { useAuth } from '../auth';
import { ErrorState, LoadingState } from '../components/StateView';
import { useArcRelaySeason } from '../queries';

export default function SeasonPage() {
  const { user } = useAuth();
  const season = useArcRelaySeason();
  if (season.isError) {
    return <ErrorState error={season.error} onRetry={() => void season.refetch()} />;
  }
  if (season.isPending || !season.data) {
    return <LoadingState label="Opening Season 1…" />;
  }

  const data = season.data;
  const mine = data.standings.filter((standing) =>
    standing.entrant.isOwner && standing.entrant.ladderOptedIn &&
    standing.entrant.kind === 'sheet');
  return (
    <div className="mx-auto flex max-w-5xl flex-col gap-4">
      <header className="panel panel-feature pad overflow-hidden">
        <div className="flex flex-wrap items-start justify-between gap-4">
          <div>
            <p className="lab mb-2">Current competition</p>
            <h1 className="type-display text-[32px]">{data.name}</h1>
            <p className="t-meta mt-2 max-w-2xl">
              Eight weeks of ranked Arc Relay. Field a tactical sheet once and the
              Season finds its matches automatically; revisions keep one record and rating.
            </p>
          </div>
          <SeasonClock status={data.status} startsAt={data.startsAt} endsAt={data.endsAt} />
        </div>
        <div className="mt-5 grid grid-cols-2 gap-3 border-t border-arena-edge pt-4 sm:grid-cols-4">
          <SeasonFact label="Entrants" value={String(data.standings.length)} />
          <SeasonFact label="Fielded" value={user ? `${mine.length}/${data.maximumFieldedPerAccount}` : '—'} />
          <SeasonFact label="Ranked daily" value={`${data.baselineMatchesPerEntrantPerDay} each`} />
          <SeasonFact label="State" value={seasonStatus(data.status)} />
        </div>
      </header>

      {user && (
        <section className="panel">
          <div className="flex items-center justify-between gap-3 border-b border-arena-edge px-3 py-2.5">
            <div>
              <h2 className="lab">Your fielded sheets</h2>
              <p className="t-micro mt-1">
                Pairing is automatic: up to {data.baselineMatchesPerEntrantPerDay} ranked games
                per sheet in a rolling 24 hours, subject to opponents and worker capacity.
              </p>
            </div>
            <Link to="/entrants" className="btn btn-primary min-h-11">Manage sheets</Link>
          </div>
          {mine.length === 0 ? (
            <div className="pad">
              <p className="t-body">Nothing is fielded yet.</p>
              <p className="t-meta mt-1">Create a sheet and field it to enter automatic ranked pairing.</p>
            </div>
          ) : (
            <div className="grid md:grid-cols-3">
              {mine.map((standing) => <EntrantStandingCard key={standing.entrant.id} standing={standing} />)}
            </div>
          )}
        </section>
      )}

      <section className="panel overflow-hidden">
        <div className="border-b border-arena-edge px-3 py-3">
          <h2 className="lab">Season standings</h2>
          <p className="t-micro mt-1">Equal ratings share a rank. Movement is measured from the opening snapshot.</p>
        </div>
        {data.standings.length === 0 ? (
          <div className="pad">
            <p className="t-body">The table is waiting for its first entrant.</p>
            <p className="t-meta mt-1">Season 1 starts everyone at the same rating.</p>
          </div>
        ) : (
          <div className="divide-y divide-arena-edge">
            {data.standings.map((standing) => (
              <SeasonRow key={standing.entrant.id} standing={standing} />
            ))}
          </div>
        )}
      </section>

      <section className="panel overflow-hidden">
        <div className="border-b border-arena-edge px-3 py-3">
          <h2 className="lab">Recent ranked matches</h2>
          <p className="t-micro mt-1">Only settled, fully revealed results appear here.</p>
        </div>
        {data.recentMatches.length === 0 ? (
          <p className="t-meta pad">No Season 1 result has settled yet.</p>
        ) : (
          <div className="divide-y divide-arena-edge">
            {data.recentMatches.map((match) => (
              <Link
                key={match.matchId}
                to={`/matches/${match.matchId}`}
                className="grid min-h-14 grid-cols-[1fr_auto_1fr] items-center gap-2 px-3 py-2.5 transition-colors hover:bg-arena-text/[0.035] focus-visible:bg-arena-text/[0.035]"
              >
                <span className={match.placementA < match.placementB ? 'font-semibold' : ''}>{match.entrantAName}</span>
                <span className="val text-arena-dim">{score(match.placementA, match.placementB)}</span>
                <span className={match.placementB < match.placementA ? 'text-right font-semibold' : 'text-right'}>{match.entrantBName}</span>
              </Link>
            ))}
          </div>
        )}
      </section>
    </div>
  );
}

function SeasonClock({ status, startsAt, endsAt }: { status: string; startsAt: string; endsAt: string }) {
  const now = Date.now();
  const start = new Date(startsAt).getTime();
  const end = new Date(endsAt).getTime();
  const days = Math.max(0, Math.ceil(((status === 'scheduled' ? start : end) - now) / 86_400_000));
  return (
    <div className="min-w-36 rounded-md border border-arena-edge px-4 py-3 text-right">
      <p className="lab">{status === 'scheduled' ? 'Starts in' : status === 'open' ? 'Time left' : 'Season state'}</p>
      <p className="type-display mt-1 text-[23px]">
        {status === 'scheduled' || status === 'open' ? `${days} days` : seasonStatus(status)}
      </p>
      <p className="t-micro mt-1">ends {new Date(endsAt).toLocaleDateString()}</p>
    </div>
  );
}

function SeasonFact({ label, value }: { label: string; value: string }) {
  return <div><p className="lab">{label}</p><p className="type-display mt-1 text-[19px]">{value}</p></div>;
}

function EntrantStandingCard({ standing }: { standing: ArcRelaySeasonStanding }) {
  return (
    <article className="border-b border-arena-edge bg-arena-panel p-3 last:border-b-0 md:border-r md:border-b-0 md:last:border-r-0">
      <div className="flex items-center gap-2.5">
        <EntrantCrest crest={standing.entrant.crest} size={42} />
        <div className="min-w-0 flex-1">
          <p className="type-display truncate text-[17px]">{standing.entrant.name}</p>
          <p className="t-micro">rank {standing.rank} · revision {standing.entrant.revision}</p>
        </div>
        <strong className="val text-[17px]">{standing.entrant.rating}</strong>
      </div>
      <div className="mt-3 flex items-center gap-2">
        <RatingSparkline standing={standing} />
        <Record standing={standing} />
      </div>
    </article>
  );
}

function SeasonRow({ standing }: { standing: ArcRelaySeasonStanding }) {
  return (
    <div className="grid min-h-16 grid-cols-[36px_40px_minmax(0,1fr)_auto] items-center gap-2 px-3 py-2 sm:grid-cols-[40px_42px_minmax(150px,1.5fr)_minmax(108px,1fr)_90px_80px]">
      <span className="type-display text-[21px]">{standing.rank}</span>
      <EntrantCrest crest={standing.entrant.crest} size={36} />
      <div className="min-w-0">
        <div className="flex min-w-0 items-baseline gap-2">
          <span className="truncate font-medium">{standing.entrant.name}</span>
        </div>
        <p className="t-micro truncate">{standing.entrant.ownerDisplayName}</p>
        <Composition standing={standing} className="mt-1 sm:hidden" />
      </div>
      <Composition standing={standing} className="hidden sm:flex" />
      <div className="hidden sm:block"><RatingSparkline standing={standing} /></div>
      <div className="text-right">
        <strong className="val block text-[16px]">{standing.entrant.rating}</strong>
        <div className="mt-1 flex items-center justify-end gap-2"><Movement value={standing.rankMovement} /><Record standing={standing} /></div>
      </div>
    </div>
  );
}

function Composition({ standing, className }: { standing: ArcRelaySeasonStanding; className: string }) {
  return (
    <div className={`${className} min-w-0 items-center gap-0.5`} aria-label="Composition">
      {standing.entrant.composition.map((slot) => (
        <ClassIcon key={slot.slot} classId={slot.classId} size={18} label={slot.className} />
      ))}
    </div>
  );
}

function RatingSparkline({ standing }: { standing: ArcRelaySeasonStanding }) {
  const values = standing.ratingHistory.map((point) => point.rating);
  if (values.length < 2) return <span className="t-micro">No movement yet</span>;
  const low = Math.min(...values);
  const high = Math.max(...values);
  const span = high - low || 1;
  const points = values.map((value, index) =>
    `${(index / (values.length - 1)) * 72},${17 - ((value - low) / span) * 14}`).join(' ');
  return (
    <svg width="72" height="20" viewBox="0 0 72 20" role="img" aria-label={`Rating moved from ${values[0]} to ${values.at(-1)}`}>
      <polyline points={points} fill="none" stroke="currentColor" strokeWidth="1.7" strokeLinejoin="round" className="text-arena-material" />
    </svg>
  );
}

function Record({ standing }: { standing: ArcRelaySeasonStanding }) {
  return <span className="val whitespace-nowrap text-arena-dim">{standing.wins}–{standing.draws}–{standing.losses}</span>;
}

function Movement({ value }: { value?: number | null }) {
  if (value == null) return <span className="t-micro">new</span>;
  if (value === 0) return <span className="t-micro">—</span>;
  return <span className={value > 0 ? 't-micro text-arena-ok' : 't-micro text-arena-hot'}>{value > 0 ? `▲${value}` : `▼${Math.abs(value)}`}</span>;
}

function seasonStatus(status: string) {
  return status === 'open' ? 'Open' : status === 'closing' ? 'Closing' : status === 'closed' ? 'Final' : status === 'scheduled' ? 'Scheduled' : 'Draft';
}

function score(a: number, b: number) {
  if (a === b) return 'draw';
  return a < b ? '1–0' : '0–1';
}
