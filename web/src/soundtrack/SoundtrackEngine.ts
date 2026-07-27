import type {
  AdaptiveScoreState,
  SoundtrackAdaptiveSeam,
  SoundtrackManifest,
  SoundtrackRetrospectiveCue,
  SoundtrackSection,
  SoundtrackStem,
  SoundtrackTrigger,
  SoundtrackTriggerEvent,
  SoundtrackTransition,
} from './types';
import {
  lowestLatencyAdaptiveRoute,
  type LoadedSoundtrack,
} from './manifest.ts';

export interface ScoreDirection {
  state: AdaptiveScoreState;
  intensity: number;
  /** Causal intensity release target, used by finite resolution envelopes. */
  targetIntensity: number;
  /** Signed recent trajectory: rising action is positive, cooling action negative. */
  momentum: number;
}

export interface ScoreStartOptions {
  /**
   * Whole-replay timing is intentionally supplied as a live getter: cue assets
   * may take seconds to download and decode while the replay keeps advancing.
   */
  retrospective?: {
    primaryPeakSeconds: number;
    getReplaySeconds: () => number;
  };
}

interface DecodedSection {
  section: SoundtrackSection;
  buffers: Map<string, AudioBuffer>;
  durationSeconds: number;
}

interface SectionVoice {
  section: SoundtrackSection;
  bus: GainNode;
  stemGains: Map<string, GainNode>;
  /** Transition-only gain layer; ordinary intensity updates never touch it. */
  seamGains: Map<string, GainNode>;
  sources: AudioBufferSourceNode[];
  startedAt: number;
  durationSeconds: number;
  stopped: boolean;
  decisionTimer: ConstantSourceNode | null;
  prefetchedSectionId: string | null;
  successorRetryAttempts: number;
  /** Offset into a source-contiguous retrospective cue, otherwise zero. */
  sourceOffsetSeconds: number;
  retrospective: boolean;
}

interface PendingTransition {
  from: SectionVoice;
  to: SectionVoice;
  when: number;
  crossfadeSeconds: number;
  stagedSeam?: {
    retreatAt: number;
    settledAt: number;
  };
  mandatory: boolean;
  timer: ConstantSourceNode;
  stingerCooldown?: {
    sectionId: string;
    previousUntil: number;
    armedUntil: number;
  };
}

interface LoadingTransition {
  from: SectionVoice;
  transition: SoundtrackTransition;
  serial: number;
  mandatory: boolean;
}

interface RetrospectivePlayback {
  cue: SoundtrackRetrospectiveCue;
  primaryPeakSeconds: number;
  getReplaySeconds: () => number;
  voice: SectionVoice;
  resolving: boolean;
}

interface VoiceOptions {
  sourceOffsetSeconds?: number;
  suppressDecision?: boolean;
  retrospective?: boolean;
}

const MIN_START_LEAD_SECONDS = 0.06;
const MIN_HORIZONTAL_COMMIT_BARS = 2;
const TRANSITION_CURVE_STEPS = 32;
const RESOLVE_CURVE_STEPS = 64;
const STINGER_ARM_BARS = 2;
const DEFAULT_STINGER_COOLDOWN_SECONDS = 28;
const FINITE_RETRY_INITIAL_SECONDS = 0.5;
const FINITE_RETRY_MAX_SECONDS = 4;
const FINITE_RETRY_MAX_ATTEMPTS = 4;
const ACCENT_RELEASE_SETTLE_TIME_CONSTANTS = 3;
const ACCENT_CLEAR_RELEASE_SECONDS = 0.45;
const SEAM_CANCEL_RESTORE_BEATS = 0.5;
const RETROSPECTIVE_START_FADE_BARS = 1;
const RETROSPECTIVE_RESTART_FADE_SECONDS = 0.12;
const RETROSPECTIVE_RESOLVE_HOLD_BARS = 0.5;
const RETROSPECTIVE_RESOLVE_FADE_BARS = 1.5;
const MIN_RETROSPECTIVE_REMAINDER_SECONDS = 0.05;
const ENERGY_STEM_ROLES = new Set(['rhythm', 'drive', 'texture']);
const TRIGGER_IMPULSES: Readonly<
  Partial<
    Record<
      SoundtrackTrigger,
      {
        boost: number;
        holdSeconds: number;
        attackSeconds: number;
        releaseSeconds: number;
        armsStinger: boolean;
      }
    >
  >
> = {
  contact: {
    boost: 0.1,
    holdSeconds: 0.1,
    attackSeconds: 0.05,
    releaseSeconds: 0.7,
    armsStinger: false,
  },
  shot: {
    boost: 0.18,
    holdSeconds: 0.14,
    attackSeconds: 0.045,
    releaseSeconds: 0.82,
    armsStinger: false,
  },
  damage: {
    boost: 0.34,
    holdSeconds: 0.2,
    attackSeconds: 0.035,
    releaseSeconds: 1,
    armsStinger: false,
  },
  overtime: {
    boost: 0.42,
    holdSeconds: 0.32,
    attackSeconds: 0.04,
    releaseSeconds: 1.15,
    armsStinger: true,
  },
  destruction: {
    boost: 0.56,
    holdSeconds: 0.3,
    attackSeconds: 0.03,
    releaseSeconds: 1.3,
    armsStinger: true,
  },
};

/**
 * Imperative Web Audio score player. React tells it where the match is heading;
 * this class keeps every stem sample-aligned and navigates the compiled section
 * graph only at musical boundaries.
 */
export class SoundtrackEngine {
  private readonly manifest: SoundtrackManifest;
  private readonly manifestUrl: URL;
  private readonly context: AudioContext;
  private readonly master: GainNode;
  private readonly compressor: DynamicsCompressorNode;
  private readonly pauseGain: GainNode;
  private readonly sections: Map<string, SoundtrackSection>;
  private readonly stems: Map<string, SoundtrackStem>;
  private readonly onError: (error: Error) => void;
  private readonly decoded = new Map<string, Promise<DecodedSection>>();
  private readonly voices = new Set<SectionVoice>();
  private readonly fetchAbort = new AbortController();
  private active: SectionVoice | null = null;
  private pending: PendingTransition | null = null;
  private loading: LoadingTransition | null = null;
  private direction: ScoreDirection = {
    state: 'sparse',
    intensity: 0,
    targetIntensity: 0,
    momentum: 0,
  };
  private transitionSerial = 0;
  private transitionLockedUntil = 0;
  private horizontalAnchor = 0;
  private lastHorizontalCommitBar = 0;
  private queuedHorizontalState: AdaptiveScoreState | null = null;
  private horizontalTimer: ConstantSourceNode | null = null;
  private accentBoost = 0;
  private accentAttackSeconds = 0.02;
  private accentReturnAt = 0;
  private accentReleaseSeconds = 0.2;
  private accentReleaseUntil = 0;
  private accentTimer: ConstantSourceNode | null = null;
  private stingerArmedUntil = 0;
  private readonly stingerCooldownUntil = new Map<string, number>();
  private readonly receivedTriggerKeys = new Set<string>();
  private retrospectiveDecoded: Promise<DecodedSection> | null = null;
  private retrospective: RetrospectivePlayback | null = null;
  private retrospectiveSerial = 0;
  private retrospectiveResolveTimer: ConstantSourceNode | null = null;
  private paused = false;
  private disposed = false;

