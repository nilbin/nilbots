import { useEffect } from 'react';

import type { BroadcastAnchor } from '@/components/arena/useArenaBridge';
import { useMatchLive } from '@/hooks/useMatch';

/**
 * Follow the server's presentation clock while a broadcast is on screen.
 *
 * A broadcasting match is *followed*, not played. Its replay exists but is truncated to
 * the ticks released so far, so playing it as an ordinary replay would run to that edge,
 * stop, and leave this viewer drifting behind everyone else watching the same fight.
 * Anchoring to the server's clock is what keeps every viewer on the same tick.
 *
 * Each poll re-anchors the page *and* re-reads the replay, because the document grows as
 * ticks are released — the same shape the site's match page uses.
 */
export function useBroadcastFollower({
  matchId,
  active,
  load,
  onBroadcastEnded,
}: {
  /** The broadcast to follow, or undefined when nothing is being followed. */
  matchId: string | undefined;
  /** Whether the page can be driven yet — a load before it is ready goes nowhere. */
  active: boolean;
  load: (matchId: string, anchor?: BroadcastAnchor) => void;
  /** The broadcast finished: the caller should stop following and let the transport take over. */
  onBroadcastEnded: (matchId: string) => void;
}) {
  const { data: clock } = useMatchLive(matchId);

  useEffect(() => {
    if (!matchId || !active || !clock) return;
    // Before a broadcast starts the server reports presentationTick as int.MaxValue —
    // "fully visible", which is right for a legacy match and catastrophic as an anchor:
    // it would pin the follower past the last tick. A match that has not completed has no
    // clock to follow yet, so wait for one.
    if (clock.status !== 'Completed') return;
    if (clock.broadcastComplete) {
      onBroadcastEnded(matchId);
      return;
    }
    load(matchId, {
      tick: clock.presentationTick,
      ticksPerSecond: clock.presentationTicksPerSecond,
    });
  }, [matchId, active, clock, load, onBroadcastEnded]);
}
