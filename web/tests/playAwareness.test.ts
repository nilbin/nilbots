import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import test from 'node:test';
import { loadReplayJson } from '../src/replayIngress.ts';
import type { ReplayCausalEvent } from '../src/replayModel.ts';
import {
  parsePlayRoleTag,
  playAwarenessTimeline,
  playForUnit,
  playRoleSummary,
} from '../src/presentation/playAwareness.ts';

function replayWithOperation() {
  const replay = loadReplayJson(
    readFileSync(
      new URL('./fixtures/generic-mind-replay-v3.json', import.meta.url),
      'utf8',
    ),
  ).replay;
  const first = replay.ticks[0]!.actorTurns[0]!;
  const second = replay.ticks[0]!.actorTurns[1]!;
  for (const [tick, phase] of [
    [0, 'p'],
    [1, 'c'],
    [2, 'r'],
  ] as const) {
    const turns = replay.ticks[tick]!.actorTurns;
    turns[1]!.actor.teamId = turns[0]!.actor.teamId;
    turns[1]!.actor.unitId = 1;
    turns[1]!.actor.unitKey = 'generic:0:unit:1';
    turns[1]!.actor.actorKey = 'generic:0:unit:1:life:0';
    turns[1]!.actorKey = 'generic:0:unit:1:life:0';
    turns[0]!.observation.self!.roleTag = `g-rh-${phase}-nh`;
    turns[1]!.observation.self!.roleTag = `g-rh-${phase}-sh`;
  }
  replay.ticks[3]!.actorTurns[0]!.observation.self!.roleTag = 'a-bal-car-n';
  replay.ticks[3]!.actorTurns[1]!.actor.teamId = 0;
  replay.ticks[3]!.actorTurns[1]!.actor.unitId = 1;
  replay.ticks[3]!.actorTurns[1]!.actor.unitKey = 'generic:0:unit:1';
  replay.ticks[3]!.actorTurns[1]!.actor.actorKey = 'generic:0:unit:1:life:0';
  replay.ticks[3]!.actorTurns[1]!.actorKey = 'generic:0:unit:1:life:0';
  replay.ticks[3]!.actorTurns[1]!.observation.self!.roleTag = 'a-bal-car-s';
  return { replay, first, second };
}

test('operation tags become bounded player-facing names without interpreting baseline tags', () => {
  assert.deepEqual(parsePlayRoleTag('g-rh-c-nh'), {
    operationCode: 'rh',
    name: 'Rear Hook',
    named: true,
    phase: 'committed',
    taskCode: 'nh',
    task: 'north hook',
  });
  assert.equal(
    playRoleSummary('g-ls-p-lan'),
    'Lantern Sweep · route probe · preparing',
  );
  assert.equal(parsePlayRoleTag('a-bal-car-n'), null);
  assert.equal(parsePlayRoleTag('g-op-x-task'), null);
  assert.equal(parsePlayRoleTag('g-op-p-task')?.name, 'Unlabelled coordination');
});

test('the transition trace groups participants and records causal release', () => {
  const { replay, first, second } = replayWithOperation();
  const timeline = playAwarenessTimeline(replay);
  const activation = timeline.activations[0];
  assert.ok(activation);
  assert.equal(activation.name, 'Rear Hook');
  assert.equal(activation.startedTick, 0);
  assert.equal(activation.committedTick, 1);
  assert.equal(activation.recoveryTick, 2);
  assert.equal(activation.releaseTick, 3);
  assert.deepEqual(
    activation.transitions.map((entry) => [entry.tick, entry.phase]),
    [[0, 'preparing'], [1, 'committed'], [2, 'recovery'], [3, 'released']],
  );
  assert.deepEqual(
    activation.participantUnitKeys,
    [first.actor.unitKey, second.actor.unitKey].sort(),
  );
  assert.equal(timeline.frames[3]!.length, 0);
});

test('selecting one claimed body resolves only its active playmates', () => {
  const { replay, first, second } = replayWithOperation();
  const play = playForUnit(replay, 1, first.actor.unitKey);
  assert.ok(play);
  assert.equal(play.phase, 'committed');
  assert.deepEqual(
    play.participants.map((entry) => entry.unitKey),
    [first.actor.unitKey, second.actor.unitKey].sort(),
  );
  assert.equal(playForUnit(replay, 3, first.actor.unitKey), null);
});

test('contact summaries distinguish a participant loss from a play casualty', () => {
  const { replay, first } = replayWithOperation();
  const opponent = {
    ...first.actor,
    teamId: 1,
    unitId: 0,
    unitKey: 'generic:1:unit:0',
    actorKey: 'generic:1:unit:0:life:0',
  } as typeof first.actor;
  replay.ticks[1]!.events.push({
    eventId: 'awareness-opponent-lost',
    tick: 1,
    type: 'destruction',
    sourceActor: first.actor,
    targetActor: opponent,
  } as unknown as ReplayCausalEvent);
  replay.ticks[2]!.events.push({
    eventId: 'awareness-participant-lost',
    tick: 2,
    type: 'destruction',
    sourceActor: opponent,
    targetActor: first.actor,
  } as unknown as ReplayCausalEvent);

  assert.deepEqual(
    playAwarenessTimeline(replay).activations[0]!.contacts.map((entry) => entry.summary),
    ['opponent destroyed at the play', 'claimed participant destroyed'],
  );
});