  constructor(
    loaded: LoadedSoundtrack,
    context: AudioContext,
    onError: (error: Error) => void = () => {},
    destination: AudioNode = context.destination,
  ) {
    this.manifest = loaded.manifest;
    this.manifestUrl = loaded.manifestUrl;
    this.context = context;
    this.onError = onError;
    this.sections = new Map(
      this.manifest.sections.map((section) => [section.id, section]),
    );
    this.stems = new Map(this.manifest.stems.map((stem) => [stem.id, stem]));

    this.compressor = context.createDynamicsCompressor();
    // The compiler's pack master preserves the authored dynamics and already
    // leaves headroom. This only catches a crossfade overshoot near full scale.
    this.compressor.threshold.value = -0.8;
    this.compressor.knee.value = 0;
    this.compressor.ratio.value = 20;
    this.compressor.attack.value = 0.001;
    this.compressor.release.value = 0.08;

    this.master = context.createGain();
    this.master.gain.value = dbToGain(this.manifest.masterGainDb);
    this.pauseGain = context.createGain();
    this.pauseGain.gain.value = 1;
    this.master.connect(this.compressor);
    this.compressor.connect(this.pauseGain);
    this.pauseGain.connect(destination);
  }

  get title(): string {
    return this.manifest.title;
  }

  async start(
    direction: ScoreDirection,
    options: ScoreStartOptions = {},
  ): Promise<void> {
    this.assertActive();
    this.direction = normalizeDirection(direction);
    const cue = this.manifest.retrospectiveCue;
    const retrospective = options.retrospective;
    if (
      cue &&
      retrospective &&
      Number.isFinite(retrospective.primaryPeakSeconds)
    ) {
      const initialOffset = this.retrospectiveOffsetFor(
        cue,
        retrospective.primaryPeakSeconds,
        safeReplaySeconds(retrospective.getReplaySeconds),
      );
      // A soundtrack can author a longer runway later. Until then, matches
      // whose known peak lies beyond this cue retain the causal/live graph.
      if (initialOffset >= 0) {
        const decoded = await this.decodeRetrospectiveCue(cue);
        if (this.disposed) return;
        this.startRetrospectivePlayback(decoded, retrospective);
        return;
      }
    }
    const entry = this.sections.get(this.manifest.entrySection);
    if (!entry) throw new Error('Soundtrack entry section is missing.');
    const decoded = await this.decodeSection(entry);
    if (this.disposed) return;
    const when = this.context.currentTime + MIN_START_LEAD_SECONDS;
    this.horizontalAnchor = when;
    this.lastHorizontalCommitBar = 0;
    this.queuedHorizontalState = null;
    const voice = this.createVoice(decoded, when, 1, this.direction.intensity);
    this.active = voice;
    if (entry.classification !== this.direction.state) {
      this.reconcileDirection();
    }
  }

  setDirection(
    direction: ScoreDirection,
    triggers: readonly SoundtrackTriggerEvent[] = [],
  ): void {
    if (this.disposed) return;
    const normalized = normalizeDirection(direction);
    const previousIntensity = this.direction.intensity;
    this.direction = this.retrospective
      ? normalized
      : {
          ...this.direction,
          intensity: normalized.intensity,
          targetIntensity: normalized.targetIntensity,
          momentum: normalized.momentum,
        };
    const stingerTrigger = this.registerTriggers(triggers);
    for (const voice of this.voices) {
      this.updateStemGains(
        voice,
        this.direction.intensity,
        this.direction.intensity > previousIntensity,
      );
    }
    if (this.retrospective) {
      this.stingerArmedUntil = 0;
      if (normalized.state === 'resolve') {
        this.beginRetrospectiveResolve();
      }
      return;
    }
    this.requestHorizontalState(normalized.state);
    if (stingerTrigger && this.direction.state === normalized.state) {
      this.tryScheduleArmedStinger();
    }
  }

  /**
   * Explicit replay seeks start a new presentation segment. Forget event
   * impulses and the per-bar state latch, but never strand a finite cue that
   * already requires a successor.
   */
  resetForDiscontinuity(): void {
    if (this.disposed) return;
    if (this.retrospective) {
      this.receivedTriggerKeys.clear();
      this.stingerArmedUntil = 0;
      this.stingerCooldownUntil.clear();
      this.clearAccentEnvelope();
      this.queuedHorizontalState = null;
      this.cancelHorizontalTimer();
      this.cancelRetrospectiveResolve();
      void this.restartRetrospectivePlayback().catch((reason: unknown) =>
        this.reportError(reason),
      );
      return;
    }
    if (
      this.pending &&
      !this.pending.mandatory &&
      this.context.currentTime + 0.005 < this.pending.when
    ) {
      this.cancelPendingTransition();
    }
    if (this.loading && !this.loading.mandatory) {
      const from = this.loading.from;
      this.transitionSerial += 1;
      this.loading = null;
      this.ensureFiniteSuccessor(from);
      this.pruneDecoded();
    }
    this.receivedTriggerKeys.clear();
    this.stingerArmedUntil = 0;
    this.stingerCooldownUntil.clear();
    this.clearAccentEnvelope();
    this.queuedHorizontalState = null;
    this.cancelHorizontalTimer();
    this.lastHorizontalCommitBar =
      this.currentHorizontalBar() - MIN_HORIZONTAL_COMMIT_BARS;
  }

  private registerTriggers(
    triggers: readonly SoundtrackTriggerEvent[],
  ): boolean {
    let strongest:
      | (typeof TRIGGER_IMPULSES)[Exclude<SoundtrackTrigger, 'resolve'>]
      | undefined;
    let armsStinger = false;
    for (const trigger of triggers) {
      const key = `${trigger.sourceTick}:${trigger.type}`;
      if (this.receivedTriggerKeys.has(key)) continue;
      this.receivedTriggerKeys.add(key);
      const impulse = TRIGGER_IMPULSES[trigger.type];
      if (!impulse) continue;
      if (!strongest || impulse.boost > strongest.boost) strongest = impulse;
      armsStinger ||= impulse.armsStinger;
    }

    if (strongest) this.beginAccentImpulse(strongest);
    if (armsStinger) {
      const barSeconds = this.manifest.barFrames / this.manifest.sampleRate;
      this.stingerArmedUntil = Math.max(
        this.stingerArmedUntil,
        this.context.currentTime + STINGER_ARM_BARS * barSeconds,
      );
    }
    return armsStinger;
  }

  private beginAccentImpulse(
    impulse: NonNullable<(typeof TRIGGER_IMPULSES)[SoundtrackTrigger]>,
  ): void {
    const now = this.context.currentTime;
    this.accentReleaseUntil = 0;
    const continuing = now < this.accentReturnAt;
    this.accentBoost = continuing
      ? Math.max(this.accentBoost, impulse.boost)
      : impulse.boost;
    this.accentAttackSeconds = continuing
      ? Math.min(this.accentAttackSeconds, impulse.attackSeconds)
      : impulse.attackSeconds;
    this.accentReleaseSeconds = continuing
      ? Math.max(this.accentReleaseSeconds, impulse.releaseSeconds)
      : impulse.releaseSeconds;
    this.accentReturnAt = Math.max(
      continuing ? this.accentReturnAt : now,
      now + impulse.holdSeconds,
    );
    if (this.accentTimer) {
      const previous = this.accentTimer;
      previous.onended = () => previous.disconnect();
      this.accentTimer = null;
    }
    let timer: ConstantSourceNode;
    timer = this.atAudioTime(this.accentReturnAt, () => {
      if (this.accentTimer !== timer || this.disposed) return;
      this.accentTimer = null;
      this.accentBoost = 0;
      this.accentReturnAt = 0;
      this.accentReleaseUntil =
        this.context.currentTime +
        this.accentReleaseSeconds * ACCENT_RELEASE_SETTLE_TIME_CONSTANTS;
      for (const voice of this.voices) {
        this.updateStemGains(
          voice,
          this.direction.intensity,
          false,
          this.accentReleaseSeconds,
        );
      }
    });
    this.accentTimer = timer;
  }

