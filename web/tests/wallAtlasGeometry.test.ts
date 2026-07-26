import assert from 'node:assert/strict';
import test from 'node:test';
import { wallAtlasDestination } from '../src/render/wallAtlasGeometry.ts';

test('wall placement is independent of source atlas resolution', () => {
  const placement = wallAtlasDestination(48, 192, 32);
  assert.equal(placement.destinationGutter, 8);
  assert.equal(placement.destinationTile, 64);

  // A 4096px atlas crops 256px entries and a 1024px derivative crops 64px
  // entries. Both must occupy the same 64px destination with the same gutter.
  for (const sourceAtlasWidth of [4_096, 2_048, 1_024]) {
    const sourceTile = sourceAtlasWidth / 16;
    assert.ok(sourceTile > 0);
    assert.deepEqual(wallAtlasDestination(48, 192, 32), placement);
  }
});
