import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import test from 'node:test';
import {
  decodeReplay,
  decodeReplayJson,
  ReplayDecodeError,
} from '../src/replayNormalize.ts';
import { validateReplayV3TickStartBoundary } from '../src/replayV3Normalize.ts';
import type { ReplayV3Document } from '../src/replayWireV3.ts';
import {
  adaptReplayV3ToFrontline,
  JS_UNSAFE_DECIMAL,
  replayV1FixtureInput,
  replayV1LivePartialFixtureInput,
  replayV2FixtureInput,
  replayV2ZeroTickPartialFixtureInput,
} from './support/replayFixtureInputs.ts';

test('strict dispatch rejects malformed and unknown replay versions', () => {
  assert.throws(() => decodeReplay(null), ReplayDecodeError);
  assert.throws(
    () => decodeReplay({ header: {} }),
    /replayVersion: missing required property/,
  );
  assert.throws(
    () => decodeReplay({ header: { replayVersion: 4 } }),
    /unsupported replay version 4/,
  );

  const malformed = structuredClone(replayV2FixtureInput()) as unknown as {
    header: { seed?: string };
  };
  delete malformed.header.seed;
  assert.throws(
    () => decodeReplay(malformed),
    /header\.seed: missing required property/,
  );
});

test('decoder retains the untouched wire object for upstream hash verification', () => {
  const input = replayV2FixtureInput();
  const decoded = decodeReplay(input);

  assert.strictEqual(decoded.wire, input);
  assert.equal(decoded.replayVersion, 2);
});

test('raw JSON decoding preserves an unsafe replay-v1 seed lexically', () => {
  const raw = replayFixtureText('golden-replay.json');
  const exact = decodeReplayJson(raw);
  const objectOnly = decodeReplay(JSON.parse(raw) as unknown);

  assert.equal(exact.replay.seed, '3004873239773946906');
  assert.equal(exact.replay.seedExact, true);
  assert.equal(exact.replay.seedEncoding, 'legacy-json-number');
  assert.equal(exact.rawJson, raw);
  assert.equal(objectOnly.replay.seedExact, false);
  assert.notEqual(objectOnly.replay.seed, exact.replay.seed);
});

test('raw JSON decoding rejects duplicate header or seed properties', () => {
  const fixture = replayV1FixtureInput();
  const serialized = JSON.stringify(fixture);
  const duplicateSeed = serialized.replace(
    '"seed":7',
    '"seed":6,"seed":7',
  );
  assert.throws(
    () => decodeReplayJson(duplicateSeed),
    /header\.seed: duplicate property/,
  );

  const duplicateHeader = `{"header":${JSON.stringify(
    fixture.header,
  )},${serialized.slice(1)}`;
  assert.throws(
    () => decodeReplayJson(duplicateHeader),
    /replay\.header: duplicate property/,
  );
});

test('replay-v1 creates canonical virtual teams, units, and lives by sparse slot', () => {
  const decoded = decodeReplay(replayV1FixtureInput());
  const replay = decoded.replay;

  assert.equal(replay.sourceVersion, 1);
  assert.deepEqual(
    replay.participants.map((participant) => participant.participantId),
    [3, 9],
  );
  assert.deepEqual(
    replay.teams.map((team) => team.teamId),
    [3, 9],
  );
  assert.deepEqual(
    replay.units.map((unit) => unit.unitKey),
    ['duel:3:unit:0', 'duel:9:unit:0'],
  );
  assert.deepEqual(
    replay.ticks[0]?.after.actors.map((actor) => actor.actorKey),
    ['duel:3:unit:0:life:0', 'duel:9:unit:0:life:0'],
  );
  assert.ok(
    replay.ticks[0]?.actorTurns.every(
      (turn) => turn.observation.completeness === 'legacy-partial',
    ),
  );
  assert.equal(replay.contract.kind, 'legacy-partial');
  if (replay.contract.kind === 'legacy-partial') {
    assert.equal(replay.contract.schemaVersion, null);
    assert.equal(replay.contract.rules.rulesFingerprint, null);
    assert.equal(replay.contract.rules.limits.maxTicks, 10);
    assert.equal(replay.contract.rules.vision.range, 4);
    assert.equal(replay.contract.rules.energy, null);
    assert.equal(replay.contract.map.mapFingerprint, null);
    assert.deepEqual(
      replay.contract.topology.unitSlots.map((unit) => unit.unitKey),
      ['duel:3:unit:0', 'duel:9:unit:0'],
    );
  }
});

test('replay-v1 accepts the live endpoint shape with omitted null result and hash', () => {
  const input = replayV1LivePartialFixtureInput();

  assert.equal(Object.hasOwn(input, 'result'), false);
  assert.equal(Object.hasOwn(input, 'replayHash'), false);
  assert.equal(input.partial, true);

  const decoded = decodeReplay(input);

  assert.strictEqual(decoded.wire, input);
  assert.equal(decoded.replay.partial, true);
  assert.equal(decoded.replay.result, null);
  assert.equal(decoded.replay.replayHash, null);
});

test('replay-v1 destroyed units do not retain an active actor key', () => {
  const input = replayV1FixtureInput();
  input.ticks[0]!.state[0]!.status = 'Destroyed';

  const unit = decodeReplay(input).replay.ticks[0]!.after.units.find(
    (candidate) => candidate.teamId === input.ticks[0]!.state[0]!.slot,
  );

  assert.equal(unit?.lifecycleStatus, 'destroyed');
  assert.equal(unit?.activeActorKey, null);
});

test('replay-v1 requires an explicit partial discriminator when final fields are absent', () => {
  const input = replayV1LivePartialFixtureInput() as {
    partial?: true;
  };
  delete input.partial;

  assert.throws(
    () => decodeReplay(input),
    /result: missing required property/,
  );
});

test('finalized replay-v1 still requires its result and replay hash', () => {
  const missingResult = replayV1FixtureInput() as unknown as {
    result?: unknown;
  };
  delete missingResult.result;
  assert.throws(
    () => decodeReplay(missingResult),
    /result: missing required property/,
  );

  const missingHash = replayV1FixtureInput() as unknown as {
    replayHash?: unknown;
  };
  delete missingHash.replayHash;
  assert.throws(
    () => decodeReplay(missingHash),
    /replayHash: missing required property/,
  );

  const explicitCompleteFlag = {
    ...replayV1FixtureInput(),
    partial: false,
  };
  assert.throws(
    () => decodeReplay(explicitCompleteFlag),
    /complete documents omit the partial property/,
  );
});

test('replay-v2 keeps stable units while adding exact fabricated lives', () => {
  const replay = decodeReplayJson(
    replayFixtureText('frontline-replay-v2.json'),
  ).replay;
  const opening = replay.ticks[0];
  const fabricated = replay.ticks[2];
  const nextTurn = replay.ticks[3];

  assert.equal(
    opening?.before.actors[0]?.actorKey,
    'frontline:0:unit:0:life:0',
  );
  assert.equal(
    fabricated?.before.actors.find(
      (actor) =>
        actor.identity.teamId === 0 &&
        actor.identity.unitId === 1,
    )?.actorKey,
    'frontline:0:unit:1:life:0',
  );
  assert.equal(
    fabricated?.before.actors.find(
      (actor) =>
        actor.identity.teamId === 0 &&
        actor.identity.unitId === 1,
    )?.unitKey,
    nextTurn?.before.actors.find(
      (actor) =>
        actor.identity.teamId === 0 &&
        actor.identity.unitId === 1,
    )?.unitKey,
  );
  assert.equal(
    replay.units.find(
      (unit) => unit.teamId === 0 && unit.unitId === 1,
    )?.initialActorKey,
    null,
  );
});

test('replay-v2 keeps opaque observation handles separate from exact event identities', () => {
  const replay = decodeReplayJson(
    replayFixtureText('frontline-replay-v2.json'),
  ).replay;
  const normalized = replay.ticks[11]!.actorTurns.find(
    (turn) => turn.actor.teamId === 0,
  )!;
  const event = normalized.observation.visibleEvents.find(
    (candidate) => candidate.type === 'shot',
  )!;

  assert.equal(event.eventHandle, 'event-11');
  assert.equal(event.projectileHandle, 'projectile-0');
  assert.equal(
    normalized.aliases.events.find(
      (alias) => alias.eventHandle === event.eventHandle,
    )?.eventId,
    'resolution:10:0',
  );
  assert.equal(
    normalized.aliases.projectiles.find(
      (alias) =>
        alias.projectileHandle === event.projectileHandle,
    )?.projectileId,
    '0',
  );
});

