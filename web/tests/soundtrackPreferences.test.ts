import assert from 'node:assert/strict';
import test from 'node:test';
import {
  soundtrackEnabledPreference,
  soundtrackPlaybackMode,
} from '../src/soundtrack/preferences.ts';

test('straight-through playback is the default score mode', () => {
  assert.equal(soundtrackPlaybackMode(''), 'straight');
  assert.equal(soundtrackPlaybackMode('?score=straight'), 'straight');
  assert.equal(soundtrackPlaybackMode('?score=unknown'), 'straight');
  assert.equal(soundtrackPlaybackMode('?score=Adaptive'), 'straight');
});

test('adaptive playback requires the explicit adaptive score query', () => {
  assert.equal(soundtrackPlaybackMode('?score=adaptive'), 'adaptive');
  assert.equal(
    soundtrackPlaybackMode('?audio=off&score=adaptive&replay=example'),
    'adaptive',
  );
});

test('soundtrack playback defaults enabled unless explicitly opted out', () => {
  assert.equal(soundtrackEnabledPreference(null), true);
  assert.equal(soundtrackEnabledPreference('true'), true);
  assert.equal(soundtrackEnabledPreference('false'), false);
  assert.equal(soundtrackEnabledPreference('False'), true);
  assert.equal(soundtrackEnabledPreference(''), true);
});
