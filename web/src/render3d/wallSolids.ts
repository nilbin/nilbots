import * as THREE from 'three';

/**
 * Walls as continuous solids traced from the tile grid, rather than a box per tile.
 *
 * A box per tile is what the arena looked like, and looked like: every corner a perfect
 * 90°, every run of wall a row of identical cubes, the whole room reading as something
 * assembled out of dice. Worse, two adjacent wall tiles buried four faces inside each
 * other — geometry that can never be seen, lit and shadowed every frame anyway.
 *
 * So the tiles are traced into outlines first. A block of nine wall tiles becomes one
 * twelve-sided polygon, not nine cubes; its corners get a radius, and the extrusion gets a
 * chamfer along the top edge. That chamfer is most of the effect: it is a surface angled
 * between the top and the side, so it catches the key light where a hard edge caught
 * nothing, and a wall stops being a silhouette with a line around it.
 *
 * Tracing also means holes are free, and the arena is mostly holes — the boundary wall is a
 * ring, which is one outer loop and one inner one, and drawing it as a filled rectangle
 * would pave the entire floor.
 */

/**
 * Tile-space corner radius.
 *
 * Small on purpose. The topology caps that sit on the wall tops are square, per tile, and
 * baked — so the rounder the solid under them, the further its corners pull away from the
 * art lying on it. This is the most that still reads as square from the play camera.
 */
const CORNER_RADIUS = 0.1;

interface Loop {
  /** Shape-space points (SVG-style y-up), already simplified to corners. */
  points: THREE.Vector2[];
  area: number;
}

/**
 * Outlines for one family's tiles, as shapes ready to extrude.
 *
 * Coordinates come back in **shape space**: x unchanged, y negated. Extruding leaves the
 * result lying in XY with depth along Z, and the one rotation that stands it up maps Z to
 * world up and Y to −Z — so pre-negating here is what makes a wall land on the tile it was
 * traced from instead of mirrored across the map.
 */
export function wallShapes(tiles: Iterable<{ x: number; y: number }>): THREE.Shape[] {
  const filled = new Set<string>();
  for (const tile of tiles) filled.add(`${tile.x},${tile.y}`);
  if (filled.size === 0) return [];

  const loops = trace(filled).map(toLoop);
  if (loops.length === 0) return [];

  // A loop inside an odd number of other loops is a hole; inside an even number (including
  // none) it is solid. That handles a courtyard inside a ring, and a pillar inside the
  // courtyard, without either being a special case.
  const outers: Loop[] = [];
  const holes: Loop[] = [];
  for (const loop of loops) {
    const depth = loops.filter(
      (other) => other !== loop && contains(other.points, loop.points[0]),
    ).length;
    (depth % 2 === 0 ? outers : holes).push(loop);
  }

  const shapes = outers.map((outer) => ({ outer, shape: rounded(outer.points, new THREE.Shape()) }));
  for (const hole of holes) {
    // Into the *smallest* container, so a pillar inside a courtyard inside a ring is
    // subtracted from the courtyard rather than from the ring around it.
    const owner = shapes
      .filter(({ outer }) => contains(outer.points, hole.points[0]))
      .sort((a, b) => a.outer.area - b.outer.area)[0];
    if (owner) owner.shape.holes.push(rounded(hole.points, new THREE.Path()));
  }
  return shapes.map(({ shape }) => shape);
}

/**
 * Every tile edge with no neighbour behind it, stitched into closed loops.
 *
 * Edges are emitted in a consistent rotational order around each cell, which is what makes
 * the loops come out with solid regions wound one way and holes the other — the winding
 * `ExtrudeGeometry` needs to tell a hole from an island.
 */
