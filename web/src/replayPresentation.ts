import type {
  ReplayArcRelayFact,
  ReplayActorLifeKey,
  ReplayActorState,
  ReplayFormTransition,
  ReplayModeState,
  ReplayModel,
  ReplayProjectileHeading,
  ReplayStableUnitKey,
  ReplayUnitLifecycleStatus,
  ReplayWorldSnapshot,
} from './replayModel';
import { playerAccent } from './presentation/playerAccent';
import { unitAccent, unitLook } from './render/unitPresentation';
import { replayMaxHealth } from './replayMetadata';
import {
  legacySlotForUnit,
  participantForUnit,
  teamName,
  unitName,
} from './replayParticipants';

/**
 * Everything a panel or bridge needs to describe one normalized replay tick.
 * Rules-derived values live here so the canvas, web cards, and hosted bridge
 * cannot drift into three interpretations of the same match.
 */

export interface LegacyControlPresentation {
  kind: 'legacy-control';
  /** Signed toward the first team, matching replay-v1's historical meter. */
  pressure: number;
  limit: number;
  overtime: boolean;
  phase: string | null;
  names: [string, string];
}

/**
 * A claim losing ground, and which of the two ways it is losing it.
 *
 * The channel has two subtractive paths and they are different events to
 * watch: an **interrupt** is a bolt landing on a body that is holding the
 * point, taking back the whole run's work in one beat, and an **erosion** is
 * the enemy standing on your claim and grinding it down at a steady multiple
 * every tick. Reading them as one "progress went down" is exactly the reading
 * that made both invisible — a hit reaction and a drain look nothing alike.
 *
 * Derived rather than published: the wire moves `captureProgress` and says
 * nothing about why, so this compares consecutive ticks of the same run and
 * asks whether hostile damage landed on the controller's bodies standing in
 * the active objective region.
 */
export interface CaptureRevertPresentation {
  kind: 'interrupt' | 'erosion';
  /** Points of progress taken back. */
  amount: number;
  /** Those points as a fraction of the threshold — what a renderer knocks back. */
  fraction: number;
  /** Where the claim stood before the revert, as a fraction of the threshold. */
  fromFraction: number;
  /** The team whose work was reverted. */
  teamId: number;
  /** Tiles the interrupting damage landed on; empty for an erosion. */
  at: { x: number; y: number }[];
  /**
   * 0 on the tick it landed. An interrupt lingers for a short beat so the
   * knockback is legible at playback speed; an erosion is only ever reported
   * on the tick it happens, because it repeats on its own.
   */
  ticksSince: number;
  /**
   * 1 on the tick it landed, falling to 0 at the end of its beat. Resolved
   * here rather than by each renderer, so the flash and the panel fade over
   * the same window and neither has to know how long the beat is.
   */
  strength: number;
}

/**
 * How long an interrupt stays readable. Playback runs 5 ticks a second at 1x,
 * so a single-tick flash is 200ms — enough to miss, and this is the beat the
 * whole mechanic turns on.
 */
const INTERRUPT_BEAT_TICKS = 3;

/** The control policy string that says a ruleset captures by channelling. */
const CHANNEL_CONTROL_POLICY_PREFIX =
  'stationary-claim-weight-versus-total-denial-weight';

/**
 * How far from the objective a body still counts as escorting it.
 *
 * A screen is a body on the firing line to a channeler and off the objective
 * itself. "On the firing line" is a heading the viewer cannot know without
 * simulating, so proximity stands in for it: near the point and not on it,
 * while a teammate channels. Far enough out and a body is doing something
 * else entirely — harvesting the side lanes, most likely — and calling that an
 * escort would make the cue meaningless.
 */
const SCREEN_REACH_TILES = 5;

export interface FrontlineControlPresentation {
  kind: 'frontline';
  activePositionIndex: number;
  positionCount: number;
  claimingTeamId: number | null;
  captureProgress: number;
  captureThreshold: number;
  controlResumesAtTick: number;
  /**
   * Rules-resolved team applying positive capture pressure this tick.
   * Null means no team has positive pressure; it never implies ownership.
   */
  captureTeamId: number | null;
  /** True when the capture policy resolves present objective weight as a contest. */
  captureContested: boolean;
  /** True while redeployment prevents capture pressure from changing the meter. */
  capturePaused: boolean;
  /** Exact replay-v3 ratchet owner; null when no hold is live. */
  holdOwnerTeamId: number | null;
  /** Exact replay-v3 expiry tick; null when no hold is live. */
  holdEndsAtTick: number | null;
  /** Presentation-only countdown derived from holdEndsAtTick and nextTick. */
  holdRemainingTicks: number | null;
  /** Contract-declared ratchet duration, when this ruleset has one. */
  holdDurationTicks: number | null;
  winnerTeamId: number | null;
  phase: string;
  /**
   * True when the declared control policy is the capture channel: standing
   * still is what captures, and taking damage on the point takes it back.
   * False on every ruleset without it, which is what keeps a pre-channel
   * replay rendering exactly as it always did.
   */
  channel: boolean;
  /** Contract-declared ceiling on the channel's gain multiplier; null off it. */
  channelGainCap: number | null;
  /**
   * This tick's channel surplus: the controlling team's stationary claim
   * weight minus every opponent's total denial weight, capped. Null off the
   * channel or while nobody controls.
   */
  channelGain: number | null;
  /** Bodies channelling for the controlling team this tick. */
  channelingUnitCount: number;
  /** Bodies screening for them — near the point, off it, while a teammate channels. */
  screeningUnitCount: number;
  /** A claim losing ground this tick, and how. Null while nothing is lost. */
  captureRevert: CaptureRevertPresentation | null;
}

export type ObjectivePresentation =
  | LegacyControlPresentation
  | FrontlineControlPresentation;

/** One upgrade track's position on the declared ladder for one team. */
export interface ScrapTrackPresentation {
  trackId: string;
  /** Display label — the track id, which is already the player's word for it. */
  label: string;
  tier: number;
  maxTier: number;
  /** Price of the next tier, or null once the track is maxed. */
  nextCost: number | null;
  /** Whether this team's bank covers that next tier right now. */
  affordable: boolean;
  /** Tiers bought within the purchase beat, newest first. */
  boughtTicksSince: number | null;
}

/** One team's economic position: what it holds, and what it has turned it into. */
export interface ScrapTeamPresentation {
  teamId: number;
  name: string;
  accent: string;
  /** Liquid scrap in the bank. */
  bank: number;
  /** Scrap this team's bodies are carrying and have not banked yet. */
  carried: number;
  tracks: ScrapTrackPresentation[];
  tierTotal: number;
  maxTotalTiers: number;
}

/** One live pile of loose scrap, with its clock resolved for a renderer. */
export interface ScrapPilePresentation {
  position: { x: number; y: number };
  amount: number;
  expiresAtTick: number;
  /** Ticks until it is gone; 0 on the tick it disappears. */
  remainingTicks: number;
  /** 1 the tick it lands, falling to 0 at expiry. */
  lifeFraction: number;
  /** True in the last quarter of its life — renderers blink it out. */
  expiring: boolean;
  /** A scheduled deposit standing on a declared site, rather than a wreck. */
  vein: boolean;
}

/** A tier bought this beat — the moment the enemy's bank became a better gun. */
export interface ScrapPurchasePresentation {
  teamId: number;
  teamName: string;
  accent: string;
  trackId: string;
  label: string;
  tier: number;
  /** 0 on the tick the purchase settled; renderers fade the beat out over it. */
  ticksSince: number;
  /** 1 on the tick it settled, falling to 0 at the end of the beat. */
  strength: number;
}

