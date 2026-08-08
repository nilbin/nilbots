import assert from 'node:assert/strict';
import { createHash } from 'node:crypto';
import { readFileSync } from 'node:fs';
import { join } from 'node:path';
import test from 'node:test';
import { createCanvas } from '@napi-rs/canvas';
import type * as THREE from 'three';
import type {
  ReplayCausalEvent,
  ReplayModel,
} from '../src/replayModel.ts';
import { isArrivalEvent } from '../src/replayModel.ts';
import { loadReplayJson } from '../src/replayIngress.ts';
import {
  arrivalsAt,
  buildOverlays,
  drawArena,
} from './.harness/harness.entry.js';

/**
 * A life arriving, which the viewer previously had no way of saying at all.
 *
 * The fixture fabricates two children on tick 2 — one per side, on each team's protected
 * pad — so every claim here is made against an engine-authored arrival rather than a
 * synthesized one. What is synthesized is the *other* spelling: a generation-3 document
 * says `life-spawned` where this one says `fabricated`, and an effect that fires on one
 * and not the other is exactly the class of bug the model's predicates exist to prevent.
 */

const here = import.meta.dirname;
const frontline = loadReplayJson(
  readFileSync(join(here, 'fixtures', 'frontline-replay-v2.json'), 'utf8'),
).replay as ReplayModel;

/** The tick the fixture fabricates a child on each side. */
const FABRICATED = 2;

const WIDTH = 360;
const HEIGHT = 280;

function frameHash(replay: ReplayModel, time: number): string {
  const canvas = createCanvas(WIDTH, HEIGHT);
  const ctx = canvas.getContext('2d');
  drawArena(
    ctx as unknown as CanvasRenderingContext2D,
    replay,
    { time, selectedUnitKey: null, showVisibility: false },
    WIDTH,
    HEIGHT,
  );
  return createHash('sha256')
    .update(ctx.getImageData(0, 0, WIDTH, HEIGHT).data)
    .digest('hex')
    .slice(0, 16);
}

/** The same replay with its arrivals removed, so a frame can be compared against itself. */
function withoutArrivals(source: ReplayModel): ReplayModel {
  const model = structuredClone(source) as ReplayModel;
  for (const tick of model.ticks)
    tick.lifecycleEvents = tick.lifecycleEvents.filter(
      (event) => !isArrivalEvent(event.type),
    );
  return model;
}

/** The same arrivals, spelled the way a generation-3 document spells them. */
function asGenericSpelling(source: ReplayModel): ReplayModel {
  const model = structuredClone(source) as ReplayModel;
  let renamed = 0;
  for (const tick of model.ticks)
    for (const event of tick.lifecycleEvents)
      if (isArrivalEvent(event.type)) {
        event.type = 'life-spawned';
        event.spawnReason = 'fabrication';
        renamed++;
      }
  assert.ok(renamed > 0, 'the fixture has arrivals worth respelling');
  return model;
}

test('an arrival is derived from the lifecycle event and the state it lands in', () => {
  const arrivals = arrivalsAt(frontline, FABRICATED + 0.2);
  assert.equal(arrivals.length, 2, 'both sides fabricate on this tick');
  for (const arrival of arrivals) {
    assert.equal(arrival.reason, 'fabrication');
    // The pad, taken from the tick's opening state rather than from the event, because
    // that is the field both replay generations agree on.
    const state = frontline.ticks[FABRICATED].before.actors.find(
      (actor) => actor.actorKey === arrival.actorKey,
    );
    assert.ok(state, 'the arriving life is in the tick it arrives on');
    assert.equal(arrival.x, state!.position.x);
    assert.equal(arrival.y, state!.position.y);
  }
  assert.deepEqual(
    arrivals.map((arrival) => arrival.teamId).sort(),
    [0, 1],
    'one per side',
  );

  // It runs at the head of its own tick and nowhere else. A materialization that leaked
  // backwards would appear before the fabrication that paid for it.
  assert.equal(arrivalsAt(frontline, FABRICATED - 0.05).length, 0);
  assert.equal(arrivalsAt(frontline, FABRICATED + 0.9).length, 0);
  assert.ok(arrivalsAt(frontline, FABRICATED).every((a) => a.age === 0));
  assert.ok(
    arrivalsAt(frontline, FABRICATED + 0.5).every((a) => a.age > 0.5),
    'and it is most of the way through by the middle of the tick',
  );
});

