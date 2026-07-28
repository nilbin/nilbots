/**
 * Hand-maintained mirror of the explicit replay-v3 codec in
 * BotArena.Engine/ReplayV3Serializer.cs.
 *
 * Replay-v3 embeds the actor contract as canonical JSON. Its root,
 * capabilities, topology, map, forms, actions, and mode declaration are
 * modelled below; extensible rule-policy objects deliberately remain JSON
 * values. The replay envelope itself is closed and all nullable fields are
 * explicit.
 */

export type ReplayV3JsonPrimitive = string | number | boolean | null;
export type ReplayV3JsonValue =
  | ReplayV3JsonPrimitive
  | ReplayV3JsonValue[]
  | { [key: string]: ReplayV3JsonValue };
export type ReplayV3JsonObject = Record<string, unknown>;

export type ReplayV3Direction = 'north' | 'east' | 'south' | 'west';
export type ReplayV3ProjectileHeading =
  | ReplayV3Direction
  | 'north-east'
  | 'south-east'
  | 'south-west'
  | 'north-west';

export interface ReplayV3ActorId {
  teamId: number;
  unitId: number;
  lifeId: number;
}

export interface ReplayV3Position {
  x: number;
  y: number;
}

export type ReplayV3ContractPosition = [number, number];

export interface ReplayV3ShotProgram {
  initialAimOffset: number;
  bendDirection: number;
  bendAfterTiles: number;
  bendEveryTiles: number;
  bendCount: number;
}

export interface ReplayV3CapabilityVersions {
  contractProfileId: string;
  runtimeProtocolVersion: string;
  runtimeConfigurationVersion: string;
  runtimeContractVersion: number;
  matchStartSchemaVersion: number;
  observationSchemaVersion: number;
  decisionSchemaVersion: number;
  matchContractSchemaVersion: number;
}

export interface ReplayV3ContractForm {
  id: string;
  maxHealth: number;
  movementProfileId: string | null;
  visionProfileId: string;
  attackProfileId: string | null;
  objectiveWeight: number;
  allowedActionIds: string[];
}

export interface ReplayV3MovementProfile {
  id: string;
  movementLayer: string;
}

export interface ReplayV3VisionProfile {
  id: string;
  range: number;
  distanceMetric: string;
  shape: string;
  omnidirectionalProximityRange: number;
  lineOfSight: string;
  hearingRadius: number;
  hearingBearingSectors: number;
  hearingBearingModel: string;
  hearingDistanceBandModel: string;
  hearingDistanceBandUpperBounds: number[];
  loudEventKinds: string[];
}

export interface ReplayV3ProjectileDefinition {
  mode: string;
  damagePerHit: number;
  maxTravelTiles: number;
  ticksPerAdvance: number;
  tilesPerAdvance: number;
  launchTiles: number;
  advancesOnLaunchTick: boolean;
  damageAppliedSimultaneously: boolean;
  diagonalCornersMustBeClear: boolean;
}

export interface ReplayV3ShotProgramDefinition extends ReplayV3JsonObject {
  enabled: boolean;
  headingSectors: number;
  minInitialAimSteps: number;
  maxInitialAimSteps: number;
  launchTiles: number;
  payloadOptional: boolean;
  defaultProgram: ReplayV3ShotProgram;
  diagonalCornersMustBeClear: boolean;
}

export interface ReplayV3AttackProfile extends ReplayV3JsonObject {
  id: string;
  omnidirectionalAim: boolean;
  projectile: ReplayV3ProjectileDefinition;
  cooldownTicks: number;
  maxEnergy: number;
  attackEnergyCost: number;
  energyRegenerationIntervalTicks: number;
  energyRegenerationAmount: number;
  shotProgram: ReplayV3ShotProgramDefinition;
}

export interface ReplayV3ActionDefinition {
  id: string;
  code: number;
  kind: string;
  parameterKinds: string[];
}

export interface ReplayV3ScoreCatalogEntry {
  channel: string;
  domain: string;
}

export interface ReplayV3RankingRule {
  channel: string;
  direction: string;
}

export interface ReplayV3DeathmatchModeDefinition extends ReplayV3JsonObject {
  kind: 'deathmatch';
  modeId: string;
  victory: ReplayV3JsonObject & {
    kind: 'deathmatch';
    timeoutRanking: ReplayV3RankingRule[];
    killsToWin: number | null;
  };
  scoreCatalog: ReplayV3ScoreCatalogEntry[];
  scoring: ReplayV3JsonObject;
}

