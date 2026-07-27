import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { join } from 'node:path';
import test from 'node:test';
import * as THREE from 'three';
import { loadReplayJson } from '../src/replayIngress.ts';
import type { ReplayStableUnitKey } from '../src/replayModel.ts';
import { buildActors, buildOverlays } from './.harness/harness.entry.js';

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
  readFileSync(join(import.meta.dirname, 'fixtures', 'golden-replay.json'), 'utf8'),
).replay;

/** The tick this fixture's loser dies on. */
const DEATH_TICK = 96;

/** Chassis are found by the pick pad they carry, which is the only stable handle. */
function chassisOf(group: THREE.Object3D, unitKey: ReplayStableUnitKey): THREE.Object3D {
  let found: THREE.Object3D | null = null;
  group.traverse((node) => {
    if (node.userData.unitKey === unitKey && node.parent) found = node.parent;
  });
  assert.ok(found, `no chassis for ${unitKey}`);
  return found;
}

/** The unit an event's actor belongs to, since events name a life rather than a slot. */
function unitOf(actor: { unitKey: ReplayStableUnitKey } | null) {
  assert.ok(actor, 'event names an actor');
  return actor.unitKey;
}

test('a destroyed bot collapses across the tick it dies in', () => {
  const actors = buildActors(replay);
  const destroyed = replay.ticks[DEATH_TICK].events.find(
    (event) => event.type === 'destroyed',
  );
  assert.ok(destroyed, 'the fixture has a destruction on the tick it claims');
  // The victim, not the killer: a destruction names the loser in `targetActor`.
  const chassis = chassisOf(actors.group, unitOf(destroyed.targetActor));

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
  const shooter = replay.ticks[DEATH_TICK].events.find((event) => event.type === 'shot')!;
  const chassis = chassisOf(actors.group, unitOf(shooter.sourceActor));
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

test('a bot drifts through a corner in the open, and not beside a wall', () => {
  const actors = buildActors(replay);
  const solid = (x: number, y: number) => {
    const row = replay.map.tileRows[y];
    return row === undefined || row[x] === undefined || row[x] === '#';
  };
  const boxedIn = (x: number, y: number) =>
    [-1, 0, 1].some((dx) => [-1, 0, 1].some((dy) => (dx || dy) && solid(x + dx, y + dy)));

  // Every turn in the fixture, split by whether the bot had room to throw its weight about.
  const turns: { tick: number; unitKey: ReplayStableUnitKey; open: boolean }[] = [];
  for (const [index, tick] of replay.ticks.entries())
    for (const event of tick.events) {
      if (event.type !== 'turn' || !event.sourceActor) continue;
      const actor = tick.after.actors.find(
        (candidate) => candidate.actorKey === event.sourceActor!.actorKey,
      );
      if (!actor) continue;
      turns.push({
        tick: index,
        unitKey: event.sourceActor.unitKey,
        open: !boxedIn(actor.position.x, actor.position.y),
      });
    }

  const leanAt = (unitKey: ReplayStableUnitKey, at: number) => {
    actors.update(at, null, false);
    const body = chassisOf(actors.group, unitKey).children.find((c) => c.type === 'Group')!;
    return { lean: Math.abs(body.rotation.x), slide: Math.abs(body.position.z) };
  };

  // In the open it banks hard, a little after the rotation finishes.
  const open = turns.find((turn) => turn.open);
  assert.ok(open, 'the fixture takes at least one corner away from a wall');
  const drifting = leanAt(open.unitKey, open.tick + 0.75);
  assert.ok(drifting.lean > 0.25, `banked into the open corner (${drifting.lean})`);
  assert.ok(drifting.slide > 0.15, `back end stepped out (${drifting.slide})`);

  // Beside a wall it does not, at all. Damping the parts that reached towards the wall was
  // tried twice and leaked both times — the nose swings diagonally, into a tile neither
  // check was looking at — so a bot with a wall anywhere around it simply does not drift.
  const walled = turns.find((turn) => !turn.open);
  assert.ok(walled, 'the fixture also turns beside a wall');
  const held = leanAt(walled.unitKey, walled.tick + 0.75);
  // Only the idle sway is left, which is an order of magnitude smaller than a drift.
  assert.ok(held.lean < 0.1, `no bank against the wall (${held.lean})`);
  assert.ok(held.slide < 0.1, `no slide into the wall (${held.slide})`);

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
      const actorKey = event.sourceActor.actorKey;
      const again = replay.ticks[tick + 1].events.some(
        (next) => next.type === 'move' && next.sourceActor?.actorKey === actorKey,
      );
      if (again) {
        run = tick;
        mover = event.sourceActor.unitKey;
      }
    }
    if (run >= 0) break;
  }
  assert.ok(run >= 0 && mover, 'the fixture has a bot moving two ticks running');

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

test('a hit throws a shockwave, and only at the instant it lands', () => {
  const overlays = buildOverlays(replay);
  const damage = replay.ticks.findIndex((tick) =>
    tick.events.some((event) => event.type === 'damage'),
  );
  assert.ok(damage >= 0, 'the fixture lands a hit somewhere');

  // By name, not by geometry: a bolt dissipating is also a ring on the floor, and counting
  // those as impacts is how this test first "passed" a moment that had none.
  const showing = () => {
    let visible = 0;
    overlays.group.traverse((node) => {
      if (node.visible && node.userData.kind === 'impact') visible += 1;
    });
    return visible;
  };

  // Impacts land late in the tick, on the same 0.6 the flash and the camera knock use.
  overlays.update(damage + 0.2, null, false);
  assert.equal(showing(), 0, 'nothing before the bolt arrives');

  overlays.update(damage + 0.75, null, false);
  assert.ok(showing() > 0, 'a wave at the moment of contact');

  // And gone by the next tick rather than left ringing.
  overlays.update(damage + 1.4, null, false);
  assert.equal(showing(), 0, 'spent by the following tick');

  overlays.dispose();
});

test('deploying into an emplacement never runs backwards', () => {
  // The bug this pins: the tip was driven by the tick's own pending transition while one
  // existed and by a fixed span from the change event once it did not. The two disagreed
  // about the length and handed over mid-animation, so the deploy ran two thirds of the
  // way, jumped back, and ran again.
  const frontline = loadReplayJson(
    readFileSync(join(import.meta.dirname, 'fixtures', 'frontline-replay-v2.json'), 'utf8'),
  ).replay;
  const emplacing = frontline.ticks
    .flatMap((tick) => tick.events)
    .find(
      (event) =>
        event.type === 'form-changed' &&
        frontline.forms.find((form) => form.formId === event.toFormId)?.canMove === false,
    );
  assert.ok(emplacing?.sourceActor, 'the fixture deploys something into an emplacement');

  const actors = buildActors(frontline);
  const chassis = chassisOf(actors.group, emplacing.sourceActor.unitKey);
  const body = chassis.children.find((child) => child.type === 'Group')!;
  const tilt = body.children[0].children[0];

  let previous = -Infinity;
  for (let t = emplacing.tick - 1; t <= emplacing.tick + 3; t += 0.1) {
    actors.update(t, null, false);
    const angle = tilt.rotation.z;
    assert.ok(
      angle + 1e-6 >= previous,
      `deploy reversed at t=${t.toFixed(2)}: ${previous.toFixed(3)} -> ${angle.toFixed(3)}`,
    );
    previous = angle;
  }
  // And it actually got somewhere, rather than passing by never moving at all.
  assert.ok(previous > 1.5, `finished upright (${previous.toFixed(2)} rad)`);

  actors.dispose();
});
