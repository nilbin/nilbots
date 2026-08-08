import type {
  ReplayCausalEvent,
  ReplayModel,
  ReplayTick,
} from '../replayModel';
import {
  isAttackEvent,
  isDestructionEvent,
  isDisqualificationEvent,
} from '../replayModel';
import type {
  AdaptiveMusicKeyframe,
  MusicTrigger,
  ResolvedMusicDirectorConfig,
} from './director';
import type { AdaptiveScoreState } from './types';

export type AdaptiveMusicTimelineMode = 'causal' | 'retrospective';

export type AdaptiveMusicHighlightKind =
  | 'contact'
  | 'combat'
  | 'objective'
  | 'damage'
  | 'decisive';

export interface AdaptiveMusicHighlight {
  id: string;
  startTick: number;
  peakTick: number;
  endTick: number;
  rawScore: number;
  relativeRank: number;
  kind: AdaptiveMusicHighlightKind;
  primary: boolean;
  /** Horizontal requests are reserved for the primary highlight. */
  requestTick: number | null;
  /** The slow vertical build begins before the horizontal request. */
  buildStartTick: number;
  evidenceEventIds: readonly string[];
}

export interface RetrospectiveMusicPlannerOptions {
  ticksPerSecond?: number;
  bpm?: number;
  beatsPerBar?: number;
}

export interface RetrospectiveMusicPlan {
  frames: readonly AdaptiveMusicKeyframe[];
  highlights: readonly AdaptiveMusicHighlight[];
  barTicks: number;
}

interface PlannerSeed {
  tick: number;
  weight: number;
  stakes: number;
  kind: AdaptiveMusicHighlightKind;
  evidenceEventIds: readonly string[];
}

interface PlannerEpisode {
  startTick: number;
  peakTick: number;
  endTick: number;
  rawScore: number;
  kind: AdaptiveMusicHighlightKind;
  evidenceEventIds: readonly string[];
}

interface SelectedEpisode extends PlannerEpisode {
  relativeRank: number;
  primary: boolean;
  requestTick: number | null;
  buildStartTick: number;
}

const DEFAULT_TICKS_PER_SECOND = 5;
const DEFAULT_BPM = 120;
const DEFAULT_BEATS_PER_BAR = 4;
const PRIMARY_REQUEST_LEAD_BARS = 3;
const BUILD_LEAD_BARS = 2;
const HIGHLIGHT_SEPARATION_BARS = 6;
const HIGHLIGHT_CAP_BARS = 12;
const RELEASE_BARS = 2;
const APPROACH_INTENSITY_LIFT = 0.16;

const KIND_PRIORITY: Readonly<Record<AdaptiveMusicHighlightKind, number>> = {
  contact: 0,
  combat: 1,
  objective: 2,
  damage: 3,
  decisive: 4,
};

/**
 * Turn a finalized replay's causal observations into a small, replay-relative
 * musical schedule. Gameplay evidence and triggers stay on their original
 * ticks; only horizontal state, intensity, momentum, and dwell are replanned.
 */
export function buildRetrospectiveMusicPlan(
  replay: ReplayModel,
  causalFrames: readonly AdaptiveMusicKeyframe[],
  config: ResolvedMusicDirectorConfig,
  options: RetrospectiveMusicPlannerOptions = {},
): RetrospectiveMusicPlan {
  const ticksPerSecond = positiveOr(
    options.ticksPerSecond,
    DEFAULT_TICKS_PER_SECOND,
  );
  const bpm = positiveOr(options.bpm, DEFAULT_BPM);
  const beatsPerBar = positiveOr(
    options.beatsPerBar,
    DEFAULT_BEATS_PER_BAR,
  );
  const barTicks = (ticksPerSecond * 60 * beatsPerBar) / bpm;
  const episodes = buildEpisodes(replay, causalFrames, barTicks);
  const highlights = selectHighlights(
    episodes,
    causalFrames,
    barTicks,
  );
  return {
    frames: applySchedule(causalFrames, highlights, barTicks, config),
    highlights: highlights.map(toPublicHighlight),
    barTicks,
  };
}

