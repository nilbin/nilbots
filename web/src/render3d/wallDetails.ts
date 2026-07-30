import * as THREE from 'three';
import type { ArenaTheme, WallFamily } from '../render/arenaThemes';
import { WallLayout } from '../render/wallTopology';

type Disposable = { dispose: () => void };
type Placement = {
  position: THREE.Vector3;
  rotationY: number;
};

const cardinalSides = [
  { dx: 0, dy: -1, rotationY: 0 },
  { dx: 1, dy: 0, rotationY: Math.PI / 2 },
  { dx: 0, dy: 1, rotationY: Math.PI },
  { dx: -1, dy: 0, rotationY: -Math.PI / 2 },
] as const;

/**
 * Sparse theme-authored hardware layered over topology-derived wall solids.
 *
 * Every placement is a pure function of family, coordinate, mask, and side.
 * The pieces remain inside the already-approved wall envelope: side details
 * are coplanar decals with polygon offset, and top plates/clamps fit inside
 * the narrowest configured upper profile.
 */
export function buildWallDetails(
  layout: WallLayout,
  theme: ArenaTheme,
  openEdgeInset: number,
  disposables: Disposable[],
): THREE.Group {
  const group = new THREE.Group();
  group.userData.kind = 'arena-wall-details';

  for (const [familyId, family] of theme.walls.families) {
    if (family.geometry3d.details === null) continue;
    const familyGroup = buildFamilyDetails(
      layout,
      familyId,
      family,
      openEdgeInset,
      disposables,
    );
    if (familyGroup.children.length > 0) group.add(familyGroup);
  }
  group.userData.detailCount = group.children.reduce(
    (sum, child) => sum + (child.userData.instanceCount ?? 0),
    0,
  );
  return group;
}

function buildFamilyDetails(
  layout: WallLayout,
  familyId: string,
  family: WallFamily,
  openEdgeInset: number,
  disposables: Disposable[],
): THREE.Group {
  const details = family.geometry3d.details!;
  const sidePanels: Placement[] = [];
  const ventBacks: Placement[] = [];
  const vents: Placement[] = [];
  const topPanels: Placement[] = [];
  const clamps: Placement[] = [];
  const upper = family.geometry3d.upperProfile;
  const topHalf =
    0.5 -
    openEdgeInset -
    (upper?.inset ?? 0) -
    (upper?.chamfer ?? 0);
  const topPanelScale = Math.min(1, Math.max(0.72, topHalf / 0.23));

  for (const wall of layout.walls()) {
    if (wall.family !== familyId) continue;
    const seed = detailHash(familyId, wall.x, wall.y, wall.mask, 7);
    const axis = wallAxis(layout, wall.x, wall.y, familyId, seed);
    if (seed % details.panelEvery === 0) {
      topPanels.push({
        position: new THREE.Vector3(
          wall.x + 0.5,
          family.geometry3d.height + 0.009,
          wall.y + 0.5,
        ),
        rotationY: axis,
      });
    }
    if (seed % details.clampEvery === 0) {
      clamps.push({
        position: new THREE.Vector3(
          wall.x + 0.5,
          family.geometry3d.height + 0.035,
          wall.y + 0.5,
        ),
        rotationY: axis,
      });
    }

    for (let side = 0; side < cardinalSides.length; side++) {
      const edge = cardinalSides[side];
      if (
        layout.familyAt(wall.x + edge.dx, wall.y + edge.dy) !==
        null
      )
        continue;
      const sideSeed = detailHash(
        familyId,
        wall.x,
        wall.y,
        wall.mask,
        side,
      );
      const placement = sidePlacement(
        wall.x,
        wall.y,
        family.geometry3d.height,
        openEdgeInset,
        edge.dx,
        edge.dy,
        edge.rotationY,
      );
      if (sideSeed % details.panelEvery === 0)
        sidePanels.push(placement);
      if (sideSeed % details.ventEvery === 0) {
        ventBacks.push(placement);
        vents.push(placement);
      }
    }
  }

  const group = new THREE.Group();
  group.userData.kind = 'arena-wall-family-details';
  group.userData.family = familyId;
  addInstances(
    group,
    roundedPlateGeometry(0.34 * topPanelScale, 0.16 * topPanelScale, 0.04),
    new THREE.MeshStandardMaterial({
      color: details.panelColor,
      roughness: 0.96,
      metalness: 0.08,
      polygonOffset: true,
      polygonOffsetFactor: -1,
    }),
    topPanels,
    'service-panels',
    disposables,
    true,
  );
  addInstances(
    group,
    new THREE.CapsuleGeometry(0.034, 0.27, 3, 8)
      .rotateZ(Math.PI / 2),
    new THREE.MeshStandardMaterial({
      color: details.clampColor,
      roughness: 0.78,
      metalness: 0.42,
    }),
    clamps,
    'copper-clamps',
    disposables,
  );
  addInstances(
    group,
    new THREE.PlaneGeometry(0.38, 0.12),
    new THREE.MeshStandardMaterial({
      color: details.panelColor,
      roughness: 0.97,
      metalness: 0.06,
      side: THREE.DoubleSide,
      polygonOffset: true,
      polygonOffsetFactor: -2,
    }),
    sidePanels,
    'side-service-panels',
    disposables,
  );
  addInstances(
    group,
    new THREE.PlaneGeometry(0.32, 0.105),
    new THREE.MeshStandardMaterial({
      color: '#100c0a',
      roughness: 1,
      metalness: 0,
      side: THREE.DoubleSide,
      polygonOffset: true,
      polygonOffsetFactor: -3,
    }),
    ventBacks,
    'vent-recesses',
    disposables,
  );
  addInstances(
    group,
    ventGeometry(),
    new THREE.MeshStandardMaterial({
      color: details.ventColor,
      emissive: details.ventColor,
      emissiveIntensity: 1.55,
      roughness: 0.7,
      metalness: 0.08,
      side: THREE.DoubleSide,
      polygonOffset: true,
      polygonOffsetFactor: -4,
    }),
    vents,
    'vent-emission',
    disposables,
  );
  group.userData.instanceCount = group.children.reduce(
    (sum, child) => sum + (child.userData.instanceCount ?? 0),
    0,
  );
  return group;
}