export interface ReplayV3FrontlineModeDefinition extends ReplayV3JsonObject {
  kind: 'frontline';
  modeId: string;
  victory: {
    kind: 'frontline';
    timeoutRanking: ReplayV3RankingRule[];
    pushesToBreach: number;
  };
  scoreCatalog: ReplayV3ScoreCatalogEntry[];
  frontlinePositionCount: number;
  capture: {
    threshold: number;
    gainPerSoleTeamTick: number;
    gainSchedule?: ReplayV3FrontlineCaptureGainPhase[];
    decayAmount: number;
    decayIntervalTicks: number;
    redeployPauseTicks: number;
    controlPolicy:
      'binary-positive-weight-per-team-no-stacking-non-sole-applies-configured-decay-opposition-erodes-to-neutral';
    timeoutPolicy:
      'signed-position-threshold-plus-claim-zero-draw-no-tiebreakers';
    territorialProgressFormula:
      'per-team-advance-delta-times-index-offset-times-threshold-plus-signed-claim';
    completionPolicy: 'base-breach-before-max-ticks';
    initialPosition: 'centre-objective-index';
    captureArithmetic:
      'checked-int64-add-compare-threshold-completes-one-push-and-discards-overshoot';
    oppositionArithmetic:
      'erode-toward-zero-without-carrying-overshoot-into-own-claim';
    decayClock:
      'consecutive-empty-or-contested-ticks-reset-by-any-sole-control';
    disabledDecay: 'zero-pair-preserves-claim-and-keeps-clock-zero';
    redeployPolicy:
      'advance-immediately-reset-claim-keep-world-pause-through-capture-plus-configured-ticks-breach-skips-pause';
    redeployTickArithmetic:
      'checked-int64-capture-tick-plus-one-plus-pause-require-int32';
  };
}

export interface ReplayV3FrontlineCaptureGainPhase
  extends ReplayV3JsonObject {
  phaseId: string;
  startsAtTick: number;
  gainPerSoleTeamTick: number;
}

export type ReplayV3GameModeDefinition =
  | ReplayV3DeathmatchModeDefinition
  | ReplayV3FrontlineModeDefinition;

export interface ReplayV3RulesContract {
  schemaVersion: number;
  rulesetId: string;
  rulesFingerprint: string;
  limits: ReplayV3JsonObject & { maxTicks: number };
  seedMechanics: ReplayV3JsonObject;
  gameMode: ReplayV3GameModeDefinition;
  lifecycle: ReplayV3JsonObject;
  forms: ReplayV3ContractForm[];
  movementProfiles: ReplayV3MovementProfile[];
  visionProfiles: ReplayV3VisionProfile[];
  attackProfiles: ReplayV3AttackProfile[];
  actions: ReplayV3ActionDefinition[];
  fabricationTransitions: ReplayV3JsonObject[];
  sameLifeTransitions: ReplayV3JsonObject[];
  replicationTransitions: ReplayV3JsonObject[];
  teamPerception: ReplayV3JsonObject & { kind: string };
  collisions: ReplayV3JsonObject;
  tickResolution: ReplayV3JsonObject & { phases: string[] };
}

export interface ReplayV3SpawnAnchor {
  spawnId: string;
  position: ReplayV3ContractPosition;
  facing: ReplayV3Direction;
  compatibleMovementLayers: string[];
}

export type ReplayV3MapRegionKind = 'objective' | 'transition-placement';

export interface ReplayV3MapRegion extends ReplayV3JsonObject {
  regionId: string;
  kind: ReplayV3MapRegionKind;
  tiles: ReplayV3ContractPosition[];
}

export type ReplayV3MapTileTagKind =
  | 'transition-placement-forbidden'
  | 'spawn-protected';

export interface ReplayV3MapTileTag extends ReplayV3JsonObject {
  tagId: string;
  kind: ReplayV3MapTileTagKind;
  tiles: ReplayV3ContractPosition[];
}

export interface ReplayV3MapContract {
  schemaVersion: number;
  mapId: string;
  mapVersion: number;
  mapFingerprint: string;
  formatVersion: number;
  width: number;
  height: number;
  tileRows: string[];
  spawnAnchors: ReplayV3SpawnAnchor[];
  regions: ReplayV3MapRegion[];
  tileTags: ReplayV3MapTileTag[];
}

