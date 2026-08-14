import assert from 'node:assert/strict';
import test from 'node:test';
import {
  healthBarState,
  HEALTH_BAR_COLORS,
} from './.harness/harness.entry.js';

test('health is a relative fill independent of the maximum', () => {
  assert.equal(healthBarState(15, 30).fraction, 0.5);
  assert.equal(healthBarState(25, 50).fraction, 0.5);
  assert.equal(healthBarState(50, 50).fraction, 1);
  assert.equal(healthBarState(0, 100).fraction, 0);
});

test('health tone moves from green through amber to red', () => {
  assert.deepEqual(
    [healthBarState(30, 30).tone, healthBarState(15, 30).tone, healthBarState(5, 30).tone],
    ['healthy', 'wounded', 'critical'],
  );
  assert.equal(healthBarState(30, 30).color, HEALTH_BAR_COLORS.healthy);
  assert.equal(healthBarState(15, 30).color, HEALTH_BAR_COLORS.wounded);
  assert.equal(healthBarState(5, 30).color, HEALTH_BAR_COLORS.critical);
});

test('health clamps invalid replay edges without changing its fixed scale', () => {
  assert.deepEqual(
    healthBarState(150, 100),
    {
      current: 100,
      maximum: 100,
      fraction: 1,
      tone: 'healthy',
      color: HEALTH_BAR_COLORS.healthy,
    },
  );
  assert.equal(healthBarState(-1, 0).fraction, 0);
});