test('replay-v2 keeps unsafe seed, projectile, score, and damage totals exact', () => {
  const replay = decodeReplay(replayV2FixtureInput()).replay;

  assert.equal(replay.seed, JS_UNSAFE_DECIMAL);
  assert.equal(
    replay.ticks[0]?.actorTurns[0]?.lifeStart?.actorRandomSeed,
    JS_UNSAFE_DECIMAL,
  );
  assert.equal(
    replay.ticks[0]?.before.projectiles?.[0]?.projectileId,
    JS_UNSAFE_DECIMAL,
  );
  assert.equal(
    replay.ticks[0]?.before.teams[0]?.damageDealt,
    JS_UNSAFE_DECIMAL,
  );
  assert.equal(replay.result?.teams[0]?.damageDealt, JS_UNSAFE_DECIMAL);
  assert.equal(replay.result?.territorialScore, `-${JS_UNSAFE_DECIMAL}`);
});

test('replay-v2 preserves exact terminal unit rows and rejects final-world drift', () => {
  const replay = decodeReplay(replayV2FixtureInput()).replay;

  assert.deepEqual(replay.result?.teams[0]?.units, [
    {
      unitKey: 'frontline:0:unit:0',
      teamId: 0,
      unitId: 0,
      defaultFormId: 'prime',
      formId: 'prime',
      lifecycleStatus: 'active',
      activeActor: {
        kind: 'frontline',
        teamId: 0,
        unitId: 0,
        lifeId: 0,
        unitKey: 'frontline:0:unit:0',
        actorKey: 'frontline:0:unit:0:life:0',
      },
      activeActorKey: 'frontline:0:unit:0:life:0',
      health: 5,
      damageDealt: JS_UNSAFE_DECIMAL,
      pendingFormTransition: null,
    },
  ]);

  const missingUnit = replayV2FixtureInput();
  missingUnit.result.teams[0]!.units = [];
  assert.throws(
    () => decodeReplay(missingUnit),
    /must cover exactly the topology units/,
  );

  const staleHealth = replayV2FixtureInput();
  staleHealth.result.teams[0]!.units[0]!.health = 4;
  assert.throws(
    () => decodeReplay(staleHealth),
    /unit 0:0 differs from the final world/,
  );
});

test('replay-v2 carries fabrication masks, queue state, and causal timing exactly', () => {
  const input = replayV2FabricationFixtureInput();
  const replay = decodeReplay(input).replay;
  const turn = replay.ticks[0]!.actorTurns[0]!;
  const fabrication = turn.observation.actions?.find(
    (action) => action.actionId === 'fabricate',
  );
  const child = replay.ticks[0]!.after.units.find(
    (unit) => unit.unitId === 1,
  );
  const queued = replay.ticks[0]!.events.find(
    (event) => event.type === 'fabrication-queued',
  );

  assert.deepEqual(fabrication?.allowedUnitKeys, [
    'frontline:0:unit:1',
  ]);
  assert.equal(child?.lifecycleStatus, 'fabrication-queued');
  assert.deepEqual(child?.reservedSpawn, { x: 0, y: 2 });
  assert.equal(child?.pendingSpawnReason, 'fabrication');
  assert.equal(child?.fabricationAtTick, 1);
  assert.equal(queued?.unitId, 1);
  assert.equal(queued?.sourceActor?.actorKey, 'frontline:0:unit:0:life:0');
  assert.equal(queued?.actionPayload?.unitKey, 'frontline:0:unit:1');

  // The exact mask is gated by the Prime's authoritative home-pad position.
  const unavailable = replayV2FabricationFixtureInput();
  unavailable.ticks[0]!.tickStart.state.teams[0]!.units[0]!.activeLife!
    .position = { x: 1, y: 2 };
  unavailable.ticks[0]!.actors[0]!.observation.self.position = {
    x: 1,
    y: 2,
  };
  const unavailableAction =
    unavailable.ticks[0]!.actors[0]!.observation.actions.find(
      (action) => action.actionId === 'fabricate',
    )!;
  unavailableAction.available = false;
  unavailableAction.allowedUnitTargets = [];
  assert.doesNotThrow(() => decodeReplay(unavailable));

  const activeTarget = replayV2FabricationFixtureInput();
  const invalidAction =
    activeTarget.ticks[0]!.actors[0]!.observation.actions.find(
      (action) => action.actionId === 'fabricate',
    )!;
  invalidAction.allowedUnitTargets = [{ teamId: 0, unitId: 0 }];
  assert.throws(
    () => decodeReplay(activeTarget),
    /fabrication mask must exactly match/,
  );

  const unavailableDespiteReadyTarget = replayV2FabricationFixtureInput();
  unavailableDespiteReadyTarget.ticks[0]!.actors[0]!.observation.actions.find(
    (action) => action.actionId === 'fabricate',
  )!.available = false;
  assert.throws(
    () => decodeReplay(unavailableDespiteReadyTarget),
    /fabrication mask must exactly match/,
  );

  const wrongQueueTiming = replayV2FabricationFixtureInput();
  wrongQueueTiming.ticks[0]!.resolution.events.find(
    (event) => event.type === 'fabrication-queued',
  )!.fabricationAtTick = 2;
  assert.throws(
    () => decodeReplay(wrongQueueTiming),
    /invalid fabrication-queued lifecycle payload/,
  );

  const extraQueueParameter = replayV2FabricationFixtureInput();
  extraQueueParameter.ticks[0]!.resolution.events.find(
    (event) => event.type === 'fabrication-queued',
  )!.actionPayload!.direction = 'north';
  assert.throws(
    () => decodeReplay(extraQueueParameter),
    /inconsistent with action fabricate/,
  );

  const missingQueueSource = replayV2FabricationFixtureInput();
  missingQueueSource.ticks[0]!.resolution.events.find(
    (event) => event.type === 'fabrication-queued',
  )!.sourceActorId = null;
  assert.throws(
    () => decodeReplay(missingQueueSource),
    /invalid fabrication-queued lifecycle payload/,
  );

  const incompleteQueueSelector = replayV2FabricationFixtureInput();
  incompleteQueueSelector.ticks[0]!.resolution.events.find(
    (event) => event.type === 'fabrication-queued',
  )!.actionCode = null;
  assert.throws(
    () => decodeReplay(incompleteQueueSelector),
    /action selector must contain both ID and code/,
  );
});

test('replay-v2 rejects frozen Frontline advance and deployment-form drift', () => {
  const duplicateAdvanceDirection = replayV2FixtureInput();
  duplicateAdvanceDirection.header.contract.rules.frontlineDefinition!
    .victory.teamAdvances[1]!.positionIndexDelta = 1;
  assert.throws(
    () => decodeReplay(duplicateAdvanceDirection),
    /map the two topology teams uniquely to -1 and \+1/,
  );

  const wrongPrimeForm = replayV2FixtureInput();
  wrongPrimeForm.ticks[0]!.tickStart.state.teams[0]!.units[0]!
    .defaultFormId = 'child';
  assert.throws(
    () => decodeReplay(wrongPrimeForm),
    /must equal the deployment default/,
  );
});

test('replay-v2 rejects noncanonical Frontline control states', () => {
  const progressWithoutClaim = replayV2FixtureInput();
  progressWithoutClaim.ticks[0]!.tickStart.state.objective.captureProgress =
    1;
  assert.throws(
    () => decodeReplay(progressWithoutClaim),
    /Frontline control state violates canonical invariants/,
  );

  const winnerWithFutureResume = replayV2FixtureInput();
  winnerWithFutureResume.ticks[0]!.tickStart.state.objective.winnerTeamId =
    0;
  winnerWithFutureResume.ticks[0]!.tickStart.state.objective
    .controlResumesAtTick = 1;
  assert.throws(
    () => decodeReplay(winnerWithFutureResume),
    /Frontline control state violates canonical invariants/,
  );
});

