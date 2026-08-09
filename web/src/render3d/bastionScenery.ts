import * as THREE from 'three';
import type {
  ArenaTheme,
  BastionCover3d,
  WallFamily,
} from '../render/arenaThemes';
import { WallLayout } from '../render/wallTopology';

type Disposable = { dispose: () => void };

type Placement = {
  position: THREE.Vector3;
  rotationY: number;
  scale: THREE.Vector3;
};

const sides = [
  { dx: 0, dy: -1, inwardRotationY: 0 },
  { dx: 1, dy: 0, inwardRotationY: -Math.PI / 2 },
  { dx: 0, dy: 1, inwardRotationY: Math.PI },
  { dx: -1, dy: 0, inwardRotationY: Math.PI / 2 },
] as const;

/**
 * Raised, topology-derived hard-surface silhouettes for an authored bastion family.
 *
 * The continuous wall extrusion remains the collision-readable substrate. These sparse
 * shoulders, end towers, sloped face ribs, and outside-map foundry supports break its
 * single-height skyline without stamping a replacement mesh into every tile. All in-map
 * parts remain inside blocked footprints; the broad apron and its supports only extend
 * away from the arena, where they cannot imply playable ground.
 */
export function buildBastionScenery(
  layout: WallLayout,
  theme: ArenaTheme,
  openEdgeInset: number,
  disposables: Disposable[],
): THREE.Group {
  const group = new THREE.Group();
  group.userData.kind = 'arena-wall-bastion-scenery';

  for (const [familyId, family] of theme.walls.families) {
    const config = family.geometry3d.bastionCover;
    if (config === null) continue;
    const familyGroup = buildFamily(
      layout,
      familyId,
      family,
      config,
      openEdgeInset,
      disposables,
    );
    if (familyGroup.children.length > 0) group.add(familyGroup);
  }

  group.userData.moduleCount = group.children.reduce(
    (sum, child) => sum + (child.userData.moduleCount ?? 0),
    0,
  );
  return group;
}

