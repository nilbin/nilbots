import clsx from 'clsx';

/**
 * The thing between a cold page and a match.
 *
 * A replay used to start itself. The clock ran from the moment the component mounted, and
 * on anything but a warm cache that meant the opening seconds of the fight played out over
 * a black field while the models and atlases were still arriving — the viewer was watching
 * a match it could not see, and by the time it could, it had missed the first exchange.
 *
 * So playback is now something you ask for, once, and this is where you ask. Two states
 * and one control:
 *
 * - **loading** — the button is genuinely `disabled`, not merely dimmed, so a keyboard and
 *   a screen reader agree with the eye; the count beneath it is the honest number of
 *   outstanding assets rather than a spinner that means nothing.
 * - **ready** — the accent lights, the ring settles, and pressing it starts the clock.
 *
 * There is no third state: once playback has begun the overlay is gone entirely, because a
 * scrim over a running match is worse than no scrim at all. The transport underneath owns
 * pause, seek and replay from there.
 *
 * The press is also what the audio session needs. Browsers will not resume an AudioContext
 * without a trusted gesture, and a viewer that auto-played had none to offer — the first
 * shots were silent even when sound was on. A deliberate press is that gesture.
 */
export default function PlayOverlay({
  ready,
  pending,
  onPlay,
}: {
  /** Every model, texture and cue the opening frame needs has arrived. */
  ready: boolean;
  /** How many are still outstanding, shown while they are. */
  pending: number;
  onPlay: () => void;
}) {
  return (
    <div
      className="absolute inset-0 z-10 flex items-center justify-center bg-arena-bg/80 backdrop-blur-[2px]"
      data-play-overlay={ready ? 'ready' : 'loading'}
    >
      <div className="flex flex-col items-center gap-4">
        <button
          type="button"
          onClick={onPlay}
          disabled={!ready}
          data-play-button
          aria-label={ready ? 'Play match' : 'Loading match'}
          className={clsx(
            'group relative grid size-[76px] place-items-center rounded-full border transition-[border-color,background-color,transform] duration-200',
            ready
              ? 'cursor-pointer border-arena-edge2 bg-arena-panel/90 hover:border-arena-ok active:translate-y-px'
              : 'cursor-not-allowed border-arena-edge bg-arena-panel/60',
          )}
        >
          {/* The ring is the progress: it sweeps while assets are outstanding and holds
              still, in the accent, once they are not. Drawn as a border rather than an
              element so it cannot drift out of round on a fractional device pixel. */}
          <span
            aria-hidden
            className={clsx(
              'absolute inset-[-5px] rounded-full border',
              ready
                ? 'border-arena-ok/40'
                : 'animate-spin border-arena-edge border-t-arena-dim [animation-duration:1.4s]',
            )}
          />
          {/* A triangle, not a glyph: ▶ renders as an emoji on some platforms and as a
              different weight on every other one. */}
          <svg
            viewBox="0 0 24 24"
            className={clsx(
              'ml-[3px] size-7 transition-colors duration-200',
              ready
                ? 'fill-arena-text group-hover:fill-arena-ok'
                : 'fill-arena-edge2',
            )}
            aria-hidden
          >
            <path d="M6 3.4 20.4 12 6 20.6Z" />
          </svg>
        </button>

        <p className="lab text-center" role="status">
          {ready ? (
            'Watch the match'
          ) : (
            <>
              Loading arena
              {pending > 0 && (
                <>
                  {' — '}
                  <span className="tabular">{pending}</span>
                  {pending === 1 ? ' asset' : ' assets'}
                </>
              )}
            </>
          )}
        </p>
      </div>
    </div>
  );
}
