import { useEffect, useMemo, useRef } from 'react';
import clsx from 'clsx';
import type { ReplayCausalEvent, ReplayModel } from '../replayModel';
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

export default function EventFeed({
  replay,
  tick,
}: {
  replay: ReplayModel;
  tick: number;
}) {
  const feedRef = useRef<HTMLOListElement>(null);
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
    return list.slice(-80);
  }, [replay, tick]);

  useEffect(() => {
    const element = feedRef.current;
    if (element) element.scrollTop = element.scrollHeight;
  }, [entries.length]);

  const describe = (event: ReplayCausalEvent): string => {
    const stableUnit = replay.units.find(
      (unit) =>
        unit.teamId === event.teamId && unit.unitId === event.unitId,
    );
    const stableName = stableUnit
      ? unitName(replay, stableUnit.unitKey)
      : `team ${event.teamId ?? '?'} unit ${event.unitId ?? '?'}`;
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
            : ` (${event.newHealth} hp left)`)
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
    <div className="flex min-h-0 flex-1 flex-col rounded-lg border border-arena-edge bg-arena-panel">
      <h2 className="border-b border-arena-edge px-3 py-2 font-mono text-xs tracking-widest text-arena-dim">
        EVENT FEED
      </h2>
      {/* The feed scrolls; it never grows the page.

          `lg:max-h-none` used to lift the cap on desktop, on the reasonable-sounding idea
          that a tall column should let the feed fill it. It has no height to fill: the
          arena and this column are two cells of one auto-height grid row, so the row is as
          tall as its tallest cell — and an uncapped feed *is* the tallest cell the moment a
          match produces more than a screenful of events. The arena, stretched to the row,
          grew with it. Watch a busy match on a wide window and the board visibly inflates
          under the playhead.

          The cap is gone again at `lg`, but the reasoning is no longer wishful: the column
          it sits in is out of flow there and has a real height to fill, so `flex-1` bounds
          the feed and it scrolls. Below `lg` the panel is content in the page and the fixed
          cap is what keeps it from burying the transport. */}
      <ol
        ref={feedRef}
        className="max-h-56 min-h-0 flex-1 space-y-1 overflow-y-auto p-3 font-mono text-xs lg:max-h-none"
        aria-live="polite"
      >
        {entries.length === 0 && (
          <li className="text-arena-dim italic">No combat events yet…</li>
        )}
        {entries.map(({ tick: eventTick, event }) => (
          <li
            key={event.eventId}
            className={clsx('flex gap-2', {
              'text-red-300':
                isDestructionEvent(event.type) || event.type === 'damage',
              'text-amber-300':
                event.type === 'fault' ||
                isDisqualificationEvent(event.type),
              'text-cyan-200':
                event.type === 'frontline-progress-changed' ||
                event.type === 'frontline-position-advanced',
              'text-violet-200':
                event.type === 'fabrication-queued' ||
                event.type === 'fabricated' ||
                event.type === 'form-transition-started',
              'text-emerald-200':
                event.type === 'fabrication-unlocked' ||
                event.type === 'rebuild-ready' ||
                event.type === 'form-changed',
              'text-amber-200':
                event.type === 'form-transition-cancelled',
              'text-sky-200': event.type === 'projectile-deflected',
              'text-arena-text':
                isAttackEvent(event.type) ||
                event.type === 'move-blocked' ||
                event.type === 'movement-blocked',
            })}
          >
            <span className="text-arena-dim">
              {String(eventTick).padStart(3, '0')}
            </span>
            <span>{describe(event)}</span>
          </li>
        ))}
      </ol>
    </div>
  );
}
