/** Owner-ruled mild oblique projection for the Canvas2D battlefield. */
export const WORLD_VERTICAL_SCALE = 0.9;

/** Canvas pixels become logical world pixels before the global vertical squash. */
export function logicalArenaHeight(physicalHeight: number): number {
  return physicalHeight / WORLD_VERTICAL_SCALE;
}

/** Inverse of the projection, used only for truthful pointer hit testing. */
export function unprojectCanvasY(physicalY: number): number {
  return physicalY / WORLD_VERTICAL_SCALE;
}
