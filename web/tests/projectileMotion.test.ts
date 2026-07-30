import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { join } from 'node:path';
import test from 'node:test';
import type { ReplayModel } from '../src/replayModel.ts';
import {
  buildActors,
  loadReplayJson,
  presentationProjectileLook,
  projectileFlightMotionAt,
  projectileMotionPhase,
} from './.harness/harness.entry.js';

test('class projectiles opt into distinct model-safe flight cues', () => {
  assert.equal(
    presentationProjectileLook('trident-spark').flightCue,
    'axial-spin',
  );
  assert.equal(
    presentationProjectileLook('rebound-diamond').flightCue,
    'flat-spin',
  );
  assert.equal(
    presentationProjectileLook('lattice-rivet').flightCue,
    'tumble',
  );
  assert.equal(
    presentationProjectileLook('pulse-bolt').flightCue,
    null,
    'legacy looks retain the historical renderer treatment',
  );
});

test('flight motion is deterministic and follows +X-forward, +Y-up axes', () => {
  assert.equal(
    projectileMotionPhase('9007199254740993'),
    projectileMotionPhase('9007199254740993'),
  );

  const axial = projectileFlightMotionAt(
    'axial-spin',
    4.25,
    'bolt-axial',
  );
  const axialLater = projectileFlightMotionAt(
    'axial-spin',
    5.25,
    'bolt-axial',
  );
  assert.ok(Math.abs(axialLater.rotationX - axial.rotationX - 5.4) < 1e-9);
  assert.equal(axial.rotationY, 0);
  assert.equal(axial.canvasRotation, 0);
  assert.ok(Math.abs(axial.canvasScaleY) >= 0.38);

  const flat = projectileFlightMotionAt(
    'flat-spin',
    4.25,
    'bolt-flat',
  );
  const flatLater = projectileFlightMotionAt(
    'flat-spin',
    5.25,
    'bolt-flat',
  );
  assert.ok(Math.abs(flatLater.rotationY - flat.rotationY - 3.2) < 1e-9);
  assert.equal(flat.rotationX, 0);
  assert.equal(flat.canvasRotation, flat.rotationY);

  const tumble = projectileFlightMotionAt(
    'tumble',
    4.25,
    'bolt-tumble',
  );
  const tumbleLater = projectileFlightMotionAt(
    'tumble',
    5.25,
    'bolt-tumble',
  );
  assert.ok(
    Math.abs(tumbleLater.rotationX - tumble.rotationX - 1.55) < 1e-9,
  );
  assert.ok(
    Math.abs(tumbleLater.rotationZ - tumble.rotationZ - 1.05) < 1e-9,
  );
  assert.ok(Math.abs(tumble.canvasScaleY) >= 0.48);

  assert.deepEqual(
    projectileFlightMotionAt('stable', 8.5, 'bolt-stable'),
    {
      rotationX: 0,
      rotationY: 0,
      rotationZ: 0,
      canvasRotation: 0,
      canvasScaleX: 1,
      canvasScaleY: 1,
    },
  );
});

test('WebGL applies each cue to the shared SVG-or-GLB head below heading', () => {
  const source = loadReplayJson(
    readFileSync(
      join(import.meta.dirname, 'fixtures', 'frontline-replay-v2.json'),
      'utf8',
    ),
  ).replay;
  const traversal = source.ticks[10]!.projectileTraversals[0]!;
  const samples = [
    ['striker-prime', 'trident-spark', 'axial-spin'],
    ['bulwark-prime', 'rebound-diamond', 'flat-spin'],
    ['fabricator-prime', 'lattice-rivet', 'tumble'],
  ] as const;

  for (const [formId, lookId, cue] of samples) {
    const replay = structuredClone(source) as ReplayModel;
    const unit = replay.units.find(
      (candidate) =>
        candidate.unitKey === traversal.ownerActor.unitKey,
    );
    assert.ok(unit);
    unit.initialFormId = formId;

    const actors = buildActors(replay);
    actors.update(10.5, null, false);
    const rig = actors.group.children.find(
      (child) =>
        child.visible &&
        child.userData.cue === 'projectile-rig' &&
        child.userData.projectileLookId === lookId,
    );
    assert.ok(rig, `${lookId} rig`);
    const head = rig.children.find(
      (child) => child.userData.cue === 'projectile-head',
    );
    assert.ok(head, `${lookId} head`);
    assert.equal(head.userData.flightCue, cue);

    const expected = projectileFlightMotionAt(
      cue,
      10.5,
      traversal.projectileId,
    );
    assert.ok(Math.abs(head.rotation.x - expected.rotationX) < 1e-9);
    assert.ok(Math.abs(head.rotation.y - expected.rotationY) < 1e-9);
    assert.ok(Math.abs(head.rotation.z - expected.rotationZ) < 1e-9);
    actors.dispose();
  }
});
