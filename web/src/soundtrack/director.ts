import type {
  ReplayCausalEvent,
  ReplayModel,
  ReplayTick,
} from '../replayModel';
import {
  createPresenter,
  type TickPresentation,
} from '../replayPresentation';
import {
  buildRetrospectiveMusicPlan,
  type AdaptiveMusicHighlight,
  type AdaptiveMusicTimelineMode,
  type RetrospectiveMusicPlannerOptions,
} from './planner';
import type { AdaptiveScoreState } from './types';

export type MusicMomentumTrend = 'rising' | 'steady' | 'falling';

export type MusicTrigger =
  | 'contact'
  | 'shot'
  | 'damage'
  | 'destruction'
  | 'overtime'
  | 'resolve';

/**
 * Normalized, causal evidence behind one director decision. All numeric values
 * are in [0, 1]. Counters include only ticks revealed up to this frame.
 */
export interface MusicDirectorFeatures {
  activity: number;
  pace: number;
  contact: number;
  /** Current opposing-unit proximity, independent of line-of-sight. */
  proximity: number;
  /** Current same-life opposing-pair closing motion from revealed positions. */
  approach: number;
  /** Rolling approach evidence; rises only from current and past positions. */
  closingPressure: number;
  /** Rolling visible chase/closing evidence used for horizontal phrase changes. */
  pursuitPressure: number;
  /** Rolling shot/projectile/damage evidence used for horizontal phrase changes. */
  combatPressure: number;
  /** Rolling exceptional danger used to earn, and not merely spike, climax. */
  climaxPressure: number;
  /** Threat that is perceptually immediate, whether or not the phrase changes. */
  acuteThreat: number;
  projectileThreat: number;
  urgency: number;
  healthUrgency: number;
  controlUrgency: number;
  lateUrgency: number;
  objectiveMotion: number;
  /** Imminence derived causally from objective distance and recent advance rate. */
  objectiveImminence: number;
  stall: number;
  quietTicks: number;
  stationaryTicks: number;
  overtime: boolean;
}

/**
 * A frame begins at `tick`. `intensity` is the value at that exact boundary;
 * `targetIntensity` is approached until another revealed tick supersedes it.
 */
export interface AdaptiveMusicKeyframe {
  tick: number;
  state: AdaptiveScoreState;
  intensity: number;
  targetIntensity: number;
  momentum: number;
  trend: MusicMomentumTrend;
  dwellTicks: number;
  features: MusicDirectorFeatures;
  triggers: readonly MusicTrigger[];
}

export interface AdaptiveMusicFrame
  extends Omit<AdaptiveMusicKeyframe, 'tick' | 'intensity' | 'momentum'> {
  time: number;
  sourceTick: number;
  intensity: number;
  momentum: number;
}

export interface ResolvedMusicDirectorConfig {
  maxHealth: number;
  risePerTick: number;
  fallPerTick: number;
  combatMemoryDecay: number;
  contactMemoryDecay: number;
  closingPressureDecay: number;
  closingPressureThreshold: number;
  pursuitPressureDecay: number;
  combatPressureDecay: number;
  climaxPressureDecay: number;
  objectiveRateDecay: number;
  pursuitPressureThreshold: number;
  combatPressureThreshold: number;
  climaxCombatThreshold: number;
  climaxPressureThreshold: number;
  objectiveClimaxEtaTicks: number;
  paceDecay: number;
  momentumDecay: number;
  trendThreshold: number;
  stallQuietStartTicks: number;
  stallQuietFullTicks: number;
  stallStationaryStartTicks: number;
  stallStationaryFullTicks: number;
  releaseTicks: number;
  stateEntryTicks: Readonly<Record<AdaptiveScoreState, number>>;
  stateIntensity: Readonly<Record<AdaptiveScoreState, number>>;
  minDwellTicks: Readonly<Record<AdaptiveScoreState, number>>;
}

export interface MusicDirectorOptions {
  /**
   * Finalized, non-live replay documents use the whole match by default.
   * Causal mode remains available for live playback, diagnostics, and prefix
   * equivalence tests.
   */
  planningMode?: 'auto' | AdaptiveMusicTimelineMode;
  /** A live clock always forces causal direction, even if its document is final. */
  followingLive?: boolean;
  planner?: RetrospectiveMusicPlannerOptions;
  /**
   * Replay headers currently omit max health. Rules 0.1-0.5 and the viewer use
   * three; a future ruleset can override this without changing replay data.
   */
  maxHealth?: number;
  risePerTick?: number;
  fallPerTick?: number;
  combatMemoryDecay?: number;
  contactMemoryDecay?: number;
  closingPressureDecay?: number;
  closingPressureThreshold?: number;
  pursuitPressureDecay?: number;
  combatPressureDecay?: number;
  climaxPressureDecay?: number;
  objectiveRateDecay?: number;
  pursuitPressureThreshold?: number;
  combatPressureThreshold?: number;
  climaxCombatThreshold?: number;
  climaxPressureThreshold?: number;
  objectiveClimaxEtaTicks?: number;
  paceDecay?: number;
  momentumDecay?: number;
  trendThreshold?: number;
  stallQuietStartTicks?: number;
  stallQuietFullTicks?: number;
  stallStationaryStartTicks?: number;
  stallStationaryFullTicks?: number;
  releaseTicks?: number;
  stateEntryTicks?: Partial<Record<AdaptiveScoreState, number>>;
  stateIntensity?: Partial<Record<AdaptiveScoreState, number>>;
  minDwellTicks?: Partial<Record<AdaptiveScoreState, number>>;
}

export interface AdaptiveMusicTimeline {
  /**
   * One frame per replay tick. Causal timelines and every partial prefix
   * produce byte-for-byte-equivalent frames for their shared prefix.
   */
  frames: readonly AdaptiveMusicKeyframe[];
  initialFrame: AdaptiveMusicKeyframe;
  config: ResolvedMusicDirectorConfig;
  mode: AdaptiveMusicTimelineMode;
  highlights: readonly AdaptiveMusicHighlight[];
  planningBarTicks: number | null;
}

const STATE_RANK: Readonly<Record<AdaptiveScoreState, number>> = {
  sparse: 0,
  tension: 1,
  pursuit: 2,
  combat: 3,
  climax: 4,
  resolve: 5,
};

const STATE_BY_RANK: readonly AdaptiveScoreState[] = [
  'sparse',
  'tension',
  'pursuit',
  'combat',
  'climax',
  'resolve',
];

const STATE_RANGE: Readonly<
  Record<AdaptiveScoreState, readonly [minimum: number, maximum: number]>