  private clearAccentEnvelope(): void {
    const hadAccent =
      this.accentBoost > 0 ||
      this.context.currentTime < this.accentReturnAt;
    if (this.accentTimer) {
      const timer = this.accentTimer;
      timer.onended = () => timer.disconnect();
      this.accentTimer = null;
    }
    this.accentBoost = 0;
    this.accentReturnAt = 0;
    if (!hadAccent) {
      this.accentReleaseUntil = 0;
      return;
    }
    this.accentReleaseSeconds = ACCENT_CLEAR_RELEASE_SECONDS;
    this.accentReleaseUntil =
      this.context.currentTime +
      ACCENT_CLEAR_RELEASE_SECONDS *
        ACCENT_RELEASE_SETTLE_TIME_CONSTANTS;
    for (const voice of this.voices) {
      this.updateStemGains(
        voice,
        this.direction.intensity,
        false,
        ACCENT_CLEAR_RELEASE_SECONDS,
      );
    }
  }

  private currentHorizontalBar(): number {
    const barSeconds = this.manifest.barFrames / this.manifest.sampleRate;
    return Math.max(
      0,
      Math.floor(
        (this.context.currentTime - this.horizontalAnchor) / barSeconds +
          Number.EPSILON,
      ),
    );
  }

  private requestHorizontalState(state: AdaptiveScoreState): void {
    if (state === this.direction.state) {
      this.queuedHorizontalState = null;
      this.cancelHorizontalTimer();
      this.reconcileDirection();
      return;
    }
    const bar = this.currentHorizontalBar();
    if (state === 'resolve') {
      this.queuedHorizontalState = null;
      this.cancelHorizontalTimer();
      this.lastHorizontalCommitBar = bar;
      this.commitHorizontalState(state);
      return;
    }
    const nextCommitBar =
      this.lastHorizontalCommitBar + MIN_HORIZONTAL_COMMIT_BARS;
    if (bar >= nextCommitBar) {
      this.queuedHorizontalState = null;
      this.cancelHorizontalTimer();
      this.lastHorizontalCommitBar = bar;
      this.commitHorizontalState(state);
      return;
    }
    this.queuedHorizontalState = state;
    if (this.horizontalTimer) return;
    const barSeconds = this.manifest.barFrames / this.manifest.sampleRate;
    const boundary =
      this.horizontalAnchor + Math.max(bar + 1, nextCommitBar) * barSeconds;
    let timer: ConstantSourceNode;
    timer = this.atAudioTime(boundary, () => {
      if (this.horizontalTimer !== timer || this.disposed) return;
      this.horizontalTimer = null;
      const queued = this.queuedHorizontalState;
      this.queuedHorizontalState = null;
      if (queued !== null) {
        this.lastHorizontalCommitBar = this.currentHorizontalBar();
        this.commitHorizontalState(queued);
      }
    });
    this.horizontalTimer = timer;
  }

  private cancelHorizontalTimer(): void {
    if (!this.horizontalTimer) return;
    const timer = this.horizontalTimer;
    timer.onended = () => timer.disconnect();
    this.horizontalTimer = null;
  }

  private tryScheduleArmedStinger(): boolean {
    const active = this.active;
    const now = this.context.currentTime;
    if (
      this.disposed ||
      !active ||
      active.stopped ||
      this.pending ||
      this.loading ||
      this.direction.state === 'resolve' ||
      now < this.transitionLockedUntil
    ) {
      return false;
    }
    if (now >= this.stingerArmedUntil) {
      this.stingerArmedUntil = 0;
      return false;
    }
    const transition = transitionsFrom(
      this.manifest.transitions,
      active.section.id,
    )
      .filter((candidate) => {
        const destination = this.sections.get(candidate.to);
        return (
          destination?.role === 'stinger' &&
          destination.classification === this.direction.state &&
          (this.stingerCooldownUntil.get(destination.id) ?? 0) <= now
        );
      })
      .sort((left, right) => this.compareTransitions(left, right))[0];
    if (!transition) return false;
    void this.followTransition(active, transition, false, false, true).catch(
      (reason: unknown) => this.reportError(reason),
    );
    return true;
  }

  private commitHorizontalState(state: AdaptiveScoreState): void {
    const previousState = this.direction.state;
    if (previousState === state) return;
    this.direction = { ...this.direction, state };
    if (previousState !== this.direction.state) {
      const replacement = this.active
        ? this.nextTransition(this.active.section.id, this.direction.state)
        : null;
      const replacementMatches = replacement
        ? this.sections.get(replacement.to)?.classification ===
          this.direction.state
        : false;
      if (
        this.pending?.mandatory &&
        this.pending.to.section.classification !== this.direction.state &&
        this.context.currentTime + 0.005 < this.pending.when &&
        (this.direction.state === 'resolve' || replacementMatches)
      ) {
        // A terminal result always outranks a previously chosen finite exit.
        // Other late state changes may retarget only when the authored graph
        // offers a direct destination, so the finite cue never loses its exit.
        this.cancelPendingTransition();
      }
      if (
        this.loading?.mandatory &&
        this.sections.get(this.loading.transition.to)?.classification !==
          this.direction.state &&
        (this.direction.state === 'resolve' || replacementMatches)
      ) {
        const from = this.loading.from;
        this.transitionSerial += 1;
        this.loading = null;
        this.ensureFiniteSuccessor(from);
      }
    }
    if (
      this.pending &&
      !this.pending.mandatory &&
      this.pending.to.section.classification !== this.direction.state &&
      this.context.currentTime + 0.005 < this.pending.when
    ) {
      this.cancelPendingTransition();
    }
    if (
      this.loading &&
      !this.loading.mandatory &&
      previousState !== this.direction.state
    ) {
      const expected = this.active
        ? this.nextTransition(this.active.section.id, this.direction.state)
        : null;
      if (
        this.active?.section.classification === this.direction.state ||
        expected?.to !== this.loading.transition.to
      ) {
        const from = this.loading.from;
        this.transitionSerial += 1;
        this.loading = null;
        this.ensureFiniteSuccessor(from);
      }
    }
    if (this.tryScheduleArmedStinger()) return;
    if (
      this.active &&
      !this.pending &&
      !this.loading &&
      this.active.section.classification !== this.direction.state &&
      this.context.currentTime >= this.transitionLockedUntil
    ) {
      this.reconcileDirection();
    }
  }

  setVolume(volume: number): void {
    if (this.disposed) return;
    const normalized = Math.max(0, Math.min(1, volume));
    const target = normalized * dbToGain(this.manifest.masterGainDb);
    this.master.gain.setTargetAtTime(target, this.context.currentTime, 0.035);
  }

  async setPaused(paused: boolean): Promise<void> {
    if (this.disposed) return;
    const changed = this.paused !== paused;
    this.paused = paused;
    if (!paused && changed && this.retrospective) {
      await this.restartRetrospectivePlayback();
      if (this.disposed) return;
    }
    const now = this.context.currentTime;
    const current = this.pauseGain.gain.value;
    this.pauseGain.gain.cancelScheduledValues(now);
    this.pauseGain.gain.setValueAtTime(current, now);
    this.pauseGain.gain.setTargetAtTime(paused ? 0 : 1, now, 0.018);
  }

