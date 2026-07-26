import { useQuery } from '@tanstack/react-query';

import { api } from '@/api/client';
import { useAuth } from '@/auth/AuthProvider';

/**
 * The signed-in player's own bots.
 *
 * Keyed by signed-in state so signing out drops the cache rather than leaving one
 * account's garage on screen for the next person, and signing in refetches rather than
 * showing an empty list from before.
 */
export function useMyBots() {
  const { status } = useAuth();
  return useQuery({
    queryKey: ['my-bots', status],
    queryFn: () => api.myBots(),
    enabled: status === 'signed-in',
    // A build finishes without the client doing anything, and the garage is where you
    // watch for it.
    refetchInterval: 20_000,
  });
}
