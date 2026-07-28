import { useMemo } from 'react';
import clsx from 'clsx';
import { adjustAccentForBackground } from '../../render/adaptiveAccent';
import { EmptyState } from './StateView';

/**
 * Rating across a bot's generations, on one axis.
 *
 * The question this answers is the one every player has after every submit: is the new
 * generation better than the one it replaced. So the generations are laid out
 * *consecutively* — one stretch of the axis each, in submit order, divided by a dashed
 * rule — rather than overlaid on a shared x. They did not happen at the same time, and
 * drawing them as if they had would invite a comparison of shapes instead of levels.
 *
 * Only the live generation gets the bot's accent. The ones it replaced are structure.
 */

/** One generation's ratings, in the order the ladder recorded them. */
export interface GenerationRatings {
  /** The counter the CLI prints on submit: `gen-10`. */
  generation: number;
  /** Every rating this generation held. Empty is legal — it keeps its stretch of axis. */
  ratings: readonly number[];
  /** The generation currently on the ladder. */
  live: boolean;
}

interface GenerationsChartProps {
  /** Oldest first: the axis is time, and one generation follows another on it. */
  series: readonly GenerationRatings[];
  /** The bot's own accent — server data, and the only saturated colour here. */
  accent: string;
  /** The live generation, which is known even when no history is. */
  liveGeneration?: number | null;
  /** Right-aligned context under the legend, e.g. when the live generation landed. */
  note?: string | null;
  emptyTitle?: string;
  emptyDetail?: string;
}

/* The mock draws this 1120 units wide because that is how wide it renders there: its
   coordinate space is CSS pixels at the width of the page it sits on. The bot page gives
   this panel a 340px rail less than that, so the space is narrower — but every other
   number is the mock's own, which is what makes the 52px label gutter, the 11.5px mono
   labels and the 16/28 margins land at the size they were drawn at. */
const width = 720;
const height = 210;
const padLeft = 52;
const padRight = 14;
const padTop = 16;
const padBottom = 28;
const plotWidth = width - padLeft - padRight;
const plotHeight = height - padTop - padBottom;

/** The live line is drawn heavier, and ends in a bigger dot, than the ones it replaced. */
const liveStroke = 2.4;
const pastStroke = 1.7;
const liveDot = 4.5;
const pastDot = 3.5;
const liveDotRing = 2.5;
const pastDotRing = 1.6;
const labelSize = 11.5;

/** `--color-arena-panel`, as a value: the contrast check needs a colour, not a class. */
const panelBackground = '#191210';

export default function GenerationsChart({
  series,
  accent,
  liveGeneration = null,
  note = null,
  emptyTitle = 'No rating history yet',
  emptyDetail,
}: GenerationsChartProps) {
  // The accent is a player's pick, so it can be anything — including something that
  // vanishes on a panel this dark. Adjust before drawing, never before storing.
  const drawn = useMemo(
    () => adjustAccentForBackground(accent, panelBackground),
    [accent],
  );
  const chart = useMemo(() => layout(series), [series]);

  const live =
    liveGeneration ?? series.find((generation) => generation.live)?.generation ?? null;

  return (
    <section className="panel pad">
      <div className="mb-2.5 flex items-center justify-between gap-2.5">
        <h2 className="lab">Rating · every generation on one axis</h2>
        {live !== null && <span className="pill shrink-0">gen-{live} live</span>}
      </div>

      {chart === null ? (
        <EmptyState title={emptyTitle} detail={emptyDetail} />
      ) : (
        <>
          <svg
            viewBox={`0 0 ${width} ${height}`}
            className="block h-auto w-full"
            role="img"
            aria-label={describe(chart.lines)}
          >
            {/* Two gridlines, because a rating chart needs a level and not a grid. */}
            {chart.grid.map((mark) => (
              <g key={mark.value}>
                <line
                  className="stroke-arena-edge"
                  x1={padLeft}
                  y1={mark.y}
                  x2={width - padRight}
                  y2={mark.y}
                  strokeWidth={1}
                />
                <text
                  className="tabular fill-arena-dim font-mono"
                  x={6}
                  y={mark.y + 3.5}
                  fontSize={labelSize}
                >
                  {mark.value}
                </text>
              </g>
            ))}

            {chart.lines.map((entry, index) => {
              const last = entry.points[entry.points.length - 1];
              return (
                <g key={entry.generation}>
                  {/* The rule between two generations: where a submit replaced one. */}
                  {index > 0 && (
                    <line
                      className="stroke-arena-edge"
                      x1={entry.startX}
                      y1={padTop}
                      x2={entry.startX}
                      y2={padTop + plotHeight}
                      strokeWidth={1}
                      strokeDasharray="2 4"
                    />
                  )}
                  {entry.points.length > 1 && (
                    <polyline
                      className={entry.live ? undefined : 'stroke-arena-edge2'}
                      points={entry.points
                        .map((point) => `${point.x.toFixed(1)},${point.y.toFixed(1)}`)
                        .join(' ')}
                      fill="none"
                      stroke={entry.live ? drawn : undefined}
                      strokeWidth={entry.live ? liveStroke : pastStroke}
                      strokeLinejoin="round"
                    />
                  )}
                  {/* Where a generation ended up is the number being compared, so it is
                      the only reading on the line that is drawn as a point. */}
                  {last !== undefined && (
                    <circle
                      className={clsx(
                        'fill-arena-bg',
                        !entry.live && 'stroke-arena-edge2',
                      )}
                      cx={last.x.toFixed(1)}
                      cy={last.y.toFixed(1)}
                      r={entry.live ? liveDot : pastDot}
                      stroke={entry.live ? drawn : undefined}
                      strokeWidth={entry.live ? liveDotRing : pastDotRing}
                    />
                  )}
                  <text
                    className="tabular fill-arena-dim font-mono"
                    x={entry.startX + 2}
                    y={height - 6}
                    fontSize={labelSize}
                  >
                    gen-{entry.generation}
                  </text>
                </g>
              );
            })}
          </svg>

          <div className="t-meta mt-2 flex flex-wrap items-center gap-3.5">
            {/* Newest first: the legend is read to find the live one. */}
            {chart.lines
              .slice()
              .reverse()
              .map((entry) => (
                <span
                  key={entry.generation}
                  className="inline-flex items-center gap-1.5"
                >
                  <span
                    aria-hidden="true"
                    className={clsx(
                      'block h-0.5 w-3.5 rounded-[2px]',
                      !entry.live && 'bg-arena-edge2',
                    )}
                    style={entry.live ? { background: drawn } : undefined}
                  />
                  {/* A generation number is a counter the toolchain assigned, like a
                      seed or a tick, and it has to read as the same token as the label
                      drawn under its stretch of the axis. */}
                  <span className="tabular font-mono">
                    gen-{entry.generation} ·{' '}
                    <span className={entry.live ? 'text-arena-text' : undefined}>
                      {entry.ended === null ? '—' : Math.round(entry.ended)}
                    </span>
                  </span>
                </span>
              ))}
            {note !== null && <span className="ml-auto">{note}</span>}
          </div>
        </>
      )}
    </section>
  );
}

