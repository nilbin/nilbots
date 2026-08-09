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
  const crucibleCollars: Placement[] = [];
  const crucibleLids: Placement[] = [];
  const crucibleClamps: Placement[] = [];
  const crucibleVents: Placement[] = [];
  const crucibleCores: Placement[] = [];
  const splitTowers: Placement[] = [];
  const trims: Placement[] = [];
  const heatSeams: Placement[] = [];
  const faceRibs: Placement[] = [];
  const foundryHalls: Placement[] = [];
  const hallRoofs: Placement[] = [];
  const hallStacks: Placement[] = [];
  const hallStackCaps: Placement[] = [];
  const hallHeatChannels: Placement[] = [];
  const boundaryPylons: Placement[] = [];
  const boundaryLinks: Placement[] = [];
  const chimneyStacks: Placement[] = [];
  const chimneyCaps: Placement[] = [];
  const chimneyBands: Placement[] = [];
  const furnaceHoods: Placement[] = [];
  const furnaceRecesses: Placement[] = [];
  const furnaceCores: Placement[] = [];
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

  const familyWalls = [...layout.walls()].filter(
    (wall) => wall.family === familyId,
  );
  const claimedByHall = new Set<string>();
  if (config.hallEvery !== null) {
    for (const wall of familyWalls) {
      const horizontal =
        Number(sameFamily(layout, wall.x - 1, wall.y, familyId)) +
        Number(sameFamily(layout, wall.x + 1, wall.y, familyId));
      const vertical =
        Number(sameFamily(layout, wall.x, wall.y - 1, familyId)) +
        Number(sameFamily(layout, wall.x, wall.y + 1, familyId));
      if (!((horizontal === 2 && vertical === 0) ||
        (vertical === 2 && horizontal === 0)))
        continue;
      const seed = bastionHash(familyId, wall.x, wall.y, wall.mask, 113);
      if (seed % config.hallEvery !== 0) continue;
      const alongX = horizontal === 2;
      const claimedTiles = [-1, 0, 1].map((offset) =>
        bastionTileKey(
          wall.x + (alongX ? offset : 0),
          wall.y + (alongX ? 0 : offset),
        ),
      );
      if (claimedTiles.some((key) => claimedByHall.has(key))) continue;
      for (const key of claimedTiles) claimedByHall.add(key);

      const moduleBase = family.geometry3d.height - 0.024;
      const riseUnit = unitByte(seed, 8);
      const rise = lerp(
        config.minRise,
        config.maxRise,
        riseUnit,
      );
      const hallRise = 0.075 + rise * (0.64 + unitByte(seed, 16) * 0.22);
      const centre = new THREE.Vector3(
        wall.x + 0.5,
        moduleBase,
        wall.y + 0.5,
      );
      const placement: Placement = {
        position: centre,
        rotationY: alongX ? 0 : Math.PI / 2,
        scale: new THREE.Vector3(
          2.48 + unitByte(seed, 24) * 0.18,
          hallRise,
          Math.min(narrowSpan * 0.78, 0.58),
        ),
      };
      foundryHalls.push(placement);
      const roof = topTrim(placement, moduleBase, hallRise, 0.9);
      roof.scale.y = 0.042;
      hallRoofs.push(roof);

      const hallTop = moduleBase + hallRise;
      const stackCount = 2 + ((seed >>> 20) % 2);
      for (let stackIndex = 0; stackIndex < stackCount; stackIndex++) {
        const stackSeed = bastionHash(
          familyId,
          wall.x,
          wall.y,
          wall.mask,
          151 + stackIndex,
        );
        const alongOffset =
          (stackIndex - (stackCount - 1) / 2) *
          (0.44 + unitByte(stackSeed, 8) * 0.08);
        const crossOffset = (stackIndex % 2 === 0 ? -1 : 1) * 0.1;
        const stackHeight =
          0.18 + rise * (0.72 + unitByte(stackSeed, 16) * 0.76);
        const stackRadius = 0.14 + unitByte(stackSeed, 24) * 0.045;
        const stackX = centre.x +
          (alongX ? alongOffset : crossOffset);
        const stackZ = centre.z +
          (alongX ? crossOffset : alongOffset);
        hallStacks.push({
          position: new THREE.Vector3(
            stackX,
            hallTop + 0.025 + stackHeight / 2,
            stackZ,
          ),
          rotationY: placement.rotationY,
          scale: new THREE.Vector3(stackRadius, stackHeight, stackRadius),
        });
        hallStackCaps.push({
          position: new THREE.Vector3(
            stackX,
            hallTop + 0.045 + stackHeight,
            stackZ,
          ),
          rotationY: placement.rotationY,
          scale: new THREE.Vector3(
            stackRadius * 1.3,
            0.072,
            stackRadius * 1.3,
          ),
        });
        maximumHeight = Math.max(
          maximumHeight,
          hallTop + stackHeight + 0.085,
        );
      }
      hallHeatChannels.push({
        position: new THREE.Vector3(
          centre.x,
          hallTop + 0.052,
          centre.z,
        ),
        rotationY: placement.rotationY,
        scale: new THREE.Vector3(0.58, 0.014, 0.055),
      });
    }
  }

  for (const wall of familyWalls) {
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
    // Keep the established low industrial profile, but let the authored rise
    // range produce enough deterministic variation to clear a bot chassis.
    const riseUnit = unitByte(seed, 8);
    const rise = lerp(
      config.minRise,
      config.maxRise,
      riseUnit,
    );
    const moduleBase = family.geometry3d.height - 0.024;
    const centre = new THREE.Vector3(
      wall.x + 0.5,
      moduleBase,
      wall.y + 0.5,
    );
    const hallClaimed = claimedByHall.has(bastionTileKey(wall.x, wall.y));

    if (
      !hallClaimed &&
      (turnsOrBranches || seed % config.towerEvery === 0)
    ) {
      const span = Math.min(
        narrowSpan * (0.72 + unitByte(seed, 16) * 0.12),
        0.5,
      );
      const towerRise = rise * (turnsOrBranches ? 1.08 : 0.88);
      const variant = (seed >>> 5) % 4;
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
        trims.push(topTrim(placement, moduleBase, towerRise, 0.84));
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
        const top = moduleBase + roundRise;
        crucibleCollars.push({
          position: new THREE.Vector3(centre.x, top + 0.02, centre.z),
          rotationY,
          scale: new THREE.Vector3(
            roundSpan * 1.04,
            0.05,
            roundSpan * 1.04,
          ),
        });
        crucibleLids.push({
          position: new THREE.Vector3(centre.x, top + 0.052, centre.z),
          rotationY,
          scale: new THREE.Vector3(
            roundSpan * 0.78,
            0.028,
            roundSpan * 0.78,
          ),
        });
        const clampAlongX = ((seed >>> 14) & 1) === 0;
        const clampOffset = roundSpan * 0.45;
        for (const direction of [-1, 1] as const) {
          crucibleClamps.push({
            position: new THREE.Vector3(
              centre.x + (clampAlongX ? direction * clampOffset : 0),
              top + 0.064,
              centre.z + (clampAlongX ? 0 : direction * clampOffset),
            ),
            rotationY,
            scale: new THREE.Vector3(
              roundSpan * (clampAlongX ? 0.15 : 0.08),
              0.025,
              roundSpan * (clampAlongX ? 0.08 : 0.15),
            ),
          });
        }
        const ventRotation = rotationY + (clampAlongX ? 0 : Math.PI / 2);
        crucibleVents.push({
          position: new THREE.Vector3(centre.x, top + 0.069, centre.z),
          rotationY: ventRotation,
          scale: new THREE.Vector3(
            roundSpan * 0.3,
            0.016,
            roundSpan * 0.07,
          ),
        });
        if ((seed >>> 12) % 5 === 0)
          crucibleCores.push({
            position: new THREE.Vector3(centre.x, top + 0.078, centre.z),
            rotationY: ventRotation,
            scale: new THREE.Vector3(
              roundSpan * 0.17,
              0.01,
              roundSpan * 0.035,
            ),
          });
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
          trims.push(topTrim(placement, moduleBase, splitRise, 0.8));
          maximumHeight = Math.max(maximumHeight, moduleBase + splitRise);
        }
      }
    } else if (!hallClaimed && seed % config.shoulderEvery === 0) {
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
      trims.push(topTrim(placement, moduleBase, shoulderRise, 0.82));
      if (seed % 5 === 0)
        heatSeams.push(
          heatSeam(placement, moduleBase, shoulderRise, length * 0.46),
        );
      maximumHeight = Math.max(maximumHeight, moduleBase + shoulderRise);
    }

    for (let sideIndex = 0; !hallClaimed && sideIndex < sides.length; sideIndex++) {
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
      if (sideSeed % 2 === 0) {
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

      if (
        config.chimneyEvery !== null &&
        sideSeed % config.chimneyEvery === 0
      ) {
        const chimneyDistance =
          0.5 +
          config.boundaryDepth *
            (0.58 + unitByte(sideSeed, 16) * 0.26);
        const chimneyHeight =
          family.geometry3d.height *
          (1.2 + unitByte(sideSeed, 8) * 0.92);
        const chimneyRadius = 0.18 + unitByte(sideSeed, 24) * 0.1;
        const chimneyX = wall.x + 0.5 + side.dx * chimneyDistance;
        const chimneyZ = wall.y + 0.5 + side.dy * chimneyDistance;
        chimneyStacks.push({
          position: new THREE.Vector3(
            chimneyX,
            0.04 + chimneyHeight / 2,
            chimneyZ,
          ),
          rotationY: side.inwardRotationY,
          scale: new THREE.Vector3(
            chimneyRadius,
            chimneyHeight,
            chimneyRadius,
          ),
        });
        chimneyCaps.push({
          position: new THREE.Vector3(
            chimneyX,
            0.07 + chimneyHeight,
            chimneyZ,
          ),
          rotationY: side.inwardRotationY,
          scale: new THREE.Vector3(
            chimneyRadius * 1.34,
            0.13,
            chimneyRadius * 1.34,
          ),
        });
        chimneyBands.push({
          position: new THREE.Vector3(
            chimneyX,
            0.04 + chimneyHeight * (0.58 + unitByte(sideSeed, 0) * 0.12),
            chimneyZ,
          ),
          rotationY: side.inwardRotationY,
          scale: new THREE.Vector3(
            chimneyRadius * 1.12,
            0.07,
            chimneyRadius * 1.12,
          ),
        });
        maximumHeight = Math.max(maximumHeight, chimneyHeight + 0.135);
      }

      if (
        config.furnaceEvery !== null &&
        sideSeed % config.furnaceEvery === 0
      ) {
        const faceDistance = 0.5 + 0.055;
        const faceX = wall.x + 0.5 + side.dx * faceDistance;
        const faceZ = wall.y + 0.5 + side.dy * faceDistance;
        furnaceHoods.push({
          position: new THREE.Vector3(faceX, 0.06, faceZ),
          rotationY: side.inwardRotationY,
          scale: new THREE.Vector3(0.48, 0.46, 0.13),
        });
        furnaceRecesses.push({
          position: new THREE.Vector3(
            faceX + side.dx * 0.075,
            0.29,
            faceZ + side.dy * 0.075,
          ),
          rotationY: side.inwardRotationY,
          scale: new THREE.Vector3(0.34, 0.27, 0.042),
        });
        furnaceCores.push({
          position: new THREE.Vector3(
            faceX + side.dx * 0.101,
            0.29,
            faceZ + side.dy * 0.101,
          ),
          rotationY: side.inwardRotationY,
          scale: new THREE.Vector3(0.22, 0.13, 0.018),
        });
      }
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
  const forgedSurface = forgedSurfaceTexture(
    bastionHash(familyId, 0, 0, 0, 313),
  );
  disposables.push(forgedSurface);

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
    new THREE.CylinderGeometry(0.5, 0.57, 1, 12, 1),
    forgedMaterial(config.bodyColor, 0.48, forgedSurface),
    crucibleCollars,
    'crucible-collars',
    disposables,
  );
  addInstances(
    familyGroup,
    new THREE.CylinderGeometry(0.5, 0.5, 1, 12, 1),
    forgedMaterial(
      mixedColor(config.bodyColor, '#767074', 0.2),
      0.58,
      forgedSurface,
    ),
    crucibleLids,
    'crucible-lids',
    disposables,
  );
  addInstances(
    familyGroup,
    new THREE.BoxGeometry(1, 1, 1),
    matteMaterial(config.trimColor, 0.46),
    crucibleClamps,
    'crucible-clamps',
    disposables,
  );
  addInstances(
    familyGroup,
    new THREE.BoxGeometry(1, 1, 1),
    matteMaterial('#111216', 0.38),
    crucibleVents,
    'crucible-vents',
    disposables,
  );
  addInstances(
    familyGroup,
    new THREE.BoxGeometry(1, 1, 1),
    heatMaterial(config.heatColor, 1.05),
    crucibleCores,
    'crucible-cores',
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
    heatMaterial(config.heatColor, 1.35),
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
    roundedPrismGeometry(0.2, 0.055),
    matteMaterial(config.bodyColor, 0.34),
    foundryHalls,
    'foundry-halls',
    disposables,
  );
  addInstances(
    familyGroup,
    roundedPrismGeometry(0.16, 0.025),
    matteMaterial(config.trimColor, 0.46),
    hallRoofs,
    'foundry-hall-roofs',
    disposables,
  );
  addInstances(
    familyGroup,
    new THREE.CylinderGeometry(0.48, 0.56, 1, 12, 1),
    matteMaterial(config.bodyColor, 0.38),
    hallStacks,
    'foundry-hall-stacks',
    disposables,
  );
  addInstances(
    familyGroup,
    new THREE.CylinderGeometry(0.58, 0.5, 1, 12, 1),
    matteMaterial(config.accentColor, 0.44),
    hallStackCaps,
    'foundry-hall-stack-caps',
    disposables,
  );
  addInstances(
    familyGroup,
    new THREE.BoxGeometry(1, 1, 1),
    heatMaterial(config.heatColor, 1.35),
    hallHeatChannels,
    'foundry-hall-heat-channels',
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
    new THREE.CylinderGeometry(0.48, 0.56, 1, 12, 1),
    matteMaterial(config.bodyColor, 0.38),
    chimneyStacks,
    'chimney-stacks',
    disposables,
  );
  addInstances(
    familyGroup,
    new THREE.CylinderGeometry(0.58, 0.5, 1, 12, 1),
    forgedMaterial(config.bodyColor, 0.5, forgedSurface),
    chimneyCaps,
    'chimney-caps',
    disposables,
  );
  addInstances(
    familyGroup,
    new THREE.CylinderGeometry(0.52, 0.52, 1, 12, 1),
    matteMaterial(config.accentColor, 0.42),
    chimneyBands,
    'chimney-bands',
    disposables,
  );
  addInstances(
    familyGroup,
    roundedPrismGeometry(0.14, 0.035),
    matteMaterial(config.accentColor, 0.36),
    furnaceHoods,
    'furnace-hoods',
    disposables,
  );
  addInstances(
    familyGroup,
    new THREE.BoxGeometry(1, 1, 1),
    matteMaterial(config.bodyColor, 0.18),
    furnaceRecesses,
    'furnace-recesses',
    disposables,
  );
  addInstances(
    familyGroup,
    new THREE.BoxGeometry(1, 1, 1),
    heatMaterial(config.heatColor, 1.65),
    furnaceCores,
    'furnace-cores',
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
  const emissive =
    kind === 'heat-seams' ||
    kind === 'crucible-cores' ||
    kind === 'furnace-cores' ||
    kind === 'foundry-hall-heat-channels';
  const surfaceTrim =
    kind === 'top-trim' ||
    kind === 'crucible-collars' ||
    kind === 'crucible-lids' ||
    kind === 'crucible-clamps' ||
    kind === 'crucible-vents' ||
    kind === 'chimney-caps' ||
    kind === 'foundry-hall-roofs';
  const inMapModule =
    kind === 'shoulders' ||
    kind === 'round-towers' ||
    kind === 'split-towers' ||
    kind === 'towers' ||
    kind === 'face-ribs' ||
    kind.startsWith('foundry-hall');
  mesh.castShadow =
    !kind.startsWith('apron') &&
    !emissive &&
    !surfaceTrim &&
    !inMapModule;
  mesh.receiveShadow = !emissive;
  if (emissive) mesh.renderOrder = 4;
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

function forgedMaterial(
  color: string,
  metalness: number,
  surface: THREE.Texture,
): THREE.MeshStandardMaterial {
  return new THREE.MeshStandardMaterial({
    color,
    roughness: 0.9,
    roughnessMap: surface,
    bumpMap: surface,
    bumpScale: 0.022,
    metalness,
  });
}

function forgedSurfaceTexture(seed: number): THREE.DataTexture {
  const size = 64;
  const data = new Uint8Array(size * size * 4);
  for (let y = 0; y < size; y++) {
    for (let x = 0; x < size; x++) {
      const hash = bastionHash('forged-surface', x, y, seed, 397);
      const grain = (hash >>> 16) & 31;
      const brushed = Math.abs(((x * 5 + y * 3 + seed) % 29) - 14) < 1
        ? -34
        : 0;
      const value = Math.max(92, Math.min(222, 174 + grain + brushed));
      const offset = (y * size + x) * 4;
      data[offset] = value;
      data[offset + 1] = value;
      data[offset + 2] = value;
      data[offset + 3] = 255;
    }
  }
  const texture = new THREE.DataTexture(
    data,
    size,
    size,
    THREE.RGBAFormat,
  );
  texture.colorSpace = THREE.NoColorSpace;
  texture.wrapS = THREE.RepeatWrapping;
  texture.wrapT = THREE.RepeatWrapping;
  texture.repeat.set(2, 2);
  texture.minFilter = THREE.LinearMipmapLinearFilter;
  texture.magFilter = THREE.LinearFilter;
  texture.generateMipmaps = true;
  texture.needsUpdate = true;
  return texture;
}

function mixedColor(base: string, target: string, amount: number): string {
  return `#${new THREE.Color(base)
    .lerp(new THREE.Color(target), amount)
    .getHexString()}`;
}

function heatMaterial(
  color: string,
  emissiveIntensity: number,
): THREE.MeshStandardMaterial {
  return new THREE.MeshStandardMaterial({
    color,
    emissive: color,
    emissiveIntensity,
    roughness: 0.76,
    metalness: 0.08,
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

function bastionTileKey(x: number, y: number): string {
  return `${x}:${y}`;
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
