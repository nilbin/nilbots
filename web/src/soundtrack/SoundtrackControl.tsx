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
      className="panel-quiet flex items-center gap-1.5 px-1.5 py-1"
      title={controlTitle}
    >
      <button
        type="button"
        onClick={controller.toggle}
        aria-label={label}
        aria-pressed={controller.enabled}
        className={clsx(
          'btn flex size-7 items-center justify-center p-0',
          controller.enabled
            ? 'btn-on'
            : 'text-arena-dim hover:text-arena-text',
        )}
      >
        {controller.status === 'loading' ? '…' : controller.enabled ? '♫' : '♩'}
      </button>
      <span className="lab hidden max-w-40 truncate sm:block">
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
          className="h-1 w-16 accent-(--color-arena-text)"
        />
      )}
    </div>
  );
}