interface LaidOutLine extends GenerationRatings {
  points: { x: number; y: number }[];
  /** Left edge of this generation's stretch of the axis. */
  startX: number;
  /** The rating it was on when it stopped being the one that mattered. */
  ended: number | null;
}

/**
 * Turn the series into coordinates, or null when there is nothing to draw.
 *
 * Every generation gets an equal stretch of the axis regardless of how many readings it
 * has, because the stretch stands for "while this generation was live" rather than for a
 * count of sets. A bot with one generation gets the whole axis; a bot with nine gets a
 * ninth each. Nothing here assumes three.
 */
function layout(
  series: readonly GenerationRatings[],
): { lines: LaidOutLine[]; grid: { value: number; y: number }[] } | null {
  const values = series.flatMap((generation) => generation.ratings);
  if (values.length === 0) return null;

  const { lo, hi } = ratingWindow(values);
  const y = (rating: number) => padTop + ((hi - rating) / (hi - lo)) * plotHeight;
  const segment = plotWidth / series.length;

  const lines = series.map((generation, index) => {
    const startX = padLeft + index * segment;
    const count = generation.ratings.length;
    return {
      ...generation,
      startX,
      // One reading sits at the end of its stretch, where the next generation takes over:
      // its position on the axis is "this is where it left off", not an index.
      points: generation.ratings.map((rating, n) => ({
        x: startX + (count === 1 ? 1 : n / (count - 1)) * segment,
        y: y(rating),
      })),
      ended: count === 0 ? null : generation.ratings[count - 1],
    };
  });

  return { lines, grid: gridlines(lo, hi).map((value) => ({ value, y: y(value) })) };
}

/** The rating window, held off the frame so a run does not travel along the edge. */
function ratingWindow(values: readonly number[]): { lo: number; hi: number } {
  const min = Math.min(...values);
  const max = Math.max(...values);
  // The floor is what stops a bot that has barely moved from being drawn as a mountain
  // range: without it a two-point wobble is stretched to fill the panel, and a bot that
  // never moved at all has an axis of zero height.
  const margin = Math.max((max - min) * 0.14, 8);
  return { lo: min - margin, hi: max + margin };
}

const gridSteps = [500, 200, 100, 50, 25, 10];

/**
 * Two gridlines, on the roundest step that puts two of them inside the window — and the
 * top two, because that is where the newest generation is and the older ones are read
 * against it.
 */
function gridlines(lo: number, hi: number): number[] {
  // A window narrower than the finest step still deserves the one line that fits, so the
  // reader has a number to hang the shape on.
  let best: number[] = [];
  for (const step of gridSteps) {
    const lines: number[] = [];
    for (let value = Math.ceil(lo / step) * step; value <= hi; value += step)
      lines.push(value);
    if (lines.length >= 2) return lines.slice(-2);
    if (lines.length > best.length) best = lines;
  }
  return best;
}

/** The chart in a sentence, for a reader who is not looking at it. */
function describe(lines: readonly LaidOutLine[]): string {
  const ends = lines.flatMap((line) =>
    line.ended === null
      ? []
      : [
          `${line.live ? 'the live ' : ''}gen-${line.generation} ends at ${Math.round(line.ended)}`,
        ],
  );
  return `Rating across ${lines.length} generation${lines.length === 1 ? '' : 's'}: ${ends.join(', ')}.`;
}
