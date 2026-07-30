import assert from 'node:assert/strict';
import test from 'node:test';
import {
  modelSpec,
  presentationBotLook,
} from './.harness/harness.entry.js';

test('the approved Striker and Fabricator bodies resolve to authored class GLBs', () => {
  assert.deepEqual(modelSpec('trident-wasp'), {
    version: 1,
    id: 'trident-wasp',
    file: 'model.glb',
    kind: 'bot',
    part: 'whole',
    facing: '+x',
    up: '+y',
  });
  assert.deepEqual(modelSpec('lattice-loom'), {
    version: 1,
    id: 'lattice-loom',
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
    'trident-spark',
    'rebound-diamond',
    'lattice-rivet',
  ])
    assert.equal(modelSpec(id), null, id);
});

test('the canonical skimmer looks keep low-hover independent of a GLB', () => {
  assert.equal(
    presentationBotLook('trident-wasp').locomotionCue,
    'low-hover',
  );
  assert.equal(
    presentationBotLook('trident-wasp-volley').locomotionCue,
    'low-hover',
  );
  assert.equal(
    presentationBotLook('lattice-loom').locomotionCue,
    'low-hover',
  );
  assert.equal(modelSpec('trident-wasp')?.kind, 'bot');
  assert.equal(modelSpec('lattice-loom')?.kind, 'bot');
  assert.equal(modelSpec('trident-wasp-volley'), null);
});

test('legacy looks without a genuine model remain on the SVG fallback path', () => {
  assert.equal(modelSpec('vanguard'), null);
  assert.equal(modelSpec('pulse-bolt'), null);
  assert.equal(modelSpec('not-a-look'), null);
});