/**
 * The declared battlefield economy, resolved for this tick.
 *
 * Null on every replay whose ruleset declares none — which is every replay
 * written before the arm existed — so a renderer that draws nothing when this
 * is null keeps those exactly as they were.
 */
export interface ScrapEconomyPresentation {
  kind: 'scrap';
  teams: ScrapTeamPresentation[];
  piles: ScrapPilePresentation[];
  carryCapacity: number;
  /** Declared deposit addresses, known before tick zero. */
  veinSites: { x: number; y: number }[];
  /** The next scheduled deposit tick, or null once the schedule is spent. */
  nextVeinTick: number | null;
  /** True on a tick a deposit is due — the metronome, made visible. */
  veinDueNow: boolean;
  /** Purchases inside the beat window, newest first. */
  purchases: ScrapPurchasePresentation[];
}

/**
 * How long a purchase stays a visible beat. Four ticks is about 0.8s at 1x,
 * which is a flash rather than a state.
 */
const PURCHASE_BEAT_TICKS = 4;

export interface UnitPresentation {
  unitKey: ReplayStableUnitKey;
  actorKey: ReplayActorLifeKey | null;
  teamId: number;
  unitId: number;
  lifeId: number | null;
  participantId: number;
  /** Replay-v1 compatibility identity; never populated for replay-v2. */
  legacySlot: number | null;
  name: string;
  accent: string;
  lookLabel: string;
  runtimeKind: string;
  formId: string;
  canMove: boolean;
  omnidirectionalVision: boolean;
  omnidirectionalShooting: boolean;
  status: ReplayUnitLifecycleStatus;
  respawnAtTick: number | null;
  unlockAtTick: number | null;
  rebuildReadyAtTick: number | null;
  fabricationAtTick: number | null;
  reservedSpawn: { x: number; y: number } | null;
  pendingSpawnReason: string | null;
  pendingFormTransition: ReplayFormTransition | null;
  health: number;
  maxHealth: number;
  cooldown: number;
  energy: number | null;
  zoneTicks: number | null;
  holdingObjective: boolean;
  actionId: string | null;
  actionLaunchHeading: ReplayProjectileHeading | null;
  actionResult: string | null;
  debug: string | null;
  visibleTiles: number;
  visibleEnemies: { x: number; y: number }[];
  /**
   * Scrap this body is carrying at the end of this tick. Always 0 without a
   * declared economy. A loaded body is a courier: killing it drops the whole
   * load plus its wreck on one tile, which is what makes it worth chasing.
   */
  carriedScrap: number;
  /** Fraction of the declared carry cap in hand; 0 without an economy. */
  carriedFraction: number;
  /**
   * What this body is doing for the channel this tick: holding the point
   * still, or standing off it near a teammate who is.
   */
  channelRole: 'channeling' | 'screening' | null;
  /**
   * The free-vocabulary label this body's MIND published, or null.
   *
   * Unlike `channelRole` — which the viewer DERIVES from world state — this is
   * what the author said the body's job is, in the author's own words. That is
   * the whole reason it is worth rendering: a spectator reading
   * `channeler / screen / screen / courier` understands the set-piece while it
   * happens, and the vocabulary an author chose is the strategy made legible.
   * Null for every per-life replay, which has no way to set one.
   */
  roleTag: string | null;
  /**
   * The runtime fault this body's turn recorded, or null.
   *
   * Per-life it costs that body its decision and its private memory, which
   * respawn would have cleared anyway. Under the mind it is participant-scoped:
   * one trap costs every own body its decision that tick AND discards the
   * mind's entire match-long memory, because there is no snapshot to restore
   * and a trap can leave a torn heap. Keeping that visible rather than smoothing
   * it over is deliberate — robustness is a real design pressure now.
   */
  runtimeFault: {
    participantId: number;
    stage: string;
    faultCode: string;
    disqualificationTriggered: boolean;
  } | null;
}

export interface ArcRelayBeatPresentation {
  kind: 'birth' | 'steal' | 'drop' | 'bank' | 'pulse';
  tick: number;
  headline: string;
  detail: string;
  teamId: number | null;
  accent: string | null;
  /** Five-tick broadcast beat: 1 on impact, fading to 0. */
  strength: number;
}

export interface ArcRelayCorePresentation {
  key: string;
  sourceLabel: string;
  position: { x: number; y: number };
  disposition: 'loose' | 'carried' | 'in-flight';
  carrierUnitKey: ReplayStableUnitKey | null;
  carrierTeamId: number | null;
  carrierName: string | null;
  distanceToBank: number | null;
  pulseCore: boolean;
}

export interface ArcRelayStoryPresentation {
  kind: 'arc-relay';
  beat: ArcRelayBeatPresentation | null;
  cue: {
    headline: string;
    detail: string;
    teamId: number | null;
    accent: string | null;
  };
  cores: ArcRelayCorePresentation[];
  wells: {
    wellId: string;
    sourceLabel: string;
    position: { x: number; y: number };
    nextBirthTick: number | null;
    outstanding: boolean;
  }[];
  reactors: {
    teamId: number;
    position: { x: number; y: number };
    chargePips: number;
    integritySegments: number;
    accent: string;
  }[];
}

export interface TickPresentation {
  tick: number;
  objective: ObjectivePresentation | null;
  units: UnitPresentation[];
  /** The declared scrap economy at this tick; null when the ruleset has none. */
  economy: ScrapEconomyPresentation | null;
  /** Arc Relay's spectator story; null on every other ruleset. */
  arcRelay: ArcRelayStoryPresentation | null;
}

export interface ReplayPresenter {
  tickCount: number;
  maxHealth: number;
  at: (tick: number) => TickPresentation;
}

export function createPresenter(replay: ReplayModel): ReplayPresenter {
  const maxHealth = replayMaxHealth(replay);
  const tickCount = replay.ticks.length;
  const legacyZone = deriveLegacyZone(replay);
  const economyRules = scrapEconomyRules(replay);
  const captureRules = frontlineCaptureRules(replay);
  const arcRelayBeats = arcRelayBeatTimeline(replay);
  // Both renderers call this once per animation frame, and the playhead crosses
  // dozens of frames per tick. The answer is a pure function of the tick, so it
  // is derived once and handed back until the tick changes.
  let memo: { tickIndex: number; value: TickPresentation } | null = null;

  const at = (rawTick: number): TickPresentation => {
    if (tickCount === 0) {
      return {
        tick: 0,
        objective: null,
        units: replay.units.map((unit) =>
          presentUnit(
            replay,
            unit.unitKey,
            replay.initialWorld,
            replay.initialWorld,
            null,
            legacyZone,
            0,
            null,
            null,
          ),
        ),
        economy: null,
        arcRelay: null,
      };
    }

    const tickIndex = Math.max(0, Math.min(rawTick, tickCount - 1));
    if (memo?.tickIndex === tickIndex) return memo.value;
    const tick = replay.ticks[tickIndex];
    const channel = channelReadingAt(replay, tickIndex, captureRules);
    const carried = carriedScrapAt(replay, tickIndex, economyRules !== null);
    const value: TickPresentation = {
      tick: tick.tick,
      objective: objectiveAt(replay, tickIndex, legacyZone, channel),
      units: replay.units.map((unit) =>
        presentUnit(
          replay,
          unit.unitKey,
          tick.before,
          tick.after,
          tick.actorTurns.find(
            (turn) => turn.actor.unitKey === unit.unitKey,
          ) ?? null,
          legacyZone,
          tickIndex,
          channel,
          economyRules === null
            ? null
            : {
                carried: carried.get(unit.unitKey) ?? 0,
                capacity: economyRules.carryCapacity,
              },
        ),
      ),
      economy: economyAt(replay, tickIndex, economyRules, carried),
      arcRelay: arcRelayAt(replay, tickIndex, arcRelayBeats),
    };
    memo = { tickIndex, value };
    return value;
  };

  return { tickCount, maxHealth, at };
}

