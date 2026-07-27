import { useEffect, useMemo, useRef } from 'react';
import clsx from 'clsx';
import type { ReplayCausalEvent, ReplayModel } from '../replayModel';
import { actorName, teamName } from '../replayParticipants';

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
        if (event.type === 'move' || event.type === 'turn') continue;
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
    switch (event.type) {
      case 'shot':
        return event.targetActor
          ? `${actorName(replay, event.sourceActor)} hits ${actorName(replay, event.targetActor)}`
          : `${actorName(replay, event.sourceActor)} fires`;
      case 'damage':
        return (
          `${actorName(replay, event.targetActor)} takes ${event.amount ?? '?'} damage` +
          (event.newHealth === null
            ? ''
            : ` (${event.newHealth} hp left)`)
        );
      case 'destroyed':
        return `${actorName(replay, event.targetActor)} is destroyed`;
      case 'respawned':
        return `${actorName(replay, event.sourceActor)} returns`;
      case 'move-blocked':
        return `${actorName(replay, event.sourceActor)} bumps into something`;
      case 'fault':
        return `${actorName(replay, event.sourceActor)} runtime fault`;
      case 'disqualified':
        return `${actorName(replay, event.targetActor)} is disqualified`;
      case 'frontline-progress-changed':
        return event.claimingTeamId === null
          ? 'Frontline pressure neutralizes'
          : `${teamName(replay, event.claimingTeamId)} advances capture to ${event.captureProgress ?? 0}`;
      case 'frontline-position-advanced':
        return `Frontline moves to position ${(event.toPositionIndex ?? 0) + 1}`;
      case 'base-breached':
        return `${teamName(replay, event.teamId ?? -1)} breaches the base`;
      default:
        return event.type;
    }
  };

  return (
    <div className="flex min-h-0 flex-1 flex-col rounded-lg border border-arena-edge bg-arena-panel">
      <h2 className="border-b border-arena-edge px-3 py-2 font-mono text-xs tracking-widest text-arena-dim">
        EVENT FEED
      </h2>
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
                event.type === 'destroyed' || event.type === 'damage',
              'text-amber-300':
                event.type === 'fault' ||
                event.type === 'disqualified',
              'text-cyan-200':
                event.type === 'frontline-progress-changed' ||
                event.type === 'frontline-position-advanced',
              'text-arena-text':
                event.type === 'shot' ||
                event.type === 'move-blocked',
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
