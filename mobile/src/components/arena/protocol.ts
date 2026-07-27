/**
 * The bridge-v2 contract between the app and the arena WebView.
 *
 * Hand-maintained, and deliberately so: it mirrors `web/src/hostedBridge.ts` and
 * `web/src/replayPresentation.ts`. The WebView owns replay decoding and every
 * rules-derived presentation value. Mobile is only a consumer of these messages.
 */

export const ARENA_BRIDGE_VERSION = 2 as const;

export type ArenaUnitKey = string;

export type ArenaParticipant = {
  participantId: number;
  teamId: number;
  name: string;
  accent: string;
  lookId: string | null;
};

export type ArenaTeam = {
  teamId: number;
  unitKeys: ArenaUnitKey[];
};

export type ArenaUnit = {
  unitKey: ArenaUnitKey;
  teamId: number;
  unitId: number;
  controllerParticipantId: number;
  initialLifeId: number | null;
  initialFormId: string | null;
};

export type ArenaForm = {
  formId: string;
  maxHealth: number;
  canMove: boolean;
  canShoot: boolean;
  omnidirectionalVision: boolean;
  omnidirectionalShooting: boolean;
  objectiveWeight: number;
  allowedActionIds: string[];
};

export type ArenaHeader = {
  replayVersion: 1 | 2;
  mapId: string;
  /** Canonical decimal text. Consult seedExact before presenting it as exact. */
  seed: string;
  seedExact: boolean;
  rulesVersion: string;
  replayHash: string | null;
  tickCount: number;
  /** A live broadcast's replay is truncated to the ticks released so far. */
  partial: boolean;
  participants: ArenaParticipant[];
  teams: ArenaTeam[];
  units: ArenaUnit[];
  forms: ArenaForm[];
};

export type ArenaTeamResult = {
  teamId: number;
  outcome: 'win' | 'loss' | 'draw';
  activeHealth: number;
  /** Canonical decimal text; never coerce this through a JavaScript number. */
  damageDealt: string;
  units: ArenaUnitResult[];
};

export type ArenaFormTransition = {
  fromFormId: string;
  toFormId: string;
  startedAtTick: number;
  completesAtTick: number;
};

export type ArenaUnitResult = {
  unitKey: ArenaUnitKey;
  teamId: number;
  unitId: number;
  defaultFormId: string;
  formId: string;
  pendingFormTransition: ArenaFormTransition | null;
  lifecycleStatus: string;
  activeActorKey: string | null;
  health: number;
  /** Canonical decimal text; never coerce this through a JavaScript number. */
  damageDealt: string;
};

export type ArenaResult = {
  winnerTeamId: number | null;
  reason: string;
  endTick: number;
  /** Canonical decimal text when the result includes a territorial tiebreak. */
  territorialScore: string | null;
  teams: ArenaTeamResult[];
} | null;

export type ArenaLegacyControlObjective = {
  kind: 'legacy-control';
  pressure: number;
  limit: number;
  overtime: boolean;
  phase: string | null;
  names: [string, string];
};

export type ArenaFrontlineObjective = {
  kind: 'frontline';
  activePositionIndex: number;
  positionCount: number;
  claimingTeamId: number | null;
  captureProgress: number;
  captureThreshold: number;
  controlResumesAtTick: number;
  winnerTeamId: number | null;
  phase: string;
};

export type ArenaObjective =
  | ArenaLegacyControlObjective
  | ArenaFrontlineObjective;

export type ArenaUnitPresentation = {
  unitKey: ArenaUnitKey;
  /** Exact runtime-life identity; null while this stable unit has no body. */
  actorKey: string | null;
  teamId: number;
  unitId: number;
  lifeId: number | null;
  participantId: number;
  /** Replay-v1 compatibility identity; absent from replay-v2 units. */
  legacySlot: number | null;
  name: string;
  accent: string;
  lookLabel: string;
  runtimeKind: string;
  formId: string;
  canMove: boolean;
  omnidirectionalVision: boolean;
  omnidirectionalShooting: boolean;
  status: string;
  respawnAtTick: number | null;
  unlockAtTick: number | null;
  rebuildReadyAtTick: number | null;
  fabricationAtTick: number | null;
  reservedSpawn: { x: number; y: number } | null;
  pendingSpawnReason: string | null;
  pendingFormTransition: ArenaFormTransition | null;
  health: number;
  maxHealth: number;
  cooldown: number;
  energy: number | null;
  zoneTicks: number | null;
  holdingObjective: boolean;
  actionId: string | null;
  actionLaunchHeading:
    | 'north'
    | 'north-east'
    | 'east'
    | 'south-east'
    | 'south'
    | 'south-west'
    | 'west'
    | 'north-west'
    | null;
  actionResult: string | null;
  debug: string | null;
  visibleTiles: number;
  visibleEnemies: { x: number; y: number }[];
};

export type ArenaTick = {
  type: 'tick';
  bridgeVersion: typeof ARENA_BRIDGE_VERSION;
  tick: number;
  objective: ArenaObjective | null;
  units: ArenaUnitPresentation[];
};

export type ArenaTransport = {
  type: 'transport';
  bridgeVersion: typeof ARENA_BRIDGE_VERSION;
  playing: boolean;
  speed: number;
  tick: number;
  tickCount: number;
  atEnd: boolean;
  /**
   * The page is following a live broadcast rather than playing a replay. The transport
   * is hidden entirely while true — every viewer is on the same tick by design.
   */
  following: boolean;
  loading: boolean;
  pendingAssets: number;
};

export type ArenaControlMethod =
  | 'play'
  | 'pause'
  | 'toggle'
  | 'restart'
  | 'step'
  | 'seek'
  | 'setSpeed'
  | 'setVisibility'
  | 'selectUnit';

/** Messages bridge v2 sends from the page to the native app. */
export type ArenaMessage =
  | {
      type: 'ready';
      bridgeVersion: typeof ARENA_BRIDGE_VERSION;
    }
  | {
      type: 'loaded';
      bridgeVersion: typeof ARENA_BRIDGE_VERSION;
    }
  | {
      type: 'error';
      bridgeVersion: typeof ARENA_BRIDGE_VERSION;
      reason?: string;
    }
  | {
      type: 'replay';
      bridgeVersion: typeof ARENA_BRIDGE_VERSION;
      header: ArenaHeader;
      result: ArenaResult;
    }
  | ArenaTick
  | ArenaTransport
  | {
      type: 'selected';
      bridgeVersion: typeof ARENA_BRIDGE_VERSION;
      unitKey: ArenaUnitKey | null;
    };

/** Playback speeds offered by the transport, matching the site's. */
export const SPEEDS = [0.5, 1, 2, 4] as const;