function buildEpisodes(
  replay: ReplayModel,
  frames: readonly AdaptiveMusicKeyframe[],
  barTicks: number,
): PlannerEpisode[] {
  const ticksByNumber = new Map(
    replay.ticks.map((tick) => [tick.tick, tick]),
  );
  const seeds = frames
    .map((frame) =>
      seedForFrame(frame, ticksByNumber.get(frame.tick) ?? null),
    )
    .filter((seed): seed is PlannerSeed => seed !== null);
  if (seeds.length === 0) return [];

  const groups: PlannerSeed[][] = [];
  for (const seed of seeds) {
    const current = groups.at(-1);
    if (
      current === undefined ||
      seed.tick - current.at(-1)!.tick >= barTicks
    ) {
      groups.push([seed]);
    } else {
      current.push(seed);
    }
  }

  return groups.map((group) => {
    const peak = [...group].sort(comparePeakSeeds)[0]!;
    const maximumWeight = Math.max(...group.map((seed) => seed.weight));
    const totalWeight = group.reduce(
      (total, seed) => total + seed.weight,
      0,
    );
    const maximumStakes = Math.max(...group.map((seed) => seed.stakes));
    const kind = group.reduce<AdaptiveMusicHighlightKind>(
      (strongest, seed) =>
        KIND_PRIORITY[seed.kind] > KIND_PRIORITY[strongest]
          ? seed.kind
          : strongest,
      'contact',
    );
    const density = clamp01(totalWeight / Math.max(1, group.length * 0.8));
    const rawScore =
      maximumWeight * 0.62 +
      density * 0.23 +
      maximumStakes * 0.15;
    return {
      startTick: group[0]!.tick,
      peakTick: peak.tick,
      endTick: group.at(-1)!.tick,
      rawScore,
      kind,
      evidenceEventIds: [
        ...new Set(group.flatMap((seed) => seed.evidenceEventIds)),
      ].sort(),
    };
  });
}

function seedForFrame(
  frame: AdaptiveMusicKeyframe,
  tick: ReplayTick | null,
): PlannerSeed | null {
  const evidence = causalEvents(tick);
  let weight = 0;
  let kind: AdaptiveMusicHighlightKind = 'contact';

  const offer = (
    candidateWeight: number,
    candidateKind: AdaptiveMusicHighlightKind,
  ) => {
    if (
      candidateWeight > weight ||
      (candidateWeight === weight &&
        KIND_PRIORITY[candidateKind] > KIND_PRIORITY[kind])
    ) {
      weight = candidateWeight;
      kind = candidateKind;
    }
  };

  for (const event of evidence) {
    // Under either generation's spelling — a v3 `attack`/`destruction` is the same
    // beat as a v1 `shot`/`destroyed`, and a highlight reel that only recognised one
    // vocabulary ranked a class-arm match as though nothing had happened in it.
    if (
      event.type === 'base-breached' ||
      isDestructionEvent(event.type) ||
      isDisqualificationEvent(event.type)
    ) {
      offer(1, 'decisive');
    } else if (event.type === 'damage') {
      offer(0.84, 'damage');
    } else if (isAttackEvent(event.type)) {
      offer(0.42, 'combat');
    }
  }
  for (const trigger of frame.triggers) {
    switch (trigger) {
      case 'destruction':
        offer(1, 'decisive');
        break;
      case 'damage':
        offer(0.84, 'damage');
        break;
      case 'overtime':
        offer(0.72, 'objective');
        break;
      case 'shot':
        offer(0.42, 'combat');
        break;
      case 'contact':
        offer(0.24, 'contact');
        break;
      case 'resolve':
        // A result ends the score but is not, by itself, a gameplay highlight.
        break;
    }
  }
  if (frame.features.objectiveMotion > 0) {
    offer(
      0.34 + frame.features.objectiveMotion * 0.36,
      'objective',
    );
  }
  if (weight <= 0) return null;

  const evidenceEventIds = evidence.map((event) => event.eventId);
  for (const trigger of frame.triggers) {
    if (trigger !== 'resolve') {
      evidenceEventIds.push(triggerEvidenceId(frame.tick, trigger));
    }
  }
  return {
    tick: frame.tick,
    weight,
    stakes: Math.max(
      frame.features.healthUrgency,
      frame.features.controlUrgency,
      frame.features.lateUrgency * 0.7,
    ),
    kind,
    evidenceEventIds,
  };
}

