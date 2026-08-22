import type {
  ReplayArcWorkshopInterruptionReason,
  ReplayArcWorkshopPhase,
  ReplayModel,
  ReplayPosition,
} from '../replayModel';

/**
 * Workshop readings shared by both renderers.
 *
 * A Workshop is a NEUTRAL landmark: its whole state is public to both teams,
 * so it is drawn under fog exactly as it is drawn in the open, and neither
 * renderer may gate it on a vision set. The state names the training TEAM and
 * nothing else — the trainee's identity exists only on the completion fact, so
 * a viewer must never guess a body from whoever happens to stand on the tile.
 *
 * Phase and the tick clock come straight off the published state; only the two
 * BEATS are folded, because a completion is a single fact on a single tick and
 * a flash that lives exactly one tick is subliminal at playback speed (the
 * lesson `drawSignatureCombatEvents` and `LEVEL_UP_BEAT_TICKS` already paid
 * for). The fold is per replay and cached, like `arcVeterancyFor`, because
 * both renderers draw every frame.
 */
export const WORKSHOP_COMPLETION_BEAT_TICKS = 2.4;
export const WORKSHOP_INTERRUPT_BEAT_TICKS = 1.4;

/** A transient landmark beat, resolved so a renderer keeps no state. */
export interface ArcWorkshopBeat {
  teamId: number;
  /** Ticks since the fact, fractional and deterministic under seeking. */
  age: number;
  /** 1 at the fact, easing to 0 at the end of the beat window. */
  strength: number;
}

export interface ArcWorkshopCompletionBeat extends ArcWorkshopBeat {
  xpAward: number;
}

export interface ArcWorkshopInterruptionBeat extends ArcWorkshopBeat {
  reason: ReplayArcWorkshopInterruptionReason;
  progressLost: number;
}

/** One renderer-neutral reading of one Workshop at one playhead. */
export interface ArcWorkshopVisual {
  workshopId: string;
  tile: ReplayPosition;
  phase: ReplayArcWorkshopPhase;
  /** The team channelling; null in every phase but `training`. */
  trainingTeamId: number | null;
  progressTicks: number;
  requiredTicks: number;
  /** 0..1 of the declared channel; 0 in every phase but `training`. */
  progressFraction: number;
  /** Whole ticks until it is ready again; null unless dormant. */
  dormantRemainingTicks: number | null;
  /**
   * 0..1 of the declared dormancy already served; null when dormant and the
   * ruleset never declared a dormancy length, because a fraction invented from
   * an unknown denominator is a lie about how close the Workshop is.
   */
  dormantFraction: number | null;
  completion: ArcWorkshopCompletionBeat | null;
  interruption: ArcWorkshopInterruptionBeat | null;
}

/**
 * The published Workshop fields both renderers hold — satisfied by the model's
 * `ReplayArcWorkshop` and by the story presentation's richer entry alike, so
 * neither renderer has to convert before asking for a reading.
 */
export interface ArcWorkshopSource {
  workshopId: string;
  trainingTile: ReplayPosition;
  phase: ReplayArcWorkshopPhase;
  trainingTeamId: number | null;
  progressTicks: number;
  requiredTicks: number;
  readyAtTick: number | null;
}

export interface ArcWorkshopIndex {
  /** Whether this replay carries Workshops at all. */
  present: boolean;
  /** Declared dormancy length in ticks; null when the rules do not say. */
  dormancyTicks: number | null;
  /** Declared channel length in ticks; null when the rules do not say. */
  requiredChannelTicks: number | null;
  visualsAt(
    workshops: readonly ArcWorkshopSource[] | null | undefined,
    time: number,
  ): ArcWorkshopVisual[];
}

const cache = new WeakMap<
  ReplayModel,
  { tickCount: number; index: ArcWorkshopIndex }
>();

export function arcWorkshopsFor(replay: ReplayModel): ArcWorkshopIndex {
  const cached = cache.get(replay);
  if (cached?.tickCount === replay.ticks.length) return cached.index;
  const index = buildArcWorkshops(replay);
  cache.set(replay, { tickCount: replay.ticks.length, index });
  return index;
}

interface CompletionFold {
  tick: number;
  teamId: number;
  xpAward: number;
}

interface InterruptionFold {
  tick: number;
  teamId: number;
  reason: ReplayArcWorkshopInterruptionReason;
  progressLost: number;
}

