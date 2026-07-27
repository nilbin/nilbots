import assert from 'node:assert/strict';
import test from 'node:test';
import {
  collectCrossedSoundtrackTriggers,
  createSoundtrackTriggerCursor,
  soundtrackPresentationId,
} from '../src/soundtrack/transport.ts';

test('crossed-tick trigger delivery aggregates, deduplicates, and resets on seeks', () => {
  const timeline = fakeTimeline([
    frame(0),
    frame(1, ['contact']),
    frame(2, ['shot', 'damage']),
    frame(3),
    frame(4, ['destruction', 'resolve']),
    frame(5, ['overtime']),
    frame(10, ['shot']),
  ]);
  const cursor = createSoundtrackTriggerCursor();

  assert.deepEqual(
    collectCrossedSoundtrackTriggers(cursor, timeline, 'match-a', 0, 0),
    { triggers: [], discontinuity: true },
  );
  assert.deepEqual(
    collectCrossedSoundtrackTriggers(cursor, timeline, 'match-a', 0, 4),
    {
      discontinuity: false,
      triggers: [
        { type: 'contact', sourceTick: 1 },
        { type: 'shot', sourceTick: 2 },
        { type: 'damage', sourceTick: 2 },
        { type: 'destruction', sourceTick: 4 },
        { type: 'resolve', sourceTick: 4 },
      ],
    },
  );
  assert.deepEqual(
    collectCrossedSoundtrackTriggers(cursor, timeline, 'match-a', 0, 4)
      .triggers,
    [],
  );
  assert.deepEqual(
    collectCrossedSoundtrackTriggers(cursor, timeline, 'match-a', 0, 5)
      .triggers,
    [{ type: 'overtime', sourceTick: 5 }],
  );

  const forwardSeek = collectCrossedSoundtrackTriggers(
    cursor,
    timeline,
    'match-a',
    1,
    10,
  );
  assert.equal(forwardSeek.discontinuity, true);
  assert.deepEqual(forwardSeek.triggers, []);

  const backwardSeek = collectCrossedSoundtrackTriggers(
    cursor,
    timeline,
    'match-a',
    1,
    2,
  );
  assert.equal(backwardSeek.discontinuity, true);
  assert.deepEqual(backwardSeek.triggers, []);

  const newPack = collectCrossedSoundtrackTriggers(
    cursor,
    timeline,
    'match-a:new-pack',
    1,
    5,
  );
  assert.equal(newPack.discontinuity, true);
  assert.deepEqual(newPack.triggers, []);
});

test('live completion keeps the current ReplayModel presentation identity', () => {
  const partial = replayIdentityFixture();
  const complete = {
    ...partial,
    partial: false,
    replayHash: 'completed-hash',
    result: {
      winnerTeamId: 0,
      reason: 'elimination',
      endTick: 8,
      territorialScore: null,
      objective: { kind: 'none' },
      teams: [],
    },
  };

  assert.equal(
    soundtrackPresentationId(partial, 'neon'),
    soundtrackPresentationId(complete, 'neon'),
  );
  assert.notEqual(
    soundtrackPresentationId(partial, 'neon'),
    soundtrackPresentationId(complete, 'alternate'),
  );
  assert.notEqual(
    soundtrackPresentationId(partial, 'neon'),
    soundtrackPresentationId(
      {
        ...complete,
        versions: {
          ...complete.versions,
          gameRulesVersion: 'different-rules',
        },
      },
      'neon',
    ),
    'immutable ReplayModel match inputs participate in the identity',
  );
});

function fakeTimeline(frames) {
  return {
    frames,
    initialFrame: frame(-1),
    config: {},
  };
}

function frame(tick, triggers = []) {
  return { tick, triggers };
}

function replayIdentityFixture() {
  return {
    sourceVersion: 1,
    versions: {
      engineVersion: 'engine',
      gameRulesVersion: 'rules',
      runtimeProtocolVersion: 'protocol',
      runtimeConfigurationVersion: 'runtime',
      actorRuntime: null,
    },
    seed: '42',
    partial: true,
    replayHash: null,
    matchContractFingerprint: null,
    contract: {
      rules: {
        rulesetId: 'legacy-rules',
        rulesFingerprint: null,
      },
    },
    map: {
      mapId: 'arena',
      mapVersion: 2,
    },
    participants: [
      {
        participantKey: 'participant:0',
        teamKey: 'team:0',
        artifactHash: 'alpha',
      },
    ],
    units: [{ unitKey: 'duel:0:unit:0' }],
    ticks: [],
  };
}
