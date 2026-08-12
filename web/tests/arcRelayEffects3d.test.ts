import assert from 'node:assert/strict';
import test from 'node:test';
import {
  ARC_SIGNATURE_PROP_SCALE,
  ARC_SIGNATURE_PRESENTATION_ID,
  ARC_SIGNATURE_WEBGL_PAINTERS,
  arcSignatureAudioDefinitionIds,
  arcSignaturePresentationDefinitions,
  arcSignatureVisualPhase,
  buildArcRelayEffects,
  latestObservedSignatureYaw,
  resolveArcSignaturePresentation,
  sentinelYawTarget,
  buildArcVeterancy,
  LEVEL_UP_BEAT_TICKS,
} from './.harness/harness.entry.js';
import * as THREE from 'three';
import { readFileSync } from 'node:fs';
import { gunzipSync } from 'node:zlib';
import { loadReplayJson } from '../src/replayIngress.ts';

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

test('the typed Arc package gives every signature an owned WebGL treatment', () => {
  const definitions = arcSignaturePresentationDefinitions();
  assert.deepEqual(
    definitions.map((definition) => definition.signatureId).sort(),
    [...signatureIds].sort(),
  );
  assert.deepEqual(
    Object.keys(ARC_SIGNATURE_WEBGL_PAINTERS).sort(),
    [...signatureIds].sort(),
  );
  assert.deepEqual(
    arcSignatureAudioDefinitionIds(),
    [...signatureIds].sort(),
    'the browser-independent replay audio registry must cover the same grammar',
  );
  assert.ok(
    definitions.every(
      (definition) =>
        definition.packageId === ARC_SIGNATURE_PRESENTATION_ID &&
        definition.audio.length > 0 &&
        definition.budget.transparentLayers <= 3,
    ),
  );
  assert.equal(
    new Set(Object.values(ARC_SIGNATURE_WEBGL_PAINTERS)).size,
    16,
    'every signature owns a discoverable painter even when two compose the prop primitive',
  );
  const runtimeCueIds = new Set([
    'attack-windup',
    'projectile',
    'impact',
    'healing',
    'signature-motion',
    'signature-construct',
    'signature-tether',
    'signature-survey',
    'signature-null',
    'signature-smoke',
    'signature-exchange',
    'signature-target',
    'signature-kinetic',
    'core-family',
    'silent',
  ]);
  assert.ok(
    definitions.every((definition) =>
      definition.audio.every((binding) => runtimeCueIds.has(binding.cue)),
    ),
    'presentation definitions cannot name an unowned audio intent',
  );
  const hook = definitions.find(
    (definition) => definition.signatureId === 'tractor-hook',
  );
  assert.deepEqual(
    hook?.audio.find((binding) => binding.trigger === 'launched'),
    {
      trigger: 'launched',
      cue: 'signature-tether',
      replacesGenericRelease: true,
    },
  );
});

