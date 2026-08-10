import assert from 'node:assert/strict';
import test from 'node:test';
import {
  STATE_SETTLES_AT,
  settledStateTick,
} from '../src/render/tickClock';

// A presentation is end-of-tick state, but bodies are drawn gliding across the tick that
// produced it. Reading it at the start of the glide showed every change a tick early --
// most visibly a Kestrel whose vector-dash crosses up to four tiles in one tick, holding
// the orb it was dashing for before it got there.

test('state stays on the previous tick while a body is still gliding', () => {
  const ticks = 100;
  assert.equal(settledStateTick(40, 0, ticks), 39);
  assert.equal(settledStateTick(40, 0.5, ticks), 39);
  assert.equal(
    settledStateTick(40, STATE_SETTLES_AT - 0.01, ticks),
    39,
    'still in flight just before the settle point',
  );
});

test('state settles onto this tick once the glide has effectively landed', () => {
  const ticks = 100;
  assert.equal(settledStateTick(40, STATE_SETTLES_AT, ticks), 40);
  assert.equal(settledStateTick(40, 1, ticks), 40);
});

test('the first tick has no earlier state to hold', () => {
  assert.equal(settledStateTick(0, 0, 100), 0);
  assert.equal(settledStateTick(0, 0.5, 100), 0);
});

test('the final tick settles at once, because nothing follows it', () => {
  // Otherwise a replay parked on its last frame would never show its own outcome.
  assert.equal(settledStateTick(99, 0, 100), 99);
  assert.equal(settledStateTick(99, 0.5, 100), 99);
});

test('the rule never reaches outside the replay', () => {
  for (const count of [1, 2, 50]) {
    for (let tick = 0; tick < count; tick += 1) {
      for (const fraction of [0, 0.5, STATE_SETTLES_AT, 1]) {
        const settled = settledStateTick(tick, fraction, count);
        assert.ok(
          settled >= 0 && settled < count,
          `tick ${tick} fraction ${fraction} of ${count} -> ${settled}`,
        );
        assert.ok(
          settled === tick || settled === tick - 1,
          'state may only be this tick or the one before it',
        );
      }
    }
  }
});
