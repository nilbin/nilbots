import type { ReplayDocument } from './types';
import { botLook, presentationAccent } from './render/arenaThemes';
import { adjustAccentForBackground } from './render/adaptiveAccent';
import { stateBefore } from './render/interpolate';
import { replayMaxHealth } from './replayMetadata';

/**
 * Everything a panel needs to describe one tick of a replay, derived once here rather
 * than in each surface that draws it.
 *
 * This exists because there are now two of those surfaces: the web viewer's own panels,
 * and the mobile app's native ones, which receive this shape over the WebView bridge
 * instead of parsing replays themselves. Control pressure, overtime limits, zone accrual
 * and hold phases are rules-derived — a second implementation of them would be a rules
 * surface that drifts the first time the rules move (see the root guide's rules-change
 * surfaces). There is one implementation, and it is this file.
 */

export interface ControlPresentation {
  /** Signed toward slot 0; the bar is centred and reads ± limit. */
  pressure: number;
  limit: number;
  overtime: boolean;
  /** Why the pressure is doing what it is doing, already worded. */
  phase: string | null;
  names: [string, string];
}

export interface BotPresentation {
  slot: number;
  name: string;
  /** Already adjusted for the panel background — not the raw bot accent. */
  accent: string;
  lookLabel: string;
  runtimeKind: string;
  status: string;
  health: number;
  maxHealth: number;
  cooldown: number;
  energy?: number;
  /** Cumulative zone ticks where the replay carries a tally; null when it does not. */
  zoneTicks: number | null;
  /** On a zone tile and counting, this tick. */
  holdingZone: boolean;
  action?: string;
  actionResult?: string;
  debug?: string;
  visibleTiles: number;
  visibleEnemies: { x: number; y: number }[];
}

export interface TickPresentation {
  tick: number;
  control: ControlPresentation | null;
  bots: BotPresentation[];
}

export interface ReplayPresenter {
  tickCount: number;
  maxHealth: number;
  at: (tick: number) => TickPresentation;
}

/**
 * Build a presenter for one replay. The zone tally is scanned once and captured here,
 * because deriving it costs a pass over every tick and it never changes.
 */
export function createPresenter(replay: ReplayDocument): ReplayPresenter {
  const maxHealth = replayMaxHealth(replay);
  const tickCount = replay.ticks.length;
  const zone = deriveZone(replay);

  const at = (rawTick: number): TickPresentation => {
    const tick = Math.max(0, Math.min(rawTick, tickCount - 1));
    const tickData = replay.ticks[tick];
    const states = stateBefore(replay, tick + 1);

    return {
      tick,
      control: deriveControl(replay, tick, zone),
      bots: replay.header.participants.map((participant) => {
        const state = states.find((candidate) => candidate.slot === participant.slot)!;
        const botTick = tickData.bots.find((candidate) => candidate.slot === participant.slot);
        const look = botLook(participant.lookId, participant.slot);
        const onZone = zone?.onZone.has(`${state.x},${state.y}`) ?? false;
        const controlLimit = controlLimitAt(replay, tick);

        return {
          slot: participant.slot,
          name: participant.name,
          accent: adjustAccentForBackground(
            presentationAccent(look, participant.accent),
            '#111823',
          ),
          lookLabel: look.label,
          runtimeKind: participant.runtimeKind,
          status: state.status,
          health: state.health,
          maxHealth,
          cooldown: state.cooldown,
          energy: state.energy,
          zoneTicks: zone?.cumulative?.[tick][participant.slot] ?? null,
          holdingZone:
            onZone &&
            state.status === 'Active' &&
            (controlLimit === undefined ||
              (botTick?.validatedAction === 'Wait' && botTick.result === 'Success')),
          action: botTick?.chosenAction,
          actionResult: botTick?.result,
          debug: botTick?.debug,
          visibleTiles: botTick?.visibleTiles.length ?? 0,
          visibleEnemies: botTick?.visibleEnemies.map((enemy) => ({ x: enemy.x, y: enemy.y })) ?? [],
        };
      }),
    };
  };

  return { tickCount, maxHealth, at };
}

type Zone = { onZone: Set<string>; cumulative: Record<number, number>[] | null };

function controlLimitAt(replay: ReplayDocument, tick: number): number | undefined {
  const tickData = replay.ticks[Math.min(tick, replay.ticks.length - 1)];
  const overtime =
    replay.header.controlOvertimeStartTick !== undefined &&
    tickData.tick >= replay.header.controlOvertimeStartTick;
  return overtime
    ? (replay.header.controlOvertimePressureLimit ?? replay.header.controlPressureLimit)
    : replay.header.controlPressureLimit;
}