function presentUnit(
  replay: ReplayModel,
  unitKey: ReplayStableUnitKey,
  before: ReplayWorldSnapshot | null,
  after: ReplayWorldSnapshot | null,
  turn: ReplayModel['ticks'][number]['actorTurns'][number] | null,
  legacyZone: LegacyZone | null,
  tickIndex: number,
  channel: ChannelReading | null,
  load: { carried: number; capacity: number } | null,
): UnitPresentation {
  const unit = replay.units.find(
    (candidate) => candidate.unitKey === unitKey,
  );
  if (!unit) throw new Error(`Replay unit ${unitKey} is missing.`);
  const afterUnit = after?.units.find(
    (candidate) => candidate.unitKey === unitKey,
  );
  const actor =
    after?.actors.find((candidate) => candidate.unitKey === unitKey) ??
    before?.actors.find((candidate) => candidate.unitKey === unitKey) ??
    null;
  const activeActor =
    after?.actors.find(
      (candidate) =>
        candidate.unitKey === unitKey && candidate.status === 'active',
    ) ??
    null;
  const formId =
    activeActor?.formId ??
    actor?.formId ??
    afterUnit?.formId ??
    unit.initialFormId ??
    '';
  const form = replay.forms.find(
    (candidate) => candidate.formId === formId,
  );
  const participant = participantForUnit(replay, unitKey);
  // Resolved through the same place the arena resolves it, for the effective form: a card
  // showing a different chassis or a different colour from the bot it names is worse than
  // no card.
  const look = unitLook(replay, unitKey, formId);
  const status =
    afterUnit?.lifecycleStatus ?? actor?.status ?? 'respawning';
  const position = activeActor?.position ?? actor?.position ?? null;
  const frontlineObjective =
    after?.objective.kind === 'frontline' ? after.objective : null;
  const objectiveTiles =
    replay.map.frontline && frontlineObjective
      ? replay.map.frontline.positions.find(
          (candidate) =>
            candidate.positionIndex ===
            frontlineObjective.activePositionIndex,
        )?.tiles ?? []
      : replay.map.objectiveTiles;
  const onObjective =
    position !== null &&
    objectiveTiles.some(
      (point) => point.x === position.x && point.y === position.y,
    );
  const zoneTicks =
    legacyZone?.cumulative?.[tickIndex]?.get(unitKey) ??
    (after?.objective.kind === 'legacy'
      ? after.objective.zoneTicks.find(
          (entry) => entry.unitKey === unitKey,
        )?.ticks
      : undefined) ??
    null;

  return {
    unitKey,
    actorKey: activeActor?.actorKey ?? null,
    teamId: unit.teamId,
    unitId: unit.unitId,
    lifeId: activeActor?.identity.lifeId ?? null,
    participantId: unit.controllerParticipantId,
    legacySlot: legacySlotForUnit(replay, unitKey),
    name: unitName(replay, unitKey),
    accent: playerAccent(unitAccent(replay, unitKey, formId)),
    lookLabel: look.label,
    runtimeKind: participant?.runtimeKind ?? 'unknown',
    formId,
    canMove: form?.canMove ?? true,
    omnidirectionalVision: form?.omnidirectionalVision ?? false,
    omnidirectionalShooting: form?.omnidirectionalShooting ?? false,
    status,
    respawnAtTick: afterUnit?.respawnAtTick ?? null,
    unlockAtTick: afterUnit?.unlockAtTick ?? null,
    rebuildReadyAtTick: afterUnit?.rebuildReadyAtTick ?? null,
    fabricationAtTick: afterUnit?.fabricationAtTick ?? null,
    reservedSpawn: afterUnit?.reservedSpawn
      ? { ...afterUnit.reservedSpawn }
      : null,
    pendingSpawnReason: afterUnit?.pendingSpawnReason ?? null,
    pendingFormTransition: activeActor?.pendingFormTransition
      ? { ...activeActor.pendingFormTransition }
      : null,
    health: activeActor?.health ?? (status === 'active' ? (actor?.health ?? 0) : 0),
    maxHealth: form?.maxHealth ?? Math.max(1, actor?.health ?? maxHealthFallback(replay)),
    cooldown: activeActor?.cooldown ?? 0,
    energy: activeActor?.energy ?? null,
    zoneTicks,
    holdingObjective:
      onObjective &&
      status === 'active' &&
      (form?.objectiveWeight ?? 1) > 0 &&
      (after?.objective.kind === 'frontline' ||
        replay.contract.rules.objective.sharedPressureEnabled ||
        (turn?.actionResolution.validatedActionId === 'wait' &&
          turn.actionResolution.result === 'success')),
    actionId: turn?.actionResolution.chosenActionId ?? null,
    actionLaunchHeading:
      turn?.actionResolution.chosenPayload?.launchHeading ?? null,
    actionResult: turn?.actionResolution.result ?? null,
    debug: turn?.runtimeReply.debugMessage ?? null,
    visibleTiles: turn?.observation.visibleTiles.length ?? 0,
    visibleEnemies:
      turn?.observation.enemies.map((enemy) => ({
        x: enemy.position.x,
        y: enemy.position.y,
      })) ?? [],
    carriedScrap: load?.carried ?? 0,
    carriedFraction:
      load === null || load.capacity <= 0
        ? 0
        : Math.min(1, load.carried / load.capacity),
    channelRole: channel?.roles.get(unitKey) ?? null,
    // From this body's own observation of itself, which under the mind is the
    // mind's published label for it. Absent renders as nothing at all: an
    // unlabelled body should look unlabelled, not broken.
    roleTag: turn?.observation.self?.roleTag ?? null,
    // A trap, and under the mind a trap is a different kind of event: the
    // Store is discarded, so the participant loses its whole match-long memory
    // rather than one body's private fields — and under the shipped Labs
    // contract the first fault also disqualifies it. That deserves a frame of
    // its own rather than an outcome word nobody reads.
    runtimeFault: turn?.actionResolution.runtimeFault ?? null,
  };
}

function maxHealthFallback(replay: ReplayModel): number {
  return Math.max(1, ...replay.forms.map((form) => form.maxHealth));
}

