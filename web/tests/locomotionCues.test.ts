import assert from 'node:assert/strict';
import test from 'node:test';
import {
  botLocomotionAt,
  clampLocomotionRoll,
  projectileFlightMotionAt,
} from './.harness/harness.entry.js';

test('class hover profiles remain distinct, restrained and replay-time pure', () => {
  const time = 7.25;
  const phase = 1.125;
  const striker = botLocomotionAt('low-hover', time, phase, false);
  const bulwark = botLocomotionAt('heavy-hover', time, phase, false);
  const fabricator = botLocomotionAt('calm-hover', time, phase, false);

  assert.deepEqual(
    botLocomotionAt('heavy-hover', time, phase, false),
    bulwark,
  );
  assert.ok(bulwark.hover >= 0.041 && bulwark.hover <= 0.049);
  assert.ok(fabricator.hover >= 0.058 && fabricator.hover <= 0.072);
  const means = (['low-hover', 'calm-hover', 'heavy-hover'] as const).map(
    (cue) =>
      Array.from(
        { length: 256 },
        (_, index) =>
          botLocomotionAt(cue, index / 8, phase, false).hover,
      ).reduce((sum, hover) => sum + hover, 0) / 256,
  );
  assert.ok(means[0] > means[1]);
  assert.ok(means[1] > means[2]);
  assert.equal(striker.settlesWhenPlanted, false);
  assert.equal(bulwark.settlesWhenPlanted, true);
  assert.equal(fabricator.settlesWhenPlanted, false);

  assert.equal(
    botLocomotionAt('heavy-hover', time, phase, true).lateralScale,
    0,
  );
  assert.equal(
    botLocomotionAt('calm-hover', time, phase, true).lateralScale,
    0,
  );
  assert.equal(
    botLocomotionAt('low-hover', time, phase, true).lateralScale,
    1,
    'the previously shipped Striker response stays unchanged',
  );
  assert.ok(
    Math.abs(clampLocomotionRoll(1, bulwark)) <= Math.PI / 60,
    'Bulwark bank stays at or below three degrees',
  );
});

test('projectile cues move only from time and exact stable identity', () => {
  const identity = 'frontline:1:unit:0:life:2:projectile:9007199254740993';
  for (const cue of ['axial-spin', 'flat-spin', 'tumble'] as const) {
    const sample = projectileFlightMotionAt(cue, 4.75, identity);
    assert.deepEqual(
      projectileFlightMotionAt(cue, 4.75, identity),
      sample,
    );
    assert.ok(sample.canvasScaleX >= 0.66);
    assert.ok(sample.canvasScaleY >= 0.58);
  }

  assert.notDeepEqual(
    projectileFlightMotionAt('tumble', 4.75, identity),
    projectileFlightMotionAt('tumble', 4.75, `${identity}:other`),
  );
  assert.deepEqual(projectileFlightMotionAt(null, 4.75, identity), {
    rotationX: 0,
    rotationY: 0,
    rotationZ: 0,
    canvasRotation: 0,
    canvasScaleX: 1,
    canvasScaleY: 1,
  });
});

test('each projectile cue keeps its promised axis semantics', () => {
  const axial = projectileFlightMotionAt('axial-spin', 2, 'axial');
  assert.notEqual(axial.rotationX, 0);
  assert.equal(axial.rotationY, 0);
  assert.equal(axial.rotationZ, 0);
  assert.equal(axial.canvasRotation, 0);

  const flat = projectileFlightMotionAt('flat-spin', 2, 'flat');
  assert.equal(flat.rotationX, 0);
  assert.notEqual(flat.rotationY, 0);
  assert.equal(flat.rotationZ, 0);
  assert.equal(flat.canvasRotation, flat.rotationY);

  const tumble = projectileFlightMotionAt('tumble', 2, 'tumble');
  assert.notEqual(tumble.rotationX, 0);
  assert.notEqual(tumble.rotationY, 0);
  assert.notEqual(tumble.rotationZ, 0);
});
