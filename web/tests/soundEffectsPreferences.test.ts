import assert from 'node:assert/strict';
import test from 'node:test';
import {
  DEFAULT_SOUND_EFFECTS_VOLUME,
  soundEffectsMutedPreference,
  soundEffectsVolumePreference,
} from '../src/audio/soundEffectsPreferences.ts';

test('sound effects default audible unless explicitly muted', () => {
  assert.equal(soundEffectsMutedPreference(null), false);
  assert.equal(soundEffectsMutedPreference('false'), false);
  assert.equal(soundEffectsMutedPreference('true'), true);
  assert.equal(soundEffectsMutedPreference('True'), false);
  assert.equal(soundEffectsMutedPreference(''), false);
});

test('sound-effects volume accepts only normalized persisted values', () => {
  assert.equal(soundEffectsVolumePreference(null), DEFAULT_SOUND_EFFECTS_VOLUME);
  assert.equal(soundEffectsVolumePreference('0'), 0);
  assert.equal(soundEffectsVolumePreference('0.4'), 0.4);
  assert.equal(soundEffectsVolumePreference('1'), 1);
  assert.equal(soundEffectsVolumePreference('-0.1'), DEFAULT_SOUND_EFFECTS_VOLUME);
  assert.equal(soundEffectsVolumePreference('1.1'), DEFAULT_SOUND_EFFECTS_VOLUME);
  assert.equal(soundEffectsVolumePreference('loud'), DEFAULT_SOUND_EFFECTS_VOLUME);
});
