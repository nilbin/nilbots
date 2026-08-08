import * as THREE from 'three';
import type {
  ArenaTheme,
  TreeCover3d,
  WallFamily,
} from '../render/arenaThemes';
import { WallLayout } from '../render/wallTopology';

type Disposable = { dispose: () => void };

type InstancePlacement = {
  position: THREE.Vector3;
  quaternion: THREE.Quaternion;
  scale: THREE.Vector3;
  color?: THREE.Color;
};

const UP = new THREE.Vector3(0, 1, 0);
const TREE_CENTER_JITTER = 0.03;

/**
 * Actual low-poly trees rooted exclusively in authoritative blocked tiles.
 *
 * The low root substrate still carries the canonical topology material, but it no
 * longer has to impersonate the whole tree. Trunks, branches, and broken-up crowns are
 * instanced geometry. Their positions hash only stable family/tile/mask inputs and every
 * crown stays inside its occupied tile's reviewed clearance envelope.
 */
export function buildWoodlandScenery(
  layout: WallLayout,
  theme: ArenaTheme,
  disposables: Disposable[],
): THREE.Group {
  const group = new THREE.Group();
  group.userData.kind = 'arena-woodland-scenery';

  for (const [familyId, family] of theme.walls.families) {
    const config = family.geometry3d.treeCover;
    if (config === null) continue;
    const familyGroup = buildTreeFamily(
      layout,
      familyId,
      family,
      config,
      disposables,
    );
    if (familyGroup.children.length > 0) group.add(familyGroup);
  }

  group.userData.treeCount = group.children.reduce(
    (sum, child) => sum + (child.userData.treeCount ?? 0),
    0,
  );
  return group;
}

function buildTreeFamily(
  layout: WallLayout,
  familyId: string,
  family: WallFamily,
  config: TreeCover3d,
  disposables: Disposable[],
): THREE.Group {
  const trunks: InstancePlacement[] = [];
  const rootFlares: InstancePlacement[] = [];
  const branches: InstancePlacement[] = [];
  const canopyLobes: InstancePlacement[] = [];
  let treeCount = 0;

  for (const wall of layout.walls()) {
    if (wall.family !== familyId) continue;
    treeCount++;
    const seed = treeHash(familyId, wall.x, wall.y, wall.mask, 0);
    const centre = new THREE.Vector3(
      wall.x + 0.5 + signedByte(seed, 8) * TREE_CENTER_JITTER,
      family.geometry3d.height,
      wall.y + 0.5 + signedByte(seed, 16) * TREE_CENTER_JITTER,
    );
    const yaw = unitByte(seed, 24) * Math.PI * 2;
    const yawRotation = new THREE.Quaternion().setFromAxisAngle(UP, yaw);

    rootFlares.push({
      position: centre.clone().add(new THREE.Vector3(0, 0.0325, 0)),
      quaternion: yawRotation.clone(),
      scale: new THREE.Vector3(
        config.trunkRadius,
        0.065,
        config.trunkRadius,
      ),
    });
    trunks.push({
      position: centre
        .clone()
        .add(new THREE.Vector3(0, config.trunkHeight / 2, 0)),
      quaternion: yawRotation.clone(),
      scale: new THREE.Vector3(
        config.trunkRadius,
        config.trunkHeight,
        config.trunkRadius,
      ),
    });

    const crownY =
      centre.y + config.trunkHeight + config.canopyHeight * 0.42;
    const lobeRadius = config.canopyRadius * 0.62;
    const lobeOffset = config.canopyRadius * 0.3;
    const branchStartY = centre.y + config.trunkHeight * 0.68;
    for (let lobe = 0; lobe < config.canopyLobes; lobe++) {
      const lobeSeed = treeHash(
        familyId,
        wall.x,
        wall.y,
        wall.mask,
        lobe + 1,
      );
      const angle =
        yaw +
        (lobe / config.canopyLobes) * Math.PI * 2 +
        signedByte(lobeSeed, 8) * 0.18;
      const lobePosition = new THREE.Vector3(
        centre.x + Math.cos(angle) * lobeOffset,
        crownY + signedByte(lobeSeed, 16) * config.canopyHeight * 0.08,
        centre.z + Math.sin(angle) * lobeOffset,
      );
      canopyLobes.push({
        position: lobePosition,
        quaternion: new THREE.Quaternion().setFromAxisAngle(
          UP,
          unitByte(lobeSeed, 24) * Math.PI * 2,
        ),
        scale: new THREE.Vector3(
          lobeRadius,
          config.canopyHeight / 2,
          lobeRadius,
        ),
        color: new THREE.Color(
          lobeSeed % 3 === 0
            ? config.canopyAccentColor
            : config.canopyColor,
        ),
      });

      const branchStart = new THREE.Vector3(
        centre.x,
        branchStartY,
        centre.z,
      );
      const branchEnd = lobePosition.clone().add(
        new THREE.Vector3(0, -config.canopyHeight * 0.18, 0),
      );
      const direction = branchEnd.clone().sub(branchStart);
      branches.push({
        position: branchStart.clone().add(direction.clone().multiplyScalar(0.5)),
        quaternion: new THREE.Quaternion().setFromUnitVectors(
          UP,
          direction.clone().normalize(),
        ),
        scale: new THREE.Vector3(
          config.trunkRadius * 0.5,
          direction.length(),
          config.trunkRadius * 0.5,
        ),
      });
    }
  }

  const familyGroup = new THREE.Group();
  familyGroup.userData.kind = 'arena-woodland-family';
  familyGroup.userData.family = familyId;
  familyGroup.userData.treeCount = treeCount;
  familyGroup.userData.maximumTileRadius =
    Math.SQRT2 * TREE_CENTER_JITTER + config.canopyRadius * 0.92;
  familyGroup.userData.maximumHeight =
    family.geometry3d.height +
    config.trunkHeight +
    config.canopyHeight;

  if (treeCount === 0) return familyGroup;

  const woodMaterial = new THREE.MeshStandardMaterial({
    color: config.trunkColor,
    roughness: 0.94,
    metalness: 0,
  });
  const canopyMaterial = new THREE.MeshStandardMaterial({
    color: '#ffffff',
    roughness: 0.98,
    metalness: 0,
  });
  addInstances(
    familyGroup,
    new THREE.CylinderGeometry(0.7, 1, 1, 7, 2),
    woodMaterial,
    trunks,
    'trunks',
    familyId,
    disposables,
  );
  addInstances(
    familyGroup,
    new THREE.CylinderGeometry(0.92, 1.7, 1, 7, 1),
    woodMaterial,
    rootFlares,
    'root-flares',
    familyId,
    disposables,
  );
  addInstances(
    familyGroup,
    new THREE.CylinderGeometry(0.42, 0.68, 1, 5, 1),
    woodMaterial,
    branches,
    'branches',
    familyId,
    disposables,
  );
  addInstances(
    familyGroup,
    new THREE.DodecahedronGeometry(1, 0),
    canopyMaterial,
    canopyLobes,
    'canopy-lobes',
    familyId,
    disposables,
  );
  disposables.push(woodMaterial, canopyMaterial);
  return familyGroup;
}