export interface ReplayV3FormatContract extends ReplayV3JsonObject {
  schemaVersion: number;
  formatId: string;
  formatFingerprint: string;
  kind: string;
  scoringTeamCount: number;
  participantsPerTeam: number;
  participantCount: number;
}

export interface ReplayV3TopologyContract {
  schemaVersion: number;
  topologyFingerprint: string;
  counts: {
    teamCount: number;
    participantCount: number;
    unitSlotCount: number;
    initialLifeCount: number;
  };
  teams: { teamId: number }[];
  participants: { participantId: number; teamId: number }[];
  unitSlots: {
    teamId: number;
    unitId: number;
    controllerParticipantId: number;
  }[];
  initialLives: {
    teamId: number;
    unitId: number;
    lifeId: number;
    formId: string;
  }[];
}

export interface ReplayV3InitialDeployment {
  spawns: {
    spawnId: string;
    position: ReplayV3ContractPosition;
    facing: ReplayV3Direction;
  }[];
  lives: {
    teamId: number;
    unitId: number;
    lifeId: number;
    formId: string;
    spawnId: string;
  }[];
}

export interface ReplayV3LifecycleAssignment extends ReplayV3JsonObject {
  teamId: number;
  unitId: number;
  lifecycleProfileId: string;
  initialGeneration: number;
  allowedFormIds: string[];
  initialAvailability: string;
  unlockTick: number | null;
  assignedRespawnSpawnId: string | null;
}

export interface ReplayV3DeathmatchModeMapBinding {
  kind: 'deathmatch';
}

export interface ReplayV3FrontlineModeMapBinding {
  kind: 'frontline';
  orderedObjectiveRegionIds: string[];
  teamAdvances: {
    teamId: number;
    direction: 'toward-lower-index' | 'toward-higher-index';
    objectiveIndexDelta: number;
  }[];
}

export type ReplayV3ModeMapBinding =
  | ReplayV3DeathmatchModeMapBinding
  | ReplayV3FrontlineModeMapBinding;

export interface ReplayV3ResolvedContract {
  schemaVersion: number;
  matchContractFingerprint: string;
  capabilityVersions: ReplayV3CapabilityVersions;
  rules: ReplayV3RulesContract;
  map: ReplayV3MapContract;
  format: ReplayV3FormatContract;
  topology: ReplayV3TopologyContract;
  initialDeployment: ReplayV3InitialDeployment;
  lifecycleAssignments: ReplayV3LifecycleAssignment[];
  participantRegionAssignments: ReplayV3JsonObject[];
  modeMapBinding: ReplayV3ModeMapBinding;
}

export interface ReplayV3RuntimeVersions {
  contractProfileId: string;
  protocolVersion: string;
  configurationVersion: string;
  runtimeContractVersion: number;
  matchStartSchemaVersion: number;
  observationSchemaVersion: number;
  decisionSchemaVersion: number;
  matchContractSchemaVersion: number;
}

export interface ReplayV3Presentation {
  themeId: string | null;
  map: {
    boundaryWall: string;
    interiorWall: string;
    wallGroups: {
      family: string;
      tiles: ReplayV3Position[];
    }[];
  } | null;
  forms: {
    formId: string;
    lookId: string | null;
    projectileLookId: string | null;
  }[];
}

export interface ReplayV3ParticipantProvenance {
  participantId: number;
  teamId: number;
  name: string;
  runtimeKind: string;
  artifactHash: string | null;
  accent: string;
  lookId: string | null;
  projectileLookId: string | null;
}

export interface ReplayV3Header {
  replayVersion: 3;
  engineVersion: string;
  gameRulesVersion: string;
  runtime: ReplayV3RuntimeVersions;
  seed: string;
  contract: ReplayV3ResolvedContract;
  presentation: ReplayV3Presentation | null;
  provenance: {
    participants: ReplayV3ParticipantProvenance[];
  } | null;
}

