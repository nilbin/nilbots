/**
 * The bridge-v3 contract between the app and the arena WebView.
 *
 * Hand-maintained, and deliberately so: it mirrors `web/src/hostedBridge.ts` and
 * `web/src/replayPresentation.ts`. The WebView owns replay decoding and every
 * rules-derived presentation value. Mobile is only a consumer of these messages.
 * Bridge v3 is replay-version-neutral and currently carries replay sources v1-v3.
 */

export const ARENA_BRIDGE_VERSION = 3 as const;

export type ArenaReplayVersion = 1 | 2 | 3;

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

export type ArenaModeIdentity = {
  kind: string;
  id: string;
};

export type ArenaHeader = {
  replayVersion: ArenaReplayVersion;
  mapId: string;
  /** Canonical decimal text. Consult seedExact before presenting it as exact. */
  seed: string;
  seedExact: boolean;
  rulesVersion: string;
  replayHash: string | null;
  tickCount: number;
  mode: ArenaModeIdentity;
  /** A live broadcast's replay is truncated to the ticks released so far. */
  partial: boolean;
  participants: ArenaParticipant[];
  teams: ArenaTeam[];
  units: ArenaUnit[];
  forms: ArenaForm[];
};

export type ArenaScoreValue = {
  channel: string;
  /** Canonical signed decimal text; never coerce this through a JavaScript number. */
  value: string;
};

export type ArenaTeamResult = {
  teamId: number;
  outcome: 'win' | 'loss' | 'draw';
  activeHealth: number;
  /** Canonical decimal text; never coerce this through a JavaScript number. */
  damageDealt: string;
  rank: number | null;
  scores: ArenaScoreValue[];
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
  /** Canonical decimal text, or null when the replay only exposes team damage. */
  damageDealt: string | null;
};

export type ArenaDeathmatchResult = {
  kind: 'deathmatch';
  reason: string;
  scores: {
    teamKey: string;
    teamId: number;
    /** Canonical decimal text; never coerce this through a JavaScript number. */
    kills: string;
    /** Canonical decimal text; never coerce this through a JavaScript number. */
    deaths: string;
    /** Canonical decimal text; never coerce this through a JavaScript number. */
    damageDealt: string;
  }[];
};

export type ArenaFrontlineResult = {
  kind: 'frontline';
  reason: 'fault-eligibility' | 'base-breach' | 'max-ticks';
  control: {
    kind: 'frontline';
    modeId: string;
    activePositionIndex: number;
    claimingTeamId: number | null;
    captureProgress: number;
    decayTicksElapsed: number;
    controlResumesAtTick: number;
    /** Team a live territory-ratchet hold protects; absent on pre-v3 replays. */
    holdOwnerTeamId?: number | null;
    /** First tick the live hold stops denying regression; absent on pre-v3 replays. */
    holdEndsAtTick?: number | null;
    /** Team owning the declared side objective; null while neutral. */
    secondaryOwnerTeamId?: number | null;
    /** Signed sole-presence ticks claimed on it: + team 0, - team 1. */
    secondaryClaimProgress?: number;
  };
  scores: {
    teamKey: string;
    teamId: number;
    /** Canonical signed decimal text; never coerce this through a JavaScript number. */
    territorialProgress: string;
  }[];
};

export type ArenaModeResult =
  | ArenaDeathmatchResult
  | ArenaFrontlineResult
  | null;

export type ArenaResult = {
  winnerTeamId: number | null;
  reason: string;
  endTick: number;
  reportedEndTick: number | null;
  eligibleTeamIds: number[];
  /** Canonical decimal text when the result includes a territorial tiebreak. */
  territorialScore: string | null;
  mode: ArenaModeResult;
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
  /** Rules-resolved team applying positive capture pressure; never ownership. */
  captureTeamId: number | null;
  /** Whether the capture policy resolves present objective weight as a contest. */
  captureContested: boolean;
  /** Whether redeployment currently prevents pressure from changing the meter. */
  capturePaused: boolean;
  /** Exact replay-v3 ratchet owner; null when no hold is live. */
  holdOwnerTeamId: number | null;
  /** Exact replay-v3 expiry tick; null when no hold is live. */
  holdEndsAtTick: number | null;
  /** Presentation countdown derived from the exact expiry and current tick. */
  holdRemainingTicks: number | null;
  /** Contract-declared ratchet duration, when this ruleset has one. */
  holdDurationTicks: number | null;
  winnerTeamId: number | null;
  phase: string;
};

export type ArenaObjective =
  | ArenaLegacyControlObjective
  | ArenaFrontlineObjective;

export type ArenaTeamScore = {
  teamKey: string;
  teamId: number;
  eligible: boolean;
  scores: ArenaScoreValue[];
};

export type ArenaScoreboard = {
  teams: ArenaTeamScore[];
};

export type ArenaModeState =
  | {
      kind: 'deathmatch';
      modeId: string;
    }
  | {
      kind: 'frontline';
      modeId: string;
      activePositionIndex: number;
      claimingTeamId: number | null;
      captureProgress: number;
      decayTicksElapsed: number;
      controlResumesAtTick: number;
      /** Team a live territory-ratchet hold protects; absent on pre-v3 replays. */
      holdOwnerTeamId?: number | null;
      /** First tick the live hold stops denying regression; absent on pre-v3 replays. */
      holdEndsAtTick?: number | null;
      /** Team owning the declared side objective; null while neutral. */
      secondaryOwnerTeamId?: number | null;
      /** Signed sole-presence ticks claimed on it: + team 0, - team 1. */
      secondaryClaimProgress?: number;
    }
  | {
      kind: string;
      modeId: string;
      state: Readonly<Record<string, unknown>>;
    };

export type ArenaUnitPresentation = {
  unitKey: ArenaUnitKey;
  /** Exact runtime-life identity; null while this stable unit has no body. */
  actorKey: string | null;
  teamId: number;
  unitId: number;
  lifeId: number | null;
  participantId: number;
  /** Replay-v1 compatibility identity; absent from replay-v2/v3 units. */
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
  scoreboard: ArenaScoreboard | null;
  mode: ArenaModeState | null;
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

/** Messages bridge v3 sends from the page to the native app. */
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
