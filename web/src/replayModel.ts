/**
 * Version-neutral replay domain consumed by future viewer integrations.
 *
 * This model deliberately separates a stable unit from an exact actor life.
 * UI selection can follow a unit through respawns while projectiles, events,
 * decisions, and observations retain causal ownership by the exact life.
 */
import type { ReplayV3ResolvedContract } from './replayWireV3';

export type ReplaySourceVersion = 1 | 2 | 3;
export type ReplayObservationCompleteness = 'exact' | 'legacy-partial';
export type ReplayStateCompleteness = 'exact' | 'legacy-derived';

export type ReplayDirection = 'north' | 'east' | 'south' | 'west';
export type ReplayProjectileHeading =
  | ReplayDirection
  | 'north-east'
  | 'south-east'
  | 'south-west'
  | 'north-west';

export type ReplayActionResult =
  | 'none'
  | 'success'
  | 'blocked'
  | 'on-cooldown'
  | 'faulted'
  | (string & {});

export type ReplayStableUnitKey =
  | `duel:${number}:unit:0`
  | `frontline:${number}:unit:${number}`
  | `generic:${number}:unit:${number}`;

export type ReplayActorLifeKey =
  | `duel:${number}:unit:0:life:0`
  | `frontline:${number}:unit:${number}:life:${number}`
  | `generic:${number}:unit:${number}:life:${number}`;

export type ReplayTeamKey = `team:${number}`;
export type ReplayParticipantKey = `participant:${number}`;

export interface ReplayDuelActorIdentity {
  kind: 'duel';
  slot: number;
  teamId: number;
  unitId: 0;
  lifeId: 0;
  unitKey: ReplayStableUnitKey;
  actorKey: ReplayActorLifeKey;
}

export interface ReplayFrontlineActorIdentity {
  kind: 'frontline';
  teamId: number;
  unitId: number;
  lifeId: number;
  unitKey: ReplayStableUnitKey;
  actorKey: ReplayActorLifeKey;
}

export interface ReplayGenericActorIdentity {
  kind: 'generic';
  teamId: number;
  unitId: number;
  lifeId: number;
  unitKey: ReplayStableUnitKey;
  actorKey: ReplayActorLifeKey;
}

export type ReplayActorIdentity =
  | ReplayDuelActorIdentity
  | ReplayFrontlineActorIdentity
  | ReplayGenericActorIdentity;

export function replayTeamKey(teamId: number): ReplayTeamKey {
  return `team:${teamId}`;
}

export function replayParticipantKey(
  participantId: number,
): ReplayParticipantKey {
  return `participant:${participantId}`;
}

export function replayDuelIdentity(slot: number): ReplayDuelActorIdentity {
  return {
    kind: 'duel',
    slot,
    teamId: slot,
    unitId: 0,
    lifeId: 0,
    unitKey: `duel:${slot}:unit:0`,
    actorKey: `duel:${slot}:unit:0:life:0`,
  };
}

export function replayFrontlineIdentity(
  teamId: number,
  unitId: number,
  lifeId: number,
): ReplayFrontlineActorIdentity {
  return {
    kind: 'frontline',
    teamId,
    unitId,
    lifeId,
    unitKey: `frontline:${teamId}:unit:${unitId}`,
    actorKey: `frontline:${teamId}:unit:${unitId}:life:${lifeId}`,
  };
}

export function replayGenericIdentity(
  teamId: number,
  unitId: number,
  lifeId: number,
): ReplayGenericActorIdentity {
  return {
    kind: 'generic',
    teamId,
    unitId,
    lifeId,
    unitKey: `generic:${teamId}:unit:${unitId}`,
    actorKey: `generic:${teamId}:unit:${unitId}:life:${lifeId}`,
  };
}

export interface ReplayPosition {
  x: number;
  y: number;
}

export interface ReplayFormTransition {
  fromFormId: string;
  toFormId: string;
  startedAtTick: number;
  completesAtTick: number;
}

export type ReplayObjectiveMode =
  | 'none'
  | 'zone-ticks'
  | 'shared-pressure'
  | 'frontline';
export type ReplayScoreMetric = 'objective' | 'health' | 'damage-dealt';
export type ReplayTeamPerception = 'individual' | 'immediate-union';
export type ReplayActionParameterKind =
  | 'shot-program'
  | 'direction'
  | 'unit-target'
  | 'form-target'
  | 'projectile-heading'
  | 'upgrade-track'
  | 'position-target';
export type ReplayActionKind =
  | 'wait'
  | 'movement'
  | 'rotation'
  | 'attack'
  | 'fabrication'
  | 'transformation'
  | 'mode-investment'
  | (string & {});
/**
 * The events presentation surfaces key off, under both names they carry.
 *
 * `ReplayCausalEvent.type` is the *source document's* vocabulary, deliberately: the model
 * is version-neutral about structure, not about naming, and re-labelling a v3 `attack` as
 * a v1 `shot` during normalization would invent an equivalence the schemas do not state.
 * The cost is that a consumer comparing against one spelling silently stops firing on the
 * other generation — which is exactly what happened to every muzzle flash, kill flare,
 * recoil, death collapse, camera knock and sound cue the moment replay-v3 arrived: none of
 * them matched `attack`/`destruction`, and a generation-3 match played back as bolts
 * appearing from nothing and bodies quietly ceasing to exist.
 *
 * So the equivalence lives here, once, named, instead of in twelve string literals.
 */
export function isAttackEvent(type: string): boolean {
  return type === 'shot' || type === 'attack';
}

export function isDestructionEvent(type: string): boolean {
  return type === 'destroyed' || type === 'destruction';
}

/**
 * A life *arriving* — the opposite beat, and the one the viewer had no word for at all.
 *
 * Every generation says it differently and says it more than once: a Frontline replay
 * emits `respawned` when a prime returns to its authored spawn and `fabricated` when a
 * fabricator builds a child, while a generation-3 replay emits one `life-spawned` whose
 * payload `reason` carries which of `automatic-return`, `fabrication` or
 * `automatic-activation` it was. All of them mean the same thing to a spectator: a body
 * that was not there is there now, and it can act on this tick.
 *
 * The reason is presentation-relevant but not presentation-*deciding* — an arrival looks
 * like an arrival however it was caused — so it stays on the event for anything that wants
 * to phrase it, and nothing keys an effect off the spelling.
 */
export function isArrivalEvent(type: string): boolean {
  return (
    type === 'life-spawned' ||
    type === 'respawned' ||
    type === 'fabricated'
  );
}

/**
 * The same equivalence for the three the *score* reads. The music director counts
 * movement and rotation as activity and treats a destruction or a disqualification as a
 * decisive beat, so a generation-3 match scored as an empty field: no shots, no deaths,
 * nobody moving, and an adaptive timeline that never left `sparse`.
 */
