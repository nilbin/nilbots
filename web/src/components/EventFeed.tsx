import { useMemo, useState } from 'react';
import clsx from 'clsx';
import type {
  ReplayCausalEvent,
  ReplayModel,
  ReplayStableUnitKey,
} from '../replayModel';
import {
  isAttackEvent,
  isDestructionEvent,
  isDisqualificationEvent,
  isMovementEvent,
  isRotationEvent,
} from '../replayModel';
import {
  actorName,
  teamName,
  unitName,
} from '../replayParticipants';
import ToggleButton from './ToggleButton';

/**
 * Events that end a unit's participation. The only ones that earn a colour.
 *
 * Asked through the model predicates rather than a literal set, because destruction and
 * disqualification each arrive in two spellings: a generation-3 replay says
 * `destruction`/`participant-disqualified` where a duel says `destroyed`/`disqualified`,
 * and a hard-coded set silently greys out every death in the newer format.
 */
function isTerminalEvent(type: string): boolean {
  return (
    isDestructionEvent(type) ||
    isDisqualificationEvent(type) ||
    type === 'fault'
  );
}

/**
 * A row is a headline and, when there is one, the clause that explains it.
 *
 * `describe` writes one sentence with ` · ` between what happened and why it matters,
 * which is exactly the seam the design puts a second line on: "Bastille gen-5
 * destroyed" over "hit by Pincer gen-10". Splitting here rather than in `describe`
 * keeps one description to maintain instead of two.
 */
function splitDetail(text: string): [string, string | null] {
  const seam = text.indexOf(' · ');
  return seam === -1
    ? [text, null]
    : [text.slice(0, seam), text.slice(seam + 3)];
}

/**
 * The index.
 *
 * This was a feed: a flat list that grew downward and coloured eight event types in
 * eight hues, which is eight things competing with the one colour that means something.
 * A deterministic replay is indexable, so the list is a seek control — newest first,
 * every row jumping to the tick it names, and filterable to the unit you selected
 * rather than making you scroll past everything the other one did.
 */
