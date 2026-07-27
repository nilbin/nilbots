import assert from 'node:assert/strict';
import test from 'node:test';
import {
  DEFAULT_SOUNDTRACK_VOLUME,
  soundtrackEnabledPreference,
  soundtrackPlaybackMode,
  soundtrackVolumePreference,
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

test('soundtrack volume defaults to the lower game mix', () => {
  assert.equal(DEFAULT_SOUNDTRACK_VOLUME, 0.4);
  assert.equal(soundtrackVolumePreference(null), 0.4);
  assert.equal(soundtrackVolumePreference('0'), 0);
  assert.equal(soundtrackVolumePreference('0.35'), 0.35);
  assert.equal(soundtrackVolumePreference('1'), 1);
  assert.equal(soundtrackVolumePreference('-0.1'), 0.4);
  assert.equal(soundtrackVolumePreference('1.1'), 0.4);
  assert.equal(soundtrackVolumePreference('loud'), 0.4);
});
