import assert from 'node:assert/strict';
import test from 'node:test';
import {
  adjustAccentForBackground,
  adjustAccentForLuminance,
  colorContrast,
} from '../src/render/adaptiveAccent.ts';

test('preserves an accent that already clears graphical contrast', () => {
  assert.equal(adjustAccentForLuminance('#38bdf8', 0.01), '#38bdf8');
});

test('darkens an accent only when a light surface needs it', () => {
  const adjusted = adjustAccentForLuminance('#f59e0b', 0.9);
  assert.notEqual(adjusted, '#f59e0b');
  assert.ok((colorContrast(adjusted, '#f3f3f3') ?? 0) >= 3);
});

test('lightens an accent only when a dark surface needs it', () => {
  const adjusted = adjustAccentForLuminance('#101820', 0.005);
  assert.notEqual(adjusted, '#101820');
  assert.ok((colorContrast(adjusted, '#101010') ?? 0) >= 3);
});

test('leaves malformed legacy accents untouched', () => {
  assert.equal(adjustAccentForLuminance('cyan', 0.5), 'cyan');
});

test('supports fixed UI surfaces without sampling a canvas', () => {
  const adjusted = adjustAccentForBackground('#02040a', '#111823');
  assert.ok((colorContrast(adjusted, '#111823') ?? 0) >= 3);
});
