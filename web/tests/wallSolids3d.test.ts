import assert from 'node:assert/strict';
import test from 'node:test';
import * as THREE from 'three';
import { wallShapes } from '../src/render3d/wallSolids.ts';

const OPEN_EDGE_INSET = 0.14462;
const CHAMFER = 0.055;
const SOURCE_INSET = OPEN_EDGE_INSET + CHAMFER;

function bounds(
  tiles: readonly { x: number; y: number }[],
  isWall?: (x: number, y: number) => boolean,
): THREE.Box3 {
  const geometry = new THREE.ExtrudeGeometry(
    wallShapes(tiles, {
      cornerRadius: 0.23,
      openEdgeInset: SOURCE_INSET,
      isWall,
    }),
    {
      depth: 0.72 - CHAMFER,
      bevelEnabled: true,
      bevelThickness: CHAMFER,
      bevelSize: CHAMFER,
      bevelSegments: 2,
      curveSegments: 3,
    },
  );
  geometry.rotateX(-Math.PI / 2);
  geometry.computeBoundingBox();
  assert.ok(geometry.boundingBox);
  const result = geometry.boundingBox.clone();
  geometry.dispose();
  return result;
}

function assertNear(actual: number, expected: number): void {
  assert.ok(
    Math.abs(actual - expected) < 1e-6,
    `expected ${expected}, received ${actual}`,
  );
}

test('an isolated wall keeps the live Striker clearance after bevel expansion', () => {
  const box = bounds([{ x: 0, y: 0 }]);
  assertNear(box.min.x, OPEN_EDGE_INSET);
  assertNear(box.max.x, 1 - OPEN_EDGE_INSET);
  assertNear(box.min.z, OPEN_EDGE_INSET);
  assertNear(box.max.z, 1 - OPEN_EDGE_INSET);
});

test('same-family neighbours merge without reopening their shared edge', () => {
  const shapes = wallShapes(
    [
      { x: 0, y: 0 },
      { x: 1, y: 0 },
    ],
    {
      cornerRadius: 0.23,
      openEdgeInset: SOURCE_INSET,
    },
  );
  assert.equal(shapes.length, 1);

  const box = bounds([
    { x: 0, y: 0 },
    { x: 1, y: 0 },
  ]);
  assertNear(box.min.x, OPEN_EDGE_INSET);
  assertNear(box.max.x, 2 - OPEN_EDGE_INSET);
});

test('different wall families meet at the authoritative grid boundary', () => {
  const box = bounds(
    [{ x: 0, y: 0 }],
    (x, y) =>
      (x === 0 && y === 0) ||
      (x === 1 && y === 0),
  );
  assertNear(box.min.x, OPEN_EDGE_INSET);
  assertNear(
    box.max.x,
    1 + CHAMFER,
  );
});
