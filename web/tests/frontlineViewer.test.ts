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
import { loadReplayJson, loadReplayObject } from '../src/replayIngress.ts';
import { participantForActor } from '../src/replayParticipants.ts';
import type { ReplayModel } from '../src/replayModel.ts';
import type { ReplayV3Document } from '../src/replayWireV3.ts';
import { adaptReplayV3ToFrontline } from './support/replayFixtureInputs.ts';

const replay = loadReplayJson(
  readFileSync(
    new URL('./fixtures/frontline-replay-v2.json', import.meta.url),
    'utf8',
  ),
).replay;

test('generic replay-v3 presents exact stable units and actor lives', () => {
  const generic = loadReplayJson(
    readFileSync(
      new URL(
        '../../tests/BotArena.Engine.Tests/Fixtures/generic-replay-v3.json',
        import.meta.url,
      ),
      'utf8',
    ),
  ).replay;
  const presenter = createPresenter(generic);
  const opening = presenter.at(0);

  assert.equal(presenter.tickCount, 2);
  assert.equal(presenter.maxHealth, 3);
  assert.deepEqual(
    opening.units.map((unit) => ({
      unitKey: unit.unitKey,
      actorKey: unit.actorKey,
      participantId: unit.participantId,
      actionId: unit.actionId,
    })),
    [
      {
        unitKey: 'generic:0:unit:0',
        actorKey: 'generic:0:unit:0:life:0',
        participantId: 10,
        actionId: 'shoot',
      },
      {
        unitKey: 'generic:1:unit:0',
        actorKey: 'generic:1:unit:0:life:0',
        participantId: 20,
        actionId: 'shoot',
      },
    ],
  );
});

test('generic Frontline replay-v3 presents contract tuning and the derived breach winner', () => {
  const source = JSON.parse(
    readFileSync(
      new URL(
        '../../tests/BotArena.Engine.Tests/Fixtures/generic-replay-v3.json',
        import.meta.url,
      ),
      'utf8',
    ),
  ) as ReplayV3Document;
  const frontline = loadReplayObject(
    adaptReplayV3ToFrontline(source, 'base-breach'),
  ).replay;
  const final = createPresenter(frontline).at(frontline.ticks.length - 1);

  assert.deepEqual(final.objective, {
    kind: 'frontline',
    activePositionIndex: 2,
    positionCount: 3,
    claimingTeamId: null,
    captureProgress: 0,
    captureThreshold: 3,
    controlResumesAtTick: 0,
    winnerTeamId: 0,
    phase: 'participant-10 BREACHES',
  });
});

test('generic replay-v3 derives form mobility from allowed actions, not ground occupancy', () => {
  const generic = loadReplayJson(
    readFileSync(
      new URL(
        '../../tests/BotArena.Engine.Tests/Fixtures/generic-frontline-replay-v3.json',
        import.meta.url,
      ),
      'utf8',
    ),
  ).replay;
  const form = generic.forms.find((candidate) => candidate.formId === 'mobile');

  assert.ok(form);
  assert.equal(form.movementLayer, 'ground');
  assert.equal(form.canMove, false);
  assert.equal(form.canShoot, true);
  if (generic.contract.kind !== 'v3-generic') {
    assert.fail('expected a generic replay-v3 contract');
  }
  const contractForm = generic.contract.rules.forms.find(
    (candidate) => candidate.id === 'mobile',
  );
  assert.equal(contractForm?.canMove, false);
  assert.equal(contractForm?.canShoot, true);
});

test('actor-life interpolation adds fabricated lives without morphing primes', () => {
  assert.deepEqual(
    posesAt(replay, 0.5).map((pose) => pose.actorKey),
    [
      'frontline:0:unit:0:life:0',
      'frontline:1:unit:0:life:0',
    ],
  );
  assert.deepEqual(
    posesAt(replay, 1.5).map((pose) => pose.actorKey),
    [
      'frontline:0:unit:0:life:0',
      'frontline:1:unit:0:life:0',
    ],
  );
  assert.deepEqual(
    posesAt(replay, 2.25).map((pose) => pose.actorKey),
    [
      'frontline:0:unit:0:life:0',
      'frontline:0:unit:1:life:0',
      'frontline:1:unit:0:life:0',
      'frontline:1:unit:1:life:0',
    ],
  );
});

