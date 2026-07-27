import type { ReplayModel } from '../replayModel';

/**
 * Which neighbour each bit of a wall's topology mask refers to, clockwise from north.
 *
 * The atlas is indexed by this mask, so the order is baked into the artwork and cannot be
 * changed without rebaking every wall sprite in every theme.
 */
export const wallNeighbourStep: readonly (readonly [number, number])[] = [
  [0, -1],
  [1, -1],
  [1, 0],
  [1, 1],
  [0, 1],
  [-1, 1],
  [-1, 0],
  [-1, -1],
];

/**
 * Where the walls are and which family each belongs to.
 *
 * Extracted so the two renderers cannot disagree about the map. They draw it very
 * differently — one paints a mask-selected sprite, the other extrudes a box — but "is this
 * tile a wall, whose family is it, and which of the 256 topology sprites does it use" has
 * exactly one right answer, and duplicating that logic is how a wall ends up solid in one
 * viewer and walkable in the other.
 */
export class WallLayout {
  private readonly tiles: readonly string[];
  private readonly overrides: ReadonlyMap<string, string>;

  constructor(
    private readonly replay: ReplayModel,
    private readonly boundaryFamily: string,
    private readonly interiorFamily: string,
  ) {
    this.tiles = replay.map.tileRows;
    const overrides = new Map<string, string>();
    for (const group of replay.map.presentation?.wallGroups ?? []) {
      for (const position of group.tiles)
        overrides.set(`${position.x},${position.y}`, group.family ?? interiorFamily);
    }
    this.overrides = overrides;
  }

  get width(): number {
    return this.replay.map.width;
  }

  get height(): number {
    return this.replay.map.height;
  }

  /**
   * The family occupying a tile, or null for open floor.
   *
   * Out of bounds counts as boundary wall: the arena is enclosed, and treating the outside
   * as empty would make every edge tile compute its topology as an exposed rim.
   */
  familyAt(x: number, y: number): string | null {
    if (x < 0 || y < 0 || x >= this.width || y >= this.height) return this.boundaryFamily;
    if (this.tiles[y][x] !== '#') return null;
    const override = this.overrides.get(`${x},${y}`);
    if (override) return override;
    return x === 0 || y === 0 || x === this.width - 1 || y === this.height - 1
      ? this.boundaryFamily
      : this.interiorFamily;
  }

  /** The atlas index for a tile: which neighbours share its family. */
  maskAt(x: number, y: number, family: string): number {
    let mask = 0;
    for (let bit = 0; bit < 8; bit++) {
      const [dx, dy] = wallNeighbourStep[bit];
      if (this.familyAt(x + dx, y + dy) === family) mask |= 1 << bit;
    }
    return mask;
  }

  /** Every wall tile, once, with the family and mask it draws with. */
  *walls(): Generator<{ x: number; y: number; family: string; mask: number }> {
    for (let y = 0; y < this.height; y++) {
      for (let x = 0; x < this.width; x++) {
        const family = this.familyAt(x, y);
        if (family === null) continue;
        yield { x, y, family, mask: this.maskAt(x, y, family) };
      }
    }
  }
}
