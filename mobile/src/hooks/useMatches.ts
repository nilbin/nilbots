import { useInfiniteQuery } from '@tanstack/react-query';

import { api } from '@/api/client';

const PAGE = 25;

/**
 * The arena feed: newest matches first, a page at a time.
 *
 * Paged rather than filtered in memory, unlike the roster and the ladder (DECISIONS
 * #117). Those are bounded by how many bots exist; this grows without limit — every game
 * ever played — so it is the one list that must not fetch everything.
 *
 * Polls on a slow cadence because the top of the feed is where a new match appears, and
 * a broadcasting one flips to a result while you are watching the list. Note the cost:
 * an interval refetch reloads *every* page loaded so far, so a reader far down the feed
 * pays for rows they cannot see. Acceptable while the feed is short; if deep scrolling
 * becomes normal, drop the interval and let pull-to-refresh own freshness rather than
 * reaching for `maxPages`, which caps retained pages and would break paging itself.
 */
export function useMatches(ranked?: boolean) {
  return useInfiniteQuery({
    queryKey: ['matches', ranked ?? 'all'],
    queryFn: ({ pageParam }) => api.matches({ take: PAGE, skip: pageParam, ranked }),
    initialPageParam: 0,
    // A short page means the end; the endpoint reports no total to compare against.
    getNextPageParam: (last, pages) =>
      last.length < PAGE ? undefined : pages.reduce((count, page) => count + page.length, 0),
    refetchInterval: 15_000,
  });
}
