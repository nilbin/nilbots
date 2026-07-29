import assert from 'node:assert/strict';
import { createHash } from 'node:crypto';
import { readFileSync } from 'node:fs';
import { join } from 'node:path';
import test from 'node:test';
import { createCanvas } from '@napi-rs/canvas';
import * as THREE from 'three';
import type {
  ReplayModel,
  ReplayStableUnitKey,
} from '../src/replayModel.ts';
import { loadReplayJson } from '../src/replayIngress.ts';
import {
  buildActors,
  classFamilyForForm,
  drawArena,
  fallbackLookIdForForm,
  fallbackProjectileLookIdForForm,
  posesAt,
  unitAccent,
  unitEmplacedLook,
  unitLook,
  unitProjectileLook,
} from './.harness/harness.entry.js';

/**
 * How a form reaches the screen: which artwork it wears, which colour, and — the part that
 * shipped broken — whether the renderers keep following it after it changes.
 *
 * Generation-3 class rulesets are what these exist for. Their replays carry no
 * presentation section, both participants arrive with the same default look and the same
 * accent, and a life may transition between a mobile form and an emplaced one *in both
 * directions*, more than once. Every assumption in the two renderers that a form only ever
 * changes once, or that presentation comes from the participant alone, fails on that.
 */

const here = import.meta.dirname;
const frontline = loadReplayJson(
  readFileSync(join(here, 'fixtures', 'frontline-replay-v2.json'), 'utf8'),
).replay;
const duel = loadReplayJson(
  readFileSync(join(here, 'fixtures', 'golden-replay.json'), 'utf8'),
).replay;

/** The life this fixture anchors at tick 9, and the slot it belongs to. */
const ANCHORING_ACTOR = 'frontline:0:unit:1:life:0';
const ANCHORING_UNIT: ReplayStableUnitKey = 'frontline:0:unit:1';

/**
 * The fixture, continued: the anchored life mobilizes back out of its turret at tick 10.
 *
 * Built by rewriting the normalized model rather than shipping a second replay, because
 * the real article is a 25 MB document and the only part that matters here is the shape
 * the renderers read — a `form-transition-started` event pointing at a weighted mobile
 * form, plus the authoritative form on every later snapshot agreeing with it.
 */
function withMobilize(source: ReplayModel): ReplayModel {
  const model = structuredClone(source) as ReplayModel;
  const anchorEvent = model.ticks[9].events.find(
    (event) =>
      event.type === 'form-transition-started' &&
      event.sourceActor?.actorKey === ANCHORING_ACTOR,
  );
  assert.ok(anchorEvent, 'the fixture anchors this life at tick 9');

  model.ticks[10].events.push({
    ...structuredClone(anchorEvent),
    tick: 10,
    fromFormId: 'turret',
    toFormId: 'child-mobile',
    formTransitionStartedAtTick: 10,
    formTransitionCompletesAtTick: 10,
  });
  for (const [index, tick] of model.ticks.entries()) {
    for (const actor of index > 10 ? tick.before.actors : [])
      if (actor.actorKey === ANCHORING_ACTOR) actor.formId = 'child-mobile';
    for (const actor of index >= 10 ? tick.after.actors : [])
      if (actor.actorKey === ANCHORING_ACTOR) actor.formId = 'child-mobile';
  }
  return model;
}

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

function partOf(
  chassis: THREE.Object3D,
  form: 'mobile' | 'stationary-omnidirectional',
): THREE.Object3D {
  const part = chassis.children.find(
    (child) => child.userData.renderForm === form,
  );
  assert.ok(part, `no ${form} form on chassis`);
  return part;
}

function frameHash(replay: ReplayModel, time: number): string {
  const canvas = createCanvas(320, 240);
  const ctx = canvas.getContext('2d');
  drawArena(
    ctx as unknown as CanvasRenderingContext2D,
    replay,
    { time, selectedUnitKey: null, showVisibility: false },
    320,
    240,
  );
  return createHash('sha256')
    .update(ctx.getImageData(0, 0, 320, 240).data)
    .digest('hex')
    .slice(0, 16);
}

/**
 * A class-shaped replay, from the frontline fixture's bones.
 *
 * Only the names change — team 0 becomes a bulwark line and team 1 a striker line, with
 * the emplaced form renamed to the family's turret. That is exactly what the generic
 * contract hands the viewer: a form catalog whose IDs are the only thing separating two
 * classes, and a header carrying no presentation at all.
 */
