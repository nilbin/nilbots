import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { join } from 'node:path';
import test from 'node:test';
import * as THREE from 'three';
import type {
  ReplayModel,
  ReplayStableUnitKey,
} from '../src/replayModel.ts';
import { loadReplayJson } from '../src/replayIngress.ts';
import {
  boltsAt,
  buildActors,
  buildOverlays,
} from './.harness/harness.entry.js';

/**
 * The reactions a bot has to being shot at, which are the one part of the 2.5D renderer
 * that cannot be judged from a screenshot.
 *
 * A destruction ends an Elimination match, so the collapse plays across the last fraction
 * of the final tick and is then covered by the outcome card — there is no moment to
 * capture. And a hit flash lasts a fraction of a tick. Both are pure functions of the
 * playhead, though, so they can simply be asked.
 */

const replay = loadReplayJson(
  readFileSync(
    join(import.meta.dirname, 'fixtures', 'golden-replay.json'),
    'utf8',
  ),
).replay;
const frontline = loadReplayJson(
  readFileSync(
    join(import.meta.dirname, 'fixtures', 'frontline-replay-v2.json'),
    'utf8',
  ),
).replay;
const emptyFrontlinePrefix = loadReplayJson(
  readFileSync(
    join(
      import.meta.dirname,
      'fixtures',
      'frontline-replay-v2-partial-zero-tick.json',
    ),
    'utf8',
  ),
).replay;

/** The tick this fixture's loser dies on, and who it is. */
const DEATH_TICK = 96;
const DEAD_UNIT: ReplayStableUnitKey = 'duel:1:unit:0';

/** Chassis are found by the pick pad they carry, which is the only stable handle. */
function chassisOf(
  group: THREE.Object3D,
  unitKey: ReplayStableUnitKey,
): THREE.Object3D {
  let found: THREE.Object3D | null = null;
  group.traverse((node) => {
    if (node.userData.unitKey === unitKey && node.parent) found = node.parent;
  });
  assert.ok(found, `no chassis for unit ${unitKey}`);
  return found;
}

function visibleUnitKeys(group: THREE.Object3D): ReplayStableUnitKey[] {
  return group.children
    .filter(
      (child) =>
        child.visible &&
        typeof child.userData.unitKey === 'string',
    )
    .map((child) => child.userData.unitKey as ReplayStableUnitKey)
    .sort();
}

function formPart(
  chassis: THREE.Object3D,
  form: 'mobile' | 'stationary-omnidirectional',
): THREE.Object3D {
  const part = chassis.children.find(
    (child) => child.userData.renderForm === form,
  );
  assert.ok(part, `no ${form} form on chassis`);
  return part;
}

