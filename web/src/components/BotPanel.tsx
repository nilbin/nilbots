import clsx from 'clsx';
import IdentityChip from './IdentityChip';
import ClassIcon from './ClassIcon';
import { visualIndexForUnit } from '../replayParticipants';
import { useMemo } from 'react';
import type {
  ReplayModel,
  ReplayStableUnitKey,
} from '../replayModel';
import { unitLook } from '../render/unitPresentation';
import {
  createPresenter,
  type FrontlineControlPresentation,
} from '../replayPresentation';
import { playerAccent } from '../presentation/playerAccent';
import { SCRAP_ACCENT } from '../presentation/scrapAccent';
import { styleVariables } from '../presentation/styleVariables';
import { roleTagColor } from '../presentation/roleTag';
import { playRoleSummary } from '../presentation/playAwareness';

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

/**
 * The capture meter, as the rules actually work it.
 *
 * Under the channel this is a segmented bar — one segment per point of the
 * declared threshold, because the threshold is 8 and a count that small is
 * faster to read as pips than as a percentage — filled in the claiming team's
 * own colour. A revert leaves the work it removed standing in place as a hot
 * ghost for its beat, which is the difference between "we lost four" and a bar
 * that is simply shorter than it was a moment ago.
 *
 * Off the channel it stays the plain proportional meter it has always been.
 */
