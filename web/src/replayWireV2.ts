/**
 * Exact hand-maintained mirror of the explicit JSON codec in
 * BotArena.Engine/ReplayV2Serializer.cs.
 *
 * Unlike replay-v1, replay-v2 writes nullable properties explicitly. Do not
 * make these fields optional or collapse null and []: that distinction is part
 * of the observation/action contract.
 */

export type ReplayV2Direction = 'north' | 'east' | 'south' | 'west';

export type ReplayV2ProjectileHeading =
  | ReplayV2Direction
  | 'north-east'
  | 'south-east'
  | 'south-west'
  | 'north-west';

export type ReplayV2ActionResult =
  | 'none'
  | 'success'
  | 'blocked'
  | 'on-cooldown'
  | 'faulted';

export type ReplayV2LifecycleStatus = 'active' | 'respawning';
export type ReplayV2TeamPerception = 'individual' | 'immediate-union';

export type ReplayV2EventType =
  | 'respawned'
  | 'turn'
  | 'move'
  | 'move-blocked'
  | 'shot'
  | 'damage'
  | 'destroyed'
  | 'frontline-progress-changed'
  | 'frontline-position-advanced'
  | 'base-breached';

export type ReplayV2ObservedEventType =
  | ReplayV2EventType
  | 'fault'
  | 'disqualified';

export type ReplayV2ActionParameterKind =
  | 'shot-program'
  | 'direction'
  | 'unit-target'
  | 'form-target';

export interface ReplayV2Position {
  x: number;
  y: number;
}

/** Public contract/map positions use compact [x,y] arrays. */
export type ReplayV2ContractPosition = [number, number];

export interface ReplayV2ActorId {
  teamId: number;
  unitId: number;
  lifeId: number;
}

export interface ReplayV2ParticipantController {
  participantId: number;
  teamId: number;
  name: string;
  runtimeKind: string;
  artifactHash: string;
  accent: string;
  lookId: string | null;
  projectileLookId: string | null;
}

export interface ReplayV2WallGroup {
  family: string;
  tiles: ReplayV2Position[];
}

export interface ReplayV2MapPresentation {
  boundaryWall: string;
  interiorWall: string;
  wallGroups: ReplayV2WallGroup[];
}

export interface ReplayV2Presentation {
  themeId: string | null;
  map: ReplayV2MapPresentation | null;
}

export interface ReplayV2ActorRuntimeContract {
  family: string;
  version: number;
  matchStartSchemaVersion: number;
  observationSchemaVersion: number;
  decisionSchemaVersion: number;
}

export type ReplayV2ObjectiveMode =
  | 'none'
  | 'zone-ticks'
  | 'shared-pressure'
  | 'frontline';

export type ReplayV2ScoreMetric =
  | 'objective'
  | 'health'
  | 'damage-dealt';

