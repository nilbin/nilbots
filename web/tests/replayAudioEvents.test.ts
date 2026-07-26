import assert from 'node:assert/strict';
import test from 'node:test';
import { replayAudioEventsAt } from '../src/audio/replayAudioEvents.ts';
import type { ReplayDocument } from '../src/types.ts';

test('authoritative combat events schedule their matching presentation cues', () => {
  const replay = replayWithEvents([
    { type: 'Shot', slot: 0 },
    { type: 'Damage', targetSlot: 1, amount: 1, newHealth: 1 },
    { type: 'Destroyed', slot: 1 },
  ]);
  assert.deepEqual(
    replayAudioEventsAt(replay, 0).map((event) => event.cue),
    ['projectile', 'impact', 'destroyed'],
  );
});

test('the review build trails match completion with the unlock candidate', () => {
  const replay = replayWithEvents([{ type: 'Destroyed', slot: 1 }]);
  replay.result = {
    winnerSlot: 0,
    reason: 'Elimination',
    endTick: 0,
    bots: [],
  };
  const events = replayAudioEventsAt(replay, 0);
  assert.deepEqual(
    events.map((event) => event.cue),
    ['destroyed', 'unlock'],
  );
  assert.ok(events[1].tickOffset > 1);
});

test('draws and non-events do not invent audio from replay state', () => {
  const replay = replayWithEvents([]);
  replay.result = {
    winnerSlot: null,
    reason: 'MaxTicks',
    endTick: 0,
    bots: [],
  };
  assert.deepEqual(replayAudioEventsAt(replay, 0), []);
  assert.deepEqual(replayAudioEventsAt(replay, 99), []);
});

function replayWithEvents(
  events: { type: 'Shot' | 'Damage' | 'Destroyed'; [key: string]: unknown }[],
): ReplayDocument {
  return {
    header: {
      participants: [],
    },
    ticks: [
      {
        tick: 0,
        bots: [],
        events,
        state: [],
      },
    ],
  } as unknown as ReplayDocument;
}