test('signature package resolution preserves legacy and fails future packages truthfully', () => {
  assert.deepEqual(
    resolveArcSignaturePresentation(
      { signaturePresentationId: null },
      'null-field',
    ).kind,
    'legacy',
  );
  assert.equal(
    resolveArcSignaturePresentation(
      { signaturePresentationId: ARC_SIGNATURE_PRESENTATION_ID },
      'null-field',
    ).kind,
    'authored',
  );
  assert.equal(
    resolveArcSignaturePresentation(
      { signaturePresentationId: 'future-package-v9' },
      'null-field',
    ).kind,
    'generic',
  );
  assert.equal(
    resolveArcSignaturePresentation(
      { signaturePresentationId: ARC_SIGNATURE_PRESENTATION_ID },
      'future-signature',
    ).kind,
    'generic',
  );
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

test('real exchange lifecycle never publishes an empty WebGL signature group', () => {
  const replay = loadReplayJson(
    gunzipSync(readFileSync(new URL(
      '../../art/reviews/arc-relay-presentation-batch-v1/runtime-proof/replays/core-roster.broadcast.json.gz',
      import.meta.url,
    ))).toString('utf8'),
  ).replay;
  const disposables: { dispose: () => void }[] = [];
  const effects = buildArcRelayEffects(replay, disposables);
  try {
    for (const [time, minimumMeshes] of [
      [99.35, 18],
      [100.35, 4],
    ] as const) {
      effects.update(time);
      const exchangeGroups: THREE.Object3D[] = [];
      effects.group.traverse((node) => {
        if (node.visible && node.userData.signatureId === 'exchange')
          exchangeGroups.push(node);
      });
      assert.ok(exchangeGroups.length > 0, `exchange is absent at ${time}`);
      for (const group of exchangeGroups) {
        let visibleMeshes = 0;
        group.traverse((node) => {
          if (node.visible && (node as THREE.Mesh).isMesh) visibleMeshes += 1;
        });
        assert.ok(visibleMeshes >= minimumMeshes);
      }
    }
  } finally {
    for (const disposable of disposables) disposable.dispose();
  }
});

test('persistent props stay legible inside their tiles and sentinel yaw never looks ahead', () => {
  assert.equal(ARC_SIGNATURE_PROP_SCALE['trip-node'], 0.75);
  assert.equal(ARC_SIGNATURE_PROP_SCALE['sentinel-seed'], 0.95);

  const shots = [
    { tick: 12, yaw: Math.PI / 2 },
    { tick: 18, yaw: -Math.PI / 2 },
  ];
  assert.equal(latestObservedSignatureYaw(shots, 11.999), null);
  assert.equal(latestObservedSignatureYaw(shots, 12), Math.PI / 2);
  assert.equal(latestObservedSignatureYaw(shots, 17.999), Math.PI / 2);
  assert.equal(latestObservedSignatureYaw(shots, 18), -Math.PI / 2);
});

test('sentinel yaw targets the authored head split and keeps legacy fallback', () => {
  const propGroup = new THREE.Group();
  const splitModel = new THREE.Group();
  const yawHead = new THREE.Group();
  yawHead.name = 'yaw-head';
  splitModel.add(yawHead);

  assert.equal(sentinelYawTarget(propGroup, splitModel), yawHead);
  assert.equal(sentinelYawTarget(propGroup, new THREE.Group()), propGroup);
  assert.equal(sentinelYawTarget(propGroup, null), propGroup);
});

test('the level-up transient starts on its exact fact and is gone after 0.9 ticks', () => {
  const replay = loadReplayJson(
    readFileSync(
      new URL('./fixtures/generic-mind-replay-v3.json', import.meta.url),
      'utf8',
    ),
  ).replay;
  const actor = replay.ticks[0]!.after.actors[0]!.identity;
  replay.ticks[2]!.events.push({
    ...replay.ticks[2]!.events[0]!,
    eventId: 'arc:level-up:test',
    arcRelayFact: {
      kind: 'leveled-up',
      actor,
      level: 2,
      position: { ...replay.ticks[2]!.after.actors[0]!.position },
    },
  });
  const veterancy = buildArcVeterancy(replay);

  assert.equal(LEVEL_UP_BEAT_TICKS, 0.9);
  assert.equal(
    veterancy.levelUpAt(1.999, actor.teamId, actor.unitId, actor.lifeId),
    null,
  );
  assert.deepEqual(
    veterancy.levelUpAt(2, actor.teamId, actor.unitId, actor.lifeId),
    { level: 2, age: 0 },
  );
  assert.ok(
    veterancy.levelUpAt(2.899, actor.teamId, actor.unitId, actor.lifeId),
  );
  assert.equal(
    veterancy.levelUpAt(2.9, actor.teamId, actor.unitId, actor.lifeId),
    null,
  );
  assert.equal(
    veterancy.levelUpAt(2.2, actor.teamId, actor.unitId, actor.lifeId + 1),
    null,
  );
});