export function isMovementEvent(type: string): boolean {
  return type === 'move' || type === 'movement';
}

export function isRotationEvent(type: string): boolean {
  return type === 'turn' || type === 'rotation';
}

export function isDisqualificationEvent(type: string): boolean {
  return type === 'disqualified' || type === 'participant-disqualified';
}

export type ReplayTickResolutionPhase =
  | 'freeze-observations'
  | 'collect-joint-decisions'
  | 'validate-actions'
  | 'rotate'
  | 'move'
  | 'advance-existing-projectiles'
  | 'launch-shots-and-apply-damage'
  | 'update-cooldowns-and-energy'
  | 'apply-runtime-faults'
  | 'update-objective'
  | 'resolve-match-completion'
  | 'apply-tick-start-lifecycle'
  | 'queue-destroyed-lives'
  | 'queue-fabrications'
  | 'start-form-transitions'
  | 'complete-form-transitions'
  | (string & {});

export interface ReplayContractLimits {
  maxTicks: number;
  faultLimit: number;
  teamCount: number;
  participantCount: number;
  unitSlotCount: number;
  initialUnitsPerTeam: number;
  maxUnitsPerTeam: number;
  destructionEndsMatch: boolean;
  respawnsEnabled: boolean;
}

export interface ReplayContractObjective {
  mode: ReplayObjectiveMode;
  zoneControlEnabled: boolean;
  zoneDominationTicks: number;
  zoneExclusiveAccrual: boolean;
  sharedPressureEnabled: boolean;
  controlBySoleOccupancy: boolean;
  controlPressureLimit: number;
  controlPressureGain: number;
  controlPressureDecayInterval: number;
  overtime: {
    startTick: number;
    pressureLimit: number;
    pressureGain: number;
    stopsDecay: boolean;
  };
  maxTickTiebreakers: ReplayScoreMetric[];
}

export interface ReplayContractFrontlineDefinition {
  teamCount: number;
  participantsPerTeam: number;
  frontlinePositionCount: number;
  initialUnitsPerTeam: number;
  maxUnitsPerTeam: number;
  teamPerception: ReplayTeamPerception;
  capture: {
    threshold: number;
    gainPerSoleTeamTick: number;
    decayAmount: number;
    decayIntervalTicks: number;
    redeployPauseTicks: number;
    pushesToBreach: number;
    presence: 'binary-positive-weight-per-team-no-stacking';
    nonSolePresence: 'decay-existing-claim';
    counterCapture: 'erode-to-neutral-before-claim';
  };
  victory: {
    initialPosition: 'centre-position-index';
    teamAdvances: {
      teamId: number;
      positionIndexDelta: number;
    }[];
    completionPrecedence: 'base-breach-before-max-ticks';
    timeoutResolution: 'signed-position-threshold-plus-claim-zero-draw-no-tiebreakers';
  };
  lifecycle: {
    primeRespawnTicks: number;
    childRebuildTicks: number;
    fabricationUnlockTicks: number[];
  };
  deployment: {
    primeDefaultFormId: string;
    childDefaultFormId: string;
    destructionTransitionClock: 'tick-start-at-destroyed-tick-plus-one-plus-delay';
    primeReturn: 'automatic-at-authored-prime-spawn';
    childReturn: 'ready-then-explicit-fabrication';
    newLife: 'fresh-runtime-form-defaults-home-facing-can-act-on-creation-tick';
    primeSpawnReservation: 'permanent-against-own-children';
    protectedPad: 'enemy-ground-entry-blocked-no-damage-immunity-no-projectile-blocking';
  };
  fabrication: {
    enabled: boolean;
    actionId: string;
    fabricatorUnitId: number;
    fabricatorFormId: string;
    targetPolicy: 'own-ready-child-slot';
    activationRegion: 'own-protected-spawn-pad';
    consumesTick: boolean;
    spawnDelayTicks: number;
    capacityEvaluation: 'post-movement-during-queue-fabrications';
    spawnRegion: 'own-protected-spawn-pad-excluding-prime-spawn';
    spawnSelection: 'first-unoccupied-unreserved-canonical-y-x';
    spawnFacing: 'own-prime-spawn-facing';
    unavailableSpawnResult: 'blocked' | 'faulted' | 'rejected';
    requiresExplicitRefabricationAfterRebuild: boolean;
  };
  anchor: {
    actionId: string;
    sourceFormId: string;
    targetFormId: string;
    windupTicks: number;
    consumesTick: boolean;
    completion: 'end-of-started-tick-plus-windup-minus-one-after-objective';
    pendingActions: 'wait-only';
    survivingDamage: 'does-not-cancel';
    death: 'cancels-with-explicit-event';
    forbiddenTiles: 'all-map-anchor-forbidden-tiles-illegal';
    pendingForm: 'source-form-until-completion';
    healthGain: number;
    healthTransition: 'minimum-target-maximum-and-current-plus-gain';
    stateContinuity: 'same-life-runtime-memory-position-facing-cooldown-energy-and-damage';
    terminal: 'preserve-future-pending-without-synthetic-cancellation';
    irreversibleForLife: boolean;
  };
  turretFire: {
    actionId: string;
    formId: string;
    allowedProjectileHeadings: ReplayProjectileHeading[];
    aim: 'absolute-eight-way-launch-heading';
    projectile: 'one-straight-non-programmed-projectile';
    facing: 'body-facing-unchanged';
    range: 'global-projectile-range';
    resources: 'standard-energy-cooldown-and-damage';
    traversal: 'standard-traversal-strict-diagonal-corners';
  };
  alliedCombat: {
    friendlyFireEnabled: boolean;
    alliedProjectilesBlock: boolean;
    projectileAttribution: 'exact-firing-life-persists-credits-stable-unit-by-actual-health-removed';
  };
}

export interface ReplayContractEnergyRules {
  enabled: boolean;
  maxEnergy: number;
  shotEnergyCost: number;
  regenerationIntervalTicks: number;
  regenerationAmount: number;
}

export interface ReplayContractForm {
  id: string;
  maxHealth: number;
  visionRange: number;
  shootCooldownTicks: number;
  omnidirectionalVision: boolean;
  omnidirectionalShooting: boolean;
  movementLayer: string;
  objectiveWeight: number;
  canMove: boolean;
  canShoot: boolean;
  allowsProgrammedShots: boolean;
  allowedActionIds: string[];
}

export interface ReplayContractAction {
  id: string;
  code: number;
  kind: ReplayActionKind;
  parameterKinds: ReplayActionParameterKind[];
  enabled: boolean;
}

