import clsx from 'clsx';
import { useMemo } from 'react';
import type {
  ReplayModel,
  ReplayStableUnitKey,
} from '../replayModel';
import { botLook } from '../render/arenaThemes';
import { createPresenter } from '../replayPresentation';
import {
  participantForUnit,
  visualIndexForUnit,
} from '../replayParticipants';

interface BotPanelProps {
  replay: ReplayModel;
  tick: number;
  selectedUnitKey: ReplayStableUnitKey | null;
  showVisibility: boolean;
  onSelectUnit: (unitKey: ReplayStableUnitKey | null) => void;
  onToggleVisibility: () => void;
}

/**
 * One plain sentence for what a bot is doing this tick.
 *
 * Action ids are engine vocabulary — `turn-left`, `shoot`, `wait` — and the panel used
 * to print them raw behind an arrow. They are generated from the id rather than mapped
 * by hand, so a new action reads sensibly the day it lands instead of falling through
 * to a code.
 */
function describeAction(
  actionId: string,
  heading: string | null,
  result: string | null,
): string {
  const words = actionId.replace(/[-_]/g, ' ');
  const phrase =
    words === 'wait'
      ? 'Holding position'
      : words.charAt(0).toUpperCase() + words.slice(1);
  const aimed = heading ? `${phrase} ${heading.toLowerCase()}` : phrase;
  return !result || result === 'success' ? aimed : `${aimed} — ${result}`;
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
          <div className="flex items-baseline justify-between gap-3">
            <span className="truncate text-[13px] text-arena-text">
              {objective.names[0]}
            </span>
            <span className="type-label shrink-0 text-[9px] text-arena-dim">
              {objective.overtime ? 'Overtime · ' : ''}Control{' '}
              <span className="tabular font-mono text-[11px] tracking-normal text-arena-text">
                {objective.pressure > 0 ? '+' : ''}
                {objective.pressure}/{objective.limit}
              </span>
            </span>
            <span className="truncate text-right text-[13px] text-arena-text">
              {objective.names[1]}
            </span>
          </div>
          <div className="relative mt-2 h-2 overflow-hidden rounded-full bg-arena-bg">
            <div className="absolute inset-y-0 left-1/2 w-px bg-arena-dim" />
            <div
              className="absolute top-0 h-full w-1 rounded bg-arena-text transition-[left]"
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
            <p className="type-label mt-2 text-center text-[9px] text-arena-dim">
              {objective.phase}
            </p>
          )}
        </div>
      )}

      {objective?.kind === 'frontline' && (
        <div className="rounded-lg border border-arena-edge bg-arena-panel/70 p-3">
          <div className="flex items-center justify-between">
            <span className="type-label text-[9px] text-arena-dim">Frontline</span>
            <span className="type-label text-[9px] text-arena-dim">
              Position{' '}
              <span className="tabular font-mono text-[11px] tracking-normal text-arena-text">
                {objective.activePositionIndex + 1}/{objective.positionCount}
              </span>
            </span>
          </div>
          <div className="mt-2 h-2 overflow-hidden rounded-full bg-arena-bg">
            <div
              className={clsx(
                'h-full transition-[width]',
                objective.claimingTeamId === null
                  ? 'bg-arena-edge2'
                  : 'bg-arena-dim',
              )}
              style={{
                width: `${
                  (100 * Math.abs(objective.captureProgress)) /
                  Math.max(1, objective.captureThreshold)
                }%`,
              }}
            />
          </div>
          <p className="type-label mt-2 text-center text-[9px] text-arena-dim">
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
        const participant = participantForUnit(replay, unit.unitKey);
        const look = botLook(
          participant?.lookId ?? undefined,
          visualIndexForUnit(replay, unit.unitKey),
        );
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
              {/* Eight statuses had eight hues, which is eight things competing with the
                  one colour that means something — the player's accent. Out of the game
                  a unit is out; everything else is a state it is passing through, and
                  the word already says which. */}
              <span
                className={clsx(
                  'type-label ml-auto text-[10px] whitespace-nowrap',
                  unit.status === 'destroyed' ||
                    unit.status === 'disqualified'
                    ? 'text-arena-hot'
                    : unit.status === 'active'
                      ? 'text-arena-text'
                      : 'text-arena-dim',
                )}
              >
                {unit.status}
                {transition ? ` · ${transition}` : ''}
              </span>
            </div>

            {/* Health, cooldown and what it is doing — three rows, one idea each.
                This was `♥♥♥ · CD 0 · ⬢ idle · → turn-left`: four encodings of state
                on one line, three of them abbreviations only the author knew. */}
            <dl className="mt-2.5 grid grid-cols-[4.5rem_1fr_auto] items-center gap-x-3 gap-y-2">
              <dt className="type-label text-[10px] text-arena-dim">Health</dt>
              <dd
                className="flex gap-[3px]"
                aria-label={`Health ${unit.health} of ${unit.maxHealth}`}
              >
                {Array.from({ length: unit.maxHealth }, (_, index) => (
                  <span
                    key={index}
                    className="h-2 flex-1 rounded-[2px] border"
                    style={
                      index < unit.health
                        ? { background: unit.accent, borderColor: unit.accent }
                        : { borderColor: 'var(--color-arena-edge)' }
                    }
                  />
                ))}
              </dd>
              <dd className="tabular font-mono text-[11px] text-arena-dim">
                {unit.health}/{unit.maxHealth}
              </dd>

              <dt className="type-label text-[10px] text-arena-dim">Weapon</dt>
              <dd className="col-span-2 text-[13px] text-arena-text">
                {unit.cooldown > 0 ? (
                  <>
                    Reloading —{' '}
                    <span className="tabular font-mono text-[12px] text-arena-dim">
                      {unit.cooldown}t
                    </span>
                  </>
                ) : (
                  'Ready'
                )}
                {unit.energy !== null && (
                  <span className="tabular font-mono text-[12px] text-arena-dim">
                    {' · '}
                    {unit.energy} energy
                  </span>
                )}
              </dd>

              {unit.actionId && (
                <>
                  <dt className="type-label text-[10px] text-arena-dim">
                    Doing
                  </dt>
                  <dd className="col-span-2 text-[13px] text-arena-text">
                    {describeAction(
                      unit.actionId,
                      unit.actionLaunchHeading,
                      unit.actionResult,
                    )}
                  </dd>
                </>
              )}

              {(objective !== null || unit.zoneTicks !== null) && (
                <>
                  <dt className="type-label text-[10px] text-arena-dim">
                    Objective
                  </dt>
                  <dd className="col-span-2 text-[13px] text-arena-text">
                    {unit.zoneTicks !== null ? (
                      <>
                        <span className="tabular font-mono text-[12px]">
                          {unit.zoneTicks}
                        </span>{' '}
                        <span className="text-arena-dim">
                          tick{unit.zoneTicks === 1 ? '' : 's'} held
                        </span>
                      </>
                    ) : unit.holdingObjective ? (
                      'Holding'
                    ) : (
                      <span className="text-arena-dim">Not on it</span>
                    )}
                  </dd>
                </>
              )}
            </dl>

            {formTransition && (
              <p className="mt-2 font-mono text-[10px] tracking-wide text-violet-300">
                ANCHORING · {formTransition.fromFormId.toUpperCase()} →{' '}
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