test('replay-v2 freezes transform and turret-fire contract semantics', () => {
  const wrongTransformCode = replayV2FixtureInput();
  wrongTransformCode.header.contract.rules.actions.find(
    (action) => action.id === 'transform',
  )!.code = 100;
  assert.throws(
    () => decodeReplay(wrongTransformCode),
    /canonical enabled Transform\/101/,
  );

  const missingHeading = replayV2FixtureInput();
  missingHeading.header.contract.rules.frontlineDefinition!
    .turretFire.allowedProjectileHeadings.pop();
  assert.throws(
    () => decodeReplay(missingHeading),
    /all eight canonical headings/,
  );

  const weightedTurret = replayV2FixtureInput();
  weightedTurret.header.contract.rules.forms.find(
    (form) => form.id === 'turret',
  )!.objectiveWeight = 1;
  assert.throws(
    () => decodeReplay(weightedTurret),
    /zero-objective-weight target form/,
  );

  const mobileTurret = replayV2FixtureInput();
  mobileTurret.header.contract.rules.forms
    .find((form) => form.id === 'turret')!
    .allowedActionIds.push('transform');
  assert.throws(
    () => decodeReplay(mobileTurret),
    /exactly ShootDirection and Wait/,
  );

  const completionBeforeObjective = replayV2FixtureInput();
  const phases =
    completionBeforeObjective.header.contract.rules.tickResolution
      .phases;
  const completion = phases.indexOf('complete-form-transitions');
  const objective = phases.indexOf('update-objective');
  [phases[completion], phases[objective]] = [
    phases[objective]!,
    phases[completion]!,
  ];
  assert.throws(
    () => decodeReplay(completionBeforeObjective),
    /complete them after objective resolution/,
  );
});

test('replay-v2 exposes a complete normalized public match contract', () => {
  const replay = decodeReplay(replayV2FixtureInput()).replay;

  assert.equal(replay.contract.kind, 'v2-full');
  if (replay.contract.kind !== 'v2-full') return;
  const { rules, map, topology } = replay.contract;

  assert.equal(replay.contract.schemaVersion, 1);
  assert.equal(
    replay.contract.matchContractFingerprint,
    'contract-fingerprint',
  );
  assert.equal(rules.rulesFingerprint, 'rules-fingerprint');
  assert.equal(rules.limits.maxUnitsPerTeam, 1);
  assert.deepEqual(rules.objective.maxTickTiebreakers, [
    'objective',
    'health',
    'damage-dealt',
  ]);
  assert.equal(rules.frontlineDefinition?.teamPerception, 'immediate-union');
  assert.equal(rules.frontlineDefinition?.capture.threshold, 3);
  assert.equal(
    rules.frontlineDefinition?.capture.presence,
    'binary-positive-weight-per-team-no-stacking',
  );
  assert.equal(
    rules.frontlineDefinition?.victory.timeoutResolution,
    'signed-position-threshold-plus-claim-zero-draw-no-tiebreakers',
  );
  assert.equal(rules.frontlineDefinition?.lifecycle.primeRespawnTicks, 2);
  assert.equal(
    rules.frontlineDefinition?.deployment.childReturn,
    'ready-then-explicit-fabrication',
  );
  assert.equal(
    rules.frontlineDefinition?.deployment.primeDefaultFormId,
    'prime',
  );
  assert.equal(rules.frontlineDefinition?.fabrication.enabled, false);
  assert.equal(
    rules.frontlineDefinition?.fabrication.capacityEvaluation,
    'post-movement-during-queue-fabrications',
  );
  assert.equal(rules.frontlineDefinition?.anchor.windupTicks, 2);
  assert.equal(
    rules.frontlineDefinition?.anchor.death,
    'cancels-with-explicit-event',
  );
  assert.equal(
    rules.frontlineDefinition?.anchor.pendingForm,
    'source-form-until-completion',
  );
  assert.deepEqual(
    rules.frontlineDefinition?.turretFire.allowedProjectileHeadings,
    [
      'north',
      'north-east',
      'east',
      'south-east',
      'south',
      'south-west',
      'west',
      'north-west',
    ],
  );
  assert.equal(
    rules.frontlineDefinition?.turretFire.facing,
    'body-facing-unchanged',
  );
  assert.equal(
    rules.frontlineDefinition?.alliedCombat.friendlyFireEnabled,
    false,
  );
  assert.equal(rules.energy.enabled, false);
  assert.deepEqual(rules.forms[0]?.allowedActionIds, [
    'transform',
    'wait',
  ]);
  assert.deepEqual(rules.actions[0]?.parameterKinds, []);
  assert.equal(rules.projectiles.mode, 'discrete');
  assert.equal(rules.shotPrograms.maxBendCount, 2);
  assert.equal(rules.vision.lineOfSight, 'corner-strict-supercover');
  assert.equal(rules.collisions.unitsBlockUnits, true);
  assert.deepEqual(rules.tickResolution.phases, [
    'queue-fabrications',
    'freeze-observations',
    'start-form-transitions',
    'launch-shots-and-apply-damage',
    'update-objective',
    'complete-form-transitions',
    'resolve-match-completion',
  ]);
  assert.equal(map.mapFingerprint, 'map-fingerprint');
  assert.deepEqual(map.spawns[0], {
    teamId: 0,
    position: { x: 0, y: 1 },
    facing: 'east',
  });
  assert.deepEqual(map.frontline?.positions[0]?.tiles, [{ x: 1, y: 1 }]);
  assert.equal(topology.teamCount, 2);
  assert.equal(topology.unitSlots[0]?.unitKey, 'frontline:0:unit:0');
  assert.equal(
    topology.initialLives[0]?.actorKey,
    'frontline:0:unit:0:life:0',
  );
});

test('replay-v2 canonicalizes numeric string IDs without mutating wire order', () => {
  const input = replayV2FixtureInput();
  const template = input.ticks[0]!.tickStart.state.projectiles[0]!;
  input.ticks[0]!.tickStart.state.projectiles = [
    { ...template, projectileId: '10' },
    { ...template, projectileId: '2' },
    template,
  ];
  const wireOrder = input.ticks[0]!.tickStart.state.projectiles.map(
    (projectile) => projectile.projectileId,
  );

  const decoded = decodeReplay(input);

  assert.deepEqual(wireOrder, ['10', '2', JS_UNSAFE_DECIMAL]);
  assert.deepEqual(
    decoded.replay.ticks[0]?.before.projectiles?.map(
      (projectile) => projectile.projectileId,
    ),
    ['2', '10', JS_UNSAFE_DECIMAL],
  );
  assert.deepEqual(
    input.ticks[0]!.tickStart.state.projectiles.map(
      (projectile) => projectile.projectileId,
    ),
    wireOrder,
  );
});

test('replay-v2 preserves null separately from supported-but-empty arrays', () => {
  const observation =
    decodeReplay(replayV2FixtureInput()).replay.ticks[0]?.actorTurns[0]
      ?.observation;
  const action = observation?.actions?.[0];

  assert.equal(observation?.visibleProjectiles, null);
  assert.deepEqual(observation?.heardSounds, []);
  assert.equal(action?.allowedDirections, null);
  assert.deepEqual(action?.allowedUnitKeys, []);
  assert.equal(action?.allowedFormTargets, null);
  assert.deepEqual(
    decodeReplay(replayV2FixtureInput()).replay.ticks[0]?.actorTurns[0]
      ?.runtimeReply.payload,
    null,
  );
});

