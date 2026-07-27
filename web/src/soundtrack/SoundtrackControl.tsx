import clsx from 'clsx';
import type { SoundtrackStatus } from './types';

export interface SoundtrackController {
  status: SoundtrackStatus;
  enabled: boolean;
  title: string | null;
  volume: number;
  error: string | null;
  toggle: () => void;
  setVolume: (volume: number) => void;
}

export default function SoundtrackControl({
  controller,
}: {
  controller: SoundtrackController;
}) {
  if (controller.status === 'unavailable') return null;

  const label = controller.enabled ? 'Disable soundtrack' : 'Enable soundtrack';
  const statusText =
    controller.status === 'armed'
      ? 'SOUNDTRACK READY · INTERACT TO START'
      : controller.status === 'loading'
        ? 'LOADING SCORE'
        : controller.status === 'paused'
          ? 'SCORE PAUSED'
          : controller.status === 'error'
            ? 'SCORE ERROR'
            : controller.enabled
              ? (controller.title?.toUpperCase() ?? 'SOUNDTRACK ON')
              : 'SOUNDTRACK OFF';
  const accessibleStatus =
    controller.status === 'error' && controller.error
      ? `${statusText}: ${controller.error}`
      : statusText;
  const controlTitle =
    controller.error ??
    (controller.status === 'armed'
      ? 'Soundtrack is enabled and will start with the next viewer interaction'
      : controller.title ?? 'Soundtrack');

  return (
    <div
      data-soundtrack-control
      className="flex items-center gap-2 rounded-md border border-arena-edge bg-arena-panel/90 px-2 py-1"
      title={controlTitle}
    >
      <button
        type="button"
        onClick={controller.toggle}
        aria-label={label}
        aria-pressed={controller.enabled}
        className={clsx(
          'flex size-7 items-center justify-center rounded font-mono text-sm transition-colors',
          controller.enabled
            ? 'bg-arena-accent/15 text-arena-accent hover:bg-arena-accent/25'
            : 'text-arena-dim hover:bg-arena-edge hover:text-arena-text',
        )}
      >
        {controller.status === 'loading' ? '…' : controller.enabled ? '♫' : '♩'}
      </button>
      <span className="hidden max-w-40 truncate font-mono text-[10px] text-arena-dim sm:block">
        {statusText}
      </span>
      <span className="sr-only" role="status" aria-live="polite">
        {accessibleStatus}
      </span>
      {controller.enabled && (
        <input
          type="range"
          min={0}
          max={1}
          step={0.01}
          value={controller.volume}
          onChange={(event) => controller.setVolume(Number(event.target.value))}
          aria-label="Soundtrack volume"
          className="h-1 w-16 accent-sky-400"
        />
      )}
    </div>
  );
}