function objectiveAt(
  replay: ReplayModel,
  tickIndex: number,
  legacyZone: LegacyZone | null,
  channel: ChannelReading | null,
): ObjectivePresentation | null {
  const tick = replay.ticks[tickIndex];
  const objective = tick.after.objective;
  if (objective.kind === 'frontline') {
    const legacyDefinition =
      replay.contract.kind === 'v2-full'
        ? replay.contract.rules.frontlineDefinition
        : null;
    const genericDefinition =
      replay.contract.kind === 'v3-generic' &&
      replay.contract.mode.kind === 'frontline'
        ? replay.contract.mode
        : null;
    const threshold =
      legacyDefinition?.capture.threshold ??
      genericDefinition?.capture.threshold ??
      1;
    const terminalWinner =
      tickIndex === replay.ticks.length - 1 &&
      replay.result?.mode?.kind === 'frontline' &&
      replay.result.mode.reason === 'base-breach'
        ? replay.result.winnerTeamId
        : null;
    const winnerTeamId = objective.winnerTeamId ?? terminalWinner;
    const holdOwnerTeamId = objective.holdOwnerTeamId ?? null;
    const holdEndsAtTick = objective.holdEndsAtTick ?? null;
    const holdRemainingTicks =
      holdEndsAtTick === null
        ? null
        : Math.max(0, holdEndsAtTick - objective.nextTick);
    const holdDurationTicks = ratchetHoldDuration(replay);
    // The channel resolves control itself — stationary claim weight against
    // total denial weight — so it never falls through to the presence rule,
    // which would read a native 2v1 push as a frozen contest.
    const captureControl =
      channel ??
      frontlineCaptureControl(
        replay,
        tick.after,
        objective.activePositionIndex,
      );
    const capturePaused =
      objective.controlResumesAtTick > objective.nextTick;
    const holdPhase =
      holdOwnerTeamId === null || holdRemainingTicks === null
        ? null
        : `${teamName(replay, holdOwnerTeamId)} RATCHET · ` +
          `${holdRemainingTicks} ${
            holdRemainingTicks === 1 ? 'TICK' : 'TICKS'
          }`;
    // A completed breach ends the run by winning it, and the zeroed meter
    // that follows is not work anybody took away.
    const captureRevert =
      channel === null || winnerTeamId !== null
        ? null
        : captureRevertAt(replay, tickIndex, threshold);
    let phase: string;
    if (winnerTeamId !== null) {
      phase = `${teamName(replay, winnerTeamId)} BREACHES`;
    } else if (captureRevert !== null && captureRevert.ticksSince === 0) {
      // The loudest true sentence about this tick. A revert is the whole point
      // of the channel, and it outranks a standing ratchet or a redeploy clock
      // for the one tick it lands on.
      phase =
        captureRevert.kind === 'interrupt'
          ? `${teamName(replay, captureRevert.teamId)} INTERRUPTED · ` +
            `−${captureRevert.amount}`
          : `${teamName(replay, captureRevert.teamId)} CLAIM ERODING · ` +
            `−${captureRevert.amount}`;
    } else if (objective.controlResumesAtTick > objective.nextTick) {
      phase =
        holdPhase === null
          ? `REDEPLOYMENT · RESUMES TICK ${objective.controlResumesAtTick}`
          : `${holdPhase} · REDEPLOY T${objective.controlResumesAtTick}`;
    } else if (holdPhase !== null) {
      phase = holdPhase;
    } else if (objective.claimingTeamId === null) {
      phase = 'FRONTLINE NEUTRAL';
    } else if (channel !== null) {
      // Under the channel the verb is not "pushing": the bodies are standing
      // still, and the multiplier is the thing a watcher wants.
      phase =
        `${teamName(replay, objective.claimingTeamId)} CHANNELING · ` +
        `${objective.captureProgress}/${threshold}` +
        (channel.gain !== null && channel.gain > 1
          ? ` · ×${channel.gain}`
          : '');
    } else {
      phase =
        `${teamName(replay, objective.claimingTeamId)} PUSHING · ` +
        `${objective.captureProgress}/${threshold}`;
    }
    return {
      kind: 'frontline',
      activePositionIndex: objective.activePositionIndex,
      positionCount:
        legacyDefinition?.frontlinePositionCount ??
        genericDefinition?.frontlinePositionCount ??
        replay.map.frontline?.positions.length ??
        0,
      claimingTeamId: objective.claimingTeamId,
      captureProgress: objective.captureProgress,
      captureThreshold: threshold,
      controlResumesAtTick: objective.controlResumesAtTick,
      captureTeamId: captureControl.teamId,
      captureContested: captureControl.contested,
      capturePaused,
      holdOwnerTeamId,
      holdEndsAtTick,
      holdRemainingTicks,
      holdDurationTicks,
      winnerTeamId,
      phase,
      channel: channel !== null,
      channelGainCap: channel?.gainCap ?? null,
      channelGain: channel?.gain ?? null,
      channelingUnitCount: channel?.channelingCount ?? 0,
      screeningUnitCount: channel?.screeningCount ?? 0,
      captureRevert,
    };
  }

  const rules = replay.contract.rules.objective;
  if (
    objective.mode !== 'shared-pressure' ||
    rules.controlPressureLimit === null ||
    legacyZone === null
  ) {
    return null;
  }
  const overtime =
    rules.overtime.startTick !== null &&
    tick.tick >= rules.overtime.startTick;
  const limit =
    overtime && rules.overtime.pressureLimit !== null
      ? rules.overtime.pressureLimit
      : rules.controlPressureLimit;
  const occupants = objectiveOccupants(tick.after, legacyZone);
  const previous =
    tickIndex > 0
      ? objectiveOccupants(
          replay.ticks[tickIndex - 1].after,
          legacyZone,
        )
      : [];
  let phase: string;
  if (rules.controlBySoleOccupancy) {
    if (occupants.length === 1) {
      const holder = unitName(replay, occupants[0]);
      phase =
        previous.length > 1
          ? `CONTEST BROKEN · ${holder} GAINS`
          : `SOLE OCCUPANT · ${holder} GAINS`;
    } else {
      phase =
        occupants.length > 1
          ? 'CONTESTED · PRESSURE DECAYS'
          : 'EMPTY · PRESSURE DECAYS';
    }
  } else {
    const holders = tick.actorTurns
      .filter(
        (turn) =>
          turn.actionResolution.validatedActionId === 'wait' &&
          turn.actionResolution.result === 'success' &&
          occupants.includes(turn.actor.unitKey),
      )
      .map((turn) => turn.actor.unitKey);
    phase =
      holders.length === 1
        ? `HOLDING · ${unitName(replay, holders[0])} GAINS`
        : holders.length > 1
          ? 'BOTH HOLD · PRESSURE FROZEN'
          : 'NO ACTIVE HOLD · PRESSURE DECAYS';
  }
  const orderedTeams = [...replay.teams].sort(
    (left, right) => left.teamId - right.teamId,
  );
  return {
    kind: 'legacy-control',
    pressure: objective.controlPressure ?? 0,
    limit,
    overtime,
    phase,
    names: [
      teamName(replay, orderedTeams[0]?.teamId ?? 0),
      teamName(replay, orderedTeams[1]?.teamId ?? 1),
    ],
  };
}

function ratchetHoldDuration(replay: ReplayModel): number | null {
  return frontlineCaptureRules(replay)?.ratchetHoldTicks ?? null;
}

type FrontlineCaptureRules = Extract<
  ReplayModel['contract'],
  { kind: 'v3-generic' }
>['rawContract']['rules']['gameMode'] extends infer Mode
  ? Mode extends { kind: 'frontline'; capture: infer Capture }
    ? Capture
    : never
  : never;

type ScrapEconomyRules = NonNullable<
  Extract<
    ReplayModel['contract'],
    { kind: 'v3-generic' }
  >['rawContract']['rules']['gameMode'] extends infer Mode
    ? Mode extends { kind: 'frontline'; scrapEconomy?: infer Economy }
      ? Economy
      : never
    : never
>;

/** The declared capture block, or null on a wire that carries no generic contract. */
function frontlineCaptureRules(
  replay: ReplayModel,
): FrontlineCaptureRules | null {
  if (replay.contract.kind !== 'v3-generic') return null;
  const mode = replay.contract.rawContract.rules.gameMode;
  return mode.kind === 'frontline' ? mode.capture : null;
}

/**
 * The declared battlefield economy, or null when the ruleset declares none.
 *
 * Absent means the mechanic does not exist for that match — the same
 * discipline the contract itself uses — so every replay written before the arm
 * existed resolves to null here and renders exactly as it did.
 */
