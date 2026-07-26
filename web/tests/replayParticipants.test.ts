import assert from 'node:assert/strict';
import test from 'node:test';
import { participantsBySlot } from '../src/replayParticipants.ts';
import type { ReplayParticipant } from '../src/types.ts';

test('participant lookup follows slots when replay order is reversed', () => {
  const lookup = participantsBySlot([
    participant(1, 'second'),
    participant(0, 'first'),
  ]);

  assert.equal(lookup.get(0)?.name, 'first');
  assert.equal(lookup.get(1)?.name, 'second');
});

test('participant lookup supports sparse slots without treating positions as identities', () => {
  const lookup = participantsBySlot([
    participant(9, 'ninth'),
    participant(3, 'third'),
  ]);

  assert.equal(lookup.get(3)?.name, 'third');
  assert.equal(lookup.get(9)?.name, 'ninth');
  assert.equal(lookup.get(0), undefined);
  assert.equal(lookup.get(1), undefined);
});

function participant(slot: number, name: string): ReplayParticipant {
  return {
    slot,
    name,
    runtimeKind: 'wasm',
    artifactHash: '',
    accent: '#ffffff',
    spawnX: 0,
    spawnY: 0,
    spawnFacing: 'North',
  };
}
