import assert from 'node:assert/strict';
import test from 'node:test';
import {
  arcMotionFrame,
  modelSpec,
} from './.harness/harness.entry.js';

function frame(
  id: string,
  overrides: Partial<Parameters<typeof arcMotionFrame>[1]> = {},
) {
  const spec = modelSpec(id);
  assert.ok(spec);
  return arcMotionFrame(spec, {
    time: 8,
    fraction: 0.5,
    facingAngle: 0,
    motionX: 0,
    motionY: 0,
    previousSpeed: 0,
    nextSpeed: 0,
    turnDelta: 0,
    signedTravel: 0,
    braced: false,
    signatureState: 'ready',
    ...overrides,
  });
}

test('Arc bodies express lateral and reverse displacement without changing facing', () => {
  const lateral = frame('arc-lantern', { motionY: 1 });
  assert.ok(lateral.bank > 0);
  assert.ok(Math.abs(lateral.pitch) < 1e-9);
  assert.ok(Math.abs(lateral.wakeYaw + Math.PI / 2) < 1e-9);
  assert.ok(lateral.wakeStrength > 0);

  const reverse = frame('arc-lantern', { motionX: -1 });
  assert.ok(reverse.pitch > 0);
  assert.ok(Math.abs(Math.abs(reverse.wakeYaw) - Math.PI) < 1e-9);
});

test('wheel and tread motion scrolls from distance and dips on start or stop', () => {
  const moving = frame('arc-towline', { signedTravel: 1.25, motionX: 1 });
  assert.ok(Math.abs(moving.wheelRotation) > 10);
  const stopping = frame('arc-towline', { previousSpeed: 1 });
  assert.ok(stopping.pitch < 0);
});

test('hardware follows handling lag, with only swift classes overshooting', () => {
  const swift = frame('arc-lantern', { fraction: 0.1, turnDelta: Math.PI / 2 });
  const deliberate = frame('arc-palisade', { fraction: 0.1, turnDelta: Math.PI / 2 });
  assert.ok(swift.hardwareYaw > 0);
  assert.ok(deliberate.hardwareYaw > swift.hardwareYaw);
  assert.equal(frame('arc-lantern', { fraction: 1, turnDelta: Math.PI / 2 }).hardwareYaw, 0);
  assert.equal(frame('arc-palisade', { fraction: 1, turnDelta: Math.PI / 2 }).hardwareYaw, 0);
});

test('a deliberate hold braces idle motion and cooldown is a diegetic body state', () => {
  const free = frame('arc-nest');
  const braced = frame('arc-nest', { braced: true });
  assert.ok(braced.idleGain < free.idleGain / 2);

  const ready = frame('arc-lantern', { signatureState: 'ready' });
  const cooling = frame('arc-lantern', { signatureState: 'cooldown' });
  assert.ok(cooling.emissiveGain < ready.emissiveGain / 2);
  assert.ok(cooling.ventStrength > 0);
  assert.equal(ready.ventStrength, 0);
});

test('skids counter-steer only while the authoritative turn is underway', () => {
  assert.ok(frame('arc-patchbay', { turnDelta: Math.PI / 2 }).counterSteer < 0);
  assert.ok(
    Math.abs(
      frame('arc-patchbay', { fraction: 1, turnDelta: Math.PI / 2 })
        .counterSteer,
    ) < 1e-9,
  );
});