export type ReplayV3UnitSlotState =
  | {
      kind: 'active';
      actorId: ReplayV3ActorId;
      generation: number;
      formId: string;
    }
  | { kind: 'availability-pending'; reason: string; dueTick: number }
  | {
      kind: 'automatic-return-pending';
      dueTick: number;
      targetFormId: string;
      generation: number;
    }
  | { kind: 'ready' }
  | {
      kind: 'fabrication-pending' | 'replication-pending';
      dueTick: number;
      sourceActorId: ReplayV3ActorId;
      transitionId: string;
      operationId: string;
      targetFormId: string;
      reservedPosition: ReplayV3Position;
    }
  | { kind: 'permanently-dormant' };

export interface ReplayV3ParticipantStatus {
  participantId: number;
  teamId: number;
  runtimeFaultCount: string;
  disqualified: boolean;
}

export interface ReplayV3PendingSameLifeTransition {
  transitionId: string;
  operationId: string;
  targetFormId: string;
  startedTick: number;
  dueTick: number;
}

export type ReplayV3RawActionArgument =
  | { kind: 'shot-program'; value: ReplayV3ShotProgram }
  | { kind: 'direction'; value: number }
  | { kind: 'unit-target'; value: { teamId: number; unitId: number } }
  | { kind: 'form-target'; formId: string | null }
  | { kind: 'projectile-heading'; value: number };

export type ReplayV3ActionArgument =
  | { kind: 'shot-program'; value: ReplayV3ShotProgram }
  | { kind: 'direction'; value: ReplayV3Direction }
  | { kind: 'unit-target'; value: { teamId: number; unitId: number } }
  | { kind: 'form-target'; formId: string }
  | { kind: 'projectile-heading'; value: ReplayV3ProjectileHeading };

export interface ReplayV3SubmittedDecision {
  actionId: string | null;
  actionCode: number;
  arguments: (ReplayV3RawActionArgument | null)[] | null;
  debugMessage: string | null;
}

export interface ReplayV3ResolvedAction {
  actionId: string;
  actionCode: number;
  arguments: ReplayV3ActionArgument[];
}

export interface ReplayV3RuntimeFault {
  participantId: number;
  actorId: ReplayV3ActorId;
  stage: string;
  faultCode: string;
  cumulativeFaultCount: string;
  disqualificationTriggered: boolean;
}

export interface ReplayV3ActionResolution {
  submittedAction: ReplayV3ResolvedAction | null;
  acceptedAction: ReplayV3ResolvedAction;
  validatedAction: ReplayV3ResolvedAction;
  outcome: string;
  runtimeFault: ReplayV3RuntimeFault | null;
}

export interface ReplayV3LifeState {
  actorId: ReplayV3ActorId;
  participantId: number;
  generation: number;
  formId: string;
  position: ReplayV3Position;
  facing: ReplayV3Direction;
  health: number;
  cooldown: number;
  energy: number | null;
  spawnedAtTick: number;
  spawnReason: string;
  parentActorId: ReplayV3ActorId | null;
  sourceTransitionId: string | null;
  sourceOperationId: string | null;
  previousActionResolution: ReplayV3ActionResolution | null;
  pendingSameLifeTransition: ReplayV3PendingSameLifeTransition | null;
}

export interface ReplayV3PendingReplication {
  sourceActorId: ReplayV3ActorId;
  participantId: number;
  sourceGeneration: number;
  sourceFormId: string;
  sourcePosition: ReplayV3Position;
  sourceFacing: ReplayV3Direction;
  transitionId: string;
  operationId: string;
  queuedTick: number;
  dueTick: number;
  descendants: {
    teamId: number;
    unitId: number;
    formId: string;
    generation: number;
    position: ReplayV3Position;
  }[];
}

export interface ReplayV3SlotState {
  teamId: number;
  unitId: number;
  participantId: number;
  nextLifeId: number;
  state: ReplayV3UnitSlotState;
  pendingParentActorId: ReplayV3ActorId | null;
  splitReservation: ReplayV3PendingReplication | null;
}

export interface ReplayV3ProjectileState {
  projectileId: string;
  ownerParticipantId: number;
  ownerTeamId: number;
  ownerActorId: ReplayV3ActorId;
  attackProfileId: string;
  spawnedAtTick: number;
  origin: ReplayV3Position;
  position: ReplayV3Position;
  launchHeading: ReplayV3ProjectileHeading;
  heading: ReplayV3ProjectileHeading;
  shotProgram: ReplayV3ShotProgram | null;
  committedPath: ReplayV3Position[];
  nextPathIndex: number;
  remainingTiles: number;
  ticksUntilAdvance: number;
}