export default function EventFeed({
  replay,
  tick,
  selectedUnitKey,
  onSeek,
}: {
  replay: ReplayModel;
  tick: number;
  selectedUnitKey?: ReplayStableUnitKey | null;
  onSeek?: (tick: number) => void;
}) {
  const [mineOnly, setMineOnly] = useState(false);
  const entries = useMemo(() => {
    const list: { tick: number; event: ReplayCausalEvent }[] = [];
    for (const replayTick of replay.ticks) {
      if (replayTick.tick > tick) break;
      for (const event of [
        ...replayTick.lifecycleEvents,
        ...replayTick.events,
      ]) {
        if (isMovementEvent(event.type) || isRotationEvent(event.type))
          continue;
        list.push({ tick: replayTick.tick, event });
      }
    }
    return list.slice(-120).reverse();
  }, [replay, tick]);

  const involves = (event: ReplayCausalEvent) =>
    !selectedUnitKey ||
    event.sourceActor?.unitKey === selectedUnitKey ||
    event.targetActor?.unitKey === selectedUnitKey;
  const shown =
    mineOnly && selectedUnitKey ? entries.filter((e) => involves(e.event)) : entries;

  const describe = (event: ReplayCausalEvent): string => {
    const stableUnit = replay.units.find(
      (unit) =>
        unit.teamId === event.teamId && unit.unitId === event.unitId,
    );
    const stableName = stableUnit
      ? unitName(replay, stableUnit.unitKey)
      : `team ${event.teamId ?? '?'} unit ${event.unitId ?? '?'}`;
    const arc = event.arcRelayFact;
    if (arc) {
      const source = 'coreId' in arc
        ? arc.coreId.sourceWellId.replace(/[-_]/g, ' ')
        : '';
      switch (arc.kind) {
        case 'core-born':
          return `${source} Core is born · contest begins at ${arc.position.x},${arc.position.y}`;
        case 'core-picked-up':
          return `${actorName(replay, arc.carrierActor)} claims the ${source} Core`;
        case 'core-handed-off':
          return `${actorName(replay, arc.sourceActor)} hands the ${source} Core to ${actorName(replay, arc.targetActor)}`;
        case 'core-dropped':
          return `${actorName(replay, arc.sourceActor)} drops the ${source} Core · loose at ${arc.position.x},${arc.position.y}`;
        case 'core-banked':
          return `${teamName(replay, arc.teamId)} banks the ${source} Core · charge ${arc.chargePips}/3`;
        case 'pulse':
          return `${teamName(replay, arc.teamId)} fires Pulse ${arc.pulseOrdinal} · opposing reactor ${arc.opposingReactorIntegrity}/3`;
        case 'core-relocated':
          return `${source} Core relocates · ${arc.relocationKind}`;
        case 'well-changed':
          return `${arc.wellId} Well ${arc.pendingCharge ? 'rearms' : 'is ready'}`;
        case 'signature-changed':
          return `${actorName(replay, arc.ownerActor)} ${arc.reason} ${arc.signatureId}`;
        case 'body-relocated':
          return `${actorName(replay, arc.ownerActor)} relocates ${actorName(replay, arc.targetActor)} · ${arc.signatureId}`;
        case 'signature-damage':
          return `${actorName(replay, arc.ownerActor)} deals ${arc.amount} with ${arc.signatureId}`;
        case 'signature-repair':
          return `${actorName(replay, arc.ownerActor)} repairs ${actorName(replay, arc.targetActor)} for ${arc.amount}`;
      }
    }
    // Attack/destruction/disqualification arrive in version-specific
    // spellings; the model predicates own that equivalence.
    if (isAttackEvent(event.type)) {
      return event.targetActor
        ? `${actorName(replay, event.sourceActor)} hits ${actorName(replay, event.targetActor)}`
        : `${actorName(replay, event.sourceActor)} fires`;
    }
    if (isDestructionEvent(event.type)) {
      return (
        `${actorName(replay, event.targetActor)} is destroyed` +
        (event.respawnAtTick !== null
          ? ` · returns T${event.respawnAtTick}`
          : event.rebuildReadyAtTick !== null
            ? ` · rebuild ready T${event.rebuildReadyAtTick}`
            : '')
      );
    }
    if (isDisqualificationEvent(event.type)) {
      return `${actorName(replay, event.targetActor)} is disqualified`;
    }
    switch (event.type) {
      case 'projectile-deflected':
        // Named for what it costs the shooter: the guard's health is unchanged
        // and the bolt is coming back at whoever fired it.
        return (
          `${actorName(replay, event.targetActor)} turns a shot back` +
          (event.toFacing ? ` · guarding ${event.toFacing}` : '')
        );
      case 'damage':
        return (
          `${actorName(replay, event.targetActor)} takes ${event.amount ?? '?'} damage` +
          (event.newHealth === null
            ? ''
            : ` · ${event.newHealth} hp left`)
        );
      case 'respawned':
        return `${actorName(replay, event.sourceActor)} returns`;
      case 'move-blocked':
      case 'movement-blocked':
        return `${actorName(replay, event.sourceActor)} bumps into something`;
      case 'fault':
        return `${actorName(replay, event.sourceActor)} runtime fault`;
      case 'frontline-progress-changed':
        return event.claimingTeamId === null
          ? 'Frontline pressure neutralizes'
          : `${teamName(replay, event.claimingTeamId)} advances capture to ${event.captureProgress ?? 0}`;
      case 'frontline-position-advanced':
        return `Frontline moves to position ${(event.toPositionIndex ?? 0) + 1}`;
      case 'base-breached':
        return `${teamName(replay, event.teamId ?? -1)} breaches the base`;
      case 'fabrication-unlocked':
        return `${stableName} unlocks for fabrication`;
      case 'fabrication-queued':
        return (
          `${actorName(replay, event.sourceActor)} queues ${stableName}` +
          (event.fabricationAtTick === null
            ? ''
            : ` · spawns T${event.fabricationAtTick}`)
        );
      case 'fabricated':
        return `${actorName(replay, event.sourceActor)} is fabricated`;
      case 'rebuild-ready':
        return `${stableName} is rebuilt and ready`;
      case 'form-transition-started':
        return (
          `${actorName(replay, event.sourceActor)} begins transforming` +
          (event.fromFormId && event.toFormId
            ? ` · ${event.fromFormId} → ${event.toFormId}`
            : '') +
          (event.formTransitionCompletesAtTick === null
            ? ''
            : ` · completes T${event.formTransitionCompletesAtTick}`)
        );
      case 'form-changed':
        return (
          `${actorName(replay, event.sourceActor)} changes form` +
          (event.toFormId ? ` · ${event.toFormId}` : '')
        );
      case 'form-transition-cancelled':
        return (
          `${actorName(replay, event.sourceActor ?? event.targetActor)} transformation is cancelled` +
          (event.toFormId ? ` · ${event.toFormId}` : '')
        );
      default:
        return event.type;
    }
  };

  return (
    <div className="panel flex min-h-0 flex-1 flex-col">
      <div className="pad flex items-center gap-2.5 pb-2">
        <h2 className="lab">
          Index · {shown.length} event{shown.length === 1 ? '' : 's'}
        </h2>
        {selectedUnitKey && (
          <span className="ml-auto flex gap-1">
            {([false, true] as const).map((only) => (
              <ToggleButton
                key={String(only)}
                onClick={() => setMineOnly(only)}
                pressed={mineOnly === only}
                className="t-micro px-2 py-1"
              >
                {only ? 'Selected' : 'All'}
              </ToggleButton>
            ))}
          </span>
        )}
      </div>
      {/* The list scrolls; it never grows the page. The cap applies at every width so
          advancing playback cannot make the index stretch the whole viewer grid. */}
      <ol
        className="grid max-h-64 min-h-0 flex-1 auto-rows-min gap-px overflow-y-auto px-3 pb-3"
        aria-live="polite"
      >
        {shown.length === 0 && (
          <li className="t-body px-2 py-2 text-arena-dim italic">
            Nothing has happened yet.
          </li>
        )}
        {shown.map(({ tick: eventTick, event }) => {
          const [headline, detail] = splitDetail(describe(event));
          const rowClass = clsx(
            'grid w-full grid-cols-[38px_1fr] items-baseline gap-[10px] rounded-[3px] border border-transparent px-2 py-[7px] text-left transition-colors',
            onSeek && 'hover:bg-arena-raise',
            eventTick === tick && 'border-arena-edge bg-arena-raise',
          );
          const contents = (
            <>
              <span className="val">
                {String(eventTick).padStart(3, '0')}
              </span>
              <span
                className={clsx(
                  't-body',
                  isTerminalEvent(event.type)
                    ? 'font-semibold text-arena-hot'
                    : involves(event)
                      ? 'text-arena-text'
                      : 'text-arena-dim',
                )}
              >
                {headline}
                {detail && (
                  <em className="t-micro mt-px block not-italic">{detail}</em>
                )}
              </span>
            </>
          );
          return (
            <li key={event.eventId}>
              {onSeek ? (
                <button
                  type="button"
                  onClick={() => onSeek(eventTick)}
                  className={rowClass}
                >
                  {contents}
                </button>
              ) : (
                <div className={rowClass}>{contents}</div>
              )}
            </li>
          );
        })}
      </ol>
    </div>
  );
}
