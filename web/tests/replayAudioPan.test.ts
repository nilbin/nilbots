import assert from 'node:assert/strict';
import test from 'node:test';
import { replayAudioEventsAt } from '../src/audio/replayAudioEvents.ts';
import { loadReplayObject } from '../src/replayIngress.ts';
import type { ReplayV1GameEvent } from '../src/replayWireV1.ts';
import { replayV1FixtureInput } from './support/replayFixtureInputs.ts';

function replayWith(mapWidth: number, events: ReplayV1GameEvent[]) {
  const wire = replayV1FixtureInput();
  wire.header.mapWidth = mapWidth;
  wire.header.mapTiles = Array.from(
    { length: wire.header.mapHeight },
    () => '.'.repeat(mapWidth),
  );
  wire.ticks[0]!.events = events;
  return loadReplayObject(wire).replay;
}

test('cues pan from the column they happened in', () => {
  const [left] = replayAudioEventsAt(
    replayWith(11, [{ type: 'Shot', fromX: 0, fromY: 0 }]),
    0,
  );
  const [middle] = replayAudioEventsAt(
    replayWith(11, [{ type: 'Shot', fromX: 5, fromY: 0 }]),
    0,
  );
  const [right] = replayAudioEventsAt(
    replayWith(11, [{ type: 'Shot', fromX: 10, fromY: 0 }]),
    0,
  );

  assert.equal(left!.pan, -1);
  assert.equal(middle!.pan, 0);
  assert.equal(right!.pan, 1);
});

test('a placeless event stays centre', () => {
  const [event] = replayAudioEventsAt(
    replayWith(11, [{ type: 'Damage' }]),
    0,
  );
  assert.equal(event!.pan, null);
});

test('a one-column map cannot be panned', () => {
  const [event] = replayAudioEventsAt(
    replayWith(1, [{ type: 'Shot', fromX: 0, fromY: 0 }]),
    0,
  );
  assert.equal(event!.pan, null);
});

test('pan never escapes the stereo field', () => {
  for (const x of [-5, 99]) {
    const [event] = replayAudioEventsAt(
      replayWith(11, [{ type: 'Shot', fromX: x, fromY: 0 }]),
      0,
    );
    assert.ok(
      event!.pan !== null && event!.pan >= -1 && event!.pan <= 1,
      `x=${x} → ${event!.pan}`,
    );
  }
});