/**
 * Zone tiles, and a cumulative per-slot tally where one can be trusted.
 *
 * Hardened replays carry the engine's own tally per tick (state.zoneTicks) — read it,
 * never re-derive. Legacy replays predate that, so both accrual modes are re-derived and
 * the one whose totals match the authoritative result is kept: the pattern that once
 * showed 136/118 for a true 18/0.
 */
function deriveZone(replay: ReplayDocument): Zone | null {
  const tiles = replay.header.zoneTiles;
  if (!tiles) return null;
  const onZone = new Set(tiles.map(([x, y]) => `${x},${y}`));
  if (replay.header.controlPressureLimit !== undefined) return { onZone, cumulative: null };

  if (replay.ticks.some((tick) => tick.state.some((state) => state.zoneTicks !== undefined))) {
    const cumulative = replay.ticks.map((tick) =>
      Object.fromEntries(tick.state.map((state) => [state.slot, state.zoneTicks ?? 0])),
    ) as Record<number, number>[];
    return { onZone, cumulative };
  }

  const sharedRun: Record<number, number> = {};
  const exclusiveRun: Record<number, number> = {};
  const shared: Record<number, number>[] = [];
  const exclusive: Record<number, number>[] = [];
  for (const tick of replay.ticks) {
    const on = tick.state.filter(
      (state) => state.status === 'Active' && onZone.has(`${state.x},${state.y}`),
    );
    for (const state of on) sharedRun[state.slot] = (sharedRun[state.slot] ?? 0) + 1;
    if (on.length === 1) exclusiveRun[on[0].slot] = (exclusiveRun[on[0].slot] ?? 0) + 1;
    shared.push({ ...sharedRun });
    exclusive.push({ ...exclusiveRun });
  }
  const final = replay.result?.bots;
  const matchesResult = (series: Record<number, number>[]) =>
    final !== undefined &&
    final.every((bot) => (series[series.length - 1]?.[bot.slot] ?? 0) === (bot.zoneTicks ?? 0));
  const cumulative = matchesResult(exclusive)
    ? exclusive
    : matchesResult(shared)
      ? shared
      : exclusive;
  return { onZone, cumulative };
}

function deriveControl(
  replay: ReplayDocument,
  tick: number,
  zone: Zone | null,
): ControlPresentation | null {
  const limit = controlLimitAt(replay, tick);
  if (limit === undefined || !zone) return null;

  const tickData = replay.ticks[Math.min(tick, replay.ticks.length - 1)];
  const overtime =
    replay.header.controlOvertimeStartTick !== undefined &&
    tickData.tick >= replay.header.controlOvertimeStartTick;
  const name = (slot: number) => replay.header.participants[slot]?.name ?? `slot ${slot}`;

  const activeOccupants = tickData.state.filter(
    (state) => state.status === 'Active' && zone.onZone.has(`${state.x},${state.y}`),
  );

  let phase: string | null;
  if (replay.header.controlBySoleOccupancy) {
    const previousTick = replay.ticks[Math.max(0, Math.min(tick - 1, replay.ticks.length - 1))];
    const previousOccupants =
      tick > 0
        ? previousTick.state.filter(
            (state) => state.status === 'Active' && zone.onZone.has(`${state.x},${state.y}`),
          )
        : [];
    const soleName = activeOccupants.length === 1 ? name(activeOccupants[0].slot) : null;
    phase =
      activeOccupants.length === 1
        ? previousOccupants.length > 1
          ? `CONTEST BROKEN · ${soleName} GAINS`
          : `SOLE OCCUPANT · ${soleName} GAINS`
        : activeOccupants.length > 1
          ? 'CONTESTED · PRESSURE DECAYS'
          : 'EMPTY · PRESSURE DECAYS';
  } else {
    const holders = tickData.bots.filter((bot) => {
      const state = tickData.state.find((candidate) => candidate.slot === bot.slot);
      return (
        bot.validatedAction === 'Wait' &&
        bot.result === 'Success' &&
        state?.status === 'Active' &&
        zone.onZone.has(`${state.x},${state.y}`)
      );
    });
    phase =
      holders.length === 1
        ? `HOLDING · ${name(holders[0].slot)} GAINS`
        : holders.length > 1
          ? 'BOTH HOLD · PRESSURE FROZEN'
          : 'NO ACTIVE HOLD · PRESSURE DECAYS';
  }

  return {
    pressure: tickData.controlPressure ?? 0,
    limit,
    overtime,
    phase,
    names: [name(0), name(1)],
  };
}