export interface ReplayV3ScoreValue {
  channel: string;
  value: string;
}

export interface ReplayV3Scoreboard {
  teams: {
    teamId: number;
    eligible: boolean;
    scores: ReplayV3ScoreValue[];
  }[];
}

export type ReplayV3ModeState =
  | { kind: 'deathmatch'; modeId: string }
  | {
      kind: 'frontline';
      modeId: string;
      activePositionIndex: number;
      claimingTeamId: number | null;
      captureProgress: number;
      decayTicksElapsed: number;
      controlResumesAtTick: number;
    };

export interface ReplayV3WorldState {
  matchContractFingerprint: string;
  nextTick: number;
  nextProjectileId: string;
  participants: ReplayV3ParticipantStatus[];
  slots: ReplayV3SlotState[];
  activeLives: ReplayV3LifeState[];
  pendingReplications: ReplayV3PendingReplication[];
  projectiles: ReplayV3ProjectileState[];
  scoreboard: ReplayV3Scoreboard;
  mode: ReplayV3ModeState;
}

export interface ReplayV3LifeStart {
  schemaVersion: number;
  runtimeContractVersion: number;
  actorId: ReplayV3ActorId;
  participantId: number;
  actorRandomSeed: string;
  origin: {
    reason: string;
    generation: number;
    parentActorId: ReplayV3ActorId | null;
    sourceTransitionId: string | null;
    sourceOperationId: string | null;
  };
  matchContractFingerprint: string;
}

export interface ReplayV3ObservedSelf {
  actorId: ReplayV3ActorId;
  generation: number;
  formId: string;
  position: ReplayV3Position;
  facing: ReplayV3Direction;
  health: number;
  cooldown: number;
  energy: number | null;
  previousActionResolution: ReplayV3ActionResolution | null;
  pendingSameLifeTransition: ReplayV3PendingSameLifeTransition | null;
}

export interface ReplayV3ObservedAlly extends ReplayV3ObservedSelf {}

export interface ReplayV3ObservedEnemy {
  actorId: ReplayV3ActorId;
  formId: string;
  position: ReplayV3Position;
  facing: ReplayV3Direction;
  health: number;
  pendingSameLifeTransition: ReplayV3PendingSameLifeTransition | null;
  observedBy: ReplayV3ActorId[];
}

export interface ReplayV3ObservedProjectile {
  projectileId: string;
  ownerTeamId: number;
  ownerActorId: ReplayV3ActorId | null;
  position: ReplayV3Position;
  heading: ReplayV3ProjectileHeading;
  tilesPerAdvance: number;
  ticksUntilAdvance: number;
  remainingTiles: number;
  observedBy: ReplayV3ActorId[];
}

export type ReplayV3ActionConstraint =
  | { kind: 'shot-program'; allowed: boolean }
  | { kind: 'direction'; allowedValues: ReplayV3Direction[] }
  | {
      kind: 'unit-target';
      allowedValues: { teamId: number; unitId: number }[];
    }
  | { kind: 'form-target'; allowedFormIds: string[] }
  | {
      kind: 'projectile-heading';
      allowedValues: ReplayV3ProjectileHeading[];
    };

export interface ReplayV3ActionLegality {
  actionId: string;
  actionCode: number;
  allowedByForm: boolean;
  available: boolean;
  constraints: ReplayV3ActionConstraint[];
}