function selectHighlights(
  episodes: readonly PlannerEpisode[],
  frames: readonly AdaptiveMusicKeyframe[],
  barTicks: number,
): SelectedEpisode[] {
  if (episodes.length === 0 || frames.length === 0) return [];
  const durationTicks = Math.max(1, frames.at(-1)!.tick + 1);
  const totalBars = durationTicks / barTicks;
  const maximum = Math.min(
    3,
    1 + Math.floor(totalBars / HIGHLIGHT_CAP_BARS),
  );
  const separationTicks = HIGHLIGHT_SEPARATION_BARS * barTicks;
  const ranked = [...episodes].sort(compareEpisodes);
  const selected: Array<PlannerEpisode & { relativeRank: number }> = [];
  for (let rank = 0; rank < ranked.length && selected.length < maximum; rank += 1) {
    const episode = ranked[rank]!;
    if (
      selected.some(
        (other) =>
          Math.abs(other.peakTick - episode.peakTick) < separationTicks,
      )
    ) {
      continue;
    }
    selected.push({ ...episode, relativeRank: rank });
  }
  if (selected.length === 0) return [];

  const primaryRank = Math.min(
    ...selected.map((episode) => episode.relativeRank),
  );
  return selected
    .map((episode): SelectedEpisode => {
      const primary = episode.relativeRank === primaryRank;
      const nominalRequest =
        episode.peakTick - PRIMARY_REQUEST_LEAD_BARS * barTicks;
      const snappedRequest = snapDownToBar(nominalRequest, barTicks);
      const requestTick =
        primary && snappedRequest >= barTicks ? snappedRequest : null;
      const buildStartTick = Math.max(
        0,
        (requestTick ?? episode.peakTick) - BUILD_LEAD_BARS * barTicks,
      );
      return {
        ...episode,
        primary,
        requestTick,
        buildStartTick,
      };
    })
    .sort((left, right) => left.peakTick - right.peakTick);
}

function applySchedule(
  causalFrames: readonly AdaptiveMusicKeyframe[],
  highlights: readonly SelectedEpisode[],
  barTicks: number,
  config: ResolvedMusicDirectorConfig,
): AdaptiveMusicKeyframe[] {
  const primary = highlights.find((highlight) => highlight.primary) ?? null;
  const peakState: AdaptiveScoreState =
    primary?.kind === 'decisive' ? 'climax' : 'combat';
  let previous: AdaptiveMusicKeyframe | null = null;

  return causalFrames.map((causal, index) => {
    const terminal =
      causal.state === 'resolve' || causal.triggers.includes('resolve');
    const state: AdaptiveScoreState = terminal
      ? 'resolve'
      : primary?.requestTick !== null &&
          primary?.requestTick !== undefined &&
          causal.tick >= primary.requestTick
        ? peakState
        : 'sparse';
    const intensity = Math.max(
      envelopeAt(
        causal.tick,
        highlights,
        peakState,
        barTicks,
        config,
      ),
      approachFloor(causal, config),
    );
    const nextFrame = causalFrames[index + 1] ?? causal;
    const targetIntensity = terminal
      ? config.stateIntensity.resolve
      : Math.max(
          envelopeAt(
            causal.tick + 1,
            highlights,
            peakState,
            barTicks,
            config,
          ),
          approachFloor(nextFrame, config),
        );
    const lookaheadFrame =
      causalFrames[
        Math.min(
          causalFrames.length - 1,
          index + Math.max(1, Math.round(barTicks)),
        )
      ] ?? causal;
    const lookahead = terminal
      ? config.stateIntensity.resolve
      : Math.max(
          envelopeAt(
            causal.tick + barTicks,
            highlights,
            peakState,
            barTicks,
            config,
          ),
          approachFloor(lookaheadFrame, config),
        );
    const momentum = terminal
      ? -1
      : clampSigned((lookahead - intensity) * 2.4);
    const trend =
      momentum > config.trendThreshold
        ? 'rising'
        : momentum < -config.trendThreshold
          ? 'falling'
          : 'steady';
    const deltaTicks =
      previous === null ? 1 : Math.max(1, causal.tick - previous.tick);
    const dwellTicks =
      previous !== null && previous.state === state
        ? previous.dwellTicks + deltaTicks
        : 0;
    const planned: AdaptiveMusicKeyframe = {
      ...causal,
      state,
      intensity,
      targetIntensity,
      momentum,
      trend,
      dwellTicks,
      // These are intentionally the exact causal observations and impulses.
      features: causal.features,
      triggers: causal.triggers,
    };
    previous = planned;
    return planned;
  });
}

