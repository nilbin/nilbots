/**
 * Scrap's colour, and it is deliberately nobody's.
 *
 * Loose scrap belongs to whoever steps on it, so painting a pile — or a load
 * riding on a body — in a team accent would say something false about a tile
 * anyone can walk onto. The arena already has a neutral warm register for
 * authored ground furniture (spawn-pad seals, the capture field's boundary);
 * this joins it one step brighter, because a pile is a thing standing on the
 * floor rather than a marking of it.
 *
 * Shared by both renderers and the panel because a device that loses its WebGL
 * context swaps between them mid-replay, and the economy must not change
 * colour when it does.
 */
export const SCRAP_ACCENT = '#e2a844';