export function buildArcWorkshops(replay: ReplayModel): ArcWorkshopIndex {
  const completions = new Map<string, CompletionFold[]>();
  const interruptions = new Map<string, InterruptionFold[]>();
  let sawWorkshopFact = false;

  for (const [tickIndex, tick] of replay.ticks.entries()) {
    // Workshop work can resolve during tick start, so both event buckets are
    // read — the same trap that made sentinel fire invisible for a campaign.
    for (const event of [...tick.lifecycleEvents, ...tick.events]) {
      const fact = event.arcRelayFact;
      if (!fact) continue;
      if (fact.kind === 'workshop-training-completed') {
        sawWorkshopFact = true;
        const beats = completions.get(fact.workshopId) ?? [];
        beats.push({
          tick: tickIndex,
          teamId: fact.teamId,
          xpAward: fact.xpAward,
        });
        completions.set(fact.workshopId, beats);
      } else if (fact.kind === 'workshop-training-interrupted') {
        sawWorkshopFact = true;
        const beats = interruptions.get(fact.workshopId) ?? [];
        beats.push({
          tick: tickIndex,
          teamId: fact.teamId,
          reason: fact.reason,
          progressLost: fact.progressLost,
        });
        interruptions.set(fact.workshopId, beats);
      } else if (
        fact.kind === 'workshop-ready' ||
        fact.kind === 'workshop-training-started'
      ) {
        sawWorkshopFact = true;
      }
    }
  }

  const declared =
    replay.contract.kind === 'v3-generic' &&
    replay.contract.rawContract.rules.gameMode.kind === 'arc-relay'
      ? replay.contract.rawContract.rules.gameMode.veterancyWorkshops ?? null
      : null;

  const anyState = replay.ticks.some((tick) => {
    const mode = tick.after.mode;
    return (
      mode?.kind === 'arc-relay' &&
      'workshops' in mode &&
      mode.workshops.length > 0
    );
  });

  return {
    present: declared !== null || anyState || sawWorkshopFact,
    dormancyTicks: declared?.dormancyTicks ?? null,
    requiredChannelTicks: declared?.requiredChannelTicks ?? null,
    visualsAt(workshops, time) {
      return (workshops ?? []).map((workshop) => {
        const phase =
          declared?.dormancyTicks === 0 && workshop.phase === 'dormant'
            ? 'ready'
            : workshop.phase;
        const requiredTicks = Math.max(1, workshop.requiredTicks);
        const dormantRemainingTicks =
          phase !== 'dormant' || workshop.readyAtTick === null
            ? null
            : Math.max(0, Math.ceil(workshop.readyAtTick - time));
        const dormancy = declared?.dormancyTicks ?? null;
        return {
          workshopId: workshop.workshopId,
          tile: { ...workshop.trainingTile },
          phase,
          trainingTeamId: workshop.trainingTeamId,
          progressTicks: workshop.progressTicks,
          requiredTicks: workshop.requiredTicks,
          progressFraction:
            phase === 'training'
              ? clamp01(workshop.progressTicks / requiredTicks)
              : 0,
          dormantRemainingTicks,
          dormantFraction:
            phase !== 'dormant' ||
              workshop.readyAtTick === null ||
              dormancy === null
              ? null
              : clamp01(
                  1 -
                    (workshop.readyAtTick - time) / Math.max(1, dormancy),
                ),
          completion: beatAt(
            completions.get(workshop.workshopId),
            time,
            WORKSHOP_COMPLETION_BEAT_TICKS,
            (entry, age, strength) => ({
              teamId: entry.teamId,
              age,
              strength,
              xpAward: entry.xpAward,
            }),
          ),
          interruption: beatAt(
            interruptions.get(workshop.workshopId),
            time,
            WORKSHOP_INTERRUPT_BEAT_TICKS,
            (entry, age, strength) => ({
              teamId: entry.teamId,
              age,
              strength,
              reason: entry.reason,
              progressLost: entry.progressLost,
            }),
          ),
        };
      });
    },
  };
}

function beatAt<TFold extends { tick: number }, TBeat>(
  beats: TFold[] | undefined,
  time: number,
  window: number,
  resolve: (entry: TFold, age: number, strength: number) => TBeat,
): TBeat | null {
  if (!beats) return null;
  let active: TBeat | null = null;
  for (const entry of beats) {
    if (entry.tick > time) break;
    const age = time - entry.tick;
    if (age + Number.EPSILON < window) {
      active = resolve(entry, age, Math.max(0, 1 - (age / window) ** 1.45));
    }
  }
  return active;
}

function clamp01(value: number): number {
  return Math.max(0, Math.min(1, value));
}
