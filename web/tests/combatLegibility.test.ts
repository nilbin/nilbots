import assert from 'node:assert/strict';
import test from 'node:test';
import {
  combatLegibility,
  contrastRatio,
  dualToneContrastFloor,
  healthIndicatorMetrics,
  projectileStrokeWidths,
} from '../src/render/combatLegibility.ts';

test('dark and light keylines guarantee the contrast floor', () => {
  assert.ok(
    dualToneContrastFloor() >= combatLegibility.minimumDualToneContrast,
  );
});

test('health lines contrast strongly with their displaced shadows', () => {
  assert.ok(
    contrastRatio(combatLegibility.light, combatLegibility.dark) >= 7,
  );
});

test('projectile layers remain ordered at every supported tile size', () => {
  for (const tile of [16, 24, 32, 48, 72, 96]) {
    const widths = projectileStrokeWidths(tile);
    assert.ok(widths.glow > widths.shadow);
    assert.ok(widths.shadow > widths.energy);
    assert.ok(widths.energy > widths.core);
  }
});

test('health indicator retains distinct capacity and fill weights', () => {
  for (const tile of [16, 24, 32, 48, 72, 96]) {
    const metrics = healthIndicatorMetrics(tile);
    assert.ok(metrics.width >= 12);
    assert.ok(metrics.width <= tile);
    assert.ok(metrics.shadowOffset >= 1);
    assert.ok(metrics.trackShadow > metrics.track);
    assert.ok(metrics.fillShadow > metrics.trackShadow);
    assert.ok(metrics.fill > metrics.track);
  }
});
