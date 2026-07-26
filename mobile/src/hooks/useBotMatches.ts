import { useQuery } from '@tanstack/react-query';

import { api } from '@/api/client';

/**
 * A bot's record and recent games. Refetches on a slow cadence because a match in
 * progress flips from broadcasting to a result without the client doing anything.
 */
export function useBotMatches(botId: string | undefined) {
  return useQuery({
    queryKey: ['bot', botId, 'matches'],
    queryFn: () => api.botMatches(botId!),
    enabled: Boolean(botId),
    refetchInterval: 15_000,
  });
}
