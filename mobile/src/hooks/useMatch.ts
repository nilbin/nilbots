import { useQuery } from '@tanstack/react-query';

import { api, type MatchDetail } from '@/api/client';

/**
 * One match.
 *
 * Polls until the match has both finished *and* finished broadcasting. Status alone is
 * the wrong stop condition: a match reaches `Completed` while its broadcast is still
 * playing out, and the outcome — winner, end reason, replay hash — stays withheld until
 * the broadcast ends. Stopping on status would leave the screen on a permanent "live"
 * with a result that never arrives.
 */
export function useMatch(matchId: string | undefined) {
  return useQuery({
    queryKey: ['match', matchId],
    queryFn: () => api.match(matchId!),
    enabled: Boolean(matchId),
    refetchInterval: (query) => (isResolved(query.state.data) ? false : 5_000),
  });
}

/**
 * A broadcasting match's playback clock — which tick the server considers visible right
 * now, and how fast it is advancing. Polled fast because it drives playback position,
 * and only until the broadcast completes.
 */
export function useMatchLive(matchId: string | undefined, enabled = true) {
  return useQuery({
    queryKey: ['match', matchId, 'live'],
    queryFn: () => api.matchLive(matchId!),
    enabled: Boolean(matchId) && enabled,
    refetchInterval: (query) => (query.state.data?.broadcastComplete ? false : 1_000),
  });
}

/** Nothing more will change: the match is over and its result is public. */
function isResolved(match: MatchDetail | undefined) {
  if (!match) return false;
  return (match.status === 'Completed' || match.status === 'Failed') && !match.broadcasting;
}
