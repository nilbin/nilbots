import { useMemo } from 'react';
import * as Slider from '@radix-ui/react-slider';
import clsx from 'clsx';
import type {
  ReplayCausalEvent,
  ReplayModel,
  ReplayStableUnitKey,
} from '../replayModel';
import { participantForUnit } from '../replayParticipants';
import type { PlaybackState } from '../playback';

/**
 * The timeline, carrying what happened rather than only where we are.
 *
 * A replay of a deterministic game is a record, not a video: every event is indexed by
 * tick, so the transport can show the shape of the match before anyone presses play.
 * A lane whose marks are three hits and a block is a bot that lost, and you can see
 * that without watching.
 *
 * **Lanes are teams, not bots.** One lane per bot reads beautifully in a duel and falls
 * apart the moment a Frontline match has six lives, so the lane is the durable thing
 * (a team) and the mark carries the transient one (which unit, in that unit's own
 * accent). A duel is then the two-lane case of one rule rather than a different design.
 * Selecting a unit adds a lane of its own above them.
 *
 * Colour here is always somebody's choice: `participant.accent` is server data a player
 * picked. The lanes themselves are achromatic, because a team has no colour of its own —
 * inventing one would be the system spending chroma it does not own.
 */

/** Lane geometry in pixels: the playhead has to be sized from it, and a percentage
 *  cannot be (see the Thumb below). */
const LANE_HEIGHT = 14;
const LANE_GAP = 6;

/** Marks are ordered by how much they matter, which is also how they stack visually. */
type MarkKind = 'fired' | 'hit' | 'lost' | 'form';

interface Mark {
  key: string;
  tick: number;
  kind: MarkKind;
  lane: string;
  accent: string;
}

/** Events that belong to the match rather than to any one team. */
const MATCH_EVENTS = new Set([
  'frontline-position-advanced',
  'base-breached',
]);

export default function Timeline({
  replay,
  playback,
  selectedUnitKey,
}: {
  replay: ReplayModel;
  playback: PlaybackState;
  selectedUnitKey: ReplayStableUnitKey | null;
}) {
  const lastTick = Math.max(1, playback.tickCount - 1);

  const { lanes, marks, moments } = useMemo(() => {
    const accentOf = (unitKey: ReplayStableUnitKey | undefined): string =>
      (unitKey ? participantForUnit(replay, unitKey)?.accent : null) ??
      'currentColor';
    const teamOf = (unitKey: ReplayStableUnitKey | undefined): number | null =>
      replay.units.find((unit) => unit.unitKey === unitKey)?.teamId ?? null;

    const collected: Mark[] = [];
    const matchMoments: { key: string; tick: number }[] = [];

    const add = (
      event: ReplayCausalEvent,
      tick: number,
      unitKey: ReplayStableUnitKey | undefined,
      kind: MarkKind,
    ) => {
      const teamId = teamOf(unitKey);
      if (teamId === null || unitKey === undefined) return;
      const accent = accentOf(unitKey);
      collected.push({
        key: `${event.eventId}:${kind}`,
        tick,
        kind,
        lane: `team:${teamId}`,
        accent,
      });
      if (unitKey === selectedUnitKey) {
        collected.push({
          key: `${event.eventId}:${kind}:sel`,
          tick,
          kind,
          lane: 'selected',
          accent,
        });
      }
    };

    for (const replayTick of replay.ticks) {
      for (const event of [
        ...replayTick.lifecycleEvents,
        ...replayTick.events,
      ]) {
        const tick = replayTick.tick;
        if (MATCH_EVENTS.has(event.type)) {
          matchMoments.push({ key: event.eventId, tick });
          continue;
        }
        switch (event.type) {
          case 'shot':
            add(event, tick, event.sourceActor?.unitKey, 'fired');
            break;
          case 'damage':
            add(event, tick, event.targetActor?.unitKey, 'hit');
            break;
          case 'destroyed':
          case 'disqualified':
            add(event, tick, event.targetActor?.unitKey, 'lost');
            break;
          case 'form-changed':
          case 'fabricated':
            add(event, tick, event.sourceActor?.unitKey, 'form');
            break;
          default:
            break;
        }
      }
    }

    // Teams in their own declared order; nothing here counts them.
    const teamLanes = replay.teams.map((team) => ({
      key: `team:${team.teamId}`,
      label: `Team ${team.teamId}`,
    }));
    const laneList = selectedUnitKey
      ? [{ key: 'selected', label: 'Selected' }, ...teamLanes]
      : teamLanes;

    return { lanes: laneList, marks: collected, moments: matchMoments };
  }, [replay, selectedUnitKey]);

  const at = (tick: number) => `${(tick / lastTick) * 100}%`;
  const progress = Math.min(playback.time, lastTick) / lastTick;

  return (
    <Slider.Root
      className="relative flex touch-none flex-col justify-center select-none"
      min={0}
      max={playback.tickCount}
      step={0.01}
      value={[Math.min(playback.time, playback.tickCount)]}
      onValueChange={([value]) => playback.seek(value)}
      aria-label="Match timeline — drag to seek"
    >
      <Slider.Track
        className="relative flex w-full grow flex-col py-1"
        style={{ gap: LANE_GAP }}
      >
        {/* Events belonging to the match cross every lane, because they happened to
            all of them: a position advancing is not one team's mark. */}
        {moments.map((moment) => (
          <span
            key={moment.key}
            aria-hidden
            className="absolute inset-y-0 w-px bg-arena-edge2"
            style={{ left: at(moment.tick) }}
          />
        ))}

        {lanes.map((lane) => (
          <span
            key={lane.key}
            className="relative block"
            style={{ height: LANE_HEIGHT }}
          >
            <span
              aria-hidden
              className="absolute top-1/2 right-0 left-0 h-px -translate-y-1/2 bg-arena-edge"
            />
            <span
              aria-hidden
              className="absolute top-1/2 left-0 h-px -translate-y-1/2 bg-arena-dim"
              style={{ width: `${progress * 100}%` }}
            />
            {marks
              .filter((mark) => mark.lane === lane.key)
              .map((mark) => (
                <span
                  key={mark.key}
                  aria-hidden
                  className={clsx(
                    'absolute top-1/2 -translate-x-1/2 -translate-y-1/2',
                    // A shot is a hairline, a hit taken is a notch, a loss is a block:
                    // weight tracks consequence, so a lane reads at a glance.
                    mark.kind === 'fired' && 'h-2 w-px opacity-70',
                    mark.kind === 'hit' && 'h-2.5 w-[3px] rounded-[1px]',
                    mark.kind === 'lost' && 'h-3.5 w-[7px] rounded-[2px]',
                    mark.kind === 'form' &&
                      'h-2 w-2 rotate-45 rounded-[1px] opacity-80',
                  )}
                  style={{ left: at(mark.tick), background: mark.accent }}
                />
              ))}
          </span>
        ))}
      </Slider.Track>

      {/* A pixel height, not a percentage. Radix wraps the thumb in a zero-height
          positioned span, so `h-[100%]` resolves to nothing and the playhead silently
          disappears — which it did. The span is the anchor; this centres on it. */}
      <Slider.Thumb
        className="absolute top-1/2 block w-0.5 -translate-y-1/2 rounded-full bg-arena-text focus:outline-2 focus:outline-offset-2 focus:outline-arena-accent"
        style={{ height: LANE_HEIGHT * lanes.length + LANE_GAP * (lanes.length - 1) + 10 }}
        aria-label="Playhead"
      />
    </Slider.Root>
  );
}