export type ReplayV3EventPayload =
  | {
      kind: 'rotation';
      actorId: ReplayV3ActorId;
      action: ReplayV3ResolvedAction;
      position: ReplayV3Position;
      fromFacing: ReplayV3Direction;
      toFacing: ReplayV3Direction;
    }
  | {
      kind: 'movement';
      actorId: ReplayV3ActorId;
      action: ReplayV3ResolvedAction;
      from: ReplayV3Position;
      to: ReplayV3Position;
      facing: ReplayV3Direction;
    }
  | {
      kind: 'movement-blocked';
      actorId: ReplayV3ActorId;
      action: ReplayV3ResolvedAction;
      from: ReplayV3Position;
      attemptedTo: ReplayV3Position;
      facing: ReplayV3Direction;
    }
  | {
      kind: 'attack';
      actorId: ReplayV3ActorId;
      action: ReplayV3ResolvedAction;
      projectileId: string;
      origin: ReplayV3Position;
      heading: ReplayV3ProjectileHeading;
    }
  | {
      kind: 'damage';
      sourceTeamId: number;
      sourceActorId: ReplayV3ActorId | null;
      targetActorId: ReplayV3ActorId;
      projectileId: string;
      amount: number;
      newHealth: number;
      position: ReplayV3Position;
    }
  | {
      kind: 'destruction';
      actorId: ReplayV3ActorId;
      sourceTeamId: number | null;
      sourceActorId: ReplayV3ActorId | null;
      projectileId: string | null;
      generation: number;
      formId: string;
      position: ReplayV3Position;
    }
  | {
      kind: 'life-spawned';
      actorId: ReplayV3ActorId;
      participantId: number;
      parentActorId: ReplayV3ActorId | null;
      generation: number;
      formId: string;
      health: number;
      position: ReplayV3Position;
      reason: string;
      sourceTransitionId: string | null;
      sourceOperationId: string | null;
    }
  | {
      kind: 'life-retired';
      actorId: ReplayV3ActorId;
      generation: number;
      formId: string;
      position: ReplayV3Position;
      reason: string;
      sourceTransitionId: string | null;
      sourceOperationId: string | null;
    }
  | {
      kind: 'runtime-fault';
      fault: ReplayV3RuntimeFault;
    }
  | {
      kind: 'participant';
      participantId: number;
      teamId: number;
    }
  | {
      kind: 'lifecycle';
      transitionId: string;
      operationId: string;
      sourceActorId: ReplayV3ActorId;
      targetTeamId: number | null;
      targetUnitId: number | null;
      dueTick: number | null;
      cancellationReason: string | null;
    }
  | {
      kind: 'form-transition';
      actorId: ReplayV3ActorId;
      transitionId: string;
      operationId: string;
      fromFormId: string;
      toFormId: string;
      startedTick: number;
      dueTick: number;
    }
  | {
      kind: 'score-changed';
      teamId: number;
      channel: string;
      newValue: string;
    }
  | { kind: 'mode-changed'; state: ReplayV3ModeState }
  | {
      kind: 'lifecycle-clock-cancelled';
      targetTeamId: number;
      targetUnitId: number;
      cancelledState: ReplayV3UnitSlotState;
      cancellationReason: string;
    };

export type ReplayV3EventKind =
  | 'rotation'
  | 'movement'
  | 'movement-blocked'
  | 'attack'
  | 'damage'
  | 'destruction'
  | 'life-spawned'
  | 'life-retired'
  | 'runtime-fault'
  | 'participant-disqualified'
  | 'lifecycle-queued'
  | 'lifecycle-cancelled'
  | 'lifecycle-completed'
  | 'form-transition-started'
  | 'form-transition-completed'
  | 'form-transition-cancelled'
  | 'score-changed'
  | 'mode-changed'
  | 'lifecycle-clock-cancelled';

export interface ReplayV3ObservedEvent {
  eventHandle: string;
  sourceTick: number;
  sourceOrdinal: number;
  kind: ReplayV3EventKind;
  payload: ReplayV3EventPayload;
  observedBy: ReplayV3ActorId[];
}

export interface ReplayV3ObservedSound {
  eventHandle: string;
  sourceTick: number;
  sourceOrdinal: number;
  observerActorId: ReplayV3ActorId;
  kind: string;
  bearing: number;
  distance: number;
}

export interface ReplayV3Observation {
  schemaVersion: number;
  tick: number;
  matchContractFingerprint: string;
  self: ReplayV3ObservedSelf;
  teamUnits: {
    teamId: number;
    unitId: number;
    state: ReplayV3UnitSlotState;
  }[];
  participants: ReplayV3ParticipantStatus[];
  allies: ReplayV3ObservedAlly[];
  enemies: ReplayV3ObservedEnemy[];
  visibleTiles: {
    position: ReplayV3Position;
    isWall: boolean;
    observedBy: ReplayV3ActorId[];
  }[];
  visibleProjectiles: ReplayV3ObservedProjectile[] | null;
  visibleEvents: ReplayV3ObservedEvent[];
  heardSounds: ReplayV3ObservedSound[] | null;
  scoreboard: ReplayV3Scoreboard;
  mode: ReplayV3ModeState;
  actionLegalities: ReplayV3ActionLegality[];
}

