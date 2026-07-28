import type {
  AuthProviders,
  BotDetail,
  BotMatchHistory,
  BotStatistics,
  BotSummary,
  CosmeticCatalog,
  LabsCatalog,
  Leaderboard,
  MatchDetail,
  MatchLive,
  MatchSetDetail,
  MatchSummary,
  Me,
  Meta,
  MyBot,
  Store,
  UserNotification,
} from '../api';
import {
  REVIEW_COMPLETED_MATCH_ID,
  REVIEW_FAILED_MATCH_ID,
  REVIEW_LIVE_MATCH_ID,
  REVIEW_SET_ID,
  authProvidersFixture,
  botDetailsFixture,
  botMatchHistoryFixtures,
  botStatisticsFixtures,
  botsFixture,
  cosmeticCatalogFixture,
  completedMatchDetailFixture,
  completedMatchLiveFixture,
  completedSetMatchDetailFixtures,
  completedSetMatchLiveFixtures,
  currentLeaderboardFixture,
  emptyMyBotsFixture,
  failedMatchDetailFixture,
  failedMatchLiveFixture,
  liveMatchFixture,
  liveMatchDetailFixture,
  labsCatalogFixture,
  matchSetFixture,
  matchesFixture,
  meFixture,
  metaFixture,
  myBotsFixture,
  notificationsFixture,
  previousLeaderboardFixture,
  storeFixture,
} from './fixtures';

type SiteReviewBody =
  | AuthProviders
  | BotDetail
  | BotMatchHistory
  | BotStatistics
  | BotSummary[]
  | CosmeticCatalog
  | LabsCatalog
  | Leaderboard
  | MatchDetail
  | MatchLive
  | MatchSetDetail
  | MatchSummary[]
  | Me
  | Meta
  | MyBot[]
  | Store
  | UserNotification[]
  | SiteReviewProblem;

interface SiteReviewProblem {
  readonly type: 'about:blank';
  readonly title: string;
  readonly status: 401;
  readonly detail: string;
}

export interface SiteReviewApiResponse<
  TBody extends SiteReviewBody = SiteReviewBody,
> {
  readonly status: 200 | 401;
  readonly body: TBody;
}

export interface SiteReviewRequestContext {
  readonly referer?: string;
}

function ok<TBody extends SiteReviewBody>(
  body: TBody,
): SiteReviewApiResponse<TBody> {
  return { status: 200, body };
}

function anonymousAccount(): SiteReviewApiResponse<SiteReviewProblem> {
  return {
    status: 401,
    body: {
      type: 'about:blank',
      title: 'Not signed in',
      status: 401,
      detail: 'The site-review login scenario is intentionally anonymous.',
    },
  };
}

/**
 * The explicit response generic is deliberate: it binds a route to its generated API
 * shape. A store fixture accidentally placed on `/api/meta`, for example, fails here at
 * compile time instead of remaining valid merely because both belong to one broad union.
 */
function endpoint<TBody extends SiteReviewBody>(
  key: string,
  body: TBody,
): readonly [string, SiteReviewApiResponse] {
  return [key, ok(body)];
}

const exactRoutes = new Map<string, SiteReviewApiResponse>([
  endpoint<Meta>('GET /api/meta', metaFixture),
  endpoint<Me>('GET /api/accounts/me', meFixture),
  endpoint<AuthProviders>(
    'GET /api/accounts/providers',
    authProvidersFixture,
  ),
  endpoint<UserNotification[]>(
    'GET /api/notifications?take=20',
    notificationsFixture,
  ),
  endpoint<CosmeticCatalog>('GET /api/cosmetics', cosmeticCatalogFixture),
  endpoint<LabsCatalog>('GET /api/labs', labsCatalogFixture),
  endpoint<Store>('GET /api/store', storeFixture),
  endpoint<Leaderboard>('GET /api/leaderboard', currentLeaderboardFixture),
  endpoint<Leaderboard>(
    'GET /api/leaderboard?rules=0.5',
    currentLeaderboardFixture,
  ),
  endpoint<Leaderboard>(
    'GET /api/leaderboard?rules=0.4',
    previousLeaderboardFixture,
  ),
  endpoint<BotSummary[]>('GET /api/bots', botsFixture),
  endpoint<MyBot[]>('GET /api/bots/mine', myBotsFixture),
  endpoint<MatchLive>(
    `GET /api/matches/${REVIEW_LIVE_MATCH_ID}/live`,
    liveMatchFixture,
  ),
  endpoint<MatchDetail>(
    `GET /api/matches/${REVIEW_LIVE_MATCH_ID}`,
    liveMatchDetailFixture,
  ),
  endpoint<MatchLive>(
    `GET /api/matches/${REVIEW_COMPLETED_MATCH_ID}/live`,
    completedMatchLiveFixture,
  ),
  endpoint<MatchDetail>(
    `GET /api/matches/${REVIEW_COMPLETED_MATCH_ID}`,
    completedMatchDetailFixture,
  ),
  endpoint<MatchLive>(
    `GET /api/matches/${REVIEW_FAILED_MATCH_ID}/live`,
    failedMatchLiveFixture,
  ),
  endpoint<MatchDetail>(
    `GET /api/matches/${REVIEW_FAILED_MATCH_ID}`,
    failedMatchDetailFixture,
  ),
  endpoint<MatchSetDetail>(
    `GET /api/matchsets/${REVIEW_SET_ID}`,
    matchSetFixture,
  ),
]);

