import clsx from 'clsx';

/**
 * A signed movement shared by ladder positions and rating receipts.
 *
 * Outcome is the one system-owned use of colour, and only the glyph carries it. The
 * explicit direction text keeps the meaning intact for screen readers and when colour
 * cannot be perceived.
 */
export default function Movement({
  change,
  suffix,
  before = null,
}: {
  change: number | null;
  suffix?: string;
  before?: number | null;
}) {
  if (change === null) return null;
  const rounded = Math.round(change * 10) / 10;
  const direction = rounded > 0 ? 'Up' : rounded < 0 ? 'Down' : 'Unchanged';

  return (
    <span className="t-micro inline-flex shrink-0 items-baseline gap-1 whitespace-nowrap">
      <span
        aria-hidden
        className={clsx(
          rounded > 0 && 'text-arena-ok',
          rounded < 0 && 'text-arena-hot',
        )}
      >
        {rounded > 0 ? '▲' : rounded < 0 ? '▼' : '—'}
      </span>
      <span className="sr-only">{direction} </span>
      <span className="val">{Math.abs(rounded)}</span>
      {suffix && <span>{suffix}</span>}
      {before !== null && (
        <span className="val">
          ({Math.round(before)} → {Math.round(before + change)})
        </span>
      )}
    </span>
  );
}
