/**
 * Version-neutral replay domain consumed by future viewer integrations.
 *
 * This model deliberately separates a stable unit from an exact actor life.
 * UI selection can follow a unit through respawns while projectiles, events,
 * decisions, and observations retain causal ownership by the exact life.
 */

export type ReplaySourceVersion = 1 | 2;
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
  | 'faulted';

export type ReplayStableUnitKey =
  | `duel:${number}:unit:0`
  | `frontline:${number}:unit:${number}`;

export type ReplayActorLifeKey =
  | `duel:${number}:unit:0:life:0`
  | `frontline:${number}:unit:${number}:life:${number}`;

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

export type ReplayActorIdentity =
  | ReplayDuelActorIdentity
  | ReplayFrontlineActorIdentity;

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

export interface ReplayPosition {
  x: number;
  y: number;
}

export interface ReplayParticipantController {
  participantKey: ReplayParticipantKey;
  participantId: number;
  teamKey: ReplayTeamKey;
  teamId: number;
  name: string;
  runtimeKind: string;
  artifactHash: string;
  accent: string;
  lookId: string | null;
  projectileLookId: string | null;
}

export interface ReplayTeam {
  teamKey: ReplayTeamKey;
  teamId: number;
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

export interface ReplayMap {
  mapId: string;
  mapVersion: number;
  formatVersion: number;
  width: number;
  height: number;
  tileRows: string[];
  objectiveTiles: ReplayPosition[];
  frontline: ReplayFrontlineMap | null;
  presentation: ReplayMapPresentation | null;
}

export type ReplayUnitLifecycleStatus =
  | 'active'
  | 'respawning'
  | 'destroyed'
  | 'disqualified';

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
  status: ReplayUnitLifecycleStatus;
}

