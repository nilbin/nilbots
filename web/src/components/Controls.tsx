import clsx from 'clsx';
import type { PlaybackState } from '../playback';
import type { ReplayModel, ReplayStableUnitKey } from '../replayModel';
import Timeline from './Timeline';

const SPEEDS = [0.25, 0.5, 1, 2, 4];

/**
 * The transport: one bar, then a legend for the marks in it.
 *
 * Everything sits on a single row because the timeline is the spine — buttons on their
 * own line above it made the track look like a caption of the controls rather than the
 * thing they move through. The track drops to a full-width line of its own below about
 * 640px instead of squeezing to nothing: the page may not scroll sideways at 390px.
 *
 * The tick is not here. It reads over the arena, where the eye already is, which is also
 * what keeps it visible when this bar is not (`Viewer`'s tick badge).
 */
export default function Controls({
  playback,
  replay,
  selectedUnitKey,
}: {
  playback: PlaybackState;
  replay: ReplayModel;
  selectedUnitKey: ReplayStableUnitKey | null;
}) {
  return (
    <div className="panel px-3 py-2.5">
      <div className="flex flex-wrap items-center gap-2.5">
        <TransportButton label="Restart" onClick={playback.restart}>
          ⟲
        </TransportButton>
        <TransportButton
          label="Step back one tick"
          onClick={() => playback.step(-1)}
        >
          ◀|
        </TransportButton>
        <TransportButton
          label={playback.playing ? 'Pause' : 'Play'}
          onClick={playback.toggle}
          wide
        >
          {playback.playing ? '❚❚' : '▶'}
        </TransportButton>
        <TransportButton
          label="Step forward one tick"
          onClick={() => playback.step(1)}
        >
          |▶
        </TransportButton>

        <div className="order-last w-full min-w-0 sm:order-none sm:w-auto sm:flex-1">
          <Timeline
            replay={replay}
            playback={playback}
            selectedUnitKey={selectedUnitKey}
          />
        </div>

        <div
          className="ml-auto flex flex-none items-center gap-1"
          role="group"
          aria-label="Playback speed"
        >
          {SPEEDS.map((speed) => (
            <button
              key={speed}
              type="button"
              onClick={() => playback.setSpeed(speed)}
              aria-pressed={playback.speed === speed}
              className={clsx(
                'btn val px-[9px] py-[5px]',
                playback.speed === speed && 'btn-on text-arena-text',
              )}
            >
              {speed}×
            </button>
          ))}
        </div>
      </div>

      {/* What the marks on the track mean. The swatches are shapes, not colours: a mark
          is drawn in the accent of whoever it happened to, and no legend can stand in
          for somebody's own colour — so weight carries the meaning here as it does
          there. */}
      <p className="t-meta mt-2 flex flex-wrap items-center gap-x-3.5 gap-y-1">
        <span className="inline-flex items-center gap-1.5">
          <i aria-hidden className="block h-[11px] w-[2px] bg-arena-dim/70" />
          fired
        </span>
        <span className="inline-flex items-center gap-1.5">
          <i
            aria-hidden
            className="block h-[11px] w-[5px] rounded-[2px] bg-arena-dim"
          />
          took a hit
        </span>
        <span className="inline-flex items-center gap-1.5">
          <i aria-hidden className="block size-2.5 rounded-[2px] bg-arena-hot" />
          destroyed
        </span>
        <span className="ml-auto">click a mark to seek · ← → step a tick</span>
      </p>
    </div>
  );
}

/**
 * Play is wider and carries the text colour; a step is a square and sits back in dim.
 * The size difference is the hierarchy — the two never needed a second one in colour,
 * least of all the accent, which in this palette is the shop rather than a signal.
 */
function TransportButton({
  children,
  label,
  onClick,
  wide,
}: {
  children: React.ReactNode;
  label: string;
  onClick: () => void;
  wide?: boolean;
}) {
  return (
    <button
      type="button"
      onClick={onClick}
      aria-label={label}
      title={label}
      className={clsx(
        'btn grid flex-none place-items-center p-0',
        wide
          ? 'h-8 w-[38px] text-[11px] tracking-[0.1em]'
          : 'size-[30px] text-arena-dim',
      )}
    >
      {children}
    </button>
  );
}
