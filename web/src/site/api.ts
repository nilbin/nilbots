import type { components } from '../api/schema';

/**
 * Every response type here is an alias onto the server's generated OpenAPI schema
 * (`src/api/schema.d.ts`), so the site cannot drift from the API. Regenerate with
 * `bash scripts/generate-api-clients.sh` — CI fails if the checked-in output is stale.
 * Do not hand-write response shapes in this file; change the endpoint instead.
 */
type Schemas = components['schemas'];

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

export type Me = Schemas['UserResponse'];

export type EntitlementNotificationItem = Schemas['EntitlementNotificationItem'];
/**
 * A notification's payload is a union discriminated by its own `kind`. Narrow on that
 * before reading fields — the outer `UserNotification.kind` is the same string but
 * TypeScript cannot use it to narrow a sibling property.
 */
export type UserNotificationPayload = Schemas['UserNotificationPayload'];
export type EntitlementEarnedPayload =
  Schemas['UserNotificationPayloadEntitlementEarnedPayload'];
export type MatchSettledPayload = Schemas['UserNotificationPayloadMatchSettledPayload'];
export type SetSettledPayload = Schemas['UserNotificationPayloadSetSettledPayload'];
export type UserNotification = Schemas['UserNotificationResponse'];

export type CosmeticUnlock = Schemas['CosmeticUnlock'];
export type CosmeticCatalogItem = Schemas['CosmeticCatalogEntry'];
export type CosmeticCatalog = Schemas['CosmeticCatalogResponse'];

export type LadderRating = Schemas['BotLadderRatingResponse'];
export type BotSummary = Schemas['BotSummaryResponse'];
export type MyBot = Schemas['MyBotResponse'];
export type BotDetail = Schemas['BotDetailResponse'];
export type BotVersion = Schemas['BotVersionResponse'];
export type LadderStanding = Schemas['LadderStanding'];

export type BotRecord = Schemas['BotRecord'];
export type BotStatistics = Schemas['BotStatistics'];

export type MatchSummaryParticipant = Schemas['MatchSummaryParticipantResponse'];
export type MatchSummary = Schemas['MatchSummaryResponse'];
export type MatchDetailParticipant = Schemas['MatchDetailParticipantResponse'];
export type MatchDetail = Schemas['MatchDetailResponse'];
export type MatchLive = Schemas['MatchLiveResponse'];

export type Meta = Schemas['MetaResponse'];
export type MetaMap = Schemas['MetaMapResponse'];

export type SetGame = Schemas['MatchSetGameResponse'];
export type MatchSetDetail = Schemas['MatchSetResponse'];

export type LeaderboardEntry = Schemas['LeaderboardEntryResponse'];
export type Leaderboard = Schemas['LeaderboardResponse'];