export interface ReplayContractProjectileRules {
  mode: 'instant-ray' | 'discrete';
  damagePerHit: number;
  maxTravelTiles: number;
  shootCooldownTicks: number;
  ticksPerAdvance: number;
  tilesPerAdvance: number;
  launchTiles: number;
  advancesOnLaunchTick: boolean;
  damageAppliedSimultaneously: boolean;
}

export interface ReplayContractShotProgramRules {
  enabled: boolean;
  headingSectors: number;
  bendStepOctants: number;
  minInitialAimOctants: number;
  maxInitialAimOctants: number;
  aimOnlyProgram: Omit<ReplayShotProgram, 'initialAimOffset'>;
  allowedCurvedBendDirections: number[];
  minBendAfterTiles: number;
  maxBendAfterTiles: number;
  minBendEveryTiles: number;
  maxBendEveryTiles: number;
  minBendCount: number;
  maxBendCount: number;
  launchTiles: number;
  payloadOptional: boolean;
  defaultProgram: ReplayShotProgram;
  invalidPayloadResult: 'blocked' | 'faulted' | 'rejected' | null;
  unsupportedPayloadResult: 'blocked' | 'faulted' | 'rejected';
  diagonalCornersMustBeClear: boolean;
}

export interface ReplayContractVisionRules {
  range: number;
  distanceMetric: 'chebyshev';
  shape: 'omnidirectional' | 'facing-quadrant';
  omnidirectionalProximityRange: number;
  lineOfSight: 'corner-strict-supercover';
  hearingRadius: number;
  hearingBearingSectors: number;
  hearingDistanceBandUpperBounds: number[];
  loudEventTypes: string[];
}

export interface ReplayContractCollisionRules {
  unitsBlockWalls: boolean;
  unitsBlockUnits: boolean;
  sameDestinationMovesBlockAll: boolean;
  swapMovesBlocked: boolean;
  followingVacatedUnitAllowed: boolean;
  projectilesBlockMovement: boolean;
  movingOntoProjectileCausesHit: boolean;
  wallsConsumeProjectiles: boolean;
  projectilesIgnoreOwner: boolean;
  projectilesStopOnFirstNonOwnerUnit: boolean;
  projectilesCollideWithProjectiles: boolean;
}

export interface ReplayContractTickResolutionRules {
  observationsUsePreTickState: boolean;
  decisionsResolveAsJointStep: boolean;
  phases: ReplayTickResolutionPhase[];
}

export interface ReplayExactRulesContract {
  schemaVersion: number;
  rulesetId: string;
  rulesFingerprint: string;
  limits: ReplayContractLimits;
  objective: ReplayContractObjective;
  frontlineDefinition: ReplayContractFrontlineDefinition | null;
  energy: ReplayContractEnergyRules;
  forms: ReplayContractForm[];
  actions: ReplayContractAction[];
  projectiles: ReplayContractProjectileRules;
  shotPrograms: ReplayContractShotProgramRules;
  vision: ReplayContractVisionRules;
  collisions: ReplayContractCollisionRules;
  tickResolution: ReplayContractTickResolutionRules;
}

export interface ReplayContractMapSpawn {
  teamId: number;
  position: ReplayPosition;
  facing: ReplayDirection;
}

export interface ReplayContractMap {
  schemaVersion: number;
  mapId: string;
  mapVersion: number;
  mapFingerprint: string;
  formatVersion: number;
  width: number;
  height: number;
  tileRows: string[];
  spawns: ReplayContractMapSpawn[];
  objectiveTiles: ReplayPosition[];
  frontline: ReplayFrontlineMap | null;
}

export interface ReplayContractTopology {
  teamCount: number;
  participantCount: number;
  unitSlotCount: number;
  initialLifeCount: number;
  teams: {
    teamId: number;
    teamKey: ReplayTeamKey;
    classId: string | null;
  }[];
  participants: {
    participantId: number;
    participantKey: ReplayParticipantKey;
    teamId: number;
    teamKey: ReplayTeamKey;
    classId: string | null;
  }[];
  unitSlots: {
    teamId: number;
    teamKey: ReplayTeamKey;
    unitId: number;
    unitKey: ReplayStableUnitKey;
    controllerParticipantId: number;
    controllerParticipantKey: ReplayParticipantKey;
  }[];
  initialLives: {
    teamId: number;
    unitId: number;
    lifeId: number;
    actorKey: ReplayActorLifeKey;
    unitKey: ReplayStableUnitKey;
    formId: string;
  }[];
}

export interface ReplayExactMatchContract {
  kind: 'v2-full';
  completeness: 'exact';
  schemaVersion: number;
  matchContractFingerprint: string;
  rules: ReplayExactRulesContract;
  map: ReplayContractMap;
  topology: ReplayContractTopology;
}

/**
 * A replay-v3 contract keeps the canonical generic actor contract intact while
 * also exposing the small compatibility projection consumed by today's
 * viewer. New actions, forms, modes, and policy fields remain available in
 * rawContract without requiring the viewer model to predict them.
 */
export type ReplayGenericModeDefinition =
  | {
      kind: 'deathmatch';
      modeId: string;
    }
  | {
      kind: 'frontline';
      modeId: string;
      frontlinePositionCount: number;
      pushesToBreach: number;
      capture: {
        threshold: number;
        gainPerSoleTeamTick: number;
        gainSchedule?: {
          phaseId: string;
          startsAtTick: number;
          gainPerSoleTeamTick: number;
        }[];
        decayAmount: number;
        decayIntervalTicks: number;
        redeployPauseTicks: number;
      };
      orderedObjectiveRegionIds: string[];
      teamAdvances: {
        teamId: number;
        positionIndexDelta: -1 | 1;
      }[];
    }
  | {
      kind: 'arc-relay';
      modeId: string;
      pendingRearmTicks: number;
      coreRelocationIntervalTicks: number;
      coresPerPulse: number;
      pulsesToDestroyReactor: number;
      orderedWellRegionIds: string[];
    };

export interface ReplayGenericMatchContract
  extends Omit<ReplayExactMatchContract, 'kind'> {
  kind: 'v3-generic';
  modeKind: ReplayGenericModeDefinition['kind'];
  modeId: string;
  mode: ReplayGenericModeDefinition;
  rawContract: ReplayV3ResolvedContract;
}

