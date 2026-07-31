import * as THREE from 'three';

export interface WallCapCorners {
  northWest: boolean;
  southWest: boolean;
  southEast: boolean;
  northEast: boolean;
}

export interface WallCapGeometryOptions {
  left: number;
  right: number;
  north: number;
  south: number;
  cornerRadius: number;
  corners: WallCapCorners;
  /** Quarter-circle segments. Three matches the wall body's gameplay-scale curve. */
  cornerSegments?: number;
}

/**
 * Build one convex topology cap with rounded corners only where two open sides meet.
 *
 * UVs remain a linear 0..1 crop of the original rectangle: north is V=1 and south is
 * V=0, matching a Three PlaneGeometry after it is laid onto XZ. The caller can therefore
 * keep the existing topology-atlas crop and cell mapping unchanged.
 */
export function wallCapGeometry({
  left,
  right,
  north,
  south,
  cornerRadius,
  corners,
  cornerSegments = 3,
}: WallCapGeometryOptions): THREE.BufferGeometry {
  const width = right - left;
  const depth = south - north;
  if (!(width > 0 && depth > 0))
    throw new Error(`Wall cap must have positive dimensions (${width} × ${depth}).`);
  const radius = Math.max(0, Math.min(cornerRadius, width / 2, depth / 2));
  const segments = Math.max(1, Math.floor(cornerSegments));
  const outline: Array<[number, number]> = [];

  const point = (x: number, z: number) => {
    const previous = outline.at(-1);
    if (previous && Math.abs(previous[0] - x) < 1e-9 && Math.abs(previous[1] - z) < 1e-9)
      return;
    outline.push([x, z]);
  };
  const arc = (
    centerX: number,
    centerZ: number,
    start: number,
    end: number,
  ) => {
    for (let index = 0; index <= segments; index += 1) {
      const angle = start + ((end - start) * index) / segments;
      point(centerX + Math.cos(angle) * radius, centerZ + Math.sin(angle) * radius);
    }
  };

  // Walk the outline in the winding that faces +Y: west → south → east → north.
  if (radius > 0 && corners.northWest) point(left, north + radius);
  else point(left, north);

  if (radius > 0 && corners.southWest)
    arc(left + radius, south - radius, Math.PI, Math.PI / 2);
  else point(left, south);

  if (radius > 0 && corners.southEast)
    arc(right - radius, south - radius, Math.PI / 2, 0);
  else point(right, south);

  if (radius > 0 && corners.northEast)
    arc(right - radius, north + radius, 0, -Math.PI / 2);
  else point(right, north);

  if (radius > 0 && corners.northWest)
    arc(left + radius, north + radius, -Math.PI / 2, -Math.PI);

  const first = outline[0];
  const last = outline.at(-1);
  if (
    first &&
    last &&
    Math.abs(first[0] - last[0]) < 1e-9 &&
    Math.abs(first[1] - last[1]) < 1e-9
  ) {
    outline.pop();
  }

  const positions = [
    (left + right) / 2,
    0,
    (north + south) / 2,
    ...outline.flatMap(([x, z]) => [x, 0, z]),
  ];
  const normals = new Array(positions.length).fill(0);
  for (let index = 1; index < normals.length; index += 3) normals[index] = 1;
  const uvs = positions.flatMap((value, index) => {
    if (index % 3 !== 0) return [];
    const x = value;
    const z = positions[index + 2];
    return [(x - left) / width, (south - z) / depth];
  });
  const indices: number[] = [];
  for (let index = 0; index < outline.length; index += 1) {
    indices.push(0, index + 1, ((index + 1) % outline.length) + 1);
  }

  const geometry = new THREE.BufferGeometry();
  geometry.setAttribute('position', new THREE.Float32BufferAttribute(positions, 3));
  geometry.setAttribute('normal', new THREE.Float32BufferAttribute(normals, 3));
  geometry.setAttribute('uv', new THREE.Float32BufferAttribute(uvs, 2));
  geometry.setIndex(indices);
  geometry.computeBoundingBox();
  geometry.computeBoundingSphere();
  return geometry;
}
