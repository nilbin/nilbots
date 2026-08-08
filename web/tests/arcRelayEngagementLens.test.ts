import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import test from 'node:test';
import { gunzipSync } from 'node:zlib';
import type * as THREE from 'three';
import type {
  ReplayModel,
  ReplayStableUnitKey,
} from '../src/replayModel.ts';
import type { ArcRelayBroadcastV1 } from '../src/replayBroadcastV1.ts';
import {
  buildActors,
  buildOverlays,
  loadReplayObject,
} from './.harness/harness.entry.js';

/**
 * The two always-on reads the arena grew for the owner's 2026-08-09 review: who
 * is FIGHTING, and where the selected body is WALKING.
 *
 * Both are cues on the 3D scene graph, which a screenshot review cannot hold to
 * account — a mark that lights for every body, or one that never lights at all,
 * looks plausible in a still. They are pure functions of the playhead and the
 * selection, so they are simply asked here instead.
 *
 * The fixture is the stock-mind preflight broadcast, the same document the order
 * column tests use, with its published columns synthesised: what is under test
 * is the cue, not the mind that happened to produce a reason.
 */

const FIXTURE = new URL(
  '../../arena-bots/arc-relay/stock-mind-v0/preflight/broadcast.json.gz',
  import.meta.url,
);

function fixture(ticks: number): ArcRelayBroadcastV1 {
  const broadcast = JSON.parse(
    gunzipSync(readFileSync(FIXTURE)).toString('utf8'),
  ) as ArcRelayBroadcastV1;
  broadcast.worlds = broadcast.worlds.slice(0, ticks);
  broadcast.turns = broadcast.turns.slice(0, ticks);
  broadcast.startEvents = broadcast.startEvents.slice(0, ticks);
  broadcast.events = broadcast.events.slice(0, ticks);
  broadcast.traversals = broadcast.traversals.slice(0, ticks);
  broadcast.births = broadcast.births.slice(0, ticks);
  if (broadcast.vision !== undefined) {
    broadcast.vision = broadcast.vision.slice(0, ticks);
  }
  if (broadcast.orders !== undefined) {
    broadcast.orders = broadcast.orders.slice(0, ticks);
  }
  if (broadcast.destinations !== undefined) {
    broadcast.destinations = broadcast.destinations.slice(0, ticks);
  }
  return broadcast;
}

/** The first body that is actually on the field at tick 0, and its team/unit ids. */
function firstBody(broadcast: ArcRelayBroadcastV1): {
  teamId: number;
  unitId: number;
  unitKey: ReplayStableUnitKey;
} {
  const [teamId, unitId] = [
    broadcast.turns[0]![0]![0][0],
    broadcast.turns[0]![0]![0][1],
  ];
  return {
    teamId,
    unitId,
    unitKey: `generic:${teamId}:unit:${unitId}` as ReplayStableUnitKey,
  };
}

function cue(
  group: THREE.Object3D,
  name: string,
  forUnitKey?: ReplayStableUnitKey,
): THREE.Object3D {
  let found: THREE.Object3D | null = null;
  group.traverse((node) => {
    if (
      node.userData.cue === name &&
      (forUnitKey === undefined || node.userData.forUnitKey === forUnitKey)
    ) {
      found = node;
    }
  });
  assert.ok(found, `no ${name} cue${forUnitKey ? ` for ${forUnitKey}` : ''}`);
  return found;
}

function engagedUnitKeys(group: THREE.Object3D): ReplayStableUnitKey[] {
  const keys: ReplayStableUnitKey[] = [];
  group.traverse((node) => {
    if (node.userData.cue !== 'engaged-mark') return;
    // The mark hangs inside the health-pip row, which is what hides it with the
    // body — so "shown" means the whole chain up to the scene is shown.
    let shown = node.visible;
    for (let at = node.parent; at && shown; at = at.parent) shown = at.visible;
    if (shown) keys.push(node.userData.forUnitKey as ReplayStableUnitKey);
  });
  return keys.sort();
}

function loadedWithOrder(action: string): {
  replay: ReplayModel;
  unitKey: ReplayStableUnitKey;
} {
  const broadcast = fixture(2);
  const { teamId, unitId, unitKey } = firstBody(broadcast);
  broadcast.orders = [[[teamId, unitId, 'race-north', action]], []];
  return { replay: loadReplayObject(broadcast).replay, unitKey };
}

test('the engaged mark lights for the body in a fight, and for nobody else', () => {
  const { replay, unitKey } = loadedWithOrder('duel-stand');
  const actors = buildActors(replay);
  try {
    actors.update(1, null, false);
    assert.deepEqual(
      engagedUnitKeys(actors.group),
      [unitKey],
      'exactly the body whose order names a fight',
    );
  } finally {
    actors.dispose();
  }
});

test('walking is not fighting: the mark stays dark for a movement order', () => {
  // The whole value of the cue is that it separates committed from passing-by.
  // A mark that lit for `formation-move` would light for the entire team all
  // match and mean nothing at all.
  const { replay } = loadedWithOrder('formation-move');
  const actors = buildActors(replay);
  try {
    actors.update(1, null, false);
    assert.deepEqual(engagedUnitKeys(actors.group), []);
  } finally {
    actors.dispose();
  }
});

test('the pathing lens points at the published destination, for the selection only', () => {
  const broadcast = fixture(2);
  const { teamId, unitId, unitKey } = firstBody(broadcast);
  broadcast.destinations = [[[teamId, unitId, 9, 4]], []];
  const { replay } = loadReplayObject(broadcast);

  const overlays = buildOverlays(replay);
  try {
    overlays.update(1, unitKey, false);
    const target = cue(overlays.group, 'pathing-lens-target');
    assert.ok(target.visible, 'the destination ring is drawn');
    // Tile centres, like every other ground mark in this renderer.
    assert.equal(target.position.x, 9.5);
    assert.equal(target.position.z, 4.5);

    const line = cue(overlays.group, 'pathing-lens-line');
    assert.ok(
      line.visible,
      'a body away from its destination gets the hairline too',
    );

    // Nothing selected is nothing drawn: eight of these at once would be a
    // diagram rather than an arena.
    overlays.update(1, null, false);
    assert.equal(cue(overlays.group, 'pathing-lens-target').visible, false);
    assert.equal(cue(overlays.group, 'pathing-lens-line').visible, false);
  } finally {
    overlays.dispose();
  }
});

test('a body that stops naming a destination loses its marker', () => {
  // A stale marker standing over a body that has moved on is worse than no
  // marker: it is the lens lying about the one thing it exists to answer.
  const broadcast = fixture(3);
  const { teamId, unitId, unitKey } = firstBody(broadcast);
  broadcast.destinations = [
    [[teamId, unitId, 9, 4]],
    [],
    [[teamId, unitId, null, null]],
  ];
  const { replay } = loadReplayObject(broadcast);

  const overlays = buildOverlays(replay);
  try {
    overlays.update(1, unitKey, false);
    assert.ok(cue(overlays.group, 'pathing-lens-target').visible);
    overlays.update(2, unitKey, false);
    assert.equal(cue(overlays.group, 'pathing-lens-target').visible, false);
  } finally {
    overlays.dispose();
  }
});
