import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { join } from 'node:path';
import test from 'node:test';
import * as THREE from 'three';
import type { ReplayModel } from '../src/replayModel.ts';
import { loadReplayJson } from '../src/replayIngress.ts';
import {
  buildArena,
  buildOverlays,
  WALL_OPEN_EDGE_INSET,
} from './.harness/harness.entry.js';

const replay = loadReplayJson(
  readFileSync(
    join(import.meta.dirname, 'fixtures', 'frontline-replay-v2.json'),
    'utf8',
  ),
).replay;

function withEmberPresentation(source: ReplayModel): ReplayModel {
  return {
    ...source,
    map: {
      ...source.map,
      presentation: {
        themeId: 'ember-forge',
        boundaryWall: 'perimeter',
        interiorWall: 'cover',
        wallGroups: [],
      },
    },
  };
}

function withWoodlandPresentation(source: ReplayModel): ReplayModel {
  return {
    ...source,
    map: {
      ...source.map,
      presentation: {
        themeId: 'woodland-grove',
        boundaryWall: 'perimeter',
        interiorWall: 'cover',
        wallGroups: [],
      },
    },
  };
}

function wallsByKind(
  scene: THREE.Scene,
  kind:
    | 'arena-wall-body'
    | 'arena-wall-caps'
    | 'arena-wall-upper-profile'
    | 'arena-woodland-groundcover',
): Map<string, THREE.Mesh> {
  const result = new Map<string, THREE.Mesh>();
  scene.traverse((node) => {
    if (
      node instanceof THREE.Mesh &&
      node.userData.kind === kind &&
      typeof node.userData.family === 'string'
    )
      result.set(node.userData.family, node);
  });
  return result;
}

function objectByKind(
  scene: THREE.Scene,
  kind: string,
): THREE.Object3D | null {
  let result: THREE.Object3D | null = null;
  scene.traverse((node) => {
    if (result === null && node.userData.kind === kind) result = node;
  });
  return result;
}

function assertNear(actual: number, expected: number): void {
  assert.ok(
    Math.abs(actual - expected) < 1e-6,
    `expected ${expected}, received ${actual}`,
  );
}

function positionBounds(
  geometry: THREE.BufferGeometry,
  include: (vertex: number) => boolean = () => true,
): THREE.Box3 {
  const position = geometry.attributes.position as THREE.BufferAttribute;
  const bounds = new THREE.Box3();
  for (let vertex = 0; vertex < position.count; vertex++) {
    if (!include(vertex)) continue;
    bounds.expandByPoint(
      new THREE.Vector3(
        position.getX(vertex),
        position.getY(vertex),
        position.getZ(vertex),
      ),
    );
  }
  return bounds;
}