export interface ReplayLegacyPartialRulesContract {
  schemaVersion: null;
  rulesetId: string;
  rulesFingerprint: null;
  limits: {
    maxTicks: number;
    faultLimit: null;
    teamCount: number;
    participantCount: number;
    unitSlotCount: number;
    initialUnitsPerTeam: number;
    maxUnitsPerTeam: number;
    destructionEndsMatch: null;
    respawnsEnabled: null;
  };
  objective: {
    mode: Exclude<ReplayObjectiveMode, 'frontline'>;
    zoneTiles: ReplayPosition[] | null;
    zoneDominationTicks: null;
    zoneExclusiveAccrual: null;
    sharedPressureEnabled: boolean;
    controlBySoleOccupancy: boolean | null;
    controlPressureLimit: number | null;
    controlPressureGain: null;
    controlPressureDecayInterval: null;
    overtime: {
      startTick: number | null;
      pressureLimit: number | null;
      pressureGain: number | null;
      stopsDecay: boolean | null;
    };
    maxTickTiebreakers: null;
  };
  frontlineDefinition: null;
  energy: null;
  forms: null;
  actions: null;
  projectiles: null;
  shotPrograms: {
    enabled: boolean | null;
    limits: {
      maxInitialAimOctants: number;
      maxBendAfterTiles: number;
      maxBendEveryTiles: number;
      maxBendCount: number;
      maxPathTiles: number;
      launchTiles: number;
      tilesPerAdvance: number;
    } | null;
  };
  vision: {
    range: number;
    shape: 'omnidirectional' | 'facing-quadrant' | null;
    distanceMetric: null;
    omnidirectionalProximityRange: null;
    lineOfSight: null;
    hearingRadius: null;
    hearingBearingSectors: null;
    hearingDistanceBandUpperBounds: null;
    loudEventTypes: null;
  };
  collisions: null;
  tickResolution: null;
  legacyMaxHealth: number | null;
}

export interface ReplayLegacyPartialMapContract {
  schemaVersion: null;
  mapId: string;
  mapVersion: number;
  mapFingerprint: null;
  formatVersion: null;
  width: number;
  height: number;
  tileRows: string[];
  spawns: ReplayContractMapSpawn[];
  objectiveTiles: ReplayPosition[] | null;
  frontline: null;
}

export interface ReplayLegacyPartialMatchContract {
  kind: 'legacy-partial';
  completeness: 'legacy-partial';
  schemaVersion: null;
  matchContractFingerprint: null;
  rules: ReplayLegacyPartialRulesContract;
  map: ReplayLegacyPartialMapContract;
  topology: ReplayContractTopology;
}

export type ReplayMatchContract =
  | ReplayExactMatchContract
  | ReplayGenericMatchContract
  | ReplayLegacyPartialMatchContract;

export interface ReplayParticipantController {
  participantKey: ReplayParticipantKey;
  participantId: number;
  teamKey: ReplayTeamKey;
  teamId: number;
  classId: string | null;
  name: string;
  runtimeKind: string;
  artifactHash: string | null;
  accent: string;
  lookId: string | null;
  projectileLookId: string | null;
}

export interface ReplayTeam {
  teamKey: ReplayTeamKey;
  teamId: number;
  classId: string | null;
  participantKeys: ReplayParticipantKey[];
  unitKeys: ReplayStableUnitKey[];
}

export interface ReplayStableUnit {
  unitKey: ReplayStableUnitKey;
  teamKey: ReplayTeamKey;
  teamId: number;
  unitId: number;
  controllerParticipantKey: ReplayParticipantKey;
  controllerParticipantId: number;
  initialActorKey: ReplayActorLifeKey | null;
  initialLifeId: number | null;
  initialFormId: string | null;
  /** Fixed per-slot launch class when the resolved topology declares one. */
  classId?: string | null;
}

export interface ReplayForm {
  formId: string;
  maxHealth: number;
  visionRange: number;
  shootCooldownTicks: number | null;
  omnidirectionalVision: boolean;
  omnidirectionalShooting: boolean;
  movementLayer: string;
  objectiveWeight: number;
  canMove: boolean;
  canShoot: boolean;
  allowsProgrammedShots: boolean;
  allowedActionIds: string[] | null;
  lookId?: string | null;
  projectileLookId?: string | null;
  completeness: ReplayStateCompleteness;
}

export interface ReplayMapPresentation {
  themeId: string | null;
  boundaryWall: string | null;
  interiorWall: string | null;
  wallGroups:
    | {
        family: string;
        tiles: ReplayPosition[];
      }[]
    | null;
}

export interface ReplayFrontlineMap {
  positions: {
    positionIndex: number;
    tiles: ReplayPosition[];
  }[];
  teamHomes: {
    teamId: number;
    primeSpawn: ReplayPosition & { facing: ReplayDirection };
    protectedSpawnPad: ReplayPosition[];
  }[];
  anchorForbiddenTiles: ReplayPosition[];
}

export interface ReplayMapRegion {
  regionId: string;
  tiles: ReplayPosition[];
}

export interface ReplayMap {
  mapId: string;
  mapVersion: number;
  formatVersion: number;
  width: number;
  height: number;
  tileRows: string[];
  objectiveTiles: ReplayPosition[];
  /**
   * Named map regions from the generic contract (wells, reactors, heal
   * zones, …), verbatim. Empty on replay generations whose wire format
   * carries no regions (v1/v2); renderers select by regionId prefix and
   * must tolerate absence.
   */
  regions: ReplayMapRegion[];
  frontline: ReplayFrontlineMap | null;
  presentation: ReplayMapPresentation | null;
}

export type ReplayUnitLifecycleStatus =
  | 'active'
  | 'respawning'
  | 'locked'
  | 'ready'
  | 'fabrication-queued'
  | 'rebuilding'
  | 'destroyed'
  | 'disqualified'
  | (string & {});

export type ReplayActorSpawnReason =
  | 'initial'
  | 'respawn'
  | 'rebuild'
  | 'fabrication'
  | 'legacy'
  | (string & {});

export interface ReplayActorState {
  identity: ReplayActorIdentity;
  actorKey: ReplayActorLifeKey;
  unitKey: ReplayStableUnitKey;
  formId: string;
  position: ReplayPosition;
  facing: ReplayDirection;
  health: number;
  cooldown: number;
  energy: number | null;
  /** Exact canonical decimal total, or null when replay-v1 did not expose it. */
  damageDealt: string | null;
  previousActionResult: ReplayActionResult;
  spawnedAtTick: number | null;
  participantId?: number;
  generation?: number;
  spawnReason?: ReplayActorSpawnReason;
  parentActor?: ReplayActorIdentity | null;
  sourceTransitionId?: string | null;
  sourceOperationId?: string | null;
  pendingFormTransition: ReplayFormTransition | null;
  status: ReplayUnitLifecycleStatus;
}

