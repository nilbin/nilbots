import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import test from 'node:test';
import {
  decodeReplay,
  decodeReplayJson,
} from '../src/replayNormalize.ts';
import type {
  ReplayV2CompleteDocument,
  ReplayV2Document,
} from '../src/replayWireV2.ts';

const JS_UNSAFE_SEED = '9007199254740993';

function readEngineFixture(name: string): {
  raw: string;
  parsed: unknown;
} {
  const raw = readFileSync(
    new URL(`./fixtures/${name}`, import.meta.url),
    'utf8',
  );
  return { raw, parsed: JSON.parse(raw) as unknown };
}

test('engine-authored finalized replay-v2 decodes without reserialization', () => {
  const { raw } = readEngineFixture('frontline-replay-v2.json');
  const decoded = decodeReplayJson(raw);
  const replay = decoded.replay;

  assert.ok(raw.includes(`"seed":"${JS_UNSAFE_SEED}"`));
  assert.equal(decoded.replayVersion, 2);
  assert.equal(replay.sourceVersion, 2);
  assert.equal(replay.seed, JS_UNSAFE_SEED);
  assert.equal(replay.seedExact, true);
  assert.equal(replay.partial, false);
  assert.match(replay.replayHash ?? '', /^[0-9a-f]{64}$/);
  assert.equal(replay.ticks.length, 4);

  assert.deepEqual(
    replay.ticks[0]?.before.actors.map((actor) => actor.actorKey),
    [
      'frontline:0:unit:0:life:0',
      'frontline:1:unit:0:life:0',
    ],
  );
  assert.deepEqual(replay.ticks[0]?.after.actors, []);
  assert.deepEqual(replay.ticks[1]?.activeActorKeys, []);
  assert.deepEqual(
    replay.ticks[2]?.before.actors.map((actor) => actor.actorKey),
    [
      'frontline:0:unit:0:life:1',
      'frontline:1:unit:0:life:1',
    ],
  );
  assert.ok(
    replay.ticks[0]?.events.filter((event) => event.type === 'destroyed')
      .length === 2,
  );
  assert.ok(
    replay.ticks[2]?.actorTurns.every(
      (turn) => turn.lifeStart?.spawnReason === 'respawn',
    ),
  );
});

test('engine-authored tick-zero failure decodes as a hashless empty prefix', () => {
  const { raw } = readEngineFixture(
    'frontline-replay-v2-partial-zero-tick.json',
  );
  const decoded = decodeReplayJson(raw);
  const replay = decoded.replay;

  assert.ok(raw.includes('"partial":true'));
  assert.equal(decoded.replayVersion, 2);
  assert.equal(replay.sourceVersion, 2);
  assert.equal(replay.seed, JS_UNSAFE_SEED);
  assert.equal(replay.seedExact, true);
  assert.equal(replay.partial, true);
  assert.equal(replay.replayHash, null);
  assert.equal(replay.result, null);
  assert.equal(replay.initialWorld, null);
  assert.deepEqual(replay.ticks, []);
});

test('engine-authored replay-v2 remains valid when wire ticks arrive out of order', () => {
  const input = finalizedV2Fixture();
  input.ticks.reverse();

  const replay = decodeReplay(input).replay;

  assert.deepEqual(
    replay.ticks.map((tick) => tick.tick),
    [0, 1, 2, 3],
  );
});

test('replay-v2 rejects tick gaps and nonzero starts', () => {
  const nonzero = finalizedV2Fixture();
  nonzero.ticks = nonzero.ticks.slice(1);
  assert.throws(
    () => decodeReplay(nonzero),
    /start at zero and be contiguous/,
  );

  const gap = finalizedV2Fixture();
  gap.ticks.splice(1, 1);
  assert.throws(
    () => decodeReplay(gap),
    /start at zero and be contiguous/,
  );
});

