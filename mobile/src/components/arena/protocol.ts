/**
 * The contract between the app and the arena WebView.
 *
 * Hand-maintained, and deliberately so: it mirrors `web/src/replayPresentation.ts`, which
 * is itself the replay mirror's neighbour rather than a generated API contract. If a
 * field here stops matching that file, the panels quietly render nothing — so change the
 * two together, the same way `web/src/types.ts` and the engine's replay format move
 * together.
 *
 * Everything rules-derived — control pressure, overtime, zone tallies, hold phases —
 * arrives already computed. The app must not re-derive any of it; a second
 * implementation would be a rules surface that drifts.
 */

export type ArenaParticipant = {
  slot: number;
  name: string;
  accent: string;
  lookId: string;
};

export type ArenaHeader = {
  mapId: string;
  seed: string;
  rulesVersion: string;
  replayHash: string | null;
  tickCount: number;
  maxHealth: number;
  /** A live broadcast's replay is truncated to the ticks released so far. */
  partial: boolean;
  participants: ArenaParticipant[];
};

export type ArenaResult = {
  winnerSlot: number | null;
  reason: string;
  endTick: number;
} | null;

export type ArenaControl = {
  pressure: number;
  limit: number;
  overtime: boolean;
  phase: string | null;
  names: [string, string];
};

export type ArenaBot = {
  slot: number;
  name: string;
  accent: string;
  lookLabel: string;
  runtimeKind: string;
  status: string;
  health: number;
  maxHealth: number;
  cooldown: number;
  energy?: number;
  zoneTicks: number | null;
  holdingZone: boolean;
  action?: string;
  actionResult?: string;
  debug?: string;
  visibleTiles: number;
  visibleEnemies: { x: number; y: number }[];
};

export type ArenaTick = {
  tick: number;
  control: ArenaControl | null;
  bots: ArenaBot[];
};

export type ArenaTransport = {
  playing: boolean;
  speed: number;
  tick: number;
  tickCount: number;
  atEnd: boolean;
  /**
   * The page is following a live broadcast rather than playing a replay. The transport
   * is hidden entirely while true — every viewer is on the same tick by design, and
   * seeking would desynchronise this one from all of them.
   */
  following: boolean;
};

/** Messages the page sends out. */
export type ArenaMessage =
  | { type: 'ready' }
  | { type: 'loaded' }
  | { type: 'error'; reason?: string }
  | ({ type: 'replay'; header: ArenaHeader; result: ArenaResult })
  | ({ type: 'tick' } & ArenaTick)
  | ({ type: 'transport' } & ArenaTransport)
  | { type: 'selected'; slot: number | null };

/** Playback speeds offered by the transport, matching the site's. */
export const SPEEDS = [0.5, 1, 2, 4] as const;