  async dispose(): Promise<void> {
    if (this.disposed) return;
    this.disposed = true;
    this.transitionSerial += 1;
    this.retrospectiveSerial += 1;
    this.fetchAbort.abort();
    this.cancelHorizontalTimer();
    if (this.retrospectiveResolveTimer) {
      const timer = this.retrospectiveResolveTimer;
      timer.onended = () => timer.disconnect();
      this.retrospectiveResolveTimer = null;
    }
    if (this.accentTimer) {
      const timer = this.accentTimer;
      timer.onended = () => timer.disconnect();
      this.accentTimer = null;
    }
    this.pending = null;
    for (const voice of this.voices) this.stopVoice(voice, this.context.currentTime);
    this.voices.clear();
    this.decoded.clear();
    this.retrospectiveDecoded = null;
    this.retrospective = null;
    this.master.disconnect();
    this.compressor.disconnect();
    this.pauseGain.disconnect();
  }

  private async moveToward(target: AdaptiveScoreState): Promise<void> {
    const active = this.active;
    if (!active || this.pending || this.loading) return;
    const transition = this.nextTransition(active.section.id, target);
    if (!transition) return;
    await this.followTransition(active, transition);
  }

  private async followTransition(
    active: SectionVoice,
    transition: SoundtrackTransition,
    mandatory = false,
    rotation = false,
    stingerAuthorized = false,
  ): Promise<void> {
    const destination = this.sections.get(transition.to);
    if (!destination) return;

    const serial = ++this.transitionSerial;
    this.loading = { from: active, transition, serial, mandatory };
    let decoded: DecodedSection;
    try {
      decoded = await this.decodeSection(destination);
    } catch (reason: unknown) {
      if (
        this.disposed ||
        serial !== this.transitionSerial ||
        this.active !== active
      ) {
        this.pruneDecoded();
        this.reconcileDirection();
        return;
      }
      throw reason;
    } finally {
      if (this.loading?.serial === serial) this.loading = null;
    }
    const currentTransition = rotation
      ? this.nextRotationTransition(active)
      : this.nextTransition(active.section.id, this.direction.state);
    const stingerStillAuthorized =
      destination.role === 'stinger' &&
      destination.classification === this.direction.state &&
      this.context.currentTime < this.stingerArmedUntil &&
      (this.stingerCooldownUntil.get(destination.id) ?? 0) <=
        this.context.currentTime;
    const rejected =
      this.disposed ||
      serial !== this.transitionSerial ||
      this.active !== active ||
      this.pending ||
      (!mandatory &&
        (stingerAuthorized
          ? !stingerStillAuthorized
          : rotation
            ? active.section.classification !== this.direction.state ||
              currentTransition?.to !== transition.to
            : active.section.classification === this.direction.state ||
              currentTransition?.to !== transition.to));
    if (rejected) {
      if (
        rotation &&
        !this.disposed &&
        this.active === active &&
        active.section.loopable
      ) {
        this.scheduleNextLoopDecision(active);
      }
      this.pruneDecoded();
      this.reconcileDirection();
      return;
    }

    const barSeconds = this.manifest.barFrames / this.manifest.sampleRate;
    const quantum = Math.max(1, transition.quantizeBars) * barSeconds;
    const elapsed = Math.max(0, this.context.currentTime - active.startedAt);
    const authoredCrossfadeSeconds = Math.min(
      transition.crossfadeBars * barSeconds,
      decoded.durationSeconds / 2,
      active.durationSeconds / 2,
    );
    const adaptiveSeam = this.stagedSeamFor(
      active,
      destination,
      transition,
    );
    const crossfadeSeconds = adaptiveSeam
      ? Math.min(
          adaptiveSeam.overlapBars * barSeconds,
          authoredCrossfadeSeconds,
        )
      : authoredCrossfadeSeconds;
    let when: number;
    if (transition.timing === 'section-end') {
      if (adaptiveSeam) {
        const cycles = Math.max(
          1,
          Math.ceil(
            (elapsed + MIN_START_LEAD_SECONDS) /
              active.durationSeconds,
          ),
        );
        // A staged edit treats the natural section end as the handoff
        // boundary. Pulling the destination head earlier would undo the
        // retreat by layering two unrelated phrases before the edit.
        when = active.startedAt + cycles * active.durationSeconds;
      } else {
        const cycles = active.section.loopable
          ? Math.max(
              1,
              Math.ceil(
                (elapsed + MIN_START_LEAD_SECONDS + crossfadeSeconds) /
                  active.durationSeconds,
              ),
            )
          : 1;
        when =
          active.startedAt +
          cycles * active.durationSeconds -
          crossfadeSeconds;
      }
      when = Math.max(this.context.currentTime + MIN_START_LEAD_SECONDS, when);
    } else {
      const boundary = Math.ceil(
        (elapsed + MIN_START_LEAD_SECONDS) / quantum,
      );
      when = active.startedAt + Math.max(1, boundary) * quantum;
      if (!active.section.loopable) {
        // A slow decode must not push a finite cue into a nonexistent next bar.
        // Once its natural end has passed, start promptly rather than waiting
        // for a quantum beyond the source buffer.
        when = Math.min(
          when,
          active.startedAt + active.durationSeconds - crossfadeSeconds,
        );
        when = Math.max(this.context.currentTime + MIN_START_LEAD_SECONDS, when);
      }
    }
    if (rotation) {
      const minimumBars =
        active.section.repeat?.minimumBars ?? active.section.barCount;
      const minimumCycles = Math.max(
        1,
        Math.ceil(minimumBars / active.section.barCount),
      );
      const cycle = Math.max(
        minimumCycles,
        Math.ceil(
          (elapsed + MIN_START_LEAD_SECONDS) /
            active.durationSeconds,
        ),
      );
      // Same-state variety changes begin at a full section-cycle boundary;
      // their prefetch timer must not let a fast decode rotate two bars early.
      // The destination downbeat starts on that boundary; the old loop remains
      // available underneath the crossfade instead of pulling the new phrase
      // ahead of the musical grid.
      when = Math.max(
        this.context.currentTime + MIN_START_LEAD_SECONDS,
        active.startedAt + cycle * active.durationSeconds,
      );
    }

    let stagedSeam:
      | {
          retreatAt: number;
          settledAt: number;
        }
      | undefined;
    if (adaptiveSeam) {
      const desiredRetreatSeconds =
        adaptiveSeam.retreatBars * barSeconds;
      if (destination.role === 'hold') {
        const available = when - this.context.currentTime;
        if (available + 1e-6 < desiredRetreatSeconds) {
          const boundaryStep =
            rotation || transition.timing === 'section-end'
              ? active.durationSeconds
              : quantum;
          when +=
            Math.ceil(
              (desiredRetreatSeconds - available) / boundaryStep,
            ) * boundaryStep;
        }
      }
      // Resolve should remain prompt. It uses as much of the requested retreat
      // as the already-selected boundary leaves available, whereas a stable
      // hold-to-hold edit may wait for the next boundary to get the full bar.
      const retreatSeconds =
        destination.role === 'resolve'
          ? Math.min(
              desiredRetreatSeconds,
              Math.max(0, when - this.context.currentTime),
            )
          : desiredRetreatSeconds;
      stagedSeam = {
        retreatAt: when - retreatSeconds,
        settledAt:
          when +
          Math.max(
            crossfadeSeconds,
            adaptiveSeam.riseBars * barSeconds,
          ),
      };
    }

    const next = this.createVoice(
      decoded,
      when,
      crossfadeSeconds > 0 ? 0 : 1,
      this.direction.intensity,
      adaptiveSeam ? 0 : 1,
    );
    if (adaptiveSeam && stagedSeam) {
      this.applyEnergySeamFade(
        active,
        stagedSeam.retreatAt,
        when - stagedSeam.retreatAt,
        false,
      );
      this.applyEnergySeamFade(
        next,
        when,
        adaptiveSeam.riseBars * barSeconds,
        true,
      );
      applyLinearFade(next.bus.gain, when, crossfadeSeconds, true);
      applyLinearFade(active.bus.gain, when, crossfadeSeconds, false);
    } else if (crossfadeSeconds > 0) {
      applyEqualPowerFade(next.bus.gain, when, crossfadeSeconds, true);
      applyEqualPowerFade(active.bus.gain, when, crossfadeSeconds, false);
    } else {
      active.bus.gain.setValueAtTime(0, when);
    }
    const timer = this.atAudioTime(when, () => {
      const pending = this.pending;
      if (
        this.disposed ||
        !pending ||
        pending.from !== active ||
        pending.to !== next
      ) {
        return;
      }
      if (!mandatory) {
        const desiredState =
          this.queuedHorizontalState ?? this.direction.state;
        const stillCurrent = stingerAuthorized
          ? destination.role === 'stinger' &&
            destination.classification === desiredState
          : rotation
            ? active.section.classification === desiredState &&
              this.nextRotationTransition(active)?.to === transition.to
            : this.nextTransition(active.section.id, desiredState)?.to ===
              transition.to;
        if (!stillCurrent) {
          this.cancelPendingTransition();
          return;
        }
      }
      this.pending = null;
      this.active = next;
      const settledAt = stagedSeam?.settledAt ?? when + crossfadeSeconds;
      this.transitionLockedUntil = settledAt;
      this.stopVoice(active, when + crossfadeSeconds + 0.002);
      this.atAudioTime(settledAt + 0.004, () => {
        if (this.disposed) return;
        this.transitionLockedUntil = 0;
        this.pruneDecoded();
        this.reconcileDirection();
      });
    });
    const stingerCooldown = stingerAuthorized
      ? {
          sectionId: destination.id,
          previousUntil: this.stingerCooldownUntil.get(destination.id) ?? 0,
          armedUntil: this.stingerArmedUntil,
        }
      : undefined;
    if (stingerCooldown) {
      this.stingerCooldownUntil.set(
        destination.id,
        when +
          (destination.cooldownSeconds ?? DEFAULT_STINGER_COOLDOWN_SECONDS),
      );
      this.stingerArmedUntil = 0;
    }
    this.pending = {
      from: active,
      to: next,
      when,
      crossfadeSeconds,
      stagedSeam,
      mandatory,
      timer,
      stingerCooldown,
    };
    if (mandatory) active.successorRetryAttempts = 0;
  }

