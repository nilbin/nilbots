import clsx from 'clsx';
import { useMemo } from 'react';
import type { ReplayDocument } from '../types';
import {
  botLook,
  presentationAccent,
} from '../render/arenaThemes';
import { adjustAccentForBackground } from '../render/adaptiveAccent';
import { stateBefore } from '../render/interpolate';
import { replayMaxHealth } from '../replayMetadata';

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
  const maxHealth = replayMaxHealth(replay);
  const controlOvertime =
    replay.header.controlOvertimeStartTick !== undefined &&
    tickData.tick >= replay.header.controlOvertimeStartTick;
  const controlLimit = controlOvertime
    ? (replay.header.controlOvertimePressureLimit ?? replay.header.controlPressureLimit)
    : replay.header.controlPressureLimit;
  const controlPressure = tickData.controlPressure ?? 0;
  // Zone scores: hardened replays carry the engine's cumulative tally per tick
  // (state.zoneTicks) — read it, never re-derive. Legacy replays (pre-tally) fall
  // back to re-deriving both accrual modes and keeping the one whose totals match
  // the authoritative result (the pattern that once showed 136/118 for a true 18/0).
  const zone = useMemo(() => {
    const tiles = replay.header.zoneTiles;
    if (!tiles) return null;
    const onZone = new Set(tiles.map(([x, y]) => `${x},${y}`));
    if (replay.header.controlPressureLimit !== undefined)
      return { onZone, cumulative: null };
    if (replay.ticks.some((t) => t.state.some((s) => s.zoneTicks !== undefined))) {
      const cumulative = replay.ticks.map((t) =>
        Object.fromEntries(t.state.map((s) => [s.slot, s.zoneTicks ?? 0])),
      ) as Record<number, number>[];
      return { onZone, cumulative };
    }
    const sharedRun: Record<number, number> = {};
    const exclusiveRun: Record<number, number> = {};
    const shared: Record<number, number>[] = [];
    const exclusive: Record<number, number>[] = [];
    for (const t of replay.ticks) {
      const on = t.state.filter((s) => s.status === 'Active' && onZone.has(`${s.x},${s.y}`));
      for (const s of on) sharedRun[s.slot] = (sharedRun[s.slot] ?? 0) + 1;
      if (on.length === 1) exclusiveRun[on[0].slot] = (exclusiveRun[on[0].slot] ?? 0) + 1;
      shared.push({ ...sharedRun });
      exclusive.push({ ...exclusiveRun });
    }
    const final = replay.result?.bots;
    const matchesResult = (series: Record<number, number>[]) =>
      final !== undefined &&
      final.every((b) => (series[series.length - 1]?.[b.slot] ?? 0) === (b.zoneTicks ?? 0));
    const cumulative = matchesResult(exclusive) ? exclusive : matchesResult(shared) ? shared : exclusive;
    return { onZone, cumulative };
  }, [replay]);
  let controlPhase: string | null = null;
  if (controlLimit !== undefined && zone) {
    const activeOccupants = tickData.state.filter(
      (state) => state.status === 'Active' && zone.onZone.has(`${state.x},${state.y}`),
    );
    if (replay.header.controlBySoleOccupancy) {
      const previousTick = replay.ticks[Math.max(0, Math.min(tick - 1, replay.ticks.length - 1))];
      const previousOccupants =
        tick > 0
          ? previousTick.state.filter(
              (state) =>
                state.status === 'Active' && zone.onZone.has(`${state.x},${state.y}`),
            )
          : [];
      const soleName =
        activeOccupants.length === 1
          ? (replay.header.participants[activeOccupants[0].slot]?.name ??
            `slot ${activeOccupants[0].slot}`)
          : null;
      controlPhase =
        activeOccupants.length === 1
          ? previousOccupants.length > 1
            ? `CONTEST BROKEN · ${soleName} GAINS`
            : `SOLE OCCUPANT · ${soleName} GAINS`
          : activeOccupants.length > 1
            ? 'CONTESTED · PRESSURE DECAYS'
            : 'EMPTY · PRESSURE DECAYS';
    } else {
      const holders = tickData.bots.filter((bot) => {
        const state = tickData.state.find((candidate) => candidate.slot === bot.slot);
        return (
          bot.validatedAction === 'Wait' &&
          bot.result === 'Success' &&
          state?.status === 'Active' &&
          zone.onZone.has(`${state.x},${state.y}`)
        );
      });
      controlPhase =
        holders.length === 1
          ? `HOLDING · ${
              replay.header.participants[holders[0].slot]?.name ?? `slot ${holders[0].slot}`
            } GAINS`
          : holders.length > 1
            ? 'BOTH HOLD · PRESSURE FROZEN'
            : 'NO ACTIVE HOLD · PRESSURE DECAYS';
    }
  }

  return (
    <div className="flex flex-col gap-3">
      {controlLimit !== undefined && (
        <div className="rounded-lg border border-arena-edge bg-arena-panel/70 p-3">
          <div className="flex justify-between font-mono text-[11px] text-arena-dim">
            <span>{replay.header.participants[0]?.name ?? 'slot 0'}</span>
            <span>
              {controlOvertime ? 'OVERTIME ' : ''}CONTROL {controlPressure > 0 ? '+' : ''}
              {controlPressure} / ±{controlLimit}
            </span>
            <span>{replay.header.participants[1]?.name ?? 'slot 1'}</span>
          </div>
          <div className="relative mt-2 h-2 overflow-hidden rounded-full bg-arena-bg">
            <div className="absolute inset-y-0 left-1/2 w-px bg-arena-dim" />
            <div
              className="absolute top-0 h-full w-1 rounded bg-yellow-400 transition-[left]"
              style={{
                left: `${Math.max(
                  0,
                  Math.min(100, 50 + (50 * controlPressure) / Math.max(1, controlLimit)),
                )}%`,
              }}
            />
          </div>
          {controlPhase && (
            <p className="mt-2 text-center font-mono text-[10px] tracking-wide text-arena-dim">
              {controlPhase}
            </p>
          )}
        </div>
      )}
      {replay.header.participants.map((participant) => {
        const state = states.find((s) => s.slot === participant.slot)!;
        const botTick = tickData.bots.find((b) => b.slot === participant.slot);
        const selected = selectedSlot === participant.slot;
        const look = botLook(participant.lookId, participant.slot);
        const accent = adjustAccentForBackground(
          presentationAccent(look, participant.accent),
          '#111823',
        );
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
              <span className="flex size-10 shrink-0 items-center justify-center rounded-md bg-arena-bg/85">
                {look.image && (
                  <img
                    src={look.imageUrl}
                    alt={`${look.label} chassis`}
                    className="size-9 object-contain"
                  />
                )}
              </span>
              <span>
                <span className="block font-semibold">{participant.name}</span>
                <span className="block font-mono text-[10px] text-arena-dim">
                  {look.label} · slot {participant.slot} · {participant.runtimeKind}
                </span>
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
              <span aria-label={`Health ${state.health} of ${maxHealth}`}>
                {Array.from({ length: maxHealth }, (_, i) => (
                  <span
                    key={i}
                    style={{ color: i < state.health ? accent : undefined }}
                    className={i < state.health ? '' : 'text-arena-edge'}
                  >
                    ♥
                  </span>
                ))}
              </span>
              <span className="text-arena-dim">
                CD <span className="text-arena-text">{state.cooldown}</span>
              </span>
              {state.energy !== undefined && (
                <span className="text-arena-dim" aria-label={`Energy ${state.energy}`}>
                  ⚡ <span className="text-arena-text">{state.energy}</span>
                </span>
              )}
              {zone && (
                <span
                  className={clsx(
                    zone.onZone.has(`${state.x},${state.y}`) &&
                      state.status === 'Active' &&
                      (controlLimit === undefined ||
                        (botTick?.validatedAction === 'Wait' && botTick.result === 'Success'))
                      ? 'text-yellow-400'
                      : 'text-arena-dim',
                  )}
                  aria-label={controlLimit === undefined ? 'Zone ticks' : 'Active zone hold'}
                  title={
                    controlLimit === undefined
                      ? 'Zone ticks (gold = on the zone now)'
                      : 'Gold = successfully waiting on a zone tile this tick'
                  }
                >
                  ⬢{' '}
                  {zone.cumulative !== null && (
                    <span className="text-arena-text">
                      {zone.cumulative[Math.min(tick, replay.ticks.length - 1)][participant.slot] ?? 0}
                    </span>
                  )}
                  {zone.cumulative === null && (
                    <span className="text-arena-text">
                      {botTick?.validatedAction === 'Wait' &&
                      botTick.result === 'Success' &&
                      zone.onZone.has(`${state.x},${state.y}`)
                        ? 'HOLD'
                        : 'idle'}
                    </span>
                  )}
                </span>
              )}
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
