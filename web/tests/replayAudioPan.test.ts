import assert from 'node:assert/strict';
import test from 'node:test';
import { replayAudioEventsAt } from '../src/audio/replayAudioEvents.ts';
import type { ReplayDocument } from '../src/types.ts';

/** Minimal replay carrying one tick of events at chosen columns. */
function replayWith(mapWidth: number, events: { type: string; fromX?: number }[]) {
  return {
    header: { mapWidth },
    ticks: [{ tick: 0, events }],
  } as unknown as ReplayDocument;
}

test('cues pan from the column they happened in', () => {
  const [left] = replayAudioEventsAt(replayWith(11, [{ type: 'Shot', fromX: 0 }]), 0);
  const [middle] = replayAudioEventsAt(replayWith(11, [{ type: 'Shot', fromX: 5 }]), 0);
  const [right] = replayAudioEventsAt(replayWith(11, [{ type: 'Shot', fromX: 10 }]), 0);

  assert.equal(left.pan, -1);
  assert.equal(middle.pan, 0);
  assert.equal(right.pan, 1);
});

test('a placeless event stays centre', () => {
  // Nothing in the world produced it, so panning it would put a UI reward off to one side.
  const [event] = replayAudioEventsAt(replayWith(11, [{ type: 'Damage' }]), 0);
  assert.equal(event.pan, null);
});

test('a one-column map cannot be panned', () => {
  // Guards the division: width - 1 would be zero.
  const [event] = replayAudioEventsAt(replayWith(1, [{ type: 'Shot', fromX: 0 }]), 0);
  assert.equal(event.pan, null);
});

test('pan never escapes the stereo field', () => {
  // A malformed replay must not produce a pan outside [-1, 1]; the Web Audio node would
  // throw and take the cue with it.
  for (const x of [-5, 99]) {
    const [event] = replayAudioEventsAt(replayWith(11, [{ type: 'Shot', fromX: x }]), 0);
    assert.ok(event.pan !== null && event.pan >= -1 && event.pan <= 1, `x=${x} → ${event.pan}`);
  }
});