export interface ReplayV2MatchLimits {
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

export interface ReplayV2ObjectiveOvertimeRules {
  startTick: number;
  pressureLimit: number;
  pressureGain: number;
  stopsDecay: boolean;
}

export interface ReplayV2ObjectiveRules {
  mode: ReplayV2ObjectiveMode;
  zoneControlEnabled: boolean;
  zoneDominationTicks: number;
  zoneExclusiveAccrual: boolean;
  sharedPressureEnabled: boolean;
  controlBySoleOccupancy: boolean;
  controlPressureLimit: number;
  controlPressureGain: number;
  controlPressureDecayInterval: number;
  overtime: ReplayV2ObjectiveOvertimeRules;
  maxTickTiebreakers: ReplayV2ScoreMetric[];
}

export interface ReplayV2FrontlineCaptureDefinition {
  threshold: number;
  gainPerSoleTeamTick: number;
  decayAmount: number;
  decayIntervalTicks: number;
  redeployPauseTicks: number;
  pushesToBreach: number;
}

export interface ReplayV2FrontlineLifecycleDefinition {
  primeRespawnTicks: number;
  childRebuildTicks: number;
  fabricationUnlockTicks: number[];
}

export interface ReplayV2FrontlineAnchorDefinition {
  windupTicks: number;
  healthGain: number;
  irreversibleForLife: boolean;
}

export interface ReplayV2FrontlineAlliedCombatDefinition {
  friendlyFireEnabled: boolean;
  alliedProjectilesBlock: boolean;
}

export interface ReplayV2FrontlineDefinition {
  teamCount: number;
  participantsPerTeam: number;
  frontlinePositionCount: number;
  initialUnitsPerTeam: number;
  maxUnitsPerTeam: number;
  teamPerception: ReplayV2TeamPerception;
  capture: ReplayV2FrontlineCaptureDefinition;
  lifecycle: ReplayV2FrontlineLifecycleDefinition;
  anchor: ReplayV2FrontlineAnchorDefinition;
  alliedCombat: ReplayV2FrontlineAlliedCombatDefinition;
}

export interface ReplayV2EnergyRules {
  enabled: boolean;
  maxEnergy: number;
  shotEnergyCost: number;
  regenerationIntervalTicks: number;
  regenerationAmount: number;
}

export interface ReplayV2FormDefinition {
  id: string;
  maxHealth: number;
  visionRange: number;
  shootCooldownTicks: number;
  omnidirectionalVision: boolean;
  omnidirectionalShooting: boolean;
  movementLayer: 'ground';
  objectiveWeight: number;
  canMove: boolean;
  canShoot: boolean;
  allowsProgrammedShots: boolean;
  allowedActionIds: string[];
}

export interface ReplayV2ActionDefinition {
  id: string;
  code: number;
  kind: 'wait' | 'movement' | 'rotation' | 'attack';
  parameterKinds: ReplayV2ActionParameterKind[];
  enabled: boolean;
}

export interface ReplayV2ProjectileRules {
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

export interface ReplayV2ShotProgram {
  initialAimOffset: number;
  bendDirection: number;
  bendAfterTiles: number;
  bendEveryTiles: number;
  bendCount: number;
}

export interface ReplayV2AimOnlyShotProgramRules {
  bendDirection: number;
  bendAfterTiles: number;
  bendEveryTiles: number;
  bendCount: number;
}

export interface ReplayV2ShotProgramRules {
  enabled: boolean;
  headingSectors: number;
  bendStepOctants: number;
  minInitialAimOctants: number;
  maxInitialAimOctants: number;
  aimOnlyProgram: ReplayV2AimOnlyShotProgramRules;
  allowedCurvedBendDirections: number[];
  minBendAfterTiles: number;
  maxBendAfterTiles: number;
  minBendEveryTiles: number;
  maxBendEveryTiles: number;
  minBendCount: number;
  maxBendCount: number;
  launchTiles: number;
  payloadOptional: boolean;
  defaultProgram: ReplayV2ShotProgram;
  invalidPayloadResult: 'blocked' | 'faulted' | 'rejected' | null;
  unsupportedPayloadResult: 'blocked' | 'faulted' | 'rejected';
  diagonalCornersMustBeClear: boolean;
}

export interface ReplayV2VisionRules {
  range: number;
  distanceMetric: 'chebyshev';
  shape: 'omnidirectional' | 'facing-quadrant';
  omnidirectionalProximityRange: number;
  lineOfSight: 'corner-strict-supercover';
  hearingRadius: number;
  hearingBearingSectors: number;
  hearingDistanceBandUpperBounds: number[];
  loudEventTypes: (
    | 'turn'
    | 'move'
    | 'move-blocked'
    | 'shot'
    | 'damage'
    | 'destroyed'
    | 'fault'
    | 'disqualified'
  )[];
}

export interface ReplayV2CollisionRules {
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

export type ReplayV2TickResolutionPhase =
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
  | 'queue-destroyed-lives';

export interface ReplayV2TickResolutionRules {
  observationsUsePreTickState: boolean;
  decisionsResolveAsJointStep: boolean;
  phases: ReplayV2TickResolutionPhase[];
}

export interface ReplayV2RulesManifest {
  schemaVersion: number;
  rulesetId: string;
  rulesFingerprint: string;
  limits: ReplayV2MatchLimits;
  objective: ReplayV2ObjectiveRules;
  /** Omitted when the contract is not a Frontline ruleset. */
  frontlineDefinition?: ReplayV2FrontlineDefinition;
  energy: ReplayV2EnergyRules;
  forms: ReplayV2FormDefinition[];
  actions: ReplayV2ActionDefinition[];
  projectiles: ReplayV2ProjectileRules;
  shotPrograms: ReplayV2ShotProgramRules;
  vision: ReplayV2VisionRules;
  collisions: ReplayV2CollisionRules;
  tickResolution: ReplayV2TickResolutionRules;
}

export interface ReplayV2MapSpawn {
  teamId: number;
  x: number;
  y: number;
  facing: ReplayV2Direction;
}

export interface ReplayV2FrontlineMapPosition {
  positionIndex: number;
  tiles: ReplayV2ContractPosition[];
}

export interface ReplayV2FrontlineTeamHome {
  teamId: number;
  primeSpawn: ReplayV2Position & { facing: ReplayV2Direction };
  protectedSpawnPad: ReplayV2ContractPosition[];
}

export interface ReplayV2FrontlineMapDefinition {
  positions: ReplayV2FrontlineMapPosition[];
  teamHomes: ReplayV2FrontlineTeamHome[];
  anchorForbiddenTiles: ReplayV2ContractPosition[];
}

export interface ReplayV2MapManifest {
  schemaVersion: number;
  mapId: string;
  mapVersion: number;
  mapFingerprint: string;
  formatVersion: number;
  width: number;
  height: number;
  tileRows: string[];
  spawns: ReplayV2MapSpawn[];
  objectiveTiles: ReplayV2ContractPosition[];
  /** Omitted for format-v1 maps. */
  frontline?: ReplayV2FrontlineMapDefinition;
}

export interface ReplayV2TopologyTeam {
  teamId: number;
}

export interface ReplayV2TopologyParticipant {
  participantId: number;
  teamId: number;
}

export interface ReplayV2TopologyUnitSlot {
  teamId: number;
  unitId: number;
  controllerParticipantId: number;
}

export interface ReplayV2TopologyInitialLife {
  teamId: number;
  unitId: number;
  lifeId: number;
  formId: string;
}

export interface ReplayV2MatchTopology {
  teamCount: number;
  participantCount: number;
  unitSlotCount: number;
  initialLifeCount: number;
  teams: ReplayV2TopologyTeam[];
  participants: ReplayV2TopologyParticipant[];
  unitSlots: ReplayV2TopologyUnitSlot[];
  initialLives: ReplayV2TopologyInitialLife[];
}

export interface ReplayV2MatchContract {
  schemaVersion: number;
  matchContractFingerprint: string;
  rules: ReplayV2RulesManifest;
  map: ReplayV2MapManifest;
  topology: ReplayV2MatchTopology;
}

export interface ReplayV2Header {
  replayVersion: 2;
  engineVersion: string;
  gameRulesVersion: string;
  actorRuntime: ReplayV2ActorRuntimeContract;
  /** Canonical unsigned decimal string; never a JSON number. */
  seed: string;
  contract: ReplayV2MatchContract;
  presentation: ReplayV2Presentation | null;
  participants: ReplayV2ParticipantController[];
}

export interface ReplayV2ObservedUnitSlot {
  teamId: number;
  unitId: number;
  formId: string;
  lifecycleStatus: ReplayV2LifecycleStatus;
  activeActorId: ReplayV2ActorId | null;
  respawnAtTick: number | null;
}

export interface ReplayV2ObservedSelf {
  actorId: ReplayV2ActorId;
  formId: string;
  position: ReplayV2Position;
  facing: ReplayV2Direction;
  health: number;
  cooldown: number;
  energy: number | null;
  previousActionResult: ReplayV2ActionResult;
}

export interface ReplayV2ObservedAlly extends ReplayV2ObservedSelf {}

export interface ReplayV2ObservedEnemy {
  actor: ReplayV2ObservedEnemyActorRef;
  formId: string;
  position: ReplayV2Position;
  facing: ReplayV2Direction;
  health: number;
  observedBy: ReplayV2ActorId[];
}

export interface ReplayV2ObservedEnemyActorRef {
  teamId: number;
  unitId: number;
  lifeHandle: string;
}

export interface ReplayV2ObservedMapTile {
  position: ReplayV2Position;
  isWall: boolean;
  observedBy: ReplayV2ActorId[];
}

export interface ReplayV2ObservedProjectile {
  projectileHandle: string;
  ownerTeamId: number;
  alliedOwnerActorId: ReplayV2ActorId | null;
  visibleEnemyOwner: ReplayV2ObservedEnemyActorRef | null;
  position: ReplayV2Position;
  heading: ReplayV2ProjectileHeading;
  tilesPerAdvance: number;
  ticksUntilAdvance: number;
  remainingTiles: number;
  observedBy: ReplayV2ActorId[];
}

export interface ReplayV2ObservedEvent {
  eventHandle: string;
  sourceTick: number;
  type: ReplayV2ObservedEventType;
  teamId: number | null;
  alliedActorId: ReplayV2ActorId | null;
  enemyActor: ReplayV2ObservedEnemyActorRef | null;
  projectileHandle: string | null;
  position: ReplayV2Position | null;
  facing: ReplayV2Direction | null;
  amount: number | null;
  newHealth: number | null;
  observedBy: ReplayV2ActorId[];
}

export interface ReplayV2ObservedSound {
  eventHandle: string;
  sourceTick: number;
  observerActorId: ReplayV2ActorId;
  type: ReplayV2ObservedEventType;
  bearing: number;
  distance: number;
}

export interface ReplayV2ObservedFrontlineObjective {
  activePositionIndex: number;
  claimingTeamId: number | null;
  captureProgress: number;
  decayTicksElapsed: number;
  controlResumesAtTick: number;
}

export interface ReplayV2ObservedUnitTarget {
  teamId: number;
  unitId: number;
}

export interface ReplayV2ObservedActionAvailability {
  actionId: string;
  actionCode: number;
  parameterKinds: ReplayV2ActionParameterKind[];
  enabled: boolean;
  available: boolean;
  shotProgramAvailable: boolean | null;
  allowedDirections: ReplayV2Direction[] | null;
  allowedUnitTargets: ReplayV2ObservedUnitTarget[] | null;
  allowedFormTargets: string[] | null;
}

export interface ReplayV2ActorObservation {
  schemaVersion: number;
  tick: number;
  matchContractFingerprint: string;
  teamPerception: ReplayV2TeamPerception;
  self: ReplayV2ObservedSelf;
  teamUnits: ReplayV2ObservedUnitSlot[];
  allies: ReplayV2ObservedAlly[];
  enemies: ReplayV2ObservedEnemy[];
  visibleTiles: ReplayV2ObservedMapTile[];
  visibleProjectiles: ReplayV2ObservedProjectile[] | null;
  visibleEvents: ReplayV2ObservedEvent[];
  heardSounds: ReplayV2ObservedSound[] | null;
  frontlineObjective: ReplayV2ObservedFrontlineObjective | null;
  actions: ReplayV2ObservedActionAvailability[];
}

export interface ReplayV2ActionPayload {
  shotProgram: ReplayV2ShotProgram | null;
  direction: ReplayV2Direction | null;
  unitTarget: ReplayV2ObservedUnitTarget | null;
  formTargetId: string | null;
}

export interface ReplayV2ActorDecision {
  actionId: string | null;
  actionCode: number | null;
  payload: ReplayV2ActionPayload | null;
  debugMessage: string | null;
  faulted: boolean;
  faultMessage: string | null;
}

export interface ReplayV2ActionResolution {
  actorId: ReplayV2ActorId;
  chosenActionId: string;
  chosenActionCode: number;
  chosenPayload: ReplayV2ActionPayload | null;
  validatedActionId: string;
  validatedActionCode: number;
  validatedPayload: ReplayV2ActionPayload | null;
  result: ReplayV2ActionResult;
}

export interface ReplayV2LifeStart {
  schemaVersion: number;
  runtimeContractVersion: number;
  actorId: ReplayV2ActorId;
  participantId: number;
  /** Canonical unsigned-64 decimal string. */
  actorRandomSeed: string;
  spawnReason: 'initial' | 'respawn' | 'rebuild' | 'fabrication';
  matchContractFingerprint: string;
}

export interface ReplayV2EnemyLifeAlias {
  lifeHandle: string;
  actorId: ReplayV2ActorId;
}

export interface ReplayV2ProjectileAlias {
  projectileHandle: string;
  projectileId: string;
}

export interface ReplayV2EventAlias {
  eventHandle: string;
  eventId: string;
}

export interface ReplayV2ObservationAliases {
  enemyLives: ReplayV2EnemyLifeAlias[];
  projectiles: ReplayV2ProjectileAlias[];
  events: ReplayV2EventAlias[];
}

export interface ReplayV2ActorTurn {
  actorId: ReplayV2ActorId;
  lifeStart: ReplayV2LifeStart | null;
  observation: ReplayV2ActorObservation;
  aliases: ReplayV2ObservationAliases;
  runtimeReply: ReplayV2ActorDecision;
  acceptedDecision: ReplayV2ActorDecision;
  actionResolution: ReplayV2ActionResolution;
}

export interface ReplayV2Event {
  eventId: string;
  tick: number;
  type: ReplayV2EventType;
  teamId: number | null;
  sourceActorId: ReplayV2ActorId | null;
  targetActorId: ReplayV2ActorId | null;
  projectileId: string | null;
  from: ReplayV2Position | null;
  to: ReplayV2Position | null;
  fromFacing: ReplayV2Direction | null;
  toFacing: ReplayV2Direction | null;
  projectileHeading: ReplayV2ProjectileHeading | null;
  actionPayload: ReplayV2ActionPayload | null;
  actionId: string | null;
  actionCode: number | null;
  actionResult: ReplayV2ActionResult | null;
  amount: number | null;
  newHealth: number | null;
  lifecycleStatus: ReplayV2LifecycleStatus | null;
  respawnAtTick: number | null;
  fromPositionIndex: number | null;
  toPositionIndex: number | null;
  claimingTeamId: number | null;
  captureProgress: number | null;
  controlResumesAtTick: number | null;
}

export interface ReplayV2ProjectileTraversal {
  projectileId: string;
  ownerActorId: ReplayV2ActorId;
  launchDirection: ReplayV2Direction;
  from: ReplayV2Position;
  path: ReplayV2Position[];
  heading: ReplayV2ProjectileHeading | null;
  shotProgram: ReplayV2ShotProgram | null;
  programmedPath: ReplayV2Position[] | null;
}

export interface ReplayV2AuthoritativeResolution {
  events: ReplayV2Event[];
  projectileTraversals: ReplayV2ProjectileTraversal[];
}

export interface ReplayV2LifeState {
  actorId: ReplayV2ActorId;
  position: ReplayV2Position;
  facing: ReplayV2Direction;
  health: number;
  cooldown: number;
  energy: number | null;
  /** Canonical non-negative signed-64 decimal string. */
  damageDealt: string;
  previousActionResult: ReplayV2ActionResult;
  spawnedAtTick: number;
}

export interface ReplayV2UnitState {
  teamId: number;
  unitId: number;
  formId: string;
  lifecycleStatus: ReplayV2LifecycleStatus;
  respawnAtTick: number | null;
  /** Canonical non-negative signed-64 decimal string. */
  damageDealt: string;
  activeLife: ReplayV2LifeState | null;
}

export interface ReplayV2TeamState {
  teamId: number;
  /** Canonical non-negative signed-64 decimal string. */
  damageDealt: string;
  units: ReplayV2UnitState[];
}

export interface ReplayV2ProjectileState {
  projectileId: string;
  ownerActorId: ReplayV2ActorId;
  position: ReplayV2Position;
  launchDirection: ReplayV2Direction;
  heading: ReplayV2ProjectileHeading | null;
  shotProgram: ReplayV2ShotProgram | null;
  programmedPath: ReplayV2Position[] | null;
  nextProgrammedPathIndex: number;
  tilesTraveled: number;
  phase: number;
}

export interface ReplayV2ControlState {
  nextTick: number;
  activePositionIndex: number;
  claimingTeamId: number | null;
  captureProgress: number;
  decayTicksElapsed: number;
  controlResumesAtTick: number;
  winnerTeamId: number | null;
}

export interface ReplayV2WorldState {
  teams: ReplayV2TeamState[];
  projectiles: ReplayV2ProjectileState[];
  objective: ReplayV2ControlState;
}

export interface ReplayV2TickStart {
  state: ReplayV2WorldState;
  activeActors: ReplayV2ActorId[];
  lifecycleEvents: ReplayV2Event[];
}

export interface ReplayV2Tick {
  tick: number;
  tickStart: ReplayV2TickStart;
  actors: ReplayV2ActorTurn[];
  resolution: ReplayV2AuthoritativeResolution;
  postState: ReplayV2WorldState;
}

export interface ReplayV2TeamResult {
  teamId: number;
  outcome: 'win' | 'loss' | 'draw';
  finalHealth: number;
  /** Canonical non-negative signed-64 decimal string. */
  damageDealt: string;
  finalLifecycleStatus: ReplayV2LifecycleStatus;
}

export interface ReplayV2Result {
  winnerTeamId: number | null;
  reason: 'base-breach' | 'max-ticks';
  endTick: number;
  /** Canonical signed-64 decimal string. */
  territorialScore: string;
  objective: ReplayV2ControlState;
  teams: ReplayV2TeamResult[];
}

interface ReplayV2DocumentBase {
  header: ReplayV2Header;
  ticks: ReplayV2Tick[];
}

export interface ReplayV2CompleteDocument extends ReplayV2DocumentBase {
  result: ReplayV2Result;
  replayHash: string;
  partial: false;
}

export interface ReplayV2PartialDocument extends ReplayV2DocumentBase {
  result: null;
  replayHash: null;
  partial: true;
}

export type ReplayV2Document =
  | ReplayV2CompleteDocument
  | ReplayV2PartialDocument;
