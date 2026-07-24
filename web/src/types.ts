// TypeScript mirror of the canonical replay document (BotArena.Engine/Replay.cs).
// The C# canonical serializer is the source of truth; keep this file in sync.

export type Direction = 'North' | 'East' | 'South' | 'West';
export type BotAction =
  | 'Wait'
  | 'MoveForward'
  | 'TurnLeft'
  | 'TurnRight'
  | 'Shoot'
  | 'StrafeLeft'
  | 'StrafeRight';
export type ActionResult = 'None' | 'Success' | 'Blocked' | 'OnCooldown' | 'Faulted';
export type BotStatus = 'Active' | 'Destroyed' | 'Disqualified';
export type GameEventType =
  | 'Turn'
  | 'Move'
  | 'MoveBlocked'
  | 'Shot'
  | 'Damage'
  | 'Destroyed'
  | 'Fault'
  | 'Disqualified';

export interface ReplayParticipant {
  slot: number;
  name: string;
  runtimeKind: string;
  artifactHash: string;
  accent: string;
  spawnX: number;
  spawnY: number;
  spawnFacing: Direction;
}

export interface ReplayHeader {
  replayVersion: number;
  engineVersion: string;
  gameRulesVersion: string;
  runtimeProtocolVersion: string;
  runtimeConfigurationVersion: string;
  mapId: string;
  mapVersion: number;
  mapWidth: number;
  mapHeight: number;
  mapTiles: string[];
  seed: number;
  maxTicks: number;
  visionRange: number;
  /** True when sight is the directional facing cone rather than omnidirectional. */
  visionCone?: boolean;
  /** [x,y] pairs; present only when these rules have zone control (experiment arms). */
  zoneTiles?: number[][];
  /** Absolute domination limit for the shared active-control meter. */
  controlPressureLimit?: number;
  participants: ReplayParticipant[];
}

export interface GameEvent {
  type: GameEventType;
  slot?: number;
  fromX?: number;
  fromY?: number;
  toX?: number;
  toY?: number;
  fromFacing?: Direction;
  toFacing?: Direction;
  hitSlot?: number;
  targetSlot?: number;
  amount?: number;
  newHealth?: number;
  message?: string;
}

export interface ReplayVisibleEnemy {
  slot: number;
  x: number;
  y: number;
  facing: Direction;
  health: number;
}

/** A redacted heard sound: bearing is the 8-way octant 0=N..7=NW (clockwise),
 * distance the band 0=Near/1=Medium/2=Far. */
export interface ReplayHeardSound {
  type: GameEventType;
  bearing: number;
  distance: number;
}

export interface ReplayBotTick {
  slot: number;
  chosenAction: BotAction;
  validatedAction: BotAction;
  result: ActionResult;
  faulted: boolean;
  debug?: string;
  visibleTiles: number[][];
  visibleEnemies: ReplayVisibleEnemy[];
  /** Sounds this bot heard this tick; absent when none or no hearing rules. */
  heardSounds?: ReplayHeardSound[];
}

export interface ReplayBotState {
  slot: number;
  x: number;
  y: number;
  facing: Direction;
  health: number;
  cooldown: number;
  status: BotStatus;
  /** Present only when these rules have an energy system. */
  energy?: number;
  /** Cumulative zone-ticks; present only when the rules emit per-tick tallies —
   * read this, never re-derive accrual in the viewer. */
  zoneTicks?: number;
}

/** Present only under projectile rules; omitted for instant-shot replays. */
export interface ReplayProjectile {
  x: number;
  y: number;
  direction: Direction;
  ownerSlot: number;
  /** 1 = the bolt advances next tick; absent in pre-hardening replays. */
  ticksUntilAdvance?: number;
  /** Tiles it can still advance before despawning (−1 = uncapped). */
  remainingTiles?: number;
  /** Ordered tile substeps taken whenever the bolt advances. */
  tilesPerAdvance?: number;
  /** Stable replay-local identity used for interpolation. */
  id?: number;
}

export interface ReplayProjectileTraversal {
  id: number;
  ownerSlot: number;
  direction: Direction;
  fromX: number;
  fromY: number;
  /** Entered tiles in authoritative order during this tick. */
  path: number[][];
}

export interface ReplayTick {
  tick: number;
  bots: ReplayBotTick[];
  events: GameEvent[];
  state: ReplayBotState[];
  projectiles?: ReplayProjectile[];
  projectileTraversals?: ReplayProjectileTraversal[];
  /** Signed shared objective pressure after this tick. */
  controlPressure?: number;
}

export interface BotMatchResult {
  slot: number;
  outcome: 'Win' | 'Loss' | 'Draw';
  finalHealth: number;
  damageDealt: number;
  faults: number;
  finalStatus: BotStatus;
  /** Present only when these rules have zone control. */
  zoneTicks?: number;
}

export interface MatchResult {
  winnerSlot: number | null;
  reason: 'Elimination' | 'Disqualification' | 'MaxTicks' | 'Domination';
  endTick: number;
  bots: BotMatchResult[];
  /** Final signed shared objective pressure. */
  controlPressure?: number;
}

export interface ReplayDocument {
  header: ReplayHeader;
  ticks: ReplayTick[];
  /** Absent while a live broadcast is still withholding the outcome. */
  result?: MatchResult;
  replayHash?: string;
  partial?: boolean;
}

declare global {
  interface Window {
    __BOTARENA_REPLAY__?: ReplayDocument;
  }
}