/**
 * Close-range approach shapes only the vertical stem bed. It cannot seed a
 * highlight or request a section, and the causal director has already reduced
 * distant closing pressure to zero.
 */
function approachFloor(
  frame: AdaptiveMusicKeyframe,
  config: ResolvedMusicDirectorConfig,
): number {
  return clamp01(
    config.stateIntensity.sparse +
      frame.features.closingPressure * APPROACH_INTENSITY_LIFT,
  );
}

function envelopeAt(
  tick: number,
  highlights: readonly SelectedEpisode[],
  peakState: AdaptiveScoreState,
  barTicks: number,
  config: ResolvedMusicDirectorConfig,
): number {
  const baseline = config.stateIntensity.sparse;
  let intensity = baseline;
  for (const highlight of highlights) {
    const strength = highlight.primary
      ? 1
      : Math.max(0.44, 0.68 - highlight.relativeRank * 0.12);
    const primaryPeak = config.stateIntensity[peakState];
    const peak = baseline + (primaryPeak - baseline) * strength;
    const releaseEnd = highlight.endTick + RELEASE_BARS * barTicks;
    let candidate = baseline;
    if (tick >= highlight.buildStartTick && tick < highlight.peakTick) {
      candidate = lerp(
        baseline,
        peak,
        smoothStep(
          highlight.buildStartTick,
          highlight.peakTick,
          tick,
        ),
      );
    } else if (tick >= highlight.peakTick && tick <= highlight.endTick) {
      candidate = peak;
    } else if (tick > highlight.endTick && tick < releaseEnd) {
      candidate = lerp(
        peak,
        baseline,
        smoothStep(highlight.endTick, releaseEnd, tick),
      );
    }
    intensity = Math.max(intensity, candidate);
  }
  return clamp01(intensity);
}

function causalEvents(tick: ReplayTick | null): ReplayCausalEvent[] {
  if (tick === null) return [];
  const events = new Map<string, ReplayCausalEvent>();
  for (const event of [...tick.lifecycleEvents, ...tick.events]) {
    events.set(event.eventId, event);
  }
  return [...events.values()];
}

function triggerEvidenceId(tick: number, trigger: MusicTrigger): string {
  return `trigger:${tick}:${trigger}`;
}

function comparePeakSeeds(left: PlannerSeed, right: PlannerSeed): number {
  return (
    right.weight - left.weight ||
    KIND_PRIORITY[right.kind] - KIND_PRIORITY[left.kind] ||
    right.stakes - left.stakes ||
    // Equal recurring events culminate at the later tick.
    right.tick - left.tick
  );
}

function compareEpisodes(
  left: PlannerEpisode,
  right: PlannerEpisode,
): number {
  return (
    right.rawScore - left.rawScore ||
    KIND_PRIORITY[right.kind] - KIND_PRIORITY[left.kind] ||
    left.peakTick - right.peakTick
  );
}

function toPublicHighlight(
  highlight: SelectedEpisode,
): AdaptiveMusicHighlight {
  return {
    id: `highlight:${highlight.peakTick}:${highlight.relativeRank}`,
    startTick: highlight.startTick,
    peakTick: highlight.peakTick,
    endTick: highlight.endTick,
    rawScore: highlight.rawScore,
    relativeRank: highlight.relativeRank,
    kind: highlight.kind,
    primary: highlight.primary,
    requestTick: highlight.requestTick,
    buildStartTick: highlight.buildStartTick,
    evidenceEventIds: highlight.evidenceEventIds,
  };
}

function snapDownToBar(value: number, barTicks: number): number {
  return Math.max(0, Math.floor(value / barTicks) * barTicks);
}

function smoothStep(edge0: number, edge1: number, value: number): number {
  if (edge1 <= edge0) return value >= edge1 ? 1 : 0;
  const normalized = clamp01((value - edge0) / (edge1 - edge0));
  return normalized * normalized * (3 - 2 * normalized);
}

function lerp(from: number, to: number, amount: number): number {
  return from + (to - from) * amount;
}

function positiveOr(value: number | undefined, fallback: number): number {
  return value !== undefined && Number.isFinite(value) && value > 0
    ? value
    : fallback;
}

function clamp01(value: number): number {
  return Math.max(0, Math.min(1, value));
}

function clampSigned(value: number): number {
  return Math.max(-1, Math.min(1, value));
}