> = {
  sparse: [0.04, 0.27],
  tension: [0.24, 0.48],
  pursuit: [0.42, 0.65],
  combat: [0.63, 0.86],
  climax: [0.82, 1],
  resolve: [0.04, 0.16],
};

export const DEFAULT_MUSIC_DIRECTOR_CONFIG: ResolvedMusicDirectorConfig = {
  maxHealth: 3,
  risePerTick: 0.34,
  fallPerTick: 0.14,
  combatMemoryDecay: 0.78,
  contactMemoryDecay: 0.84,
  closingPressureDecay: 0.84,
  closingPressureThreshold: 0.1,
  pursuitPressureDecay: 0.82,
  combatPressureDecay: 0.86,
  climaxPressureDecay: 0.65,
  objectiveRateDecay: 0.9,
  pursuitPressureThreshold: 0.52,
  combatPressureThreshold: 0.56,
  climaxCombatThreshold: 0.78,
  climaxPressureThreshold: 0.65,
  objectiveClimaxEtaTicks: 30,
  paceDecay: 0.76,
  momentumDecay: 0.7,
  trendThreshold: 0.055,
  stallQuietStartTicks: 8,
  stallQuietFullTicks: 28,
  stallStationaryStartTicks: 5,
  stallStationaryFullTicks: 20,
  releaseTicks: 16,
  stateEntryTicks: {
    sparse: 0,
    tension: 2,
    pursuit: 2,
    combat: 2,
    climax: 2,
    resolve: 0,
  },
  stateIntensity: {
    sparse: 0.08,
    tension: 0.3,
    pursuit: 0.5,
    combat: 0.72,
    climax: 0.9,
    resolve: 0.08,
  },
  minDwellTicks: {
    sparse: 2,
    tension: 4,
    pursuit: 10,
    combat: 10,
    climax: 10,
    resolve: Number.POSITIVE_INFINITY,
  },
};

interface DirectorMemory {
  state: AdaptiveScoreState;
  dwellTicks: number;
  pendingRiseState: AdaptiveScoreState | null;
  pendingRiseTicks: number;
  pendingReleaseState: AdaptiveScoreState | null;
  pendingReleaseTicks: number;
  recentCombat: number;
  recentContact: number;
  closingPressure: number;
  pursuitPressure: number;
  combatPressure: number;
  climaxPressure: number;
  objectiveRate: number;
  pace: number;
  momentum: number;
  quietTicks: number;
  stationaryTicks: number;
  causalMaxHealth: number;
  previousTick: DirectorTick | null;
  previousFrame: AdaptiveMusicKeyframe | null;
}

interface DirectorTick {
  replay: ReplayTick;
  presentation: TickPresentation;
}

interface TickSignals {
  hasShot: boolean;
  hasDamage: boolean;
  hasDestruction: boolean;
  terminal: boolean;
  visibleContact: boolean;
  contactStarted: boolean;
  closing: boolean;
  overtimeStarted: boolean;
  activity: number;
  motion: number;
  pace: number;
  contact: number;
  proximity: number;
  approach: number;
  closingPressure: number;
  pursuitPressure: number;
  combatPressure: number;
  climaxPressure: number;
  acuteThreat: number;
  projectileThreat: number;
  urgency: number;
  healthUrgency: number;
  controlUrgency: number;
  lateUrgency: number;
  objectiveMotion: number;
  objectiveRate: number;
  objectiveImminence: number;
  stall: number;
  quietTicks: number;
  stationaryTicks: number;
  overtime: boolean;
  recentCombat: number;
  recentContact: number;
}

interface StateMemory {
  state: AdaptiveScoreState;
  dwellTicks: number;
  pendingRiseState: AdaptiveScoreState | null;
  pendingRiseTicks: number;
  pendingReleaseState: AdaptiveScoreState | null;
  pendingReleaseTicks: number;
}

/**
 * Build the score plan for the ticks currently present in a replay document.
 * Partial documents and live followers use the causal director. A finalized,
 * non-live replay is deliberately replanned against its complete narrative.
 *
 * Schema assumptions:
 * - `ticks` are an ordered, strictly increasing revealed prefix.
 * - `events` and `bots` describe that tick; `state`, projectiles and control
 *   pressure are authoritative post-tick presentation data.
 * - header limits/overtime are public from tick zero.
 * - `result` may already exist in an offline replay; the causal pass ignores it
 *   until its own `endTick`, while the retrospective pass uses finalization as
 *   permission to rank the complete match.
 *
 * Causal frames never read a later tick, replay length, winner, or replay hash.
 */
export function buildAdaptiveTimeline(
  replay: ReplayModel,
  options: MusicDirectorOptions = {},
): AdaptiveMusicTimeline {
  const causal = buildCausalAdaptiveTimeline(replay, options);
  if (!shouldUseRetrospectivePlan(replay, options)) return causal;
  const plan = buildRetrospectiveMusicPlan(
    replay,
    causal.frames,
    causal.config,
    options.planner,
  );
  return {
    ...causal,
    frames: plan.frames,
    mode: 'retrospective',
    highlights: plan.highlights,
    planningBarTicks: plan.barTicks,
  };
}

