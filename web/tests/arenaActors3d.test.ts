import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { join } from 'node:path';
import test from 'node:test';
import * as THREE from 'three';
import type { ReplayDocument } from '../src/types';
import { buildActors } from './.harness/harness.entry.js';

/**
 * The reactions a bot has to being shot at, which are the one part of the 2.5D renderer
 * that cannot be judged from a screenshot.
 *
 * A destruction ends an Elimination match, so the collapse plays across the last fraction
 * of the final tick and is then covered by the outcome card — there is no moment to
 * capture. And a hit flash lasts a fraction of a tick. Both are pure functions of the
 * playhead, though, so they can simply be asked.
 */

const replay = JSON.parse(
  readFileSync(join(import.meta.dirname, 'fixtures', 'golden-replay.json'), 'utf8'),
) as ReplayDocument;

/** The tick this fixture's loser dies on, and who it is. */
const DEATH_TICK = 96;
const DEAD_SLOT = 1;

/** Chassis are found by the pick pad they carry, which is the only stable handle. */
function chassisOf(group: THREE.Object3D, slot: number): THREE.Object3D {
  let found: THREE.Object3D | null = null;
  group.traverse((node) => {
    if (node.userData.slot === slot && node.parent) found = node.parent;
  });
  assert.ok(found, `no chassis for slot ${slot}`);
  return found;
}

test('a destroyed bot collapses across the tick it dies in', () => {
  const actors = buildActors(replay);
  const chassis = chassisOf(actors.group, DEAD_SLOT);

  // Early in the tick it is still standing, upright and full size.
  actors.update(DEATH_TICK + 0.2, null, false);
  assert.equal(chassis.visible, true, 'still on the floor when the tick opens');
  assert.equal(chassis.rotation.z, 0, 'upright');
  assert.equal(chassis.scale.x, 1, 'full size');

  // By the end of it, it has gone over, sunk and shrunk.
  actors.update(DEATH_TICK + 0.98, null, false);
  assert.equal(chassis.visible, true, 'the collapse is visible, not skipped');
  assert.ok(chassis.rotation.z > 0.3, `nosed over (${chassis.rotation.z})`);
  assert.ok(chassis.position.y < -0.05, `settled into the floor (${chassis.position.y})`);
  assert.ok(chassis.scale.x < 0.9, `shrunk (${chassis.scale.x})`);

  actors.dispose();
});

test('a firing bot recoils, and only while its shot is in progress', () => {
  const actors = buildActors(replay);
  const shooter = replay.ticks[DEATH_TICK].events.find((event) => event.type === 'Shot')!;
  const chassis = chassisOf(actors.group, shooter.slot!);
  // The body kicks inside the chassis, so the pool of light on the floor stays put.
  const body = chassis.children.find((child) => child.type === 'Group')!;

  actors.update(DEATH_TICK + 0.1, null, false);
  // Not `equal(…, 0)`: the kick multiplies out to −0, and strict equality is `Object.is`,
  // which says −0 is not 0.
  assert.ok(Math.abs(body.position.x) < 1e-9, 'no kick before the shot leaves');

  actors.update(DEATH_TICK + 0.7, null, false);
  assert.ok(body.position.x < -0.01, `kicked backwards (${body.position.x})`);

  actors.dispose();
});

test('a bot drifts through a corner and is never still when it is not', () => {
  const actors = buildActors(replay);
  // An *isolated* corner: a bot that turns and then does not turn again for two ticks.
  // A bot spinning several ticks running holds full slip throughout — correctly, and it
  // never recovers inside the window this test is about.
  let turn = -1;
  let turning = -1;
  for (let index = 0; index < replay.ticks.length - 3 && turn < 0; index++) {
    for (const event of replay.ticks[index].events) {
      if (event.type !== 'Turn') continue;
      const spinning = [1, 2, 3].some((ahead) =>
        replay.ticks[index + ahead]?.events.some(
          (later) => later.type === 'Turn' && later.slot === event.slot,
        ),
      );
      if (!spinning) {
        turn = index;
        turning = event.slot!;
        break;
      }
    }
  }
  assert.ok(turn >= 0, 'the fixture has a bot taking a corner on its own');
  const chassis = chassisOf(actors.group, turning);
  const body = chassis.children.find((child) => child.type === 'Group')!;

  // The slip peaks a little *after* the rotation finishes, and that is the whole point:
  // driven straight from the angular rate it existed only while the bot was turning, which
  // is one tick — gone before it read as anything.
  actors.update(turn + 0.75, null, false);
  const lean = Math.abs(body.rotation.x);
  const slide = Math.abs(body.position.z);
  assert.ok(lean > 0.25, `banked hard into the corner (${lean})`);
  assert.ok(slide > 0.15, `back end stepped out (${slide})`);

  // Still sliding a whole tick after the turn completed…
  actors.update(turn + 1.4, null, false);
  assert.ok(Math.abs(body.position.z) > slide * 0.4, 'the slide outlives the rotation');

  // …and unwound a tick after that, rather than left cocked over for the rest of the match.
  actors.update(turn + 2.6, null, false);
  assert.ok(Math.abs(body.rotation.x) < lean * 0.3, 'recovers out of the corner');

  // Idle life: a bot doing nothing still moves, and moves differently a moment later.
  const still = replay.ticks.findIndex((tick, index) =>
    index > 0 && tick.events.length === 0,
  );
  if (still > 0) {
    const idle = chassisOf(actors.group, 0).children.find((c) => c.type === 'Group')!;
    actors.update(still + 0.1, null, false);
    const first = idle.position.z;
    actors.update(still + 0.6, null, false);
    assert.notEqual(idle.position.z, first, 'not frozen between frames');
  }

  actors.dispose();
});

test('a bot crossing tiles in a row does not stop at every boundary', () => {
  const actors = buildActors(replay);
  // Two consecutive Move ticks for the same bot: the case that used to stop dead between.
  let run = -1;
  let mover = -1;
  for (let tick = 1; tick < replay.ticks.length - 1; tick++) {
    for (const event of replay.ticks[tick].events) {
      if (event.type !== 'Move') continue;
      const again = replay.ticks[tick + 1].events.some(
        (next) => next.type === 'Move' && next.slot === event.slot,
      );
      if (again) {
        run = tick;
        mover = event.slot!;
      }
    }
    if (run >= 0) break;
  }
  assert.ok(run >= 0, 'the fixture has a bot moving two ticks running');

  const chassis = chassisOf(actors.group, mover);
  const sample = (t: number) => {
    actors.update(t, null, false);
    return chassis.position.clone();
  };

  // Speed either side of the shared tile boundary. Eased-per-tick, it was zero there.
  const before = sample(run + 0.97).distanceTo(sample(run + 0.99));
  const after = sample(run + 1.01).distanceTo(sample(run + 1.03));
  assert.ok(before > 0.005, `still moving into the boundary (${before})`);
  assert.ok(after > 0.005, `still moving out of it (${after})`);

  actors.dispose();
});
