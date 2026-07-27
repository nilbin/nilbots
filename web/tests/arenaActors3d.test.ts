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
