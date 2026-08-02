import type { components } from '../api/schema';
import { loadReplayJson, type LoadedReplay } from '../replayIngress';

/**
 * Every response type here is an alias onto the server's generated OpenAPI schema
 * (`src/api/schema.d.ts`), so the site cannot drift from the API. Regenerate with
 * `bash scripts/generate-api-clients.sh` — CI fails if the checked-in output is stale.
 * Do not hand-write response shapes in this file; change the endpoint instead.
 */
type Schemas = components['schemas'];

export type ApplicationProblem = Schemas['ApplicationProblemResponse'];

export class ApiError extends Error {
  public status: number;
  public code: string | null;
  public traceId: string | null;
  public retryAfterSeconds: number | null;

  constructor(
    status: number,
    message: string,
    code: string | null = null,
    traceId: string | null = null,
    retryAfterSeconds: number | null = null,
  ) {
    super(message);
    this.status = status;
    this.code = code;
    this.traceId = traceId;
    this.retryAfterSeconds = retryAfterSeconds;
  }
}

async function request<T>(method: string, url: string, body?: unknown): Promise<T> {
  const response = await responseFor(method, url, body);
  if (response.status === 204) return undefined as T;
  return (await response.json()) as T;
}

async function requestText(method: string, url: string): Promise<string> {
  const response = await responseFor(method, url);
  return response.text();
}

async function responseFor(
  method: string,
  url: string,
  body?: unknown,
): Promise<Response> {
  const response = await fetch(url, {
    method,
    headers: body !== undefined ? { 'Content-Type': 'application/json' } : undefined,
    body: body !== undefined ? JSON.stringify(body) : undefined,
    credentials: 'same-origin',
  });
  if (!response.ok) {
    let message = response.statusText;
    let code: string | null = null;
    let traceId: string | null = null;
    let retryAfterSeconds: number | null = null;
    try {
      const problem =
        (await response.json()) as Partial<ApplicationProblem>;
      message = problem.detail ?? problem.title ?? message;
      code = typeof problem.code === 'string' ? problem.code : null;
      traceId =
        typeof problem.traceId === 'string' ? problem.traceId : null;
      retryAfterSeconds =
        typeof problem.retryAfterSeconds === 'number'
          ? problem.retryAfterSeconds
          : null;
    } catch {
      /* not json */
    }
    throw new ApiError(
      response.status,
      message,
      code,
      traceId,
      retryAfterSeconds,
    );
  }
  return response;
}

/**
 * The raw verbs, for one-off calls that do not warrant a named endpoint (mutations, the
 * odd admin action). Prefer `endpoints` below for anything a page reads.
 */
export const api = {
  get: <T>(url: string) => request<T>('GET', url),
  getText: (url: string) => requestText('GET', url),
  post: <T>(url: string, body?: unknown) => request<T>('POST', url, body),
  put: <T>(url: string, body?: unknown) => request<T>('PUT', url, body),
};

export type Me = Schemas['UserResponse'];
export type AuthProviders = Schemas['AuthProvidersResponse'];
export type StorePackItem = Schemas['StorePackItemResponse'];
export type StorePack = Schemas['StorePackResponse'];
export type StoreCategory = Schemas['StoreCategoryResponse'];
export type Store = Schemas['StoreResponse'];

export type EntitlementNotificationItem = Schemas['EntitlementNotificationItem'];
/**
 * A notification's payload is a union discriminated by its own `kind`. Narrow on that
 * before reading fields — the outer `UserNotification.kind` is the same string but
 * TypeScript cannot use it to narrow a sibling property.
 */
export type UserNotificationPayload = Schemas['UserNotificationPayload'];
export type EntitlementEarnedPayload =
  Schemas['UserNotificationPayloadEntitlementEarnedPayload'];
export type MatchChallengedPayload = Schemas['UserNotificationPayloadMatchChallengedPayload'];
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
export type BotMatchHistory = Schemas['BotMatchHistoryResponse'];

export type MatchSummaryParticipant = Schemas['MatchSummaryParticipantResponse'];
export type MatchSummary = Schemas['MatchSummaryResponse'];
export type MatchDetailParticipant = Schemas['MatchDetailParticipantResponse'];
export type MatchDetail = Schemas['MatchDetailResponse'];
export type MatchLive = Schemas['MatchLiveResponse'];

export type Meta = Schemas['MetaResponse'];
export type MetaMap = Schemas['MetaMapResponse'];
export type ArenaCapabilities = Schemas['ArenaCapabilitiesResponse'];
export type ArenaAllowance = Schemas['ArenaAllowanceResponse'];
export type RankedArenaAllowance =
  Schemas['RankedArenaAllowanceResponse'];