function buildCausalAdaptiveTimeline(
  replay: ReplayModel,
  options: MusicDirectorOptions,
): AdaptiveMusicTimeline {
  const presenter = createPresenter(replay);
  const config = resolveConfig({
    ...options,
    maxHealth:
      options.maxHealth ??
      (replay.contract.kind === 'legacy-partial'
        ? replay.contract.rules.legacyMaxHealth ??
          DEFAULT_MUSIC_DIRECTOR_CONFIG.maxHealth
        : presenter.maxHealth),
  });
  const initialFrame = createInitialFrame(config);
  const frames: AdaptiveMusicKeyframe[] = [];
  const memory: DirectorMemory = {
    state: 'sparse',
    dwellTicks: 0,
    pendingRiseState: null,
    pendingRiseTicks: 0,
    pendingReleaseState: null,
    pendingReleaseTicks: 0,
    recentCombat: 0,
    recentContact: 0,
    closingPressure: 0,
    pursuitPressure: 0,
    combatPressure: 0,
    climaxPressure: 0,
    objectiveRate: 0,
    pace: 0,
    momentum: 0,
    quietTicks: 0,
    stationaryTicks: 0,
    causalMaxHealth: config.maxHealth,
    previousTick: null,
    previousFrame: null,
  };

  for (let tickIndex = 0; tickIndex < replay.ticks.length; tickIndex += 1) {
    const sourceTick = replay.ticks[tickIndex];
    const tick: DirectorTick = {
      replay: sourceTick,
      presentation: presenter.at(tickIndex),
    };
    if (
      memory.previousTick !== null &&
      sourceTick.tick <= memory.previousTick.replay.tick
    ) {
      throw new Error(
        'Adaptive music requires strictly increasing replay ticks; ' +
          `got ${sourceTick.tick} after ${memory.previousTick.replay.tick}.`,
      );
    }

    const deltaTicks =
      memory.previousTick === null
        ? 1
        : Math.max(
            1,
            sourceTick.tick - memory.previousTick.replay.tick,
          );
    const causalMaxHealth =
      replay.sourceVersion === 1
        ? Math.max(
            memory.causalMaxHealth,
            ...tick.presentation.units.map((unit) => unit.health),
          )
        : memory.causalMaxHealth;
    const signals = analyzeTick(
      replay,
      tick,
      memory,
      config,
      deltaTicks,
      causalMaxHealth,
    );
    const candidate = chooseCandidateState(signals, config);
    const stateMemory = applyStateHysteresis(
      {
        state: memory.state,
        dwellTicks: memory.dwellTicks,
        pendingRiseState: memory.pendingRiseState,
        pendingRiseTicks: memory.pendingRiseTicks,
        pendingReleaseState: memory.pendingReleaseState,
        pendingReleaseTicks: memory.pendingReleaseTicks,
      },
      candidate,
      deltaTicks,
      config,
    );

    const targetIntensity = targetForState(stateMemory.state, signals, config);
    const carriedIntensity =
      memory.previousFrame === null
        ? initialFrame.intensity
        : approachIntensity(
            memory.previousFrame.intensity,
            memory.previousFrame.targetIntensity,
            sourceTick.tick - memory.previousFrame.tick,
            config,
          );
    const immediateFloor = immediateIntensityFloor(signals, stateMemory.state);
    const intensity = clamp01(Math.max(carriedIntensity, immediateFloor));
    const momentum = calculateMomentum(
      memory.momentum,
      intensity,
      targetIntensity,
      signals,
      deltaTicks,
      config,
    );
    const triggers = collectTriggers(signals);

    const frame: AdaptiveMusicKeyframe = {
      tick: sourceTick.tick,
      state: stateMemory.state,
      intensity,
      targetIntensity,
      momentum,
      trend: trendForMomentum(momentum, config.trendThreshold),
      dwellTicks: stateMemory.dwellTicks,
      features: {
        activity: signals.activity,
        pace: signals.pace,
        contact: signals.contact,
        proximity: signals.proximity,
        approach: signals.approach,
        closingPressure: signals.closingPressure,
        pursuitPressure: signals.pursuitPressure,
        combatPressure: signals.combatPressure,
        climaxPressure: signals.climaxPressure,
        acuteThreat: signals.acuteThreat,
        projectileThreat: signals.projectileThreat,
        urgency: signals.urgency,
        healthUrgency: signals.healthUrgency,
        controlUrgency: signals.controlUrgency,
        lateUrgency: signals.lateUrgency,
        objectiveMotion: signals.objectiveMotion,
        objectiveImminence: signals.objectiveImminence,
        stall: signals.stall,
        quietTicks: signals.quietTicks,
        stationaryTicks: signals.stationaryTicks,
        overtime: signals.overtime,
      },
      triggers,
    };
    frames.push(frame);

    memory.state = stateMemory.state;
    memory.dwellTicks = stateMemory.dwellTicks;
    memory.pendingRiseState = stateMemory.pendingRiseState;
    memory.pendingRiseTicks = stateMemory.pendingRiseTicks;
    memory.pendingReleaseState = stateMemory.pendingReleaseState;
    memory.pendingReleaseTicks = stateMemory.pendingReleaseTicks;
    memory.recentCombat = signals.recentCombat;
    memory.recentContact = signals.recentContact;
    memory.closingPressure = signals.closingPressure;
    memory.pursuitPressure = signals.pursuitPressure;
    memory.combatPressure = signals.combatPressure;
    memory.climaxPressure = signals.climaxPressure;
    memory.objectiveRate = signals.objectiveRate;
    memory.pace = signals.pace;
    memory.momentum = momentum;
    memory.quietTicks = signals.quietTicks;
    memory.stationaryTicks = signals.stationaryTicks;
    memory.causalMaxHealth = causalMaxHealth;
    memory.previousTick = tick;
    memory.previousFrame = frame;
  }

  return {
    frames,
    initialFrame,
    config,
    mode: 'causal',
    highlights: [],
    planningBarTicks: null,
  };
}

function shouldUseRetrospectivePlan(
  replay: ReplayModel,
  options: MusicDirectorOptions,
): boolean {
  if (options.planningMode === 'causal' || options.followingLive === true) {
    return false;
  }
  const finalTick = replay.ticks.at(-1)?.tick;
  return (
    replay.partial === false &&
    replay.result !== null &&
    finalTick !== undefined &&
    replay.result.endTick === finalTick
  );
}

/**
 * Sample without interpolating toward the next frame. The latest revealed
 * frame projects its own target forward, so fractional sampling cannot leak a
 * future damage event or result. Consumers should deduplicate `triggers` by
 * `sourceTick` when sampling repeatedly.
 */
export function sampleAdaptiveTimeline(
  timeline: AdaptiveMusicTimeline,
  tickTime: number,
): AdaptiveMusicFrame {
  const time = Number.isFinite(tickTime) ? tickTime : 0;
  const frame =
    latestFrameAtOrBefore(timeline.frames, time) ?? timeline.initialFrame;
  const elapsed = Math.max(0, time - frame.tick);
  const intensity = approachIntensity(
    frame.intensity,
    frame.targetIntensity,
    elapsed,
    timeline.config,
  );
  const momentum = clampSigned(
    frame.momentum * Math.pow(timeline.config.momentumDecay, elapsed),
  );
  return {
    time,
    sourceTick: frame.tick,
    state: frame.state,
    intensity,
    targetIntensity: frame.targetIntensity,
    momentum,
    trend: trendForMomentum(momentum, timeline.config.trendThreshold),
    dwellTicks: frame.dwellTicks,
    features: frame.features,
    triggers: frame.triggers,
  };
}