function scrapEconomyRules(
  replay: ReplayModel,
): ScrapEconomyRules | null {
  if (replay.contract.kind !== 'v3-generic') return null;
  const mode = replay.contract.rawContract.rules.gameMode;
  return mode.kind === 'frontline' ? (mode.scrapEconomy ?? null) : null;
}

/**
 * One tick of the capture channel, resolved through its own control rule.
 *
 * The channel does not count presence: a team's **claim weight** is only the
 * objective weight of its bodies whose tile did not change this tick, while its
 * **denial weight** is all of them. Control needs claim to strictly exceed
 * every opponent's denial, which is why a defender who keeps moving still
 * subtracts from an attacker's total and an attacker who takes a step
 * contributes nothing that tick. Deriving that here rather than in a renderer
 * is what lets the arena say who is channelling and who is screening without
 * either viewer re-deciding the rule.
 */
interface ChannelReading {
  /** Same shape `frontlineCaptureControl` returns, so it substitutes for it. */
  teamId: number | null;
  contested: boolean;
  /** min(cap, claim − enemy denial) for the controlling team; null while none. */
  gain: number | null;
  gainCap: number | null;
  roles: Map<ReplayStableUnitKey, 'channeling' | 'screening'>;
  channelingCount: number;
  screeningCount: number;
}

function channelReadingAt(
  replay: ReplayModel,
  tickIndex: number,
  captureRules: FrontlineCaptureRules | null,
): ChannelReading | null {
  if (
    captureRules === null ||
    !captureRules.controlPolicy.startsWith(CHANNEL_CONTROL_POLICY_PREFIX)
  )
    return null;
  const tick = replay.ticks[tickIndex];
  const objective = tick?.after.objective;
  if (!tick || objective?.kind !== 'frontline') return null;
  const tiles =
    replay.map.frontline?.positions.find(
      (position) =>
        position.positionIndex === objective.activePositionIndex,
    )?.tiles ?? [];
  const roles = new Map<ReplayStableUnitKey, 'channeling' | 'screening'>();
  if (tiles.length === 0)
    return {
      teamId: null,
      contested: false,
      gain: null,
      gainCap: captureRules.stationaryGainMultiplierCap ?? null,
      roles,
      channelingCount: 0,
      screeningCount: 0,
    };

  const footprint = new Set(
    tiles.map((position) => `${position.x},${position.y}`),
  );
  const claim = new Map<number, number>();
  const denial = new Map<number, number>();
  const teamsChannelling = new Set<number>();
  for (const actor of tick.after.actors) {
    if (actor.status !== 'active') continue;
    const onPoint = footprint.has(`${actor.position.x},${actor.position.y}`);
    if (!onPoint) continue;
    const weight =
      replay.forms.find((form) => form.formId === actor.formId)
        ?.objectiveWeight ?? 1;
    // Objective weight gates the whole mechanic: an anchored turret neither
    // claims nor denies, and it cannot channel.
    if (weight <= 0) continue;
    const previous = tick.before?.actors.find(
      (candidate) => candidate.actorKey === actor.actorKey,
    );
    // Stillness is positional, not intentional — a blocked move did not move,
    // and a life with no previous position (the tick it spawns) counts as
    // stationary.
    const stationary =
      previous === undefined ||
      (previous.position.x === actor.position.x &&
        previous.position.y === actor.position.y);
    const team = actor.identity.teamId;
    denial.set(team, (denial.get(team) ?? 0) + weight);
    if (!stationary) continue;
    claim.set(team, (claim.get(team) ?? 0) + weight);
    roles.set(actor.unitKey, 'channeling');
    teamsChannelling.add(team);
  }

  const totalDenial = [...denial.values()].reduce(
    (total, weight) => total + weight,
    0,
  );
  let teamId: number | null = null;
  let gain: number | null = null;
  const cap = captureRules.stationaryGainMultiplierCap ?? null;
  for (const [team, weight] of claim) {
    const opposingDenial = totalDenial - (denial.get(team) ?? 0);
    if (weight <= opposingDenial) continue;
    teamId = team;
    const surplus = weight - opposingDenial;
    gain = cap === null ? surplus : Math.min(cap, surplus);
  }

  // Escorts: near the point, off it, while a teammate holds it still. The
  // screen is what makes a solo channel survivable, so a viewer that shows the
  // channeler without it shows half the formation.
  if (teamsChannelling.size > 0) {
    for (const actor of tick.after.actors) {
      if (actor.status !== 'active') continue;
      if (roles.has(actor.unitKey)) continue;
      if (!teamsChannelling.has(actor.identity.teamId)) continue;
      if (footprint.has(`${actor.position.x},${actor.position.y}`)) continue;
      const reach = tiles.reduce(
        (nearest, tile) =>
          Math.min(
            nearest,
            Math.max(
              Math.abs(tile.x - actor.position.x),
              Math.abs(tile.y - actor.position.y),
            ),
          ),
        Number.POSITIVE_INFINITY,
      );
      if (reach <= SCREEN_REACH_TILES)
        roles.set(actor.unitKey, 'screening');
    }
  }

  let channelingCount = 0;
  let screeningCount = 0;
  for (const role of roles.values())
    if (role === 'channeling') channelingCount++;
    else screeningCount++;

  return {
    teamId,
    // Under the channel a second team on the point is not automatically a
    // stall: it is a contest only while nobody's claim clears the opposition.
    contested: teamId === null && denial.size > 1,
    gain,
    gainCap: cap,
    roles,
    channelingCount,
    screeningCount,
  };
}

/**
 * A claim that lost ground on this tick, and which of the two paths took it.
 *
 * Both paths move exactly one published number, so the reading is a comparison
 * of consecutive ticks of the same run plus one question about this tick's
 * damage: did any land on a body of the controlling team standing in the
 * active objective region? That is the interrupt's declared scope, verbatim.
 *
 * A position that advanced is a completed capture, not a revert — the claim is
 * meant to be back at zero — so an index change ends the run instead.
 */
function captureRevertAt(
  replay: ReplayModel,
  tickIndex: number,
  threshold: number,
): CaptureRevertPresentation | null {
  for (let offset = 0; offset < INTERRUPT_BEAT_TICKS; offset++) {
    const index = tickIndex - offset;
    if (index < 0) return null;
    const revert = revertOnTick(replay, index, threshold);
    if (revert === null) continue;
    // An erosion repeats every tick it is happening, so it never needs to be
    // held over; only the one-off hit reaction does.
    if (offset > 0 && revert.kind !== 'interrupt') return null;
    return {
      ...revert,
      ticksSince: offset,
      // An erosion is only reported on its own tick and repeats on its own, so
      // it is always at full strength; only the hit reaction fades.
      strength:
        revert.kind === 'interrupt'
          ? Math.max(0, 1 - offset / INTERRUPT_BEAT_TICKS)
          : 1,
    };
  }
  return null;
}

