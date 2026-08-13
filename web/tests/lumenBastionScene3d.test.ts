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
} from './.harness/harness.entry.js';

const replay = loadReplayJson(
  readFileSync(
    join(import.meta.dirname, 'fixtures', 'frontline-replay-v2.json'),
    'utf8',
  ),
).replay;

function withLumenPresentation(source: ReplayModel): ReplayModel {
  return {
    ...source,
    map: {
      ...source.map,
      presentation: {
        themeId: 'lumen-bastion',
        boundaryWall: 'perimeter',
        interiorWall: 'cover',
        wallGroups: [],
      },
    },
  };
}

function wallsByKind(
  scene: THREE.Scene,
  kind: 'arena-wall-body' | 'arena-wall-upper-profile',
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

function objectByKind(scene: THREE.Scene, kind: string): THREE.Object3D | null {
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

test('optional Lumen package builds a high-key bastion with sparse working lights', () => {
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
    const source = withLumenPresentation(replay);
    const arena = buildArena(source, { shadowMapSize: 1024 });
    assert.equal((arena.scene.background as THREE.Color).getHexString(), 'dfe4e9');
    assert.equal(arena.camera.fov, 28);
    assertNear(arena.cameraYaw, (12 * Math.PI) / 180);
    assertNear(arena.cameraPitch, (52 * Math.PI) / 180);

    const bodies = wallsByKind(arena.scene, 'arena-wall-body');
    const profiles = wallsByKind(arena.scene, 'arena-wall-upper-profile');
    assert.equal(bodies.get('perimeter')!.userData.height, 0.8);
    assert.equal(bodies.get('cover')!.userData.height, 0.54);
    assert.equal(profiles.get('perimeter')!.userData.height, 0.23);
    assert.equal(profiles.get('cover')!.userData.height, 0.16);

    const lights = arena.scene.children.filter(
      (node): node is THREE.Light => node instanceof THREE.Light,
    );
    assert.deepEqual(
      lights.map((light) => light.intensity).sort((left, right) => left - right),
      [0.9, 1.72, 3.2],
    );
    assert.ok(objectByKind(arena.scene, 'arena-wall-vent-emission'));
    assert.ok(objectByKind(arena.scene, 'arena-wall-bastion-crucible-cores'));
    assert.ok(objectByKind(arena.scene, 'arena-wall-bastion-heat-seams'));
    assert.equal(
      objectByKind(arena.scene, 'arena-wall-bastion-chimney-stacks'),
      null,
    );

    const families = new Map<string, THREE.Object3D>();
    arena.scene.traverse((node) => {
      if (
        node.userData.kind === 'arena-wall-bastion-family' &&
        typeof node.userData.family === 'string'
      )
        families.set(node.userData.family, node);
    });
    assert.equal(families.get('perimeter')!.userData.boundaryDepth, 1.6);
    assert.equal(families.get('cover')!.userData.boundaryDepth, 0);

    const overlays = buildOverlays(source, undefined, arena.scene);
    const raised = objectByKind(
      arena.scene,
      'arena-wall-bastion-shoulders',
    ) as THREE.InstancedMesh;
    assert.ok(raised);
    assert.ok(!Array.isArray(raised.material));
    const fog = raised.material.userData.arenaVisionFog as {
      darkness: { value: number };
      visibleLift: { value: number };
    };
    assert.ok(fog);
    assert.equal(fog.darkness.value, 0.42);
    assert.equal(fog.visibleLift.value, 0.08);
    overlays.dispose();
    arena.dispose();
  } finally {
    globalThis.Image = image;
  }
});