function analyzeTick(
  replay: ReplayModel,
  tick: DirectorTick,
  memory: DirectorMemory,
  config: ResolvedMusicDirectorConfig,
  deltaTicks: number,
  causalMaxHealth: number,
): TickSignals {
  const previous = memory.previousTick;
  const source = tick.replay;
  const hasShot = hasEvent(source, 'shot');
  const hasDamage = hasEvent(source, 'damage');
  const hasDestruction =
    hasEvent(source, 'destroyed') ||
    hasEvent(source, 'disqualified');
  const movementEvents = countEvent(source, 'move');
  const turnEvents = countEvent(source, 'turn');
  const positionMotion = positionMotionSince(previous, tick);
  const visibleContact = tick.presentation.units.some(
    (unit) => unit.visibleEnemies.length > 0,
  );
  const previousVisibleContact =
    previous?.presentation.units.some(
      (unit) => unit.visibleEnemies.length > 0,
    ) ?? false;
  const contactStarted = visibleContact && !previousVisibleContact;
  const approachEvidence = calculateApproachEvidence(
    replay,
    previous,
    tick,
    deltaTicks,
  );
  const closing = approachEvidence.approach > 0;

  const traversalTiles =
    source.projectileTraversals.reduce(
      (total, traversal) => total + traversal.path.length,
      0,
    );
  const projectileCount = source.after.projectiles?.length ?? 0;
  const projectileThreat = clamp01(
    projectileCount * 0.34 + traversalTiles * 0.16,
  );

  const controlUrgency = calculateControlUrgency(tick.presentation);
  const healthUrgency = calculateHealthUrgency(
    tick.presentation,
    replay.sourceVersion === 1 ? causalMaxHealth : null,
  );
  const lateUrgency = calculateLateUrgency(
    replay.contract.rules.limits.maxTicks,
    source.tick,
  );
  const overtime = isOvertime(tick.presentation);
  const previousOvertime =
    previous === null ? false : isOvertime(previous.presentation);
  const overtimeStarted = overtime && !previousOvertime;
  const objectiveMotion = calculateObjectiveMotion(previous, tick);
  const objectiveRate = calculateObjectiveRate(
    memory.objectiveRate,
    previous,
    tick,
    deltaTicks,
    config.objectiveRateDecay,
  );
  const objectiveImminence = calculateObjectiveImminence(
    controlUrgency,
    objectiveRate,
    config.objectiveClimaxEtaTicks,
  );
  const urgency = clamp01(
    Math.max(
      healthUrgency,
      controlUrgency,
      lateUrgency * 0.68,
      overtime ? 0.6 + controlUrgency * 0.3 : 0,
    ),
  );

  const visibleContactLevel = visibleContact ? 1 : 0;
  const proximity = approachEvidence.proximity;
  const approach = approachEvidence.approach;
  const closingPressure = clamp01(
    memory.closingPressure *
      Math.pow(config.closingPressureDecay, deltaTicks) +
      approach * 0.34,
  );
  const currentContact = Math.max(visibleContactLevel, proximity * 0.72);
  const recentContact = Math.max(
    memory.recentContact *
      Math.pow(config.contactMemoryDecay, deltaTicks),
    currentContact,
  );

  const participantScale = Math.max(1, replay.units.length);
  const motion = clamp01(
    (movementEvents + positionMotion * 0.7 + turnEvents * 0.15) /
      participantScale,
  );
  const activity = clamp01(
    Math.max(
      hasDamage || hasDestruction ? 1 : 0,
      hasShot ? 0.84 : 0,
      projectileThreat * 0.78,
      visibleContact ? 0.25 + motion * 0.45 : 0,
      motion * 0.52,
      approach * 0.42,
      objectiveMotion * 0.72,
    ),
  );
  const paceBlend =
    1 - Math.pow(config.paceDecay, Math.max(1, deltaTicks));
  const pace = clamp01(memory.pace + (activity - memory.pace) * paceBlend);

  const pursuitImpulse =
    visibleContact
      ? Math.max(
          closing ? 0.38 : 0,
          motion >= 0.12 ? 0.3 : 0,
          contactStarted ? 0.16 : 0,
        )
      : 0;
  const pursuitPressure = Math.max(
    closingPressure,
    clamp01(
      memory.pursuitPressure *
        Math.pow(config.pursuitPressureDecay, deltaTicks) +
        pursuitImpulse,
    ),
  );
  // A newly launched projectile and its shot event describe the same attack.
  // Use their stronger contribution instead of counting that launch twice.
  const attackImpulse = Math.max(
    hasShot ? 0.34 : 0,
    projectileThreat * 0.65,
  );
  const combatImpulse = clamp01(
    (hasDamage ? 0.48 : 0) +
      (hasDestruction ? 1 : 0) +
      attackImpulse,
  );
  const combatPressure = clamp01(
    memory.combatPressure *
      Math.pow(config.combatPressureDecay, deltaTicks) +
      combatImpulse,
  );
  const immediateCombat = hasDamage
    ? 1
    : hasDestruction
      ? 1
      : hasShot
        ? 0.82
        : projectileThreat * 0.75;
  const recentCombat = Math.max(
    memory.recentCombat *
      Math.pow(config.combatMemoryDecay, deltaTicks),
    immediateCombat,
  );
  const acuteThreat = clamp01(
    Math.max(
      hasDamage || hasDestruction ? 1 : 0,
      hasShot ? 0.68 : 0,
      projectileThreat,
      visibleContact
        ? Math.max(closing ? 0.72 : 0, activity >= 0.3 ? activity : 0.18)
        : 0,
      recentCombat * 0.65,
    ),
  );
  const climaxImpulse = clamp01(
    (hasDamage ? 0.55 : 0) +
      (projectileThreat > 0.75 ? 0.3 : 0) +
      (visibleContact && closing && motion >= 0.12 ? 0.25 : 0),
  );
  const climaxPressure = clamp01(
    memory.climaxPressure *
      Math.pow(config.climaxPressureDecay, deltaTicks) +
      climaxImpulse,
  );

  const consequential =
    hasShot ||
    hasDamage ||
    hasDestruction ||
    projectileThreat > 0 ||
    motion > 0.05 ||
    objectiveMotion > 0.02;
  const quietTicks = consequential ? 0 : memory.quietTicks + deltaTicks;
  const stationaryTicks =
    positionMotion > 0 || movementEvents > 0
      ? 0
      : memory.stationaryTicks + deltaTicks;
  const quietStall = counterRamp(
    quietTicks,
    config.stallQuietStartTicks,
    config.stallQuietFullTicks,
  );
  const stationaryStall = counterRamp(
    stationaryTicks,
    config.stallStationaryStartTicks,
    config.stallStationaryFullTicks,
  );
  const stallSuppression = hasDamage || hasDestruction || hasShot ? 0.12 : 1;
  const urgencySuppression = 1 - Math.max(0, urgency - 0.7) * 0.9;
  const stall = clamp01(
    (quietStall * 0.56 + stationaryStall * 0.44) *
      stallSuppression *
      urgencySuppression,
  );

  const terminal = isTerminalTick(
    replay,
    source,
    hasDestruction,
    controlUrgency,
  );
  return {
    hasShot,
    hasDamage,
    hasDestruction,
    terminal,
    visibleContact,
    contactStarted,
    closing,
    overtimeStarted,
    activity,
    motion,
    pace,
    contact: clamp01(Math.max(currentContact, recentContact * 0.75)),
    proximity,
    approach,
    closingPressure,
    pursuitPressure,
    combatPressure,
    climaxPressure,
    acuteThreat,
    projectileThreat,
    urgency,
    healthUrgency,
    controlUrgency,
    lateUrgency,
    objectiveMotion,
    objectiveRate,
    objectiveImminence,
    stall,
    quietTicks,
    stationaryTicks,
    overtime,
    recentCombat,
    recentContact,
  };
}