export interface ReplayUnitState {
  unitKey: ReplayStableUnitKey;
  teamKey: ReplayTeamKey;
  teamId: number;
  unitId: number;
  defaultFormId: string;
  /** Effective form: the active life's current form, or the slot default. */
  formId: string;
  lifecycleStatus: ReplayUnitLifecycleStatus;
  respawnAtTick: number | null;
  unlockAtTick: number | null;
  rebuildReadyAtTick: number | null;
  fabricationAtTick: number | null;
  reservedSpawn: ReplayPosition | null;
  pendingSpawnReason: ReplayActorSpawnReason | null;
  hasSpawned: boolean;
  nextLifeId: number | null;
  /** Exact canonical decimal total, or null when replay-v1 did not expose it. */
  damageDealt: string | null;
  activeActorKey: ReplayActorLifeKey | null;
}

export interface ReplayTeamState {
  teamKey: ReplayTeamKey;
  teamId: number;
  /** Exact canonical decimal total, or null when replay-v1 did not expose it. */
  damageDealt: string | null;
  unitKeys: ReplayStableUnitKey[];
}

export interface ReplayParticipantStatus {
  participantKey: ReplayParticipantKey;
  participantId: number;
  teamKey: ReplayTeamKey;
  teamId: number;
  classId: string | null;
  runtimeFaultCount: string;
  disqualified: boolean;
}

export interface ReplayScoreValue {
  channel: string;
  /** Canonical signed decimal text. */
  value: string;
}

export interface ReplayTeamScore {
  teamKey: ReplayTeamKey;
  teamId: number;
  eligible: boolean;
  scores: ReplayScoreValue[];
}

export interface ReplayScoreboard {
  teams: ReplayTeamScore[];
}

export type ReplayModeState =
  | {
      kind: 'deathmatch';
      modeId: string;
    }
  | ReplayArcRelayModeState
  | {
      kind: 'frontline';
      modeId: string;
      activePositionIndex: number;
      claimingTeamId: number | null;
      captureProgress: number;
      decayTicksElapsed: number;
      controlResumesAtTick: number;
      /**
       * Team whose advance a live territory-ratchet hold protects, or null
       * when no hold is live — which includes every ruleset whose redeploy
       * policy has no ratchet at all. Only the generic contract carries the
       * fact, so it is absent on replays normalized from older wires.
       */
      holdOwnerTeamId?: number | null;
      /** First tick the live hold stops denying regression; null when none. */
      holdEndsAtTick?: number | null;
      /**
       * Team that owns the declared side objective, or null while it is
       * neutral — which includes every ruleset that declares none. Only a
       * generation-3 contract carries the fact, so it is absent on replays
       * normalized from older wires.
       */
      secondaryOwnerTeamId?: number | null;
      /**
       * Running claim on the side objective as signed sole-presence ticks:
       * positive for team 0, negative for team 1, zero when none stands.
       */
      secondaryClaimProgress?: number;
      /**
       * Both teams' bank and tier vector under a declared scrap economy,
       * ordered by team ID. Undefined on every ruleset without one.
       */
      scrapTeams?: ReplayScrapTeam[];
      /** Live piles of loose scrap, ordered by (y, x). */
      scrapPiles?: ReplayScrapPile[];
    }
  | {
      kind: string;
      modeId: string;
      state: Readonly<Record<string, unknown>>;
    };

export interface ReplayArcCoreId {
  sourceWellId: string;
  sourceOrdinal: number;
}

export interface ReplayArcRelayModeState {
  kind: 'arc-relay';
  modeId: string;
  wells: {
    wellId: string;
    position: ReplayPosition;
    nextScheduledBirthTick: number | null;
    outstandingCoreId: ReplayArcCoreId | null;
    pendingCharge: boolean;
    rearmCompletesAtTick: number | null;
  }[];
  reactors: {
    teamId: number;
    position: ReplayPosition;
    chargePips: number;
    integritySegments: number;
    /** Threefold sockets in canonical well order; empty otherwise. */
    filledSocketWellIds: string[];
  }[];
  visibleCores: {
    coreId: ReplayArcCoreId;
    position: ReplayPosition;
    disposition: 'loose' | 'carried' | 'in-flight';
    carrierActor: ReplayActorIdentity | null;
    nextRelocationTick: number;
    flightTarget: ReplayPosition | null;
    flightCompletesAtTick: number | null;
  }[];
  visibleSignatures: {
    operationId: string;
    signatureId: string;
    signatureKind: string;
    ownerActor: ReplayActorIdentity;
    ownerTeamId: number;
    phase: 'tell' | 'active' | 'channel' | 'in-flight';
    startedTick: number;
    completesAtTick: number | null;
    endsAtTick: number | null;
    positions: ReplayPosition[];
    targetActor: ReplayActorIdentity | null;
    remainingCapacity: number;
    suppressed: boolean;
  }[];
  latestPulseTeamId: number | null;
  latestPulseTick: number | null;
  /**
   * Declared strikes in windup (DECISIONS #212): the shooter, the tick the
   * ray resolves, and the frozen tiles it will trace. Empty on every ruleset
   * without strike windups.
   */
  pendingStrikes: {
    shooter: ReplayActorIdentity;
    resolveAtTick: number;
    tiles: ReplayPosition[];
  }[];
}

export interface ReplayProjectileState {
  projectileId: string;
  ownerActor: ReplayActorIdentity;
  ownerActorKey: ReplayActorLifeKey;
  position: ReplayPosition;
  launchDirection: ReplayProjectileHeading;
  heading: ReplayProjectileHeading | null;
  shotProgram: ReplayShotProgram | null;
  programmedPath: ReplayPosition[] | null;
  ticksUntilAdvance: number | null;
  remainingTiles: number | null;
  tilesPerAdvance: number | null;
  nextProgrammedPathIndex: number | null;
  tilesTraveled: number | null;
  phase: number | null;
  ownerParticipantId?: number;
  attackProfileId?: string;
  spawnedAtTick?: number;
  origin?: ReplayPosition;
  committedPath?: ReplayPosition[];
}

export interface ReplayLegacyObjectiveState {
  kind: 'legacy';
  mode: 'none' | 'zone-ticks' | 'shared-pressure';
  controlPressure: number | null;
  zoneTicks: {
    unitKey: ReplayStableUnitKey;
    ticks: number;
  }[];
  completeness: 'legacy-derived';
}

export interface ReplayFrontlineObjectiveState {
  kind: 'frontline';
  nextTick: number;
  activePositionIndex: number;
  claimingTeamId: number | null;
  captureProgress: number;
  decayTicksElapsed: number;
  controlResumesAtTick: number;
  holdOwnerTeamId?: number | null;
  holdEndsAtTick?: number | null;
  winnerTeamId: number | null;
  completeness: 'exact';
}

export type ReplayObjectiveState =
  | ReplayLegacyObjectiveState
  | ReplayFrontlineObjectiveState;

