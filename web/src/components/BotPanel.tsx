import clsx from 'clsx';
import IdentityChip from './IdentityChip';
import {
  participantForUnit,
  visualIndexForUnit,
} from '../replayParticipants';
import { useMemo } from 'react';
import type {
  ReplayModel,
  ReplayStableUnitKey,
} from '../replayModel';
import { createPresenter } from '../replayPresentation';

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
 *
 * How it went is not part of the sentence: it is a standing fact about the action, so it
 * stands beside it as a pill rather than turning one clause into two.
 */
function describeAction(
  actionId: string,
  heading: string | null,
): string {
  const words = actionId.replace(/[-_]/g, ' ');
  const phrase =
    words === 'wait'
      ? 'Holding position'
      : words.charAt(0).toUpperCase() + words.slice(1);
  return heading ? `${phrase} ${heading.toLowerCase()}` : phrase;
}

/**
 * How long a full reload is, so the cooldown bar has a denominator.
 *
 * `form.shootCooldownTicks` is the declared value and replay v2/v3 carry it — replay v1,
 * which is every shipped duel, leaves it null. The longest cooldown a unit is ever seen
 * holding is the same number observed rather than declared, so the bar drains truthfully
 * in both. Computed once per replay, not per tick.
 */
function useCooldownScale(
  replay: ReplayModel,
): Map<ReplayStableUnitKey, number> {
  return useMemo(() => {
    const observed = new Map<ReplayStableUnitKey, number>();
    for (const replayTick of replay.ticks) {
      for (const actor of replayTick.after.actors) {
        observed.set(
          actor.unitKey,
          Math.max(observed.get(actor.unitKey) ?? 0, actor.cooldown),
        );
      }
    }
    return observed;
  }, [replay]);
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
  const cooldownScale = useCooldownScale(replay);

  return (
    <div className="flex flex-col gap-2.5">
      {objective?.kind === 'legacy-control' && (
        <div className="panel-quiet pad">
          <div className="flex items-baseline justify-between gap-3">
            <span className="t-body truncate text-arena-text">
              {objective.names[0]}
            </span>
            <span className="lab shrink-0">
              {objective.overtime ? 'Overtime · ' : ''}Control{' '}
              <span className="val tracking-normal text-arena-text">
                {objective.pressure > 0 ? '+' : ''}
                {objective.pressure}/{objective.limit}
              </span>
            </span>
            <span className="t-body truncate text-right text-arena-text">
              {objective.names[1]}
            </span>
          </div>
          <div className="relative mt-2 h-[5px] overflow-hidden rounded-[3px] bg-arena-edge">
            <div className="absolute inset-y-0 left-1/2 w-px bg-arena-dim" />
            <div
              className="absolute top-0 h-full w-1 rounded-[2px] bg-arena-text transition-[left]"
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
            <p className="lab mt-2 text-center">{objective.phase}</p>
          )}
        </div>
      )}

      {objective?.kind === 'frontline' && (
        <div className="panel-quiet pad">
          <div className="flex items-center justify-between">
            <span className="lab">Frontline</span>
            <span className="lab">
              Position{' '}
              <span className="val tracking-normal text-arena-text">
                {objective.activePositionIndex + 1}/{objective.positionCount}
              </span>
            </span>
          </div>
          <div className="mt-2 h-[5px] overflow-hidden rounded-[3px] bg-arena-edge">
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
          <p className="lab mt-2 text-center">{objective.phase}</p>
        </div>
      )}

      {units.map((unit) => {
        const selected = selectedUnitKey === unit.unitKey;
        // Out of the game, as the design's dead card reads it: the whole card recedes
        // rather than one word turning a colour.
        const out =
          unit.status === 'destroyed' || unit.status === 'disqualified';
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
        const declaredCooldown =
          replay.forms.find((form) => form.formId === unit.formId)
            ?.shootCooldownTicks ?? 0;
        const cooldownMax = Math.max(
          1,
          declaredCooldown,
          cooldownScale.get(unit.unitKey) ?? 0,
        );
        const actionResult =
          unit.actionResult &&
          unit.actionResult !== 'success' &&
          unit.actionResult !== 'none'
            ? unit.actionResult
            : null;
        return (
          <button
            key={unit.unitKey}
            onClick={() =>
              onSelectUnit(selected ? null : unit.unitKey)
            }
            aria-pressed={selected}
            className={clsx(
              'panel pad text-left transition-colors',
              out && 'opacity-[0.62]',
              // Selection is a state, and state is never the accent here — the accent is
              // the shop's own colour. Raised ground and a brighter edge say it instead.
              selected
                ? 'border-arena-edge2 bg-arena-raise'
                : 'hover:border-arena-edge2',
            )}
          >
            <div className="flex items-center gap-2.5">
              <IdentityChip
                lookId={participantForUnit(replay, unit.unitKey)?.lookId}
                visualIndex={visualIndexForUnit(replay, unit.unitKey)}
                accent={unit.accent}
                name={unit.name}
                nameClassName="text-[14px]"
                sub={`${unit.lookLabel} · ${
                  unit.legacySlot === null
                    ? `team ${unit.teamId} · unit ${unit.unitId}${unit.lifeId === null ? '' : ` · life ${unit.lifeId}`}`
                    : `slot ${unit.legacySlot}`
                }`}
              />
              {/* Eight statuses had eight hues, which is eight things competing with the
                  one colour that means something — the player's accent. Out of the game
                  a unit is out; everything else is a state it is passing through, and
                  the word already says which. */}
              <span
                className={clsx(
                  'pill ml-auto',
                  out
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
            <dl className="mt-[9px] grid grid-cols-[64px_1fr_auto] items-center gap-x-[10px] gap-y-[9px]">
              <dt className="lab">Health</dt>
              <dd
                className="flex gap-[3px]"
                aria-label={`Health ${unit.health} of ${unit.maxHealth}`}
              >
                {Array.from({ length: unit.maxHealth }, (_, index) => (
                  <span
                    key={index}
                    className={clsx(
                      'h-[9px] flex-1 rounded-[2px] border',
                      index >= unit.health && 'border-arena-edge',
                      index >= unit.health && out && 'border-dashed',
                    )}
                    // Again: the colour is the only thing here that is data.
                    style={
                      index < unit.health
                        ? { background: unit.accent, borderColor: unit.accent }
                        : undefined
                    }
                  />
                ))}
              </dd>
              <dd className="val">
                {unit.health}/{unit.maxHealth}
              </dd>

              {/* A draining bar, not a sentence: how much of the reload is left is a
                  quantity, and a quantity is faster to see than to read. */}
              <dt className="lab">Cooldown</dt>
              <dd>
                <span
                  className="block h-[5px] overflow-hidden rounded-[3px] bg-arena-edge"
                  aria-hidden
                >
                  <span
                    className="block h-full bg-arena-dim transition-[width]"
                    style={{
                      width: `${Math.min(100, (100 * unit.cooldown) / cooldownMax)}%`,
                    }}
                  />
                </span>
              </dd>
              <dd className="val">{unit.cooldown}t</dd>

              {unit.energy !== null && (
                <>
                  <dt className="lab">Energy</dt>
                  <dd className="val col-span-2">{unit.energy}</dd>
                </>
              )}

              {unit.actionId && (
                <>
                  <dt className="lab">Doing</dt>
                  <dd
                    className={clsx(
                      't-body text-arena-text',
                      actionResult === null && 'col-span-2',
                    )}
                  >
                    {describeAction(
                      unit.actionId,
                      unit.actionLaunchHeading,
                    )}
                  </dd>
                  {actionResult !== null && (
                    <dd>
                      <span className="pill">{actionResult}</span>
                    </dd>
                  )}
                </>
              )}

              {(objective !== null || unit.zoneTicks !== null) && (
                <>
                  <dt className="lab">Objective</dt>
                  <dd className="t-body col-span-2 text-arena-text">
                    {unit.zoneTicks !== null ? (
                      <>
                        <span className="val text-arena-text">
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
              <p className="lab mt-2">
                ANCHORING · {formTransition.fromFormId.toUpperCase()} →{' '}
                {formTransition.toFormId.toUpperCase()} · COMPLETES T
                {formTransition.completesAtTick}
              </p>
            )}

            {!unit.canMove && (
              <p className="lab mt-2">
                STATIONARY ·{' '}
                {unit.omnidirectionalVision ? '360° VISION' : 'DIRECTED VISION'}
                {' · '}
                {unit.omnidirectionalShooting
                  ? '360° FIRE'
                  : 'DIRECTED FIRE'}
              </p>
            )}

            {selected && unit.debug && (
              <pre className="term mt-2 whitespace-pre-wrap text-arena-dim">
                {unit.debug}
              </pre>
            )}
            {selected && unit.actionId && (
              <p className="val mt-2">
                sees {unit.visibleTiles} tiles ·{' '}
                {unit.visibleEnemies.length > 0
                  ? `enemy at ${unit.visibleEnemies.map((enemy) => `(${enemy.x},${enemy.y})`).join(' ')}`
                  : 'no enemies visible'}
              </p>
            )}
          </button>
        );
      })}

      <label className="t-meta flex cursor-pointer items-center gap-2 px-1 select-none">
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
