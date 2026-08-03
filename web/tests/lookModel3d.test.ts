import assert from 'node:assert/strict';
import test from 'node:test';
import {
  lookModelSource,
  modelSpec,
  presentationBotLook,
} from './.harness/harness.entry.js';

const arcLooks = [
  'kestrel',
  'palisade',
  'towline',
  'patchbay',
  'lantern',
  'mortar',
  'minesmith',
  'hush',
  'relay',
  'switchback',
  'longshot',
  'mason',
  'sunder',
  'repulsor',
  'veil',
  'nest',
] as const;

test('the approved Striker and complete Arc Relay fleet resolve to authored GLBs', () => {
  assert.deepEqual(modelSpec('trident-wasp'), {
    version: 1,
    id: 'trident-wasp',
    file: 'model.glb',
    kind: 'bot',
    part: 'whole',
    facing: '+x',
    up: '+y',
  });

  for (const id of [
    'trident-wasp-volley',
    'aegis-tortoise',
    'aegis-tortoise-shell',
    'aegis-tortoise-turret',
    'lattice-loom',
    'trident-spark',
    'rebound-diamond',
    'lattice-rivet',
  ])
    assert.equal(modelSpec(id), null, id);

  for (const classId of arcLooks) {
    const id = `arc-${classId}`;
    const spec = modelSpec(id);
    assert.equal(spec?.id, id);
    assert.equal(spec?.kind, 'bot');
    assert.equal(spec?.part, 'whole');
    assert.equal(spec?.facing, '+x');
    assert.equal(spec?.up, '+y');
    assert.equal(spec?.nodes, undefined, `${id} is an approved monolithic mesh`);
    assert.ok(spec?.motion);
    assert.ok(spec?.signature);
    assert.equal(
      spec?.source?.generator,
      'scripts/class-models/promote-meshy-arc-fleet.mjs',
    );
    assert.equal(spec?.source?.provider, 'Meshy');
    assert.equal(spec?.source?.model, 'meshy-t2');
    assert.ok((spec?.ledger?.bytes ?? Number.POSITIVE_INFINITY) <= 1024 * 1024);
    assert.equal(lookModelSource(id), 'gltf');
    assert.equal(lookModelSource(id, 'front'), 'fallback');
  }
});

test('the canonical Striker looks keep low-hover independent of a GLB', () => {
  assert.equal(
    presentationBotLook('trident-wasp').locomotionCue,
    'low-hover',
  );
  assert.equal(
    presentationBotLook('trident-wasp-volley').locomotionCue,
    'low-hover',
  );
  assert.equal(modelSpec('trident-wasp')?.kind, 'bot');
  assert.equal(modelSpec('trident-wasp-volley'), null);
});

test('legacy looks without a genuine model remain on the SVG fallback path', () => {
  assert.equal(modelSpec('vanguard'), null);
  assert.equal(modelSpec('pulse-bolt'), null);
  assert.equal(modelSpec('not-a-look'), null);
});

test('approved Arc Relay signature props resolve independently of bot looks', () => {
  for (const [id, signature, orientation] of [
    ['arc-trip-node', 'trip-node', 'lay-flat-x'],
    ['arc-sentinel-seed', 'sentinel-seed', 'identity'],
  ] as const) {
    const spec = modelSpec(id);
    assert.equal(spec?.kind, 'signature');
    assert.equal(spec?.part, 'whole');
    assert.equal(spec?.signature, signature);
    assert.equal(spec?.source?.orientation, orientation);
    assert.equal(spec?.source?.textureTier, 'arc-relay-signatures-ktx2-selective-v1');
    assert.ok((spec?.ledger?.bytes ?? Number.POSITIVE_INFINITY) <= 1024 * 1024);
    assert.equal(spec?.nodes, undefined);
    assert.equal(spec?.motion, undefined);
  }
});

test('a whole-body GLB cannot serve a turret sector and yields to the sprite', () => {
  // The turret is the chassis' forward section repeated around an axis. A layered SVG can
  // be cropped to that section; the Striker's `part: 'whole'` mesh cannot, and handing the
  // whole body back put four entire strikers, tipped on their noses, around the unit —
  // a cage of hardware where an emplacement belonged.
  assert.equal(modelSpec('trident-wasp')?.part, 'whole');
  assert.equal(lookModelSource('trident-wasp'), 'gltf');
  assert.equal(lookModelSource('trident-wasp', 'front'), 'fallback');

  // The mobile body is unaffected: no sector is asked for, so the model still wins.
  assert.equal(lookModelSource('trident-wasp', undefined), 'gltf');

  // And a look with no model at all was always the sprite, sector or not.
  assert.equal(lookModelSource('lattice-loom'), 'fallback');
  assert.equal(lookModelSource('lattice-loom', 'front'), 'fallback');
});