  private reconcileDirection(): void {
    if (
      this.disposed ||
      this.retrospective ||
      this.pending ||
      this.loading ||
      !this.active ||
      this.context.currentTime < this.transitionLockedUntil
    ) {
      return;
    }
    // A distinctive trigger can arrive while a matching transition is loading,
    // pending, or crossfading. Re-check its short-lived arm before concluding
    // that an already-matching active classification needs no work.
    if (this.tryScheduleArmedStinger()) return;
    if (this.active.section.classification === this.direction.state) return;
    void this.moveToward(this.direction.state).catch((reason: unknown) =>
      this.reportError(reason),
    );
  }

  private cancelPendingTransition(): void {
    const pending = this.pending;
    if (!pending) return;
    this.transitionSerial += 1;
    this.pending = null;
    if (pending.stingerCooldown) {
      this.stingerCooldownUntil.set(
        pending.stingerCooldown.sectionId,
        pending.stingerCooldown.previousUntil,
      );
      if (pending.stingerCooldown.armedUntil > this.context.currentTime) {
        this.stingerArmedUntil = Math.max(
          this.stingerArmedUntil,
          pending.stingerCooldown.armedUntil,
        );
      }
    }
    pending.timer.onended = () => pending.timer.disconnect();
    const now = this.context.currentTime;
    pending.from.bus.gain.cancelScheduledValues(now);
    pending.from.bus.gain.setValueAtTime(1, now);
    pending.to.bus.gain.cancelScheduledValues(now);
    pending.to.bus.gain.setValueAtTime(0, now);
    if (pending.stagedSeam) {
      const beatSeconds =
        this.manifest.barFrames /
        this.manifest.sampleRate /
        this.manifest.beatsPerBar;
      for (const [stemId, gain] of pending.from.seamGains) {
        if (!this.isEnergyStem(stemId)) continue;
        holdAutomationAtTime(gain.gain, now);
        gain.gain.setTargetAtTime(
          1,
          now,
          Math.max(0.05, beatSeconds * SEAM_CANCEL_RESTORE_BEATS),
        );
      }
    }
    this.stopVoice(pending.to, now + 0.002);
    if (pending.from.section.loopable) {
      this.scheduleNextLoopDecision(pending.from);
    } else {
      this.ensureFiniteSuccessor(pending.from);
    }
    this.pruneDecoded();
  }

  private nextTransition(
    from: string,
    target: AdaptiveScoreState,
  ): SoundtrackTransition | null {
    const route = lowestLatencyAdaptiveRoute(this.manifest, from, target);
    const nextId = route?.path[1];
    if (!nextId) return null;
    return (
      transitionsFrom(this.manifest.transitions, from)
        .filter((transition) => transition.to === nextId)
        .sort((left, right) => this.compareTransitions(left, right))[0] ?? null
    );
  }

  private nextRotationTransition(
    voice: SectionVoice,
  ): SoundtrackTransition | null {
    return (
      transitionsFrom(this.manifest.transitions, voice.section.id)
        .filter((transition) => {
          const destination = this.sections.get(transition.to);
          return (
            transition.to !== voice.section.id &&
            transition.timing === 'next-quantum' &&
            destination?.role === 'hold' &&
            destination?.classification === voice.section.classification
          );
        })
        .sort((left, right) => this.compareTransitions(left, right))[0] ?? null
    );
  }

  private stagedSeamFor(
    active: SectionVoice,
    destination: SoundtrackSection,
    transition: SoundtrackTransition,
  ): SoundtrackAdaptiveSeam | null {
    const seam = this.manifest.adaptiveSeam;
    if (
      seam?.strategy !== 'staged' ||
      seam.curve !== 'linear' ||
      transition.crossfadeBars <= 0 ||
      active.section.role !== 'hold' ||
      (destination.role !== 'hold' && destination.role !== 'resolve')
    ) {
      return null;
    }
    return seam;
  }

  private decodeRetrospectiveCue(
    cue: SoundtrackRetrospectiveCue,
  ): Promise<DecodedSection> {
    if (!this.retrospectiveDecoded) {
      const section: SoundtrackSection = {
        id: `retrospective-${cue.id}`,
        label: `Retrospective ${cue.id}`,
        classification: 'climax',
        role: 'bridge',
        startBar: cue.startBar,
        barCount: cue.barCount,
        durationSeconds: cue.durationSeconds,
        energy: 1,
        loopable: false,
        files: cue.files,
      };
      const pending = this.fetchAndDecode(section);
      this.retrospectiveDecoded = pending;
      pending.catch(() => {
        if (this.retrospectiveDecoded === pending) {
          this.retrospectiveDecoded = null;
        }
      });
    }
    return this.retrospectiveDecoded;
  }

