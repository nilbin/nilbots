import type { BotDetail } from '../api';

export default function CurrentLadderStanding({
  standing,
}: {
  standing: BotDetail['currentStanding'];
}) {
  return (
    <section
      className={
        'flex flex-wrap items-center gap-x-5 gap-y-1 rounded-lg border px-4 py-3 ' +
        (standing
          ? 'border-arena-accent/50 bg-arena-accent/5'
          : 'border-arena-edge bg-arena-panel/40')
      }
    >
      <span className="font-mono text-[11px] tracking-wider text-arena-dim">
        CURRENT LADDER
      </span>
      {standing ? (
        <>
          <strong className="text-xl text-arena-accent">#{standing.rank}</strong>
          <span className="font-mono text-sm">
            {standing.rating.toLocaleString()} rating
          </span>
          <span className="text-xs text-arena-dim">
            {standing.rankedSets.toLocaleString()} ranked{' '}
            {standing.rankedSets === 1 ? 'set' : 'sets'} · rules{' '}
            {standing.rulesVersion}
          </span>
        </>
      ) : (
        <>
          <strong className="text-sm">Unranked</strong>
          <span className="text-xs text-arena-dim">
            No completed ranked set on the current rules ladder yet.
          </span>
        </>
      )}
    </section>
  );
}
