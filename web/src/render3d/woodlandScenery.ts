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

type TreeRoot = {
  x: number;
  z: number;
  seedX: number;
  seedY: number;
  mask: number;
  salt: number;
  border: boolean;
};

/**
 * Actual low-poly trees rooted exclusively in authoritative blocked tiles.
 *
 * Trunks, branches, broken-up crowns, and low understory are instanced geometry. Their
 * positions hash only stable family/tile/mask inputs. Roots stay in authoritative blocked
 * cells, while crowns and flat understory deliberately overlap their cell edges to hide
 * the tile cadence. There is no raised wall substrate below them: the living cluster and
 * flush foliage field are the visual blocker, so the blocked grid never appears as a
 * rectangular planting bed.
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
  const coniferTiers: InstancePlacement[] = [];
  const understoryLobes: InstancePlacement[] = [];
  const roots = treeRoots(layout, familyId, config);

  for (const root of roots) {
    const seed = treeHash(
      familyId,
      root.seedX,
      root.seedY,
      root.mask,
      root.salt,
    );
    const shapeSeed = treeHash(
      familyId,
      root.seedX,
      root.seedY,
      root.mask,
      root.salt + 17,
    );
    const heightNoise = unitByte(shapeSeed, 0);
    const widthNoise = unitByte(shapeSeed, 8);
    const heightScale = lerp(
      config.minHeightScale,
      config.maxHeightScale,
      heightNoise,
    );
    const radialScale =
      lerp(
        config.minWidthScale,
        config.maxWidthScale,
        widthNoise * 0.68 + heightNoise * 0.32,
      ) * (root.border ? 1.12 : 1);
    const canopyHeightScale = 0.72 + unitByte(shapeSeed, 16) * 0.48;
    const lobeReduction = Math.floor(
      unitByte(shapeSeed, 24) * Math.min(3, config.canopyLobes - 1),
    );
    const lobeCount = Math.max(
      2,
      config.canopyLobes - lobeReduction + (shapeSeed % 4 === 0 ? 1 : 0),
    );
    const trunkHeight = config.trunkHeight * heightScale;
    const trunkRadius = config.trunkRadius * radialScale;
    const canopyHeight = config.canopyHeight * canopyHeightScale;
    const woodColor = new THREE.Color(config.trunkColor).multiplyScalar(
      0.82 + unitByte(seed, 0) * 0.18,
    );
    const canopyColor = new THREE.Color(config.canopyColor);
    const canopyAccent = new THREE.Color(config.canopyAccentColor);
    const treeAccentMix = 0.06 + unitByte(shapeSeed, 24) * 0.36;
    const centre = new THREE.Vector3(root.x, 0, root.z);
    const yaw = unitByte(seed, 24) * Math.PI * 2;
    const yawRotation = new THREE.Quaternion().setFromAxisAngle(UP, yaw);
    const trunkDirection = new THREE.Vector3(
      signedByte(seed, 8) * 0.13,
      1,
      signedByte(seed, 16) * 0.13,
    ).normalize();
    const trunkTop = centre
      .clone()
      .add(trunkDirection.clone().multiplyScalar(trunkHeight));
    const trunkRotation = new THREE.Quaternion().setFromUnitVectors(
      UP,
      trunkDirection,
    );

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
        .add(trunkDirection.clone().multiplyScalar(trunkHeight / 2)),
      quaternion: trunkRotation,
      scale: new THREE.Vector3(
        trunkRadius,
        trunkHeight,
        trunkRadius,
      ),
      color: woodColor,
    });

    const crownY = trunkTop.y + canopyHeight * 0.42;
    const lobeRadius = config.canopyRadius * 0.62 * radialScale;
    const lobeOffset = config.canopyRadius * 0.34 * radialScale;
    const branchStartY = centre.y + trunkHeight * 0.68;
    const conifer = shapeSeed % 6 === 0 || (root.border && shapeSeed % 5 === 0);
    if (conifer) {
      const crownHeight = config.canopyHeight * (1.85 + unitByte(seed, 16) * 0.55);
      const tierCount = 2 + (shapeSeed % 2);
      for (let tier = 0; tier < tierCount; tier++) {
        const tierFraction = tier / Math.max(1, tierCount - 1);
        const tierRadius =
          config.canopyRadius * radialScale * (0.82 - tierFraction * 0.25);
        const tierHeight = crownHeight * (0.52 - tierFraction * 0.08);
        coniferTiers.push({
          position: new THREE.Vector3(
            trunkTop.x,
            trunkTop.y + crownHeight * (0.18 + tierFraction * 0.25),
            trunkTop.z,
          ),
          quaternion: yawRotation.clone(),
          scale: new THREE.Vector3(tierRadius, tierHeight, tierRadius),
          color: canopyColor.clone().lerp(
            canopyAccent,
            0.08 + tierFraction * 0.22,
          ),
        });
      }
    } else {
      canopyLobes.push({
        position: new THREE.Vector3(trunkTop.x, crownY, trunkTop.z),
        quaternion: yawRotation.clone(),
        scale: new THREE.Vector3(
          config.canopyRadius * 0.76 * radialScale,
          canopyHeight * 0.56,
          config.canopyRadius *
            (0.62 + unitByte(shapeSeed, 16) * 0.18) *
            radialScale,
        ),
        color: canopyColor.clone().lerp(
          canopyAccent,
          0.12 + unitByte(shapeSeed, 8) * 0.48,
        ),
      });
      for (let lobe = 0; lobe < lobeCount; lobe++) {
        const lobeSeed = treeHash(
          familyId,
          root.seedX,
          root.seedY,
          root.mask,
          root.salt + lobe + 1,
        );
        const angle =
          yaw +
          (lobe / lobeCount) * Math.PI * 2 +
          signedByte(lobeSeed, 8) * 0.24;
        const lobePosition = new THREE.Vector3(
          trunkTop.x + Math.cos(angle) * lobeOffset,
          crownY + signedByte(lobeSeed, 16) * canopyHeight * 0.14,
          trunkTop.z + Math.sin(angle) * lobeOffset,
        );
        canopyLobes.push({
          position: lobePosition,
          quaternion: new THREE.Quaternion().setFromAxisAngle(
            UP,
            unitByte(lobeSeed, 24) * Math.PI * 2,
          ),
          scale: new THREE.Vector3(
            lobeRadius * (0.84 + unitByte(lobeSeed, 0) * 0.22),
            canopyHeight / 2,
            lobeRadius * (0.78 + unitByte(lobeSeed, 16) * 0.28),
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
          lerp(centre.x, trunkTop.x, 0.68),
          branchStartY,
          lerp(centre.z, trunkTop.z, 0.68),
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
    }

    understoryLobes.push({
      position: new THREE.Vector3(
        centre.x + signedByte(shapeSeed, 8) * config.canopyRadius * 0.18,
        config.canopyRadius * 0.075,
        centre.z + signedByte(shapeSeed, 16) * config.canopyRadius * 0.18,
      ),
      quaternion: yawRotation.clone(),
      scale: new THREE.Vector3(
        config.canopyRadius * radialScale * (0.52 + unitByte(seed, 0) * 0.16),
        config.canopyRadius * 0.075,
        config.canopyRadius * radialScale * (0.44 + unitByte(seed, 24) * 0.18),
      ),
      color: canopyColor.clone().lerp(canopyAccent, 0.34),
    });

    const understoryCount = 2 + (shapeSeed % 3);
    const understoryRadius = config.canopyRadius * radialScale * 0.32;
    const understoryOffset = config.canopyRadius * radialScale * 0.5;
    for (let shrub = 0; shrub < understoryCount; shrub++) {
      const shrubSeed = treeHash(
        familyId,
        root.seedX,
        root.seedY,
        root.mask,
        root.salt + 41 + shrub,
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
  familyGroup.userData.treeCount = roots.length;
  familyGroup.userData.borderTreeCount = roots.filter((root) => root.border).length;
  familyGroup.userData.maximumRootSpread = config.rootSpread;
  familyGroup.userData.maximumCanopyRadius =
    config.canopyRadius * config.maxWidthScale * 0.96 * 1.12;
  familyGroup.userData.allowsCanopyOverhang = true;
  familyGroup.userData.boundaryDepth = config.boundaryDepth;
  familyGroup.userData.boundaryShape =
    config.boundaryDepth > 0 ? 'scattered-rounded-band' : 'none';
  familyGroup.userData.maximumHeight =
    config.trunkHeight * config.maxHeightScale + config.canopyHeight * 2.4;

  if (roots.length === 0) return familyGroup;

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
    new THREE.ConeGeometry(1, 1, 8, 1, false),
    canopyMaterial,
    coniferTiers,
    'conifer-tiers',
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

function treeRoots(
  layout: WallLayout,
  familyId: string,
  config: TreeCover3d,
): TreeRoot[] {
  const roots: TreeRoot[] = [];
  const guaranteed = Math.floor(config.density);
  const fractional = config.density - guaranteed;
  for (const wall of layout.walls()) {
    if (wall.family !== familyId) continue;
    const densitySeed = treeHash(familyId, wall.x, wall.y, wall.mask, 73);
    const count =
      guaranteed + (unitByte(densitySeed, 0) < fractional ? 1 : 0);
    for (let slot = 0; slot < count; slot++) {
      const rootSeed = treeHash(
        familyId,
        wall.x,
        wall.y,
        wall.mask,
        101 + slot * 13,
      );
      roots.push({
        x: wall.x + 0.5 + signedByte(rootSeed, 8) * config.rootSpread,
        z: wall.y + 0.5 + signedByte(rootSeed, 16) * config.rootSpread,
        seedX: wall.x,
        seedY: wall.y,
        mask: wall.mask,
        salt: 101 + slot * 13,
        border: false,
      });
    }
  }
  if (config.boundaryDepth > 0)
    roots.push(...boundaryForestRoots(layout, familyId, config.boundaryDepth));
  return roots;
}

function boundaryForestRoots(
  layout: WallLayout,
  familyId: string,
  depth: number,
): TreeRoot[] {
  const roots: TreeRoot[] = [];
  // Fill one jittered band instead of drawing parallel rows along four sides. Distance
  // from the map is Euclidean at the corners, rounding the old square outline, while a
  // pair of slow waves changes the outer reach along each edge. Sparse rejection near
  // that reach feathers the silhouette rather than terminating every tree at one line.
  const spacing = 0.49;
  const paddedWidth = layout.width + depth * 2;
  const paddedHeight = layout.height + depth * 2;
  const columns = Math.ceil(paddedWidth / spacing);
  const rows = Math.ceil(paddedHeight / spacing);
  const stepX = paddedWidth / columns;
  const stepZ = paddedHeight / rows;
  for (let row = 0; row < rows; row++) {
    for (let column = 0; column < columns; column++) {
      const seed = treeHash(familyId, column, row, 255, 211);
      const x =
        -depth +
        (column + 0.5) * stepX +
        signedByte(seed, 8) * stepX * 0.42;
      const z =
        -depth +
        (row + 0.5) * stepZ +
        signedByte(seed, 16) * stepZ * 0.42;
      const boundary = outsideBoundary(layout, x, z);
      if (boundary.distance <= 0.06) continue;
      const reach = boundaryReach(familyId, boundary.side, boundary.along, depth);
      if (boundary.distance > reach) continue;
      const edgeFraction = boundary.distance / reach;
      const keepProbability =
        edgeFraction < 0.62
          ? 0.96
          : lerp(0.9, 0.48, (edgeFraction - 0.62) / 0.38);
      if (unitByte(seed, 24) > keepProbability) continue;
      roots.push({
        x,
        z,
        seedX: column,
        seedY: row,
        mask: 255,
        salt: 211,
        border: true,
      });
    }
  }
  return roots;
}

type OutsideBoundary = {
  distance: number;
  side: 0 | 1 | 2 | 3;
  along: number;
};

function outsideBoundary(
  layout: WallLayout,
  x: number,
  z: number,
): OutsideBoundary {
  const outsideLeft = Math.max(0, -x);
  const outsideRight = Math.max(0, x - layout.width);
  const outsideNear = Math.max(0, -z);
  const outsideFar = Math.max(0, z - layout.height);
  const outsideX = Math.max(outsideLeft, outsideRight);
  const outsideZ = Math.max(outsideNear, outsideFar);
  if (outsideX >= outsideZ)
    return {
      distance: Math.hypot(outsideX, outsideZ),
      side: outsideLeft > 0 ? 2 : 3,
      along: z,
    };
  return {
    distance: Math.hypot(outsideX, outsideZ),
    side: outsideNear > 0 ? 0 : 1,
    along: x,
  };
}

function boundaryReach(
  familyId: string,
  side: number,
  along: number,
  depth: number,
): number {
  const phaseSeed = treeHash(familyId, side, 0, 255, 419);
  const phaseA = unitByte(phaseSeed, 0) * Math.PI * 2;
  const phaseB = unitByte(phaseSeed, 8) * Math.PI * 2;
  const wave =
    Math.sin(along * 0.53 + phaseA) * 0.62 +
    Math.sin(along * 1.37 + phaseB) * 0.38;
  return depth * (0.78 + wave * 0.22);
}

function lerp(from: number, to: number, amount: number): number {
  return from + (to - from) * amount;
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