test('replay-v2 requires lifeStart exactly on each actor life first turn', () => {
  const missing = finalizedV2Fixture();
  missing.ticks[0]!.actors[0]!.lifeStart = null;
  assert.throws(
    () => decodeReplay(missing),
    /lifeStart.*first turn/,
  );

  const repeated = finalizedV2Fixture();
  const firstRespawn = repeated.ticks[2]!.actors[0]!;
  const nextTurn = repeated.ticks[3]!.actors.find(
    (turn) =>
      turn.actorId.teamId === firstRespawn.actorId.teamId &&
      turn.actorId.unitId === firstRespawn.actorId.unitId &&
      turn.actorId.lifeId === firstRespawn.actorId.lifeId,
  )!;
  nextTurn.lifeStart = structuredClone(firstRespawn.lifeStart);
  assert.throws(
    () => decodeReplay(repeated),
    /lifeStart.*first turn/,
  );
});

test('replay-v2 rejects empty payload envelopes at every payload boundary', () => {
  const emptyPayload = () => ({
    shotProgram: null,
    direction: null,
    unitTarget: null,
    formTargetId: null,
  });
  const mutations: ((
    input: ReplayV2CompleteDocument,
  ) => void)[] = [
    (input) => {
      input.ticks[0]!.actors[0]!.runtimeReply.payload =
        emptyPayload();
    },
    (input) => {
      input.ticks[0]!.actors[0]!.acceptedDecision.payload =
        emptyPayload();
    },
    (input) => {
      input.ticks[0]!.actors[0]!.actionResolution.chosenPayload =
        emptyPayload();
    },
    (input) => {
      input.ticks[0]!.actors[0]!.actionResolution.validatedPayload =
        emptyPayload();
    },
    (input) => {
      input.ticks[0]!.resolution.events[0]!.actionPayload =
        emptyPayload();
    },
  ];

  for (const mutate of mutations) {
    const input = finalizedV2Fixture();
    mutate(input);
    assert.throws(
      () => decodeReplay(input),
      /empty action payload must canonicalize to null/,
    );
  }
});

test('replay-v2 requires accepted selectors and payloads to equal chosen resolution', () => {
  const selectorMismatch = finalizedV2Fixture();
  const selectorTurn = selectorMismatch.ticks[0]!.actors[0]!;
  selectorTurn.acceptedDecision = {
    ...selectorTurn.acceptedDecision,
    actionId: 'wait',
    actionCode: 0,
    payload: null,
  };
  assert.throws(
    () => decodeReplay(selectorMismatch),
    /selector and payload must equal the chosen action resolution/,
  );

  const payloadMismatch = finalizedV2Fixture();
  const payload =
    payloadMismatch.ticks[0]!.actors[0]!.acceptedDecision.payload!;
  payload.shotProgram!.initialAimOffset = 1;
  assert.throws(
    () => decodeReplay(payloadMismatch),
    /selector and payload must equal the chosen action resolution/,
  );
});

test('replay-v2 rejects terminal and objective team drift', () => {
  const missingTeam = finalizedV2Fixture();
  missingTeam.result.teams.pop();
  assert.throws(
    () => decodeReplay(missingTeam),
    /cover exactly the topology team IDs/,
  );

  const unknownWinner = finalizedV2Fixture();
  unknownWinner.result.winnerTeamId = 99;
  assert.throws(
    () => decodeReplay(unknownWinner),
    /winnerTeamId.*topology team/,
  );

  const unknownObjectiveTeam = finalizedV2Fixture();
  unknownObjectiveTeam.ticks[0]!.tickStart.state.objective.claimingTeamId =
    99;
  assert.throws(
    () => decodeReplay(unknownObjectiveTeam),
    /claimingTeamId.*topology team/,
  );

  const objectiveDrift = finalizedV2Fixture();
  objectiveDrift.result.objective.captureProgress += 1;
  assert.throws(
    () => decodeReplay(objectiveDrift),
    /final post-state objective/,
  );
});

test('replay-v2 rejects dangling alias sidecars', () => {
  const input = finalizedV2Fixture();
  input.ticks[0]!.actors[0]!.aliases.events.push({
    eventHandle: 'event-99',
    eventId: 'resolution:999:0',
  });

  assert.throws(
    () => decodeReplay(input),
    /aliases\.events.*exactly match event handles/,
  );
});

function finalizedV2Fixture(): ReplayV2CompleteDocument {
  const { parsed } = readEngineFixture('frontline-replay-v2.json');
  const document = parsed as ReplayV2Document;
  assert.equal(document.partial, false);
  return document as ReplayV2CompleteDocument;
}