function chooseCandidateState(
  signals: TickSignals,
  config: ResolvedMusicDirectorConfig,
): AdaptiveScoreState {
  if (signals.terminal) return 'resolve';

  const criticalObjective =
    signals.controlUrgency >= 0.65 &&
    signals.objectiveImminence > 0;
  const acuteHealthPressure =
    signals.hasDamage ||
    signals.projectileThreat > 0.75 ||
    (signals.visibleContact &&
      signals.closing &&
      signals.motion >= 0.12);
  const criticalHealthUnderPressure =
    signals.healthUrgency >= 0.82 &&
    acuteHealthPressure &&
    signals.combatPressure >= config.climaxCombatThreshold &&
    signals.climaxPressure >= config.climaxPressureThreshold;
  const criticalCombat =
    (signals.controlUrgency >= 0.72 || signals.lateUrgency >= 0.86) &&
    signals.combatPressure >= config.climaxCombatThreshold &&
    signals.climaxPressure >= config.climaxPressureThreshold &&
    signals.acuteThreat >= 0.5;
  const overtimeContest =
    signals.overtime &&
    (signals.objectiveImminence > 0 ||
      signals.combatPressure >= config.climaxCombatThreshold);
  if (
    criticalObjective ||
    criticalHealthUnderPressure ||
    criticalCombat ||
    overtimeContest
  ) {
    return 'climax';
  }

  if (signals.combatPressure >= config.combatPressureThreshold) {
    return 'combat';
  }

  if (signals.pursuitPressure >= config.pursuitPressureThreshold) {
    return 'pursuit';
  }

  if (
    signals.visibleContact ||
    signals.contact >= 0.48 ||
    signals.closingPressure >= config.closingPressureThreshold ||
    signals.urgency >= 0.36 ||
    signals.objectiveMotion >= 0.08 ||
    signals.recentContact >= 0.3
  ) {
    return 'tension';
  }

  return 'sparse';
}

function applyStateHysteresis(
  memory: StateMemory,
  candidate: AdaptiveScoreState,
  deltaTicks: number,
  config: ResolvedMusicDirectorConfig,
): StateMemory {
  if (memory.state === 'resolve') return memory;
  if (candidate === 'resolve') {
    return {
      state: 'resolve',
      dwellTicks: 0,
      pendingRiseState: null,
      pendingRiseTicks: 0,
      pendingReleaseState: null,
      pendingReleaseTicks: 0,
    };
  }

  const currentRank = STATE_RANK[memory.state];
  const candidateRank = STATE_RANK[candidate];
  if (candidateRank > currentRank) {
    const nextState = STATE_BY_RANK[currentRank + 1] ?? candidate;
    const heldTicks = memory.dwellTicks + deltaTicks;
    const pendingRiseTicks =
      memory.pendingRiseState === nextState
        ? memory.pendingRiseTicks + deltaTicks
        : deltaTicks;
    const canRise =
      heldTicks >= config.minDwellTicks[memory.state] &&
      pendingRiseTicks >= config.stateEntryTicks[nextState];
    if (!canRise) {
      return {
        state: memory.state,
        dwellTicks: heldTicks,
        pendingRiseState: nextState,
        pendingRiseTicks,
        pendingReleaseState: null,
        pendingReleaseTicks: 0,
      };
    }
    return {
      state: nextState,
      dwellTicks: 0,
      pendingRiseState: null,
      pendingRiseTicks: 0,
      pendingReleaseState: null,
      pendingReleaseTicks: 0,
    };
  }

  if (candidate === memory.state) {
    return {
      state: memory.state,
      dwellTicks: memory.dwellTicks + deltaTicks,
      pendingRiseState: null,
      pendingRiseTicks: 0,
      pendingReleaseState: null,
      pendingReleaseTicks: 0,
    };
  }

  const pendingReleaseTicks =
    memory.pendingReleaseState === candidate
      ? memory.pendingReleaseTicks + deltaTicks
      : deltaTicks;
  const canRelease =
    memory.dwellTicks >= config.minDwellTicks[memory.state] &&
    pendingReleaseTicks >= config.releaseTicks;
  if (canRelease) {
    return {
      state: candidate,
      dwellTicks: 0,
      pendingRiseState: null,
      pendingRiseTicks: 0,
      pendingReleaseState: null,
      pendingReleaseTicks: 0,
    };
  }

  return {
    state: memory.state,
    dwellTicks: memory.dwellTicks + deltaTicks,
    pendingRiseState: null,
    pendingRiseTicks: 0,
    pendingReleaseState: candidate,
    pendingReleaseTicks,
  };
}

function targetForState(
  state: AdaptiveScoreState,
  signals: TickSignals,
  config: ResolvedMusicDirectorConfig,
): number {
  if (state === 'resolve') return config.stateIntensity.resolve;

  const base = config.stateIntensity[state];
  const energized =
    base +
    signals.activity * 0.1 +
    signals.pace * 0.07 +
    signals.approach * 0.04 +
    signals.closingPressure * 0.06 +
    signals.urgency * 0.13 +
    signals.objectiveMotion * 0.04 -
    signals.stall * 0.19;
  const [minimum, maximum] = STATE_RANGE[state];
  let target = clamp(energized, minimum, maximum);
  if (signals.hasDamage) {
    target = Math.max(target, state === 'climax' ? 0.94 : 0.82);
  }
  return clamp01(target);
}

