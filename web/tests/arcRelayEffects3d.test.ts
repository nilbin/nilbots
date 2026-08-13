import assert from 'node:assert/strict';
import test from 'node:test';
import {
  ARC_SIGNATURE_PROP_SCALE,
  SENTINEL_YAW_SETTLE_TICKS,
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
  buildOverlays,
  LEVEL_UP_BEAT_TICKS,
  unitAccent,
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

test('review corrections remove beam-like guide geometry from Vector Dash and Target Paint', () => {
  for (const [replayName, signatureId, time] of [
    ['core-roster', 'vector-dash', 22.35],
    ['remaining-roster', 'target-paint', 192.35],
  ] as const) {
    const replay = loadReplayJson(
      gunzipSync(readFileSync(new URL(
        `../../art/reviews/arc-relay-presentation-batch-v1/runtime-proof/replays/${replayName}.broadcast.json.gz`,
        import.meta.url,
      ))).toString('utf8'),
    ).replay;
    const disposables: { dispose: () => void }[] = [];
    const effects = buildArcRelayEffects(replay, disposables);
    try {
      effects.update(time);
      const group = effects.group.children.find(
        (candidate) => candidate.visible && candidate.userData.signatureId === signatureId,
      );
      assert.ok(group, `${signatureId} is visible at ${time}`);
      let visibleSegments = 0;
      group!.traverse((node) => {
        if (node.visible && node.userData.primitive === 'segment') visibleSegments += 1;
      });
      assert.equal(
        visibleSegments,
        0,
        `${signatureId} does not draw a long guide/scan line`,
      );
    } finally {
      for (const disposable of disposables) disposable.dispose();
    }
  }
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
      [99.35, 6],
      [100.35, 2],
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

test('Prism Wall and Hardlight Block deploy body-height matte silhouettes', () => {
  for (const [replayName, signatureId, time] of [
    ['remaining-roster', 'prism-wall', 14.68],
    ['core-roster', 'hardlight-block', 52.68],
  ] as const) {
    const replay = loadReplayJson(
      gunzipSync(readFileSync(new URL(
        `../../art/reviews/arc-relay-presentation-batch-v1/runtime-proof/replays/${replayName}.broadcast.json.gz`,
        import.meta.url,
      ))).toString('utf8'),
    ).replay;
    const disposables: { dispose: () => void }[] = [];
    const effects = buildArcRelayEffects(replay, disposables);
    try {
      effects.update(time);
      effects.group.updateMatrixWorld(true);
      const group = effects.group.children.find(
        (candidate) =>
          candidate.visible && candidate.userData.signatureId === signatureId,
      );
      assert.ok(group, `${signatureId} has a visible deployment rig`);
      const bounds = new THREE.Box3().setFromObject(group!);
      assert.ok(
        bounds.max.y - bounds.min.y >= 0.55,
        `${signatureId} rises to a body-height silhouette (${bounds.max.y - bounds.min.y})`,
      );
      assert.ok(
        bounds.max.x - bounds.min.x >= 0.82 && bounds.max.z - bounds.min.z >= 0.5,
        `${signatureId} occupies its authored ground envelope`,
      );
    } finally {
      for (const disposable of disposables) disposable.dispose();
    }
  }
});

test('persistent constructs leave a replay-derived exit instead of vanishing', () => {
  for (const [replayName, signatureId, removalTime] of [
    ['remaining-roster', 'prism-wall', 22.35],
    ['core-roster', 'hardlight-block', 22.35],
    ['core-roster', 'survey-flare', 22.35],
    ['remaining-roster', 'sentinel-seed', 32.35],
  ] as const) {
    const replay = loadReplayJson(
      gunzipSync(readFileSync(new URL(
        `../../art/reviews/arc-relay-presentation-batch-v1/runtime-proof/replays/${replayName}.broadcast.json.gz`,
        import.meta.url,
      ))).toString('utf8'),
    ).replay;
    const disposables: { dispose: () => void }[] = [];
    const effects = buildArcRelayEffects(replay, disposables);
    try {
      effects.update(removalTime);
      const exits = effects.group.children.filter(
        (candidate) =>
          candidate.visible &&
          candidate.userData.signatureId === signatureId &&
          candidate.userData.phase === 'outcome',
      );
      assert.ok(exits.length > 0, `${signatureId} has a visible removal outcome`);
      assert.ok(
        exits.some((exit) => {
          let meshes = 0;
          exit.traverse((node) => {
            if (node.visible && (node as THREE.Mesh).isMesh) meshes += 1;
          });
          return meshes > 0;
        }),
        `${signatureId} removal outcome contains authored geometry`,
      );
    } finally {
      for (const disposable of disposables) disposable.dispose();
    }
  }
});

test('floor pressure waves stay horizontal and active fields animate from their phase boundary', () => {
  const replay = loadReplayJson(
    gunzipSync(readFileSync(new URL(
      '../../art/reviews/arc-relay-presentation-batch-v1/runtime-proof/replays/remaining-roster.broadcast.json.gz',
      import.meta.url,
    ))).toString('utf8'),
  ).replay;
  const disposables: { dispose: () => void }[] = [];
  const effects = buildArcRelayEffects(replay, disposables);
  try {
    const boundsAt = (signatureId: string, time: number) => {
      effects.update(time);
      effects.group.updateMatrixWorld(true);
      const group = effects.group.children.find(
        (candidate) =>
          candidate.visible && candidate.userData.signatureId === signatureId,
      );
      assert.ok(group, `${signatureId} is visible at ${time}`);
      return new THREE.Box3().setFromObject(group!);
    };

    const kinetic = boundsAt('kinetic-burst', 48.45);
    const kineticSize = kinetic.getSize(new THREE.Vector3());
    assert.ok(
      kineticSize.y < Math.max(kineticSize.x, kineticSize.z) * 0.35,
      `the Kinetic outcome is a low floor wave, not an upright blade (${kineticSize.toArray()})`,
    );

    const smokeOpening = boundsAt('smoke-canister', 104.05)
      .getSize(new THREE.Vector3());
    const smokeSettled = boundsAt('smoke-canister', 105.15)
      .getSize(new THREE.Vector3());
    assert.ok(
      smokeSettled.x > smokeOpening.x * 1.02 ||
        smokeSettled.z > smokeOpening.z * 1.02,
      `Smoke blooms after landing (${smokeOpening.toArray()} -> ${smokeSettled.toArray()})`,
    );
  } finally {
    for (const disposable of disposables) disposable.dispose();
  }
});

test('pooled signature transforms are identical after direct seek and played phase history', () => {
  const replay = loadReplayJson(
    gunzipSync(readFileSync(new URL(
      '../../art/reviews/arc-relay-presentation-batch-v1/runtime-proof/replays/core-roster.broadcast.json.gz',
      import.meta.url,
    ))).toString('utf8'),
  ).replay;
  const directDisposables: { dispose: () => void }[] = [];
  const playedDisposables: { dispose: () => void }[] = [];
  const direct = buildArcRelayEffects(replay, directDisposables);
  const played = buildArcRelayEffects(replay, playedDisposables);
  const snapshot = (effects: ReturnType<typeof buildArcRelayEffects>) => {
    const signature = effects.group.children.find(
      (candidate) =>
        candidate.visible && candidate.userData.signatureId === 'survey-flare',
    );
    assert.ok(signature, 'Survey Flare has a visible active rig');
    const transforms: number[][] = [];
    signature.traverse((node) => {
      if (!node.visible) return;
      transforms.push([
        node.position.x, node.position.y, node.position.z,
        node.rotation.x, node.rotation.y, node.rotation.z,
        node.scale.x, node.scale.y, node.scale.z,
      ]);
    });
    return transforms;
  };
  try {
    direct.update(14.45);
    played.update(13.45);
    played.update(14.45);
    assert.deepEqual(snapshot(played), snapshot(direct));
  } finally {
    for (const disposable of [...directDisposables, ...playedDisposables])
      disposable.dispose();
  }
});

test('persistent props stay legible inside their tiles and sentinel yaw never looks ahead', () => {
  assert.equal(ARC_SIGNATURE_PROP_SCALE['trip-node'], 0.94);
  assert.equal(ARC_SIGNATURE_PROP_SCALE['sentinel-seed'], 0.72);

  const shots = [
    { tick: 12, yaw: Math.PI / 2 },
    { tick: 18, yaw: -Math.PI / 2 },
  ];
  assert.equal(latestObservedSignatureYaw(shots, 11.999), null);
  assert.equal(latestObservedSignatureYaw(shots, 12), 0);
  const turning = latestObservedSignatureYaw(
    shots,
    12 + SENTINEL_YAW_SETTLE_TICKS / 2,
  );
  assert.ok(
    turning !== null && turning > 0 && turning < Math.PI / 2,
    'the public shot bearing rotates the head instead of snapping it',
  );
  assert.equal(latestObservedSignatureYaw(shots, 17.999), Math.PI / 2);
  assert.equal(latestObservedSignatureYaw(shots, 18), Math.PI / 2);
  assert.equal(
    latestObservedSignatureYaw(shots, 18 + SENTINEL_YAW_SETTLE_TICKS),
    -Math.PI / 2,
    'the half-turn uses one deterministic shortest direction',
  );
});

test('Tractor Hook keeps a non-circular cable attached through the recorded pull', () => {
  const replay = loadReplayJson(
    gunzipSync(readFileSync(new URL(
      '../../art/reviews/arc-relay-presentation-batch-v1/runtime-proof/replays/core-roster.broadcast.json.gz',
      import.meta.url,
    ))).toString('utf8'),
  ).replay;
  const ownerActor = replay.ticks[0]!.after.actors[0]!.identity;
  const targetActor = replay.ticks[0]!.after.actors.find(
    (actor) => actor.identity.teamId !== ownerActor.teamId,
  )!.identity;
  const operationId = 'test-tractor-hook-pull';
  const owner = replay.ticks[0]!.after.actors.find(
    (actor) => actor.actorKey === ownerActor.actorKey,
  )!;
  const target = replay.ticks[0]!.after.actors.find(
    (actor) => actor.actorKey === targetActor.actorKey,
  )!;
  owner.position = { x: 13, y: 12 };
  target.position = { x: 16, y: 12 };
  for (const side of ['before', 'after'] as const) {
    const ownerState = replay.ticks[1]![side].actors.find(
      (actor) => actor.actorKey === ownerActor.actorKey,
    )!;
    const targetState = replay.ticks[1]![side].actors.find(
      (actor) => actor.actorKey === targetActor.actorKey,
    )!;
    ownerState.position = { x: 13, y: 12 };
    targetState.position = { x: 16, y: 12 };
    const ownerPull = replay.ticks[2]![side].actors.find(
      (actor) => actor.actorKey === ownerActor.actorKey,
    )!;
    const targetPull = replay.ticks[2]![side].actors.find(
      (actor) => actor.actorKey === targetActor.actorKey,
    )!;
    ownerPull.position = { x: 13, y: 12 };
    targetPull.position = side === 'before' ? { x: 16, y: 12 } : { x: 14, y: 12 };
  }
  replay.ticks[0]!.after.mode = replay.ticks[0]!.before.mode;
  assert.equal(replay.ticks[0]!.after.mode?.kind, 'arc-relay');
  if (replay.ticks[0]!.after.mode?.kind !== 'arc-relay' ||
      !('visibleSignatures' in replay.ticks[0]!.after.mode)) return;
  replay.ticks[0]!.after.mode.visibleSignatures = [];
  for (const tickIndex of [1, 2]) {
    for (const side of ['before', 'after'] as const) {
      const mode = replay.ticks[tickIndex]![side].mode;
      assert.equal(mode?.kind, 'arc-relay');
      if (mode?.kind === 'arc-relay' && 'visibleSignatures' in mode)
        mode.visibleSignatures = [];
    }
    replay.ticks[tickIndex]!.events = [];
    replay.ticks[tickIndex]!.lifecycleEvents = [];
  }
  const launchMode = replay.ticks[1]!.before.mode;
  assert.equal(launchMode?.kind, 'arc-relay');
  if (launchMode?.kind !== 'arc-relay' || !('visibleSignatures' in launchMode))
    return;
  launchMode.visibleSignatures = [{
    operationId,
    signatureId: 'tractor-hook',
    signatureKind: 'targeted',
    ownerActor,
    ownerTeamId: ownerActor.teamId,
    phase: 'tell',
    startedTick: 0,
    completesAtTick: 1,
    endsAtTick: null,
    positions: [{ x: 13, y: 12 }, { x: 16, y: 12 }],
    targetActor,
    remainingCapacity: 0,
    suppressed: false,
  }];
  replay.ticks[1]!.events = [{
    ...replay.ticks[0]!.events[0]!,
    eventId: 'test:tractor:launch',
    tick: 1,
    arcRelayFact: {
      kind: 'signature-changed',
      operationId,
      signatureId: 'tractor-hook',
      ownerActor,
      phase: null,
      reason: 'launched',
    },
  }];
  replay.ticks[2]!.lifecycleEvents = [{
    ...replay.ticks[0]!.events[0]!,
    eventId: 'test:tractor:pull',
    tick: 2,
    arcRelayFact: {
      kind: 'body-relocated',
      operationId,
      signatureId: 'tractor-hook',
      ownerActor,
      targetActor,
      from: { x: 16, y: 12 },
      to: { x: 14, y: 12 },
    },
  }];
  const disposables: { dispose: () => void }[] = [];
  const effects = buildArcRelayEffects(replay, disposables);
  const visibleGroup = (time: number) => {
    effects.update(time);
    return effects.group.children.find(
      (candidate) =>
        candidate.visible &&
        candidate.userData.signatureId === 'tractor-hook' &&
        candidate.userData.phase === 'outcome',
    );
  };
  try {
    const pullStart = visibleGroup(2);
    assert.ok(pullStart, 'the relocation takes over without a cable gap');
    assert.equal(
      pullStart.children.some(
        (child) => child.visible && child.geometry?.type === 'TorusGeometry',
      ),
      false,
      'the hook treatment contains no spool/target donut',
    );
    const cable = pullStart.children.find(
      (child) =>
        child.visible &&
        child.geometry?.type === 'BoxGeometry' &&
        (child as THREE.Mesh).material instanceof THREE.MeshStandardMaterial,
    ) as THREE.Mesh | undefined;
    assert.ok(cable);
    const expectedCableColour = new THREE.Color(
      unitAccent(replay, ownerActor.unitKey),
    ).lerp(new THREE.Color('#dbe7ea'), 0.12);
    assert.equal(
      (cable.material as THREE.MeshStandardMaterial).color.getHexString(),
      expectedCableColour.getHexString(),
      'the one physical strand keeps the owning team colour',
    );
    assert.ok(
      cable.scale.z < 0.7,
      `the physical hook cable stays a narrow strand (${cable.scale.z})`,
    );
    const startHead = pullStart.children.find(
      (child) => child.visible && child.geometry?.type === 'CapsuleGeometry',
    );
    assert.ok(startHead);
    const startX = startHead.position.x;
    const pullMiddle = visibleGroup(2.5);
    const middleHead = pullMiddle?.children.find(
      (child) => child.visible && child.geometry?.type === 'CapsuleGeometry',
    );
    assert.ok(middleHead);
    assert.ok(middleHead.position.x < startX, 'the latch follows the pulled body');
    assert.equal(visibleGroup(3), undefined, 'the cable releases after the pull tick');
  } finally {
    for (const disposable of disposables) disposable.dispose();
  }
});

test('signature-owned line tells do not stack the generic red attack aim', () => {
  const replay = loadReplayJson(
    gunzipSync(readFileSync(new URL(
      '../../art/reviews/arc-relay-presentation-batch-v1/runtime-proof/replays/core-roster.broadcast.json.gz',
      import.meta.url,
    ))).toString('utf8'),
  ).replay;
  const time = 25.35;
  const tick = replay.ticks[Math.floor(time)]!;
  const mode = tick.after.mode;
  assert.equal(mode?.kind, 'arc-relay');
  if (mode?.kind !== 'arc-relay' || !('visibleSignatures' in mode)) return;
  const signatureOwners = new Set(
    mode.visibleSignatures
      .filter(
        (signature) =>
          signature.phase === 'tell' &&
          (signature.signatureId === 'tractor-hook' ||
            signature.signatureId === 'rail-line'),
      )
      .map((signature) => signature.ownerActor.actorKey),
  );
  assert.ok(signatureOwners.size > 0, 'the pinned frame contains a line signature');
  const expectedGenericAims = mode.pendingStrikes.filter(
    (strike) =>
      strike.target !== null && !signatureOwners.has(strike.shooter.actorKey),
  ).length;
  const overlays = buildOverlays(replay);
  try {
    overlays.update(time, null, false);
    const aimGroup = overlays.group.children.find(
      (node) => node.userData.kind === 'strike-aims',
    );
    assert.ok(aimGroup);
    const visibleAims: THREE.Object3D[] = [];
    aimGroup.traverse((node) => {
      if (node.visible && node.userData.kind === 'strike-aim')
        visibleAims.push(node);
    });
    assert.equal(
      visibleAims.length,
      expectedGenericAims,
      'ordinary attacks keep their aim while Hook/Rail own exactly one tell line',
    );
  } finally {
    overlays.dispose();
  }
});

test('Sentinel Seed transforms upward from its floor seed', () => {
  const replay = loadReplayJson(
    gunzipSync(readFileSync(new URL(
      '../../art/reviews/arc-relay-presentation-batch-v1/runtime-proof/replays/remaining-roster.broadcast.json.gz',
      import.meta.url,
    ))).toString('utf8'),
  ).replay;
  const disposables: { dispose: () => void }[] = [];
  const effects = buildArcRelayEffects(replay, disposables);
  const propAt = (time: number) => {
    effects.update(time);
    return effects.group.children.find(
      (candidate) =>
        candidate.visible &&
        candidate.userData.signatureId === 'sentinel-seed' &&
        candidate.userData.phase === undefined,
    );
  };
  try {
    const opening = propAt(15);
    assert.ok(opening);
    assert.equal(opening.position.y, 0);
    assert.ok(opening.scale.y < opening.scale.x * 0.2);
    const openingHeight = opening.scale.y;
    const openingWidth = opening.scale.x;
    const growing = propAt(15.45);
    assert.ok(growing);
    assert.equal(growing.position.y, 0);
    assert.ok(growing.scale.y > openingHeight * 4);
    assert.ok(growing.scale.x > openingWidth);
  } finally {
    for (const disposable of disposables) disposable.dispose();
  }
});

test('Sentinel Seed tell leaves the casting Nest completely unobscured', () => {
  const replay = loadReplayJson(
    gunzipSync(readFileSync(new URL(
      '../../art/reviews/arc-relay-presentation-batch-v1/runtime-proof/replays/remaining-roster.broadcast.json.gz',
      import.meta.url,
    ))).toString('utf8'),
  ).replay;
  const disposables: { dispose: () => void }[] = [];
  const effects = buildArcRelayEffects(replay, disposables);
  try {
    effects.update(14.5);
    const tell = effects.group.children.find(
      (candidate) =>
        candidate.visible &&
        candidate.userData.signatureId === 'sentinel-seed' &&
        candidate.userData.phase === 'tell',
    );
    assert.equal(
      tell,
      undefined,
      'the whole-body plant pose owns the tell without a floor ring or seed shell',
    );
  } finally {
    for (const disposable of disposables) disposable.dispose();
  }
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

test('the level-up transient starts on its exact fact and is gone after 1.8 ticks', () => {
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

  assert.equal(LEVEL_UP_BEAT_TICKS, 1.8);
  assert.equal(
    veterancy.levelUpAt(1.999, actor.teamId, actor.unitId, actor.lifeId),
    null,
  );
  assert.deepEqual(
    veterancy.levelUpAt(2, actor.teamId, actor.unitId, actor.lifeId),
    { level: 2, age: 0 },
  );
  assert.ok(
    veterancy.levelUpAt(3.799, actor.teamId, actor.unitId, actor.lifeId),
  );
  assert.equal(
    veterancy.levelUpAt(3.8, actor.teamId, actor.unitId, actor.lifeId),
    null,
  );
  assert.equal(
    veterancy.levelUpAt(2.2, actor.teamId, actor.unitId, actor.lifeId + 1),
    null,
  );
});
