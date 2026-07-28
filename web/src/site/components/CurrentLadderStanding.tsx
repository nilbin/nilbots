import type { BotDetail } from '../api';

export default function CurrentLadderStanding({
  standing,
}: {
  standing: BotDetail['currentStanding'];
}) {
  return (
    <section
      className={
        'panel-quiet pad flex flex-wrap items-center gap-x-3.5 gap-y-1.5 ' +
        (standing ? 'border-arena-edge2 bg-arena-raise' : '')
      }
    >
      <span className="lab">Current ladder</span>
      {standing ? (
        <>
          <strong className="type-display tabular text-[22px] text-arena-text">
            #{standing.rank}
          </strong>
          <span className="t-meta">
            <span className="val text-arena-text">
              {standing.rating.toLocaleString()}
            </span>{' '}
            rating
          </span>
          <span className="t-meta">
            <span className="val">
              {standing.rankedSets.toLocaleString()}
            </span>{' '}
            ranked{' '}
            {standing.rankedSets === 1 ? 'set' : 'sets'} · rules{' '}
            <span className="val">{standing.rulesVersion}</span>
          </span>
        </>
      ) : (
        <>
          <strong className="t-body font-semibold text-arena-text">Unranked</strong>
          <span className="t-meta">
            No completed ranked set on the current rules ladder yet.
          </span>
        </>
      )}
    </section>
  );
}
