import {
  useInfiniteQuery,
  useMutation,
  useQuery,
  useQueryClient,
} from '@tanstack/react-query';
import {
  endpoints,
  type MatchDetail,
  type MatchLive,
  type MatchSetDetail,
  type MatchSummary,
  type SubmitVersionRequest,
  type UpdateBotAppearanceRequest,
} from './api';

/**
 * One hook per resource, each owning its own key and cadence.
 *
 * Pages had a `useEffect` + `useState` pair each, with `null` doubling as "loading" — so a
 * rejected request left the page on "Loading…" forever, and the four polling loops died
 * on their first failure with no retry. Query gives loading, error, retry, caching and
 * dedupe once instead of nine times.
 *
 * Polling lives here, never in a component. Where a resource stops changing — a broadcast
 * that has completed, a set that has been revealed — its hook stops asking, rather than
 * every caller remembering to.
 */

const keys = {
  meta: ['meta'] as const,
  bots: ['bots'] as const,
  bot: (key: string) => ['bot', key] as const,
  botMatches: (botId: string) => ['bot', botId, 'matches'] as const,
  botStats: (botId: string) => ['bot', botId, 'stats'] as const,
  leaderboard: (rules: string | null) => ['leaderboard', rules ?? 'current'] as const,
  matches: (filters: MatchFilters) =>
    ['matches', filters.bot, filters.map, filters.ranked] as const,
  match: (id: string) => ['match', id] as const,
  matchLive: (id: string) => ['match', id, 'live'] as const,
  matchSet: (id: string) => ['set', id] as const,
  cosmetics: (revision: number) => ['cosmetics', revision] as const,
  store: ['store'] as const,
  authProviders: ['auth-providers'] as const,
  me: ['me'] as const,
  myBots: ['my-bots'] as const,
  notifications: ['notifications'] as const,
};

/**
 * Which external sign-ins to offer.
 *
 * Server-driven rather than a build flag: whether Google works depends on credentials
 * being present in *that* deployment, and a button rendered from a compile-time constant
 * would be wrong on any environment configured differently from the one that built it.
 */
export function useAuthProviders() {
  return useQuery({
    queryKey: keys.authProviders,
    queryFn: endpoints.authProviders,
    staleTime: 5 * 60_000,
  });
}

/**
 * What is for sale, and what this account already owns.
 *
 * Keyed without the account, and invalidated by `useLogout` clearing the cache — ownership
 * comes back with the response rather than being derived client-side, so a signed-out
 * visitor simply sees everything as unowned.
 */
export function useStore() {
  return useQuery({ queryKey: keys.store, queryFn: endpoints.store, staleTime: 60_000 });
}

/** The signed-in account. `data === null` means anonymous, which is not an error. */
export function useMe() {
  return useQuery({ queryKey: keys.me, queryFn: endpoints.me, staleTime: 60_000 });
}

/**
 * Signing out, which must also forget everything the session could see.
 *
 * Clearing the cache is the point rather than a tidy-up: without it my bots, my unread
 * notifications and every private response stay in memory, and the next render of a page
 * that reads them shows the previous account's data before any request goes out.
 */
export function useLogout() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: endpoints.logout,
    onSuccess: () => {
      client.setQueryData(keys.me, null);
      client.clear();
    },
  });
}

/**
 * Unread notifications, reloaded on a slow poll.
 *
 * The poll is the *recovery* path, not the delivery path — SignalR delivers, and this
 * catches whatever arrived while the socket was down or the tab was asleep. The two are
 * deliberately independent: a failed poll must not disturb the hub, and a dropped hub
 * must not stop the poll.
 */
export function useNotifications(enabled: boolean) {
  return useQuery({
    queryKey: keys.notifications,
    queryFn: endpoints.notifications,
    enabled,
    refetchInterval: 60_000,
  });
}

/** Versions and rules; changes about as often as a deploy. */
export function useMeta() {
  return useQuery({ queryKey: keys.meta, queryFn: endpoints.meta, staleTime: 5 * 60_000 });
}

export function useBots() {
  return useQuery({ queryKey: keys.bots, queryFn: endpoints.bots });
}

/**
 * One bot, polled only while a version of it is still compiling.
 *
 * Build state is the one thing on a bot's page that changes without the reader doing
 * anything, and it is what they are watching for after a submit. Once nothing is queued
 * or building, the page is static and the polling stops.
 */
export function useBot(key: string | undefined) {
  return useQuery({
    queryKey: keys.bot(key ?? ''),
    queryFn: () => endpoints.bot(key!),
    enabled: Boolean(key),
    refetchInterval: (query) =>
      query.state.data?.versions.some(
        (version) => version.status === 'Pending' || version.status === 'Building',
      )
        ? 2_500
        : false,
  });
}