function revertOnTick(
  replay: ReplayModel,
  tickIndex: number,
  threshold: number,
): Omit<CaptureRevertPresentation, 'ticksSince' | 'strength'> | null {
  const tick = replay.ticks[tickIndex];
  const current = tick?.after.objective;
  const previous =
    (tickIndex > 0
      ? replay.ticks[tickIndex - 1]?.after.objective
      : replay.initialWorld?.objective) ?? null;
  if (
    !tick ||
    current?.kind !== 'frontline' ||
    previous?.kind !== 'frontline' ||
    previous.activePositionIndex !== current.activePositionIndex
  )
    return null;
  const claimantTeamId = previous.claimingTeamId;
  if (claimantTeamId === null) return null;
  if (
    current.claimingTeamId !== null &&
    current.claimingTeamId !== claimantTeamId
  )
    return null;
  const held =
    current.claimingTeamId === claimantTeamId ? current.captureProgress : 0;
  const amount = previous.captureProgress - held;
  if (amount <= 0) return null;

  const tiles =
    replay.map.frontline?.positions.find(
      (position) =>
        position.positionIndex === current.activePositionIndex,
    )?.tiles ?? [];
  const footprint = new Set(
    tiles.map((position) => `${position.x},${position.y}`),
  );
  const at: { x: number; y: number }[] = [];
  for (const event of tick.events) {
    if (event.type !== 'damage') continue;
    if (event.targetActor?.teamId !== claimantTeamId) continue;
    // Generation-3 damage carries the contact tile as the event's position;
    // the older wires spell the same fact `from`.
    const position = event.to ?? event.from;
    if (!position || !footprint.has(`${position.x},${position.y}`)) continue;
    at.push({ x: position.x, y: position.y });
  }

  return {
    kind: at.length > 0 ? 'interrupt' : 'erosion',
    amount,
    fraction: amount / Math.max(1, threshold),
    fromFraction: Math.min(
      1,
      previous.captureProgress / Math.max(1, threshold),
    ),
    teamId: claimantTeamId,
    at,
  };
}

type FrontlineModeState = Extract<ReplayModeState, { kind: 'frontline' }>;

/**
 * The typed Frontline mode state, or null for any other mode.
 *
 * The model's third mode member is an open `{ kind: string }` bag, so a
 * `kind === 'frontline'` comparison alone does not discriminate the union —
 * a fact worth stating once here rather than casting at four call sites.
 */
function frontlineMode(
  mode: ReplayModeState | undefined,
): FrontlineModeState | null {
  return mode !== undefined &&
    mode.kind === 'frontline' &&
    'captureProgress' in mode
    ? mode
    : null;
}

/**
 * Every body's load at the end of this tick, by stable unit.
 *
 * The authoritative world state carries no load at all — only an observation
 * publishes one — and an observation is frozen at the *start* of its tick. So
 * the load a body ends this tick holding is the one the next tick observes,
 * which is also exactly the tick whose world state shows the pile it took the
 * load from as gone. Reading this tick's own observation instead would draw a
 * courier one tick behind the pile it just picked up.
 */
function carriedScrapAt(
  replay: ReplayModel,
  tickIndex: number,
  declared: boolean,
): Map<ReplayStableUnitKey, number> {
  const loads = new Map<ReplayStableUnitKey, number>();
  if (!declared) return loads;
  const source = replay.ticks[tickIndex + 1] ?? replay.ticks[tickIndex];
  if (!source) return loads;
  for (const turn of source.actorTurns) {
    const observed = [
      turn.observation.self,
      ...turn.observation.allies,
      ...turn.observation.enemies,
    ];
    for (const body of observed) {
      if (!body || body.actor.kind !== 'exact') continue;
      const unitKey = body.actor.identity.unitKey;
      // Every observer of the same frozen tick reports the same number; the
      // maximum simply keeps a body that one observer cannot see from being
      // written back down to zero.
      loads.set(
        unitKey,
        Math.max(loads.get(unitKey) ?? 0, body.carriedScrap),
      );
    }
  }
  return loads;
}

/**
 * The declared economy at this tick: both banks, both tier vectors, every live
 * pile, and any tier bought inside the purchase beat.
 *
 * A tier change moves the mode state and rides the ordinary mode-changed fact,
 * so the purchase is read the same way the enemy reads it — by the bank
 * dropping and the tier rising together on the tick they happen.
 */
function economyAt(
  replay: ReplayModel,
  tickIndex: number,
  rules: ScrapEconomyRules | null,
  carried: Map<ReplayStableUnitKey, number>,
): ScrapEconomyPresentation | null {
  if (rules === null) return null;
  const tick = replay.ticks[tickIndex];
  const state = frontlineMode(tick?.after.mode);
  const banks = state?.scrapTeams ?? [];
  const carriedByTeam = new Map<number, number>();
  for (const unit of replay.units) {
    const load = carried.get(unit.unitKey) ?? 0;
    if (load > 0)
      carriedByTeam.set(
        unit.teamId,
        (carriedByTeam.get(unit.teamId) ?? 0) + load,
      );
  }

  const purchases = purchasesAt(replay, tickIndex, rules);
  const teams: ScrapTeamPresentation[] = [...replay.teams]
    .sort((left, right) => left.teamId - right.teamId)
    .map((team) => {
      const bank = banks.find((entry) => entry.teamId === team.teamId);
      const tiers = bank?.tierLevels ?? rules.tracks.map(() => 0);
      const unitKey = replay.units.find(
        (unit) => unit.teamId === team.teamId,
      )?.unitKey;
      return {
        teamId: team.teamId,
        name: teamName(replay, team.teamId),
        accent: playerAccent(
          unitKey === undefined
            ? '#94a3b8'
            : unitAccent(replay, unitKey),
        ),
        bank: bank?.bank ?? 0,
        carried: carriedByTeam.get(team.teamId) ?? 0,
        tierTotal: tiers.reduce((total, tier) => total + tier, 0),
        maxTotalTiers: rules.maxTotalTiers,
        tracks: rules.tracks.map((track, index) => {
          const tier = tiers[index] ?? 0;
          const nextCost =
            tier >= track.maxTier ? null : (track.tierCosts[tier] ?? null);
          const bought = purchases.find(
            (purchase) =>
              purchase.teamId === team.teamId &&
              purchase.trackId === track.trackId,
          );
          return {
            trackId: track.trackId,
            label: track.trackId,
            tier,
            maxTier: track.maxTier,
            nextCost,
            affordable:
              nextCost !== null && (bank?.bank ?? 0) >= nextCost,
            boughtTicksSince: bought?.ticksSince ?? null,
          };
        }),
      };
    });

  const veinSites = rules.veinSites.map((site) => ({
    x: site.x,
    y: site.y,
  }));
  const siteKeys = new Set(veinSites.map((site) => `${site.x},${site.y}`));
  const now = tick?.tick ?? 0;
  const piles: ScrapPilePresentation[] = (state?.scrapPiles ?? []).map(
    (pile) => {
      const remainingTicks = Math.max(0, pile.expiresAtTick - now);
      const lifeFraction = Math.max(
        0,
        Math.min(1, remainingTicks / Math.max(1, rules.pileLifetimeTicks)),
      );
      return {
        position: { x: pile.position.x, y: pile.position.y },
        amount: pile.amount,
        expiresAtTick: pile.expiresAtTick,
        remainingTicks,
        lifeFraction,
        expiring: lifeFraction <= 0.25,
        vein: siteKeys.has(`${pile.position.x},${pile.position.y}`),
      };
    },
  );

  let nextVeinTick: number | null = null;
  for (
    let due = rules.veinFirstSpawnTick;
    due <= rules.veinLastSpawnTick;
    due += Math.max(1, rules.veinSpawnIntervalTicks)
  ) {
    if (due >= now) {
      nextVeinTick = due;
      break;
    }
  }

  return {
    kind: 'scrap',
    teams,
    piles,
    carryCapacity: rules.carryCapacity,
    veinSites,
    nextVeinTick,
    veinDueNow: nextVeinTick === now,
    purchases,
  };
}

