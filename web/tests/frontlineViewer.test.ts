import assert from 'node:assert/strict';
import { createHash } from 'node:crypto';
import { readFileSync } from 'node:fs';
import test from 'node:test';
import { createCanvas } from '@napi-rs/canvas';
import {
  createPresenter,
  drawArena,
  posesAt,
} from './.harness/harness.entry.js';
import { loadReplayJson } from '../src/replayIngress.ts';
import { participantForActor } from '../src/replayParticipants.ts';
import type { ReplayModel } from '../src/replayModel.ts';

const replay = loadReplayJson(
  readFileSync(
    new URL('./fixtures/frontline-replay-v2.json', import.meta.url),
    'utf8',
  ),
).replay;

test('actor-life interpolation never morphs a destroyed life into its respawn', () => {
  assert.deepEqual(
    posesAt(replay, 0.5).map((pose) => pose.actorKey),
    [
      'frontline:0:unit:0:life:0',
      'frontline:1:unit:0:life:0',
    ],
  );
  assert.deepEqual(posesAt(replay, 1.5), []);
  assert.deepEqual(
    posesAt(replay, 2.25).map((pose) => pose.actorKey),
    [
      'frontline:0:unit:0:life:1',
      'frontline:1:unit:0:life:1',
    ],
  );
});

test('Frontline presentation follows stable units across the respawn gap', () => {
  const presenter = createPresenter(replay);
  const opening = presenter.at(0);
  const gap = presenter.at(1);
  const returned = presenter.at(2);

  assert.equal(opening.objective?.kind, 'frontline');
  assert.equal(
    opening.objective?.kind === 'frontline'
      ? opening.objective.captureThreshold
      : null,
    3,
  );
  assert.deepEqual(
    opening.units.map((unit) => unit.unitKey),
    gap.units.map((unit) => unit.unitKey),
  );
  assert.deepEqual(
    gap.units.map((unit) => unit.status),
    ['respawning', 'respawning'],
  );
  assert.deepEqual(
    returned.units.map((unit) => unit.actorKey),
    [
      'frontline:0:unit:0:life:1',
      'frontline:1:unit:0:life:1',
    ],
  );

  const retainedDestroyedActor = structuredClone(replay);
  const actor = retainedDestroyedActor.ticks[2]!.after.actors[0]!;
  actor.status = 'destroyed';
  retainedDestroyedActor.ticks[2]!.after.units[0]!.lifecycleStatus =
    'destroyed';
  const destroyed = createPresenter(retainedDestroyedActor).at(2).units[0]!;
  assert.equal(destroyed.actorKey, null);
  assert.equal(destroyed.lifeId, null);
});

test('Frontline, stationary 360 forms, and old-life projectiles render', () => {
  const base = frameHash(replay, 2.25);
  const blank = createHash('sha256')
    .update(createCanvas(640, 480).toBuffer('image/png'))
    .digest('hex');
  assert.notEqual(base, blank);

  const turretReplay = structuredClone(replay);
  for (const world of [
    turretReplay.ticks[2]!.before,
    turretReplay.ticks[2]!.after,
  ]) {
    for (const unit of world.units) {
      unit.formId = 'turret';
      if (unit.activeActorKey) {
        const actor = world.actors.find(
          (candidate) => candidate.actorKey === unit.activeActorKey,
        );
        if (actor) actor.formId = 'turret';
      }
    }
    for (const actor of world.actors) actor.formId = 'turret';
  }
  assert.notEqual(frameHash(turretReplay, 2.25), base);

  const projectileReplay = structuredClone(replay);
  const traversal = projectileReplay.ticks[0]!.projectileTraversals[0]!;
  projectileReplay.ticks[1]!.after.projectiles = [
    {
      projectileId: 'old-life-projectile',
      ownerActor: traversal.ownerActor,
      ownerActorKey: traversal.ownerActorKey,
      position: { x: 4, y: 2 },
      launchDirection: traversal.launchDirection,
      heading: traversal.heading,
      shotProgram: traversal.shotProgram,
      programmedPath: traversal.programmedPath,
      ticksUntilAdvance: 1,
      remainingTiles: 2,
      tilesPerAdvance: 1,
      nextProgrammedPathIndex: null,
      tilesTraveled: null,
      phase: null,
    },
  ];
  assert.equal(
    participantForActor(projectileReplay, traversal.ownerActor)?.name,
    'Fixture Zero',
  );
  assert.notEqual(
    frameHash(projectileReplay, 1.25),
    frameHash(replay, 1.25),
  );
});

function frameHash(source: ReplayModel, time: number): string {
  const canvas = createCanvas(640, 480);
  const context = canvas.getContext('2d');
  drawArena(
    context as unknown as CanvasRenderingContext2D,
    source,
    { time, selectedUnitKey: null, showVisibility: false },
    640,
    480,
  );
  return createHash('sha256')
    .update(canvas.toBuffer('image/png'))
    .digest('hex');
}