  private startRetrospectivePlayback(
    decoded: DecodedSection,
    timing: NonNullable<ScoreStartOptions['retrospective']>,
  ): void {
    const { when, offset } = this.retrospectiveStartPoint(
      this.manifest.retrospectiveCue!,
      timing.primaryPeakSeconds,
      safeReplaySeconds(timing.getReplaySeconds),
    );
    const voice = this.createVoice(
      decoded,
      when,
      0,
      this.direction.intensity,
      1,
      {
        sourceOffsetSeconds: offset,
        suppressDecision: true,
        retrospective: true,
      },
    );
    const barSeconds = this.manifest.barFrames / this.manifest.sampleRate;
    applyLinearFade(
      voice.bus.gain,
      when,
      RETROSPECTIVE_START_FADE_BARS * barSeconds,
      true,
    );
    this.horizontalAnchor = when;
    this.lastHorizontalCommitBar = 0;
    this.queuedHorizontalState = null;
    this.active = voice;
    this.retrospective = {
      cue: this.manifest.retrospectiveCue!,
      primaryPeakSeconds: timing.primaryPeakSeconds,
      getReplaySeconds: timing.getReplaySeconds,
      voice,
      resolving: false,
    };
    if (this.direction.state === 'resolve') {
      this.beginRetrospectiveResolve();
    }
  }

  private async restartRetrospectivePlayback(): Promise<void> {
    const playback = this.retrospective;
    if (!playback) return;
    const serial = ++this.retrospectiveSerial;
    const resumeResolve = playback.resolving;
    this.cancelRetrospectiveResolve();
    const decoded = await this.decodeRetrospectiveCue(playback.cue);
    if (
      this.disposed ||
      serial !== this.retrospectiveSerial ||
      this.retrospective !== playback
    ) {
      return;
    }
    const { when, offset } = this.retrospectiveStartPoint(
      playback.cue,
      playback.primaryPeakSeconds,
      safeReplaySeconds(playback.getReplaySeconds),
    );
    const previous = playback.voice;
    const next = this.createVoice(
      decoded,
      when,
      0,
      this.direction.intensity,
      1,
      {
        sourceOffsetSeconds: offset,
        suppressDecision: true,
        retrospective: true,
      },
    );
    applyLinearFade(
      next.bus.gain,
      when,
      RETROSPECTIVE_RESTART_FADE_SECONDS,
      true,
    );
    holdAutomationAtTime(previous.bus.gain, when);
    applyLinearFade(
      previous.bus.gain,
      when,
      RETROSPECTIVE_RESTART_FADE_SECONDS,
      false,
    );
    this.stopVoice(
      previous,
      when + RETROSPECTIVE_RESTART_FADE_SECONDS + 0.002,
    );
    playback.voice = next;
    this.active = next;
    if (resumeResolve || this.direction.state === 'resolve') {
      this.beginRetrospectiveResolve();
    }
  }

  private beginRetrospectiveResolve(): void {
    const playback = this.retrospective;
    if (!playback || playback.resolving || playback.voice.stopped) return;
    playback.resolving = true;
    const voice = playback.voice;
    const barSeconds = this.manifest.barFrames / this.manifest.sampleRate;
    const fadeAt = Math.max(
      this.context.currentTime +
        RETROSPECTIVE_RESOLVE_HOLD_BARS * barSeconds,
      voice.startedAt + RETROSPECTIVE_START_FADE_BARS * barSeconds,
    );
    const fadeSeconds = RETROSPECTIVE_RESOLVE_FADE_BARS * barSeconds;
    applyLinearFade(voice.bus.gain, fadeAt, fadeSeconds, false);
    let timer: ConstantSourceNode;
    timer = this.atAudioTime(fadeAt + fadeSeconds + 0.002, () => {
      if (
        this.retrospectiveResolveTimer !== timer ||
        this.disposed ||
        this.retrospective !== playback ||
        playback.voice !== voice ||
        !playback.resolving
      ) {
        return;
      }
      this.retrospectiveResolveTimer = null;
      this.stopVoice(voice, this.context.currentTime + 0.002);
    });
    this.retrospectiveResolveTimer = timer;
  }

  private cancelRetrospectiveResolve(): void {
    const playback = this.retrospective;
    if (this.retrospectiveResolveTimer) {
      const timer = this.retrospectiveResolveTimer;
      timer.onended = () => timer.disconnect();
      this.retrospectiveResolveTimer = null;
    }
    if (!playback) return;
    playback.resolving = false;
    if (playback.voice.stopped) return;
    const now = this.context.currentTime;
    holdAutomationAtTime(playback.voice.bus.gain, now);
    playback.voice.bus.gain.setTargetAtTime(1, now, 0.04);
  }

  private retrospectiveOffsetFor(
    cue: SoundtrackRetrospectiveCue,
    primaryPeakSeconds: number,
    replaySeconds: number,
  ): number {
    const barSeconds = this.manifest.barFrames / this.manifest.sampleRate;
    return (
      cue.anchorBar * barSeconds -
      (primaryPeakSeconds - replaySeconds)
    );
  }

  private retrospectiveStartPoint(
    cue: SoundtrackRetrospectiveCue,
    primaryPeakSeconds: number,
    replaySeconds: number,
  ): { when: number; offset: number } {
    const now = this.context.currentTime;
    const baseOffset = this.retrospectiveOffsetFor(
      cue,
      primaryPeakSeconds,
      replaySeconds,
    );
    const beatSeconds =
      this.manifest.barFrames /
      this.manifest.sampleRate /
      this.manifest.beatsPerBar;
    const earliestOffset = baseOffset + MIN_START_LEAD_SECONDS;
    const beatOffset =
      Math.ceil((earliestOffset - 1e-9) / beatSeconds) * beatSeconds;
    const offset = Math.max(
      0,
      Math.min(
        cue.durationSeconds - MIN_RETROSPECTIVE_REMAINDER_SECONDS,
        beatOffset,
      ),
    );
    return {
      // Waiting for the matching source beat preserves exact highlight
      // alignment without fading up halfway through a beat or sustained note.
      when:
        now +
        Math.max(
          MIN_START_LEAD_SECONDS,
          offset - baseOffset,
        ),
      offset,
    };
  }

  private async decodeSection(section: SoundtrackSection): Promise<DecodedSection> {
    let pending = this.decoded.get(section.id);
    if (!pending) {
      pending = this.fetchAndDecode(section);
      this.decoded.set(section.id, pending);
      pending.catch(() => {
        if (this.decoded.get(section.id) === pending) {
          this.decoded.delete(section.id);
        }
      });
    }
    return pending;
  }

  private async fetchAndDecode(section: SoundtrackSection): Promise<DecodedSection> {
    const entries = await Promise.all(
      Object.entries(section.files).map(async ([stemId, path]) => {
        const url = new URL(path, this.manifestUrl);
        const response = await fetch(url, {
          cache: 'force-cache',
          signal: this.fetchAbort.signal,
        });
        if (!response.ok) {
          throw new Error(`Could not load soundtrack asset ${url.pathname}.`);
        }
        const bytes = await response.arrayBuffer();
        const buffer = await this.context.decodeAudioData(bytes);
        return [stemId, buffer] as const;
      }),
    );
    const buffers = new Map(entries);
    const durationSeconds =
      (section.barCount * this.manifest.barFrames) / this.manifest.sampleRate;
    for (const [, buffer] of entries) {
      const expectedFrames = Math.round(durationSeconds * buffer.sampleRate);
      if (buffer.length !== expectedFrames) {
        throw new Error(
          `Section "${section.id}" decoded to ${buffer.length} frames; expected ${expectedFrames}.`,
        );
      }
    }
    return { section, buffers, durationSeconds };
  }

