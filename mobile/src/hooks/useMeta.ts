import { useQuery } from '@tanstack/react-query';

import { api } from '@/api/client';

/** Server versions and the map pool. Changes only on deploy, so cache it hard. */
export function useMeta() {
  return useQuery({ queryKey: ['meta'], queryFn: api.meta, staleTime: 5 * 60_000 });
}