test('replay-v2 preserves runtime, accepted, and resolved generic payloads independently', () => {
  const input = replayV2FixtureInput();
  input.header.contract.rules.forms.push({
    ...input.header.contract.rules.forms[0]!,
    id: 'flight',
    allowedActionIds: ['future-flight'],
  });
  input.header.contract.rules.actions.push({
    id: 'future-flight',
    code: 9_007,
    kind: 'attack',
    parameterKinds: ['direction', 'unit-target', 'form-target'],
    enabled: true,
  });
  input.header.contract.rules.forms
    .find((form) => form.id === 'prime')!
    .allowedActionIds.push('future-flight');
  for (const actor of input.ticks[0]!.actors) {
    actor.observation.actions.push({
      actionId: 'future-flight',
      actionCode: 9_007,
      parameterKinds: ['direction', 'unit-target', 'form-target'],
      enabled: true,
      available: true,
      shotProgramAvailable: null,
      allowedDirections: ['north'],
      allowedProjectileHeadings: null,
      allowedUnitTargets: [{ teamId: 0, unitId: 0 }],
      allowedFormTargets: ['flight'],
    });
  }
  const turn = input.ticks[0]!.actors[0]!;
  turn.runtimeReply = {
    actionId: 'future-flight',
    actionCode: 9_007,
    payload: {
      shotProgram: null,
      direction: 'north',
      launchHeading: null,
      unitTarget: null,
      formTargetId: 'flight',
    },
    debugMessage: 'raw runtime reply',
    faulted: false,
    faultMessage: null,
  };
  turn.acceptedDecision = {
    actionId: 'wait',
    actionCode: 0,
    payload: null,
    debugMessage: null,
    faulted: false,
    faultMessage: null,
  };
  input.ticks[0]!.resolution.events = [
    {
      eventId: 'resolution:0:0',
      tick: 0,
      type: 'shot',
      teamId: 0,
      unitId: null,
      sourceActorId: turn.actorId,
      targetActorId: null,
      projectileId: JS_UNSAFE_DECIMAL,
      from: { x: 0, y: 1 },
      to: { x: 1, y: 1 },
      fromFacing: 'east',
      toFacing: null,
      projectileHeading: 'east',
      actionId: 'future-flight',
      actionCode: 9_007,
      actionPayload: {
        shotProgram: null,
        direction: 'north',
        launchHeading: null,
        unitTarget: { teamId: 0, unitId: 0 },
        formTargetId: 'flight',
      },
      actionResult: 'success',
      fromFormId: null,
      toFormId: null,
      formTransitionStartedAtTick: null,
      formTransitionCompletesAtTick: null,
      amount: null,
      newHealth: null,
      lifecycleStatus: null,
      spawnReason: null,
      respawnAtTick: null,
      unlockAtTick: null,
      rebuildReadyAtTick: null,
      fabricationAtTick: null,
      fromPositionIndex: null,
      toPositionIndex: null,
      claimingTeamId: null,
      captureProgress: null,
      controlResumesAtTick: null,
    },
  ];

  const normalized = decodeReplay(input).replay.ticks[0]!;

  assert.equal(normalized.actorTurns[0]?.runtimeReply.actionId, 'future-flight');
  assert.equal(normalized.actorTurns[0]?.acceptedDecision.actionId, 'wait');
  assert.equal(normalized.actorTurns[0]?.acceptedDecision.payload, null);
  assert.deepEqual(normalized.events[0]?.actionPayload, {
    shotProgram: null,
    direction: 'north',
    launchHeading: null,
    unitKey: 'frontline:0:unit:0',
    formTargetId: 'flight',
  });
});

test('replay-v2 exposes authoritative before and after snapshots', () => {
  const tick = decodeReplay(replayV2FixtureInput()).replay.ticks[0];

  assert.equal(tick?.before.completeness, 'exact');
  assert.equal(tick?.after.completeness, 'exact');
  assert.deepEqual(tick?.before.actors[0]?.position, { x: 0, y: 1 });
  assert.deepEqual(tick?.after.actors[0]?.position, { x: 1, y: 1 });
  assert.notStrictEqual(tick?.before, tick?.after);
});

test('zero-tick replay-v2 partial retains topology without inventing world state', () => {
  const replay = decodeReplay(replayV2ZeroTickPartialFixtureInput()).replay;

  assert.equal(replay.partial, true);
  assert.equal(replay.replayHash, null);
  assert.equal(replay.result, null);
  assert.equal(replay.initialWorld, null);
  assert.deepEqual(replay.ticks, []);
  assert.equal(replay.teams.length, 2);
  assert.equal(replay.units.length, 2);
});

test('replay-v2 requires explicit nullable keys instead of treating omission as null', () => {
  const input = structuredClone(replayV2FixtureInput()) as unknown as {
    ticks: {
      actors: {
        observation: { visibleProjectiles?: unknown };
      }[];
    }[];
  };
  delete input.ticks[0]!.actors[0]!.observation.visibleProjectiles;

  assert.throws(
    () => decodeReplay(input),
    /visibleProjectiles: missing required property/,
  );
});

test('replay-v3 normalizes the Engine golden without collapsing unit and life identity', () => {
  const raw = replayV3FixtureText();
  const decoded = decodeReplayJson(raw);
  const replay = decoded.replay;

  assert.equal(decoded.replayVersion, 3);
  assert.equal(replay.sourceVersion, 3);
  assert.equal(JSON.stringify(decoded.wire), raw);
  assert.equal(decoded.rawJson, raw);
  assert.deepEqual(
    replay.units.map((unit) => unit.unitKey),
    ['generic:0:unit:0', 'generic:1:unit:0'],
  );
  assert.deepEqual(
    replay.initialWorld?.actors.map((actor) => actor.actorKey),
    [
      'generic:0:unit:0:life:0',
      'generic:1:unit:0:life:0',
    ],
  );
  assert.deepEqual(
    replay.initialLifeStarts?.map((start) => ({
      actorKey: start.actor.actorKey,
      participantId: start.participantId,
      generation: start.generation,
    })),
    [
      {
        actorKey: 'generic:0:unit:0:life:0',
        participantId: 10,
        generation: 0,
      },
      {
        actorKey: 'generic:1:unit:0:life:0',
        participantId: 20,
        generation: 0,
      },
    ],
  );
  assert.deepEqual(
    replay.initialWorld?.scoreboard?.teams[0]?.scores,
    [
      { channel: 'kills', value: '0' },
      { channel: 'deaths', value: '0' },
      { channel: 'damage-dealt', value: '0' },
      { channel: 'active-health', value: '3' },
    ],
  );
  assert.deepEqual(replay.result?.mode, {
    kind: 'deathmatch',
    reason: 'max-ticks',
    scores: [
      {
        teamKey: 'team:0',
        teamId: 0,
        kills: '0',
        deaths: '0',
        damageDealt: '1',
      },
      {
        teamKey: 'team:1',
        teamId: 1,
        kills: '0',
        deaths: '0',
        damageDealt: '1',
      },
    ],
  });
  assert.equal(replay.contract.kind, 'v3-generic');
  if (replay.contract.kind === 'v3-generic') {
    assert.equal(replay.contract.modeKind, 'deathmatch');
    assert.equal(replay.contract.rawContract.format.participantCount, 2);
  }
});

test('replay-v3 accepts a declared automatic return at the tick-start boundary', () => {
  const fixture = replayV3FixtureInput();
  const before = structuredClone(fixture.initialFrame.state);
  const returned = structuredClone(fixture.initialFrame.state);
  const priorActorId = { teamId: 0, unitId: 0, lifeId: 0 };
  const returnedActorId = { teamId: 0, unitId: 0, lifeId: 1 };

  const beforeSlot = before.slots.find(
    (slot) => slot.teamId === 0 && slot.unitId === 0,
  )!;
  beforeSlot.state = {
    kind: 'automatic-return-pending',
    dueTick: 0,
    targetFormId: 'mobile',
    generation: 0,
  };
  beforeSlot.pendingParentActorId = priorActorId;
  before.activeLives = before.activeLives.filter(
    (life) => life.actorId.teamId !== 0,
  );
  before.scoreboard.teams[0]!.scores.find(
    (score) => score.channel === 'active-health',
  )!.value = '0';

  const returnedSlot = returned.slots.find(
    (slot) => slot.teamId === 0 && slot.unitId === 0,
  )!;
  returnedSlot.nextLifeId = 2;
  if (returnedSlot.state.kind !== 'active') {
    assert.fail('expected an active fixture slot');
  }
  returnedSlot.state.actorId = returnedActorId;
  const returnedLife = returned.activeLives.find(
    (life) => life.actorId.teamId === 0,
  )!;
  returnedLife.actorId = returnedActorId;
  returnedLife.spawnReason = 'automatic-return';
  returnedLife.parentActorId = priorActorId;

  const start = structuredClone(fixture.initialFrame.lifeStarts[0]!);
  start.actorId = returnedActorId;
  start.origin.reason = 'automatic-return';
  start.origin.parentActorId = priorActorId;
  const event = structuredClone(fixture.initialFrame.events[0]!);
  if (event.payload.kind !== 'life-spawned') {
    assert.fail('expected a LifeSpawned fixture event');
  }
  event.payload.actorId = returnedActorId;
  event.payload.reason = 'automatic-return';
  event.payload.parentActorId = priorActorId;

  const tickStart = {
    tick: 0,
    state: returned,
    activeActorIds: returned.activeLives.map((life) => life.actorId),
    lifeStarts: [start],
    events: [event],
    traversals: [],
  };
  const fail = (path: string, message: string): never => {
    throw new ReplayDecodeError(`${path}: ${message}`);
  };

  assert.doesNotThrow(() =>
    validateReplayV3TickStartBoundary(
      before,
      tickStart,
      'replay.ticks[0].tickStart.state',
      fail,
    ),
  );

  const unexplained = structuredClone(tickStart);
  unexplained.state.mode = {
    kind: 'deathmatch',
    modeId: 'undeclared-mode-change',
  };
  assert.throws(
    () =>
      validateReplayV3TickStartBoundary(
        before,
        unexplained,
        'replay.ticks[0].tickStart.state',
        fail,
      ),
    /cannot change participants, mode, projectile issuance/,
  );
});