test('Frontline builds the approved Ember perimeter and cover profiles', () => {
  // The Node canvas shim provides a decode-oriented Image without the DOM event surface
  // used by Three textures. A complete one-pixel stand-in keeps this test about geometry
  // while still building the texture-backed topology caps.
  const image = globalThis.Image;
  class GeometryImage {
    complete = true;
    naturalWidth = 1;
    decoding = 'async';
    src = '';
    addEventListener(): void {}
  }
  globalThis.Image = GeometryImage as unknown as typeof Image;
  try {
    const arena = buildArena(withEmberPresentation(replay), {
      shadowMapSize: 1024,
    });
    const shadowLight = arena.scene.children.find(
      (node): node is THREE.DirectionalLight =>
        node instanceof THREE.DirectionalLight && node.castShadow,
    );
    assert.ok(shadowLight);
    assert.equal(shadowLight.shadow.mapSize.width, 1024);
    assert.equal(shadowLight.shadow.mapSize.height, 1024);
    const bodies = wallsByKind(arena.scene, 'arena-wall-body');
    const caps = wallsByKind(arena.scene, 'arena-wall-caps');
    const profiles = wallsByKind(
      arena.scene,
      'arena-wall-upper-profile',
    );

    assert.deepEqual([...bodies.keys()].sort(), ['cover', 'perimeter']);
    assert.deepEqual([...caps.keys()].sort(), ['cover', 'perimeter']);
    assert.deepEqual([...profiles.keys()].sort(), ['cover', 'perimeter']);
    assert.equal(bodies.get('perimeter')!.userData.height, 0.72);
    assert.equal(bodies.get('perimeter')!.userData.cornerRadius, 0.23);
    assert.equal(bodies.get('cover')!.userData.height, 0.46);
    assert.equal(bodies.get('cover')!.userData.cornerRadius, 0.31);
    assert.equal(profiles.get('perimeter')!.userData.height, 0.19);
    assert.equal(profiles.get('perimeter')!.userData.inset, 0.025);
    assert.equal(profiles.get('cover')!.userData.height, 0.14);
    assert.equal(profiles.get('cover')!.userData.inset, 0.04);

    for (const wall of [...bodies.values(), ...caps.values()])
      assert.equal(wall.userData.openEdgeInset, WALL_OPEN_EDGE_INSET);

    const coverProfile = profiles.get('cover')!;
    const coverCaps = caps.get('cover')!;
    const profilePosition =
      coverProfile.geometry.attributes.position as THREE.BufferAttribute;
    const profileNormal =
      coverProfile.geometry.attributes.normal as THREE.BufferAttribute;
    const topBounds = positionBounds(
      coverProfile.geometry,
      (vertex) =>
        profileNormal.getY(vertex) > 0.999 &&
        Math.abs(profilePosition.getY(vertex) - 0.46) < 1e-6,
    );
    const capBounds = positionBounds(coverCaps.geometry);

    // The two exposed sides of Frontline's cover columns prove the cap ends on the
    // extrusion's planar top. Its old bounds ended at 3.14462 / 11.85538: a 0.055-tile
    // unsupported square lip over the chamfer.
    assertNear(
      capBounds.min.x,
      3 + WALL_OPEN_EDGE_INSET + 0.04 + 0.03,
    );
    assertNear(
      capBounds.max.x,
      12 - WALL_OPEN_EDGE_INSET - 0.04 - 0.03,
    );
    assertNear(capBounds.min.x, topBounds.min.x);
    assertNear(capBounds.max.x, topBounds.max.x);
    assertNear(capBounds.min.z, topBounds.min.z);
    assertNear(capBounds.max.z, topBounds.max.z);

    // Cover tile (3,1) has open west/east sides, a different-family wall to the north,
    // and a same-family join south. Its mask is 16, so it occupies atlas column 0, row 1.
    // Find its four built vertices and assert the manifest's 32/(192+64) cell gutter is
    // mapped to every exposed rim while the connected side retains the full gutter.
    const capPosition = coverCaps.geometry.attributes.position as THREE.BufferAttribute;
    const capUv = coverCaps.geometry.attributes.uv as THREE.BufferAttribute;
    let targetOffset = -1;
    for (let offset = 0; offset < capPosition.count; offset += 4) {
      const quad = new THREE.Box3();
      for (let vertex = offset; vertex < offset + 4; vertex++)
        quad.expandByPoint(
          new THREE.Vector3(
            capPosition.getX(vertex),
            capPosition.getY(vertex),
            capPosition.getZ(vertex),
          ),
        );
      if (
        Math.abs(
          quad.min.x -
            (3 + WALL_OPEN_EDGE_INSET + 0.04 + 0.03),
        ) < 1e-6 &&
        Math.abs(
          quad.max.x -
            (4 - WALL_OPEN_EDGE_INSET - 0.04 - 0.03),
        ) < 1e-6 &&
        Math.abs(quad.min.z - 1) < 1e-6
      ) {
        targetOffset = offset;
        break;
      }
    }
    assert.notEqual(targetOffset, -1, 'expected the (3,1) cover cap quad');

    const localUs: number[] = [];
    const localVs: number[] = [];
    const atlasColumns = 16;
    const atlasRowBottom = 1 - (1 + 1) / atlasColumns;
    for (let vertex = targetOffset; vertex < targetOffset + 4; vertex++) {
      localUs.push(capUv.getX(vertex) * atlasColumns);
      localVs.push((capUv.getY(vertex) - atlasRowBottom) * atlasColumns);
    }
    assertNear(Math.min(...localUs), 0.125);
    assertNear(Math.max(...localUs), 0.875);
    assertNear(Math.min(...localVs), 0);
    assertNear(Math.max(...localVs), 0.875);

    const details = objectByKind(arena.scene, 'arena-wall-details');
    assert.ok(details);
    assert.ok(details.userData.detailCount > 0);
    assert.ok(
      objectByKind(
        arena.scene,
        'arena-wall-vent-emission',
      ),
    );

    arena.dispose();
  } finally {
    globalThis.Image = image;
  }
});

