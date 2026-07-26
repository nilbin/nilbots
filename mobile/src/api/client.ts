import type { components } from './schema';
import { API_BASE_URL } from './config';

/**
 * Every response type is an alias onto the server's generated OpenAPI schema, exactly as
 * the site does it. Regenerate with `bash scripts/generate-api-clients.sh`; CI fails if the
 * committed output is stale. Never hand-write a response shape here.
 */
type Schemas = components['schemas'];

export type Meta = Schemas['MetaResponse'];
export type BotSummary = Schemas['BotSummaryResponse'];
export type BotDetail = Schemas['BotDetailResponse'];
export type BotVersion = Schemas['BotVersionResponse'];
export type Leaderboard = Schemas['LeaderboardResponse'];
export type LeaderboardEntry = Schemas['LeaderboardEntryResponse'];
export type MatchSummary = Schemas['MatchSummaryResponse'];
export type MatchDetail = Schemas['MatchDetailResponse'];
export type MatchLive = Schemas['MatchLiveResponse'];
export type MatchSet = Schemas['MatchSetResponse'];
export type BotMatchHistory = Schemas['BotMatchHistoryResponse'];
export type BotStatistics = Schemas['BotStatistics'];
export type MyBot = Schemas['MyBotResponse'];

export class ApiError extends Error {
  constructor(
    public status: number,
    message: string,
  ) {
    super(message);
  }
}

/**
 * Supplies a valid access token, or null when signed out.
 *
 * Injected by `AuthProvider` rather than imported, because auth needs this module (the
 * API base URL, this very setter) and importing it back would be a cycle. It also keeps
 * the folder contract intact: this stays the only module that calls `fetch`, and auth
 * stays the only module that knows how a token is obtained or renewed.
 */
let accessTokenProvider: () => Promise<string | null> = async () => null;

export function setAccessTokenProvider(provider: () => Promise<string | null>): void {
  accessTokenProvider = provider;
}

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  // Asked for per request, not cached here: the provider renews an expired token, so
  // holding one would mean sending a stale one after the first hour.
  const token = await accessTokenProvider();

  let response: Response;
  try {
    response = await fetch(`${API_BASE_URL}${path}`, {
      ...init,
      headers: {
        Accept: 'application/json',
        ...(token ? { Authorization: `Bearer ${token}` } : {}),
        ...init?.headers,
      },
    });
  } catch (cause) {
    // A dead dev server is the overwhelmingly likely cause on a phone, and the default
    // "Network request failed" says nothing about which host was even tried.
    throw new ApiError(0, `Cannot reach ${API_BASE_URL} — is the server running?`);
  }

  if (!response.ok) {
    let message = response.statusText;
    try {
      const problem = await response.json();
      message = problem.detail ?? problem.title ?? message;
    } catch {
      /* not a problem+json body */
    }
    throw new ApiError(response.status, message);
  }
  if (response.status === 204) return undefined as T;
  return (await response.json()) as T;
}

export const api = {
  meta: () => request<Meta>('/api/meta'),
  bots: () => request<BotSummary[]>('/api/bots'),
  bot: (key: string) => request<BotDetail>(`/api/bots/${key}`),
  botMatches: (botId: string) => request<BotMatchHistory>(`/api/bots/${botId}/matches`),
  botStats: (botId: string) => request<BotStatistics>(`/api/bots/${botId}/stats`),
  leaderboard: (rules?: string) =>
    request<Leaderboard>(`/api/leaderboard${rules ? `?rules=${encodeURIComponent(rules)}` : ''}`),
  matches: () => request<MatchSummary[]>('/api/matches'),
  match: (matchId: string) => request<MatchDetail>(`/api/matches/${matchId}`),
  matchLive: (matchId: string) => request<MatchLive>(`/api/matches/${matchId}/live`),
  matchSet: (setId: string) => request<MatchSet>(`/api/matchsets/${setId}`),
  /** The signed-in player's own bots. 401 when signed out — the garage handles that. */
  myBots: () => request<MyBot[]>('/api/bots/mine'),
};
