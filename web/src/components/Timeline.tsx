import { useMemo } from 'react';
import * as Slider from '@radix-ui/react-slider';
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
} from '../replayModel';
import type { PlaybackState } from '../playback';
import { playerAccent } from '../presentation/playerAccent';
import { unitAccent } from '../render/unitPresentation';
import { styleVariables } from '../presentation/styleVariables';

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

/**
 * Lane geometry. The classes below are the source of truth for what is drawn —
 * `h-3.5` and `gap-1` — and these two exist only because the playhead's height is
 * arithmetic over them and cannot be a percentage (see the Thumb).
 *
 * 14 and 4 give an 18px lane pitch, which with the track's own 8px above and below is
 * the 48px band the design draws two lanes in.
 */
const LANE_HEIGHT = 14; // h-3.5
const LANE_GAP = 4; //    gap-1

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
    // Through `unitAccent`, not `participant.accent`: a generation-3 class match
    // gives every participant the same default colour, and two lanes of marks in
    // one hue is the same as no lanes at all.
    const accentOf = (unitKey: ReplayStableUnitKey | undefined): string =>
      unitKey === undefined
        ? 'currentColor'
        : playerAccent(unitAccent(replay, unitKey), 'panel');
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
        // Firing, dying and being disqualified each arrive in two spellings —
        // a duel says `shot`/`destroyed`/`disqualified` where a generic
        // replay-v3 match says `attack`/`destruction`/`participant-disqualified`.
        // Matched on the literals alone, every generation-3 timeline is blank.
        if (isAttackEvent(event.type)) {
          add(event, tick, event.sourceActor?.unitKey, 'fired');
        } else if (
          isDestructionEvent(event.type) ||
          isDisqualificationEvent(event.type)
        ) {
          add(event, tick, event.targetActor?.unitKey, 'lost');
        } else if (event.type === 'damage') {
          add(event, tick, event.targetActor?.unitKey, 'hit');
        } else if (
          event.type === 'form-changed' ||
          event.type === 'fabricated'
        ) {
          add(event, tick, event.sourceActor?.unitKey, 'form');
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
      <Slider.Track className="relative flex w-full grow flex-col gap-1 py-2">
        {/* Events belonging to the match cross every lane, because they happened to
            all of them: a position advancing is not one team's mark. */}
        {moments.map((moment) => (
          <span
            key={moment.key}
            aria-hidden
            className="runtime-position absolute inset-y-0 w-px bg-arena-edge2"
            style={styleVariables({ '--runtime-position': at(moment.tick) })}
          />
        ))}

        {/* A destruction is the one thing in a lane that changed the whole match, so it
            also carries a rule across all of them. Dashed and at 40%, because it is
            context for the other lanes rather than an event in them. */}
        {marks
          .filter((mark) => mark.kind === 'lost' && mark.lane !== 'selected')
          .map((mark) => (
            <span
              key={`${mark.key}:rule`}
              aria-hidden
              className="player-accent-border runtime-position absolute inset-y-0 w-0 border-l border-dashed opacity-40"
              style={styleVariables({
                '--runtime-position': at(mark.tick),
                '--player-accent': mark.accent,
              })}
            />
          ))}

        {lanes.map((lane) => (
          <span key={lane.key} className="relative block h-3.5">
            <span
              aria-hidden
              className="absolute top-1/2 right-0 left-0 h-[1.5px] -translate-y-1/2 bg-arena-edge2"
            />
            <span
              aria-hidden
              className="runtime-progress absolute top-1/2 left-0 h-[1.5px] -translate-y-1/2 bg-arena-dim"
              style={styleVariables({
                '--runtime-progress': `${progress * 100}%`,
              })}
            />
            {marks
              .filter((mark) => mark.lane === lane.key)
              .map((mark) => (
                <span
                  key={mark.key}
                  aria-hidden
                  className={clsx(
                    'absolute top-1/2 -translate-x-1/2 -translate-y-1/2',
                    'player-accent-fill runtime-position',
                    // A shot is a hairline, a hit taken is a notch, a loss is a block:
                    // weight tracks consequence, so a lane reads at a glance.
                    mark.kind === 'fired' && 'h-2 w-[1.5px] opacity-75',
                    mark.kind === 'hit' && 'h-[11px] w-[5px] rounded-[2px]',
                    mark.kind === 'lost' && 'size-2.5 rounded-[2.5px]',
                    mark.kind === 'form' &&
                      'h-2 w-2 rotate-45 rounded-[1px] opacity-80',
                  )}
                  style={styleVariables({
                    '--runtime-position': at(mark.tick),
                    '--player-accent': mark.accent,
                  })}
                />
              ))}
          </span>
        ))}
      </Slider.Track>

      {/* A pixel height, not a percentage. Radix wraps the thumb in a zero-height
          positioned span, so `h-[100%]` resolves to nothing and the playhead silently
          disappears — which it did. The span is the anchor; this centres on it. */}
      <Slider.Thumb
        className="runtime-height absolute top-1/2 block w-[1.5px] -translate-y-1/2 rounded-full bg-arena-text focus:outline-2 focus:outline-offset-2 focus:outline-arena-text"
        style={styleVariables({
          '--runtime-height':
            LANE_HEIGHT * lanes.length +
            LANE_GAP * (lanes.length - 1) +
            8,
        })}
        aria-label="Playhead"
      >
        {/* The knob is what says this is a handle rather than a marker, and it is the
            only part of the playhead big enough to put a finger on. */}
        <span
          aria-hidden
          className="absolute top-1/2 left-1/2 block size-[9px] -translate-x-1/2 -translate-y-1/2 rounded-full bg-arena-text"
        />
      </Slider.Thumb>
    </Slider.Root>
  );
}
