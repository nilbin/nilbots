/**
 * Which baked atlas size this device should download and decode.
 *
 * Decoded size follows dimensions, not file size — a 4096×4096 atlas is 64 MB of RAM
 * however well WebP compresses it on disk. So the master is too heavy for a phone, while
 * a phone-sized bake is visibly soft on a desktop. The choice belongs to the client at
 * load, which is why one artifact ships every variant and picks here.
 */

/** Atlas cells are 16 across, each 192 content pixels plus a 2×32 gutter. */
const COLUMNS = 16;
const CONTENT_RATIO = 192 / 256;

/** Emitted by `scripts/generate-atlas-variants.mjs`; 4096 is the master, used as-is. */
const WIDTHS = [1024, 2048, 4096] as const;

/**
 * Accept a little undersampling before paying for the next bake.
 *
 * Exactly meeting demand would send an ordinary retina laptop to the 4096 master — 64 MB
 * decoded per atlas to gain detail no one can resolve at this tile size. At 0.75 a 2×
 * desktop and a 3× laptop both land on 2048, which is 16 MB and indistinguishable in
 * practice. Lower this only with a side-by-side that shows the difference.
 */
const TOLERANCE = 0.75;

/** A typical arena, used to estimate on-screen tile size before a replay is loaded. */
const NOMINAL_MAP = { width: 24, height: 18 };

export function atlasContentPixels(atlasWidth: number): number {
  return (atlasWidth / COLUMNS) * CONTENT_RATIO;
}

/**
 * The smallest baked width whose content resolution covers this viewport, within
 * TOLERANCE. Falls back to the master when nothing is big enough, and when there is no
 * window at all — a server render or a test has no device to measure.
 */
export function preferredAtlasWidth(
  viewportWidth = typeof window === 'undefined' ? 0 : window.innerWidth,
  viewportHeight = typeof window === 'undefined' ? 0 : window.innerHeight,
  devicePixelRatio = typeof window === 'undefined' ? 1 : (window.devicePixelRatio ?? 1),
): number {
  if (viewportWidth <= 0 || viewportHeight <= 0) return 4096;

  // drawArena fits the map with a one-tile margin, so this mirrors its sizing.
  const tileCss = Math.min(
    viewportWidth / (NOMINAL_MAP.width + 1),
    viewportHeight / (NOMINAL_MAP.height + 1),
  );
  const needed = tileCss * devicePixelRatio * TOLERANCE;

  return WIDTHS.find((width) => atlasContentPixels(width) >= needed) ?? 4096;
}
