import clsx from 'clsx';
import { useMemo } from 'react';
import type {
  ReplayModel,
  ReplayStableUnitKey,
} from '../replayModel';
import { unitLook } from '../render/unitPresentation';
import { createPresenter } from '../replayPresentation';

interface BotPanelProps {
  replay: ReplayModel;
  tick: number;
  selectedUnitKey: ReplayStableUnitKey | null;
  showVisibility: boolean;
  onSelectUnit: (unitKey: ReplayStableUnitKey | null) => void;
  onToggleVisibility: () => void;
}

export default function BotPanel({
  replay,
  tick,
  selectedUnitKey,
  showVisibility,
  onSelectUnit,
  onToggleVisibility,
}: BotPanelProps) {
  const presenter = useMemo(() => createPresenter(replay), [replay]);
  const { objective, units } = presenter.at(tick);

  return (
    <div className="flex flex-col gap-3">
      {objective?.kind === 'legacy-control' && (
        <div className="rounded-lg border border-arena-edge bg-arena-panel/70 p-3">
          <div className="flex justify-between font-mono text-[11px] text-arena-dim">
            <span>{objective.names[0]}</span>
            <span>
              {objective.overtime ? 'OVERTIME ' : ''}CONTROL{' '}
              {objective.pressure > 0 ? '+' : ''}
              {objective.pressure} / ±{objective.limit}
            </span>
            <span>{objective.names[1]}</span>
          </div>
          <div className="relative mt-2 h-2 overflow-hidden rounded-full bg-arena-bg">
            <div className="absolute inset-y-0 left-1/2 w-px bg-arena-dim" />
            <div
              className="absolute top-0 h-full w-1 rounded bg-yellow-400 transition-[left]"
              style={{
                left: `${Math.max(
                  0,
                  Math.min(
                    100,
                    50 +
                      (50 * objective.pressure) /
                        Math.max(1, objective.limit),
                  ),
                )}%`,
              }}
            />
          </div>
          {objective.phase && (
            <p className="mt-2 text-center font-mono text-[10px] tracking-wide text-arena-dim">
              {objective.phase}
            </p>
          )}
        </div>
      )}

      {objective?.kind === 'frontline' && (
        <div className="rounded-lg border border-arena-edge bg-arena-panel/70 p-3">
          <div className="flex items-center justify-between font-mono text-[11px] text-arena-dim">
            <span>FRONTLINE</span>
            <span>
              POSITION {objective.activePositionIndex + 1}/
              {objective.positionCount}
            </span>
          </div>
          <div className="mt-2 h-2 overflow-hidden rounded-full bg-arena-bg">
            <div
              className={clsx(
                'h-full transition-[width]',
                objective.claimingTeamId === null
                  ? 'bg-arena-dim'
                  : 'bg-yellow-400',
              )}
              style={{
                width: `${
                  (100 * Math.abs(objective.captureProgress)) /
                  Math.max(1, objective.captureThreshold)
                }%`,
              }}
            />
          </div>
          <p className="mt-2 text-center font-mono text-[10px] tracking-wide text-arena-dim">
            {objective.phase}
          </p>
        </div>
      )}

      {units.map((unit) => {
        const selected = selectedUnitKey === unit.unitKey;
        const transition =
          unit.status === 'respawning' && unit.respawnAtTick !== null
            ? `RESPAWN T${unit.respawnAtTick}`
            : unit.status === 'locked' && unit.unlockAtTick !== null
              ? `UNLOCK T${unit.unlockAtTick}`
              : unit.status === 'rebuilding' &&
                  unit.rebuildReadyAtTick !== null
                ? `READY T${unit.rebuildReadyAtTick}`
                : unit.status === 'fabrication-queued' &&
                    unit.fabricationAtTick !== null
                  ? `SPAWN T${unit.fabricationAtTick}`
                  : null;
        const formTransition = unit.pendingFormTransition;
        // The card wears the effective form's chassis, so anchoring and mobilizing show
        // up in the panel at the same tick they show up in the arena.
        const look = unitLook(replay, unit.unitKey, unit.formId);
        return (
          <button
            key={unit.unitKey}
            onClick={() =>
              onSelectUnit(selected ? null : unit.unitKey)
            }
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
                    alt={`${unit.lookLabel} chassis`}
                    className="size-9 object-contain"
                  />
                )}
              </span>
              <span>
                <span className="block font-semibold">{unit.name}</span>
                <span className="block font-mono text-[10px] text-arena-dim">
                  {unit.lookLabel} ·{' '}
                  {unit.legacySlot === null
                    ? `team ${unit.teamId} · unit ${unit.unitId}${unit.lifeId === null ? '' : ` · life ${unit.lifeId}`}`
                    : `slot ${unit.legacySlot}`}{' '}
                  · {unit.runtimeKind}
                </span>
              </span>
              <span
                className={clsx('ml-auto font-mono text-[11px]', {
                  'text-emerald-400': unit.status === 'active',
                  'text-red-400': unit.status === 'destroyed',
                  'text-amber-400': unit.status === 'disqualified',
                  'text-cyan-300': unit.status === 'respawning',
                  'text-arena-dim': unit.status === 'locked',
                  'text-emerald-300': unit.status === 'ready',
                  'text-violet-300':
                    unit.status === 'fabrication-queued',
                  'text-amber-300': unit.status === 'rebuilding',
                })}
              >
                {unit.status.toUpperCase()}
                {transition ? ` · ${transition}` : ''}
              </span>
            </div>

            <div className="mt-2 flex flex-wrap items-center gap-3 font-mono text-xs">
              <span aria-label={`Health ${unit.health} of ${unit.maxHealth}`}>
                {Array.from({ length: unit.maxHealth }, (_, index) => (
                  <span
                    key={index}
                    style={{
                      color:
                        index < unit.health ? unit.accent : undefined,
                    }}
                    className={
                      index < unit.health ? '' : 'text-arena-edge'
                    }
                  >
                    ♥
                  </span>
                ))}
              </span>
              <span className="text-arena-dim">
                CD <span className="text-arena-text">{unit.cooldown}</span>
              </span>
              {unit.energy !== null && (
                <span
                  className="text-arena-dim"
                  aria-label={`Energy ${unit.energy}`}
                >
                  ⚡ <span className="text-arena-text">{unit.energy}</span>
                </span>
              )}
              {objective === null && unit.zoneTicks === null ? null : (
                <span
                  className={clsx(
                    unit.holdingObjective
                      ? 'text-yellow-400'
                      : 'text-arena-dim',
                  )}
                  aria-label="Objective presence"
                >
                  ⬢{' '}
                  <span className="text-arena-text">
                    {unit.zoneTicks ??
                      (unit.holdingObjective ? 'HOLD' : 'idle')}
                  </span>
                </span>
              )}
              {unit.actionId && (
                <span className="text-arena-dim">
                  → <span className="text-arena-text">{unit.actionId}</span>
                  {unit.actionLaunchHeading && (
                    <span className="text-cyan-200">
                      {' '}
                      · {unit.actionLaunchHeading.toUpperCase()}
                    </span>
                  )}
                  {unit.actionResult !== 'success' && (
                    <span className="text-amber-400">
                      {' '}
                      ({unit.actionResult})
                    </span>
                  )}
                </span>
              )}
            </div>

            {formTransition && (
              <p className="mt-2 font-mono text-[10px] tracking-wide text-violet-300">
                TRANSFORMING · {formTransition.fromFormId.toUpperCase()} →{' '}
                {formTransition.toFormId.toUpperCase()} · COMPLETES T
                {formTransition.completesAtTick}
              </p>
            )}

            {!unit.canMove && (
              <p className="mt-2 font-mono text-[10px] tracking-wide text-cyan-300">
                STATIONARY ·{' '}
                {unit.omnidirectionalVision ? '360° VISION' : 'DIRECTED VISION'}
                {' · '}
                {unit.omnidirectionalShooting
                  ? '360° FIRE'
                  : 'DIRECTED FIRE'}
              </p>
            )}

            {selected && unit.debug && (
              <pre className="mt-2 overflow-x-auto rounded bg-arena-bg/80 p-2 font-mono text-[11px] whitespace-pre-wrap text-arena-dim">
                {unit.debug}
              </pre>
            )}
            {selected && unit.actionId && (
              <div className="mt-2 font-mono text-[11px] text-arena-dim">
                sees {unit.visibleTiles} tiles ·{' '}
                {unit.visibleEnemies.length > 0
                  ? `enemy at ${unit.visibleEnemies.map((enemy) => `(${enemy.x},${enemy.y})`).join(' ')}`
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
        Show selected unit&apos;s field of view
      </label>
    </div>
  );
}