function trace(filled: Set<string>): THREE.Vector2[][] {
  const pending = new Map<string, THREE.Vector2[][]>();
  const push = (from: [number, number], to: [number, number]) => {
    const key = `${from[0]},${from[1]}`;
    const edge = [new THREE.Vector2(from[0], from[1]), new THREE.Vector2(to[0], to[1])];
    (pending.get(key) ?? pending.set(key, []).get(key)!).push(edge);
  };

  for (const cell of filled) {
    const [x, y] = cell.split(',').map(Number);
    if (!filled.has(`${x},${y - 1}`)) push([x, y], [x + 1, y]);
    if (!filled.has(`${x + 1},${y}`)) push([x + 1, y], [x + 1, y + 1]);
    if (!filled.has(`${x},${y + 1}`)) push([x + 1, y + 1], [x, y + 1]);
    if (!filled.has(`${x - 1},${y}`)) push([x, y + 1], [x, y]);
  }

  const loops: THREE.Vector2[][] = [];
  const take = (key: string): THREE.Vector2[] | null => {
    const edges = pending.get(key);
    if (!edges || edges.length === 0) return null;
    const edge = edges.pop()!;
    if (edges.length === 0) pending.delete(key);
    return edge;
  };

  while (pending.size > 0) {
    const start = pending.keys().next().value as string;
    let edge = take(start);
    if (!edge) continue;
    const loop = [edge[0]];
    // Walk end-to-start until the path closes. Two tiles meeting only at a corner leave two
    // edges leaving the same point; either choice closes into a valid loop, so the first
    // available one is taken rather than resolving the ambiguity.
    while (edge) {
      loop.push(edge[1]);
      edge = take(`${edge[1].x},${edge[1].y}`);
    }
    if (loop.length > 3) loops.push(loop);
  }
  return loops;
}

/** Drop the run-of-the-mill points along a straight edge, keeping only the corners. */
function toLoop(points: THREE.Vector2[]): Loop {
  const flipped = points.map((point) => new THREE.Vector2(point.x, -point.y));
  // The traced loop repeats its first point at the end; corners are found on the cycle.
  if (flipped.length > 1 && flipped[0].equals(flipped[flipped.length - 1])) flipped.pop();

  const corners: THREE.Vector2[] = [];
  for (let index = 0; index < flipped.length; index++) {
    const previous = flipped[(index - 1 + flipped.length) % flipped.length];
    const current = flipped[index];
    const next = flipped[(index + 1) % flipped.length];
    const turn =
      (current.x - previous.x) * (next.y - current.y) -
      (current.y - previous.y) * (next.x - current.x);
    if (Math.abs(turn) > 1e-9) corners.push(current);
  }

  let area = 0;
  for (let index = 0; index < corners.length; index++) {
    const current = corners[index];
    const next = corners[(index + 1) % corners.length];
    area += current.x * next.y - next.x * current.y;
  }
  return { points: corners, area: Math.abs(area) / 2 };
}

/** Write a loop into a path, cutting each corner with an arc instead of a right angle. */
function rounded<T extends THREE.Path>(points: THREE.Vector2[], path: T): T {
  const count = points.length;
  // A radius bigger than half the shortest edge would overshoot into the next corner and
  // fold the outline inside out, so the whole loop uses what its tightest edge allows.
  let radius = CORNER_RADIUS;
  for (let index = 0; index < count; index++)
    radius = Math.min(radius, points[index].distanceTo(points[(index + 1) % count]) / 2);
  if (radius <= 1e-6) {
    path.setFromPoints(points);
    path.closePath();
    return path;
  }

  const approach = (corner: THREE.Vector2, towards: THREE.Vector2) =>
    corner.clone().addScaledVector(towards.clone().sub(corner).normalize(), radius);

  for (let index = 0; index < count; index++) {
    const previous = points[(index - 1 + count) % count];
    const corner = points[index];
    const next = points[(index + 1) % count];
    const entry = approach(corner, previous);
    const exit = approach(corner, next);
    if (index === 0) path.moveTo(entry.x, entry.y);
    else path.lineTo(entry.x, entry.y);
    // The corner itself is the control point, so the arc leans into it the way a cast or
    // milled edge does rather than cutting straight across.
    path.quadraticCurveTo(corner.x, corner.y, exit.x, exit.y);
  }
  path.closePath();
  return path;
}

/** Ray casting, used only to decide which loops sit inside which. */
function contains(polygon: THREE.Vector2[], point: THREE.Vector2): boolean {
  let inside = false;
  for (let index = 0, previous = polygon.length - 1; index < polygon.length; previous = index++) {
    const a = polygon[index];
    const b = polygon[previous];
    if (
      a.y > point.y !== b.y > point.y &&
      point.x < ((b.x - a.x) * (point.y - a.y)) / (b.y - a.y) + a.x
    )
      inside = !inside;
  }
  return inside;
}