test('both spellings of an arrival are the same arrival', () => {
  const generic = asGenericSpelling(frontline);
  assert.deepEqual(
    arrivalsAt(generic, FABRICATED + 0.3).map(({ actorKey, x, y, age }) => ({
      actorKey,
      x,
      y,
      age,
    })),
    arrivalsAt(frontline, FABRICATED + 0.3).map(
      ({ actorKey, x, y, age }) => ({ actorKey, x, y, age }),
    ),
  );
  for (const time of [FABRICATED, FABRICATED + 0.3, FABRICATED + 0.6])
    assert.equal(
      frameHash(generic, time),
      frameHash(frontline, time),
      `a generation-3 arrival draws the same frame at ${time}`,
    );
});

test('an arrival is drawn at the instant it happens, and only then', () => {
  const bare = withoutArrivals(frontline);
  assert.equal(arrivalsAt(bare, FABRICATED + 0.3).length, 0);

  assert.notEqual(
    frameHash(frontline, FABRICATED + 0.3),
    frameHash(bare, FABRICATED + 0.3),
    'the materialization is unmistakably on screen',
  );
  assert.notEqual(
    frameHash(frontline, FABRICATED),
    frameHash(bare, FABRICATED),
    'from the first instant of the tick — a life can act on the tick it arrives on',
  );
  // The body is settled and the ring is gone before the tick ends, so a machine that
  // fires on its creation tick is never drawn half-materialized while it shoots.
  assert.equal(
    frameHash(frontline, FABRICATED + 0.85),
    frameHash(bare, FABRICATED + 0.85),
    'and it is over well inside its own tick',
  );
  // The tick before carries the fabrication order, not the body.
  assert.equal(
    frameHash(frontline, FABRICATED - 0.4),
    frameHash(bare, FABRICATED - 0.4),
  );
});

test('an arrival is not a destruction — it condenses where one throws out', () => {
  // The same tile, the same instant, the same accent resolution: the only difference is
  // which thing happened there. If these ever matched, "a machine was built here" would
  // be rendered as "a machine died here".
  const arrival = arrivalsAt(frontline, FABRICATED + 0.7)[0]!;
  const destroyedInstead = withoutArrivals(frontline);
  const template = destroyedInstead.ticks[0].events[0]!;
  destroyedInstead.ticks[FABRICATED].events.push({
    ...structuredClone(template),
    eventId: 'synthetic:destruction',
    tick: FABRICATED,
    type: 'destroyed',
    from: { x: arrival.x, y: arrival.y },
    to: { x: arrival.x, y: arrival.y },
  } as ReplayCausalEvent);

  assert.notEqual(
    frameHash(frontline, FABRICATED + 0.7),
    frameHash(destroyedInstead, FABRICATED + 0.7),
  );
});

test('the 3D renderer materializes on the same clock, with meshes of its own', () => {
  const overlays = buildOverlays(frontline);
  // Radii, not meshes: the pool hands the same objects back every frame, so holding one
  // and reading it later would compare an instant against itself.
  const ringsAt = (time: number) => {
    overlays.update(time, null, false);
    const radii: number[] = [];
    overlays.group.traverse((node: THREE.Object3D) => {
      if (node.userData.cue === 'arrival' && node.visible) radii.push(node.scale.x);
    });
    return radii;
  };

  assert.equal(ringsAt(FABRICATED - 0.3).length, 0);
  const opening = ringsAt(FABRICATED + 0.05);
  assert.equal(opening.length, 2, 'one closing ring per arriving life');
  const closing = ringsAt(FABRICATED + 0.6);
  assert.equal(closing.length, 2);
  // Inward, which is the whole distinction from every other effect in this renderer.
  assert.ok(
    closing[0] < opening[0],
    `the ring collapses (${opening[0]} → ${closing[0]})`,
  );
  assert.equal(ringsAt(FABRICATED + 0.95).length, 0, 'and it is done inside the tick');
  overlays.dispose();
});
