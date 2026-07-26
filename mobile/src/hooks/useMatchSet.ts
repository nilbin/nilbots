import { useQuery } from '@tanstack/react-query';

import { api, type MatchSet } from '@/api/client';

/**
 * One ranked set — six games across three map and seed pairs.
 *
 * `revealed` is the stop condition, not status. A set holds back its scores, rating
 * changes and winner until every game has finished broadcasting, so an unrevealed set
 * still has news coming even once its last game reads Completed.
 */
export function useMatchSet(setId: string | undefined) {
  return useQuery({
    queryKey: ['set', setId],
    queryFn: () => api.matchSet(setId!),
    enabled: Boolean(setId),
    refetchInterval: (query) => (isResolved(query.state.data) ? false : 5_000),
  });
}

function isResolved(set: MatchSet | undefined) {
  return Boolean(set?.revealed) && !set!.games.some((game) => game.broadcasting);
}
