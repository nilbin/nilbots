import * as THREE from 'three';
import type {
  ArenaTheme,
  TreeCover3d,
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
const TREE_CENTER_JITTER = 0.045;

/**
 * Actual low-poly trees rooted exclusively in authoritative blocked tiles.
 *
 * Trunks, branches, broken-up crowns, and low understory are instanced geometry. Their
 * positions hash only stable family/tile/mask inputs and every crown stays inside its
 * occupied tile's reviewed clearance envelope. There is deliberately no raised wall
 * substrate below them: the living cluster and flush foliage field are the visual
 * blocker, so the blocked grid never appears as a rectangular planting bed.
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
  config: TreeCover3d,
  disposables: Disposable[],
): THREE.Group {
  const trunks: InstancePlacement[] = [];
  const rootFlares: InstancePlacement[] = [];
  const branches: InstancePlacement[] = [];
  const canopyLobes: InstancePlacement[] = [];
  const understoryLobes: InstancePlacement[] = [];
  let treeCount = 0;

  for (const wall of layout.walls()) {
    if (wall.family !== familyId) continue;
    treeCount++;
    const seed = treeHash(familyId, wall.x, wall.y, wall.mask, 0);
    const shapeSeed = treeHash(familyId, wall.x, wall.y, wall.mask, 17);
    const heightScale = 0.5 + unitByte(shapeSeed, 0) * 0.48;
    const radialScale = 0.84 + unitByte(shapeSeed, 8) * 0.24;
    const canopyHeightScale = 0.8 + unitByte(shapeSeed, 16) * 0.25;
    const lobeReduction = Math.floor(
      unitByte(shapeSeed, 24) * Math.min(3, config.canopyLobes - 1),
    );
    const lobeCount = Math.max(2, config.canopyLobes - lobeReduction);
    const trunkHeight = config.trunkHeight * heightScale;
    const trunkRadius = config.trunkRadius * radialScale;
    const canopyHeight = config.canopyHeight * canopyHeightScale;
    const woodColor = new THREE.Color(config.trunkColor).multiplyScalar(
      0.82 + unitByte(seed, 0) * 0.18,
    );
    const canopyColor = new THREE.Color(config.canopyColor);
    const canopyAccent = new THREE.Color(config.canopyAccentColor);
    const treeAccentMix = 0.06 + unitByte(shapeSeed, 24) * 0.36;
    const centre = new THREE.Vector3(
      wall.x + 0.5 + signedByte(seed, 8) * TREE_CENTER_JITTER,
      0,
      wall.y + 0.5 + signedByte(seed, 16) * TREE_CENTER_JITTER,
    );
    const yaw = unitByte(seed, 24) * Math.PI * 2;
    const yawRotation = new THREE.Quaternion().setFromAxisAngle(UP, yaw);

    rootFlares.push({
      position: centre.clone().add(new THREE.Vector3(0, 0.0325, 0)),
      quaternion: yawRotation.clone(),
      scale: new THREE.Vector3(
        trunkRadius,
        0.065,
        trunkRadius,
      ),
      color: woodColor,
    });
    trunks.push({
      position: centre
        .clone()
        .add(new THREE.Vector3(0, trunkHeight / 2, 0)),
      quaternion: yawRotation.clone(),
      scale: new THREE.Vector3(
        trunkRadius,
        trunkHeight,
        trunkRadius,
      ),
      color: woodColor,
    });

    const crownY =
      centre.y + trunkHeight + canopyHeight * 0.42;
    const lobeRadius = config.canopyRadius * 0.62 * radialScale;
    const lobeOffset = config.canopyRadius * 0.3 * radialScale;
    const branchStartY = centre.y + trunkHeight * 0.68;
    canopyLobes.push({
      position: new THREE.Vector3(centre.x, crownY, centre.z),
      quaternion: yawRotation.clone(),
      scale: new THREE.Vector3(
        config.canopyRadius * 0.74 * radialScale,
        canopyHeight * 0.54,
        config.canopyRadius * 0.7 * radialScale,
      ),
      color: canopyColor.clone().lerp(
        canopyAccent,
        0.12 + unitByte(shapeSeed, 8) * 0.48,
      ),
    });
    for (let lobe = 0; lobe < lobeCount; lobe++) {
      const lobeSeed = treeHash(
        familyId,
        wall.x,
        wall.y,
        wall.mask,
        lobe + 1,
      );
      const angle =
        yaw +
        (lobe / lobeCount) * Math.PI * 2 +
        signedByte(lobeSeed, 8) * 0.18;
      const lobePosition = new THREE.Vector3(
        centre.x + Math.cos(angle) * lobeOffset,
        crownY + signedByte(lobeSeed, 16) * canopyHeight * 0.08,
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
          canopyHeight / 2,
          lobeRadius,
        ),
        color: canopyColor.clone().lerp(
          canopyAccent,
          Math.min(
            0.72,
            treeAccentMix + (lobeSeed % 3 === 0 ? 0.2 : 0),
          ),
        ),
      });

      const branchStart = new THREE.Vector3(
        centre.x,
        branchStartY,
        centre.z,
      );
      const branchEnd = lobePosition.clone().add(
        new THREE.Vector3(0, -canopyHeight * 0.18, 0),
      );
      const direction = branchEnd.clone().sub(branchStart);
      branches.push({
        position: branchStart.clone().add(direction.clone().multiplyScalar(0.5)),
        quaternion: new THREE.Quaternion().setFromUnitVectors(
          UP,
          direction.clone().normalize(),
        ),
        scale: new THREE.Vector3(
          trunkRadius * 0.5,
          direction.length(),
          trunkRadius * 0.5,
        ),
        color: woodColor,
      });
    }

    understoryLobes.push({
      position: new THREE.Vector3(
        centre.x + signedByte(shapeSeed, 8) * config.canopyRadius * 0.12,
        config.canopyRadius * 0.075,
        centre.z + signedByte(shapeSeed, 16) * config.canopyRadius * 0.12,
      ),
      quaternion: yawRotation.clone(),
      scale: new THREE.Vector3(
        config.canopyRadius * (0.52 + unitByte(seed, 0) * 0.12),
        config.canopyRadius * 0.075,
        config.canopyRadius * (0.44 + unitByte(seed, 24) * 0.14),
      ),
      color: canopyColor.clone().lerp(canopyAccent, 0.34),
    });

    const understoryCount = 2 + (shapeSeed % 2);
    const understoryRadius = config.canopyRadius * 0.34;
    const understoryOffset = config.canopyRadius * 0.48;
    for (let shrub = 0; shrub < understoryCount; shrub++) {
      const shrubSeed = treeHash(
        familyId,
        wall.x,
        wall.y,
        wall.mask,
        41 + shrub,
      );
      const angle =
        yaw +
        (shrub / understoryCount) * Math.PI * 2 +
        signedByte(shrubSeed, 8) * 0.36;
      const shrubScale = 0.78 + unitByte(shrubSeed, 16) * 0.22;
      understoryLobes.push({
        position: new THREE.Vector3(
          centre.x + Math.cos(angle) * understoryOffset,
          understoryRadius * 0.34,
          centre.z + Math.sin(angle) * understoryOffset,
        ),
        quaternion: new THREE.Quaternion().setFromAxisAngle(
          UP,
          unitByte(shrubSeed, 24) * Math.PI * 2,
        ),
        scale: new THREE.Vector3(
          understoryRadius * shrubScale,
          understoryRadius * 0.34,
          understoryRadius * (0.68 + unitByte(shrubSeed, 0) * 0.22),
        ),
        color: canopyColor.clone().lerp(
          canopyAccent,
          0.24 + unitByte(shrubSeed, 8) * 0.34,
        ),
      });
    }
  }

  const familyGroup = new THREE.Group();
  familyGroup.userData.kind = 'arena-woodland-family';
  familyGroup.userData.family = familyId;
  familyGroup.userData.treeCount = treeCount;
  familyGroup.userData.maximumTileRadius =
    Math.SQRT2 * TREE_CENTER_JITTER + config.canopyRadius * 0.92 * 1.08;
  familyGroup.userData.maximumHeight =
    config.trunkHeight + config.canopyHeight;

  if (treeCount === 0) return familyGroup;

  const woodMaterial = new THREE.MeshStandardMaterial({
    color: '#ffffff',
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
  addInstances(
    familyGroup,
    new THREE.DodecahedronGeometry(1, 0),
    canopyMaterial,
    understoryLobes,
    'understory-lobes',
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