test('Woodland roots remain authoritative while organic foliage overhangs', () => {
  const image = globalThis.Image;
  class GeometryImage {
    complete = true;
    naturalWidth = 1;
    decoding = 'async';
    src = '';
    addEventListener(): void {}
  }
  globalThis.Image = GeometryImage as unknown as typeof Image;
  try {
    const source = withWoodlandPresentation(replay);
    const arena = buildArena(source, { shadowMapSize: 1024 });
    const woodland = objectByKind(
      arena.scene,
      'arena-woodland-scenery',
    );
    assert.ok(woodland);
    assert.ok(woodland.userData.treeCount > 0);

    const familyGroups = new Map<string, THREE.Object3D>();
    const canopies: THREE.InstancedMesh[] = [];
    const trunks: THREE.InstancedMesh[] = [];
    const conifers: THREE.InstancedMesh[] = [];
    const transitionFringes: THREE.InstancedMesh[] = [];
    arena.scene.traverse((node) => {
      if (
        node.userData.kind === 'arena-woodland-family' &&
        typeof node.userData.family === 'string'
      )
        familyGroups.set(node.userData.family, node);
      if (
        node instanceof THREE.InstancedMesh &&
        node.userData.kind === 'arena-woodland-canopy-lobes'
      )
        canopies.push(node);
      if (
        node instanceof THREE.InstancedMesh &&
        node.userData.kind === 'arena-woodland-trunks'
      )
        trunks.push(node);
      if (
        node instanceof THREE.InstancedMesh &&
        node.userData.kind === 'arena-woodland-conifer-tiers'
      )
        conifers.push(node);
      if (
        node instanceof THREE.InstancedMesh &&
        node.userData.kind === 'arena-woodland-transition-fringe'
      )
        transitionFringes.push(node);
    });
    assert.ok(Math.abs(arena.cameraYaw - (22 * Math.PI) / 180) < 1e-9);
    assert.ok(Math.abs(arena.cameraPitch - (52 * Math.PI) / 180) < 1e-9);
    assert.equal(arena.camera.fov, 26);
    assert.ok(arena.scene.fog instanceof THREE.Fog);
    assert.ok(
      arena.scene.fog.far > source.map.width * 3,
      'the narrow MOBA lens must move the fog plane with the camera',
    );
    assert.deepEqual(
      [...wallsByKind(arena.scene, 'arena-wall-body').keys()],
      [],
      'living cover must not reveal the tile grid as a raised substrate',
    );
    assert.deepEqual(
      [...wallsByKind(arena.scene, 'arena-wall-caps').keys()],
      [],
      'living cover must not carry rectangular topology caps',
    );
    assert.deepEqual(
      [...wallsByKind(arena.scene, 'arena-woodland-groundcover').keys()].sort(),
      ['cover', 'perimeter'],
      'living blockers need one continuous, flush understory field per family',
    );
    for (const groundcover of wallsByKind(
      arena.scene,
      'arena-woodland-groundcover',
    ).values())
      assert.equal(
        groundcover.userData.openEdgeInset,
        0,
        'organic groundcover must not expose a per-tile clearance gutter',
      );
    assert.deepEqual([...familyGroups.keys()].sort(), ['cover', 'perimeter']);
    assert.ok(
      familyGroups.get('perimeter')!.userData.maximumHeight >
        familyGroups.get('cover')!.userData.maximumHeight,
    );
    assert.ok(
      familyGroups.get('perimeter')!.userData.maximumRootSpread +
          familyGroups.get('perimeter')!.userData.maximumCanopyRadius >
        0.5 - WALL_OPEN_EDGE_INSET,
      'organic crowns intentionally overhang the old per-cell padding',
    );
    assert.ok(familyGroups.get('perimeter')!.userData.borderTreeCount > 40);
    assert.equal(
      familyGroups.get('perimeter')!.userData.boundaryShape,
      'scattered-rounded-band',
    );
    assert.equal(familyGroups.get('cover')!.userData.borderTreeCount, 0);
    assert.equal(familyGroups.get('cover')!.userData.boundaryShape, 'none');
    assert.ok(
      familyGroups.get('perimeter')!.userData.transitionFringeCount > 40,
    );
    assert.equal(familyGroups.get('cover')!.userData.transitionFringeCount, 0);
    assert.ok(conifers.some((mesh) => mesh.count > 0));
    assert.equal(transitionFringes.length, 1);

    const matrix = new THREE.Matrix4();
    const position = new THREE.Vector3();
    const rotation = new THREE.Quaternion();
    const scale = new THREE.Vector3();
    const trunkHeights = new Set<string>();
    const trunkWidths = new Set<string>();
    let outsideBorderRoots = 0;
    let roundedCornerRoots = 0;
    const outsideDepths = new Set<string>();
    for (const trunk of trunks) {
      for (let index = 0; index < trunk.count; index++) {
        trunk.getMatrixAt(index, matrix);
        matrix.decompose(position, rotation, scale);
        trunkHeights.add(scale.y.toFixed(4));
        trunkWidths.add(scale.x.toFixed(4));
        if (
          position.x < 0 ||
          position.z < 0 ||
          position.x >= source.map.width ||
          position.z >= source.map.height
        ) {
          outsideBorderRoots++;
          const outsideX = Math.max(
            0,
            -position.x,
            position.x - source.map.width,
          );
          const outsideZ = Math.max(
            0,
            -position.z,
            position.z - source.map.height,
          );
          outsideDepths.add(Math.hypot(outsideX, outsideZ).toFixed(2));
          if (outsideX > 0 && outsideZ > 0) roundedCornerRoots++;
        } else {
          assert.equal(
            source.map.tileRows[Math.floor(position.z)]?.[
              Math.floor(position.x)
            ],
            '#',
            'in-map tree roots remain authoritative blockers',
          );
        }
      }
    }
    assert.ok(
      trunkHeights.size > 8,
      'tree cover must alternate trunk heights instead of stamping one tree',
    );
    assert.ok(
      trunkWidths.size > 8,
      'tree cover must alternate trunk widths instead of stamping one tree',
    );
    assert.ok(outsideBorderRoots > 40, 'the perimeter must grow outward in depth');
    assert.ok(
      outsideDepths.size > 30,
      'the outer forest must occupy a scattered band rather than aligned rows',
    );
    assert.ok(
      roundedCornerRoots > 8,
      'the outer forest must round and soften all four map corners',
    );
    for (const fringe of transitionFringes) {
      for (let index = 0; index < fringe.count; index++) {
        fringe.getMatrixAt(index, matrix);
        matrix.decompose(position, rotation, scale);
        assert.notEqual(
          source.map.tileRows[Math.floor(position.z)]?.[
            Math.floor(position.x)
          ],
          '#',
          'the flat transition fringe should soften the floor side of the seam',
        );
        assert.ok(scale.y <= 0.03, 'transition foliage must stay visibly flat');
      }
    }
    let overhangingCrowns = 0;
    for (const canopy of canopies) {
      for (let index = 0; index < canopy.count; index++) {
        canopy.getMatrixAt(index, matrix);
        matrix.decompose(position, rotation, scale);
        if (
          position.x < 0 ||
          position.z < 0 ||
          position.x >= source.map.width ||
          position.z >= source.map.height
        )
          continue;
        const tileX = Math.floor(position.x);
        const tileY = Math.floor(position.z);
        const centreDistance = Math.hypot(
          position.x - (tileX + 0.5),
          position.z - (tileY + 0.5),
        );
        if (centreDistance + Math.max(scale.x, scale.z) > 0.5)
          overhangingCrowns++;
      }
    }
    assert.ok(
      overhangingCrowns > 8,
      'crowns should cross cell edges instead of exposing tile-shaped padding',
    );

    const overlays = buildOverlays(source, undefined, arena.scene);
    const canopyMaterial = canopies[0]!.material;
    assert.ok(!Array.isArray(canopyMaterial));
    const fog = canopyMaterial.userData.arenaVisionFog as {
      enabled: { value: number };
      texture: THREE.DataTexture;
    };
    assert.ok(fog, 'living wall materials receive the team-vision mask');
    const selected = source.units.find((unit) => unit.teamId === 0)!;
    overlays.update(0, selected.unitKey, true);
    assert.equal(fog.enabled.value, 1);
    const mask = fog.texture.image.data as Uint8Array;
    assert.ok(mask.some((value, index) => index % 4 === 3 && value === 0));
    assert.ok(
      mask.some((value, index) => index % 4 === 3 && value > 0 && value < 200),
      'unseen terrain is subdued rather than blacked out',
    );

    const shader = {
      uniforms: {},
      vertexShader: 'void main() {\n#include <project_vertex>\n}',
      fragmentShader: 'void main() {\n#include <opaque_fragment>\n}',
    };
    canopyMaterial.onBeforeCompile(shader as never, {} as never);
    assert.match(shader.vertexShader, /instanceMatrix/);
    assert.match(shader.fragmentShader, /arenaVisionFogMap/);
    assert.ok('arenaVisionFogEnabled' in shader.uniforms);
    overlays.update(0, null, false);
    assert.equal(fog.enabled.value, 0, 'turning vision off restores all terrain');
    overlays.dispose();
    arena.dispose();
  } finally {
    globalThis.Image = image;
  }
});