function ChannelMeter({
  objective,
  claimAccent,
}: {
  objective: FrontlineControlPresentation;
  claimAccent: string | null;
}) {
  const revert = objective.captureRevert;
  const threshold = Math.max(1, objective.captureThreshold);
  const held = Math.max(0, Math.min(threshold, objective.captureProgress));
  const ghost =
    revert === null
      ? held
      : Math.min(threshold, Math.round(revert.fromFraction * threshold));
  const accent = claimAccent ?? (revert ? '#f4c477' : null);

  if (!objective.channel || threshold > 16) {
    return (
      <div className="mt-2 h-[5px] overflow-hidden rounded-[3px] bg-arena-edge">
        <div
          className={clsx(
            'runtime-progress h-full transition-[width]',
            objective.claimingTeamId === null
              ? 'bg-arena-edge2'
              : 'bg-arena-dim',
          )}
          style={styleVariables({
            '--runtime-progress': `${(100 * held) / threshold}%`,
          })}
        />
      </div>
    );
  }

  return (
    <div
      className="mt-2 flex gap-[3px]"
      aria-label={`Capture ${held} of ${threshold}`}
      style={
        accent
          ? styleVariables({ '--player-accent': playerAccent(accent, 'panel') })
          : undefined
      }
    >
      {Array.from({ length: threshold }, (_, index) => {
        const filled = index < held;
        const lost = !filled && index < ghost && revert !== null;
        return (
          <span
            key={index}
            className={clsx(
              'h-[7px] flex-1 rounded-[2px] border',
              filled && accent && 'player-accent-border player-accent-fill',
              filled && !accent && 'border-arena-dim bg-arena-dim',
              lost &&
                (revert.kind === 'interrupt'
                  ? 'border-arena-hot bg-arena-hot/40'
                  : 'border-arena-dim border-dashed'),
              !filled && !lost && 'border-arena-edge',
            )}
          />
        );
      })}
    </div>
  );
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
  const { objective, units, economy } = presenter.at(tick);
  const cooldownScale = useCooldownScale(replay);
  const claimAccent =
    objective?.kind === 'frontline'
      ? (units.find(
          (unit) =>
            unit.teamId ===
            (objective.claimingTeamId ?? objective.captureRevert?.teamId),
        )?.accent ?? null)
      : null;

  // One mind drives every body a participant owns, so the cards below are its
  // army rather than N independent programs. Saying so once, quietly, is the
  // whole indicator: a spectator needs to know the role labels come from one
  // author's plan, not from nine bodies agreeing.
  const mindProfile =
    replay.versions.actorRuntime?.family === 'generic-mind-match-1';
  // Arc Relay has sixteen bodies. Pair equal slots across teams so the roster
  // reads like a lineup, rather than a long diagnostic log for one team and
  // then another. Full detail remains one click away.
  const presentedUnits = mindProfile
    ? [...units].sort(
        (left, right) =>
          left.unitId - right.unitId || left.teamId - right.teamId,
      )
    : units;

  return (
    <div className="flex min-w-0 flex-col gap-2.5">
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
              className="runtime-position absolute top-0 h-full w-1 rounded-[2px] bg-arena-text transition-[left]"
              style={styleVariables({
                '--runtime-position': `${Math.max(
                  0,
                  Math.min(
                    100,
                    50 +
                      (50 * objective.pressure) /
                        Math.max(1, objective.limit),
                  ),
                )}%`,
              })}
            />
          </div>
          {objective.phase && (
            <p className="lab mt-2 text-center">{objective.phase}</p>
          )}
        </div>
      )}

      {objective?.kind === 'frontline' && (
        <div className="panel-quiet pad">
          <div className="flex items-baseline justify-between gap-3">
            <span className="lab shrink-0">
              {objective.channel ? 'Channel' : 'Frontline'}
            </span>
            <span className="lab text-right">
              Position{' '}
              <span className="val tracking-normal text-arena-text">
                {objective.activePositionIndex + 1}/{objective.positionCount}
              </span>
            </span>
          </div>
          {/* A channel is a bar filling toward a threshold that a hit can take
              back, so it is drawn as one — in the claimant's own colour, with
              the work a revert removed left standing beside it. Off the
              channel this is the meter it has always been. */}
          <ChannelMeter objective={objective} claimAccent={claimAccent} />
          <p
            className={clsx(
              'lab mt-2 text-center',
              objective.captureRevert?.ticksSince === 0 &&
                'text-arena-hot',
            )}
          >
            {objective.phase}
          </p>
          {/* The escort formation, counted. Two bodies standing still on the
              point take it twice as fast; one standing still with a screen on
              the firing line takes it at all. */}
          {objective.channelingUnitCount > 0 && (
            <p className="lab mt-1 text-center">
              <span className="val tracking-normal text-arena-text">
                {objective.channelingUnitCount}
              </span>
              {' holding'}
              {objective.screeningUnitCount > 0 && (
                <>
                  {' · '}
                  <span className="val tracking-normal text-arena-text">
                    {objective.screeningUnitCount}
                  </span>
                  {' screening'}
                </>
              )}
            </p>
          )}
        </div>
      )}

      {economy && (
        <div className="panel-quiet pad">
          <div className="flex items-center justify-between">
            <span className="lab">Scrap</span>
            <span className="lab">
              {economy.nextVeinTick === null
                ? 'DEPOSITS SPENT'
                : economy.veinDueNow
                  ? 'DEPOSIT NOW'
                  : `NEXT T${economy.nextVeinTick}`}
            </span>
          </div>
          <dl className="mt-2 flex flex-col gap-2">
            {economy.teams.map((team) => {
              // The purchase beat, panel-side: the row the buy happened on
              // lights its own accent edge and names the tier. It fades over
              // four ticks, which is about a second of playback — a flash
              // rather than a state.
              const bought = economy.purchases.find(
                (purchase) => purchase.teamId === team.teamId,
              );
              return (
                <div
                  key={team.teamId}
                  className={clsx(
                    'rounded-[3px] border px-2 py-1.5 transition-colors',
                    bought
                      ? 'player-accent-border bg-arena-raise'
                      : 'border-arena-edge',
                  )}
                  style={styleVariables({
                    '--player-accent': playerAccent(team.accent, 'panel'),
                  })}
                >
                  <div className="flex items-baseline justify-between gap-2">
                    <span className="t-body min-w-0 truncate text-arena-text">
                      {team.name}
                    </span>
                    <span className="val shrink-0 text-arena-text">
                      {team.bank}
                      {team.carried > 0 && (
                        <span className="text-arena-dim">
                          {' '}
                          +{team.carried} carried
                        </span>
                      )}
                    </span>
                  </div>
                  <div className="mt-1.5 flex flex-wrap items-center gap-x-3 gap-y-1">
                    {team.tracks.map((track) => (
                      <span
                        key={track.trackId}
                        className="flex items-center gap-1"
                        aria-label={`${track.trackId} tier ${track.tier} of ${track.maxTier}`}
                      >
                        <span
                          className={clsx(
                            'lab',
                            track.boughtTicksSince === null
                              ? track.tier > 0
                                ? 'text-arena-text'
                                : undefined
                              : 'player-accent-text',
                          )}
                        >
                          {track.label}
                        </span>
                        {Array.from(
                          { length: track.maxTier },
                          (_, index) => (
                            <span
                              key={index}
                              className={clsx(
                                'h-[7px] w-[7px] rounded-[2px] border',
                                index < track.tier
                                  ? 'player-accent-border player-accent-fill'
                                  : track.affordable &&
                                      index === track.tier
                                    ? 'border-arena-dim'
                                    : 'border-arena-edge',
                              )}
                            />
                          ),
                        )}
                      </span>
                    ))}
                    <span className="lab ml-auto">
                      {team.tierTotal}/{team.maxTotalTiers}
                    </span>
                  </div>
                  {bought && (
                    <p className="lab player-accent-text mt-1.5">
                      BOUGHT {bought.label.toUpperCase()}{' '}
                      {'I'.repeat(bought.tier)}
                    </p>
                  )}
                </div>
              );
            })}
          </dl>
        </div>
      )}

      <div className={clsx(mindProfile && 'grid grid-cols-2 gap-1.5')}>
      {presentedUnits.map((unit) => {
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
        const visibleAccent = playerAccent(unit.accent, 'panel');
        // The card wears the effective form's chassis, so anchoring, mobilizing and
        // entering a stance show up in the panel at the same tick they show up in the
        // arena.
        const look = unitLook(replay, unit.unitKey, unit.formId);
        // The live tag is the unit's own answer to "what am I doing" —
        // race-north, guard-home, ghost-stalk. Suppressing it on Arc Relay
        // made authored guards unreadable from bugs (owner review): always
        // fall back to the raw tag.
        const displayedRole = playRoleSummary(unit.roleTag) ?? unit.roleTag;
        const controllerName = replay.participants.find(
          (participant) => participant.participantId === unit.participantId,
        )?.name;
        return (
          <article
            key={unit.unitKey}
            className={clsx(
              'panel min-w-0 transition-colors',
              mindProfile
                ? selected
                  ? 'pad col-span-2'
                  : 'px-2 py-1.5'
                : 'pad',
              out && 'opacity-[0.62]',
              // Selection is a state, and state is never the accent here — the accent is
              // the shop's own colour. Raised ground and a brighter edge say it instead.
              selected
                ? 'border-arena-edge2 bg-arena-raise'
                : 'hover:border-arena-edge2',
            )}
          >
            <div className="flex min-w-0 items-center gap-2">
              {look.classId ? <span className="flex min-w-0 items-center gap-2">
                <ClassIcon classId={look.classId} label={`${classLabel(look.classId)} class`}
                  accent={unit.accent} size={mindProfile && !selected ? 26 : 30} />
                <span className="min-w-0">
                  <span className={clsx(
                    'block truncate font-semibold text-arena-text',
                    mindProfile && !selected ? 'text-[12px]' : 'text-[14px]',
                  )}>
                    {classLabel(look.classId)}
                  </span>
                  {(!mindProfile || (selected && controllerName)) && (
                    <span className="block truncate text-[9px] uppercase tracking-[.1em] text-arena-dim">
                      {selected && controllerName
                        ? controllerName
                        : `team ${unit.teamId}`}
                    </span>
                  )}
                </span>
              </span> : <IdentityChip
                  lookId={look.id}
                  visualIndex={visualIndexForUnit(replay, unit.unitKey)}
                  accent={unit.accent}
                  name={unit.name}
                  nameClassName="text-[14px]"
                  sub={unit.legacySlot === null
                    ? `team ${unit.teamId} · body ${unit.unitId + 1}`
                    : `slot ${unit.legacySlot}`}
                />}
              {/* Eight statuses had eight hues, which is eight things competing with the
                  one colour that means something — the player's accent. Out of the game
                  a unit is out; everything else is a state it is passing through, and
                  the word already says which. */}
              <span className="ml-auto flex shrink-0 items-center gap-1.5">
                {(!mindProfile || unit.status !== 'active') && <span
                  className={clsx(
                    'pill',
                    out
                      ? 'text-arena-hot'
                      : unit.status === 'active'
                        ? 'text-arena-text'
                        : 'text-arena-dim',
                  )}
                >
                  {unit.status}
                  {transition ? ` · ${transition}` : ''}
                </span>}
                <button
                  type="button"
                  onClick={() =>
                    onSelectUnit(selected ? null : unit.unitKey)
                  }
                  aria-pressed={selected}
                  aria-label={`${selected ? 'Close' : 'Inspect'} ${look.classId ? classLabel(look.classId) : unit.name}${controllerName ? ` for ${controllerName}` : ''}`}
                  title={selected ? 'Close details' : 'Inspect body'}
                  className={clsx(
                    'btn',
                    mindProfile && !selected && 'px-1.5 py-0.5',
                    selected && 'btn-on',
                  )}
                >
                  {selected ? 'Close' : mindProfile ? '···' : 'Inspect'}
                </button>
              </span>
            </div>

            {mindProfile && !selected && (
              <div className="mt-1.5 flex min-w-0 items-center gap-1.5">
                <span
                  className="flex shrink-0 gap-[2px]"
                  aria-label={`Health ${unit.health} of ${unit.maxHealth}`}
                >
                  {Array.from({ length: unit.maxHealth }, (_, index) => (
                    <i
                      key={index}
                      className={clsx(
                        'h-[4px] w-[9px] rounded-[1px] border',
                        index < unit.health
                          ? 'player-accent-border player-accent-fill'
                          : 'border-arena-edge',
                      )}
                      style={
                        index < unit.health
                          ? styleVariables({ '--player-accent': visibleAccent })
                          : undefined
                      }
                    />
                  ))}
                </span>
                <span className="min-w-0 truncate text-[9px] text-arena-dim">
                  {displayedRole ??
                    (unit.actionId
                      ? describeAction(unit.actionId, unit.actionLaunchHeading)
                      : unit.status)}
                </span>
              </div>
            )}

            {/* Health, cooldown and what it is doing — three rows, one idea each.
                This was `♥♥♥ · CD 0 · ⬢ idle · → turn-left`: four encodings of state
                on one line, three of them abbreviations only the author knew. */}
            {(!mindProfile || selected) && <dl className="mt-[9px] grid grid-cols-[64px_1fr_auto] items-center gap-x-[10px] gap-y-[9px]">
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
                      index < unit.health &&
                        'player-accent-border player-accent-fill',
                    )}
                    style={
                      index < unit.health
                        ? styleVariables({
                            '--player-accent': visibleAccent,
                          })
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
                    className="runtime-progress block h-full bg-arena-dim transition-[width]"
                    style={styleVariables({
                      '--runtime-progress': `${Math.min(
                        100,
                        (100 * unit.cooldown) / cooldownMax,
                      )}%`,
                    })}
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

              {/* The mind's OWN word for this body's job, beside the derived
                  "Doing" and "Objective" lines. Rendered only when a mind set
                  one, because an unlabelled body should look unlabelled. */}
              {/* A trap under the mind is a single dramatic frame: the whole
                  instance and its match-long memory go, and under this
                  contract's zero allowance so does the match. Say that, rather
                  than leaving a lone "faulted" outcome word. */}
              {unit.runtimeFault !== null && (
                <>
                  <dt className="lab">Fault</dt>
                  <dd className="t-body col-span-2 text-(--color-arena-loss)">
                    {mindProfile
                      ? 'this mind trapped — it forgot the match'
                      : 'runtime fault'}
                    {unit.runtimeFault.disqualificationTriggered
                      ? ' · disqualified'
                      : ''}
                    <span className="val"> · {unit.runtimeFault.faultCode}</span>
                  </dd>
                </>
              )}

              {displayedRole !== null && (
                <>
                  <dt className="lab">Role</dt>
                  <dd className="t-body col-span-2">
                    <span
                      className="pill"
                      style={{
                        // Coloured by a stable hash of the tag, so `channeler`
                        // is the same colour all match and across matches.
                        color: roleTagColor(displayedRole),
                        borderColor: roleTagColor(displayedRole),
                      }}
                    >
                      {displayedRole}
                    </span>
                  </dd>
                </>
              )}

              {(objective !== null || unit.zoneTicks !== null) && (
                <>
                  <dt className="lab">Objective</dt>
                  <dd className="t-body col-span-2 text-arena-text">
                    {/* What this body is doing *now* outranks what it has
                        accumulated: under the channel, standing still on the
                        point is the action, and a running tally of ticks held
                        is the footnote. */}
                    {unit.channelRole === 'channeling' ? (
                      <>
                        Channeling
                        {unit.zoneTicks !== null && (
                          <span className="text-arena-dim">
                            {' '}
                            · {unit.zoneTicks} held
                          </span>
                        )}
                      </>
                    ) : unit.channelRole === 'screening' ? (
                      <>
                        Screening
                        {unit.zoneTicks !== null && (
                          <span className="text-arena-dim">
                            {' '}
                            · {unit.zoneTicks} held
                          </span>
                        )}
                      </>
                    ) : unit.zoneTicks !== null ? (
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

              {/* Only on a loaded body. A courier is the most valuable target
                  on the map for as long as it is carrying, and this is the
                  card saying so. */}
              {economy !== null && unit.carriedScrap > 0 && (
                <>
                  <dt className="lab">Carrying</dt>
                  <dd
                    className="flex gap-[3px]"
                    aria-label={`Carrying ${unit.carriedScrap} of ${economy.carryCapacity} scrap`}
                    style={styleVariables({
                      '--player-accent': SCRAP_ACCENT,
                    })}
                  >
                    {Array.from(
                      { length: economy.carryCapacity },
                      (_, index) => (
                        <span
                          key={index}
                          className={clsx(
                            'h-[7px] flex-1 rounded-[2px] border',
                            index < unit.carriedScrap
                              ? 'player-accent-border player-accent-fill'
                              : 'border-arena-edge',
                          )}
                        />
                      ),
                    )}
                  </dd>
                  <dd className="val">
                    {unit.carriedScrap}/{economy.carryCapacity}
                  </dd>
                </>
              )}
            </dl>}

            {formTransition && (!mindProfile || selected) && (
              <p className="lab mt-2">
                Transforming · {formTransition.fromFormId} →{' '}
                {formTransition.toFormId} · completes T
                {formTransition.completesAtTick}
              </p>
            )}

            {!unit.canMove && (!mindProfile || selected) && (
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
          </article>
        );
      })}
      </div>

      <label className="t-meta flex cursor-pointer items-center gap-2 px-1 select-none">
        <input
          type="checkbox"
          checked={showVisibility}
          onChange={onToggleVisibility}
          className="accent-(--color-arena-text)"
        />
        {mindProfile
          ? "Show this mind's team vision"
          : "Show selected team's field of view"}
      </label>
    </div>
  );
}

function classLabel(classId: string): string {
  return classId.split('-').map((word) =>
    word.length === 0 ? word : word[0].toUpperCase() + word.slice(1),
  ).join(' ');
}
