import assert from 'node:assert/strict';
import test from 'node:test';
import { replayMaxHealth } from '../src/replayMetadata.ts';
import type { ReplayDocument } from '../src/types.ts';

test('rules snapshot is authoritative even when all recorded states are damaged', () => {
  assert.equal(replayMaxHealth(replayWithHealth(2, 5)), 5);
});

test('legacy replay recovers a higher custom maximum from recorded state', () => {
  assert.equal(replayMaxHealth(replayWithHealth(7)), 7);
});

test('legacy replay retains the historical three-health default', () => {
  assert.equal(replayMaxHealth(replayWithHealth(2)), 3);
});

function replayWithHealth(
  observedHealth: number,
  maxHealth?: number,
): ReplayDocument {
  return {
    header: {
      maxHealth,
      participants: [],
    },
    ticks: [
      {
        tick: 0,
        state: [
          {
            slot: 0,
            x: 0,
            y: 0,
            facing: 'East',
            health: observedHealth,
            cooldown: 0,
            status: 'Active',
          },
        ],
        bots: [],
        events: [],
      },
    ],
    result: {},
    replayHash: '',
  } as unknown as ReplayDocument;
}