  private createVoice(
    decoded: DecodedSection,
    when: number,
    initialBusGain: number,
    intensity: number,
    initialEnergySeamGain = 1,
    options: VoiceOptions = {},
  ): SectionVoice {
    const sourceOffsetSeconds = Math.max(
      0,
      Math.min(
        decoded.durationSeconds - MIN_RETROSPECTIVE_REMAINDER_SECONDS,
        options.sourceOffsetSeconds ?? 0,
      ),
    );
    const bus = this.context.createGain();
    bus.gain.setValueAtTime(initialBusGain, when);
    bus.connect(this.master);
    const voice: SectionVoice = {
      section: decoded.section,
      bus,
      stemGains: new Map(),
      seamGains: new Map(),
      sources: [],
      startedAt: when,
      durationSeconds: decoded.durationSeconds,
      stopped: false,
      decisionTimer: null,
      prefetchedSectionId: null,
      successorRetryAttempts: 0,
      sourceOffsetSeconds,
      retrospective: options.retrospective === true,
    };

    for (const [stemId, buffer] of decoded.buffers) {
      const source = this.context.createBufferSource();
      const gain = this.context.createGain();
      const seamGain = this.context.createGain();
      gain.gain.value = this.targetStemGain(
        decoded.section,
        stemId,
        this.effectiveStemIntensity(stemId, intensity),
      ) * this.stemAccentMultiplier(stemId);
      seamGain.gain.value = this.isEnergyStem(stemId)
        ? initialEnergySeamGain
        : 1;
      if (decoded.section.role === 'resolve') {
        applyResolveEnvelope(
          gain.gain,
          when,
          decoded.durationSeconds,
          (progress) =>
            this.targetStemGain(
              decoded.section,
              stemId,
              intensity +
                (this.direction.targetIntensity - intensity) * progress,
            ),
        );
      }
      source.buffer = buffer;
      source.loop = decoded.section.loopable;
      source.loopEnd = decoded.durationSeconds;
      source.connect(gain);
      gain.connect(seamGain);
      seamGain.connect(bus);
      if (sourceOffsetSeconds > 0) {
        source.start(when, sourceOffsetSeconds);
      } else {
        source.start(when);
      }
      voice.sources.push(source);
      voice.stemGains.set(stemId, gain);
      voice.seamGains.set(stemId, seamGain);
    }
    this.voices.add(voice);
    if (options.suppressDecision) return voice;
    if (!decoded.section.loopable) {
      const natural = transitionsFrom(
        this.manifest.transitions,
        decoded.section.id,
      )
        .filter(
          (transition) =>
            transition.timing === 'section-end' &&
            this.sections.get(transition.to)?.role !== 'stinger',
        )
        .sort((left, right) => this.compareTransitions(left, right))[0];
      const destination = natural ? this.sections.get(natural.to) : null;
      if (destination) {
        // Start network/decode work while this finite section is still playing.
        // A rejected prefetch evicts itself and will be surfaced if it is needed.
        void this.decodeSection(destination).catch(() => {});
        voice.prefetchedSectionId = destination.id;
      }
    }
    const barSeconds = this.manifest.barFrames / this.manifest.sampleRate;
    const decisionLead = Math.min(barSeconds * 2, decoded.durationSeconds / 2);
    const minimumBars =
      decoded.section.repeat?.minimumBars ?? decoded.section.barCount;
    const minimumCycles = decoded.section.loopable
      ? Math.max(1, Math.ceil(minimumBars / decoded.section.barCount))
      : 1;
    voice.decisionTimer = this.atAudioTime(
      when +
        Math.max(
          0,
          minimumCycles * decoded.durationSeconds - decisionLead,
        ),
      () => {
        voice.decisionTimer = null;
        if (decoded.section.loopable) this.handleLoopDecision(voice);
        else this.handleSectionEnding(voice);
      },
    );
    return voice;
  }

  private handleLoopDecision(voice: SectionVoice): void {
    if (this.disposed || voice.stopped || this.active !== voice) return;
    if (this.pending || this.loading) {
      this.scheduleNextLoopDecision(voice);
      return;
    }
    if (voice.section.classification !== this.direction.state) {
      this.reconcileDirection();
      this.scheduleNextLoopDecision(voice);
      return;
    }
    const rotation = this.nextRotationTransition(voice);
    if (!rotation) {
      this.scheduleNextLoopDecision(voice);
      return;
    }
    void this.followTransition(voice, rotation, false, true).catch(
      (reason: unknown) => this.reportError(reason),
    );
  }

  private scheduleNextLoopDecision(voice: SectionVoice): void {
    if (
      this.disposed ||
      voice.stopped ||
      voice.decisionTimer ||
      this.active !== voice
    ) {
      return;
    }
    const barSeconds = this.manifest.barFrames / this.manifest.sampleRate;
    const decisionLead = Math.min(barSeconds * 2, voice.durationSeconds / 2);
    const elapsed = Math.max(0, this.context.currentTime - voice.startedAt);
    const nextCycle = Math.floor(
      (elapsed + decisionLead) / voice.durationSeconds,
    ) + 1;
    voice.decisionTimer = this.atAudioTime(
      voice.startedAt + nextCycle * voice.durationSeconds - decisionLead,
      () => {
        voice.decisionTimer = null;
        this.handleLoopDecision(voice);
      },
    );
  }

  private handleSectionEnding(voice: SectionVoice): void {
    if (
      this.disposed ||
      voice.stopped ||
      this.active !== voice ||
      this.pending ||
      this.loading
    ) {
      return;
    }
    if (voice.section.role === 'resolve') return;
    const transition =
      this.nextTransition(voice.section.id, this.direction.state) ??
      transitionsFrom(this.manifest.transitions, voice.section.id)
        .filter(
          (candidate) =>
            this.sections.get(candidate.to)?.role !== 'stinger',
        )
        .sort((left, right) => this.compareTransitions(left, right))[0];
    if (transition) {
      void this.followTransition(voice, transition, true).catch(
        (reason: unknown) => {
          if (!this.scheduleFiniteSuccessorRetry(voice)) {
            this.reportError(reason);
          }
        },
      );
    }
  }

  private scheduleFiniteSuccessorRetry(voice: SectionVoice): boolean {
    if (
      this.disposed ||
      voice.stopped ||
      voice.section.loopable ||
      this.active !== voice ||
      this.pending ||
      this.loading ||
      voice.decisionTimer
    ) {
      return false;
    }
    if (voice.successorRetryAttempts >= FINITE_RETRY_MAX_ATTEMPTS) {
      return false;
    }
    const delay = Math.min(
      FINITE_RETRY_MAX_SECONDS,
      FINITE_RETRY_INITIAL_SECONDS *
        2 ** Math.min(voice.successorRetryAttempts, 3),
    );
    voice.successorRetryAttempts += 1;
    voice.decisionTimer = this.atAudioTime(
      this.context.currentTime + delay,
      () => {
        voice.decisionTimer = null;
        this.handleSectionEnding(voice);
      },
    );
    return true;
  }

  private ensureFiniteSuccessor(voice: SectionVoice): void {
    if (
      voice.section.loopable ||
      voice.decisionTimer ||
      this.pending ||
      this.loading
    ) {
      return;
    }
    this.handleSectionEnding(voice);
  }

  private compareTransitions(
    left: SoundtrackTransition,
    right: SoundtrackTransition,
  ): number {
    const desiredEnergy = Math.max(
      0,
      Math.min(1, this.direction.intensity + this.direction.momentum * 0.14),
    );
    const score = (transition: SoundtrackTransition): number => {
      const energy = this.sections.get(transition.to)?.energy ?? desiredEnergy;
      return transition.weight - Math.abs(energy - desiredEnergy) * 0.45;
    };
    return score(right) - score(left) || left.to.localeCompare(right.to);
  }