function immediateIntensityFloor(
  signals: TickSignals,
  state: AdaptiveScoreState,
): number {
  if (signals.hasDestruction) return 1;
  if (signals.terminal) return 0.94;
  if (signals.hasDamage) return state === 'climax' ? 0.94 : 0.84;
  if (signals.overtimeStarted) return 0.76;
  if (signals.hasShot) return 0.62;
  if (signals.projectileThreat > 0.12) return 0.58;
  if (signals.contactStarted) return 0.38;
  return 0;
}

function calculateMomentum(
  previousMomentum: number,
  intensity: number,
  targetIntensity: number,
  signals: TickSignals,
  deltaTicks: number,
  config: ResolvedMusicDirectorConfig,
): number {
  if (signals.terminal) return -1;
  const retained =
    previousMomentum * Math.pow(config.momentumDecay, deltaTicks);
  const eventImpulse =
    (signals.hasDamage ? 0.42 : 0) +
    (signals.hasShot ? 0.16 : 0) +
    (signals.contactStarted ? 0.1 : 0) +
    (signals.overtimeStarted ? 0.14 : 0) +
    signals.approach * 0.08;
  const releaseDrag = signals.stall * 0.16;
  return clampSigned(
    retained + (targetIntensity - intensity) * 1.1 + eventImpulse - releaseDrag,
  );
}

function collectTriggers(signals: TickSignals): readonly MusicTrigger[] {
  const triggers: MusicTrigger[] = [];
  if (signals.overtimeStarted) triggers.push('overtime');
  if (signals.contactStarted) triggers.push('contact');
  if (signals.hasShot) triggers.push('shot');
  if (signals.hasDamage) triggers.push('damage');
  if (signals.hasDestruction) triggers.push('destruction');
  if (signals.terminal) triggers.push('resolve');
  return triggers;
}

function isTerminalTick(
  replay: ReplayModel,
  tick: ReplayTick,
  hasDestruction: boolean,
  controlUrgency: number,
): boolean {
  if (
    tick.after.objective.kind === 'frontline' &&
    tick.after.objective.winnerTeamId !== null
  ) {
    return true;
  }
  if (tick.events.some((event) => event.type === 'base-breached')) {
    return true;
  }
  if (replay.result !== null && tick.tick >= replay.result.endTick) return true;

  const limits = replay.contract.rules.limits;
  if (limits.maxTicks > 0 && tick.tick >= limits.maxTicks - 1) return true;
  if (
    tick.after.objective.kind === 'legacy' &&
    tick.after.objective.mode === 'shared-pressure' &&
    controlUrgency >= 1
  ) {
    return true;
  }

  // Replay-v1 predates the exact rules-contract field, but every released v1
  // ruleset is a duel whose destruction ends the match. Replay-v2 carries the
  // exact value and Frontline deliberately sets it to false.
  const destructionEndsMatch =
    limits.destructionEndsMatch ?? replay.sourceVersion === 1;
  return hasDestruction && destructionEndsMatch;
}

function calculateHealthUrgency(
  presentation: TickPresentation,
  legacyMaxHealth: number | null,
): number {
  const active = presentation.units.filter(
    (unit) => unit.status === 'active' && unit.actorKey !== null,
  );
  if (active.length === 0) return 1;
  return active.reduce((highest, unit) => {
    const maxHealth = legacyMaxHealth ?? unit.maxHealth;
    const urgency =
      maxHealth <= 1
        ? unit.health <= 1
          ? 1
          : 0
        : (maxHealth - unit.health) / (maxHealth - 1);
    return Math.max(highest, clamp01(urgency));
  }, 0);
}

function calculateControlUrgency(
  presentation: TickPresentation,
): number {
  return objectiveProgress(presentation);
}

function calculateLateUrgency(maxTicks: number, tick: number): number {
  if (maxTicks <= 0) return 0;
  const progress = clamp01((tick + 1) / maxTicks);
  return smoothStep(0.62, 1, progress);
}

function calculateObjectiveMotion(
  previous: DirectorTick | null,
  current: DirectorTick,
): number {
  if (previous === null) return 0;
  const before = previous.presentation.objective;
  const after = current.presentation.objective;
  if (before?.kind === 'legacy-control' && after?.kind === 'legacy-control') {
    return clamp01(
      Math.abs(after.pressure - before.pressure) /
        Math.max(1, after.limit),
    );
  }
  if (before?.kind === 'frontline' && after?.kind === 'frontline') {
    const captureMotion =
      Math.abs(after.captureProgress - before.captureProgress) /
      Math.max(1, after.captureThreshold);
    const positionMotion =
      Math.abs(after.activePositionIndex - before.activePositionIndex) /
      Math.max(1, after.positionCount - 1);
    const progressMotion = Math.abs(
      objectiveProgress(current.presentation) -
        objectiveProgress(previous.presentation),
    );
    return clamp01(
      Math.max(captureMotion, positionMotion, progressMotion),
    );
  }
  return Math.abs(
    objectiveProgress(current.presentation) -
      objectiveProgress(previous.presentation),
  );
}

function calculateObjectiveRate(
  previousRate: number,
  previous: DirectorTick | null,
  current: DirectorTick,
  deltaTicks: number,
  decay: number,
): number {
  const retained = Math.pow(decay, Math.max(1, deltaTicks));
  const advancePerTick =
    calculateObjectiveAdvance(previous, current) /
    Math.max(1, deltaTicks);
  return clamp01(
    previousRate * retained + advancePerTick * (1 - retained),
  );
}

function calculateObjectiveAdvance(
  previous: DirectorTick | null,
  current: DirectorTick,
): number {
  if (previous === null) return 0;
  const currentProgress = objectiveProgress(current.presentation);
  const previousProgress = objectiveProgress(previous.presentation);
  return clamp01(Math.max(0, currentProgress - previousProgress));
}

function calculateObjectiveImminence(
  controlUrgency: number,
  objectiveRate: number,
  maximumEtaTicks: number,
): number {
  if (
    controlUrgency <= 0 ||
    objectiveRate <= 0 ||
    maximumEtaTicks <= 0
  ) {
    return 0;
  }
  const etaTicks = Math.max(0, 1 - controlUrgency) / objectiveRate;
  return clamp01(1 - etaTicks / maximumEtaTicks);
}

function objectiveProgress(presentation: TickPresentation): number {
  const objective = presentation.objective;
  if (objective === null) return 0;
  if (objective.kind === 'legacy-control') {
    if (objective.limit <= 0) return 0;
    return clamp01(Math.abs(objective.pressure) / objective.limit);
  }
  if (objective.winnerTeamId !== null) return 1;
  const centre = (objective.positionCount - 1) / 2;
  const stepsToEdge = Math.max(1, Math.ceil(centre));
  const positionProgress =
    Math.abs(objective.activePositionIndex - centre) / (stepsToEdge + 1);
  const captureProgress =
    clamp01(
      objective.captureProgress /
        Math.max(1, objective.captureThreshold),
    ) /
    (stepsToEdge + 1);
  return clamp01(positionProgress + captureProgress);
}