test('replay-v3 accepts a declared Split cancellation at the tick-start boundary', () => {
  const fixture = replayV3FixtureInput();
  const before = structuredClone(fixture.initialFrame.state);
  const after = structuredClone(fixture.initialFrame.state);
  const source = before.activeLives[0]!;
  const operationId = 'split:cancelled-before-completion';
  before.pendingReplications = [
    {
      sourceActorId: source.actorId,
      participantId: source.participantId,
      sourceGeneration: source.generation,
      sourceFormId: source.formId,
      sourcePosition: source.position,
      sourceFacing: source.facing,
      transitionId: 'split-prime',
      operationId,
      queuedTick: -1,
      dueTick: 0,
      descendants: [],
    },
  ];
  const cancellation = {
    eventHandle: 'authoritative-event:split-cancelled',
    tick: 0,
    globalOrdinal: '0',
    sourceOrdinal: 0,
    kind: 'lifecycle-cancelled',
    payload: {
      kind: 'lifecycle',
      transitionId: 'split-prime',
      operationId,
      sourceActorId: source.actorId,
      targetTeamId: source.actorId.teamId,
      targetUnitId: 1,
      dueTick: 0,
      cancellationReason: 'insufficient-health',
    },
    audience: {
      kind: 'spatial',
      primaryPosition: source.position,
    },
  } as const;
  const tickStart = {
    tick: 0,
    state: after,
    activeActorIds: after.activeLives.map((life) => life.actorId),
    lifeStarts: [],
    events: [cancellation],
    traversals: [],
  };
  const fail = (path: string, message: string): never => {
    throw new ReplayDecodeError(`${path}: ${message}`);
  };

  assert.doesNotThrow(() =>
    validateReplayV3TickStartBoundary(
      before,
      tickStart,
      'replay.ticks[0].tickStart.state',
      fail,
    ),
  );

  const unexplained = structuredClone(tickStart);
  unexplained.events = [];
  assert.throws(
    () =>
      validateReplayV3TickStartBoundary(
        before,
        unexplained,
        'replay.ticks[0].tickStart.state',
        fail,
      ),
    /pending replication state changed without resolution evidence/,
  );
});

test('replay-v3 Deathmatch rejects illegal endings, counter drift, and standing drift', () => {
  const fixture = () => {
    const input = replayV3FixtureInput();
    if (input.result?.mode.kind !== 'deathmatch') {
      assert.fail('expected Deathmatch replay-v3 fixture');
    }
    return input;
  };

  const unknownReason = fixture() as unknown as {
    result: {
      completionReason: string;
      mode: { reason: string };
    };
  };
  unknownReason.result.completionReason = 'future-ending';
  unknownReason.result.mode.reason = 'future-ending';
  assert.throws(
    () => decodeReplay(unknownReason),
    /unknown deathmatch end reason/,
  );

  const impossibleKillLimit = fixture();
  impossibleKillLimit.result!.completionReason = 'kill-limit';
  impossibleKillLimit.result!.mode.reason = 'kill-limit';
  assert.throws(
    () => decodeReplay(impossibleKillLimit),
    /kill-limit requires multiple eligible teams and a configured reached kill threshold/,
  );

  const counterDrift = fixture();
  counterDrift.result!.mode.scores[0]!.kills = '999';
  assert.throws(
    () => decodeReplay(counterDrift),
    /must match the final kills scoreboard value/,
  );

  const standingDrift = fixture();
  standingDrift.result!.standings.winnerTeamId = 0;
  standingDrift.result!.standings.teams[0]!.outcome = 'win';
  standingDrift.result!.standings.teams[1]!.rank = 2;
  standingDrift.result!.standings.teams[1]!.outcome = 'loss';
  assert.throws(
    () => decodeReplay(standingDrift),
    /does not follow deathmatch eligibility and victory ranking/,
  );

  const reversedEligibility = fixture();
  reversedEligibility.result!.eligibleTeamIds.reverse();
  assert.throws(
    () => decodeReplay(reversedEligibility),
    /must be in canonical ascending team order/,
  );

  const pastMaximum = fixture();
  pastMaximum.header.contract.rules.limits.maxTicks = 1;
  assert.throws(
    () => decodeReplay(pastMaximum),
    /cannot extend beyond the configured maximum tick boundary/,
  );

  const timeoutAfterKillLimit = fixture();
  if (timeoutAfterKillLimit.header.contract.rules.gameMode.kind !== 'deathmatch') {
    assert.fail('expected Deathmatch rules');
  }
  timeoutAfterKillLimit.header.contract.rules.gameMode.victory.killsToWin = 1;
  const finalWorld = timeoutAfterKillLimit.ticks.at(-1)!.postState;
  finalWorld.scoreboard.teams[0]!.scores.find(
    (score) => score.channel === 'kills',
  )!.value = '1';
  timeoutAfterKillLimit.result!.mode.scores[0]!.kills = '1';
  timeoutAfterKillLimit.result!.standings.teams[0]!.scores.find(
    (score) => score.channel === 'kills',
  )!.value = '1';
  assert.throws(
    () => decodeReplay(timeoutAfterKillLimit),
    /with no reached kill limit/,
  );

  const unknownScoringPolicy = fixture();
  if (
    unknownScoringPolicy.header.contract.rules.gameMode.kind !==
    'deathmatch'
  ) {
    assert.fail('expected Deathmatch rules');
  }
  unknownScoringPolicy.header.contract.rules.gameMode.scoring.deathIncrement =
    'future-policy';
  assert.throws(
    () => decodeReplay(unknownScoringPolicy),
    /one-raw-death-to-destroyed-actor-team/,
  );

  const unknownTerminalPrecedence = fixture();
  if (
    unknownTerminalPrecedence.header.contract.rules.gameMode.kind !==
    'deathmatch'
  ) {
    assert.fail('expected Deathmatch rules');
  }
  unknownTerminalPrecedence.header.contract.rules.gameMode.victory
    .terminalTickPrecedence = 'future-precedence';
  assert.throws(
    () => decodeReplay(unknownTerminalPrecedence),
    /supported Deathmatch completion precedence/,
  );

  const wrongPrimaryRanking = fixture();
  if (
    wrongPrimaryRanking.header.contract.rules.gameMode.kind !==
    'deathmatch'
  ) {
    assert.fail('expected Deathmatch rules');
  }
  wrongPrimaryRanking.header.contract.rules.gameMode.victory
    .timeoutRanking[0] = {
      channel: 'damage-dealt',
      direction: 'higher-wins',
    };
  assert.throws(
    () => decodeReplay(wrongPrimaryRanking),
    /must begin with higher kills/,
  );

  const wrongScoreDomain = fixture();
  if (
    wrongScoreDomain.header.contract.rules.gameMode.kind !==
    'deathmatch'
  ) {
    assert.fail('expected Deathmatch rules');
  }
  wrongScoreDomain.header.contract.rules.gameMode.scoreCatalog[0]!.domain =
    'signed';
  assert.throws(
    () => decodeReplay(wrongScoreDomain),
    /non-negative domains/,
  );

  const duplicateRanking = fixture();
  if (
    duplicateRanking.header.contract.rules.gameMode.kind !==
    'deathmatch'
  ) {
    assert.fail('expected Deathmatch rules');
  }
  duplicateRanking.header.contract.rules.gameMode.victory.timeoutRanking[1] =
    {
      channel: 'kills',
      direction: 'lower-wins',
    };
  assert.throws(
    () => decodeReplay(duplicateRanking),
    /must be unique and reference a declared Deathmatch score channel/,
  );
});