for (const bot of botDetailsFixture) {
  exactRoutes.set(`GET /api/bots/${bot.slug}`, ok<BotDetail>(bot));
  exactRoutes.set(`GET /api/bots/${bot.id}`, ok<BotDetail>(bot));

  const statistics = botStatisticsFixtures[bot.id];
  const history = botMatchHistoryFixtures[bot.id];
  if (!statistics || !history) {
    throw new Error(`Incomplete site-review bot routes for ${bot.id}.`);
  }
  exactRoutes.set(
    `GET /api/bots/${bot.id}/stats`,
    ok<BotStatistics>(statistics),
  );
  exactRoutes.set(
    `GET /api/bots/${bot.id}/matches`,
    ok<BotMatchHistory>(history),
  );
}

completedSetMatchDetailFixtures.forEach((detail, index) => {
  const live = completedSetMatchLiveFixtures[index];
  if (!live) {
    throw new Error(`Missing site-review live state for ${detail.id}.`);
  }
  exactRoutes.set(
    `GET /api/matches/${detail.id}`,
    ok<MatchDetail>(detail),
  );
  exactRoutes.set(
    `GET /api/matches/${detail.id}/live`,
    ok<MatchLive>(live),
  );
});

/**
 * Resolve a controlled API response. Exact routes stay exact; the match feed is the one
 * parameterized endpoint because its visible filters are part of the screen under review.
 */
export function siteReviewApiResponse(
  method: string | undefined,
  requestUrl: string,
  context: SiteReviewRequestContext = {},
): SiteReviewApiResponse | undefined {
  const url = new URL(requestUrl, 'http://nilbots.site-review');
  const verb = (method ?? 'GET').toUpperCase();

  if (
    verb === 'GET' &&
    url.pathname === '/api/accounts/me' &&
    url.search === '' &&
    isAnonymousReview(context.referer)
  ) {
    return anonymousAccount();
  }

  if (
    verb === 'GET' &&
    url.pathname === '/api/bots/mine' &&
    url.search === '' &&
    isFirstRunReview(context.referer)
  ) {
    return ok<MyBot[]>(emptyMyBotsFixture);
  }

  if (verb === 'GET' && url.pathname === '/api/matches') {
    return filteredMatches(url);
  }

  return exactRoutes.get(siteReviewRequestKey(verb, requestUrl));
}

export function siteReviewRequestKey(
  method: string | undefined,
  requestUrl: string,
): string {
  const url = new URL(requestUrl, 'http://nilbots.site-review');
  return `${(method ?? 'GET').toUpperCase()} ${url.pathname}${url.search}`;
}

function filteredMatches(url: URL): SiteReviewApiResponse<MatchSummary[]> | undefined {
  const allowed = new Set(['take', 'skip', 'bot', 'map', 'ranked']);
  if ([...url.searchParams.keys()].some((key) => !allowed.has(key))) {
    return undefined;
  }
  if (
    [...allowed].some((key) => url.searchParams.getAll(key).length > 1) ||
    url.searchParams.get('take') !== '30'
  ) {
    return undefined;
  }

  const skipText = url.searchParams.get('skip');
  const skip = skipText === null ? 0 : Number(skipText);
  if (!Number.isSafeInteger(skip) || skip < 0) return undefined;

  const botSlug = url.searchParams.get('bot');
  const map = url.searchParams.get('map');
  const ranked = url.searchParams.get('ranked');
  if (ranked !== null && ranked !== 'true' && ranked !== 'false') {
    return undefined;
  }

  const botName =
    botSlug === null
      ? null
      : (botsFixture.find((bot) => bot.slug === botSlug)?.name ?? null);
  if (botSlug !== null && botName === null) return ok<MatchSummary[]>([]);

  const filtered = matchesFixture.filter(
    (match) =>
      (botName === null ||
        match.participants.some(
          (participant) => participant.nameSnapshot === botName,
        )) &&
      (map === null || match.mapId === map) &&
      (ranked === null ||
        (ranked === 'true'
          ? match.matchSetId !== null
          : match.matchSetId === null)),
  );

  return ok<MatchSummary[]>(filtered.slice(skip, skip + 30));
}

function isFirstRunReview(referer: string | undefined): boolean {
  if (!referer) return false;
  try {
    const page = new URL(referer);
    return (
      page.pathname === '/garage' &&
      page.searchParams.get('review') === 'first-run'
    );
  } catch {
    return false;
  }
}

function isAnonymousReview(referer: string | undefined): boolean {
  if (!referer) return false;
  try {
    return new URL(referer).pathname === '/login';
  } catch {
    return false;
  }
}
