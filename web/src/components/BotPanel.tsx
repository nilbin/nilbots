import clsx from 'clsx';
import { useMemo } from 'react';
import type { ReplayDocument } from '../types';
import { botLook } from '../render/arenaThemes';
import { createPresenter } from '../replayPresentation';

interface BotPanelProps {
  replay: ReplayDocument;
  tick: number;
  selectedSlot: number | null;
  showVisibility: boolean;
  onSelectSlot: (slot: number | null) => void;
  onToggleVisibility: () => void;
}

/**
 * The viewer's per-tick readout. Every rules-derived number here — control pressure,
 * overtime limits, zone tallies, hold phases — comes from `createPresenter`, which the
 * mobile app's native panels also render from. Deriving any of it inline again would
 * make this a second rules surface to keep in step.
 */
export default function BotPanel({
  replay,
  tick,
  selectedSlot,
  showVisibility,
  onSelectSlot,
  onToggleVisibility,
}: BotPanelProps) {
  const presenter = useMemo(() => createPresenter(replay), [replay]);
  const { control, bots } = presenter.at(tick);

  return (
    <div className="flex flex-col gap-3">
      {control && (
        <div className="rounded-lg border border-arena-edge bg-arena-panel/70 p-3">
          <div className="flex justify-between font-mono text-[11px] text-arena-dim">
            <span>{control.names[0]}</span>
            <span>
              {control.overtime ? 'OVERTIME ' : ''}CONTROL {control.pressure > 0 ? '+' : ''}
              {control.pressure} / ±{control.limit}
            </span>
            <span>{control.names[1]}</span>
          </div>
          <div className="relative mt-2 h-2 overflow-hidden rounded-full bg-arena-bg">
            <div className="absolute inset-y-0 left-1/2 w-px bg-arena-dim" />
            <div
              className="absolute top-0 h-full w-1 rounded bg-yellow-400 transition-[left]"
              style={{
                left: `${Math.max(
                  0,
                  Math.min(100, 50 + (50 * control.pressure) / Math.max(1, control.limit)),
                )}%`,
              }}
            />
          </div>
          {control.phase && (
            <p className="mt-2 text-center font-mono text-[10px] tracking-wide text-arena-dim">
              {control.phase}
            </p>
          )}
        </div>
      )}

      {bots.map((bot) => {
        const selected = selectedSlot === bot.slot;
        const look = botLook(replay.header.participants[bot.slot]?.lookId, bot.slot);
        return (
          <button
            key={bot.slot}
            onClick={() => onSelectSlot(selected ? null : bot.slot)}
            aria-pressed={selected}
            className={clsx(
              'rounded-lg border p-3 text-left transition-colors',
              selected
                ? 'border-arena-accent/70 bg-arena-panel'
                : 'border-arena-edge bg-arena-panel/60 hover:border-arena-dim',
            )}
          >
            <div className="flex items-center gap-2">
              <span className="flex size-10 shrink-0 items-center justify-center rounded-md bg-arena-bg/85">
                {look.image && (
                  <img
                    src={look.imageUrl}
                    alt={`${bot.lookLabel} chassis`}
                    className="size-9 object-contain"
                  />
                )}
              </span>
              <span>
                <span className="block font-semibold">{bot.name}</span>
                <span className="block font-mono text-[10px] text-arena-dim">
                  {bot.lookLabel} · slot {bot.slot} · {bot.runtimeKind}
                </span>
              </span>
              <span
                className={clsx('ml-auto font-mono text-[11px]', {
                  'text-emerald-400': bot.status === 'Active',
                  'text-red-400': bot.status === 'Destroyed',
                  'text-amber-400': bot.status === 'Disqualified',
                })}
              >
                {bot.status.toUpperCase()}
              </span>
            </div>

            <div className="mt-2 flex items-center gap-3 font-mono text-xs">
              <span aria-label={`Health ${bot.health} of ${bot.maxHealth}`}>
                {Array.from({ length: bot.maxHealth }, (_, i) => (
                  <span
                    key={i}
                    style={{ color: i < bot.health ? bot.accent : undefined }}
                    className={i < bot.health ? '' : 'text-arena-edge'}
                  >
                    ♥
                  </span>
                ))}
              </span>
              <span className="text-arena-dim">
                CD <span className="text-arena-text">{bot.cooldown}</span>
              </span>
              {bot.energy !== undefined && (
                <span className="text-arena-dim" aria-label={`Energy ${bot.energy}`}>
                  ⚡ <span className="text-arena-text">{bot.energy}</span>
                </span>
              )}
              {control === null && bot.zoneTicks === null ? null : (
                <span
                  className={clsx(bot.holdingZone ? 'text-yellow-400' : 'text-arena-dim')}
                  aria-label={control ? 'Active zone hold' : 'Zone ticks'}
                  title={
                    control
                      ? 'Gold = successfully waiting on a zone tile this tick'
                      : 'Zone ticks (gold = on the zone now)'
                  }
                >
                  ⬢{' '}
                  <span className="text-arena-text">
                    {bot.zoneTicks ?? (bot.holdingZone ? 'HOLD' : 'idle')}
                  </span>
                </span>
              )}
              {bot.action && (
                <span className="text-arena-dim">
                  → <span className="text-arena-text">{bot.action}</span>
                  {bot.actionResult !== 'Success' && (
                    <span className="text-amber-400"> ({bot.actionResult})</span>
                  )}
                </span>
              )}
            </div>

            {selected && bot.debug && (
              <pre className="mt-2 overflow-x-auto rounded bg-arena-bg/80 p-2 font-mono text-[11px] whitespace-pre-wrap text-arena-dim">
                {bot.debug}
              </pre>
            )}
            {selected && bot.action && (
              <div className="mt-2 font-mono text-[11px] text-arena-dim">
                sees {bot.visibleTiles} tiles ·{' '}
                {bot.visibleEnemies.length > 0
                  ? `enemy at ${bot.visibleEnemies.map((e) => `(${e.x},${e.y})`).join(' ')}`
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