export type MatchPlayability = Schemas['MatchPlayabilityResponse'];

export type SetGame = Schemas['MatchSetGameResponse'];
export type MatchSetDetail = Schemas['MatchSetResponse'];

export type LeaderboardEntry = Schemas['LeaderboardEntryResponse'];
export type Leaderboard = Schemas['LeaderboardResponse'];

export type LabsCatalog = Schemas['LabsCatalogResponse'];
export type LabsPlaylist = Schemas['LabsPlaylistResponse'];
export type CreatedMatch = Schemas['CreatedMatchResponse'];
export type ArcRelayCatalog = Schemas['ArcRelayCatalogResponse'];
export type ArcRelayClass = Schemas['ArcRelayClassResponse'];
export type ArcRelaySheet = Schemas['ArcRelaySheetResponse'];
export type ArcRelaySheetDocument = Schemas['ArcRelaySheetDocument'];
export type ArcRelaySheetSlot = Schemas['ArcRelaySheetSlot'];
export type ArcRelaySheetPoint = Schemas['ArcRelaySheetPoint'];
export type ArcRelaySheetGambit = Schemas['ArcRelaySheetGambit'];
export type ArcRelayEntrant = Schemas['ArcRelayEntrantCardResponse'];
export type ArcRelayLadder = Schemas['ArcRelayLadderResponse'];
export type ArcRelayMind = Schemas['ArcRelayMindResponse'];
export type ArcRelayCrest = Schemas['ArcRelayCrestDescriptor'];
export type ArcRelayCrestOptions = Schemas['ArcRelayCrestOptionsResponse'];

export type CreateBotRequest = Schemas['CreateBotRequest'];
export type CreatedBot = Schemas['CreatedBot'];
export type AssignBotClassRequest = Schemas['AssignBotClassRequest'];
export type AssignedBotClass = Schemas['AssignedBotClass'];
export type CreateLabsMatchRequest = Schemas['CreateLabsMatchRequest'];
export type SaveArcRelaySheetRequest = Schemas['SaveArcRelaySheetRequest'];
export type CreateArcRelayScrimmageRequest = Schemas['CreateArcRelayScrimmageRequest'];
export type CreateArcRelayMindRequest = Schemas['CreateArcRelayMindRequest'];
export type ReviseArcRelayMindRequest = Schemas['ReviseArcRelayMindRequest'];
export type SetArcRelayLadderOptInRequest = Schemas['SetArcRelayLadderOptInRequest'];
export type SetArcRelayCrestRequest = Schemas['SetArcRelayCrestRequest'];
export type SubmitVersionRequest = Schemas['SubmitVersionRequest'];
export type ChallengeRequest = Schemas['ChallengeRequest'];
export type RankedChallengeRequest = Schemas['RankedChallengeRequest'];
export type RegisterRequest = Schemas['RegisterRequest'];
export type LoginRequest = Schemas['LoginRequest'];
export type UpdateBotAppearanceRequest = Schemas['UpdateBotAppearanceRequest'];

/**
 * Every endpoint the site reads, with its path and response type bound together in one
 * place.
 *
 * `api.get<BotSummary[]>('/api/bots')` states the two separately at each call site, so
 * nothing stops them disagreeing — a path typo or a changed response shape type-checks
 * perfectly and fails at runtime. Naming the endpoint once means the compiler carries the
 * pairing to all of them, which is what the mobile client already does.
 */
