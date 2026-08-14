import assert from 'node:assert/strict';
import test from 'node:test';
import {
  healthSegments,
  MAX_HEALTH_SEGMENTS,
} from './.harness/harness.entry.js';

test('historical hulls retain one exact segment per health point', () => {
  assert.deepEqual(healthSegments(3, 5), { count: 5, filled: 3 });
  assert.deepEqual(healthSegments(0, 5), { count: 5, filled: 0 });
});

test('combat-scale and vitality hulls stay proportional and bounded', () => {
  assert.equal(MAX_HEALTH_SEGMENTS, 10);
  assert.deepEqual(healthSegments(30, 30), { count: 10, filled: 10 });
  assert.deepEqual(healthSegments(25, 50), { count: 10, filled: 5 });
  assert.deepEqual(healthSegments(1, 100), { count: 10, filled: 1 });
  assert.deepEqual(healthSegments(0, 100), { count: 10, filled: 0 });
});
