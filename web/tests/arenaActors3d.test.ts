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
 * The reactions a bot has to being shot at, which are the one part of the 3D renderer
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
  form: 'mobile' | 'stationary-omnidirectional' | 'stance-directional',
): THREE.Object3D {
  const part = chassis.children.find(
    (child) => child.userData.renderForm === form,
  );
  assert.ok(part, `no ${form} form on chassis`);
  return part;
}

function healthPipsOf(
  group: THREE.Object3D,
  unitKey: ReplayStableUnitKey,
): THREE.Object3D {
  let found: THREE.Object3D | null = null;
  group.traverse((node) => {
    if (
      node.userData.cue === 'health-pips' &&
      node.userData.forUnitKey === unitKey
    ) {
      found = node;
    }
  });
  assert.ok(found, `no health pips for unit ${unitKey}`);
  return found;
}

function assertCenteredPips(
  pips: THREE.Object3D,
  expectedCount: number,
): void {
  const visible = pips.children.filter((pip) => pip.visible);
  assert.equal(visible.length, expectedCount);
  assert.ok(expectedCount > 0);
  const first = visible[0]!.position.x;
  const last = visible.at(-1)!.position.x;
  assert.ok(
    Math.abs(first + last) < 1e-9,
    `pip row is not centred (${first} to ${last})`,
  );
}

test('a destroyed bot collapses across the tick it dies in', () => {
  const actors = buildActors(replay);
  const chassis = chassisOf(actors.group, DEAD_UNIT);
  const deathPosition = replay.ticks[DEATH_TICK].before.actors.find(
    (actor) => actor.unitKey === DEAD_UNIT,
  )?.position;
  assert.ok(deathPosition);

  // Early in the tick it is still standing, upright and full size.
  actors.update(DEATH_TICK + 0.2, null, false);
  assert.equal(chassis.visible, true, 'still on the floor when the tick opens');
  assert.equal(chassis.rotation.z, 0, 'upright');
  assert.equal(chassis.scale.x, 1, 'full size');

  // By the end of it, it has gone over, sunk and shrunk.
  actors.update(DEATH_TICK + 0.98, null, false);
  assert.equal(chassis.visible, true, 'the collapse is visible, not skipped');
  assert.equal(
    chassis.position.x,
    deathPosition.x + 0.5,
    'collapses at its authoritative final column',
  );
  assert.equal(
    chassis.position.z,
    deathPosition.y + 0.5,
    'collapses at its authoritative final row',
  );
  assert.ok(chassis.rotation.z > 0.3, `nosed over (${chassis.rotation.z})`);
  assert.ok(chassis.position.y < -0.05, `settled into the floor (${chassis.position.y})`);
  assert.ok(chassis.scale.x < 0.9, `shrunk (${chassis.scale.x})`);

  actors.dispose();
});