export interface ReplayWorldSnapshot {
  completeness: ReplayStateCompleteness;
  participants?: ReplayParticipantStatus[];
  teams: ReplayTeamState[];
  units: ReplayUnitState[];
  actors: ReplayActorState[];
  projectiles: ReplayProjectileState[] | null;
  scoreboard?: ReplayScoreboard;
  mode?: ReplayModeState;
  objective: ReplayObjectiveState;
}

export interface ReplayShotProgram {
  initialAimOffset: number;
  bendDirection: number;
  bendAfterTiles: number;
  bendEveryTiles: number;
  bendCount: number;
}

export interface ReplayObservedUnit {
  unitKey: ReplayStableUnitKey;
  teamId: number;
  unitId: number;
  formId: string;
  lifecycleStatus: ReplayUnitLifecycleStatus;
  activeActor: ReplayActorIdentity | null;
  respawnAtTick: number | null;
  unlockAtTick: number | null;
  rebuildReadyAtTick: number | null;
  fabricationAtTick: number | null;
}

export interface ReplayOpaqueEnemyActorRef {
  kind: 'opaque-enemy';
  teamId: number;
  unitId: number;
  lifeHandle: string;
}

export interface ReplayExactObservedActorRef {
  kind: 'exact';
  identity: ReplayActorIdentity;
}

export type ReplayObservedActorRef =
  | ReplayExactObservedActorRef
  | ReplayOpaqueEnemyActorRef;

export interface ReplayObservedActor {
  actor: ReplayObservedActorRef;
  classId: string | null;
  formId: string;
  position: ReplayPosition;
  facing: ReplayDirection;
  health: number;
  cooldown: number | null;
  energy: number | null;
  previousActionResult: ReplayActionResult | null;
  pendingFormTransition: ReplayFormTransition | null;
  observedBy: ReplayActorLifeKey[];
  /**
   * Scrap this body is carrying, under a declared scrap economy.
   *
   * Zero rather than absent once normalized: the wire omits the key while a
   * body carries nothing, and every wire without the economy omits it always,
   * so an absent key and an empty load are the same picture to a viewer. This
   * is the **only** place the fact exists — authoritative world lives carry no
   * load, so a courier is readable only through the observations of the tick
   * that follows the pickup.
   */
  carriedScrap: number;
  /**
   * The free-vocabulary label this body's MIND published for it, or null.
   *
   * Entirely non-authoritative — the engine never reads it — and public on
   * visible enemies as well as own bodies, which is what makes it worth
   * rendering: a spectator reading `channeler / screen / screen / courier`
   * understands the set-piece without being taught the rules, and a
   * deliberately wrong label is a real move. Null for every per-life replay,
   * which has no way to set one.
   */
  roleTag: string | null;
}

export interface ReplayObservedTile {
  position: ReplayPosition;
  isWall: boolean | null;
  observedBy: ReplayActorLifeKey[];
  spawnReservation: ReplayObservedSpawnReservation | null;
}

export interface ReplayObservedSpawnReservation {
  teamId: number;
  unitId: number;
  unitKey: ReplayStableUnitKey;
  kind: 'automatic-return' | 'fabrication' | 'replication';
  dueTick: number | null;
}

export interface ReplayObservedProjectile {
  /** Opaque match-local observation handle; null only for replay-v1. */
  projectileHandle: string | null;
  ownerTeamId: number;
  /** Exact owner when replay-v3 exposes it, regardless of team. */
  ownerActor?: ReplayActorIdentity | null;
  alliedOwnerActor: ReplayActorIdentity | null;
  visibleEnemyOwner: ReplayOpaqueEnemyActorRef | null;
  position: ReplayPosition;
  heading: ReplayProjectileHeading;
  tilesPerAdvance: number;
  ticksUntilAdvance: number;
  remainingTiles: number;
  observedBy: ReplayActorLifeKey[];
  /** Exact authoritative projectile identity in replay-v3. */
  projectileId?: string;
  /**
   * Declared tick cadence between advances for the profile that fired this
   * projectile. Generation-3 observations publish it; older wires do not.
   */
  ticksPerAdvance?: number;
  /** Health one contact removes. Generation-3 observations publish it. */
  damagePerHit?: number;
}

export interface ReplayObservedEvent {
  /** Opaque observation handle; authoritative IDs live in the alias sidecar. */
  eventHandle: string | null;
  sourceTick: number;
  type: string;
  teamId: number | null;
  alliedActor: ReplayActorIdentity | null;
  enemyActor: ReplayOpaqueEnemyActorRef | null;
  projectileHandle: string | null;
  position: ReplayPosition | null;
  facing: ReplayDirection | null;
  projectileHeading: ReplayProjectileHeading | null;
  fromFormId: string | null;
  toFormId: string | null;
  formTransitionStartedAtTick: number | null;
  formTransitionCompletesAtTick: number | null;
  actionId: string | null;
  actionCode: number | null;
  formTargetId: string | null;
  actionResult: ReplayActionResult | null;
  amount: number | null;
  newHealth: number | null;
  observedBy: ReplayActorLifeKey[];
  sourceOrdinal?: number;
  payloadKind?: string;
}

export interface ReplayObservedSound {
  eventHandle: string | null;
  sourceTick: number | null;
  observerActor: ReplayActorIdentity;
  type: string;
  bearing: number;
  distance: number;
}

export interface ReplayObservedActionAvailability {
  actionId: string;
  actionCode: number;
  parameterKinds: string[];
  enabled: boolean;
  available: boolean;
  shotProgramAvailable: boolean | null;
  allowedDirections: ReplayDirection[] | null;
  allowedProjectileHeadings: ReplayProjectileHeading[] | null;
  allowedUnitKeys: ReplayStableUnitKey[] | null;
  allowedFormTargets: string[] | null;
  allowedPositions?: ReplayPosition[] | null;
  /**
   * Upgrade tracks this body's team may buy the next tier of this tick, or
   * null when the action declares no such parameter. Affordability and the
   * caps live in the mask, so an empty array means "nothing is buyable right
   * now" rather than "no economy exists".
   */
  allowedUpgradeTracks?: string[] | null;
}

/** One team's published economic position under a declared scrap economy. */
export interface ReplayScrapTeam {
  teamId: number;
  bank: number;
  /** Tier held per track, positional against the declared track order. */
  tierLevels: number[];
}

/** One live pile of loose scrap; gone the first tick `tick >= expiresAtTick`. */
export interface ReplayScrapPile {
  position: ReplayPosition;
  amount: number;
  expiresAtTick: number;
}

export interface ReplayObservedFrontlineObjective {
  activePositionIndex: number;
  claimingTeamId: number | null;
  captureProgress: number;
  decayTicksElapsed: number;
  controlResumesAtTick: number;
  holdOwnerTeamId?: number | null;
  holdEndsAtTick?: number | null;
}

