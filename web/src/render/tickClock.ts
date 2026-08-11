/**
 * Which tick's state a renderer should show while it is part-way through drawing one.
 *
 * A presentation is END-of-tick state: the engine resolves a tick atomically, so there is
 * no sub-tick truth to be had. Bodies, though, are drawn gliding from where they started
 * to where they ended. Reading `at(tick)` while that glide is in flight therefore shows
 * every change a whole tick early, for the tick's entire visual duration. A Kestrel's
 * vector-dash crosses up to four tiles in one tick, and the orb it is dashing for
 * appeared in its hands at the start of the dash rather than the end of it (owner review
 * 2026-08-10).
 *
 * The renderer invents the glide, so the renderer owes it a consistent clock: state stays
 * on the previous tick until the glide has effectively landed. That makes both halves of
 * a body's picture agree, at the cost of a paused mid-tick frame reading one tick behind
 * the panels -- which is the correct trade, because the panels are text and the arena is
 * a moving picture of where things are.
 *
 * The final tick is exempt: nothing follows it to reveal its state, so it settles at once.
 */
export const STATE_SETTLES_AT = 0.85;

export function settledStateTick(
  tick: number,
  fraction: number,
  tickCount: number,
): number {
  if (tick >= tickCount - 1 || fraction >= STATE_SETTLES_AT) return tick;
  return Math.max(0, tick - 1);
}
