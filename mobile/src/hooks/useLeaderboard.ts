import { useQuery } from '@tanstack/react-query';

import { api } from '@/api/client';

/** One ladder per rules version; omit `rules` for the server's current ladder. */
export function useLeaderboard(rules?: string) {
  return useQuery({
    queryKey: ['leaderboard', rules ?? 'current'],
    queryFn: () => api.leaderboard(rules),
  });
}
