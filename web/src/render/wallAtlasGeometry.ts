export interface WallAtlasDestination {
  destinationTile: number;
  destinationGutter: number;
}

/**
 * Atlas entries preserve the manifest's core:gutter ratio at any bake
 * resolution. Source pixels choose the crop; logical ratios choose placement.
 */
export function wallAtlasDestination(
  tile: number,
  contentPixels: number,
  gutterPixels: number,
): WallAtlasDestination {
  const destinationGutter = tile * (gutterPixels / contentPixels);
  return {
    destinationTile: tile + destinationGutter * 2,
    destinationGutter,
  };
}
