import assert from 'node:assert/strict';
import test from 'node:test';
import {
  lookModelSource,
  modelSpec,
  presentationBotLook,
  presentationProjectileLook,
} from './.harness/harness.entry.js';

test('the three approved mobile bodies and default projectiles resolve to authored GLBs', () => {
  for (const id of ['trident-wasp', 'aegis-tortoise', 'lattice-loom'])
    assert.deepEqual(modelSpec(id), {
      version: 1,
      id,
      file: 'model.glb',
      kind: 'bot',
      part: 'whole',
      facing: '+x',
      up: '+y',
      ...(id === 'aegis-tortoise'
        ? {
            semanticAnimation: {
              version: 1,
              stanceDeployClip: 'Shell_Deploy',
              emplacedDeployClip: 'Tower_Deploy',
              mobileRecoilClip: 'Mobile_Recoil',
              stanceRecoilClip: 'Shell_Recoil',
              emplacedRecoilClip: 'Tower_Recoil',
            },
          }
        : {}),
    });

  for (const id of [
    'trident-spark',
    'rebound-diamond',
    'lattice-rivet',
  ])
    assert.deepEqual(modelSpec(id), {
      version: 1,
      id,
      file: 'model.glb',
      kind: 'projectile',
      part: 'whole',
      facing: '+x',
      up: '+y',
    });

  for (const id of [
    'trident-wasp-volley',
    'aegis-tortoise-shell',
    'aegis-tortoise-turret',
  ])
    assert.equal(modelSpec(id), null, id);
});

test('class motion language stays in look manifests, independent of a GLB', () => {
  assert.deepEqual(
    ['trident-wasp', 'aegis-tortoise', 'lattice-loom'].map(
      (id) => presentationBotLook(id).locomotionCue,
    ),
    ['low-hover', 'heavy-hover', 'calm-hover'],
  );
  assert.equal(
    presentationBotLook('trident-wasp-volley').locomotionCue,
    'low-hover',
  );
  assert.deepEqual(
    ['trident-spark', 'rebound-diamond', 'lattice-rivet'].map(
      (id) => presentationProjectileLook(id).flightCue,
    ),
    ['axial-spin', 'flat-spin', 'tumble'],
  );
  assert.deepEqual(
    ['trident-spark', 'rebound-diamond', 'lattice-rivet'].map(
      (id) => presentationProjectileLook(id).terminalCue,
    ),
    ['fork-burst', 'facet-bloom', 'lattice-unweave'],
  );
  assert.equal(presentationProjectileLook('pulse-bolt').terminalCue, null);
});

test('legacy looks without a genuine model remain on the SVG fallback path', () => {
  assert.equal(modelSpec('vanguard'), null);
  assert.equal(modelSpec('pulse-bolt'), null);
  assert.equal(modelSpec('not-a-look'), null);
});

test('a whole-body GLB cannot serve a turret sector and yields to the sprite', () => {
  // The turret is the chassis' forward section repeated around an axis. A layered SVG can
  // be cropped to that section; the Striker's `part: 'whole'` mesh cannot, and handing the
  // whole body back put four entire strikers, tipped on their noses, around the unit —
  // a cage of hardware where an emplacement belonged.
  for (const id of ['trident-wasp', 'aegis-tortoise', 'lattice-loom']) {
    assert.equal(modelSpec(id)?.part, 'whole');
    assert.equal(lookModelSource(id), 'gltf');
    assert.equal(lookModelSource(id, 'front'), 'fallback');
    assert.equal(lookModelSource(id, undefined), 'gltf');
  }
});