function addInstances(
  group: THREE.Group,
  geometry: THREE.BufferGeometry,
  material: THREE.Material,
  placements: readonly InstancePlacement[],
  kind: string,
  familyId: string,
  disposables: Disposable[],
): void {
  const mesh = new THREE.InstancedMesh(
    geometry,
    material,
    placements.length,
  );
  const matrix = new THREE.Matrix4();
  for (let index = 0; index < placements.length; index++) {
    const placement = placements[index];
    matrix.compose(
      placement.position,
      placement.quaternion,
      placement.scale,
    );
    mesh.setMatrixAt(index, matrix);
    if (placement.color) mesh.setColorAt(index, placement.color);
  }
  mesh.instanceMatrix.needsUpdate = true;
  if (mesh.instanceColor) mesh.instanceColor.needsUpdate = true;
  mesh.userData.kind = `arena-woodland-${kind}`;
  mesh.userData.family = familyId;
  mesh.userData.instanceCount = placements.length;
  mesh.castShadow = true;
  mesh.receiveShadow = true;
  group.add(mesh);
  disposables.push(geometry);
}

function signedByte(value: number, shift: number): number {
  return unitByte(value, shift) * 2 - 1;
}

function unitByte(value: number, shift: number): number {
  return ((value >>> shift) & 0xff) / 255;
}

function treeHash(
  family: string,
  x: number,
  y: number,
  mask: number,
  salt: number,
): number {
  let value = 2166136261;
  for (const character of family) {
    value ^= character.charCodeAt(0);
    value = Math.imul(value, 16777619);
  }
  for (const component of [x, y, mask, salt]) {
    value ^= component;
    value = Math.imul(value, 16777619);
  }
  return value >>> 0;
}
