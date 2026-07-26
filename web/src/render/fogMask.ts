/**
 * The fog of war, as a soft mask rather than a grid of rectangles.
 *
 * It used to be `fillRect` per unseen tile, painted after the walls. But walls are not
 * tile-shaped on screen — cover and perimeter sprites overhang their logical tile to give
 * them thickness — so the fog grid sliced across wall art: a visible wall whose overhang
 * reached a fogged neighbour was half-darkened, and a fogged wall whose overhang reached a
 * visible neighbour kept a bright sliver. Inconsistent rather than uniformly wrong, which
 * is why it read as a bug rather than a style.
 *
 * Building the shroud on its own surface fixes both that and the hard edges:
 *
 *  1. fill the whole arena with shroud;
 *  2. punch out what the bot can see — and punch visible *walls* out at their **drawn**
 *     extent, not their tile, so a wall you can see is never cut in half by the tile grid;
 *  3. blur, so the boundary falls off instead of stepping;
 *  4. composite once over the scene.
 *
 * Applied to composited pixels, the shroud cannot disagree with wall geometry, because by
 * then the walls are just pixels underneath it.
 */

export interface FogGeometry {
  /** Screen position of a tile's top-left corner. */
  px: (x: number) => number;
  py: (y: number) => number;
  tile: number;
  /**
   * How far a wall sprite overhangs its tile on each side. Visible walls are cleared at
   * this larger extent so the shroud follows the art rather than the grid.
   */
  wallGutter: number;
}

export interface FogInputs {
  mapWidth: number;
  mapHeight: number;
  /** `${x},${y}` of every tile the selected bot can see. */
  visible: ReadonlySet<string>;
  isWall: (x: number, y: number) => boolean;
}

/** Opacity of the shroud over unseen ground. */
const SHROUD = 'rgba(4, 7, 12, 0.62)';

/** Falloff, as a fraction of tile size — enough to soften the step, not to blur the map. */
const BLUR_TILES = 0.38;

/**
 * Draw the fog for one bot onto `ctx`, which is expected to already hold the floor, zone
 * and walls.
 *
 * Falls back to per-tile rectangles when an offscreen surface or canvas filters are
 * unavailable — an older WebView should get hard-edged fog rather than none, since fog is
 * load-bearing information about what the bot knew.
 */
export function drawFogMask(
  ctx: CanvasRenderingContext2D,
  geometry: FogGeometry,
  inputs: FogInputs,
): void {
  const { px, py, tile, wallGutter } = geometry;
  const { mapWidth, mapHeight, visible, isWall } = inputs;

  const left = px(0);
  const top = py(0);
  const width = tile * mapWidth;
  const height = tile * mapHeight;
  if (width <= 0 || height <= 0) return;

  const mask = createMask(ctx, width, height);
  if (!mask) {
    ctx.fillStyle = SHROUD;
    for (let y = 0; y < mapHeight; y++)
      for (let x = 0; x < mapWidth; x++)
        if (!visible.has(`${x},${y}`)) ctx.fillRect(px(x), py(y), tile, tile);
    return;
  }

  mask.fillStyle = SHROUD;
  mask.fillRect(0, 0, width, height);

  // Clear what is seen. Mask coordinates are arena-relative, so shift by the origin.
  mask.globalCompositeOperation = 'destination-out';
  mask.fillStyle = '#000';
  for (let y = 0; y < mapHeight; y++) {
    for (let x = 0; x < mapWidth; x++) {
      if (!visible.has(`${x},${y}`)) continue;
      // A visible wall clears its drawn extent; visible ground clears its tile.
      const bleed = isWall(x, y) ? wallGutter : 0;
      mask.fillRect(
        px(x) - left - bleed,
        py(y) - top - bleed,
        tile + bleed * 2,
        tile + bleed * 2,
      );
    }
  }
  mask.globalCompositeOperation = 'source-over';

  ctx.save();
  // Blurring on the way out keeps the mask's own edges crisp while softening the boundary
  // where it meets the scene.
  ctx.filter = `blur(${Math.max(1, tile * BLUR_TILES).toFixed(2)}px)`;
  ctx.drawImage(mask.canvas, left, top);
  ctx.restore();
}

/**
 * An offscreen 2D surface the size of the arena, or null when the environment cannot
 * provide one — no OffscreenCanvas, no document, or no filter support.
 */
function createMask(
  ctx: CanvasRenderingContext2D,
  width: number,
  height: number,
): CanvasRenderingContext2D | OffscreenCanvasRenderingContext2D | null {
  if (typeof ctx.filter !== 'string') return null;

  if (typeof OffscreenCanvas !== 'undefined') {
    const surface = new OffscreenCanvas(Math.ceil(width), Math.ceil(height));
    return surface.getContext('2d');
  }
  if (typeof document === 'undefined') return null;

  const surface = document.createElement('canvas');
  surface.width = Math.ceil(width);
  surface.height = Math.ceil(height);
  return surface.getContext('2d');
}