test('legacy damage and destruction flash at their normalized impact tile', () => {
  const impact = replay.ticks[DEATH_TICK].events.find(
    (event) => event.type === 'damage',
  );
  assert.ok(impact?.from);
  assert.equal(
    impact.to,
    null,
    'the replay-v1 fixture keeps its impact tile in from',
  );

  const overlays = buildOverlays(replay);
  overlays.update(DEATH_TICK + 0.1, null, false);
  let flash: THREE.Object3D | null = null;
  overlays.group.traverse((node) => {
    if (
      node.visible &&
      node.userData.cue === 'event-flash' &&
      node.userData.eventType === 'damage'
    ) {
      flash = node;
    }
  });

  assert.ok(flash, 'the 3D renderer emits the legacy impact flare');
  assert.equal(flash.position.x, impact.from.x + 0.5);
  assert.equal(flash.position.z, impact.from.y + 0.5);
  overlays.dispose();
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
      // In the open, too. A wall anywhere around a bot cancels the drift outright — the
      // nose swings diagonally through a corner, so damping only what reaches along an
      // axis left it going through the wall. Most turns in this fixture happen against a
      // wall, so a test that does not ask is testing the suppression by accident.
      const settled = replay.ticks[index].after.actors.find(
        (actor) => actor.actorKey === event.sourceActor?.actorKey,
      );
      if (!settled) continue;
      const walled = [-1, 0, 1].some((dx) =>
        [-1, 0, 1].some((dy) => {
          if (!dx && !dy) return false;
          const row = replay.map.tileRows[settled.position.y + dy];
          return (
            row === undefined ||
            row[settled.position.x + dx] === undefined ||
            row[settled.position.x + dx] === '#'
          );
        }),
      );
      if (walled) continue;
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
  assert.ok(turn >= 0, 'the fixture takes a corner on its own, away from walls');
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

test('3D health pips follow the effective form maximum and remain centred', () => {
  const actors = buildActors(frontline);
  const child = healthPipsOf(
    actors.group,
    'frontline:0:unit:1',
  );
  const prime = healthPipsOf(
    actors.group,
    'frontline:0:unit:0',
  );

  actors.update(2.25, null, false);
  assertCenteredPips(child, 3);
  assertCenteredPips(prime, 3);

  actors.update(9.99, null, false);
  assertCenteredPips(child, 5);
  assertCenteredPips(prime, 3);

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
  assert.ok(
    mobile.position.y > 0.05,
    `the chassis lifts while tipping into its tower form (${mobile.position.y})`,
  );
  assert.equal(anchor.visible, true);
  assert.equal(chassis.userData.formId, 'child-mobile');

  actors.update(9.99, 'frontline:0:unit:1', false);
  assert.equal(mobile.visible, false);
  assert.equal(turret.visible, true);
  // The cue now tracks the *deploy*, not the pending flag. It has to: this fixture's form
  // change completes in the tick it started, so a cue tied to pending state never appeared
  // at all, and one that vanished the moment the form switched never finished its sweep.
  assert.equal(anchor.visible, true, 'still counting the deploy down');
  assert.equal(chassis.userData.formId, 'turret');
  assert.equal(chassis.userData.stationary, true);
  assert.equal(chassis.userData.omnidirectional, true);

  // Gone once the deploy is over, rather than left spinning on a finished turret — and the
  // scan wedge takes over, which is the standing statement that this thing watches every
  // direction. Both were lost once in a merge; neither is visible in a still frame the way
  // geometry is, so they are pinned here.
  const scan = chassis.children.find(
    (child) => child.userData.cue === 'stationary-scan',
  );
  assert.ok(scan, 'the turret has a scan wedge');
  actors.update(11.2, 'frontline:0:unit:1', false);
  assert.equal(anchor.visible, false, 'spent once the turret is out');
  assert.equal(scan.visible, true, 'scanning once emplaced');

  actors.update(2, 'frontline:0:unit:1', false);
  assert.equal(scan.visible, false, 'not scanning while it is still a bot');

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
  const spawnPads: THREE.Object3D[] = [];
  const lifecycleMeshes: THREE.Object3D[] = [];
  overlays.group.traverse((node) => {
    if (typeof node.userData.positionIndex === 'number')
      positionMeshes.push(node);
    if (node.userData.kind === 'frontline-spawn-pad')
      spawnPads.push(node);
    if (typeof node.userData.lifecycleStatus === 'string')
      lifecycleMeshes.push(node);
  });
  assert.equal(positionMeshes.length, 5);
  assert.equal(
    positionMeshes.filter((mesh) => mesh.userData.active).length,
    1,
  );
  assert.equal(spawnPads.length, 2, 'both authored home pads are visible');
  assert.deepEqual(
    spawnPads.map((pad) => pad.userData.teamId).sort(),
    [0, 1],
  );
  assert.ok(
    spawnPads.every(
      (pad) =>
        typeof pad.userData.accent === 'string' &&
        pad.userData.accent.length > 0,
    ),
    'spawn-pad team colour comes from renderer presentation',
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

test('Frontline capture fields derive contested state through the binary capture policy', () => {
  const contestedReplay = structuredClone(frontline) as ReplayModel;
  const definition = contestedReplay.map.frontline;
  assert.ok(definition);
  const active = definition.positions.find(
    (position) => position.positionIndex === 1,
  );
  assert.ok(active);
  // Both active Prime positions at tick zero, expressed as an authored
  // multi-tile objective footprint rather than an invented overlay position.
  active.tiles = [
    { x: 1, y: 4 },
    { x: 13, y: 4 },
  ];

  const overlays = buildOverlays(contestedReplay);
  overlays.update(0.5, null, false);

  const fields: THREE.Object3D[] = [];
  overlays.group.traverse((node) => {
    if (node.userData.kind === 'frontline-capture-field')
      fields.push(node);
  });
  const activeField = fields.find((field) => field.userData.active);
  assert.ok(activeField);
  assert.equal(activeField.userData.positionIndex, 1);
  assert.equal(activeField.userData.state, 'contested');

  overlays.dispose();
});

test('3D capture overlays separate build, erosion, and exact post-advance ratchet ownership', () => {
  const building = captureReplay({
    activePositionIndex: 1,
    tiles: [{ x: 1, y: 4 }],
    claimingTeamId: 0,
    captureProgress: 2,
  });
  const buildOverlaysResult = buildOverlays(building);
  buildOverlaysResult.update(0.5, null, false);
  const buildField = activeCaptureField(buildOverlaysResult.group);
  assert.equal(buildField.userData.state, 'building');
  assert.equal(buildField.userData.progressDirection, 'building');
  assert.equal(buildField.userData.claimantTeamId, 0);
  assert.ok(
    captureMaterial(buildField, 'frontline-capture-progress').opacity >
      0.8,
  );
  assert.ok(
    captureMaterial(buildField, 'frontline-capture-ownership').opacity >
      0,
  );
  buildOverlaysResult.dispose();

  const eroding = captureReplay({
    activePositionIndex: 1,
    tiles: [{ x: 13, y: 4 }],
    claimingTeamId: 0,
    captureProgress: 2,
  });
  const erosionOverlays = buildOverlays(eroding);
  erosionOverlays.update(0.5, null, false);
  const erosionField = activeCaptureField(erosionOverlays.group);
  assert.equal(erosionField.userData.state, 'eroding');
  assert.equal(erosionField.userData.progressDirection, 'eroding');
  assert.equal(erosionField.userData.claimantTeamId, 0);
  assert.equal(erosionField.userData.challengerTeamId, 1);
  assert.ok(
    captureMaterial(erosionField, 'frontline-capture-erosion').opacity >
      0.6,
  );
  erosionOverlays.dispose();

  const held = captureReplay({
    activePositionIndex: 2,
    tiles: [{ x: 9, y: 2 }],
    claimingTeamId: null,
    captureProgress: 0,
    holdOwnerTeamId: 1,
    holdEndsAtTick: 31,
  });
  const holdOverlays = buildOverlays(held);
  holdOverlays.update(0.5, null, false);
  const heldField = activeCaptureField(holdOverlays.group);
  assert.equal(heldField.userData.positionIndex, 2);
  assert.equal(heldField.userData.state, 'holding');
  assert.equal(heldField.userData.holdOwnerTeamId, 1);
  assert.equal(heldField.userData.holdEndsAtTick, 31);
  assert.ok(
    captureMaterial(heldField, 'frontline-capture-ownership').opacity >=
      0.2,
  );
  assert.ok(
    captureMaterial(heldField, 'frontline-capture-hold').opacity > 0.7,
  );
  holdOverlays.dispose();
});

test('the accent pool stays transparent when a bot is followed', () => {
  // The pool is an additively blended `PlaneGeometry` two and a half tiles square with a
  // radial glow for a texture and no alpha test — everything it draws is alpha. Following a
  // bot raises its base opacity to exactly 1, and `fade` used to *assign*
  // `transparent = factor < 1 || base < 1`, which at full strength is `false`. Dropped into
  // the opaque pass, the soft circle of light became a hard square: the rectangular box
  // reported around one unit at a time — the selected one.
  const actors = buildActors(replay);
  const followed: ReplayStableUnitKey = 'duel:0:unit:0';

  const poolOf = (unitKey: ReplayStableUnitKey) => {
    let material: THREE.MeshBasicMaterial | null = null;
    chassisOf(actors.group, unitKey).traverse((node) => {
      if (node.userData.cue !== 'accent-pool') return;
      const mesh = node as THREE.Mesh;
      assert.ok(mesh.material instanceof THREE.MeshBasicMaterial);
      material = mesh.material;
    });
    assert.ok(material, `no accent pool for ${unitKey}`);
    return material as THREE.MeshBasicMaterial;
  };

  // Unfollowed: base is below 1, so even the old rule kept this one honest.
  actors.update(10, null, false);
  assert.equal(poolOf(followed).transparent, true);

  // Followed, unfogged, fully emerged — base 1 and factor 1, the exact corner that broke.
  actors.update(10, followed, false);
  const pool = poolOf(followed);
  assert.equal(
    pool.transparent,
    true,
    'the followed bot\'s pool must stay in the transparent pass',
  );
  assert.ok(pool.opacity > 0.99, 'and it is at full strength while followed');

  // Selecting a different bot must hand the first one back unbroken.
  actors.update(10, 'duel:1:unit:0' as ReplayStableUnitKey, false);
  assert.equal(poolOf(followed).transparent, true);

  actors.dispose();
});

test('capture arcs spin about their own tile, never about the map corner', () => {
  // The erosion and hold arcs are `InstancedMesh`es whose tile translation lives in the
  // instance matrices. Turning the mesh — the obvious way to spin them — swung every arc
  // around tile (0,0) on a radius of however far into the arena its tile sat, at nearly two
  // revolutions a second. That is what "random flying circles all over the map" was: rings
  // of light orbiting the corner of the arena, surfacing wherever the playhead landed.
  const held = captureReplay({
    activePositionIndex: 2,
    tiles: [{ x: 9, y: 2 }],
    claimingTeamId: null,
    captureProgress: 0,
    holdOwnerTeamId: 1,
    holdEndsAtTick: 31,
  });
  const overlays = buildOverlays(held);

  // Several playheads, because the angle is a function of time: at t=0 even a mesh
  // rotation is the identity, so a single early frame proves nothing.
  for (const time of [0, 0.5, 7.5, 31.25, 220]) {
    overlays.update(time, null, false);
    overlays.group.updateMatrixWorld(true);

    const placed: Record<string, THREE.Vector3> = {};
    overlays.group.traverse((node) => {
      const kind = node.userData.kind;
      const mesh = node as THREE.InstancedMesh;
      if (typeof kind !== 'string' || !mesh.isInstancedMesh) return;
      const matrix = new THREE.Matrix4();
      mesh.getMatrixAt(0, matrix);
      placed[kind] = new THREE.Vector3()
        .setFromMatrixPosition(matrix)
        .applyMatrix4(mesh.matrixWorld);
    });

    for (const kind of [
      'frontline-capture-progress',
      'frontline-capture-erosion',
      'frontline-capture-hold',
    ]) {
      const at = placed[kind];
      assert.ok(at, `${kind} is instanced`);
      assert.ok(
        Math.abs(at.x - 9.5) < 1e-6 && Math.abs(at.z - 2.5) < 1e-6,
        `${kind} sits on its tile at t=${time}, not at (${at.x.toFixed(2)}, ${at.z.toFixed(2)})`,
      );
    }
  }

  overlays.dispose();
});

test('the capture arcs still turn — the spin is local, not removed', () => {
  // The fix must not be "delete the rotation". The arcs are meant to counter-rotate; what
  // changed is the centre they turn about, so the instance matrices have to differ between
  // two playheads while their translation stays put.
  const held = captureReplay({
    activePositionIndex: 2,
    tiles: [{ x: 9, y: 2 }],
    claimingTeamId: null,
    captureProgress: 0,
    holdOwnerTeamId: 1,
    holdEndsAtTick: 31,
  });
  const overlays = buildOverlays(held);

  const basisAt = (time: number): number[] => {
    overlays.update(time, null, false);
    let found: number[] | null = null;
    overlays.group.traverse((node) => {
      if (node.userData.kind !== 'frontline-capture-hold') return;
      const matrix = new THREE.Matrix4();
      (node as THREE.InstancedMesh).getMatrixAt(0, matrix);
      found = [...matrix.elements];
    });
    assert.ok(found);
    return found;
  };

  const early = basisAt(1);
  const later = basisAt(2.5);
  assert.notDeepEqual(early, later, 'the arc turns between two playheads');
  // Elements 12/13/14 are the translation column: unchanged while the basis rotates.
  assert.deepEqual(early.slice(12, 15), later.slice(12, 15));

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

function captureReplay({
  activePositionIndex,
  tiles,
  claimingTeamId,
  captureProgress,
  holdOwnerTeamId = null,
  holdEndsAtTick = null,
}: {
  activePositionIndex: number;
  tiles: { x: number; y: number }[];
  claimingTeamId: number | null;
  captureProgress: number;
  holdOwnerTeamId?: number | null;
  holdEndsAtTick?: number | null;
}): ReplayModel {
  const candidate = structuredClone(frontline) as ReplayModel;
  const definition = candidate.map.frontline;
  assert.ok(definition);
  const position = definition.positions.find(
    (entry) => entry.positionIndex === activePositionIndex,
  );
  assert.ok(position);
  position.tiles = tiles;
  const objective = candidate.ticks[0]!.after.objective;
  assert.equal(objective.kind, 'frontline');
  if (objective.kind !== 'frontline') return candidate;
  objective.activePositionIndex = activePositionIndex;
  objective.claimingTeamId = claimingTeamId;
  objective.captureProgress = captureProgress;
  objective.holdOwnerTeamId = holdOwnerTeamId;
  objective.holdEndsAtTick = holdEndsAtTick;
  return candidate;
}

function activeCaptureField(group: THREE.Object3D): THREE.Object3D {
  let active: THREE.Object3D | null = null;
  group.traverse((node) => {
    if (
      node.userData.kind === 'frontline-capture-field' &&
      node.userData.active
    ) {
      active = node;
    }
  });
  assert.ok(active);
  return active;
}

function captureMaterial(
  field: THREE.Object3D,
  kind: string,
): THREE.MeshBasicMaterial {
  const mesh = field.children.find(
    (child) => child.userData.kind === kind,
  ) as THREE.Mesh | undefined;
  assert.ok(mesh);
  assert.ok(mesh.material instanceof THREE.MeshBasicMaterial);
  return mesh.material;
}

/**
 * A striker's volley, as a replay the renderer can be asked about.
 *
 * The Labs replays that carry a real one are twelve megabytes each, and the animation
 * being pinned is a pure function of two events and a form catalog — so the shape is
 * borrowed from the engine-authored Frontline fixture rather than a fixture of its own.
 * Its unit 1 already performs a same-tick form change on tick 9, which is exactly the
 * shape of a volley entry; pointing it at a stance form instead of a turret, and adding
 * the automatic return on the tick the bolts leave, reproduces the move a Labs striker
 * makes. The engine fixture itself is never touched (see `tests/fixtures/README.md`).
 */
const VOLLEY_CASTER: ReplayStableUnitKey = 'frontline:0:unit:1';
const VOLLEY_STANCE_FORM = 'child-mobile-volley-stance';
/** Entered here, and cast on the next tick — the one-tick windup this exists for. */
const VOLLEY_ENTER_TICK = 9;
const VOLLEY_CAST_TICK = 10;

function volleyReplay(): ReplayModel {
  const model = structuredClone(frontline) as ReplayModel;
  const mobile = model.forms.find(
    (form) => form.formId === 'child-mobile',
  )!;
  model.forms.push({
    ...mobile,
    formId: VOLLEY_STANCE_FORM,
    canMove: false,
  });

  let entry: (typeof model.ticks)[number]['events'][number] | null = null;
  for (const tick of model.ticks) {
    for (const event of tick.events) {
      if (
        event.sourceActor?.unitKey !== VOLLEY_CASTER ||
        (event.type !== 'form-transition-started' &&
          event.type !== 'form-changed')
      ) {
        continue;
      }
      event.toFormId = VOLLEY_STANCE_FORM;
      if (event.type === 'form-transition-started') entry = event;
    }
    // The authoritative form follows the same story, so nothing downstream of the
    // animation reads a turret where a stance is being drawn.
    for (const state of [tick.before, tick.after]) {
      for (const actor of state.actors) {
        if (actor.unitKey !== VOLLEY_CASTER) continue;
        if (actor.formId !== 'turret') continue;
        actor.formId =
          tick.tick === VOLLEY_ENTER_TICK
            ? VOLLEY_STANCE_FORM
            : 'child-mobile';
      }
    }
  }
  assert.ok(entry, 'the fixture no longer carries a same-tick form change');

  const cast = model.ticks.find(
    (tick) => tick.tick === VOLLEY_CAST_TICK,
  )!;
  cast.events.push({
    ...structuredClone(entry),
    eventId: 'synthetic:volley-return',
    tick: VOLLEY_CAST_TICK,
    fromFormId: VOLLEY_STANCE_FORM,
    toFormId: 'child-mobile',
    formTransitionStartedAtTick: VOLLEY_CAST_TICK,
    formTransitionCompletesAtTick: VOLLEY_CAST_TICK,
  });
  return model;
}

test('the volley stance is fully out on the tick it fires, and never pops', () => {
  const actors = buildActors(volleyReplay());
  const chassis = chassisOf(actors.group, VOLLEY_CASTER);
  const mobile = formPart(chassis, 'mobile');
  const stance = formPart(chassis, 'stance-directional');
  const anchor = chassis.children.find(
    (child) => child.userData.cue === 'form-transition-pending',
  );
  const pool = chassis.children.find(
    (child) => child.userData.cue === 'accent-pool',
  );
  assert.ok(anchor && pool);
  const hinges = stance.children.filter(
    (child) => child.userData.fanAngle !== undefined,
  );
  assert.equal(hinges.length, 3, 'three launch lanes');

  /** How far the fan has swung, as a share of the heading it fires along. */
  const fanned = () =>
    Math.abs(hinges[0]!.rotation.y) /
    Math.abs(hinges[0]!.userData.fanAngle as number);
  /** The size of whichever body is actually on screen. */
  const shownSize = () =>
    mobile.visible ? mobile.scale.x : stance.scale.x;

  // The reported bug, stated as a number. The entry used to run on Anchor's 1.5-tick
  // fallback, so at the instant the three bolts left the muzzle the fan was 60% open and
  // still moving — the telegraph arrived after the thing it announced.
  actors.update(VOLLEY_CAST_TICK, null, false);
  assert.equal(stance.visible, true, 'the stance body is what fires');
  assert.equal(mobile.visible, false);
  assert.ok(
    fanned() > 0.99,
    `the fan is open when the volley leaves (${(fanned() * 100).toFixed(0)}%)`,
  );
  assert.ok(
    Math.abs(hinges[0]!.rotation.y) <=
      Math.abs(hinges[0]!.userData.fanAngle as number) + 1e-9,
    'and never past the heading the profile actually fires along',
  );

  // No pop. The two bodies used to cross at 0.58 and 0.71 — a fifth of the machine's size,
  // gained in one frame, on top of a model swap. Sampled finely across the whole move,
  // because a discontinuity is exactly what an eye catches and an end-state assertion does
  // not.
  let previous: number | null = null;
  let worst = 0;
  let charge = 0;
  for (let t = VOLLEY_ENTER_TICK; t <= VOLLEY_CAST_TICK + 1; t += 0.02) {
    actors.update(t, null, false);
    if (previous !== null) worst = Math.max(worst, Math.abs(shownSize() - previous));
    previous = shownSize();
    charge = Math.max(charge, pool.scale.x);
    assert.equal(
      anchor.visible,
      false,
      `no windup dial on a one-tick stance (t=${t.toFixed(2)})`,
    );
  }
  assert.ok(worst < 0.02, `the body never jumps size (worst step ${worst.toFixed(3)})`);

  // And the striker does light up: the accent pool flares and spreads while it winds.
  assert.ok(charge > 1.2, `the charge reads (${charge.toFixed(2)}×)`);
  actors.update(VOLLEY_CAST_TICK + 2, null, false);
  assert.ok(pool.scale.x < 1.01, 'and is given back once the move is over');

  actors.dispose();
});

/** The floor ring belonging to one unit, found by its cue like the health pips are. */
function selectionRingOf(
  group: THREE.Object3D,
  unitKey: ReplayStableUnitKey,
): THREE.Object3D {
  let found: THREE.Object3D | null = null;
  group.traverse((node) => {
    if (
      node.userData.cue === 'selection-ring' &&
      node.userData.forUnitKey === unitKey
    ) {
      found = node;
    }
  });
  assert.ok(found, `no selection ring for unit ${unitKey}`);
  return found;
}

/**
 * The ring under the selected body.
 *
 * Three things about it are decisions rather than styling, and all three are invisible in
 * a screenshot of a dark map: it belongs to exactly one bot at a time, it sits outside
 * the chassis silhouette rather than across it, and it is above the fog plane so a body
 * standing at the edge of its own team's vision keeps it.
 */
test('the selected body is ringed on the floor, and only that one', () => {
  const actors = buildActors(replay);
  const [first, second] = replay.units.map((unit) => unit.unitKey);
  assert.ok(first && second && first !== second);

  actors.update(2, null, false);
  assert.equal(
    selectionRingOf(actors.group, first).visible,
    false,
    'nothing is ringed until something is picked',
  );

  actors.update(2, first, false);
  const ring = selectionRingOf(actors.group, first);
  assert.equal(ring.visible, true);
  assert.equal(
    selectionRingOf(actors.group, second).visible,
    false,
    'selection is one bot at a time',
  );

  // Above the fog mask (0.03), which is the whole of "readable in fog": every other floor
  // cue sits under it because the fog is entitled to hide what is happening on that
  // ground, and this one is the viewer's own state rather than the match's.
  assert.ok(ring.position.y > 0.03, 'the ring clears the fog plane');

  // Outside the machine, not drawn across it. The first attempt at a selection ring was
  // removed for reading as painted over the chassis, and the fix is geometric.
  const radii = ring.children.map((child) => {
    const geometry = (child as THREE.Mesh).geometry as THREE.BufferGeometry;
    geometry.computeBoundingSphere();
    return geometry.boundingSphere!.radius;
  });
  assert.ok(radii.length === 2, 'a dark backing under a bright edge, so any theme reads');
  assert.ok(Math.min(...radii) > 0.9, 'and it sits out at the tile boundary');

  actors.update(2, second, false);
  assert.equal(
    selectionRingOf(actors.group, first).visible,
    false,
    'picking another bot gives the ground back',
  );
  assert.equal(selectionRingOf(actors.group, second).visible, true);
});
