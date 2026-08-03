import assert from 'node:assert/strict';
import test from 'node:test';
import {
  ARC_SIGNATURE_PROP_SCALE,
  ARC_SIGNATURE_STYLES,
  arcSignatureVisualPhase,
  latestObservedSignatureYaw,
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

test('approved persistent props stay inside their tiles and sentinel yaw never looks ahead', () => {
  assert.equal(ARC_SIGNATURE_PROP_SCALE['trip-node'], 0.46);
  assert.equal(ARC_SIGNATURE_PROP_SCALE['sentinel-seed'], 0.66);

  const shots = [
    { tick: 12, yaw: Math.PI / 2 },
    { tick: 18, yaw: -Math.PI / 2 },
  ];
  assert.equal(latestObservedSignatureYaw(shots, 11.999), null);
  assert.equal(latestObservedSignatureYaw(shots, 12), Math.PI / 2);
  assert.equal(latestObservedSignatureYaw(shots, 17.999), Math.PI / 2);
  assert.equal(latestObservedSignatureYaw(shots, 18), -Math.PI / 2);
});
