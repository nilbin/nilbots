import assert from 'node:assert/strict';
import test from 'node:test';
import * as THREE from 'three';
import {
  arcMotionFrame,
  arcSignatureBodyPose,
  createArcModelMotionRig,
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
    previousMotionX: 0,
    previousMotionY: 0,
    previousSpeed: 0,
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

test('an approved monolithic Meshy body keeps renderer-owned root motion effects', () => {
  const spec = modelSpec('arc-hush');
  assert.ok(spec?.motion);
  assert.equal(spec.nodes, undefined);
  const disposables: { dispose: () => void }[] = [];
  const rig = createArcModelMotionRig(spec, 1, new THREE.Color('#57d9ff'), disposables);
  assert.ok(rig);
  assert.doesNotThrow(() => rig.bind(new THREE.Group()));
  const pose = rig.update({
    time: 8,
    fraction: 0.5,
    facingAngle: 0,
    motionX: 0,
    motionY: 1,
    previousMotionX: 0,
    previousMotionY: 1,
    previousSpeed: 1,
    turnDelta: 0,
    signedTravel: 1,
    braced: false,
    signatureState: 'cooldown',
  }, 1);
  assert.ok(pose.bank > 0);
  assert.ok(rig.wake.visible);
  assert.ok(rig.vents.visible);
  for (const disposable of disposables) disposable.dispose();
});

test('lean and exhaust stay continuous through an active movement segment', () => {
  const continued = { previousMotionY: 1, previousSpeed: 1, motionY: 1 };
  const start = frame('arc-lantern', { ...continued, fraction: 0 });
  const middle = frame('arc-lantern', { ...continued, fraction: 0.5 });
  const end = frame('arc-lantern', { ...continued, fraction: 1 });

  assert.ok(start.bank > 0);
  assert.equal(start.bank, middle.bank);
  assert.equal(middle.bank, end.bank);
  assert.ok(start.wakeStrength > 0);
  assert.equal(start.wakeStrength, middle.wakeStrength);
  assert.equal(middle.wakeStrength, end.wakeStrength);
});

test('exhaust names current displacement while revealed inertia stays inside the hull', () => {
  const corner = frame('arc-lantern', {
    fraction: 0.1,
    previousMotionX: 1,
    previousSpeed: 1,
    motionY: 1,
  });
  assert.ok(Math.abs(corner.wakeYaw + Math.PI / 2) < 1e-9);
  assert.ok(Math.abs(corner.hullLagForward) > 0, 'the hull still carries old momentum');
  assert.ok(Math.abs(corner.hullLagLateral) > 0, 'the hull begins taking the new direction');
});

test('body lean catches a revealed corner instead of snapping to the new axis', () => {
  const entering = frame('arc-lantern', {
    fraction: 0,
    previousMotionX: 1,
    previousSpeed: 1,
    motionY: 1,
  });
  const leaving = frame('arc-lantern', {
    fraction: 1,
    previousMotionX: 1,
    previousSpeed: 1,
    motionY: 1,
  });
  assert.ok(entering.pitch < 0);
  assert.ok(Math.abs(entering.bank) < 1e-9);
  assert.ok(leaving.bank > 0);
  assert.ok(Math.abs(leaving.pitch) < 1e-9);
  assert.equal(entering.wakeYaw, leaving.wakeYaw, 'exhaust is current-direction truth');
});

test('wheel and tread motion scrolls from distance and dips on start or stop', () => {
  const moving = frame('arc-towline', { signedTravel: 1.25, motionX: 1 });
  assert.ok(Math.abs(moving.wheelRotation) > 10);
  const rolling = frame('arc-towline', { previousSpeed: 1, motionX: 1 });
  assert.ok(
    Math.abs(rolling.pitch) < Math.abs(moving.pitch),
    'continued travel does not replay the start dip at each tile',
  );
  const stopping = frame('arc-towline', { previousSpeed: 1 });
  assert.ok(stopping.pitch < 0);
});

test('the inner hull carries revealed momentum into a slow hold without moving the chassis', () => {
  const arriving = frame('arc-lantern', {
    fraction: 1,
    motionX: 1,
  });
  const settling = frame('arc-lantern', {
    fraction: 0,
    previousMotionX: 1,
  });
  const settled = frame('arc-lantern', {
    fraction: 1,
    previousMotionX: 1,
  });

  assert.ok(arriving.hullLagForward < -0.04);
  assert.ok(Math.abs(arriving.hullLagForward - settling.hullLagForward) < 1e-9);
  assert.ok(Math.abs(settled.hullLagForward) < 1e-9);
  assert.ok(Math.abs(arriving.hullLagLateral) < 1e-9);
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

test('signature whole-body acting gives the grammar distinct physical verbs', () => {
  const hook = arcSignatureBodyPose('tractor-hook', 'tell', 0.55);
  const mortar = arcSignatureBodyPose('falling-star', 'tell', 0.55);
  const deploy = arcSignatureBodyPose('sentinel-seed', 'tell', 0.55);
  const exchange = arcSignatureBodyPose('exchange', 'tell', 0.55);
  const kinetic = arcSignatureBodyPose('kinetic-burst', 'tell', 0.55);

  assert.ok(hook.forward < 0 && hook.pitch > 0, 'Towline leans back into cable tension');
  assert.ok(mortar.rise < 0 && mortar.pitch > 0, 'Mortar braces into recoil');
  assert.ok(deploy.forward > 0 && deploy.pitch < 0, 'Nest leans over the deployment tile');
  assert.ok(exchange.rise > 0 && exchange.yaw > 0, 'Switchback lifts and folds');
  assert.ok(kinetic.scale > 1.05 && kinetic.rise < 0, 'Repulsor squats and loads outward pressure');
  assert.notDeepEqual(hook, mortar, 'two casts do not share one generic pulse');
});

test('signature body acting is seek-stable and reduced motion keeps the pose legible', () => {
  const direct = arcSignatureBodyPose('rail-line', 'tell', 0.63);
  const repeated = arcSignatureBodyPose('rail-line', 'tell', 0.63);
  const reduced = arcSignatureBodyPose('rail-line', 'tell', 0.63, true);

  assert.deepEqual(direct, repeated);
  assert.ok(Math.abs(reduced.pitch) > 0, 'reduced motion preserves the authored brace');
  assert.ok(Math.abs(reduced.pitch) < Math.abs(direct.pitch));
  assert.deepEqual(
    arcSignatureBodyPose(null, null, 99),
    { scale: 1, rise: 0, forward: 0, lateral: 0, roll: 0, pitch: 0, yaw: 0 },
  );
});