function sidePlacement(
  x: number,
  y: number,
  height: number,
  inset: number,
  dx: number,
  dy: number,
  rotationY: number,
): Placement {
  return {
    position: new THREE.Vector3(
      x + 0.5 + dx * (0.5 - inset),
      height * 0.42,
      y + 0.5 + dy * (0.5 - inset),
    ),
    rotationY,
  };
}

function wallAxis(
  layout: WallLayout,
  x: number,
  y: number,
  family: string,
  seed: number,
): number {
  const horizontal =
    Number(layout.familyAt(x - 1, y) === family) +
    Number(layout.familyAt(x + 1, y) === family);
  const vertical =
    Number(layout.familyAt(x, y - 1) === family) +
    Number(layout.familyAt(x, y + 1) === family);
  if (horizontal > vertical) return 0;
  if (vertical > horizontal) return Math.PI / 2;
  return seed % 2 === 0 ? 0 : Math.PI / 2;
}

function addInstances(
  group: THREE.Group,
  geometry: THREE.BufferGeometry,
  material: THREE.Material,
  placements: readonly Placement[],
  kind: string,
  disposables: Disposable[],
  horizontal = false,
): void {
  if (placements.length === 0) {
    geometry.dispose();
    material.dispose();
    return;
  }
  const mesh = new THREE.InstancedMesh(
    geometry,
    material,
    placements.length,
  );
  const matrix = new THREE.Matrix4();
  const quaternion = new THREE.Quaternion();
  const pitch = new THREE.Quaternion().setFromAxisAngle(
    new THREE.Vector3(1, 0, 0),
    horizontal ? -Math.PI / 2 : 0,
  );
  for (let index = 0; index < placements.length; index++) {
    const placement = placements[index];
    quaternion
      .setFromAxisAngle(
        new THREE.Vector3(0, 1, 0),
        placement.rotationY,
      )
      .multiply(pitch);
    matrix.compose(
      placement.position,
      quaternion,
      new THREE.Vector3(1, 1, 1),
    );
    mesh.setMatrixAt(index, matrix);
  }
  mesh.instanceMatrix.needsUpdate = true;
  mesh.userData.kind = `arena-wall-${kind}`;
  mesh.userData.instanceCount = placements.length;
  mesh.castShadow = kind === 'copper-clamps';
  mesh.receiveShadow = true;
  group.add(mesh);
  disposables.push(geometry, material);
}

function roundedPlateGeometry(
  width: number,
  height: number,
  radius: number,
): THREE.ShapeGeometry {
  const shape = new THREE.Shape();
  const left = -width / 2;
  const right = width / 2;
  const bottom = -height / 2;
  const top = height / 2;
  shape.moveTo(left + radius, bottom);
  shape.lineTo(right - radius, bottom);
  shape.quadraticCurveTo(right, bottom, right, bottom + radius);
  shape.lineTo(right, top - radius);
  shape.quadraticCurveTo(right, top, right - radius, top);
  shape.lineTo(left + radius, top);
  shape.quadraticCurveTo(left, top, left, top - radius);
  shape.lineTo(left, bottom + radius);
  shape.quadraticCurveTo(left, bottom, left + radius, bottom);
  return new THREE.ShapeGeometry(shape, 4);
}

function ventGeometry(): THREE.BufferGeometry {
  const parts = [-0.035, 0, 0.035].map((offset) => {
    const slot = new THREE.PlaneGeometry(0.22, 0.016);
    slot.translate(0, offset, 0);
    return slot;
  });
  const geometry = merge(parts);
  for (const part of parts) part.dispose();
  return geometry;
}

function merge(parts: readonly THREE.BufferGeometry[]): THREE.BufferGeometry {
  const geometry = new THREE.BufferGeometry();
  for (const name of ['position', 'normal', 'uv'] as const) {
    const arrays = parts.map(
      (part) => part.attributes[name].array as Float32Array,
    );
    const combined = new Float32Array(
      arrays.reduce((sum, array) => sum + array.length, 0),
    );
    let offset = 0;
    for (const array of arrays) {
      combined.set(array, offset);
      offset += array.length;
    }
    geometry.setAttribute(
      name,
      new THREE.BufferAttribute(
        combined,
        parts[0].attributes[name].itemSize,
      ),
    );
  }
  const indices: number[] = [];
  let vertexOffset = 0;
  for (const part of parts) {
    const index = part.getIndex()!;
    for (let item = 0; item < index.count; item++)
      indices.push(index.getX(item) + vertexOffset);
    vertexOffset += part.attributes.position.count;
  }
  geometry.setIndex(indices);
  return geometry;
}

function detailHash(
  family: string,
  x: number,
  y: number,
  mask: number,
  side: number,
): number {
  let value = 2166136261;
  for (const character of family) {
    value ^= character.charCodeAt(0);
    value = Math.imul(value, 16777619);
  }
  for (const component of [x, y, mask, side]) {
    value ^= component;
    value = Math.imul(value, 16777619);
  }
  return value >>> 0;
}