function asClassArms(source: ReplayModel): ReplayModel {
  const model = structuredClone(source) as ReplayModel;
  const rename = (teamId: number, formId: string) => {
    const family = teamId === 0 ? 'bulwark' : 'striker';
    if (formId === 'turret') return `${family}-child-turret`;
    return `${family}-${formId.replace('-mobile', '')}`;
  };
  model.forms = [
    ...['prime-mobile', 'child-mobile', 'turret'].flatMap((formId) =>
      [0, 1].map((teamId) => ({
        ...model.forms.find((form) => form.formId === formId)!,
        formId: rename(teamId, formId),
      })),
    ),
  ];
  const teamOf = (unitKey: string) => (unitKey.includes(':0:') ? 0 : 1);
  for (const unit of model.units)
    unit.initialFormId =
      unit.initialFormId === null
        ? null
        : rename(unit.teamId, unit.initialFormId);
  for (const snapshot of [
    model.initialWorld,
    ...model.ticks.flatMap((tick) => [tick.before, tick.after]),
  ]) {
    if (!snapshot) continue;
    for (const unit of snapshot.units) {
      unit.defaultFormId = rename(unit.teamId, unit.defaultFormId);
      unit.formId = rename(unit.teamId, unit.formId);
    }
    for (const actor of snapshot.actors)
      actor.formId = rename(teamOf(actor.unitKey), actor.formId);
  }
  // Both bots submitted the same cosmetic, which is the case the class arms actually
  // produce and the reason the teams were indistinguishable.
  for (const participant of model.participants) {
    participant.accent = '#22d3ee';
    participant.lookId = 'vanguard';
  }
  return model;
}

test('the flat renderer follows the effective form in both directions', () => {
  const mobilizing = withMobilize(frontline);
  const formAt = (replay: ReplayModel, time: number) =>
    posesAt(replay, time).find(
      (pose) => pose.actorKey === ANCHORING_ACTOR,
    )?.formId;

  // `posesAt` is the one form source both renderers read, so this is the flat renderer's
  // emplacement ring, its hover, its vision halo and its chassis all at once.
  assert.equal(formAt(mobilizing, 8.5), 'child-mobile');
  assert.equal(formAt(mobilizing, 9.95), 'turret');
  assert.equal(formAt(mobilizing, 10.5), 'turret', 'still emplaced mid-windup');
  assert.equal(formAt(mobilizing, 11.5), 'child-mobile', 'mobile again');

  // And the picture actually changes: an emplaced frame and a mobile frame of the same
  // replay are not the same pixels.
  assert.notEqual(
    frameHash(mobilizing, 10.2),
    frameHash(mobilizing, 11.5),
    'the flat renderer redraws the life that mobilized',
  );
});

test('the 2.5D renderer returns to a mobile body after a mobilize', () => {
  const actors = buildActors(withMobilize(frontline));
  const chassis = chassisOf(actors.group, ANCHORING_UNIT);
  const mobile = partOf(chassis, 'mobile');
  const turret = partOf(chassis, 'stationary-omnidirectional');

  actors.update(9.99, null, false);
  assert.equal(turret.visible, true, 'anchored');
  assert.equal(mobile.visible, false);

  // The regression: the deploy clock was a single first-only entry per life, so once a
  // life had anchored it stayed clamped at fully deployed for the rest of the match. The
  // form, the health maximum and the move events all said mobile; the body said turret.
  actors.update(11.6, null, false);
  assert.equal(chassis.userData.formId, 'child-mobile');
  assert.equal(chassis.userData.stationary, false);
  assert.equal(mobile.visible, true, 'the mobile body comes back');
  assert.equal(turret.visible, false, 'and the turret goes away');

  const scan = chassis.children.find(
    (child) => child.userData.cue === 'stationary-scan',
  );
  assert.equal(scan?.visible, false, 'a mobile bot is not scanning');

  actors.dispose();
});

test('an unanchored life is never drawn deployed, and an anchored one still is', () => {
  const actors = buildActors(frontline);
  const never = chassisOf(actors.group, 'frontline:0:unit:0');
  const anchors = chassisOf(actors.group, ANCHORING_UNIT);

  actors.update(11.5, null, false);
  assert.equal(partOf(never, 'mobile').visible, true);
  assert.equal(partOf(never, 'stationary-omnidirectional').visible, false);
  assert.equal(partOf(anchors, 'mobile').visible, false);
  assert.equal(partOf(anchors, 'stationary-omnidirectional').visible, true);

  actors.dispose();
});