function isOvertime(presentation: TickPresentation): boolean {
  return (
    presentation.objective?.kind === 'legacy-control' &&
    presentation.objective.overtime
  );
}

interface ApproachEvidence {
  distance: number | null;
  proximity: number;
  approach: number;
}

/**
 * Measure only movement between already-revealed snapshots of the same actor
 * lives. Pairing by actor key prevents a respawn or a nearest-enemy swap from
 * being mistaken for closing motion, while considering every opposing pair
 * lets a second engagement register even when the globally-nearest pair opens.
 */
function calculateApproachEvidence(
  replay: ReplayModel,
  previous: DirectorTick | null,
  current: DirectorTick,
  deltaTicks: number,
): ApproachEvidence {
  const active = current.replay.after.actors.filter(
    (actor) => actor.status === 'active',
  );
  if (active.length < 2) {
    return { distance: null, proximity: 0, approach: 0 };
  }
  const previousByActor = new Map(
    (previous?.replay.after.actors ?? [])
      .filter((actor) => actor.status === 'active')
      .map((actor) => [actor.actorKey, actor]),
  );
  let nearest = Number.POSITIVE_INFINITY;
  const closingScores: number[] = [];
  for (let left = 0; left < active.length; left += 1) {
    for (let right = left + 1; right < active.length; right += 1) {
      if (
        active[left].identity.teamId ===
        active[right].identity.teamId
      ) {
        continue;
      }
      const distance = actorDistance(active[left], active[right]);
      nearest = Math.min(nearest, distance);
      const previousLeft = previousByActor.get(active[left].actorKey);
      const previousRight = previousByActor.get(active[right].actorKey);
      if (previousLeft === undefined || previousRight === undefined) continue;
      const closedTiles =
        actorDistance(previousLeft, previousRight) - distance;
      if (closedTiles <= 0) continue;
      const closingRate = clamp01(
        closedTiles / (2 * Math.max(1, deltaTicks)),
      );
      const pairProximity = proximityLevel(replay, distance);
      // Far-away motion is not an encounter. In particular, a unit walking
      // vaguely closer on the other side of the arena must not accumulate
      // pressure before the pair enters the public proximity horizon.
      closingScores.push(closingRate * pairProximity);
    }
  }
  const distance = Number.isFinite(nearest) ? nearest : null;
  closingScores.sort((left, right) => right - left);
  return {
    distance,
    proximity: proximityLevel(replay, distance),
    approach: clamp01(
      (closingScores[0] ?? 0) + (closingScores[1] ?? 0) * 0.2,
    ),
  };
}

function actorDistance(
  left: DirectorTick['replay']['after']['actors'][number],
  right: DirectorTick['replay']['after']['actors'][number],
): number {
  return Math.max(
    Math.abs(left.position.x - right.position.x),
    Math.abs(left.position.y - right.position.y),
  );
}

function proximityLevel(replay: ReplayModel, distance: number | null): number {
  if (distance === null) return 0;
  const horizon = Math.max(
    4,
    ...replay.forms.map((form) => form.visionRange),
  );
  return clamp01(1 - (distance - 1) / horizon);
}

function positionMotionSince(
  previous: DirectorTick | null,
  current: DirectorTick,
): number {
  if (previous === null) return 0;
  const previousByActor = new Map(
    previous.replay.after.actors
      .filter((actor) => actor.status === 'active')
      .map((actor) => [actor.actorKey, actor]),
  );
  let distance = 0;
  for (const actor of current.replay.after.actors) {
    if (actor.status !== 'active') continue;
    const before = previousByActor.get(actor.actorKey);
    if (before === undefined) continue;
    distance += Math.max(
      Math.abs(actor.position.x - before.position.x),
      Math.abs(actor.position.y - before.position.y),
    );
  }
  return distance;
}

function hasEvent(tick: ReplayTick, type: string): boolean {
  return causalEvents(tick).some((event) => event.type === type);
}

function countEvent(tick: ReplayTick, type: string): number {
  return causalEvents(tick).reduce(
    (count, event) => count + (event.type === type ? 1 : 0),
    0,
  );
}

function causalEvents(tick: ReplayTick): ReplayCausalEvent[] {
  const events = new Map<string, ReplayCausalEvent>();
  for (const event of [...tick.lifecycleEvents, ...tick.events]) {
    events.set(event.eventId, event);
  }
  return [...events.values()];
}

function latestFrameAtOrBefore(
  frames: readonly AdaptiveMusicKeyframe[],
  time: number,
): AdaptiveMusicKeyframe | null {
  let low = 0;
  let high = frames.length - 1;
  let found: AdaptiveMusicKeyframe | null = null;
  while (low <= high) {
    const middle = Math.floor((low + high) / 2);
    const frame = frames[middle];
    if (frame.tick <= time) {
      found = frame;
      low = middle + 1;
    } else {
      high = middle - 1;
    }
  }
  return found;
}

function approachIntensity(
  from: number,
  target: number,
  elapsedTicks: number,
  config: ResolvedMusicDirectorConfig,
): number {
  if (elapsedTicks <= 0 || from === target) return clamp01(from);
  const rate = target >= from ? config.risePerTick : config.fallPerTick;
  return clamp01(target + (from - target) * Math.pow(1 - rate, elapsedTicks));
}

function trendForMomentum(
  momentum: number,
  threshold: number,
): MusicMomentumTrend {
  if (momentum > threshold) return 'rising';
  if (momentum < -threshold) return 'falling';
  return 'steady';
}

function counterRamp(value: number, start: number, full: number): number {
  if (value <= start) return 0;
  if (full <= start) return 1;
  return clamp01((value - start) / (full - start));
}

function smoothStep(edge0: number, edge1: number, value: number): number {
  if (edge1 <= edge0) return value >= edge1 ? 1 : 0;
  const normalized = clamp01((value - edge0) / (edge1 - edge0));
  return normalized * normalized * (3 - 2 * normalized);
}

