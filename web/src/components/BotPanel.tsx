import clsx from 'clsx';
import type { ReplayDocument } from '../types';
import { stateBefore } from '../render/interpolate';

interface BotPanelProps {
  replay: ReplayDocument;
  tick: number;
  selectedSlot: number | null;
  showVisibility: boolean;
  onSelectSlot: (slot: number | null) => void;
  onToggleVisibility: () => void;
}

export default function BotPanel({
  replay,
  tick,
  selectedSlot,
  showVisibility,
  onSelectSlot,
  onToggleVisibility,
}: BotPanelProps) {
  const tickData = replay.ticks[Math.min(tick, replay.ticks.length - 1)];
  const states = stateBefore(replay, tick + 1);

  return (
    <div className="flex flex-col gap-3">
      {replay.header.participants.map((participant) => {
        const state = states.find((s) => s.slot === participant.slot)!;
        const botTick = tickData.bots.find((b) => b.slot === participant.slot);
        const selected = selectedSlot === participant.slot;
        return (
          <button
            key={participant.slot}
            onClick={() => onSelectSlot(selected ? null : participant.slot)}
            aria-pressed={selected}
            className={clsx(
              'rounded-lg border p-3 text-left transition-colors',
              selected
                ? 'border-arena-accent/70 bg-arena-panel'
                : 'border-arena-edge bg-arena-panel/60 hover:border-arena-dim',
            )}
          >
            <div className="flex items-center gap-2">
              <span
                className="inline-block size-3 rounded-full"
                style={{ background: participant.accent }}
                aria-hidden
              />
              <span className="font-semibold">{participant.name}</span>
              <span className="font-mono text-[11px] text-arena-dim">
                slot {participant.slot} · {participant.runtimeKind}
              </span>
              <span
                className={clsx('ml-auto font-mono text-[11px]', {
                  'text-emerald-400': state.status === 'Active',
                  'text-red-400': state.status === 'Destroyed',
                  'text-amber-400': state.status === 'Disqualified',
                })}
              >
                {state.status.toUpperCase()}
              </span>
            </div>

            <div className="mt-2 flex items-center gap-3 font-mono text-xs">
              <span aria-label={`Health ${state.health} of 3`}>
                {Array.from({ length: 3 }, (_, i) => (
                  <span
                    key={i}
                    className={i < state.health ? 'text-arena-accent' : 'text-arena-edge'}
                  >
                    ♥
                  </span>
                ))}
              </span>
              <span className="text-arena-dim">
                CD <span className="text-arena-text">{state.cooldown}</span>
              </span>
              {botTick && (
                <span className="text-arena-dim">
                  → <span className="text-arena-text">{botTick.chosenAction}</span>
                  {botTick.result !== 'Success' && (
                    <span className="text-amber-400"> ({botTick.result})</span>
                  )}
                </span>
              )}
            </div>

            {selected && botTick?.debug && (
              <pre className="mt-2 overflow-x-auto rounded bg-arena-bg/80 p-2 font-mono text-[11px] whitespace-pre-wrap text-arena-dim">
                {botTick.debug}
              </pre>
            )}
            {selected && botTick && (
              <div className="mt-2 font-mono text-[11px] text-arena-dim">
                sees {botTick.visibleTiles.length} tiles ·{' '}
                {botTick.visibleEnemies.length > 0
                  ? `enemy at ${botTick.visibleEnemies
                      .map((e) => `(${e.x},${e.y})`)
                      .join(' ')}`
                  : 'no enemies visible'}
              </div>
            )}
          </button>
        );
      })}

      <label className="flex cursor-pointer items-center gap-2 px-1 text-xs text-arena-dim select-none">
        <input
          type="checkbox"
          checked={showVisibility}
          onChange={onToggleVisibility}
          className="accent-(--color-arena-accent)"
        />
        Show selected bot's field of view
      </label>
    </div>
  );
}
