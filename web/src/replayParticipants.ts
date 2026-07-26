import type { ReplayParticipant } from './types';

/**
 * Participant arrays preserve replay order; slots are their stable identities.
 *
 * Replay v1 happens to emit slots 0 and 1 in slot order, but viewer code must not turn
 * that serialization detail into an identity contract. Keeping the lookup here gives
 * renderers and presentation surfaces one slot-aware path that also works for reordered
 * and sparse participant arrays.
 */
export function participantsBySlot(
  participants: readonly ReplayParticipant[],
): ReadonlyMap<number, ReplayParticipant> {
  return new Map(participants.map((participant) => [participant.slot, participant]));
}
