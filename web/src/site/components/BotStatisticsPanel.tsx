import { useEffect, useState } from 'react';
import { api, type BotRecord, type BotStatistics } from '../api';

export default function BotStatisticsPanel({ botId }: { botId: string }) {
  const [statistics, setStatistics] = useState<BotStatistics | null>(null);

  useEffect(() => {
    void api
      .get<BotStatistics>(`/api/bots/${botId}/stats`)
      .then(setStatistics)
      .catch(() => setStatistics(null));
  }, [botId]);

  if (!statistics) return null;

  return (
    <section>
      <h2 className="mb-3 font-mono text-xs tracking-widest text-arena-dim">
        PERFORMANCE
      </h2>
      <div className="grid gap-3 sm:grid-cols-3">
        <RecordCard label="Overall" record={statistics.overall} unit="match" featured />
        <RecordCard label="Ranked" record={statistics.ranked} unit="set" />
        <RecordCard label="Unranked" record={statistics.unranked} unit="match" />
      </div>
      <div className="mt-3 rounded-lg border border-arena-edge bg-arena-panel/40 px-4 py-3">
        <p className="font-mono text-[11px] tracking-wider text-arena-dim">
          COMBAT TOTALS
        </p>
        <p className="mt-1 text-sm text-arena-text">
          {formatCount(statistics.combat.games, 'arena game')}{' '}
          <span className="text-arena-dim">·</span>{' '}
          {statistics.combat.damageDealt.toLocaleString()} damage dealt{' '}
          <span className="text-arena-dim">·</span>{' '}
          {formatCount(statistics.combat.faults, 'fault')}
        </p>
      </div>
      <p className="mt-2 text-xs text-arena-dim">
        A ranked set and an unranked challenge each count as one match. Combat
        totals retain all six arena games inside every ranked set.
      </p>
    </section>
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
  return (
    <article
      className={
        'rounded-lg border p-4 ' +
        (featured
          ? 'border-arena-accent/50 bg-arena-accent/5'
          : 'border-arena-edge bg-arena-panel/60')
      }
    >
      <div className="flex items-baseline justify-between gap-2">
        <h3 className="font-mono text-xs tracking-wider text-arena-dim">
          {label.toUpperCase()}
        </h3>
        <span className="font-mono text-[11px] text-arena-dim">
          {formatCount(
            record.played,
            unit,
            unit === 'match' ? 'matches' : undefined,
          )}
        </span>
      </div>
      <p className="mt-3 text-xl font-bold">
        <span className="text-emerald-400">{record.wins}W</span>{' '}
        <span className="text-red-400">{record.losses}L</span>{' '}
        <span className="text-arena-text">{record.draws}D</span>
      </p>
      <p className="mt-1 font-mono text-[11px] text-arena-dim">
        {winRate}% win rate
      </p>
    </article>
  );
}

function formatCount(value: number, singular: string, plural = `${singular}s`) {
  return `${value.toLocaleString()} ${value === 1 ? singular : plural}`;
}
