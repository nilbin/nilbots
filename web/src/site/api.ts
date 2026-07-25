export class ApiError extends Error {
  constructor(
    public status: number,
    message: string,
  ) {
    super(message);
  }
}

async function request<T>(method: string, url: string, body?: unknown): Promise<T> {
  const response = await fetch(url, {
    method,
    headers: body !== undefined ? { 'Content-Type': 'application/json' } : undefined,
    body: body !== undefined ? JSON.stringify(body) : undefined,
    credentials: 'same-origin',
  });
  if (!response.ok) {
    let message = response.statusText;
    try {
      const problem = await response.json();
      message = problem.detail ?? problem.title ?? message;
    } catch {
      /* not json */
    }
    throw new ApiError(response.status, message);
  }
  if (response.status === 204) return undefined as T;
  return (await response.json()) as T;
}

export const api = {
  get: <T>(url: string) => request<T>('GET', url),
  post: <T>(url: string, body?: unknown) => request<T>('POST', url, body),
  put: <T>(url: string, body?: unknown) => request<T>('PUT', url, body),
};

export interface Me {
  id: string;
  displayName: string;
  email: string;
}

export interface EntitlementNotificationItem {
  key: string;
  kind: 'bot-look' | 'projectile-look';
  id: string;
  label: string;
}

export interface EntitlementEarnedPayload {
  sourceKind: string;
  sourceId: string;
  reason: string | null;
  items: EntitlementNotificationItem[];
}

export interface UserNotification {
  id: string;
  kind: 'entitlement-earned';
  createdAt: string;
  readAt: string | null;
  payload: EntitlementEarnedPayload;
}

export interface CosmeticUnlock {
  sourceKind: string;
  sourceId: string;
  hint: string;
}

export interface CosmeticCatalogItem {
  key: string;
  kind: 'bot-look' | 'projectile-look';
  id: string;
  label: string;
  availability: 'starter' | 'entitlement';
  unlock: CosmeticUnlock | null;
  owned: boolean;
  progress: {
    current: number;
    target: number;
    unit: 'ranked-matches';
  } | null;
}

export interface CosmeticCatalog {
  version: number;
  items: CosmeticCatalogItem[];
}

export interface LadderRating {
  rulesVersion: string;
  rating: number;
  rankedSets: number;
}

export interface BotSummary {
  id: string;
  name: string;
  slug: string;
  accent: string;
  lookId: string;
  projectileLookId: string;
  owner: string;
  /** One entry per rules-version ladder the bot has fought on, newest first. */
  ratings: LadderRating[];
  activeVersion: { id: string; versionNumber: number; artifactHash: string } | null;
  versionCount: number;
}

export interface MyBot {
  id: string;
  name: string;
  slug: string;
  accent: string;
  lookId: string;
  projectileLookId: string;
  latestVersion: { versionNumber: number; status: string; isActive: boolean } | null;
}

export interface BotDetail {
  id: string;
  name: string;
  slug: string;
  accent: string;
  lookId: string;
  projectileLookId: string;
  owner: string;
  isOwner: boolean;
  currentStanding: {
    rulesVersion: string;
    rating: number;
    rankedSets: number;
    rank: number;
  } | null;
  versions: {
    id: string;
    versionNumber: number;
    status: string;
    artifactHash: string | null;
    isActive: boolean;
    createdAt: string;
    buildLog: string | null;
    entryType: string | null;
    sources: { relativePath: string; content: string }[] | null;
  }[];
}

export interface BotRecord {
  played: number;
  wins: number;
  losses: number;
  draws: number;
}

export interface BotStatistics {
  overall: BotRecord;
  ranked: BotRecord;
  unranked: BotRecord;
  combat: {
    games: number;
    damageDealt: number;
    faults: number;
  };
}

export interface MatchSummaryParticipant {
  slot: number;
  nameSnapshot: string;
  ownerDisplayNameSnapshot: string;
  accentSnapshot: string;
  lookIdSnapshot: string;
  projectileLookIdSnapshot: string;
  outcome: string | null;
  finalHealth: number | null;
}

export interface MatchSummary {
  id: string;
  mapId: string;
  status: string;
  broadcasting: boolean;
  matchSetId: string | null;
  setGame: number | null;
  winnerSlot: number | null;
  endReason: string | null;
  endTick: number | null;
  createdAt: string;
  completedAt: string | null;
  participants: MatchSummaryParticipant[];
}

export interface MatchDetailParticipant extends MatchSummaryParticipant {
  botId: string;
  artifactHashSnapshot: string;
  damageDealt: number | null;
  faults: number | null;
}

export interface MatchDetail extends Omit<MatchSummary, 'participants'> {
  seed: number;
  replayHash: string | null;
  error: string | null;
  participants: MatchDetailParticipant[];
}

export interface Meta {
  engineVersion: string;
  gameRulesVersion: string;
  maps: { id: string; width: number; height: number; themeId?: string }[];
}

export interface SetGame {
  id: string;
  game: number;
  mapId: string;
  status: string;
  broadcasting: boolean;
  winnerBotId: string | null;
  draw: boolean;
  participants: {
    slot: number;
    botId: string;
    nameSnapshot: string;
    ownerDisplayNameSnapshot: string;
    accentSnapshot: string;
    lookIdSnapshot: string;
    projectileLookIdSnapshot: string;
  }[];
}

export interface MatchSetDetail {
  id: string;
  status: string;
  rulesVersion: string;
  botA: { id: string; name: string; accent: string; lookId: string };
  botB: { id: string; name: string; accent: string; lookId: string };
  createdAt: string;
  revealed: boolean;
  scoreA: number | null;
  scoreB: number | null;
  ratingChangeA: number | null;
  ratingChangeB: number | null;
  winnerBotId: string | null;
  games: SetGame[];
}

export interface LeaderboardEntry {
  id: string;
  slug: string;
  name: string;
  accent: string;
  lookId: string;
  owner: string;
  rating: number;
  rankedSets: number;
  rank: number;
}

/** One elo ladder per rules version; `ladders` lists every version with results. */
export interface Leaderboard {
  rulesVersion: string;
  /** The one ladder still accepting ranked sets; the rest are historical. */
  activeRulesVersion: string;
  ladders: string[];
  entries: LeaderboardEntry[];
}