function purchasesAt(
  replay: ReplayModel,
  tickIndex: number,
  rules: ScrapEconomyRules,
): ScrapPurchasePresentation[] {
  const purchases: ScrapPurchasePresentation[] = [];
  for (let offset = 0; offset < PURCHASE_BEAT_TICKS; offset++) {
    const index = tickIndex - offset;
    if (index < 0) break;
    const after = frontlineMode(replay.ticks[index]?.after.mode);
    const before = frontlineMode(
      index > 0
        ? replay.ticks[index - 1]?.after.mode
        : replay.initialWorld?.mode,
    );
    if (after === null) continue;
    for (const team of after.scrapTeams ?? []) {
      const previous = before?.scrapTeams?.find(
        (entry) => entry.teamId === team.teamId,
      );
      team.tierLevels.forEach((tier, trackIndex) => {
        const was = previous?.tierLevels[trackIndex] ?? 0;
        const track = rules.tracks[trackIndex];
        if (track === undefined) return;
        for (let step = was + 1; step <= tier; step++) {
          const unitKey = replay.units.find(
            (unit) => unit.teamId === team.teamId,
          )?.unitKey;
          purchases.push({
            teamId: team.teamId,
            teamName: teamName(replay, team.teamId),
            accent: playerAccent(
              unitKey === undefined
                ? '#94a3b8'
                : unitAccent(replay, unitKey),
            ),
            trackId: track.trackId,
            label: track.trackId,
            tier: step,
            ticksSince: offset,
            strength: 1 - offset / PURCHASE_BEAT_TICKS,
          });
        }
      });
    }
  }
  return purchases;
}

/**
 * Resolve objective presence through the replay's declared capture policy.
 *
 * Binary Frontline counts each positive-weight team once, so any second team
 * contests. Net-control Frontline instead compares summed objective weight:
 * a team has positive pressure only when its weight exceeds every opponent's
 * combined weight. Keeping this rules-derived value in the shared presenter
 * prevents both renderers from mistaking a native 2:1 push for a frozen 1:1
 * contest.
 */
function frontlineCaptureControl(
  replay: ReplayModel,
  world: ReplayWorldSnapshot,
  activePositionIndex: number,
): { teamId: number | null; contested: boolean } {
  const tiles = replay.map.frontline?.positions.find(
    (position) =>
      position.positionIndex === activePositionIndex,
  )?.tiles;
  if (!tiles || tiles.length === 0)
    return { teamId: null, contested: false };

  const footprint = new Set(
    tiles.map((position) => `${position.x},${position.y}`),
  );
  const weights = new Map<number, number>();
  for (const actor of world.actors) {
    if (
      actor.status !== 'active' ||
      !footprint.has(`${actor.position.x},${actor.position.y}`)
    )
      continue;
    const weight =
      replay.forms.find((form) => form.formId === actor.formId)
        ?.objectiveWeight ?? 1;
    if (weight <= 0) continue;
    weights.set(
      actor.identity.teamId,
      (weights.get(actor.identity.teamId) ?? 0) + weight,
    );
  }

  const present = [...weights.entries()].filter(
    ([, weight]) => weight > 0,
  );
  if (present.length === 0)
    return { teamId: null, contested: false };

  const mode =
    replay.contract.kind === 'v3-generic'
      ? replay.contract.rawContract.rules.gameMode
      : null;
  const netControl =
    mode?.kind === 'frontline' &&
    mode.capture.controlPolicy ===
      'net-positive-objective-weight-difference-scales-gain-non-positive-applies-configured-decay-opposition-erodes-to-neutral';
  if (!netControl) {
    return present.length === 1
      ? { teamId: present[0]![0], contested: false }
      : { teamId: null, contested: true };
  }

  const totalWeight = present.reduce(
    (total, [, weight]) => total + weight,
    0,
  );
  const positive = present.filter(
    ([, weight]) => weight > totalWeight - weight,
  );
  return positive.length === 1
    ? { teamId: positive[0]![0], contested: false }
    : { teamId: null, contested: present.length > 1 };
}

type LegacyZone = {
  tiles: Set<string>;
  cumulative: Map<ReplayStableUnitKey, number>[] | null;
};

function deriveLegacyZone(replay: ReplayModel): LegacyZone | null {
  if (replay.map.objectiveTiles.length === 0) return null;
  const tiles = new Set(
    replay.map.objectiveTiles.map((point) => `${point.x},${point.y}`),
  );
  if (replay.contract.rules.objective.sharedPressureEnabled) {
    return { tiles, cumulative: null };
  }

  const authoritative = replay.ticks.map((tick) =>
    tick.after.objective.kind === 'legacy'
      ? new Map(
          tick.after.objective.zoneTicks.map((entry) => [
            entry.unitKey,
            entry.ticks,
          ]),
        )
      : new Map<ReplayStableUnitKey, number>(),
  );
  if (authoritative.some((tally) => tally.size > 0)) {
    return { tiles, cumulative: authoritative };
  }

  // Historical replay-v1 omitted per-tick tallies. Preserve its viewer
  // compatibility derivation, selecting the accrual mode that agrees with the
  // authoritative terminal result.
  const sharedRun = new Map<ReplayStableUnitKey, number>();
  const exclusiveRun = new Map<ReplayStableUnitKey, number>();
  const shared: Map<ReplayStableUnitKey, number>[] = [];
  const exclusive: Map<ReplayStableUnitKey, number>[] = [];
  for (const tick of replay.ticks) {
    const on = objectiveOccupants(tick.after, { tiles, cumulative: null });
    for (const unitKey of on) {
      sharedRun.set(unitKey, (sharedRun.get(unitKey) ?? 0) + 1);
    }
    if (on.length === 1) {
      exclusiveRun.set(
        on[0],
        (exclusiveRun.get(on[0]) ?? 0) + 1,
      );
    }
    shared.push(new Map(sharedRun));
    exclusive.push(new Map(exclusiveRun));
  }
  const matchesResult = (
    series: Map<ReplayStableUnitKey, number>[],
  ): boolean =>
    replay.result !== null &&
    replay.result.teams.every((team) => {
      const unitKey = replay.units.find(
        (unit) => unit.teamId === team.teamId,
      )?.unitKey;
      return (
        unitKey !== undefined &&
        (series.at(-1)?.get(unitKey) ?? 0) === (team.zoneTicks ?? 0)
      );
    });
  return {
    tiles,
    cumulative: matchesResult(exclusive)
      ? exclusive
      : matchesResult(shared)
        ? shared
        : exclusive,
  };
}

function objectiveOccupants(
  world: ReplayWorldSnapshot,
  zone: LegacyZone,
): ReplayStableUnitKey[] {
  return world.actors
    .filter(
      (actor: ReplayActorState) =>
        actor.status === 'active' &&
        zone.tiles.has(`${actor.position.x},${actor.position.y}`),
    )
    .map((actor) => actor.unitKey);
}

const ARC_RELAY_BEAT_TICKS = 5;

type ArcRelayBeat = Omit<ArcRelayBeatPresentation, 'strength'>;

function arcCoreKey(core: { sourceWellId: string; sourceOrdinal: number }): string {
  return `${core.sourceWellId}:${core.sourceOrdinal}`;
}

function arcSourceLabel(wellId: string): string {
  const label = wellId.replace(/[-_]/g, ' ');
  return label.charAt(0).toUpperCase() + label.slice(1);
}

function arcActorName(
  replay: ReplayModel,
  actor: { unitKey: ReplayStableUnitKey },
): string {
  return unitName(replay, actor.unitKey);
}

