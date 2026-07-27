import clsx from 'clsx';
import type { PlaybackState } from '../playback';
import type { ReplayModel, ReplayStableUnitKey } from '../replayModel';
import Timeline from './Timeline';

const SPEEDS = [0.25, 0.5, 1, 2, 4];

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
    <div className="flex flex-col gap-3 rounded-lg border border-arena-edge bg-arena-panel p-4">
      <div className="flex items-center gap-4">
        <span className="tabular font-mono text-xs text-arena-dim">
          <span className="text-arena-text">
            {String(playback.tick).padStart(3, '0')}
          </span>
          /{String(playback.tickCount - 1).padStart(3, '0')}
        </span>
        <div className="min-w-0 flex-1">
          <Timeline
            replay={replay}
            playback={playback}
            selectedUnitKey={selectedUnitKey}
          />
        </div>
      </div>

      <div className="flex flex-wrap items-center gap-2">
        <ControlButton label="Restart" onClick={playback.restart}>
          ⟲
        </ControlButton>
        <ControlButton label="Step back one tick" onClick={() => playback.step(-1)}>
          ⏮
        </ControlButton>
        <ControlButton
          label={playback.playing ? 'Pause' : 'Play'}
          onClick={playback.toggle}
          primary
        >
          {playback.playing ? '⏸' : '▶'}
        </ControlButton>
        <ControlButton label="Step forward one tick" onClick={() => playback.step(1)}>
          ⏭
        </ControlButton>

        <div className="ml-auto flex items-center gap-1" role="group" aria-label="Playback speed">
          {SPEEDS.map((speed) => (
            <button
              key={speed}
              onClick={() => playback.setSpeed(speed)}
              className={clsx(
                'rounded px-2 py-1 font-mono text-xs transition-colors',
                playback.speed === speed
                  ? 'bg-arena-accent text-slate-950'
                  : 'text-arena-dim hover:bg-arena-edge hover:text-arena-text',
              )}
            >
              {speed}×
            </button>
          ))}
        </div>
      </div>
    </div>
  );
}

function ControlButton({
  children,
  label,
  onClick,
  primary,
}: {
  children: React.ReactNode;
  label: string;
  onClick: () => void;
  primary?: boolean;
}) {
  return (
    <button
      onClick={onClick}
      aria-label={label}
      title={label}
      className={clsx(
        'flex size-9 items-center justify-center rounded-md border text-sm transition-colors',
        primary
          ? 'border-arena-accent bg-arena-accent/15 text-arena-accent hover:bg-arena-accent/25'
          : 'border-arena-edge text-arena-text hover:bg-arena-edge',
      )}
    >
      {children}
    </button>
  );
}