test('Frontline presentation follows stable units through fabrication and anchoring', () => {
  const presenter = createPresenter(replay);
  const opening = presenter.at(0);
  const queued = presenter.at(1);
  const fabricated = presenter.at(2);
  const anchored = presenter.at(9);

  assert.equal(opening.objective?.kind, 'frontline');
  assert.equal(
    opening.objective?.kind === 'frontline'
      ? opening.objective.captureThreshold
      : null,
    3,
  );
  assert.deepEqual(
    opening.units.map((unit) => unit.unitKey),
    queued.units.map((unit) => unit.unitKey),
  );
  assert.deepEqual(
    queued.units.map((unit) => unit.status),
    [
      'active',
      'fabrication-queued',
      'locked',
      'active',
      'fabrication-queued',
      'locked',
    ],
  );
  assert.equal(
    fabricated.units.find(
      (unit) => unit.teamId === 0 && unit.unitId === 1,
    )?.actorKey,
    'frontline:0:unit:1:life:0',
  );
  assert.equal(
    anchored.units.find(
      (unit) => unit.teamId === 0 && unit.unitId === 1,
    )?.formId,
    'turret',
  );

  const retainedDestroyedActor = structuredClone(replay);
  const actor = retainedDestroyedActor.ticks[2]!.after.actors[0]!;
  actor.status = 'destroyed';
  actor.health = 4;
  actor.cooldown = 7;
  actor.energy = 9;
  retainedDestroyedActor.ticks[2]!.after.units[0]!.lifecycleStatus =
    'rebuilding';
  const destroyed = createPresenter(retainedDestroyedActor).at(2).units[0]!;
  assert.equal(destroyed.actorKey, null);
  assert.equal(destroyed.lifeId, null);
  assert.equal(destroyed.health, 0);
  assert.equal(destroyed.cooldown, 0);
  assert.equal(destroyed.energy, null);
});

test('Frontline, stationary 360 forms, and attributed projectiles render', () => {
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
  const traversal = projectileReplay.ticks[10]!.projectileTraversals[0]!;
  projectileReplay.ticks[8]!.after.projectiles = [
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
    frameHash(projectileReplay, 8.25),
    frameHash(replay, 8.25),
  );
});

test('same-tick anchoring telegraphs before the body becomes a turret', () => {
  const anchored = structuredClone(replay);
  const tick = anchored.ticks[2]!;
  const before = tick.before.actors[0]!;
  const after = tick.after.actors.find(
    (candidate) => candidate.actorKey === before.actorKey,
  )!;
  before.formId = 'child-mobile';
  before.pendingFormTransition = null;
  after.formId = 'turret';
  after.pendingFormTransition = null;

  const template = anchored.ticks[0]!.events[0]!;
  tick.events = [
    {
      ...template,
      eventId: 'resolution:2:0',
      tick: 2,
      ordinal: 0,
      type: 'form-transition-started',
      teamId: before.identity.teamId,
      unitId: before.identity.unitId,
      sourceActor: before.identity,
      targetActor: null,
      projectileId: null,
      from: { ...before.position },
      to: { ...before.position },
      fromFacing: before.facing,
      toFacing: before.facing,
      projectileHeading: null,
      fromFormId: 'child-mobile',
      toFormId: 'turret',
      formTransitionStartedAtTick: 2,
      formTransitionCompletesAtTick: 2,
      actionPayload: {
        shotProgram: null,
        direction: null,
        launchHeading: null,
        unitKey: null,
        formTargetId: 'turret',
      },
      actionId: 'transform',
      actionCode: 101,
      actionResult: 'success',
      newHealth: after.health,
    },
  ];

  const windingUp = posesAt(anchored, 2.25)[0]!;
  assert.equal(windingUp.formId, 'child-mobile');
  assert.deepEqual(windingUp.pendingFormTransition, {
    fromFormId: 'child-mobile',
    toFormId: 'turret',
    startedAtTick: 2,
    completesAtTick: 2,
  });

  const transformed = posesAt(anchored, 2.99)[0]!;
  assert.equal(transformed.formId, 'turret');
  assert.equal(transformed.pendingFormTransition, null);

  const withoutTelegraph = structuredClone(anchored);
  withoutTelegraph.ticks[2]!.events = [];
  assert.notEqual(
    frameHash(anchored, 2.25),
    frameHash(withoutTelegraph, 2.25),
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