function arcBeat(
  replay: ReplayModel,
  tick: number,
  fact: ReplayArcRelayFact,
  lastOwner: Map<string, { teamId: number; unitKey: ReplayStableUnitKey }>,
): ArcRelayBeat | null {
  switch (fact.kind) {
    case 'core-born':
      return {
        kind: 'birth',
        tick,
        headline: 'CORE BORN',
        detail: `${arcSourceLabel(fact.coreId.sourceWellId)} Well is live`,
        teamId: null,
        accent: null,
      };
    case 'core-picked-up': {
      const previous = lastOwner.get(arcCoreKey(fact.coreId));
      lastOwner.set(arcCoreKey(fact.coreId), fact.carrierActor);
      if (previous === undefined || previous.teamId === fact.carrierActor.teamId)
        return null;
      return {
        kind: 'steal',
        tick,
        headline: 'CORE STOLEN',
        detail: `${arcActorName(replay, fact.carrierActor)} takes the ${arcSourceLabel(fact.coreId.sourceWellId)} Core`,
        teamId: fact.carrierActor.teamId,
        accent: unitAccent(replay, fact.carrierActor.unitKey),
      };
    }
    case 'core-handed-off':
      lastOwner.set(arcCoreKey(fact.coreId), fact.targetActor);
      return null;
    case 'core-relocated':
      if (fact.carrierActor)
        lastOwner.set(arcCoreKey(fact.coreId), fact.carrierActor);
      return null;
    case 'core-dropped':
      lastOwner.set(arcCoreKey(fact.coreId), fact.sourceActor);
      return {
        kind: 'drop',
        tick,
        headline: 'CORE DROPPED',
        detail: `${arcSourceLabel(fact.coreId.sourceWellId)} Core is loose at ${fact.position.x},${fact.position.y}`,
        teamId: fact.sourceActor.teamId,
        accent: unitAccent(replay, fact.sourceActor.unitKey),
      };
    case 'core-banked':
      lastOwner.set(arcCoreKey(fact.coreId), fact.carrierActor);
      return {
        kind: 'bank',
        tick,
        headline: 'CORE BANKED',
        detail: `${teamName(replay, fact.teamId)} reaches ${fact.chargePips}/3 charge`,
        teamId: fact.teamId,
        accent: unitAccent(replay, fact.carrierActor.unitKey),
      };
    case 'pulse': {
      const unit = replay.units.find((candidate) =>
        candidate.teamId === fact.teamId,
      );
      return {
        kind: 'pulse',
        tick,
        headline: `PULSE ${fact.pulseOrdinal}`,
        detail: `${teamName(replay, fact.teamId)} hits the opposing reactor · ${fact.opposingReactorIntegrity} integrity left`,
        teamId: fact.teamId,
        accent: unit ? unitAccent(replay, unit.unitKey) : null,
      };
    }
    default:
      return null;
  }
}

function arcRelayBeatTimeline(replay: ReplayModel): ArcRelayBeat[] {
  if (replay.contract.kind !== 'v3-generic' || replay.contract.modeKind !== 'arc-relay')
    return [];
  const owner = new Map<
    string,
    { teamId: number; unitKey: ReplayStableUnitKey }
  >();
  const beats: ArcRelayBeat[] = [];
  for (const tick of replay.ticks) {
    for (const event of tick.events) {
      if (!event.arcRelayFact) continue;
      const beat = arcBeat(replay, tick.tick, event.arcRelayFact, owner);
      if (beat) beats.push(beat);
    }
  }
  return beats;
}

function arcRelayAt(
  replay: ReplayModel,
  tickIndex: number,
  beats: readonly ArcRelayBeat[],
): ArcRelayStoryPresentation | null {
  const tick = replay.ticks[tickIndex];
  const state = tick?.after.mode;
  if (!tick || state?.kind !== 'arc-relay' || !('wells' in state)) return null;

  const reactors = new Map(state.reactors.map((reactor) => [reactor.teamId, reactor]));
  const cores: ArcRelayCorePresentation[] = state.visibleCores.map((core) => {
    const carrier = core.carrierActor;
    const reactor = carrier ? reactors.get(carrier.teamId) : undefined;
    const distance = reactor
      ? Math.max(
          Math.abs(reactor.position.x - core.position.x),
          Math.abs(reactor.position.y - core.position.y),
        )
      : null;
    return {
      key: arcCoreKey(core.coreId),
      sourceLabel: arcSourceLabel(core.coreId.sourceWellId),
      position: core.position,
      disposition: core.disposition,
      carrierUnitKey: carrier?.unitKey ?? null,
      carrierTeamId: carrier?.teamId ?? null,
      carrierName: carrier ? arcActorName(replay, carrier) : null,
      distanceToBank: distance,
      pulseCore: reactor?.chargePips === 2,
    };
  });
  const carried = cores
    .filter((core) => core.carrierTeamId !== null)
    .sort((left, right) =>
      Number(right.pulseCore) - Number(left.pulseCore) ||
      (left.distanceToBank ?? Number.MAX_SAFE_INTEGER) -
        (right.distanceToBank ?? Number.MAX_SAFE_INTEGER) ||
      left.key.localeCompare(right.key),
    )[0];
  let cue: ArcRelayStoryPresentation['cue'];
  if (carried) {
    const unit = replay.units.find((candidate) =>
      candidate.unitKey === carried.carrierUnitKey,
    );
    cue = {
      headline: carried.pulseCore
        ? `${teamName(replay, carried.carrierTeamId!)} HAS THE PULSE CORE`
        : `${teamName(replay, carried.carrierTeamId!)} IS CARRYING`,
      detail: `${carried.sourceLabel} Core · ${carried.carrierName} · ${carried.distanceToBank} tiles from bank`,
      teamId: carried.carrierTeamId,
      accent: unit ? unitAccent(replay, unit.unitKey) : null,
    };
  } else if (cores.length > 0) {
    const loose = [...cores].sort((left, right) =>
      left.key.localeCompare(right.key),
    )[0]!;
    cue = {
      headline: `LOOSE ${loose.sourceLabel.toUpperCase()} CORE`,
      detail: `Contest at ${loose.position.x},${loose.position.y}`,
      teamId: null,
      accent: null,
    };
  } else {
    const next = [...state.wells]
      .filter((well) => well.nextScheduledBirthTick !== null)
      .sort((left, right) =>
        left.nextScheduledBirthTick! - right.nextScheduledBirthTick! ||
        left.wellId.localeCompare(right.wellId),
      )[0];
    cue = next
      ? {
          headline: `${arcSourceLabel(next.wellId).toUpperCase()} CORE NEXT`,
          detail: `Birth in ${Math.max(0, next.nextScheduledBirthTick! - tick.tick)} ticks`,
          teamId: null,
          accent: null,
        }
      : {
          headline: 'WELLS REARMING',
          detail: 'No live Core — teams are setting the next contest',
          teamId: null,
          accent: null,
        };
  }

  const latest = [...beats]
    .reverse()
    .find((candidate) =>
      candidate.tick <= tick.tick &&
      tick.tick - candidate.tick < ARC_RELAY_BEAT_TICKS,
    );
  return {
    kind: 'arc-relay',
    cue,
    cores,
    wells: state.wells.map((well) => ({
      wellId: well.wellId,
      sourceLabel: arcSourceLabel(well.wellId),
      position: well.position,
      nextBirthTick: well.nextScheduledBirthTick,
      outstanding: well.outstandingCoreId !== null,
    })),
    reactors: state.reactors.map((reactor) => {
      const unit = replay.units.find((candidate) =>
        candidate.teamId === reactor.teamId,
      );
      return {
        teamId: reactor.teamId,
        position: reactor.position,
        chargePips: reactor.chargePips,
        integritySegments: reactor.integritySegments,
        accent: unit ? unitAccent(replay, unit.unitKey) : '#94a3b8',
      };
    }),
    beat: latest
      ? {
          ...latest,
          strength: 1 - (tick.tick - latest.tick) / ARC_RELAY_BEAT_TICKS,
        }
      : null,
  };
}