function buildFamily(
  layout: WallLayout,
  familyId: string,
  family: WallFamily,
  config: BastionCover3d,
  openEdgeInset: number,
  disposables: Disposable[],
): THREE.Group {
  const shoulders: Placement[] = [];
  const towers: Placement[] = [];
  const roundTowers: Placement[] = [];
  const splitTowers: Placement[] = [];
  const trims: Placement[] = [];
  const heatSeams: Placement[] = [];
  const faceRibs: Placement[] = [];
  const boundaryPylons: Placement[] = [];
  const boundaryLinks: Placement[] = [];
  const cornerPylons: Placement[] = [];
  let maximumHeight = family.geometry3d.height;

  const upper = family.geometry3d.upperProfile;
  const narrowSpan = Math.max(
    0.28,
    1 -
      2 *
        (openEdgeInset +
          (upper?.inset ?? 0) +
          (upper?.chamfer ?? 0)),
  );

  for (const wall of layout.walls()) {
    if (wall.family !== familyId) continue;
    const seed = bastionHash(familyId, wall.x, wall.y, wall.mask, 29);
    const horizontal =
      Number(sameFamily(layout, wall.x - 1, wall.y, familyId)) +
      Number(sameFamily(layout, wall.x + 1, wall.y, familyId));
    const vertical =
      Number(sameFamily(layout, wall.x, wall.y - 1, familyId)) +
      Number(sameFamily(layout, wall.x, wall.y + 1, familyId));
    const degree = horizontal + vertical;
    const turnsOrBranches =
      degree !== 2 || (horizontal === 1 && vertical === 1);
    const rotationY =
      horizontal > vertical
        ? 0
        : vertical > horizontal
          ? Math.PI / 2
          : seed % 2 === 0
            ? 0
            : Math.PI / 2;
    const rise = lerp(
      config.minRise,
      config.maxRise,
      unitByte(seed, 8),
    );
    const moduleBase = family.geometry3d.height - 0.024;
    const centre = new THREE.Vector3(
      wall.x + 0.5,
      moduleBase,
      wall.y + 0.5,
    );

    if (turnsOrBranches || seed % config.towerEvery === 0) {
      const span = Math.min(
        narrowSpan * (0.72 + unitByte(seed, 16) * 0.12),
        0.5,
      );
      const towerRise = rise * (turnsOrBranches ? 1.08 : 0.88);
      const variant = (seed >>> 5) % 3;
      if (variant === 0) {
        const placement = {
          position: centre,
          rotationY,
          scale: new THREE.Vector3(
            span * (0.82 + unitByte(seed, 0) * 0.28),
            towerRise,
            span * (0.8 + unitByte(seed, 24) * 0.25),
          ),
        };
        towers.push(placement);
        trims.push(topTrim(placement, moduleBase, towerRise, 1.06));
        if (seed % 4 === 0)
          heatSeams.push(
            heatSeam(placement, moduleBase, towerRise, span * 0.42),
          );
        maximumHeight = Math.max(maximumHeight, moduleBase + towerRise);
      } else if (variant === 1) {
        const roundRise = towerRise * (0.86 + unitByte(seed, 24) * 0.32);
        const roundSpan = span * (0.82 + unitByte(seed, 0) * 0.18);
        const placement = {
          position: new THREE.Vector3(
            centre.x,
            moduleBase + roundRise / 2,
            centre.z,
          ),
          rotationY,
          scale: new THREE.Vector3(roundSpan, roundRise, roundSpan),
        };
        roundTowers.push(placement);
        maximumHeight = Math.max(maximumHeight, moduleBase + roundRise);
      } else {
        const gap = span * (0.3 + unitByte(seed, 0) * 0.14);
        for (const direction of [-1, 1] as const) {
          const splitRise = towerRise *
            (direction < 0
              ? 0.68 + unitByte(seed, 16) * 0.18
              : 0.92 + unitByte(seed, 24) * 0.2);
          const alongX = Math.abs(Math.cos(rotationY)) > 0.5;
          const placement = {
            position: new THREE.Vector3(
              centre.x + (alongX ? direction * gap : 0),
              moduleBase,
              centre.z + (alongX ? 0 : direction * gap),
            ),
            rotationY,
            scale: new THREE.Vector3(span * 0.45, splitRise, span * 0.74),
          };
          splitTowers.push(placement);
          trims.push(topTrim(placement, moduleBase, splitRise, 1.05));
          maximumHeight = Math.max(maximumHeight, moduleBase + splitRise);
        }
      }
    } else if (seed % config.shoulderEvery === 0) {
      const neighbours = rotationY === 0 ? horizontal : vertical;
      const length = Math.min(
        neighbours === 2 ? 0.9 : neighbours === 1 ? 0.72 : narrowSpan,
        0.74 + unitByte(seed, 24) * 0.18,
      );
      const cross = Math.min(
        narrowSpan * (0.5 + unitByte(seed, 16) * 0.38),
        0.52,
      );
      const shoulderRise = rise * (0.72 + unitByte(seed, 0) * 0.36);
      const placement = {
        position: centre,
        rotationY,
        scale: new THREE.Vector3(length, shoulderRise, cross),
      };
      shoulders.push(placement);
      trims.push(topTrim(placement, moduleBase, shoulderRise, 1.04));
      if (seed % 5 === 0)
        heatSeams.push(
          heatSeam(placement, moduleBase, shoulderRise, length * 0.46),
        );
      maximumHeight = Math.max(maximumHeight, moduleBase + shoulderRise);
    }

    for (let sideIndex = 0; sideIndex < sides.length; sideIndex++) {
      const side = sides[sideIndex];
      if (
        layout.familyAt(wall.x + side.dx, wall.y + side.dy) !== null
      )
        continue;
      const sideSeed = bastionHash(
        familyId,
        wall.x,
        wall.y,
        wall.mask,
        sideIndex,
      );
      if (sideSeed % 3 !== 0) continue;
      const depth = 0.18 + unitByte(sideSeed, 8) * 0.08;
      faceRibs.push({
        position: new THREE.Vector3(
          wall.x +
            0.5 +
            side.dx * (0.5 - openEdgeInset - depth / 2),
          0.012,
          wall.y +
            0.5 +
            side.dy * (0.5 - openEdgeInset - depth / 2),
        ),
        rotationY: side.inwardRotationY,
        scale: new THREE.Vector3(
          0.2 + unitByte(sideSeed, 16) * 0.16,
          family.geometry3d.height + rise * 0.38,
          depth,
        ),
      });
    }

    if (config.boundaryDepth <= 0) continue;
    for (let sideIndex = 0; sideIndex < sides.length; sideIndex++) {
      const side = sides[sideIndex];
      if (!isMapBoundary(wall.x, wall.y, side.dx, side.dy, layout))
        continue;
      const sideSeed = bastionHash(
        familyId,
        wall.x,
        wall.y,
        wall.mask,
        sideIndex + 71,
      );
      if (sideSeed % 2 !== 0) continue;
      const outsideDistance =
        0.5 +
        config.boundaryDepth *
          (0.42 + unitByte(sideSeed, 8) * 0.4);
      const radial = 0.38 + unitByte(sideSeed, 16) * 0.3;
      const tangent = 0.3 + unitByte(sideSeed, 24) * 0.26;
      const height =
        family.geometry3d.height *
        (0.68 + unitByte(sideSeed, 0) * 0.72);
      const x = wall.x + 0.5 + side.dx * outsideDistance;
      const z = wall.y + 0.5 + side.dy * outsideDistance;
      boundaryPylons.push({
        position: new THREE.Vector3(x, 0.018, z),
        rotationY: side.inwardRotationY,
        scale: new THREE.Vector3(tangent, height, radial),
      });
      boundaryLinks.push({
        position: new THREE.Vector3(
          wall.x + 0.5 + side.dx * outsideDistance * 0.48,
          0.045,
          wall.y + 0.5 + side.dy * outsideDistance * 0.48,
        ),
        rotationY: side.inwardRotationY,
        scale: new THREE.Vector3(
          tangent * 0.52,
          0.12 + unitByte(sideSeed, 16) * 0.08,
          outsideDistance * 0.9,
        ),
      });
    }
  }

  if (config.boundaryDepth > 0) {
    const offset = config.boundaryDepth * 0.68;
    for (const [x, z] of [
      [-offset, -offset],
      [layout.width + offset, -offset],
      [layout.width + offset, layout.height + offset],
      [-offset, layout.height + offset],
    ] as const) {
      const cornerHeight =
        family.geometry3d.height + config.maxRise * 0.72;
      cornerPylons.push({
        position: new THREE.Vector3(x, cornerHeight / 2, z),
        rotationY: Math.PI / 4,
        scale: new THREE.Vector3(
          0.62 + config.boundaryDepth * 0.1,
          cornerHeight,
          0.62 + config.boundaryDepth * 0.1,
        ),
      });
    }
  }

  const familyGroup = new THREE.Group();
  familyGroup.userData.kind = 'arena-wall-bastion-family';
  familyGroup.userData.family = familyId;
  familyGroup.userData.maximumHeight = maximumHeight;
  familyGroup.userData.boundaryDepth = config.boundaryDepth;

  addInstances(
    familyGroup,
    roundedPrismGeometry(0.13, 0.055),
    matteMaterial(config.bodyColor, 0.28),
    shoulders,
    'shoulders',
    disposables,
  );
  addInstances(
    familyGroup,
    new THREE.CylinderGeometry(0.5, 0.6, 1, 10, 1),
    matteMaterial(config.bodyColor, 0.3),
    roundTowers,
    'round-towers',
    disposables,
  );
  addInstances(
    familyGroup,
    roundedPrismGeometry(0.08, 0.035),
    matteMaterial(config.bodyColor, 0.34),
    splitTowers,
    'split-towers',
    disposables,
  );
  addInstances(
    familyGroup,
    roundedPrismGeometry(0.24, 0.06),
    matteMaterial(config.bodyColor, 0.32),
    towers,
    'towers',
    disposables,
  );
  addInstances(
    familyGroup,
    roundedPrismGeometry(0.16, 0.02),
    matteMaterial(config.trimColor, 0.5),
    trims,
    'top-trim',
    disposables,
  );
  addInstances(
    familyGroup,
    new THREE.BoxGeometry(1, 1, 1),
    new THREE.MeshStandardMaterial({
      color: config.heatColor,
      emissive: config.heatColor,
      emissiveIntensity: 1.35,
      roughness: 0.76,
      metalness: 0.08,
    }),
    heatSeams,
    'heat-seams',
    disposables,
  );
  addInstances(
    familyGroup,
    wedgeGeometry(),
    matteMaterial(config.trimColor, 0.38),
    faceRibs,
    'face-ribs',
    disposables,
  );
  addInstances(
    familyGroup,
    roundedPrismGeometry(0.2, 0.045),
    matteMaterial(config.bodyColor, 0.32),
    boundaryPylons,
    'boundary-pylons',
    disposables,
  );
  addInstances(
    familyGroup,
    new THREE.BoxGeometry(1, 1, 1),
    matteMaterial(config.trimColor, 0.42),
    boundaryLinks,
    'boundary-links',
    disposables,
  );
  addInstances(
    familyGroup,
    new THREE.CylinderGeometry(0.5, 0.62, 1, 10, 1),
    matteMaterial(config.bodyColor, 0.34),
    cornerPylons,
    'corner-pylons',
    disposables,
  );

  if (config.boundaryDepth > 0)
    familyGroup.add(
      buildBoundaryApron(layout, config, disposables),
    );

  familyGroup.userData.moduleCount = familyGroup.children.reduce(
    (sum, child) => sum + (child.userData.instanceCount ?? 0),
    0,
  );
  return familyGroup;
}

