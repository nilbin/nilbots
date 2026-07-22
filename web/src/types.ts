// TypeScript mirror of the canonical replay document (BotArena.Engine/Replay.cs).
// The C# canonical serializer is the source of truth; keep this file in sync.

export type Direction = 'North' | 'East' | 'South' | 'West';
export type BotAction = 'Wait' | 'MoveForward' | 'TurnLeft' | 'TurnRight' | 'Shoot';
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

export interface ReplayBotTick {
  slot: number;
  chosenAction: BotAction;
  validatedAction: BotAction;
  result: ActionResult;
  faulted: boolean;
  debug?: string;
  visibleTiles: number[][];
  visibleEnemies: ReplayVisibleEnemy[];
}

export interface ReplayBotState {
  slot: number;
  x: number;
  y: number;
  facing: Direction;
  health: number;
  cooldown: number;
  status: BotStatus;
}

export interface ReplayTick {
  tick: number;
  bots: ReplayBotTick[];
  events: GameEvent[];
  state: ReplayBotState[];
}

export interface BotMatchResult {
  slot: number;
  outcome: 'Win' | 'Loss' | 'Draw';
  finalHealth: number;
  damageDealt: number;
  faults: number;
  finalStatus: BotStatus;
}

export interface MatchResult {
  winnerSlot: number | null;
  reason: 'Elimination' | 'Disqualification' | 'MaxTicks';
  endTick: number;
  bots: BotMatchResult[];
}

export interface ReplayDocument {
  header: ReplayHeader;
  ticks: ReplayTick[];
  result: MatchResult;
  replayHash: string;
}

declare global {
  interface Window {
    __BOTARENA_REPLAY__?: ReplayDocument;
  }
}