test('replay-v3 Frontline normalizes typed rules, ordered geometry, control, and signed terminal scores', () => {
  const replay = decodeReplay(
    adaptReplayV3ToFrontline(replayV3FixtureInput()),
  ).replay;

  assert.equal(replay.contract.kind, 'v3-generic');
  if (replay.contract.kind !== 'v3-generic') {
    assert.fail('expected replay-v3 generic contract');
  }
  assert.deepEqual(replay.contract.mode, {
    kind: 'frontline',
    modeId: 'frontline',
    frontlinePositionCount: 3,
    pushesToBreach: 2,
    capture: {
      threshold: 3,
      gainPerSoleTeamTick: 1,
      decayAmount: 1,
      decayIntervalTicks: 2,
      redeployPauseTicks: 1,
    },
    orderedObjectiveRegionIds: [
      'frontline-low',
      'frontline-centre',
      'frontline-high',
    ],
    teamAdvances: [
      { teamId: 0, positionIndexDelta: 1 },
      { teamId: 1, positionIndexDelta: -1 },
    ],
  });
  assert.deepEqual(replay.map.frontline?.positions, [
    { positionIndex: 0, tiles: [{ x: 3, y: 3 }] },
    { positionIndex: 1, tiles: [{ x: 4, y: 3 }] },
    { positionIndex: 2, tiles: [{ x: 5, y: 3 }] },
  ]);
  assert.deepEqual(replay.result?.mode, {
    kind: 'frontline',
    reason: 'max-ticks',
    control: {
      kind: 'frontline',
      modeId: 'frontline',
      activePositionIndex: 1,
      claimingTeamId: null,
      captureProgress: 0,
      decayTicksElapsed: 0,
      controlResumesAtTick: 0,
    },
    scores: [
      {
        teamKey: 'team:0',
        teamId: 0,
        territorialProgress: '0',
      },
      {
        teamKey: 'team:1',
        teamId: 1,
        territorialProgress: '0',
      },
    ],
  });
});

test('replay-v3 Frontline preserves and strictly validates a phased capture-gain schedule', () => {
  const fixture = () => {
    const input = adaptReplayV3ToFrontline(replayV3FixtureInput());
    if (input.header.contract.rules.gameMode.kind !== 'frontline') {
      assert.fail('expected Frontline rules');
    }
    input.header.contract.rules.gameMode.capture.gainSchedule = [
      {
        phaseId: 'opening',
        startsAtTick: 0,
        gainPerSoleTeamTick: 1,
      },
      {
        phaseId: 'late-escalation',
        startsAtTick: 1,
        gainPerSoleTeamTick: 2,
      },
    ];
    return input;
  };

  const replay = decodeReplay(fixture()).replay;
  if (
    replay.contract.kind !== 'v3-generic' ||
    replay.contract.mode.kind !== 'frontline'
  ) {
    assert.fail('expected generic Frontline contract');
  }
  assert.deepEqual(replay.contract.mode.capture.gainSchedule, [
    {
      phaseId: 'opening',
      startsAtTick: 0,
      gainPerSoleTeamTick: 1,
    },
    {
      phaseId: 'late-escalation',
      startsAtTick: 1,
      gainPerSoleTeamTick: 2,
    },
  ]);

  const empty = fixture();
  if (empty.header.contract.rules.gameMode.kind !== 'frontline') {
    assert.fail('expected Frontline rules');
  }
  empty.header.contract.rules.gameMode.capture.gainSchedule = [];
  assert.throws(
    () => decodeReplay(empty),
    /gainSchedule: must be omitted instead of emitted empty/,
  );

  const duplicateStart = fixture();
  if (duplicateStart.header.contract.rules.gameMode.kind !== 'frontline') {
    assert.fail('expected Frontline rules');
  }
  duplicateStart.header.contract.rules.gameMode.capture.gainSchedule![1]!
    .startsAtTick = 0;
  assert.throws(
    () => decodeReplay(duplicateStart),
    /must be strictly increasing, non-negative, and before maxTicks/,
  );

  const invalidId = fixture();
  if (invalidId.header.contract.rules.gameMode.kind !== 'frontline') {
    assert.fail('expected Frontline rules');
  }
  invalidId.header.contract.rules.gameMode.capture.gainSchedule![1]!.phaseId =
    'Late_Escalation';
  assert.throws(
    () => decodeReplay(invalidId),
    /expected a 1-64 character lowercase-kebab semantic ID/,
  );

  const unknownPhaseField = fixture() as unknown as {
    header: {
      contract: {
        rules: {
          gameMode: {
            capture: {
              gainSchedule: { presentationHint?: string }[];
            };
          };
        };
      };
    };
  };
  unknownPhaseField.header.contract.rules.gameMode.capture.gainSchedule[0]!
    .presentationHint = 'opening';
  assert.throws(
    () => decodeReplay(unknownPhaseField),
    /gainSchedule\[0\]\.presentationHint: unknown property/,
  );
});

test('replay-v3 Frontline rejects unknown arms and terminal/control/score/standing drift', () => {
  const fixture = () =>
    adaptReplayV3ToFrontline(replayV3FixtureInput(), 'base-breach');

  const unknownRule = fixture() as unknown as {
    header: { contract: { rules: { gameMode: { kind: string } } } };
  };
  unknownRule.header.contract.rules.gameMode.kind = 'future-mode';
  assert.throws(
    () => decodeReplay(unknownRule),
    /unknown game mode future-mode/,
  );

  const unknownBinding = fixture() as unknown as {
    header: { contract: { modeMapBinding: { kind: string } } };
  };
  unknownBinding.header.contract.modeMapBinding.kind = 'future-binding';
  assert.throws(
    () => decodeReplay(unknownBinding),
    /unknown mode-map binding future-binding/,
  );

  const unknownResult = fixture() as unknown as {
    result: { mode: { kind: string } };
  };
  unknownResult.result.mode.kind = 'future-mode';
  assert.throws(
    () => decodeReplay(unknownResult),
    /unknown mode result future-mode/,
  );

  const unknownReason = fixture() as unknown as {
    result: { mode: { reason: string } };
  };
  unknownReason.result.mode.reason = 'sudden-death';
  assert.throws(
    () => decodeReplay(unknownReason),
    /unknown frontline end reason/,
  );

  const controlDrift = fixture();
  if (controlDrift.result?.mode.kind !== 'frontline') {
    assert.fail('expected Frontline result');
  }
  controlDrift.result.mode.control.activePositionIndex = 1;
  assert.throws(
    () => decodeReplay(controlDrift),
    /must exactly match final authoritative frontline control/,
  );

  const malformedSigned = fixture();
  if (malformedSigned.result?.mode.kind !== 'frontline') {
    assert.fail('expected Frontline result');
  }
  malformedSigned.result.mode.scores[1]!.territorialProgress = '+3';
  assert.throws(
    () => decodeReplay(malformedSigned),
    /expected a canonical signed 64-bit decimal string/,
  );

  const finalScoreDrift = fixture();
  if (finalScoreDrift.result?.mode.kind !== 'frontline') {
    assert.fail('expected Frontline result');
  }
  finalScoreDrift.result.mode.scores[0]!.territorialProgress = '2';
  assert.throws(
    () => decodeReplay(finalScoreDrift),
    /must match the final territorial-progress scoreboard value/,
  );

  const standingDrift = fixture();
  standingDrift.result!.standings.teams[0]!.rank = 2;
  assert.throws(
    () => decodeReplay(standingDrift),
    /does not follow frontline eligibility and victory ranking/,
  );

  const endTickDrift = fixture();
  endTickDrift.result!.endTick = null;
  assert.throws(
    () => decodeReplay(endTickDrift),
    /must be null exactly when no joint tick executed/,
  );

  const excessiveRedeployPause = adaptReplayV3ToFrontline(
    replayV3FixtureInput(),
  );
  if (excessiveRedeployPause.initialFrame.state.mode.kind !== 'frontline') {
    assert.fail('expected Frontline initial control');
  }
  excessiveRedeployPause.initialFrame.state.mode.controlResumesAtTick = 2;
  assert.throws(
    () => decodeReplay(excessiveRedeployPause),
    /violates frontline control invariants/,
  );

  const maxTicksWithOneEligible = adaptReplayV3ToFrontline(
    replayV3FixtureInput(),
  );
  const finalWorld = maxTicksWithOneEligible.ticks.at(-1)!.postState;
  finalWorld.scoreboard.teams[1]!.eligible = false;
  maxTicksWithOneEligible.result!.eligibleTeamIds = [0];
  assert.throws(
    () => decodeReplay(maxTicksWithOneEligible),
    /max-ticks requires multiple eligible teams/,
  );

  for (const reason of ['fault-eligibility', 'base-breach'] as const) {
    const afterTimeout = adaptReplayV3ToFrontline(
      replayV3FixtureInput(),
      reason,
    );
    afterTimeout.header.contract.rules.limits.maxTicks = 1;
    assert.throws(
      () => decodeReplay(afterTimeout),
      /configured maximum tick boundary/,
    );
  }

  const emptyObjective = fixture();
  emptyObjective.header.contract.map.regions[0]!.tiles = [];
  assert.throws(
    () => decodeReplay(emptyObjective),
    /must reference a non-empty objective map region/,
  );

  const reversedScores = fixture();
  reversedScores.result!.mode.scores.reverse();
  assert.throws(
    () => decodeReplay(reversedScores),
    /must be in canonical ascending team order/,
  );

  const reversedAdvances = fixture();
  if (reversedAdvances.header.contract.modeMapBinding.kind !== 'frontline') {
    assert.fail('expected Frontline mode-map binding');
  }
  reversedAdvances.header.contract.modeMapBinding.teamAdvances.reverse();
  assert.throws(
    () => decodeReplay(reversedAdvances),
    /must be in canonical ascending team order/,
  );
});

