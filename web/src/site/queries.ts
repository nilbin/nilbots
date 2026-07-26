import { useQuery } from '@tanstack/react-query';
import { endpoints, type MatchDetail, type MatchLive, type MatchSetDetail } from './api';

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
  match: (id: string) => ['match', id] as const,
  matchLive: (id: string) => ['match', id, 'live'] as const,
  matchSet: (id: string) => ['set', id] as const,
  myBots: ['my-bots'] as const,
};

/** Versions and rules; changes about as often as a deploy. */
export function useMeta() {
  return useQuery({ queryKey: keys.meta, queryFn: endpoints.meta, staleTime: 5 * 60_000 });
}

export function useBots() {
  return useQuery({ queryKey: keys.bots, queryFn: endpoints.bots });
}

export function useBot(key: string | undefined) {
  return useQuery({
    queryKey: keys.bot(key ?? ''),
    queryFn: () => endpoints.bot(key!),
    enabled: Boolean(key),
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
  return useQuery({
    queryKey: ['match', matchId ?? '', 'replay'],
    queryFn: () => endpoints.matchReplay(matchId!),
    enabled: Boolean(matchId) && ready,
    refetchInterval: () => (live && !live.broadcastComplete ? 1_500 : false),
    // Truncated mid-broadcast, so the cached copy goes stale the moment it lands.
    staleTime: 0,
  });
}

export function useMyBots(enabled: boolean) {
  return useQuery({ queryKey: keys.myBots, queryFn: endpoints.myBots, enabled });
}

function isResolved(match: MatchDetail | undefined) {
  if (!match) return false;
  return (match.status === 'Completed' || match.status === 'Failed') && !match.broadcasting;
}

function isSetResolved(set: MatchSetDetail | undefined) {
  if (!set) return false;
  return set.revealed || set.status === 'Failed';
}
