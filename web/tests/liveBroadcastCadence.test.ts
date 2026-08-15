import assert from 'node:assert/strict';
import test from 'node:test';
import {
  liveClockPollMilliseconds,
  replayPrefixPollMilliseconds,
} from './.harness/harness.entry.js';

test('the causal clock is sampled at least twice per presented tick', () => {
  assert.equal(liveClockPollMilliseconds(), 500);
  assert.equal(liveClockPollMilliseconds({
    presentationTicksPerSecond: 1.25,
    broadcastComplete: false,
  }), 400);
  assert.equal(liveClockPollMilliseconds({
    presentationTicksPerSecond: 2.5,
    broadcastComplete: false,
  }), 200);
  assert.equal(liveClockPollMilliseconds({
    presentationTicksPerSecond: 1.25,
    broadcastComplete: true,
  }), false);
});

test('the replay prefix is fetched only after the clock reveals another tick', () => {
  const live = {
    presentationTicksPerSecond: 1.25,
    presentationTick: 12,
    broadcastComplete: false,
  };
  assert.equal(replayPrefixPollMilliseconds(live, 12), 100);
  assert.equal(replayPrefixPollMilliseconds(live, 13), false);
  assert.equal(replayPrefixPollMilliseconds({ ...live, broadcastComplete: true }, 12), false);
});
