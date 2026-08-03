import assert from 'node:assert/strict';
import test from 'node:test';
import {
  ARC_SIGNATURE_STYLES,
  arcSignatureVisualPhase,
} from './.harness/harness.entry.js';

const signatureIds = [
  'vector-dash',
  'prism-wall',
  'tractor-hook',
  'repair-beam',
  'survey-flare',
  'falling-star',
  'trip-node',
  'null-field',
  'arc-toss',
  'exchange',
  'rail-line',
  'hardlight-block',
  'target-paint',
  'kinetic-burst',
  'smoke-canister',
  'sentinel-seed',
] as const;

test('the 3D Arc effect registry gives every signature a distinct authored form', () => {
  assert.deepEqual(Object.keys(ARC_SIGNATURE_STYLES), signatureIds);
  assert.equal(new Set(Object.values(ARC_SIGNATURE_STYLES).map((style) => style.form)).size, 16);
  assert.equal(ARC_SIGNATURE_STYLES['survey-flare'].polish, 'priority');
  assert.equal(ARC_SIGNATURE_STYLES['smoke-canister'].polish, 'priority');
  assert.equal(ARC_SIGNATURE_STYLES['repair-beam'].polish, 'priority');
  assert.equal(ARC_SIGNATURE_STYLES['vector-dash'].polish, 'simple');
  assert.equal(ARC_SIGNATURE_STYLES['kinetic-burst'].polish, 'simple');
});

test('signature anticipation never starts early or outlives its authoritative tell', () => {
  const signature = {
    operationId: 'op',
    signatureId: 'survey-flare',
    signatureKind: 'field',
    ownerActor: {
      actorKey: 'a',
      unitKey: 'u',
      teamId: 0,
      unitId: 0,
      lifeId: 0,
    },
    ownerTeamId: 0,
    phase: 'tell' as const,
    startedTick: 8,
    completesAtTick: 11,
    endsAtTick: 15,
    positions: [],
    targetActor: null,
    remainingCapacity: 0,
    suppressed: false,
  };
  assert.equal(arcSignatureVisualPhase(signature, 7.999), 'hidden');
  assert.equal(arcSignatureVisualPhase(signature, 8), 'tell');
  assert.equal(arcSignatureVisualPhase(signature, 10.999), 'tell');
  assert.equal(arcSignatureVisualPhase(signature, 11), 'hidden');
  assert.equal(
    arcSignatureVisualPhase({ ...signature, phase: 'active' }, 14.999),
    'active',
  );
  assert.equal(
    arcSignatureVisualPhase({ ...signature, phase: 'active' }, 15),
    'hidden',
  );
});