export interface ReplayV3ActorTurn {
  tick: number;
  participantId: number;
  actorId: ReplayV3ActorId;
  observation: ReplayV3Observation;
  submittedDecision: ReplayV3SubmittedDecision | null;
  actionResolution: ReplayV3ActionResolution;
}

export type ReplayV3EventAudience =
  | { kind: 'public' }
  | { kind: 'spatial'; primaryPosition: ReplayV3Position }
  | { kind: 'team-private'; teamId: number };

export interface ReplayV3AuthoritativeEvent {
  eventHandle: string;
  tick: number;
  globalOrdinal: string;
  sourceOrdinal: number;
  kind: ReplayV3EventKind;
  payload: ReplayV3EventPayload;
  audience: ReplayV3EventAudience;
}

export type ReplayV3TraversalTerminal =
  | { kind: 'retained' }
  | { kind: 'wall-or-path-exhausted' }
  | { kind: 'range-exhausted' }
  | {
      kind: 'actor-contact' | 'movement-contact';
      targetActorId: ReplayV3ActorId;
      appliedDamage: boolean;
    }
  | { kind: 'lifecycle-placement-purge'; position: ReplayV3Position }
  | { kind: 'participant-disqualification'; participantId: number };

export interface ReplayV3ProjectileTraversal {
  tick: number;
  globalOrdinal: string;
  phase: string;
  trigger: string;
  projectileId: string;
  ownerParticipantId: number;
  ownerTeamId: number;
  ownerActorId: ReplayV3ActorId;
  attackProfileId: string;
  from: ReplayV3Position;
  path: ReplayV3Position[];
  launchHeading: ReplayV3ProjectileHeading;
  finalHeading: ReplayV3ProjectileHeading;
  shotProgram: ReplayV3ShotProgram | null;
  terminal: ReplayV3TraversalTerminal;
}

export interface ReplayV3InitialFrame {
  state: ReplayV3WorldState;
  lifeStarts: ReplayV3LifeStart[];
  events: ReplayV3AuthoritativeEvent[];
}

export interface ReplayV3TickStart {
  tick: number;
  state: ReplayV3WorldState;
  activeActorIds: ReplayV3ActorId[];
  lifeStarts: ReplayV3LifeStart[];
  events: ReplayV3AuthoritativeEvent[];
  traversals: ReplayV3ProjectileTraversal[];
}

export interface ReplayV3Tick {
  tick: number;
  tickStart: ReplayV3TickStart;
  actorTurns: ReplayV3ActorTurn[];
  events: ReplayV3AuthoritativeEvent[];
  traversals: ReplayV3ProjectileTraversal[];
  postState: ReplayV3WorldState;
}

export interface ReplayV3Result {
  completionReason: string;
  endTick: number | null;
  standings: {
    winnerTeamId: number | null;
    teams: {
      teamId: number;
      rank: number;
      outcome: 'win' | 'loss' | 'draw';
      scores: ReplayV3ScoreValue[];
    }[];
  };
  eligibleTeamIds: number[];
  units: {
    slot: ReplayV3SlotState;
    activeLife: ReplayV3LifeState | null;
  }[];
  mode: ReplayV3ModeResult;
}

export type ReplayV3DeathmatchEndReason =
  | 'fault-eligibility'
  | 'kill-limit'
  | 'max-ticks';

export interface ReplayV3DeathmatchResult {
  kind: 'deathmatch';
  reason: ReplayV3DeathmatchEndReason;
  scores: {
    teamId: number;
    kills: string;
    deaths: string;
    damageDealt: string;
  }[];
}

export type ReplayV3FrontlineEndReason =
  | 'fault-eligibility'
  | 'base-breach'
  | 'max-ticks';

export interface ReplayV3FrontlineResult {
  kind: 'frontline';
  reason: ReplayV3FrontlineEndReason;
  control: Extract<ReplayV3ModeState, { kind: 'frontline' }>;
  scores: {
    teamId: number;
    territorialProgress: string;
  }[];
}

export type ReplayV3ModeResult =
  | ReplayV3DeathmatchResult
  | ReplayV3FrontlineResult;

export interface ReplayV3Document {
  header: ReplayV3Header;
  initialFrame: ReplayV3InitialFrame;
  ticks: ReplayV3Tick[];
  result: ReplayV3Result | null;
  replayHash: string | null;
  partial: boolean;
}
