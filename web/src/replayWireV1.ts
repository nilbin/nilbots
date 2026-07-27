/**
 * Hand-maintained mirror of the replay-v1 JSON written by
 * BotArena.Engine/Replay.cs and ReplaySerializer.
 *
 * These are wire types, not viewer-domain types. Keep historical optional
 * fields optional: replay-v1 deliberately omitted null values to preserve
 * canonical hashes as features were added.
 */

export type ReplayV1Direction = 'North' | 'East' | 'South' | 'West';

export type ReplayV1ProjectileHeading =
  | ReplayV1Direction
  | 'NorthEast'
  | 'SouthEast'
  | 'SouthWest'
  | 'NorthWest';

export type ReplayV1BotAction =
  | 'Wait'
  | 'MoveForward'
  | 'TurnLeft'
  | 'TurnRight'
  | 'Shoot'
  | 'StrafeLeft'
  | 'StrafeRight';

export type ReplayV1ActionResult =
  | 'None'
  | 'Success'
  | 'Blocked'
  | 'OnCooldown'
  | 'Faulted';

export type ReplayV1BotStatus = 'Active' | 'Destroyed' | 'Disqualified';

export type ReplayV1GameEventType =
  | 'Turn'
  | 'Move'
  | 'MoveBlocked'
  | 'Shot'
  | 'Damage'
  | 'Destroyed'
  | 'Fault'
  | 'Disqualified';

export interface ReplayV1Participant {
  slot: number;
  name: string;
  runtimeKind: string;
  artifactHash: string;
  accent: string;
  spawnX: number;
  spawnY: number;
  spawnFacing: ReplayV1Direction;
  lookId?: string;
  projectileLookId?: string;
}

export interface ReplayV1MapWallGroup {
  family: string;
  tiles: ReplayV1Coordinate[];
}

export interface ReplayV1Coordinate {
  x: number;
  y: number;
}

export interface ReplayV1MapPresentation {
  boundaryWall: string;
  interiorWall: string;
  wallGroups: ReplayV1MapWallGroup[];
}

export interface ReplayV1ShotProgramLimits {
  maxInitialAimOctants: number;
  maxBendAfterTiles: number;
  maxBendEveryTiles: number;
  maxBendCount: number;
  maxPathTiles: number;
  launchTiles: number;
  tilesPerAdvance: number;
}

export interface ReplayV1Header {
  replayVersion: 1;
  engineVersion: string;
  gameRulesVersion: string;
  runtimeProtocolVersion: string;
  runtimeConfigurationVersion: string;
  mapId: string;
  mapVersion: number;
  themeId?: string;
  presentation?: ReplayV1MapPresentation;
  mapWidth: number;
  mapHeight: number;
  mapTiles: string[];
  /**
   * replay-v1 writes ulong as a JSON number. Keep the wire type honest even
   * though values beyond Number.MAX_SAFE_INTEGER cannot be represented exactly.
   */
  seed: number;
  maxTicks: number;
  maxHealth?: number;
  visionRange: number;
  visionCone?: boolean;
  zoneTiles?: ReplayV1Position[];
  controlPressureLimit?: number;
  controlBySoleOccupancy?: boolean;
  controlOvertimeStartTick?: number;
  controlOvertimePressureLimit?: number;
  controlOvertimePressureGain?: number;
  controlOvertimeStopsDecay?: boolean;
  programmedShots?: boolean;
  programmedShotLimits?: ReplayV1ShotProgramLimits;
  participants: ReplayV1Participant[];
}

export type ReplayV1Position = [number, number];

export interface ReplayV1GameEvent {
  type: ReplayV1GameEventType;
  slot?: number;
  fromX?: number;
  fromY?: number;
  toX?: number;
  toY?: number;
  fromFacing?: ReplayV1Direction;
  toFacing?: ReplayV1Direction;
  hitSlot?: number;
  targetSlot?: number;
  amount?: number;
  newHealth?: number;
  message?: string;
}

export interface ReplayV1VisibleEnemy {
  slot: number;
  x: number;
  y: number;
  facing: ReplayV1Direction;
  health: number;
}

export interface ReplayV1HeardSound {
  type: ReplayV1GameEventType;
  bearing: number;
  distance: number;
}

export interface ReplayV1ShotProgram {
  initialAimOffset: number;
  bendDirection: number;
  bendAfterTiles: number;
  bendEveryTiles: number;
  bendCount: number;
}

export interface ReplayV1BotTick {
  slot: number;
  chosenAction: ReplayV1BotAction;
  validatedAction: ReplayV1BotAction;
  result: ReplayV1ActionResult;
  faulted: boolean;
  shotProgram?: ReplayV1ShotProgram;
  debug?: string;
  visibleTiles: ReplayV1Position[];
  visibleEnemies: ReplayV1VisibleEnemy[];
  heardSounds?: ReplayV1HeardSound[];
}

export interface ReplayV1BotState {
  slot: number;
  x: number;
  y: number;
  facing: ReplayV1Direction;
  health: number;
  cooldown: number;
  status: ReplayV1BotStatus;
  energy?: number;
  zoneTicks?: number;
}

export interface ReplayV1Projectile {
  x: number;
  y: number;
  direction: ReplayV1Direction;
  ownerSlot: number;
  ticksUntilAdvance?: number;
  remainingTiles?: number;
  tilesPerAdvance?: number;
  id?: number;
  heading?: ReplayV1ProjectileHeading;
  programmedPath?: ReplayV1Position[];
}

export interface ReplayV1ProjectileTraversal {
  id: number;
  ownerSlot: number;
  direction: ReplayV1Direction;
  fromX: number;
  fromY: number;
  path: ReplayV1Position[];
  heading?: ReplayV1ProjectileHeading;
  programmedPath?: ReplayV1Position[];
}

export interface ReplayV1Tick {
  tick: number;
  bots: ReplayV1BotTick[];
  events: ReplayV1GameEvent[];
  state: ReplayV1BotState[];
  projectiles?: ReplayV1Projectile[];
  projectileTraversals?: ReplayV1ProjectileTraversal[];
  controlPressure?: number;
}

export interface ReplayV1BotMatchResult {
  slot: number;
  outcome: 'Win' | 'Loss' | 'Draw';
  finalHealth: number;
  damageDealt: number;
  faults: number;
  finalStatus: ReplayV1BotStatus;
  zoneTicks?: number;
}

export interface ReplayV1MatchResult {
  /** Omitted, rather than written as null, for a draw. */
  winnerSlot?: number;
  reason: 'Elimination' | 'Disqualification' | 'MaxTicks' | 'Domination';
  endTick: number;
  bots: ReplayV1BotMatchResult[];
  controlPressure?: number;
}

export interface ReplayV1CompleteDocument {
  header: ReplayV1Header;
  ticks: ReplayV1Tick[];
  result: ReplayV1MatchResult;
  replayHash: string;
}

/**
 * Shape emitted by the live-broadcast endpoint while the result is withheld.
 *
 * MatchesEndpoints constructs result/replayHash as null, but replay-v1's
 * canonical JSON options omit both null properties. Keep them optional-null
 * for compatibility with the pre-serialization object as well as its actual
 * JSON output. `partial: true` is the required wire discriminator.
 */
export interface ReplayV1PartialDocument {
  header: ReplayV1Header;
  ticks: ReplayV1Tick[];
  result?: null;
  replayHash?: null;
  partial: true;
}

export type ReplayV1Document =
  | ReplayV1CompleteDocument
  | ReplayV1PartialDocument;