test('a destroyed bot collapses across the tick it dies in', () => {
  const actors = buildActors(replay);
  const chassis = chassisOf(actors.group, DEAD_UNIT);

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
  const shooter = replay.ticks[DEATH_TICK].events.find(
    (event) => event.type === 'shot',
  )!;
  assert.ok(shooter.sourceActor);
  const chassis = chassisOf(actors.group, shooter.sourceActor.unitKey);
  // The body kicks inside the chassis, so the pool of light on the floor stays put.
  const body = chassis.children.find(
    (child) => child.userData.renderForm === 'mobile',
  )!;

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
  // An isolated corner: consecutive turns should hold full slip rather than recover.
  let turn = -1;
  let turning: ReplayModel['ticks'][number]['events'][number]['sourceActor'] =
    null;
  for (
    let index = 0;
    index < replay.ticks.length - 3 && turn < 0;
    index++
  ) {
    for (const event of replay.ticks[index].events) {
      if (event.type !== 'turn' || !event.sourceActor) continue;
      const spinning = [1, 2, 3].some((ahead) =>
        replay.ticks[index + ahead]?.events.some(
          (later) =>
            later.type === 'turn' &&
            later.sourceActor?.actorKey ===
              event.sourceActor?.actorKey,
        ),
      );
      if (!spinning) {
        turn = index;
        turning = event.sourceActor;
        break;
      }
    }
  }
  assert.ok(turn >= 0, 'the fixture has a bot taking a corner on its own');
  assert.ok(turning);
  const chassis = chassisOf(actors.group, turning.unitKey);
  const body = chassis.children.find(
    (child) => child.userData.renderForm === 'mobile',
  )!;

  // Slip peaks after the rotation and remains readable into the following tick.
  actors.update(turn + 0.75, null, false);
  const lean = Math.abs(body.rotation.x);
  const slide = Math.abs(body.position.z);
  assert.ok(lean > 0.25, `banked hard into the corner (${lean})`);
  assert.ok(slide > 0.15, `back end stepped out (${slide})`);

  actors.update(turn + 1.4, null, false);
  assert.ok(
    Math.abs(body.position.z) > slide * 0.4,
    'the slide outlives the rotation',
  );
  actors.update(turn + 2.6, null, false);
  assert.ok(
    Math.abs(body.rotation.x) < lean * 0.3,
    'recovers out of the corner',
  );

  // Idle life: a bot doing nothing still moves, and moves differently a moment later.
  const still = replay.ticks.findIndex((tick, index) =>
    index > 0 && tick.events.length === 0,
  );
  if (still > 0) {
    const idle = chassisOf(
      actors.group,
      'duel:0:unit:0',
    ).children.find((child) => child.userData.renderForm === 'mobile')!;
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
  let mover: ReplayStableUnitKey | null = null;
  for (let tick = 1; tick < replay.ticks.length - 1; tick++) {
    for (const event of replay.ticks[tick].events) {
      if (event.type !== 'move' || !event.sourceActor) continue;
      const again = replay.ticks[tick + 1].events.some(
        (next) =>
          next.type === 'move' &&
          next.sourceActor?.actorKey === event.sourceActor?.actorKey,
      );
      if (again) {
        run = tick;
        mover = event.sourceActor.unitKey;
      }
    }
    if (run >= 0) break;
  }
  assert.ok(
    run >= 0 && mover !== null,
    'the fixture has a bot moving two ticks running',
  );

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

test('Frontline rigs follow variable stable-unit lifecycle without leaving old lives behind', () => {
  const actors = buildActors(frontline);

  actors.update(0.5, null, false);
  assert.deepEqual(visibleUnitKeys(actors.group), [
    'frontline:0:unit:0',
    'frontline:1:unit:0',
  ]);

  actors.update(1.5, null, false);
  assert.deepEqual(
    visibleUnitKeys(actors.group),
    [
      'frontline:0:unit:0',
      'frontline:1:unit:0',
    ],
    'fabrication-queued slots have no invented body',
  );

  actors.update(2.25, null, false);
  assert.deepEqual(visibleUnitKeys(actors.group), [
    'frontline:0:unit:0',
    'frontline:0:unit:1',
    'frontline:1:unit:0',
    'frontline:1:unit:1',
  ]);
  assert.equal(
    chassisOf(actors.group, 'frontline:0:unit:2').visible,
    false,
    'locked slots remain absent',
  );

  actors.dispose();
});

test('Anchor telegraphs pending state, then authoritatively switches to a stationary 360 body', () => {
  const actors = buildActors(frontline);
  const chassis = chassisOf(
    actors.group,
    'frontline:0:unit:1',
  );
  const mobile = formPart(chassis, 'mobile');
  const turret = formPart(chassis, 'stationary-omnidirectional');
  const anchor = chassis.children.find(
    (child) => child.userData.cue === 'form-transition-pending',
  );
  assert.ok(anchor);

  actors.update(9.25, 'frontline:0:unit:1', false);
  assert.equal(mobile.visible, true);
  assert.equal(turret.visible, false);
  assert.equal(anchor.visible, true);
  assert.equal(chassis.userData.formId, 'child-mobile');

  actors.update(9.99, 'frontline:0:unit:1', false);
  assert.equal(mobile.visible, false);
  assert.equal(turret.visible, true);
  assert.equal(anchor.visible, false);
  assert.equal(chassis.userData.formId, 'turret');
  assert.equal(chassis.userData.stationary, true);
  assert.equal(chassis.userData.omnidirectional, true);

  actors.update(10.1, 'frontline:0:unit:1', false);
  const position = chassis.position.clone();
  actors.update(10.8, 'frontline:0:unit:1', false);
  assert.equal(
    chassis.position.x,
    position.x,
    'absolute turret fire does not move the body',
  );
  assert.equal(chassis.position.z, position.z);

  actors.dispose();
});

test('projectile interpolation keeps exact owner identity and all eight-way headings', () => {
  const diagonal = structuredClone(frontline) as ReplayModel;
  const traversal = diagonal.ticks[10]!.projectileTraversals[0]!;
  traversal.path = [
    {
      x: traversal.from.x + 1,
      y: traversal.from.y - 1,
    },
  ];

  const bolt = boltsAt(diagonal, 10.5).find(
    (candidate) => candidate.id === traversal.projectileId,
  );
  assert.ok(bolt);
  assert.equal(bolt.heading, 'north-east');
  assert.equal(bolt.ownerActor.actorKey, traversal.ownerActor.actorKey);
  assert.equal(bolt.x, traversal.from.x + 0.5);
  assert.equal(bolt.y, traversal.from.y - 0.5);
});

test('3D overlays show the five-position Frontline and absent-unit lifecycle cues', () => {
  const fivePositionReplay = structuredClone(frontline) as ReplayModel;
  assert.ok(fivePositionReplay.map.frontline);
  fivePositionReplay.map.frontline.positions = [
    { positionIndex: 0, tiles: [{ x: 2, y: 2 }] },
    { positionIndex: 1, tiles: [{ x: 3, y: 2 }] },
    { positionIndex: 2, tiles: [{ x: 4, y: 2 }] },
    { positionIndex: 3, tiles: [{ x: 5, y: 2 }] },
    { positionIndex: 4, tiles: [{ x: 6, y: 2 }] },
  ];
  const overlays = buildOverlays(fivePositionReplay);
  overlays.update(1.5, null, false);

  const positionMeshes: THREE.Object3D[] = [];
  const lifecycleMeshes: THREE.Object3D[] = [];
  overlays.group.traverse((node) => {
    if (typeof node.userData.positionIndex === 'number')
      positionMeshes.push(node);
    if (typeof node.userData.lifecycleStatus === 'string')
      lifecycleMeshes.push(node);
  });
  assert.equal(positionMeshes.length, 5);
  assert.equal(
    positionMeshes.filter((mesh) => mesh.userData.active).length,
    1,
  );
  const queued = lifecycleMeshes.find(
    (mesh) =>
      mesh.userData.unitKey === 'frontline:0:unit:1' &&
      mesh.userData.lifecycleStatus === 'fabrication-queued',
  );
  assert.ok(queued?.visible, 'queued fabrication is marked on its reserved pad');
  const locked = lifecycleMeshes.find(
    (mesh) =>
      mesh.userData.unitKey === 'frontline:0:unit:2' &&
      mesh.userData.lifecycleStatus === 'locked',
  );
  assert.equal(
    locked?.visible,
    false,
    'a child without a reservation has no invented arena position',
  );

  overlays.update(2.25, null, false);
  assert.equal(
    queued.visible,
    false,
    'the lifecycle cue yields to the authoritative new body',
  );

  overlays.dispose();
});

test('zero-tick replay-v2 prefixes do not invent bodies, lifecycle, or an active position', () => {
  const actors = buildActors(emptyFrontlinePrefix);
  actors.update(0, null, false);
  assert.deepEqual(visibleUnitKeys(actors.group), []);

  const overlays = buildOverlays(emptyFrontlinePrefix);
  overlays.update(0, null, false);
  overlays.group.traverse((node) => {
    if (typeof node.userData.unitKey === 'string')
      assert.equal(node.visible, false);
    if (typeof node.userData.positionIndex === 'number')
      assert.equal(node.userData.active, false);
  });

  actors.dispose();
  overlays.dispose();
});