test('replay-v3 mirrors emitted map region and tile-tag fields exactly', () => {
  const input = JSON.parse(replayV3FixtureText()) as {
    header: {
      contract: {
        map: {
          regions: unknown[];
          tileTags: unknown[];
        };
      };
    };
  };
  input.header.contract.map.regions.push({
    regionId: 'center-objective',
    kind: 'objective',
    tiles: [[4, 3]],
  });
  input.header.contract.map.tileTags.push({
    tagId: 'center-anchor-policy',
    kind: 'transition-placement-forbidden',
    tiles: [[4, 3]],
  });

  assert.doesNotThrow(() => decodeReplay(input));

  const legacyTagField = structuredClone(input) as typeof input;
  legacyTagField.header.contract.map.tileTags[0] = {
    tag: 'center-anchor-policy',
    kind: 'transition-placement-forbidden',
    tiles: [[4, 3]],
  };
  assert.throws(
    () => decodeReplay(legacyTagField),
    /map\.tileTags\[0\]\.tag: unknown property/,
  );

  const missingRegionKind = structuredClone(input) as typeof input;
  missingRegionKind.header.contract.map.regions[0] = {
    regionId: 'center-objective',
    tiles: [[4, 3]],
  };
  assert.throws(
    () => decodeReplay(missingRegionKind),
    /map\.regions\[0\]\.kind: missing required property/,
  );
});

test('replay-v3 accepts backend-grouped event tags without collapsing payload kinds', () => {
  const input = JSON.parse(replayV3FixtureText()) as {
    ticks: {
      events: unknown[];
      actorTurns: {
        observation: { visibleEvents: unknown[] };
      }[];
    }[];
  };
  const actorId = { teamId: 0, unitId: 0, lifeId: 0 };
  const grouped = [
    {
      kind: 'participant-disqualified',
      payload: { kind: 'participant', participantId: 10, teamId: 0 },
    },
    {
      kind: 'lifecycle-queued',
      payload: {
        kind: 'lifecycle',
        transitionId: 'split',
        operationId: 'split:queued',
        sourceActorId: actorId,
        targetTeamId: 0,
        targetUnitId: 0,
        dueTick: 1,
        cancellationReason: null,
      },
    },
    {
      kind: 'lifecycle-cancelled',
      payload: {
        kind: 'lifecycle',
        transitionId: 'split',
        operationId: 'split:cancelled',
        sourceActorId: actorId,
        targetTeamId: 0,
        targetUnitId: 0,
        dueTick: null,
        cancellationReason: 'superseded',
      },
    },
    {
      kind: 'lifecycle-completed',
      payload: {
        kind: 'lifecycle',
        transitionId: 'split',
        operationId: 'split:completed',
        sourceActorId: actorId,
        targetTeamId: 0,
        targetUnitId: 0,
        dueTick: null,
        cancellationReason: null,
      },
    },
    ...[
      'form-transition-started',
      'form-transition-completed',
      'form-transition-cancelled',
    ].map((kind, index) => ({
      kind,
      payload: {
        kind: 'form-transition',
        actorId,
        transitionId: 'deploy',
        operationId: `deploy:${index}`,
        fromFormId: 'mobile',
        toFormId: 'turret',
        startedTick: 0,
        dueTick: 1,
      },
    })),
  ];
  input.ticks[0]!.events.push(
    ...grouped.map((event, index) => ({
      eventHandle: `synthetic-grouped:${index}`,
      tick: 0,
      globalOrdinal: String(100 + index),
      sourceOrdinal: 4 + index,
      ...event,
      audience: { kind: 'public' },
    })),
  );
  input.ticks[0]!.actorTurns[0]!.observation.visibleEvents.push({
    eventHandle: 'synthetic-observed-disqualification',
    sourceTick: 0,
    sourceOrdinal: 99,
    kind: 'participant-disqualified',
    payload: { kind: 'participant', participantId: 10, teamId: 0 },
    observedBy: [actorId],
  });

  const replay = decodeReplay(input).replay;
  assert.deepEqual(
    replay.ticks[0]!.events.slice(-grouped.length).map((event) => [
      event.type,
      event.payloadKind,
    ]),
    grouped.map((event) => [event.kind, event.payload.kind]),
  );
  const observed =
    replay.ticks[0]!.actorTurns[0]!.observation.visibleEvents.at(-1);
  assert.equal(observed?.type, 'participant-disqualified');
  assert.equal(observed?.payloadKind, 'participant');

  const mismatched = structuredClone(input) as typeof input;
  (
    mismatched.ticks[0]!.events.at(-1) as {
      payload: unknown;
    }
  ).payload = { kind: 'participant', participantId: 10, teamId: 0 };
  assert.throws(
    () => decodeReplay(mismatched),
    /must use payload kind form-transition/,
  );
});

test('replay-v3 accepts the optional movement facing coupling and rejects an inert or unknown one', () => {
  const coupled = replayV3FixtureInput();
  coupled.header.contract.rules.movementProfiles[0]!.facingCoupling =
    'face-movement-direction';
  const decoded = decodeReplay(coupled).replay;

  assert.equal(decoded.sourceVersion, 3);
  assert.equal(decoded.forms[0]?.movementLayer, 'ground');

  // The engine's canonical writer omits the property entirely while the
  // profile preserves facing, so an explicitly inert value is a second,
  // non-canonical encoding of the same contract.
  const inert = replayV3FixtureInput();
  inert.header.contract.rules.movementProfiles[0]!.facingCoupling =
    'preserve-facing';
  assert.throws(
    () => decodeReplay(inert),
    /facingCoupling: must be omitted instead of emitted inert/,
  );

  const unknown = replayV3FixtureInput();
  unknown.header.contract.rules.movementProfiles[0]!.facingCoupling =
    'tank-controls';
  assert.throws(
    () => decodeReplay(unknown),
    /facingCoupling: expected face-movement-direction or facing-locked/,
  );

  const strayField = replayV3FixtureInput();
  (
    unknown.header.contract.rules.movementProfiles[0] as unknown as {
      facingCoupling?: string;
    }
  ).facingCoupling = undefined;
  (
    strayField.header.contract.rules.movementProfiles[0] as unknown as {
      couplings?: string;
    }
  ).couplings = 'face-movement-direction';
  assert.throws(
    () => decodeReplay(strayField),
    /movementProfiles\[0\]\.couplings: unknown property/,
  );
});

