import assert from 'node:assert/strict';
import test from 'node:test';
import { wallCapGeometry } from '../src/render3d/wallCapGeometry.ts';

const rectangle = {
  left: -0.3,
  right: 0.3,
  north: -0.25,
  south: 0.25,
  cornerRadius: 0.2,
};

const hasPoint = (points: number[][], x: number, z: number) =>
  points.some(
    ([candidateX, candidateZ]) =>
      Math.abs(candidateX - x) < 1e-6 && Math.abs(candidateZ - z) < 1e-6,
  );

test('wall cap removes unsupported square lips at exposed corners', () => {
  const geometry = wallCapGeometry({
    ...rectangle,
    corners: {
      northWest: true,
      southWest: true,
      southEast: true,
      northEast: true,
    },
  });
  const position = geometry.attributes.position;
  const points = Array.from({ length: position.count }, (_, index) => [
    position.getX(index),
    position.getZ(index),
  ]);

  assert.equal(hasPoint(points, rectangle.left, rectangle.north), false);
  assert.equal(hasPoint(points, rectangle.right, rectangle.north), false);
  assert.equal(hasPoint(points, rectangle.left, rectangle.south), false);
  assert.equal(hasPoint(points, rectangle.right, rectangle.south), false);
  assert.equal(geometry.getIndex()?.count, 48);
});

test('wall cap keeps connected and internal corners square', () => {
  const geometry = wallCapGeometry({
    ...rectangle,
    corners: {
      northWest: false,
      southWest: false,
      southEast: false,
      northEast: false,
    },
  });
  const position = geometry.attributes.position;
  const points = Array.from({ length: position.count }, (_, index) => [
    position.getX(index),
    position.getZ(index),
  ]);

  assert.equal(hasPoint(points, rectangle.left, rectangle.north), true);
  assert.equal(hasPoint(points, rectangle.right, rectangle.south), true);
  assert.equal(position.count, 5);
  assert.equal(geometry.getIndex()?.count, 12);
});

test('wall cap preserves atlas crop coordinates and upward winding', () => {
  const geometry = wallCapGeometry({
    ...rectangle,
    corners: {
      northWest: true,
      southWest: false,
      southEast: false,
      northEast: false,
    },
  });
  const position = geometry.attributes.position;
  const normal = geometry.attributes.normal;
  const uv = geometry.attributes.uv;
  for (let index = 0; index < position.count; index += 1) {
    assert.equal(normal.getY(index), 1);
    assert.ok(
      Math.abs(
        uv.getX(index) -
          (position.getX(index) - rectangle.left) /
            (rectangle.right - rectangle.left),
      ) < 1e-6,
    );
    assert.ok(
      Math.abs(
        uv.getY(index) -
          (rectangle.south - position.getZ(index)) /
            (rectangle.south - rectangle.north),
      ) < 1e-6,
    );
  }

  const indices = geometry.getIndex();
  assert.ok(indices);
  const ax = position.getX(indices.getX(0));
  const az = position.getZ(indices.getX(0));
  const bx = position.getX(indices.getX(1));
  const bz = position.getZ(indices.getX(1));
  const cx = position.getX(indices.getX(2));
  const cz = position.getZ(indices.getX(2));
  const crossY = (bz - az) * (cx - ax) - (bx - ax) * (cz - az);
  assert.ok(crossY > 0);
});