test('the 2.5D mobile body carries a facing marker and an emplaced one does not', () => {
  const actors = buildActors(frontline);
  const chassis = chassisOf(actors.group, ANCHORING_UNIT);
  const mobile = partOf(chassis, 'mobile');
  const nose = mobile.children.find(
    (child) => child.userData.cue === 'facing-marker',
  );
  assert.ok(nose, 'the mobile body states which way it points');
  assert.ok(
    nose.position.x > 0,
    `the marker rides the leading edge (${nose.position.x})`,
  );

  actors.update(2, null, false);
  assert.equal(mobile.visible, true, 'facing is shown while it has one');
  actors.update(11.5, null, false);
  assert.equal(
    mobile.visible,
    false,
    'an emplacement that fires in every direction shows no facing',
  );

  actors.dispose();
});

test('class form IDs resolve to the selected class-owned looks and projectiles', () => {
  assert.equal(classFamilyForForm('striker-prime'), 'striker');
  assert.equal(classFamilyForForm('bulwark-child-turret'), 'bulwark');
  assert.equal(classFamilyForForm('fabricator-prime'), 'fabricator');

  assert.deepEqual(
    ['striker', 'bulwark', 'fabricator'].map((family) => [
      fallbackLookIdForForm(`${family}-prime`),
      fallbackProjectileLookIdForForm(`${family}-prime`),
    ]),
    [
      ['trident-wasp', 'trident-spark'],
      ['aegis-tortoise', 'rebound-diamond'],
      ['lattice-loom', 'lattice-rivet'],
    ],
  );
  assert.equal(
    fallbackLookIdForForm('bulwark-child-turret'),
    'aegis-tortoise-turret',
  );

  // Legacy and Frontline form IDs are not class forms and must keep their pixels.
  for (const formId of ['legacy-mobile', 'prime-mobile', 'child-mobile', 'turret'])
    assert.equal(fallbackLookIdForForm(formId), null, formId);
});

test('a class replay renders its classes and its teams apart', () => {
  const classes = asClassArms(frontline);
  const bulwark = unitLook(classes, 'frontline:0:unit:1', 'bulwark-child');
  const striker = unitLook(classes, 'frontline:1:unit:1', 'striker-child');
  assert.notEqual(
    bulwark.id,
    striker.id,
    'two classes wearing one default look was the reported bug',
  );

  const emplaced = unitEmplacedLook(
    classes,
    'frontline:0:unit:1',
    'bulwark-child',
  );
  assert.ok(emplaced, 'a class family knows what its emplacement looks like');
  assert.notEqual(
    emplaced.id,
    bulwark.id,
    'anchoring changes the silhouette, not just a ring around it',
  );
  assert.equal(
    unitLook(classes, 'frontline:0:unit:1', 'bulwark-child-turret').id,
    emplaced?.id,
    'the emplaced form resolves to the emplaced look from either direction',
  );

  assert.notEqual(
    unitAccent(classes, 'frontline:0:unit:1'),
    unitAccent(classes, 'frontline:1:unit:1'),
    'two teams that submitted one accent still read as two teams',
  );
  assert.equal(
    unitAccent(classes, 'frontline:0:unit:1'),
    unitAccent(classes, 'frontline:0:unit:0'),
    'and one team reads as one team',
  );
});

test('authored presentation and distinct participant accents are left alone', () => {
  // A duel where each bot chose its own look and colour: nothing here may be overridden.
  assert.equal(unitLook(duel, 'duel:0:unit:0').id, 'vanguard');
  assert.equal(unitLook(duel, 'duel:1:unit:0').id, 'orbiter');
  assert.equal(unitAccent(duel, 'duel:0:unit:0'), '#f97316');
  assert.equal(unitAccent(duel, 'duel:1:unit:0'), '#a78bfa');

  // A replay that *does* author per-form artwork outranks the fallback table.
  const authored = structuredClone(asClassArms(frontline)) as ReplayModel;
  for (const form of authored.forms)
    if (form.formId === 'bulwark-child') form.lookId = 'rift-runner';
  assert.equal(
    unitLook(authored, 'frontline:0:unit:1', 'bulwark-child').id,
    'rift-runner',
  );

  const form = authored.forms.find(
    (candidate) => candidate.formId === 'bulwark-child',
  );
  assert.ok(form);
  form.projectileLookId = 'ion-orb';
  assert.equal(
    unitProjectileLook(
      authored,
      'frontline:0:unit:1',
      'bulwark-child',
    ).id,
    'ion-orb',
    'an authored per-form projectile outranks the class default',
  );
  assert.equal(
    unitProjectileLook(duel, 'duel:0:unit:0').id,
    'pulse-bolt',
    'a non-class replay keeps the participant projectile',
  );
});
