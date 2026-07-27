import assert from 'node:assert/strict';
import test from 'node:test';
import { participantsById } from '../src/replayParticipants.ts';
import type { ReplayParticipantController } from '../src/replayModel.ts';

test('participant lookup follows IDs when replay order is reversed', () => {
  const lookup = participantsById([
    participant(1, 'second'),
    participant(0, 'first'),
  ]);

  assert.equal(lookup.get(0)?.name, 'first');
  assert.equal(lookup.get(1)?.name, 'second');
});

test('participant lookup supports sparse IDs without treating positions as identities', () => {
  const lookup = participantsById([
    participant(9, 'ninth'),
    participant(3, 'third'),
  ]);

  assert.equal(lookup.get(3)?.name, 'third');
  assert.equal(lookup.get(9)?.name, 'ninth');
  assert.equal(lookup.get(0), undefined);
  assert.equal(lookup.get(1), undefined);
});

function participant(
  participantId: number,
  name: string,
): ReplayParticipantController {
  return {
    participantKey: `participant:${participantId}`,
    participantId,
    teamKey: `team:${participantId}`,
    teamId: participantId,
    name,
    runtimeKind: 'wasm',
    artifactHash: '',
    accent: '#ffffff',
    lookId: null,
    projectileLookId: null,
  };
}