function topTrim(
  placement: Placement,
  base: number,
  rise: number,
  footprintScale: number,
): Placement {
  return {
    position: new THREE.Vector3(
      placement.position.x,
      base + rise - 0.012,
      placement.position.z,
    ),
    rotationY: placement.rotationY,
    scale: new THREE.Vector3(
      placement.scale.x * footprintScale,
      0.026,
      placement.scale.z * footprintScale,
    ),
  };
}

function heatSeam(
  placement: Placement,
  base: number,
  rise: number,
  length: number,
): Placement {
  return {
    position: new THREE.Vector3(
      placement.position.x,
      base + rise + 0.026,
      placement.position.z,
    ),
    rotationY: placement.rotationY,
    scale: new THREE.Vector3(length, 0.012, 0.038),
  };
}

function buildBoundaryApron(
  layout: WallLayout,
  config: BastionCover3d,
  disposables: Disposable[],
): THREE.Group {
  const apron = new THREE.Group();
  apron.userData.kind = 'arena-wall-bastion-apron';
  const placements: Placement[] = [
    {
      position: new THREE.Vector3(
        layout.width / 2,
        -0.03,
        -config.boundaryDepth / 2,
      ),
      rotationY: 0,
      scale: new THREE.Vector3(layout.width, 0.16, config.boundaryDepth),
    },
    {
      position: new THREE.Vector3(
        layout.width / 2,
        -0.03,
        layout.height + config.boundaryDepth / 2,
      ),
      rotationY: 0,
      scale: new THREE.Vector3(layout.width, 0.16, config.boundaryDepth),
    },
    {
      position: new THREE.Vector3(
        -config.boundaryDepth / 2,
        -0.03,
        layout.height / 2,
      ),
      rotationY: 0,
      scale: new THREE.Vector3(config.boundaryDepth, 0.16, layout.height),
    },
    {
      position: new THREE.Vector3(
        layout.width + config.boundaryDepth / 2,
        -0.03,
        layout.height / 2,
      ),
      rotationY: 0,
      scale: new THREE.Vector3(config.boundaryDepth, 0.16, layout.height),
    },
  ];
  addInstances(
    apron,
    new THREE.BoxGeometry(1, 1, 1),
    matteMaterial(config.bodyColor, 0.22),
    placements,
    'apron-bands',
    disposables,
  );

  const railInset = Math.min(0.18, config.boundaryDepth * 0.12);
  const rails: Placement[] = [
    {
      position: new THREE.Vector3(
        layout.width / 2,
        0.075,
        -config.boundaryDepth + railInset,
      ),
      rotationY: 0,
      scale: new THREE.Vector3(
        layout.width + config.boundaryDepth * 1.7,
        0.22,
        railInset * 1.4,
      ),
    },
    {
      position: new THREE.Vector3(
        layout.width / 2,
        0.075,
        layout.height + config.boundaryDepth - railInset,
      ),
      rotationY: 0,
      scale: new THREE.Vector3(
        layout.width + config.boundaryDepth * 1.7,
        0.22,
        railInset * 1.4,
      ),
    },
    {
      position: new THREE.Vector3(
        -config.boundaryDepth + railInset,
        0.075,
        layout.height / 2,
      ),
      rotationY: 0,
      scale: new THREE.Vector3(
        railInset * 1.4,
        0.22,
        layout.height + config.boundaryDepth * 1.7,
      ),
    },
    {
      position: new THREE.Vector3(
        layout.width + config.boundaryDepth - railInset,
        0.075,
        layout.height / 2,
      ),
      rotationY: 0,
      scale: new THREE.Vector3(
        railInset * 1.4,
        0.22,
        layout.height + config.boundaryDepth * 1.7,
      ),
    },
  ];
  addInstances(
    apron,
    roundedPrismGeometry(0.12, 0.035),
    matteMaterial(config.trimColor, 0.34),
    rails,
    'apron-outer-rails',
    disposables,
  );

  const corners: Placement[] = [
    [0, 0],
    [layout.width, 0],
    [layout.width, layout.height],
    [0, layout.height],
  ].map(([x, z]) => ({
    position: new THREE.Vector3(x, -0.03, z),
    rotationY: 0,
    scale: new THREE.Vector3(
      config.boundaryDepth * 2,
      0.16,
      config.boundaryDepth * 2,
    ),
  }));
  addInstances(
    apron,
    new THREE.CylinderGeometry(0.5, 0.5, 1, 20, 1),
    matteMaterial(config.bodyColor, 0.22),
    corners,
    'apron-corners',
    disposables,
  );
  return apron;
}