export interface ReplayActorObservation {
  completeness: ReplayObservationCompleteness;
  schemaVersion: number | null;
  tick: number;
  matchContractFingerprint: string | null;
  teamPerception: string | null;
  participants?: ReplayParticipantStatus[];
  self: ReplayObservedActor | null;
  teamUnits: ReplayObservedUnit[];
  allies: ReplayObservedActor[];
  enemies: ReplayObservedActor[];
  visibleTiles: ReplayObservedTile[];
  visibleProjectiles: ReplayObservedProjectile[] | null;
  visibleEvents: ReplayObservedEvent[];
  heardSounds: ReplayObservedSound[] | null;
  scoreboard?: ReplayScoreboard;
  mode?: ReplayModeState;
  frontlineObjective: ReplayObservedFrontlineObjective | null;
  actions: ReplayObservedActionAvailability[] | null;
}

export interface ReplayActionPayload {
  shotProgram: ReplayShotProgram | null;
  direction: ReplayDirection | null;
  launchHeading: ReplayProjectileHeading | null;
  unitKey: ReplayStableUnitKey | null;
  formTargetId: string | null;
  /** Exact targeted map tile for position-parameter actions in replay v3. */
  positionTarget?: ReplayPosition | null;
}

export interface ReplayActorDecision {
  actionId: string | null;
  actionCode: number | null;
  payload: ReplayActionPayload | null;
  debugMessage: string | null;
  faulted: boolean;
  faultMessage: string | null;
}

export interface ReplayActionResolution {
  chosenActionId: string;
  chosenActionCode: number | null;
  chosenPayload: ReplayActionPayload | null;
  validatedActionId: string;
  validatedActionCode: number | null;
  validatedPayload: ReplayActionPayload | null;
  result: ReplayActionResult;
  submittedActionId?: string | null;
  runtimeFault?: {
    participantId: number;
    actor: ReplayActorIdentity;
    stage: string;
    faultCode: string;
    cumulativeFaultCount: string;
    disqualificationTriggered: boolean;
  } | null;
}

export interface ReplayActorLifeStart {
  completeness: ReplayObservationCompleteness;
  schemaVersion: number | null;
  runtimeContractVersion: number | null;
  actor: ReplayActorIdentity;
  participantId: number;
  actorRandomSeed: string | null;
  /**
   * Root seed of the scoring team's shared per-tick stream — identical for
   * every life on the team. Null for a replay generation that predates it.
   */
  teamRandomSeed?: string | null;
  spawnReason: ReplayActorSpawnReason;
  generation?: number;
  parentActor?: ReplayActorIdentity | null;
  sourceTransitionId?: string | null;
  sourceOperationId?: string | null;
  matchContractFingerprint: string | null;
}

export interface ReplayObservationAliases {
  completeness: ReplayObservationCompleteness;
  enemyLives: {
    lifeHandle: string;
    actor: ReplayActorIdentity;
  }[];
  projectiles: {
    projectileHandle: string;
    projectileId: string;
  }[];
  events: {
    eventHandle: string;
    eventId: string;
  }[];
}

export interface ReplayActorTurn {
  actor: ReplayActorIdentity;
  actorKey: ReplayActorLifeKey;
  lifeStart: ReplayActorLifeStart | null;
  observation: ReplayActorObservation;
  aliases: ReplayObservationAliases;
  runtimeReply: ReplayActorDecision;
  acceptedDecision: ReplayActorDecision;
  actionResolution: ReplayActionResolution;
}

export interface ReplayCausalEvent {
  eventId: string;
  tick: number;
  ordinal: number;
  type: string;
  teamId: number | null;
  unitId: number | null;
  sourceActor: ReplayActorIdentity | null;
  targetActor: ReplayActorIdentity | null;
  projectileId: string | null;
  from: ReplayPosition | null;
  to: ReplayPosition | null;
  fromFacing: ReplayDirection | null;
  toFacing: ReplayDirection | null;
  projectileHeading: ReplayProjectileHeading | null;
  fromFormId: string | null;
  toFormId: string | null;
  formTransitionStartedAtTick: number | null;
  formTransitionCompletesAtTick: number | null;
  actionPayload: ReplayActionPayload | null;
  actionId: string | null;
  actionCode: number | null;
  actionResult: ReplayActionResult | null;
  amount: number | null;
  newHealth: number | null;
  lifecycleStatus: ReplayUnitLifecycleStatus | null;
  spawnReason: ReplayActorSpawnReason | null;
  respawnAtTick: number | null;
  unlockAtTick: number | null;
  rebuildReadyAtTick: number | null;
  fabricationAtTick: number | null;
  fromPositionIndex: number | null;
  toPositionIndex: number | null;
  claimingTeamId: number | null;
  captureProgress: number | null;
  controlResumesAtTick: number | null;
  completeness: ReplayObservationCompleteness;
  globalOrdinal?: string;
  payloadKind?: string;
  arcRelayFact?: ReplayArcRelayFact;
  audience?:
    | { kind: 'public' }
    | { kind: 'spatial'; primaryPosition: ReplayPosition }
    | { kind: 'team-private'; teamId: number };
}

export type ReplayArcRelayFact =
  | {
      kind: 'core-born';
      coreId: ReplayArcCoreId;
      position: ReplayPosition;
      chargeValue: number;
    }
  | {
      kind: 'core-ripened';
      coreId: ReplayArcCoreId;
      position: ReplayPosition;
      value: number;
    }
  | {
      kind: 'leveled-up';
      actor: ReplayActorIdentity;
      level: number;
      position: ReplayPosition;
    }
  | {
      kind: 'zone-healed';
      actor: ReplayActorIdentity;
      amount: number;
      newHealth: number;
      position: ReplayPosition;
    }
  | {
      kind: 'core-picked-up';
      coreId: ReplayArcCoreId;
      carrierActor: ReplayActorIdentity;
      position: ReplayPosition;
      nextRelocationTick: number;
    }
  | {
      kind: 'core-relocated';
      coreId: ReplayArcCoreId;
      carrierActor: ReplayActorIdentity | null;
      from: ReplayPosition;
      to: ReplayPosition;
      nextRelocationTick: number;
      relocationKind: string;
    }
  | {
      kind: 'core-handed-off';
      coreId: ReplayArcCoreId;
      sourceActor: ReplayActorIdentity;
      targetActor: ReplayActorIdentity;
      position: ReplayPosition;
      nextRelocationTick: number;
    }
  | {
      kind: 'core-dropped';
      coreId: ReplayArcCoreId;
      sourceActor: ReplayActorIdentity;
      position: ReplayPosition;
      nextRelocationTick: number;
      dropKind: string;
    }
  | {
      kind: 'core-banked';
      coreId: ReplayArcCoreId;
      carrierActor: ReplayActorIdentity;
      teamId: number;
      position: ReplayPosition;
      chargePips: number;
    }
  | {
      kind: 'well-changed';
      wellId: string;
      pendingCharge: boolean;
      rearmCompletesAtTick: number | null;
      outstandingCoreId: ReplayArcCoreId | null;
    }
  | {
      kind: 'pulse';
      teamId: number;
      pulseOrdinal: number;
      opposingReactorIntegrity: number;
    }
  | {
      kind: 'signature-changed';
      operationId: string;
      signatureId: string;
      ownerActor: ReplayActorIdentity;
      phase: string | null;
      reason: string;
    }
  | {
      kind: 'body-relocated';
      operationId: string;
      signatureId: string;
      ownerActor: ReplayActorIdentity;
      targetActor: ReplayActorIdentity;
      from: ReplayPosition;
      to: ReplayPosition;
    }
  | {
      kind: 'signature-damage' | 'signature-repair';
      operationId: string;
      signatureId: string;
      ownerActor: ReplayActorIdentity;
      targetActor: ReplayActorIdentity;
      amount: number;
      newHealth: number;
      position: ReplayPosition;
    };

