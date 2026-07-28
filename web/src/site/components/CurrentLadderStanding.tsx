import type { BotDetail } from '../api';
import { Link } from 'react-router-dom';

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
      <h2 className="lab">Current ladder</h2>
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
            {standing.rankedSets === 1 ? 'set' : 'sets'}
          </span>
        </>
      ) : (
        <>
          <strong className="t-body font-semibold text-arena-text">Unranked</strong>
          <span className="t-meta">
            No completed ranked set on the current ladder yet.
          </span>
        </>
      )}
      <Link to="/" className="t-meta ml-auto text-link">
        View rankings
      </Link>
    </section>
  );
}