  private updateStemGains(
    voice: SectionVoice,
    intensity: number,
    attacking: boolean,
    timeConstant?: number,
  ): void {
    if (
      voice.section.role === 'resolve' &&
      this.context.currentTime < voice.startedAt + voice.durationSeconds
    ) {
      return;
    }
    const now = this.context.currentTime;
    const responseSeconds =
      timeConstant ??
      (this.accentBoost > 0
        ? this.accentAttackSeconds
        : attacking
          ? this.verticalResponseSeconds(true)
          : now < this.accentReleaseUntil
            ? this.accentReleaseSeconds
            : this.verticalResponseSeconds(false));
    for (const [stemId, gain] of voice.stemGains) {
      const target = this.targetStemGain(
        voice.section,
        stemId,
        this.effectiveStemIntensity(stemId, intensity),
      ) * this.stemAccentMultiplier(stemId);
      holdAutomationAtTime(gain.gain, now);
      gain.gain.setTargetAtTime(target, now, responseSeconds);
    }
  }

  private applyEnergySeamFade(
    voice: SectionVoice,
    when: number,
    duration: number,
    fadeIn: boolean,
  ): void {
    for (const [stemId, gain] of voice.seamGains) {
      if (!this.isEnergyStem(stemId)) continue;
      applyLinearFade(gain.gain, when, duration, fadeIn);
    }
  }

  private isEnergyStem(stemId: string): boolean {
    const role = this.stems.get(stemId)?.role;
    return role !== undefined && ENERGY_STEM_ROLES.has(role);
  }

  private verticalResponseSeconds(attacking: boolean): number {
    const beatSeconds =
      this.manifest.barFrames /
      this.manifest.sampleRate /
      this.manifest.beatsPerBar;
    return attacking
      ? Math.max(0.35, Math.min(0.8, beatSeconds))
      : Math.max(0.9, Math.min(1.8, beatSeconds * 2.4));
  }

  private effectiveStemIntensity(stemId: string, intensity: number): number {
    const stem = this.stems.get(stemId);
    if (!stem || stem.response.full <= stem.response.minimum) return intensity;
    return Math.max(0, Math.min(1, intensity + this.accentBoost));
  }

  private stemAccentMultiplier(stemId: string): number {
    const stem = this.stems.get(stemId);
    if (!stem || stem.response.full <= stem.response.minimum) return 1;
    // A restrained trim keeps impacts audible even when the response curve is
    // already fully open, without pumping the foundation or the whole mix.
    return 1 + Math.min(0.1, this.accentBoost * 0.18);
  }

  private targetStemGain(
    section: SoundtrackSection,
    stemId: string,
    intensity: number,
  ): number {
    const stem = this.stems.get(stemId);
    if (!stem) return 0;
    const response = responseAt(stem, intensity);
    const sectionGain = section.stemGainsDb?.[stemId] ?? 0;
    return response * dbToGain(stem.gainDb + sectionGain);
  }

  private stopVoice(voice: SectionVoice, when: number): void {
    if (voice.stopped) return;
    voice.stopped = true;
    if (voice.decisionTimer) {
      const timer = voice.decisionTimer;
      timer.onended = () => timer.disconnect();
      voice.decisionTimer = null;
    }
    for (const source of voice.sources) {
      try {
        source.stop(when);
      } catch {
        // A source may have ended naturally before a delayed graph transition.
      }
    }
    this.atAudioTime(when + 0.002, () => {
      for (const source of voice.sources) source.disconnect();
      for (const gain of voice.stemGains.values()) gain.disconnect();
      for (const gain of voice.seamGains.values()) gain.disconnect();
      voice.bus.disconnect();
      this.voices.delete(voice);
    });
  }

  private atAudioTime(when: number, callback: () => void): ConstantSourceNode {
    const timer = this.context.createConstantSource();
    timer.offset.value = 0;
    timer.connect(this.master);
    timer.onended = () => {
      timer.disconnect();
      callback();
    };
    timer.start();
    timer.stop(Math.max(this.context.currentTime + 0.001, when));
    return timer;
  }

  private pruneDecoded(): void {
    const keep = new Set<string>();
    for (const voice of this.voices) {
      if (voice.stopped) continue;
      keep.add(voice.section.id);
      if (voice.prefetchedSectionId) keep.add(voice.prefetchedSectionId);
    }
    if (this.loading) keep.add(this.loading.transition.to);
    if (this.pending) keep.add(this.pending.to.section.id);
    for (const sectionId of this.decoded.keys()) {
      if (!keep.has(sectionId)) this.decoded.delete(sectionId);
    }
  }

  private assertActive(): void {
    if (this.disposed) throw new Error('Soundtrack engine has been disposed.');
  }

  private reportError(reason: unknown): void {
    if (this.disposed) return;
    this.onError(
      reason instanceof Error ? reason : new Error('Adaptive soundtrack failed.'),
    );
  }
}

function transitionsFrom(
  transitions: SoundtrackTransition[],
  sectionId: string,
): SoundtrackTransition[] {
  return transitions.filter((transition) => transition.from === sectionId);
}

function normalizeDirection(direction: ScoreDirection): ScoreDirection {
  return {
    state: direction.state,
    intensity: Math.max(0, Math.min(1, direction.intensity)),
    targetIntensity: Math.max(0, Math.min(1, direction.targetIntensity)),
    momentum: Math.max(-1, Math.min(1, direction.momentum)),
  };
}

function safeReplaySeconds(getReplaySeconds: () => number): number {
  try {
    const seconds = getReplaySeconds();
    return Number.isFinite(seconds) ? Math.max(0, seconds) : 0;
  } catch {
    return 0;
  }
}

function responseAt(stem: SoundtrackStem, intensity: number): number {
  const { minimum, full } = stem.response;
  if (full <= minimum) return intensity >= full ? 1 : 0;
  const normalized = Math.max(0, Math.min(1, (intensity - minimum) / (full - minimum)));
  return normalized * normalized * (3 - 2 * normalized);
}

function dbToGain(db: number): number {
  return 10 ** (db / 20);
}

function holdAutomationAtTime(parameter: AudioParam, when: number): void {
  if (typeof parameter.cancelAndHoldAtTime === 'function') {
    parameter.cancelAndHoldAtTime(when);
    return;
  }
  // Older Web Audio implementations retain automation that began before the
  // cancellation time, so a following target continues from that value.
  parameter.cancelScheduledValues(when);
}

function applyEqualPowerFade(
  parameter: AudioParam,
  when: number,
  duration: number,
  fadeIn: boolean,
): void {
  const curve = new Float32Array(TRANSITION_CURVE_STEPS);
  for (let index = 0; index < curve.length; index += 1) {
    const progress = index / (curve.length - 1);
    curve[index] = fadeIn
      ? Math.sin(progress * Math.PI * 0.5)
      : Math.cos(progress * Math.PI * 0.5);
  }
  parameter.cancelScheduledValues(when);
  parameter.setValueCurveAtTime(curve, when, Math.max(0.001, duration));
}

function applyLinearFade(
  parameter: AudioParam,
  when: number,
  duration: number,
  fadeIn: boolean,
): void {
  const curve = new Float32Array(TRANSITION_CURVE_STEPS);
  for (let index = 0; index < curve.length; index += 1) {
    const progress = index / (curve.length - 1);
    curve[index] = fadeIn ? progress : 1 - progress;
  }
  parameter.cancelScheduledValues(when);
  parameter.setValueCurveAtTime(curve, when, Math.max(0.001, duration));
}

function applyResolveEnvelope(
  parameter: AudioParam,
  when: number,
  duration: number,
  valueAtProgress: (progress: number) => number,
): void {
  const curve = new Float32Array(RESOLVE_CURVE_STEPS);
  for (let index = 0; index < curve.length; index += 1) {
    const linear = index / (curve.length - 1);
    const progress = linear * linear * (3 - 2 * linear);
    curve[index] = valueAtProgress(progress);
  }
  parameter.cancelScheduledValues(when);
  parameter.setValueCurveAtTime(curve, when, Math.max(0.001, duration));
}