export interface ReplayProjectileTraversal {
  projectileId: string;
  ownerActor: ReplayActorIdentity;
  ownerActorKey: ReplayActorLifeKey;
  launchDirection: ReplayProjectileHeading;
  from: ReplayPosition;
  path: ReplayPosition[];
  heading: ReplayProjectileHeading | null;
  shotProgram: ReplayShotProgram | null;
  programmedPath: ReplayPosition[] | null;
  globalOrdinal?: string;
  phase?: string;
  trigger?: string;
  ownerParticipantId?: number;
  ownerTeamId?: number;
  attackProfileId?: string;
  finalHeading?: ReplayProjectileHeading;
  terminal?: Readonly<Record<string, unknown>>;
}

export interface ReplayTick {
  tick: number;
  before: ReplayWorldSnapshot;
  activeActorKeys: ReplayActorLifeKey[];
  lifecycleEvents: ReplayCausalEvent[];
  actorTurns: ReplayActorTurn[];
  /**
   * A compact spectator transport can publish one shared vision snapshot per
   * team instead of repeating it in every body turn. Undefined on canonical
   * replays and compact broadcasts that predate team-perspective fog.
   */
  publishedTeamVision?: ReplayPublishedTeamVision[];
  events: ReplayCausalEvent[];
  projectileTraversals: ReplayProjectileTraversal[];
  after: ReplayWorldSnapshot;
}

export interface ReplayPublishedTeamVision {
  teamId: number;
  visibleTiles: ReplayPosition[];
}

export interface ReplayTeamResult {
  teamKey: ReplayTeamKey;
  teamId: number;
  outcome: 'win' | 'loss' | 'draw';
  activeHealth: number;
  damageDealt: string;
  units: ReplayUnitResult[];
  faults: number | null;
  zoneTicks: number | null;
  rank?: number;
  scores?: ReplayScoreValue[];
}

export interface ReplayUnitResult {
  unitKey: ReplayStableUnitKey;
  teamId: number;
  unitId: number;
  defaultFormId: string;
  formId: string;
  lifecycleStatus: ReplayUnitLifecycleStatus;
  activeActor: ReplayActorIdentity | null;
  activeActorKey: ReplayActorLifeKey | null;
  health: number;
  /** Null when the source format exposes only team-level damage. */
  damageDealt: string | null;
  pendingFormTransition: ReplayFormTransition | null;
  participantId?: number;
  generation?: number | null;
  nextLifeId?: number;
}

export interface ReplayDeathmatchResult {
  kind: 'deathmatch';
  reason: string;
  scores: {
    teamKey: ReplayTeamKey;
    teamId: number;
    kills: string;
    deaths: string;
    damageDealt: string;
  }[];
}

export interface ReplayFrontlineResult {
  kind: 'frontline';
  reason: 'fault-eligibility' | 'base-breach' | 'max-ticks';
  control: Extract<ReplayModeState, { kind: 'frontline' }>;
  scores: {
    teamKey: ReplayTeamKey;
    teamId: number;
    /** Canonical signed decimal text. */
    territorialProgress: string;
  }[];
}

export interface ReplayArcRelayResult {
  kind: 'arc-relay';
  reason: 'fault-eligibility' | 'reactor-destroyed' | 'max-ticks';
  state: ReplayArcRelayModeState;
}

export interface ReplayTerminalResult {
  winnerTeamId: number | null;
  reason: string;
  endTick: number;
  territorialScore: string | null;
  objective: ReplayObjectiveState;
  teams: ReplayTeamResult[];
  /** Exact wire value; null is permitted by the generic result contract. */
  reportedEndTick?: number | null;
  eligibleTeamIds?: number[];
  mode?: ReplayDeathmatchResult | ReplayFrontlineResult | ReplayArcRelayResult;
}

export interface ReplayHeaderVersions {
  engineVersion: string;
  gameRulesVersion: string;
  runtimeProtocolVersion: string | null;
  runtimeConfigurationVersion: string | null;
  actorRuntime: {
    family: string;
    protocolVersion: string;
    configurationVersion: string;
    version: number;
    matchStartSchemaVersion: number;
    observationSchemaVersion: number;
    decisionSchemaVersion: number;
  } | null;
}

export interface ReplayModel {
  sourceVersion: ReplaySourceVersion;
  versions: ReplayHeaderVersions;
  /**
   * Decimal seed text. Exact for replay-v2 and for replay-v1 decoded from raw
   * JSON through decodeReplayJson. Object-only replay-v1 decoding can only
   * preserve the already-rounded JavaScript number; consult seedExact.
   */
  seed: string;
  seedExact: boolean;
  seedEncoding: 'legacy-json-number' | 'decimal-string';
  partial: boolean;
  replayHash: string | null;
  matchContractFingerprint: string | null;
  contract: ReplayMatchContract;
  map: ReplayMap;
  forms: ReplayForm[];
  participants: ReplayParticipantController[];
  teams: ReplayTeam[];
  units: ReplayStableUnit[];
  /**
   * Null when a partial replay has no authoritative world snapshot yet.
   * Topology, map, forms, and participants remain available.
   */
  initialWorld: ReplayWorldSnapshot | null;
  initialLifeStarts?: ReplayActorLifeStart[];
  initialEvents?: ReplayCausalEvent[];
  ticks: ReplayTick[];
  result: ReplayTerminalResult | null;
}
