import assert from 'node:assert/strict';
import test from 'node:test';
import { replayMaxHealth } from '../src/replayMetadata.ts';
import { loadReplayObject } from '../src/replayIngress.ts';
import { replayV1FixtureInput } from './support/replayFixtureInputs.ts';

test('rules snapshot is authoritative even when all recorded states are damaged', () => {
  assert.equal(replayMaxHealth(replayWithHealth(2, 5)), 5);
});

test('legacy replay recovers a higher custom maximum from recorded state', () => {
  assert.equal(replayMaxHealth(replayWithHealth(7)), 7);
});

test('legacy replay retains the historical three-health default', () => {
  assert.equal(replayMaxHealth(replayWithHealth(2)), 3);
});

function replayWithHealth(observedHealth: number, maxHealth?: number) {
  const wire = replayV1FixtureInput();
  if (maxHealth === undefined) delete wire.header.maxHealth;
  else wire.header.maxHealth = maxHealth;
  for (const state of wire.ticks[0]!.state) {
    state.health = observedHealth;
  }
  return loadReplayObject(wire).replay;
}