export interface ReplayUnitState {
  unitKey: ReplayStableUnitKey;
  teamKey: ReplayTeamKey;
  teamId: number;
  unitId: number;
  formId: string;
  lifecycleStatus: ReplayUnitLifecycleStatus;
  respawnAtTick: number | null;
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

export interface ReplayProjectileState {
  projectileId: string;
  ownerActor: ReplayActorIdentity;
  ownerActorKey: ReplayActorLifeKey;
  position: ReplayPosition;
  launchDirection: ReplayDirection;
  heading: ReplayProjectileHeading | null;
  shotProgram: ReplayShotProgram | null;
  programmedPath: ReplayPosition[] | null;
  ticksUntilAdvance: number | null;
  remainingTiles: number | null;
  tilesPerAdvance: number | null;
  nextProgrammedPathIndex: number | null;
  tilesTraveled: number | null;
  phase: number | null;
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
  winnerTeamId: number | null;
  completeness: 'exact';
}

export type ReplayObjectiveState =
  | ReplayLegacyObjectiveState
  | ReplayFrontlineObjectiveState;

export interface ReplayWorldSnapshot {
  completeness: ReplayStateCompleteness;
  teams: ReplayTeamState[];
  units: ReplayUnitState[];
  actors: ReplayActorState[];
  projectiles: ReplayProjectileState[] | null;
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
  formId: string;
  position: ReplayPosition;
  facing: ReplayDirection;
  health: number;
  cooldown: number | null;
  energy: number | null;
  previousActionResult: ReplayActionResult | null;
  observedBy: ReplayActorLifeKey[];
}

export interface ReplayObservedTile {
  position: ReplayPosition;
  isWall: boolean | null;
  observedBy: ReplayActorLifeKey[];
}

export interface ReplayObservedProjectile {
  /** Opaque match-local observation handle; null only for replay-v1. */
  projectileHandle: string | null;
  ownerTeamId: number;
  alliedOwnerActor: ReplayActorIdentity | null;
  visibleEnemyOwner: ReplayOpaqueEnemyActorRef | null;
  position: ReplayPosition;
  heading: ReplayProjectileHeading;
  tilesPerAdvance: number;
  ticksUntilAdvance: number;
  remainingTiles: number;
  observedBy: ReplayActorLifeKey[];
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
  amount: number | null;
  newHealth: number | null;
  observedBy: ReplayActorLifeKey[];
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
  allowedUnitKeys: ReplayStableUnitKey[] | null;
  allowedFormTargets: string[] | null;
}

export interface ReplayObservedFrontlineObjective {
  activePositionIndex: number;
  claimingTeamId: number | null;
  captureProgress: number;
  decayTicksElapsed: number;
  controlResumesAtTick: number;
}

export interface ReplayActorObservation {
  completeness: ReplayObservationCompleteness;
  schemaVersion: number | null;
  tick: number;
  matchContractFingerprint: string | null;
  teamPerception: string | null;
  self: ReplayObservedActor | null;
  teamUnits: ReplayObservedUnit[];
  allies: ReplayObservedActor[];
  enemies: ReplayObservedActor[];
  visibleTiles: ReplayObservedTile[];
  visibleProjectiles: ReplayObservedProjectile[] | null;
  visibleEvents: ReplayObservedEvent[];
  heardSounds: ReplayObservedSound[] | null;
  frontlineObjective: ReplayObservedFrontlineObjective | null;
  actions: ReplayObservedActionAvailability[] | null;
}

export interface ReplayActionPayload {
  shotProgram: ReplayShotProgram | null;
  direction: ReplayDirection | null;
  unitKey: ReplayStableUnitKey | null;
  formTargetId: string | null;
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
}

export interface ReplayActorLifeStart {
  completeness: ReplayObservationCompleteness;
  schemaVersion: number | null;
  runtimeContractVersion: number | null;
  actor: ReplayActorIdentity;
  participantId: number;
  actorRandomSeed: string | null;
  spawnReason: 'initial' | 'respawn' | 'rebuild' | 'fabrication' | 'legacy';
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
  sourceActor: ReplayActorIdentity | null;
  targetActor: ReplayActorIdentity | null;
  projectileId: string | null;
  from: ReplayPosition | null;
  to: ReplayPosition | null;
  fromFacing: ReplayDirection | null;
  toFacing: ReplayDirection | null;
  projectileHeading: ReplayProjectileHeading | null;
  actionPayload: ReplayActionPayload | null;
  actionId: string | null;
  actionCode: number | null;
  actionResult: ReplayActionResult | null;
  amount: number | null;
  newHealth: number | null;
  lifecycleStatus: ReplayUnitLifecycleStatus | null;
  respawnAtTick: number | null;
  fromPositionIndex: number | null;
  toPositionIndex: number | null;
  claimingTeamId: number | null;
  captureProgress: number | null;
  controlResumesAtTick: number | null;
  completeness: ReplayObservationCompleteness;
}

export interface ReplayProjectileTraversal {
  projectileId: string;
  ownerActor: ReplayActorIdentity;
  ownerActorKey: ReplayActorLifeKey;
  launchDirection: ReplayDirection;
  from: ReplayPosition;
  path: ReplayPosition[];
  heading: ReplayProjectileHeading | null;
  shotProgram: ReplayShotProgram | null;
  programmedPath: ReplayPosition[] | null;
}

export interface ReplayTick {
  tick: number;
  before: ReplayWorldSnapshot;
  activeActorKeys: ReplayActorLifeKey[];
  lifecycleEvents: ReplayCausalEvent[];
  actorTurns: ReplayActorTurn[];
  events: ReplayCausalEvent[];
  projectileTraversals: ReplayProjectileTraversal[];
  after: ReplayWorldSnapshot;
}

export interface ReplayTeamResult {
  teamKey: ReplayTeamKey;
  teamId: number;
  outcome: 'win' | 'loss' | 'draw';
  finalHealth: number;
  damageDealt: string;
  finalLifecycleStatus: ReplayUnitLifecycleStatus;
  faults: number | null;
  zoneTicks: number | null;
}

export interface ReplayTerminalResult {
  winnerTeamId: number | null;
  reason: string;
  endTick: number;
  territorialScore: string | null;
  objective: ReplayObjectiveState;
  teams: ReplayTeamResult[];
}

export interface ReplayHeaderVersions {
  engineVersion: string;
  gameRulesVersion: string;
  runtimeProtocolVersion: string | null;
  runtimeConfigurationVersion: string | null;
  actorRuntime: {
    family: string;
    version: number;
    matchStartSchemaVersion: number;
    observationSchemaVersion: number;
    decisionSchemaVersion: number;
  } | null;
}

export interface ReplayModel {
  sourceVersion: ReplaySourceVersion;
  versions: ReplayHeaderVersions;
  /** Always a decimal string in the normalized model. */
  seed: string;
  partial: boolean;
  replayHash: string | null;
  matchContractFingerprint: string | null;
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
  ticks: ReplayTick[];
  result: ReplayTerminalResult | null;
}