export function useBotMatches(botId: string | undefined) {
  return useQuery({
    queryKey: keys.botMatches(botId ?? ''),
    queryFn: () => endpoints.botMatches(botId!),
    enabled: Boolean(botId),
    refetchInterval: 15_000,
  });
}

export function useBotStats(botId: string | undefined) {
  return useQuery({
    queryKey: keys.botStats(botId ?? ''),
    queryFn: () => endpoints.botStats(botId!),
    enabled: Boolean(botId),
  });
}

export function useLeaderboard(rules: string | null) {
  return useQuery({
    queryKey: keys.leaderboard(rules),
    queryFn: () => endpoints.leaderboard(rules),
  });
}

export interface MatchFilters {
  bot: string;
  map: string;
  ranked: string;
}

/** How many matches a page of the feed asks for. Also the "there is more" test. */
export const MATCH_PAGE = 30;

/**
 * The public match feed: paged, filterable, and followed while anything is still moving.
 *
 * Pages are `skip`/`take` over a feed that grows at the *front*, so a new match arriving
 * mid-scroll shifts everything down a slot and the next page can repeat one. Left alone
 * because the alternative is a cursor the endpoint does not offer, and a duplicate row in
 * a browsing feed costs less than a keyset parameter the server would have to learn.
 *
 * The poll deliberately refetches *every* loaded page rather than only the first: a match
 * on page three finishing is exactly the update someone scrolled down to watch.
 */
export function useMatches(filters: MatchFilters) {
  return useInfiniteQuery({
    queryKey: keys.matches(filters),
    queryFn: ({ pageParam }) => endpoints.matches(matchQuery(filters, pageParam)),
    initialPageParam: 0,
    // A short page means the end of the feed; anything else may have more behind it.
    getNextPageParam: (last: MatchSummary[], all) =>
      last.length < MATCH_PAGE ? undefined : all.flat().length,
    // Only follow a feed that has something moving in it — and a broadcast is moving.
    // Status alone stops too early here for exactly the reason it does on `useMatch`: a
    // match reaches Completed while its broadcast is still playing out and its result is
    // still withheld, so a feed that stopped there would leave Watch's live cards frozen
    // and never reveal the winner they are holding back.
    refetchInterval: (query) =>
      query.state.data?.pages
        .flat()
        .some((m) => m.broadcasting || m.status === 'Pending' || m.status === 'Running')
        ? 2_500
        : false,
  });
}

function matchQuery(filters: MatchFilters, skip: number) {
  const q = new URLSearchParams({ take: String(MATCH_PAGE) });
  if (skip > 0) q.set('skip', String(skip));
  if (filters.bot !== '') q.set('bot', filters.bot);
  if (filters.map !== '') q.set('map', filters.map);
  if (filters.ranked !== '') q.set('ranked', filters.ranked);
  return `/api/matches?${q}`;
}

/**
 * A match, polled until it is finished *and* done broadcasting.
 *
 * Status alone is the wrong stop condition: a match reaches Completed while its broadcast
 * is still playing out and its result is still withheld, so stopping there would leave the
 * page on a permanent "live" with a result that never arrives.
 */
export function useMatch(matchId: string | undefined) {
  return useQuery({
    queryKey: keys.match(matchId ?? ''),
    queryFn: () => endpoints.match(matchId!),
    enabled: Boolean(matchId),
    refetchInterval: (query) => (isResolved(query.state.data) ? false : 5_000),
  });
}

/** The shared presentation clock, polled fast and only while a broadcast is running. */
export function useMatchLive(matchId: string | undefined) {
  return useQuery({
    queryKey: keys.matchLive(matchId ?? ''),
    queryFn: () => endpoints.matchLive(matchId!),
    enabled: Boolean(matchId),
    refetchInterval: (query) => (query.state.data?.broadcastComplete ? false : 1_500),
  });
}

/** A ranked set, polled until revealed — its score is withheld until every game is public. */
export function useMatchSet(setId: string | undefined) {
  return useQuery({
    queryKey: keys.matchSet(setId ?? ''),
    queryFn: () => endpoints.matchSet(setId!),
    enabled: Boolean(setId),
    refetchInterval: (query) => (isSetResolved(query.state.data) ? false : 2_000),
  });
}

/**
 * A match's replay, followed while it broadcasts.
 *
 * Depends on the clock rather than standing alone: there is no replay before the match
 * completes, and while it is broadcasting the document is truncated to the ticks released
 * so far — so it is re-read on the same cadence until the broadcast ends, then left alone.
 */