function addInstances(
  group: THREE.Group,
  geometry: THREE.BufferGeometry,
  material: THREE.Material,
  placements: readonly Placement[],
  kind: string,
  disposables: Disposable[],
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
  const up = new THREE.Vector3(0, 1, 0);
  for (let index = 0; index < placements.length; index++) {
    const placement = placements[index];
    quaternion.setFromAxisAngle(up, placement.rotationY);
    matrix.compose(
      placement.position,
      quaternion,
      placement.scale,
    );
    mesh.setMatrixAt(index, matrix);
  }
  mesh.instanceMatrix.needsUpdate = true;
  mesh.userData.kind = `arena-wall-bastion-${kind}`;
  mesh.userData.instanceCount = placements.length;
  mesh.castShadow = !kind.startsWith('apron') && kind !== 'heat-seams';
  mesh.receiveShadow = kind !== 'heat-seams';
  if (kind === 'heat-seams') mesh.renderOrder = 4;
  group.add(mesh);
  disposables.push(geometry, material);
}

function roundedPrismGeometry(
  radius: number,
  bevel: number,
): THREE.ExtrudeGeometry {
  const shape = new THREE.Shape();
  roundedRectangle(shape, -0.5, -0.5, 0.5, 0.5, radius);
  const geometry = new THREE.ExtrudeGeometry(shape, {
    depth: 1 - bevel * 2,
    bevelEnabled: true,
    bevelThickness: bevel,
    bevelSize: bevel,
    bevelSegments: 2,
    curveSegments: 3,
  });
  geometry.rotateX(-Math.PI / 2);
  return geometry;
}

