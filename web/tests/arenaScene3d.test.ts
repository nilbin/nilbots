import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { join } from 'node:path';
import test from 'node:test';
import * as THREE from 'three';
import type { ReplayModel } from '../src/replayModel.ts';
import { loadReplayJson } from '../src/replayIngress.ts';
import {
  buildArena,
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

function wallsByKind(
  scene: THREE.Scene,
  kind: 'arena-wall-body' | 'arena-wall-caps',
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
    const arena = buildArena(withEmberPresentation(replay));
    const bodies = wallsByKind(arena.scene, 'arena-wall-body');
    const caps = wallsByKind(arena.scene, 'arena-wall-caps');

    assert.deepEqual([...bodies.keys()].sort(), ['cover', 'perimeter']);
    assert.deepEqual([...caps.keys()].sort(), ['cover', 'perimeter']);
    assert.equal(bodies.get('perimeter')!.userData.height, 0.72);
    assert.equal(bodies.get('perimeter')!.userData.cornerRadius, 0.23);
    assert.equal(bodies.get('cover')!.userData.height, 0.46);
    assert.equal(bodies.get('cover')!.userData.cornerRadius, 0.31);

    for (const wall of [...bodies.values(), ...caps.values()])
      assert.equal(wall.userData.openEdgeInset, WALL_OPEN_EDGE_INSET);

    const coverBody = bodies.get('cover')!;
    const coverCaps = caps.get('cover')!;
    const bodyPosition = coverBody.geometry.attributes.position as THREE.BufferAttribute;
    const bodyNormal = coverBody.geometry.attributes.normal as THREE.BufferAttribute;
    const topBounds = positionBounds(
      coverBody.geometry,
      (vertex) =>
        bodyNormal.getY(vertex) > 0.999 &&
        Math.abs(bodyPosition.getY(vertex) - 0.46) < 1e-6,
    );
    const capBounds = positionBounds(coverCaps.geometry);

    // The two exposed sides of Frontline's cover columns prove the cap ends on the
    // extrusion's planar top. Its old bounds ended at 3.14462 / 11.85538: a 0.055-tile
    // unsupported square lip over the chamfer.
    assertNear(capBounds.min.x, 3 + WALL_OPEN_EDGE_INSET + 0.055);
    assertNear(capBounds.max.x, 12 - WALL_OPEN_EDGE_INSET - 0.055);
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
        Math.abs(quad.min.x - (3 + WALL_OPEN_EDGE_INSET + 0.055)) < 1e-6 &&
        Math.abs(quad.max.x - (4 - WALL_OPEN_EDGE_INSET - 0.055)) < 1e-6 &&
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

    arena.dispose();
  } finally {
    globalThis.Image = image;
  }
});
