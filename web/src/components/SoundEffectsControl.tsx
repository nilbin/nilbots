import clsx from 'clsx';
import type { ReplaySoundEffectsController } from '../audio/useReplaySoundEffects';

export default function SoundEffectsControl({
  effects,
}: {
  effects: ReplaySoundEffectsController;
}) {
  const audible = effects.enabled && !effects.muted;
  const label = !effects.enabled
    ? 'Enable sound effects'
    : effects.muted
      ? 'Unmute sound effects'
      : 'Mute sound effects';
  const statusText = effects.activating
    ? 'LOADING SFX'
    : effects.error
      ? 'SFX ERROR'
      : !effects.enabled
        ? 'SFX READY · INTERACT TO START'
        : effects.muted
          ? 'SFX MUTED'
          : effects.suspendedForSpeed
            ? 'SFX PAUSED ABOVE 2×'
            : effects.packLabel.toUpperCase();
  const toggle = () => {
    if (!effects.enabled) {
      void effects.enable().catch(() => {
        // The controller exposes the error in this control.
      });
      return;
    }
    effects.setMuted(!effects.muted);
  };

  return (
    <div
      data-sound-effects-control
      className="flex items-center gap-2 rounded-md border border-arena-edge bg-arena-panel/90 px-2 py-1"
      title={effects.error ?? effects.packLabel}
    >
      <button
        type="button"
        onClick={toggle}
        disabled={effects.activating}
        aria-label={label}
        aria-pressed={audible}
        className={clsx(
          'flex size-7 items-center justify-center rounded font-mono text-sm transition-colors disabled:opacity-50',
          audible
            ? 'bg-arena-accent/15 text-arena-accent hover:bg-arena-accent/25'
            : 'text-arena-dim hover:bg-arena-edge hover:text-arena-text',
        )}
      >
        {effects.activating ? (
          '…'
        ) : (
          <span
            className={clsx(
              'text-[8px] font-bold tracking-tight',
              !audible && 'line-through',
            )}
          >
            SFX
          </span>
        )}
      </button>
      <span className="hidden max-w-44 truncate font-mono text-[10px] text-arena-dim sm:block">
        {statusText}
      </span>
      <span className="sr-only" role="status" aria-live="polite">
        {effects.error ? `${statusText}: ${effects.error}` : statusText}
      </span>
      {effects.enabled && (
        <input
          type="range"
          min={0}
          max={1}
          step={0.01}
          value={effects.volume}
          onChange={(event) =>
            effects.setVolume(Number(event.currentTarget.value))
          }
          aria-label="Sound effects volume"
          className="h-1 w-16 accent-sky-400"
        />
      )}
    </div>
  );
}
