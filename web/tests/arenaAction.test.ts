import assert from 'node:assert/strict';
import test from 'node:test';
import {
  arenaModeParticipantsReady,
  defaultChallengeContextRole,
  resolveChallengeParticipants,
} from './.harness/harness.entry.js';

test('a direct Challenge action pins the contextual bot as the opponent', () => {
  const role = defaultChallengeContextRole();

  assert.equal(role, 'opponent');
  assert.deepEqual(
    resolveChallengeParticipants(
      'viewed-bot',
      role,
      'my-selected-bot',
      'ignored-opponent',
    ),
    {
      botId: 'my-selected-bot',
      opponentBotId: 'viewed-bot',
    },
  );
});

test('an owned direct Challenge action also targets the contextual bot', () => {
  const role = defaultChallengeContextRole();

  assert.equal(role, 'opponent');
  assert.deepEqual(
    resolveChallengeParticipants(
      'viewed-owned-bot',
      role,
      'my-other-bot',
      '',
    ),
    {
      botId: 'my-other-bot',
      opponentBotId: 'viewed-owned-bot',
    },
  );
});

test('explicit Play and replay actions keep an owned contextual bot as entrant', () => {
  assert.deepEqual(
    resolveChallengeParticipants(
      'my-context-bot',
      'entrant',
      '',
      'selected-opponent',
    ),
    {
      botId: 'my-context-bot',
      opponentBotId: 'selected-opponent',
    },
  );
});

test('participant readiness is scoped to the selected mode', () => {
  assert.equal(arenaModeParticipantsReady('ranked', false, false), true);
  assert.equal(arenaModeParticipantsReady('challenge', false, true), false);
  assert.equal(arenaModeParticipantsReady('challenge', true, false), true);
  assert.equal(arenaModeParticipantsReady('labs', true, false), false);
  assert.equal(arenaModeParticipantsReady('labs', false, true), true);
});