export function useMatchReplay(matchId: string | undefined, live: MatchLive | undefined) {
  const ready = live?.status === 'Completed';
  const complete = live?.broadcastComplete ?? false;
  return useQuery({
    // Completion is a different representation: partial broadcasts withhold
    // the result. A distinct key guarantees one final full-document request
    // when the independent live clock flips, instead of merely stopping the
    // partial query's interval and leaving its last response cached forever.
    queryKey: ['match', matchId ?? '', 'replay', complete],
    queryFn: () => endpoints.matchReplay(matchId!),
    enabled: Boolean(matchId) && ready,
    // Keep Viewer (and its shared audio session) mounted while the completion
    // key's full document replaces the final partial response.
    placeholderData: (previous) => previous,
    refetchInterval: () => (live && !live.broadcastComplete ? 1_500 : false),
    // Truncated mid-broadcast, so the cached copy goes stale the moment it lands.
    staleTime: 0,
  });
}

/**
 * The bots this account owns.
 *
 * Polls only while it owns none. The first-run screen's last step happens in a terminal
 * rather than in the page, and it promises the page turns over on its own when the
 * submission lands — so the empty answer is the one answer worth re-asking. The condition
 * stops the interval on the first bot, which means no account that has ever shipped one
 * pays for it.
 */
export function useMyBots(enabled: boolean) {
  return useQuery({
    queryKey: keys.myBots,
    queryFn: endpoints.myBots,
    enabled,
    refetchInterval: (query) =>
      query.state.data?.length === 0 ? 10_000 : false,
  });
}

/**
 * What this account has unlocked.
 *
 * `revision` is not a cache-buster but part of the identity: entitlements are granted by
 * playing, so the catalog after a build lands is genuinely a different answer from the
 * one before it. Callers pass their own notion of "something happened that could have
 * unlocked a thing" and get a fresh fetch exactly then.
 */
export function useCosmetics(revision = 0) {
  return useQuery({
    queryKey: keys.cosmetics(revision),
    queryFn: endpoints.cosmetics,
    staleTime: 60_000,
  });
}

/**
 * Writes, in the same file as the reads that they invalidate.
 *
 * Each of these was a hand-rolled `busy`/`error` pair around a bare `api.post`, repeated
 * five times with five slightly different error-message fallbacks. `useMutation` carries
 * pending and error state, and — the part that was missing everywhere — knows which
 * cached reads the write just invalidated.
 *
 * They live beside the queries deliberately: a mutation that does not say what it stales
 * is how a page ends up showing the value the user just changed away from.
 */
export function useCreateBot() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: endpoints.createBot,
    // A new bot belongs to both rosters, and the garage is usually the page that ordered it.
    onSuccess: () => {
      void client.invalidateQueries({ queryKey: keys.myBots });
      void client.invalidateQueries({ queryKey: keys.bots });
    },
  });
}

/**
 * Submitting a version starts a build, and `useBot` polls while one is running — so the
 * refetch here is what *starts* that polling rather than merely refreshing a list.
 */
export function useSubmitVersion(botKey: string, botId: string) {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (body: SubmitVersionRequest) => endpoints.submitVersion(botId, body),
    onSuccess: () => client.invalidateQueries({ queryKey: keys.bot(botKey) }),
  });
}

/** Appearance is drawn from `bot.accent`/`lookId` everywhere, so the bot is what stales. */
export function useUpdateAppearance(botKey: string, botId: string) {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (body: UpdateBotAppearanceRequest) =>
      endpoints.updateAppearance(botId, body),
    onSuccess: () => client.invalidateQueries({ queryKey: keys.bot(botKey) }),
  });
}

export function useChallenge() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: endpoints.challenge,
    onSuccess: () => client.invalidateQueries({ queryKey: ['matches'] }),
  });
}

/** No opponent: the server matchmakes by rating (DECISIONS #95). */
export function useRankedChallenge() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: endpoints.rankedChallenge,
    onSuccess: () => client.invalidateQueries({ queryKey: ['matches'] }),
  });
}

export function useRegister() {
  return useMutation({ mutationFn: endpoints.register });
}

export function useLogin() {
  return useMutation({ mutationFn: endpoints.login });
}

/**
 * Acknowledging a notification. Failure is deliberately silent at the call site: the
 * unread poll will offer it again, and a toast that refuses to dismiss because its
 * acknowledgement 500'd is worse than one acknowledged twice.
 */
export function useReadNotification() {
  return useMutation({ mutationFn: endpoints.readNotification });
}

function isResolved(match: MatchDetail | undefined) {
  if (!match) return false;
  return (match.status === 'Completed' || match.status === 'Failed') && !match.broadcasting;
}

function isSetResolved(set: MatchSetDetail | undefined) {
  if (!set) return false;
  return set.revealed || set.status === 'Failed';
}