export const endpoints = {
  meta: () => api.get<Meta>('/api/meta'),
  /**
   * The signed-in account, or null.
   *
   * 401 is the *answer* here, not a failure — "nobody is signed in" is what an anonymous
   * visitor should get, and surfacing it as an error would put an error state on every
   * page of a public site.
   */
  me: async (): Promise<Me | null> => {
    try {
      return await api.get<Me>('/api/accounts/me');
    } catch (cause) {
      if (cause instanceof ApiError && cause.status === 401) return null;
      throw cause;
    }
  },
  logout: () => api.post<unknown>('/api/accounts/logout'),
  /** Which external sign-ins this deployment can offer. Unconfigured ones must not render. */
  authProviders: () => api.get<AuthProviders>('/api/accounts/providers'),
  bots: () => api.get<BotSummary[]>('/api/bots'),
  bot: (key: string) => api.get<BotDetail>(`/api/bots/${key}`),
  botMatches: (botId: string) => api.get<BotMatchHistory>(`/api/bots/${botId}/matches`),
  botStats: (botId: string) => api.get<BotStatistics>(`/api/bots/${botId}/stats`),
  arena: () => api.get<ArenaCapabilities>('/api/arena'),
  labs: () => api.get<LabsCatalog>('/api/labs'),
  arcRelayCatalog: () => api.get<ArcRelayCatalog>('/api/arc-relay/catalog'),
  arcRelaySheets: () => api.get<ArcRelaySheet[]>('/api/arc-relay/sheets'),
  arcRelayEntrants: () => api.get<ArcRelayEntrant[]>('/api/arc-relay/entrants'),
  arcRelayLadder: () => api.get<ArcRelayLadder>('/api/arc-relay/ladder'),
  arcRelayMind: (entrantId: string) =>
    api.get<ArcRelayMind>(`/api/arc-relay/minds/${entrantId}`),
  leaderboard: (rules?: string | null) =>
    api.get<Leaderboard>(
      `/api/leaderboard${rules ? `?rules=${encodeURIComponent(rules)}` : ''}`,
    ),
  matches: (query: string) => api.get<MatchSummary[]>(query),
  match: (matchId: string) => api.get<MatchDetail>(`/api/matches/${matchId}`),
  matchLive: (matchId: string) => api.get<MatchLive>(`/api/matches/${matchId}/live`),
  matchReplay: async (matchId: string): Promise<LoadedReplay> =>
    loadReplayJson(
      await api.getText(`/api/matches/${matchId}/replay`),
    ),
  matchSet: (setId: string) => api.get<MatchSetDetail>(`/api/matchsets/${setId}`),
  myBots: () => api.get<MyBot[]>('/api/bots/mine'),
  cosmetics: () => api.get<CosmeticCatalog>('/api/cosmetics'),
  store: () => api.get<Store>('/api/store'),
  notifications: () => api.get<UserNotification[]>('/api/notifications?take=20'),

  // Writes, bound the same way and for the same reason. The request bodies are generated
  // too, so a field the server renamed fails here rather than silently posting a shape it
  // ignores — which a bare `api.post(url, { ... })` cannot catch at all.
  createBot: (body: CreateBotRequest) => api.post<CreatedBot>('/api/bots', body),
  assignBotClass: (botId: string, body: AssignBotClassRequest) =>
    api.put<AssignedBotClass>(`/api/bots/${botId}/class`, body),
  submitVersion: (botId: string, body: SubmitVersionRequest) =>
    api.post<unknown>(`/api/bots/${botId}/versions`, body),
  challenge: (body: ChallengeRequest) =>
    api.post<{ id: string }>('/api/matches/challenge', body),
  rankedChallenge: (body: RankedChallengeRequest) =>
    api.post<{ id: string }>('/api/matches/ranked', body),
  createLabsMatch: (body: CreateLabsMatchRequest) =>
    api.post<CreatedMatch>('/api/labs/matches', body),
  createArcRelaySheet: (body: SaveArcRelaySheetRequest) =>
    api.post<ArcRelaySheet>('/api/arc-relay/sheets', body),
  updateArcRelaySheet: (sheetId: string, body: SaveArcRelaySheetRequest) =>
    api.put<ArcRelaySheet>(`/api/arc-relay/sheets/${sheetId}`, body),
  createArcRelayScrimmage: (body: CreateArcRelayScrimmageRequest) =>
    api.post<CreatedMatch>('/api/arc-relay/scrimmages', body),
  createArcRelayMind: (body: CreateArcRelayMindRequest) =>
    api.post<ArcRelayMind>('/api/arc-relay/minds', body),
  reviseArcRelayMind: (entrantId: string, body: ReviseArcRelayMindRequest) =>
    api.put<ArcRelayMind>(`/api/arc-relay/minds/${entrantId}`, body),
  arcRelayCrestOptions: (entrantId: string) =>
    api.get<ArcRelayCrestOptions>(`/api/arc-relay/entrants/${entrantId}/crest-options`),
  setArcRelayCrest: (entrantId: string, body: SetArcRelayCrestRequest) =>
    api.put<ArcRelayCrest>(`/api/arc-relay/entrants/${entrantId}/crest`, body),
  preflightArcRelayMind: (entrantId: string) =>
    api.post<Schemas['ArcRelayPreflightResponse']>(`/api/arc-relay/entrants/${entrantId}/preflight`, {}),
  setArcRelayLadder: (entrantId: string, body: SetArcRelayLadderOptInRequest) =>
    api.put<ArcRelayEntrant>(`/api/arc-relay/entrants/${entrantId}/ladder`, body),
  updateAppearance: (botId: string, body: UpdateBotAppearanceRequest) =>
    api.put<unknown>(`/api/bots/${botId}/appearance`, body),
  register: (body: RegisterRequest) => api.post<unknown>('/api/accounts/register', body),
  login: (body: LoginRequest) => api.post<unknown>('/api/accounts/login', body),
  readNotification: (id: string) => api.post<unknown>(`/api/notifications/${id}/read`, {}),
};
