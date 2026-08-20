import type { TacticalSheetCatalog } from '../api';
import type { Point } from './tacticsDocument';

/**
 * Corner-strict eight-way BFS over the catalog map — the same movement rule
 * as the authoritative engine, so preview routes (converge fans, the run
 * home, heal links) bend around walls the way real bots will.
 */
export function makePathfinder(catalog: TacticalSheetCatalog) {
  const width = catalog.map.width;
  const height = catalog.map.height;
  const rows = catalog.map.tileRows;
  const floor = (x: number, y: number) =>
    x >= 0 && y >= 0 && x < width && y < height && rows[y][x] !== '#';

  /** The complete tile-by-tile route. Projection uses this form so a patrol
   * preview can distribute bots along tiles the movement rule actually
   * reaches, rather than along the author's straight waypoint segments. */
  function findPathTiles(from: Point, to: Point): Point[] | null {
    if (!floor(from[0], from[1]) || !floor(to[0], to[1])) return null;
    const key = (x: number, y: number) => y * width + x;
    const seen = new Map<number, number | null>([[key(from[0], from[1]), null]]);
    const queue: Point[] = [from];
    let head = 0;
    while (head < queue.length) {
      const [x, y] = queue[head++];
      if (x === to[0] && y === to[1]) {
        const path: Point[] = [];
        let k: number | null | undefined = key(x, y);
        while (k !== null && k !== undefined) {
          path.push([k % width, Math.floor(k / width)]);
          k = seen.get(k);
        }
        return path.reverse();
      }
      for (let dx = -1; dx <= 1; dx++) for (let dy = -1; dy <= 1; dy++) {
        if (!dx && !dy) continue;
        const nx = x + dx;
        const ny = y + dy;
        if (!floor(nx, ny) || seen.has(key(nx, ny))) continue;
        if (dx && dy && (!floor(x + dx, y) || !floor(x, y + dy))) continue;
        seen.set(key(nx, ny), key(x, y));
        queue.push([nx, ny]);
      }
    }
    return null;
  }

  function findPath(from: Point, to: Point): Point[] | null {
    const route = findPathTiles(from, to);
    return route ? thin(route) : null;
  }

  function thin(points: Point[]): Point[] {
    if (points.length < 3) return points;
    const out: Point[] = [points[0]];
    for (let index = 1; index < points.length - 1; index++) {
      const [ax, ay] = out[out.length - 1];
      const [bx, by] = points[index];
      const [cx, cy] = points[index + 1];
      if (Math.sign(bx - ax) !== Math.sign(cx - bx)
        || Math.sign(by - ay) !== Math.sign(cy - by)) out.push(points[index]);
    }
    out.push(points[points.length - 1]);
    return out;
  }

  function nearestFloor([x, y]: Point): Point {
    if (floor(x, y)) return [x, y];
    for (let radius = 1; radius < 8; radius++) {
      for (let dy = -radius; dy <= radius; dy++) {
        for (let dx = -radius; dx <= radius; dx++) {
          if (floor(x + dx, y + dy)) return [x + dx, y + dy];
        }
      }
    }
    return [x, y];
  }

  /** Stitch clicked points into a walkable polyline for display. */
  function connect(points: Point[]): Point[] {
    const out: Point[] = [];
    for (let index = 0; index < points.length - 1; index++) {
      const segment = findPath(points[index], points[index + 1])
        ?? [points[index], points[index + 1]];
      out.push(...(index === 0 ? segment : segment.slice(1)));
    }
    return out.length ? out : points;
  }

  /** Stitch waypoints without inventing an illegal straight segment. The
   * ordinary geometry renderer keeps `connect` for compatibility with loaded
   * invalid drafts; bot projections and continuity lines use this truthful
   * variant and render nothing when any segment is unresolved. */
  function connectTiles(points: Point[]): Point[] | null {
    if (points.length === 0) return [];
    if (points.length === 1) {
      return floor(points[0][0], points[0][1]) ? [points[0]] : null;
    }
    const out: Point[] = [];
    for (let index = 0; index < points.length - 1; index++) {
      const segment = findPathTiles(points[index], points[index + 1]);
      if (!segment) return null;
      out.push(...(index === 0 ? segment : segment.slice(1)));
    }
    return out;
  }

  function connectSafe(points: Point[]): Point[] | null {
    const route = connectTiles(points);
    return route ? thin(route) : null;
  }

  return { floor, findPath, findPathTiles, nearestFloor, connect, connectTiles, connectSafe };
}

export type Pathfinder = ReturnType<typeof makePathfinder>;
