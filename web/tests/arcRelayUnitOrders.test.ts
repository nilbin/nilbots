import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import test from 'node:test';
import { gunzipSync } from 'node:zlib';
// Through the SSR-built harness, like every other presenter test here: the
// presenter resolves looks and accents, and that reaches `import.meta.glob`,
// which only exists inside Vite.
import { createPresenter, loadReplayObject } from './.harness/harness.entry.js';
import type { ArcRelayBroadcastV1 } from '../src/replayBroadcastV1.ts';
import { parseCommandReason } from '../src/presentation/commandReason.ts';

/**
 * What a selected body is DOING, across the two documents that can carry it.
 *
 * A canonical replay keeps every body's diagnostic text. A broadcast drops all
 * of it — deliberately, it is private reasoning — and used to leave a selected
 * body in a gallery with a role tag and nothing else, all match. The compact
 * `orders` column publishes back the one clause a spectator is asking about,
 * and this pins the two halves that make that trustworthy: the split is the
 * same one the projection script performs, and the column is a delta the viewer
 * carries forward rather than a table repeated per tick.
 */

function fixture(): ArcRelayBroadcastV1 {
  return JSON.parse(
    gunzipSync(
      readFileSync(
        new URL(
          '../../arena-bots/arc-relay/stock-mind-v0/preflight/broadcast.json.gz',
          import.meta.url,
        ),
      ),
    ).toString('utf8'),
  ) as ArcRelayBroadcastV1;
}

function truncated(broadcast: ArcRelayBroadcastV1, ticks: number): void {
  broadcast.worlds = broadcast.worlds.slice(0, ticks);
  broadcast.turns = broadcast.turns.slice(0, ticks);
  broadcast.startEvents = broadcast.startEvents.slice(0, ticks);
  broadcast.events = broadcast.events.slice(0, ticks);
  broadcast.traversals = broadcast.traversals.slice(0, ticks);
  broadcast.births = broadcast.births.slice(0, ticks);
  if (broadcast.vision !== undefined) {
    broadcast.vision = broadcast.vision.slice(0, ticks);
  }
}

test('a mind diagnostic splits into the order it names and what it is doing', () => {
  // The exact twin of `command_reason` in scripts/arc-relay-broadcast.py. A
  // broadcast and a canonical replay of the same match must read identically,
  // so these cases are the contract between the two implementations.
  assert.deepEqual(
    parseCommandReason('tp:prowl:shadow-group:ghost-patrol:formation-move via East'),
    { orderId: 'ghost-patrol', action: 'formation-move' },
  );
  assert.deepEqual(
    parseCommandReason('idle-break:tp:prowl:well-group:race-north:streak via North'),
    { orderId: 'race-north', action: 'idle-break/streak' },
  );
  assert.deepEqual(
    parseCommandReason('turn for tp:prowl:well-group:well-watch:formation-move'),
    { orderId: 'well-watch', action: 'turn/formation-move' },
  );
  // Unrecognised vocabulary is carried whole rather than discarded: a reader
  // must not decide that a word it was not taught is not worth showing.
  assert.deepEqual(parseCommandReason('custody:committed-delivery-wait'), {
    orderId: null,
    action: 'custody:committed-delivery-wait',
  });
  assert.equal(parseCommandReason(null), null);
  assert.equal(parseCommandReason(''), null);
  assert.equal(parseCommandReason('   '), null);
  assert.equal(
    parseCommandReason('x'.repeat(120))?.action.length,
    48,
    'a pathological diagnostic is capped, not carried',
  );
});

test('the published order column is a delta the viewer carries forward', () => {
  const broadcast = fixture();
  truncated(broadcast, 3);
  const [teamId, unitId] = [
    broadcast.turns[0]![0]![0][0],
    broadcast.turns[0]![0]![0][1],
  ];
  broadcast.orders = [
    [[teamId, unitId, 'race-north', 'formation-move']],
    [],
    [[teamId, unitId, 'race-north', 'duel-stand']],
  ];

  const { replay } = loadReplayObject(broadcast);
  assert.deepEqual(replay.ticks[0]!.publishedUnitOrders, [
    { teamId, unitId, orderId: 'race-north', action: 'formation-move' },
  ]);
  // The silent tick keeps the order rather than clearing it — which is the
  // whole reason the column can afford to be a delta.
  assert.deepEqual(replay.ticks[1]!.publishedUnitOrders, [
    { teamId, unitId, orderId: 'race-north', action: 'formation-move' },
  ]);
  assert.deepEqual(replay.ticks[2]!.publishedUnitOrders, [
    { teamId, unitId, orderId: 'race-north', action: 'duel-stand' },
  ]);

  const presenter = createPresenter(replay);
  const unit = presenter
    .at(2)
    .units.find(
      (candidate) => candidate.teamId === teamId && candidate.unitId === unitId,
    );
  assert.ok(unit);
  assert.deepEqual(unit.order, {
    orderId: 'race-north',
    action: 'duel-stand',
  });
  // Nobody else invented one.
  assert.ok(
    presenter
      .at(2)
      .units.filter((candidate) => candidate.unitKey !== unit.unitKey)
      .every((candidate) => candidate.order === null),
  );
});

test('a broadcast written before published orders still loads', () => {
  const broadcast = fixture();
  truncated(broadcast, 2);
  delete broadcast.orders;

  const { replay } = loadReplayObject(broadcast);
  assert.equal(replay.ticks.length, 2);
  assert.equal(replay.ticks[0]!.publishedUnitOrders, undefined);
  assert.ok(
    createPresenter(replay)
      .at(0)
      .units.every((unit) => unit.order === null),
    'an absent column renders as nothing at all, never as a broken panel',
  );
});

test('an order column of the wrong length is rejected rather than misaligned', () => {
  const broadcast = fixture();
  truncated(broadcast, 3);
  broadcast.orders = [[]];
  assert.throws(() => loadReplayObject(broadcast), /equal tick counts/);
});