function roundedRectangle(
  path: THREE.Path,
  left: number,
  top: number,
  right: number,
  bottom: number,
  radius: number,
): void {
  path.moveTo(left + radius, top);
  path.lineTo(right - radius, top);
  path.quadraticCurveTo(right, top, right, top + radius);
  path.lineTo(right, bottom - radius);
  path.quadraticCurveTo(right, bottom, right - radius, bottom);
  path.lineTo(left + radius, bottom);
  path.quadraticCurveTo(left, bottom, left, bottom - radius);
  path.lineTo(left, top + radius);
  path.quadraticCurveTo(left, top, left + radius, top);
  path.closePath();
}

function wedgeGeometry(): THREE.BufferGeometry {
  const geometry = new THREE.BufferGeometry();
  geometry.setAttribute(
    'position',
    new THREE.Float32BufferAttribute(
      [
        -0.5, 0, -0.5,
        0.5, 0, -0.5,
        -0.5, 0, 0.5,
        0.5, 0, 0.5,
        -0.5, 0.18, -0.5,
        0.5, 0.18, -0.5,
        -0.5, 1, 0.5,
        0.5, 1, 0.5,
      ],
      3,
    ),
  );
  geometry.setIndex([
    0, 2, 1, 1, 2, 3,
    4, 5, 6, 5, 7, 6,
    0, 1, 4, 1, 5, 4,
    2, 6, 3, 3, 6, 7,
    0, 4, 2, 2, 4, 6,
    1, 3, 5, 3, 7, 5,
  ]);
  geometry.computeVertexNormals();
  return geometry;
}

function matteMaterial(
  color: string,
  metalness: number,
): THREE.MeshStandardMaterial {
  return new THREE.MeshStandardMaterial({
    color,
    roughness: 0.92,
    metalness,
  });
}

function sameFamily(
  layout: WallLayout,
  x: number,
  y: number,
  family: string,
): boolean {
  return (
    x >= 0 &&
    y >= 0 &&
    x < layout.width &&
    y < layout.height &&
    layout.familyAt(x, y) === family
  );
}

function isMapBoundary(
  x: number,
  y: number,
  dx: number,
  dy: number,
  layout: WallLayout,
): boolean {
  const nextX = x + dx;
  const nextY = y + dy;
  return (
    nextX < 0 ||
    nextY < 0 ||
    nextX >= layout.width ||
    nextY >= layout.height
  );
}

function bastionHash(
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

function unitByte(value: number, shift: number): number {
  return ((value >>> shift) & 0xff) / 255;
}

function lerp(from: number, to: number, amount: number): number {
  return from + (to - from) * amount;
}
