import { useMemo, useState } from 'react';
import clsx from 'clsx';
import type {
  ReplayCausalEvent,
  ReplayModel,
  ReplayStableUnitKey,
} from '../replayModel';
import {
  actorName,
  teamName,
  unitName,
} from '../replayParticipants';

/** Events that end a unit's participation. The only ones that earn a colour. */
const TERMINAL = new Set(['destroyed', 'disqualified', 'fault']);

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
        if (event.type === 'move' || event.type === 'turn') continue;
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
            : ` · ${event.newHealth} hp left`)
        );
      case 'destroyed':
        return (
          `${actorName(replay, event.targetActor)} is destroyed` +
          (event.respawnAtTick !== null
            ? ` · returns T${event.respawnAtTick}`
            : event.rebuildReadyAtTick !== null
              ? ` · rebuild ready T${event.rebuildReadyAtTick}`
              : '')
        );
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
          `${actorName(replay, event.sourceActor)} begins anchoring` +
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
          `${actorName(replay, event.sourceActor ?? event.targetActor)} anchoring is cancelled` +
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
              <button
                key={String(only)}
                type="button"
                onClick={() => setMineOnly(only)}
                aria-pressed={mineOnly === only}
                className={clsx(
                  'pill transition-colors',
                  mineOnly === only
                    ? 'btn-on text-arena-text'
                    : 'text-arena-dim hover:text-arena-text',
                )}
              >
                {only ? 'Selected' : 'All'}
              </button>
            ))}
          </span>
        )}
      </div>
      {/* The list scrolls; it never grows the page. Below `lg` the panel is content in
          the page and the cap keeps it from burying the transport; at `lg` the column is
          out of flow with a real height, so flex-1 bounds it. */}
      <ol
        className="grid max-h-64 min-h-0 flex-1 auto-rows-min gap-px overflow-y-auto px-3 pb-3 lg:max-h-none"
        aria-live="polite"
      >
        {shown.length === 0 && (
          <li className="t-body px-2 py-2 text-arena-dim italic">
            Nothing has happened yet.
          </li>
        )}
        {shown.map(({ tick: eventTick, event }) => {
          const [headline, detail] = splitDetail(describe(event));
          return (
            <li key={event.eventId}>
              <button
                type="button"
                onClick={() => onSeek?.(eventTick)}
                disabled={!onSeek}
                className={clsx(
                  'grid w-full grid-cols-[38px_1fr] items-baseline gap-[10px] rounded-[3px] border border-transparent px-2 py-[7px] text-left transition-colors',
                  onSeek && 'hover:bg-arena-raise',
                  // Where the playhead is, in the index — the row and the tick badge are
                  // the same fact seen from two places.
                  eventTick === tick && 'border-arena-edge bg-arena-raise',
                )}
              >
                <span className="val">
                  {String(eventTick).padStart(3, '0')}
                </span>
                <span
                  className={clsx(
                    't-body',
                    TERMINAL.has(event.type)
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
              </button>
            </li>
          );
        })}
      </ol>
    </div>
  );
}
