import assert from 'node:assert/strict';
import test from 'node:test';
import {
  decodeReplay,
  ReplayDecodeError,
} from '../src/replayNormalize.ts';
import {
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
    () => decodeReplay({ header: { replayVersion: 3 } }),
    /unsupported replay version 3/,
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

test('replay-v2 keeps a stable unit while separating exact respawn lives', () => {
  const replay = decodeReplay(replayV2FixtureInput()).replay;
  const tick = replay.ticks[0];

  assert.equal(tick?.before.actors[0]?.actorKey, 'frontline:0:unit:0:life:0');
  assert.equal(tick?.after.actors[0]?.actorKey, 'frontline:0:unit:0:life:1');
  assert.equal(
    tick?.before.actors[0]?.unitKey,
    tick?.after.actors[0]?.unitKey,
  );
  assert.equal(tick?.before.actors[0]?.unitKey, 'frontline:0:unit:0');
  assert.equal(replay.units[0]?.initialActorKey, 'frontline:0:unit:0:life:0');
});

test('replay-v2 keeps opaque observation handles separate from exact alias identities', () => {
  const input = replayV2FixtureInput();
  const turn = input.ticks[0]!.actors[0]!;
  const exactEnemy = { teamId: 0, unitId: 0, lifeId: 99 };
  turn.aliases.enemyLives = [
    { lifeHandle: 'enemy-life-0', actorId: exactEnemy },
  ];
  turn.observation.enemies = [
    {
      actor: { teamId: 0, unitId: 0, lifeHandle: 'enemy-life-0' },
      formId: 'prime',
      position: { x: 2, y: 1 },
      facing: 'west',
      health: 2,
      observedBy: [turn.actorId],
    },
  ];

  const normalized = decodeReplay(input).replay.ticks[0]!.actorTurns[0]!;

  assert.deepEqual(normalized.observation.enemies[0]?.actor, {
    kind: 'opaque-enemy',
    teamId: 0,
    unitId: 0,
    lifeHandle: 'enemy-life-0',
  });
  assert.equal(
    normalized.aliases.enemyLives[0]?.actor.actorKey,
    'frontline:0:unit:0:life:99',
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
    {
      shotProgram: null,
      direction: null,
      unitKey: null,
      formTargetId: null,
    },
  );
});

test('replay-v2 preserves runtime, accepted, and resolved generic payloads independently', () => {
  const input = replayV2FixtureInput();
  const turn = input.ticks[0]!.actors[0]!;
  turn.runtimeReply = {
    actionId: 'future-flight',
    actionCode: 9_007,
    payload: {
      shotProgram: null,
      direction: 'north',
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
        unitTarget: { teamId: 0, unitId: 0 },
        formTargetId: 'flight',
      },
      actionResult: 'success',
      amount: null,
      newHealth: null,
      lifecycleStatus: null,
      respawnAtTick: null,
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
  assert.equal(replay.teams.length, 1);
  assert.equal(replay.units.length, 1);
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