function createInitialFrame(
  config: ResolvedMusicDirectorConfig,
): AdaptiveMusicKeyframe {
  const intensity = config.stateIntensity.sparse;
  return {
    tick: -1,
    state: 'sparse',
    intensity,
    targetIntensity: intensity,
    momentum: 0,
    trend: 'steady',
    dwellTicks: 0,
    features: {
      activity: 0,
      pace: 0,
      contact: 0,
      proximity: 0,
      approach: 0,
      closingPressure: 0,
      pursuitPressure: 0,
      combatPressure: 0,
      climaxPressure: 0,
      acuteThreat: 0,
      projectileThreat: 0,
      urgency: 0,
      healthUrgency: 0,
      controlUrgency: 0,
      lateUrgency: 0,
      objectiveMotion: 0,
      objectiveImminence: 0,
      stall: 0,
      quietTicks: 0,
      stationaryTicks: 0,
      overtime: false,
    },
    triggers: [],
  };
}

function resolveConfig(options: MusicDirectorOptions): ResolvedMusicDirectorConfig {
  const defaults = DEFAULT_MUSIC_DIRECTOR_CONFIG;
  const quietStart = nonNegativeInteger(
    options.stallQuietStartTicks,
    defaults.stallQuietStartTicks,
  );
  const stationaryStart = nonNegativeInteger(
    options.stallStationaryStartTicks,
    defaults.stallStationaryStartTicks,
  );
  return {
    maxHealth: positiveFinite(options.maxHealth, defaults.maxHealth),
    risePerTick: unitInterval(options.risePerTick, defaults.risePerTick),
    fallPerTick: unitInterval(options.fallPerTick, defaults.fallPerTick),
    combatMemoryDecay: unitInterval(
      options.combatMemoryDecay,
      defaults.combatMemoryDecay,
    ),
    contactMemoryDecay: unitInterval(
      options.contactMemoryDecay,
      defaults.contactMemoryDecay,
    ),
    closingPressureDecay: unitInterval(
      options.closingPressureDecay,
      defaults.closingPressureDecay,
    ),
    closingPressureThreshold: unitInterval(
      options.closingPressureThreshold,
      defaults.closingPressureThreshold,
    ),
    pursuitPressureDecay: unitInterval(
      options.pursuitPressureDecay,
      defaults.pursuitPressureDecay,
    ),
    combatPressureDecay: unitInterval(
      options.combatPressureDecay,
      defaults.combatPressureDecay,
    ),
    climaxPressureDecay: unitInterval(
      options.climaxPressureDecay,
      defaults.climaxPressureDecay,
    ),
    objectiveRateDecay: unitInterval(
      options.objectiveRateDecay,
      defaults.objectiveRateDecay,
    ),
    pursuitPressureThreshold: unitInterval(
      options.pursuitPressureThreshold,
      defaults.pursuitPressureThreshold,
    ),
    combatPressureThreshold: unitInterval(
      options.combatPressureThreshold,
      defaults.combatPressureThreshold,
    ),
    climaxCombatThreshold: unitInterval(
      options.climaxCombatThreshold,
      defaults.climaxCombatThreshold,
    ),
    climaxPressureThreshold: unitInterval(
      options.climaxPressureThreshold,
      defaults.climaxPressureThreshold,
    ),
    objectiveClimaxEtaTicks: positiveFinite(
      options.objectiveClimaxEtaTicks,
      defaults.objectiveClimaxEtaTicks,
    ),
    paceDecay: unitInterval(options.paceDecay, defaults.paceDecay),
    momentumDecay: unitInterval(
      options.momentumDecay,
      defaults.momentumDecay,
    ),
    trendThreshold: clamp01(
      finiteOr(options.trendThreshold, defaults.trendThreshold),
    ),
    stallQuietStartTicks: quietStart,
    stallQuietFullTicks: Math.max(
      quietStart + 1,
      nonNegativeInteger(
        options.stallQuietFullTicks,
        defaults.stallQuietFullTicks,
      ),
    ),
    stallStationaryStartTicks: stationaryStart,
    stallStationaryFullTicks: Math.max(
      stationaryStart + 1,
      nonNegativeInteger(
        options.stallStationaryFullTicks,
        defaults.stallStationaryFullTicks,
      ),
    ),
    releaseTicks: nonNegativeInteger(
      options.releaseTicks,
      defaults.releaseTicks,
    ),
    stateEntryTicks: mergeStateNumbers(
      defaults.stateEntryTicks,
      options.stateEntryTicks,
      (value) => Math.max(0, Math.round(value)),
    ),
    stateIntensity: mergeStateNumbers(
      defaults.stateIntensity,
      options.stateIntensity,
      clamp01,
    ),
    minDwellTicks: mergeStateNumbers(
      defaults.minDwellTicks,
      options.minDwellTicks,
      (value) =>
        value === Number.POSITIVE_INFINITY
          ? value
          : Math.max(0, Math.round(value)),
    ),
  };
}

function mergeStateNumbers(
  defaults: Readonly<Record<AdaptiveScoreState, number>>,
  overrides: Partial<Record<AdaptiveScoreState, number>> | undefined,
  normalize: (value: number) => number,
): Readonly<Record<AdaptiveScoreState, number>> {
  return {
    sparse: normalize(finiteStateValue(overrides?.sparse, defaults.sparse)),
    tension: normalize(finiteStateValue(overrides?.tension, defaults.tension)),
    pursuit: normalize(finiteStateValue(overrides?.pursuit, defaults.pursuit)),
    combat: normalize(finiteStateValue(overrides?.combat, defaults.combat)),
    climax: normalize(finiteStateValue(overrides?.climax, defaults.climax)),
    resolve:
      defaults.resolve === Number.POSITIVE_INFINITY &&
      overrides?.resolve === undefined
        ? defaults.resolve
        : normalize(finiteStateValue(overrides?.resolve, defaults.resolve)),
  };
}

function finiteStateValue(value: number | undefined, fallback: number): number {
  if (value === Number.POSITIVE_INFINITY) return value;
  return finiteOr(value, fallback);
}

function positiveFinite(value: number | undefined, fallback: number): number {
  const resolved = finiteOr(value, fallback);
  return resolved > 0 ? resolved : fallback;
}

function unitInterval(value: number | undefined, fallback: number): number {
  return clamp(finiteOr(value, fallback), 0.0001, 1);
}

function nonNegativeInteger(
  value: number | undefined,
  fallback: number,
): number {
  return Math.max(0, Math.round(finiteOr(value, fallback)));
}

function finiteOr(value: number | undefined, fallback: number): number {
  return value !== undefined && Number.isFinite(value) ? value : fallback;
}

function clamp01(value: number): number {
  return clamp(value, 0, 1);
}

function clampSigned(value: number): number {
  return clamp(value, -1, 1);
}

function clamp(value: number, minimum: number, maximum: number): number {
  return Math.max(minimum, Math.min(maximum, value));
}
