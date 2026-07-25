import { useQuery } from '@tanstack/react-query';

import { api } from '@/api/client';

export function useBots() {
  return useQuery({ queryKey: ['bots'], queryFn: api.bots });
}

export function useBot(key: string | undefined) {
  return useQuery({
    queryKey: ['bot', key],
    queryFn: () => api.bot(key!),
    enabled: Boolean(key),
  });
}
