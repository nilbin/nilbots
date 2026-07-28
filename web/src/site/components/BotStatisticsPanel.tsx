import { type BotRecord } from '../api';
import { useBotStats } from '../queries';
import { ErrorState, LoadingState } from './StateView';

export default function BotStatisticsPanel({ botId }: { botId: string }) {
  const { data: statistics, isPending, isError, error, refetch } = useBotStats(botId);

  return (
    <section>
      <h2 className="lab mb-2">Performance</h2>
      {isPending ? (
        <LoadingState label="Loading performance…" />
      ) : isError ? (
        // Previously `.catch(() => setStatistics(null))`, which made a failed request
        // indistinguishable from a pending one and left the section silently absent —
        // the reader could not tell the bot had no record from the server being down.
        <ErrorState error={error} onRetry={() => void refetch()} />
      ) : (
        <BotStatisticsContent statistics={statistics} />
      )}
    </section>
  );
}

function BotStatisticsContent({
  statistics,
}: {
  statistics: NonNullable<ReturnType<typeof useBotStats>['data']>;
}) {
  return (
    <>
      <div className="grid gap-2.5 sm:grid-cols-3">
        <RecordCard label="Overall" record={statistics.overall} unit="match" featured />
        <RecordCard label="Ranked" record={statistics.ranked} unit="set" />
        <RecordCard label="Unranked" record={statistics.unranked} unit="match" />
      </div>
      <div className="panel-quiet pad mt-2.5">
        <p className="lab">Combat totals</p>
        <p className="t-body tabular mt-1 text-arena-text">
          {formatCount(statistics.combat.games, 'arena game')}{' '}
          <span className="text-arena-dim">·</span>{' '}
          {statistics.combat.damageDealt.toLocaleString()} damage dealt{' '}
          <span className="text-arena-dim">·</span>{' '}
          {formatCount(statistics.combat.faults, 'fault')}
        </p>
      </div>
      <p className="t-meta mt-2">
        A ranked set and an unranked challenge each count as one match. Combat
        totals retain all six arena games inside every ranked set.
      </p>
    </>
  );
}

function RecordCard({
  label,
  record,
  unit,
  featured = false,
}: {
  label: string;
  record: BotRecord;
  unit: string;
  featured?: boolean;
}) {
  const winRate =
    record.played === 0 ? 0 : Math.round((record.wins / record.played) * 100);
  const playedUnit =
    record.played === 1 ? unit : unit === 'match' ? 'matches' : `${unit}s`;
  return (
    <article
      className={
        'pad ' +
        (featured ? 'panel border-arena-edge2 bg-arena-raise' : 'panel-quiet')
      }
    >
      <div className="flex items-baseline justify-between gap-2">
        <h3 className="lab">{label}</h3>
        <span className="t-meta">
          <span className="val">{record.played.toLocaleString()}</span>{' '}
          {playedUnit}
        </span>
      </div>
      <p className="t-body tabular mt-2 font-mono text-arena-text">
        {record.wins}W · {record.losses}L · {record.draws}D
      </p>
      <p className="t-micro mt-1">
        <span className="val">{winRate}%</span> win rate
      </p>
    </article>
  );
}

function formatCount(value: number, singular: string, plural = `${singular}s`) {
  return `${value.toLocaleString()} ${value === 1 ? singular : plural}`;
}