test('replay-v3 accepts a movement-coupled facing change with no rotation evidence', () => {
  const coupled = replayV3FixtureInput();
  coupled.header.contract.rules.movementProfiles[0]!.facingCoupling =
    'face-movement-direction';
  const tick = coupled.ticks.at(-1)!;
  const life = tick.postState.activeLives[0]!;
  const from = { ...life.position };
  const to = { x: from.x, y: from.y - 1 };
  const turned = life.facing === 'north' ? 'south' : 'north';

  // Under FaceMovementDirection the Movement event is itself the
  // facing-change evidence: no rotation event accompanies it, and the tick's
  // authoritative post-state carries the new facing.
  life.facing = turned;
  life.position = to;
  const resultUnit = coupled.result!.units.find(
    (unit) =>
      unit.slot.teamId === life.actorId.teamId &&
      unit.slot.unitId === life.actorId.unitId,
  )!;
  resultUnit.activeLife = structuredClone(life);
  tick.events.push({
    eventHandle: 'synthetic-coupled-movement',
    tick: tick.tick,
    globalOrdinal: '900',
    sourceOrdinal: (tick.events.at(-1)?.sourceOrdinal ?? -1) + 1,
    kind: 'movement',
    payload: {
      kind: 'movement',
      actorId: { ...life.actorId },
      action: { actionId: 'move', actionCode: 1, arguments: [] },
      from,
      to,
      facing: turned,
    },
    audience: { kind: 'spatial', primaryPosition: to },
  } as (typeof tick.events)[number]);

  const replay = decodeReplay(coupled).replay;
  const moved = replay.ticks.at(-1)!.after.actors.find(
    (actor) =>
      actor.identity.teamId === life.actorId.teamId &&
      actor.identity.unitId === life.actorId.unitId,
  );

  assert.equal(moved?.facing, turned);
  assert.equal(
    replay.ticks.at(-1)!.events.some((event) => event.type === 'rotation'),
    false,
  );
  assert.equal(
    replay.ticks.at(-1)!.events.at(-1)?.type,
    'movement',
  );
});

test('replay-v3 strictly rejects unknown fields and cross-frame identity drift', () => {
  const duplicate = replayV3FixtureText().replace(
    '"partial":false',
    '"partial":true,"partial":false',
  );
  assert.throws(
    () => decodeReplayJson(duplicate),
    /replay\.partial: duplicate property/,
  );

  const unknown = JSON.parse(replayV3FixtureText()) as {
    initialFrame: { extra?: boolean };
  };
  unknown.initialFrame.extra = true;
  assert.throws(
    () => decodeReplay(unknown),
    /initialFrame\.extra: unknown property/,
  );

  const fingerprintDrift = JSON.parse(replayV3FixtureText()) as {
    ticks: {
      actorTurns: {
        observation: { matchContractFingerprint: string };
      }[];
    }[];
  };
  fingerprintDrift.ticks[0]!.actorTurns[0]!.observation
    .matchContractFingerprint = 'wrong';
  assert.throws(
    () => decodeReplay(fingerprintDrift),
    /turn identity, tick, or observation contract is inconsistent/,
  );

  const lifeDrift = JSON.parse(replayV3FixtureText()) as {
    ticks: {
      postState: {
        activeLives: { actorId: { lifeId: number } }[];
      };
    }[];
  };
  lifeDrift.ticks[0]!.postState.activeLives[0]!.actorId.lifeId = 7;
  assert.throws(
    () => decodeReplay(lifeDrift),
    /active slot must match exactly one active life/,
  );
});

function replayFixtureText(name: string): string {
  return readFileSync(
    new URL(`./fixtures/${name}`, import.meta.url),
    'utf8',
  );
}

function replayV3FixtureText(): string {
  return readFileSync(
    new URL(
      '../../tests/BotArena.Engine.Tests/Fixtures/generic-replay-v3.json',
      import.meta.url,
    ),
    'utf8',
  );
}

function replayV3FixtureInput(): ReplayV3Document {
  return JSON.parse(replayV3FixtureText()) as ReplayV3Document;
}

function replayV2FabricationFixtureInput() {
  const input = replayV2FixtureInput();
  const contract = input.header.contract;
  const frontline = contract.rules.frontlineDefinition!;
  const topology = contract.topology;
  const tick = input.ticks[0]!;
  const turn = tick.actors[0]!;

  contract.rules.limits.unitSlotCount = 3;
  contract.rules.limits.maxUnitsPerTeam = 2;
  frontline.maxUnitsPerTeam = 2;
  frontline.lifecycle.fabricationUnlockTicks = [0];
  frontline.fabrication.enabled = true;
  contract.rules.forms
    .find((form) => form.id === 'prime')!
    .allowedActionIds.push('fabricate');
  contract.rules.actions.push({
    id: 'fabricate',
    code: 7,
    kind: 'fabrication',
    parameterKinds: ['unit-target'],
    enabled: true,
  });
  topology.unitSlotCount = 3;
  topology.unitSlots.push({
    teamId: 0,
    unitId: 1,
    controllerParticipantId: 0,
  });
  contract.map.frontline!.teamHomes[0]!.protectedSpawnPad.push([0, 2]);

  const readyChild = {
    teamId: 0,
    unitId: 1,
    defaultFormId: 'child',
    lifecycleStatus: 'ready' as const,
    respawnAtTick: null,
    unlockAtTick: 0,
    rebuildReadyAtTick: null,
    fabricationAtTick: null,
    reservedSpawn: null,
    pendingSpawnReason: null,
    hasSpawned: false,
    nextLifeId: 0,
    damageDealt: '0',
    activeLife: null,
  };
  tick.tickStart.state.teams[0]!.units.push(readyChild);
  tick.postState.teams[0]!.units.push({
    ...readyChild,
    lifecycleStatus: 'fabrication-queued',
    fabricationAtTick: 1,
    reservedSpawn: { x: 0, y: 2 },
    pendingSpawnReason: 'fabrication',
  });
  turn.observation.teamUnits.push({
    teamId: 0,
    unitId: 1,
    formId: 'child',
    lifecycleStatus: 'ready',
    activeActorId: null,
    respawnAtTick: null,
    unlockAtTick: 0,
    rebuildReadyAtTick: null,
    fabricationAtTick: null,
  });
  for (const actorTurn of tick.actors) {
    actorTurn.observation.actions.push({
      actionId: 'fabricate',
      actionCode: 7,
      parameterKinds: ['unit-target'],
      enabled: true,
      available: actorTurn.actorId.teamId === 0,
      shotProgramAvailable: null,
      allowedDirections: null,
      allowedProjectileHeadings: null,
      allowedUnitTargets:
        actorTurn.actorId.teamId === 0
          ? [{ teamId: 0, unitId: 1 }]
          : [],
      allowedFormTargets: null,
    });
  }
  tick.resolution.events.push({
    eventId: 'resolution:0:fabrication',
    tick: 0,
    type: 'fabrication-queued',
    teamId: 0,
    unitId: 1,
    sourceActorId: turn.actorId,
    targetActorId: null,
    projectileId: null,
    from: null,
    to: { x: 0, y: 2 },
    fromFacing: null,
    toFacing: null,
    projectileHeading: null,
    actionPayload: {
      shotProgram: null,
      direction: null,
      launchHeading: null,
      unitTarget: { teamId: 0, unitId: 1 },
      formTargetId: null,
    },
    actionId: 'fabricate',
    actionCode: 7,
    actionResult: 'success',
    fromFormId: null,
    toFormId: null,
    formTransitionStartedAtTick: null,
    formTransitionCompletesAtTick: null,
    amount: null,
    newHealth: null,
    lifecycleStatus: 'fabrication-queued',
    spawnReason: 'fabrication',
    respawnAtTick: null,
    unlockAtTick: null,
    rebuildReadyAtTick: null,
    fabricationAtTick: 1,
    fromPositionIndex: null,
    toPositionIndex: null,
    claimingTeamId: null,
    captureProgress: null,
    controlResumesAtTick: null,
  });
  input.result.teams[0]!.units.push({
    teamId: 0,
    unitId: 1,
    defaultFormId: 'child',
    formId: 'child',
    pendingFormTransition: null,
    lifecycleStatus: 'fabrication-queued',
    activeActorId: null,
    health: 0,
    damageDealt: '0',
  });

  return input;
}
