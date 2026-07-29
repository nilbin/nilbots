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
  ReplayProjectileHeading,
  ReplayStableUnitKey,
} from '../src/replayModel.ts';
import { loadReplayJson } from '../src/replayIngress.ts';
import {
  buildActors,
  drawArena,
  fallbackLookIdForForm,
  stanceFormForUnit,
  stanceKindForForm,
  unitEmplacedLook,
  unitLook,
  unitStanceLook,
  volleyArrowOutline,
  volleyLanes,
  volleysAt,
} from './.harness/harness.entry.js';

/**
 * The three prototype class skills, as the viewer has to state them.
 *
 * A skill that the arena cannot say out loud is a skill an outcome-blind reviewer scores
 * as "nothing happened". These pin the three claims the presentation makes:
 *
 * - a stance is a **third body**, distinct from both the mobile chassis and the
 *   omnidirectional emplacement, and it keeps a facing because both stances are about one;
 * - a volley is **one glyph**, recovered from same-owner / same-tick / contiguous-ID
 *   projectiles and cut when a member terminates;
 * - a deflection is **not a hit on the guard**: it turns the bolt back.
 */

const here = import.meta.dirname;
const frontline = loadReplayJson(
  readFileSync(join(here, 'fixtures', 'frontline-replay-v2.json'), 'utf8'),
).replay;

const SHOOTER = 'frontline:0:unit:1:life:0';
const SHOOTER_UNIT: ReplayStableUnitKey = 'frontline:0:unit:1';
/** The tick the fixture already fires on, and the one after it. */
const LAUNCH = 10;

/**
 * The fixture rewritten as a striker arm that owns the volley stance.
 *
 * Same bones as `formPresentation.test.ts` uses: only the form IDs change, because the
 * form catalog is the entirety of what a generation-3 replay gives the viewer to tell one
 * class or one body from another.
 */
function asVolleyArm(source: ReplayModel): ReplayModel {
  const model = structuredClone(source) as ReplayModel;
  const rename = (formId: string) =>
    formId === 'turret'
      ? 'striker-child-turret'
      : `striker-${formId.replace('-mobile', '')}`;
  model.forms = model.forms.map((form) => ({
    ...form,
    formId: rename(form.formId),
  }));
  // The stance: immobile like the turret, and unlike the turret it keeps its objective
  // weight and its facing. Derived from the mobile form's entry so nothing else drifts.
  const mobile = model.forms.find(
    (form) => form.formId === 'striker-child',
  )!;
  model.forms.push({
    ...mobile,
    formId: 'striker-child-volley-stance',
    canMove: false,
  });
  for (const unit of model.units)
    unit.initialFormId =
      unit.initialFormId === null ? null : rename(unit.initialFormId);
  // Deduplicated: the normalizer shares one snapshot object between `initialWorld` and
  // the first tick's opening state, and renaming through it twice yields
  // `striker-striker-child` — a form nothing resolves and no assertion would name.
  for (const snapshot of new Set([
    model.initialWorld,
    ...model.ticks.flatMap((tick) => [tick.before, tick.after]),
  ])) {
    if (!snapshot) continue;
    for (const unit of snapshot.units) {
      unit.defaultFormId = rename(unit.defaultFormId);
      unit.formId = rename(unit.formId);
    }
    for (const actor of snapshot.actors) actor.formId = rename(actor.formId);
  }
  for (const tick of model.ticks)
    for (const event of tick.events) {
      if (event.fromFormId) event.fromFormId = rename(event.fromFormId);
      if (event.toFormId) event.toFormId = rename(event.toFormId);
    }
  return model;
}

/** The same arm, with the tick-9 transition redirected into the stance. */
function enteringTheStance(source: ReplayModel): ReplayModel {
  const model = structuredClone(source) as ReplayModel;
  for (const tick of model.ticks)
    for (const event of tick.events)
      if (
        event.type === 'form-transition-started' &&
        event.sourceActor?.actorKey === SHOOTER
      )
        event.toFormId = 'striker-child-volley-stance';
  for (const [index, tick] of model.ticks.entries()) {
    if (index < 10) continue;
    for (const actor of [...tick.before.actors, ...tick.after.actors])
      if (actor.actorKey === SHOOTER)
        actor.formId = 'striker-child-volley-stance';
  }
  return model;
}

/**
 * A three-bolt volley written into the fixture at `LAUNCH`, exactly as the engine emits
 * one: one actor, one tick, contiguous ascending IDs in leftmost-lane-first order.
 *
 * `survivors` says which lanes are still in flight on the tick after, so a cut fan can be
 * asked for as easily as an intact one.
 */
function withVolley(
  source: ReplayModel,
  ids: readonly string[],
  survivors: readonly string[],
): ReplayModel {
  const model = structuredClone(source) as ReplayModel;
  const template = model.ticks[LAUNCH].events.find(
    (event) => event.type === 'shot',
  )!;
  const origin = { x: 3, y: 6 };
  const headings: ReplayProjectileHeading[] = [
    'north-east',
    'east',
    'south-east',
  ];
  const step: Record<string, [number, number]> = {
    'north-east': [1, -1],
    east: [1, 0],
    'south-east': [1, 1],
  };
  const launchTick = model.ticks[LAUNCH];
  launchTick.events = launchTick.events.filter(
    (event) => event.type !== 'shot',
  );
  launchTick.projectileTraversals = [];
  launchTick.after.projectiles = [];
  const nextTick = model.ticks[LAUNCH + 1];
  nextTick.projectileTraversals = [];
  nextTick.after.projectiles = [];

  const owner = template.sourceActor!;
  const traversal = (
    id: string,
    heading: ReplayProjectileHeading,
    from: { x: number; y: number },
    to: { x: number; y: number },
  ) => ({
    projectileId: id,
    ownerActor: owner,
    ownerActorKey: owner.actorKey,
    launchDirection: heading,
    heading,
    from,
    path: [to],
    shotProgram: null,
    programmedPath: null,
  });
  const inFlight = (
    id: string,
    heading: ReplayProjectileHeading,
    at: { x: number; y: number },
    travelled: number,
  ) => ({
    projectileId: id,
    ownerActor: owner,
    ownerActorKey: owner.actorKey,
    position: at,
    launchDirection: heading,
    heading,
    shotProgram: null,
    programmedPath: null,
    ticksUntilAdvance: 1,
    remainingTiles: null,
    tilesPerAdvance: 1,
    nextProgrammedPathIndex: 0,
    tilesTraveled: travelled,
    phase: 0,
  });

  ids.forEach((id, lane) => {
    const heading = headings[lane];
    const [dx, dy] = step[heading];
    const first = { x: origin.x + dx, y: origin.y + dy };
    const second = { x: first.x + dx, y: first.y + dy };
    launchTick.events.push({
      ...structuredClone(template),
      eventId: `synthetic:${LAUNCH}:${id}`,
      projectileId: id,
      projectileHeading: heading,
      from: origin,
      to: first,
    } as ReplayCausalEvent);
    launchTick.projectileTraversals.push(
      traversal(id, heading, origin, first),
    );
    // A lane that does not survive its launch tick is absent from the tick's closing
    // state — exactly how the engine records a bolt consumed by a wall or a shell.
    if (!survivors.includes(id)) return;
    launchTick.after.projectiles!.push(inFlight(id, heading, first, 1));
    nextTick.projectileTraversals.push(
      traversal(id, heading, first, second),
    );
    nextTick.after.projectiles!.push(inFlight(id, heading, second, 2));
  });
  return model;
}

function frameHash(replay: ReplayModel, time: number): string {
  const canvas = createCanvas(360, 280);
  const ctx = canvas.getContext('2d');
  drawArena(
    ctx as unknown as CanvasRenderingContext2D,
    replay,
    { time, selectedUnitKey: null, showVisibility: false },
    360,
    280,
  );
  return createHash('sha256')
    .update(ctx.getImageData(0, 0, 360, 280).data)
    .digest('hex')
    .slice(0, 16);
}

function partOf(group: THREE.Object3D, unitKey: string, form: string) {
  let chassis: THREE.Object3D | null = null;
  group.traverse((node) => {
    if (node.userData.unitKey === unitKey && node.parent)
      chassis = node.parent;
  });
  assert.ok(chassis, `no chassis for ${unitKey}`);
  const part = (chassis as THREE.Object3D).children.find(
    (child) => child.userData.renderForm === form,
  );
  assert.ok(part, `no ${form} body on ${unitKey}`);
  return part;
}

test('a stance is a third body, not the turret and not the chassis', () => {
  assert.equal(stanceKindForForm('striker-prime-volley-stance'), 'volley');
  assert.equal(stanceKindForForm('striker-child-volley-stance'), 'volley');
  assert.equal(stanceKindForForm('bulwark-prime-aegis-shell'), 'aegis');
  assert.equal(stanceKindForForm('bulwark-child-aegis-shell'), 'aegis');
  // Everything that is not a stance must keep saying so, or the emplacement treatment
  // and the mobile chassis both start losing bodies to this branch.
  for (const formId of [
    'striker-prime',
    'bulwark-child-turret',
    'fabricator-prime',
    'turret',
    'child-mobile',
    null,
  ])
    assert.equal(stanceKindForForm(formId), null, String(formId));

  for (const [family, mobile] of [
    ['striker', 'striker-prime'],
    ['bulwark', 'bulwark-prime'],
  ] as const) {
    const looks = [
      fallbackLookIdForForm(mobile),
      fallbackLookIdForForm(`${mobile}-turret`),
      fallbackLookIdForForm(
        family === 'striker'
          ? `${mobile}-volley-stance`
          : `${mobile}-aegis-shell`,
      ),
    ];
    assert.equal(
      new Set(looks).size,
      3,
      `${family} wears three silhouettes (${looks.join(', ')})`,
    );
  }
  // A family with no stance skill is never handed one.
  assert.equal(fallbackLookIdForForm('fabricator-prime-volley-stance'), 'orbiter');
});

test('a unit resolves its stance from the replay catalog, not from a naming rule', () => {
  const arm = asVolleyArm(frontline);
  const stance = stanceFormForUnit(arm, SHOOTER_UNIT, 'striker-child');
  assert.deepEqual(stance, {
    formId: 'striker-child-volley-stance',
    kind: 'volley',
  });
  assert.equal(
    stanceFormForUnit(frontline, SHOOTER_UNIT, 'child-mobile'),
    null,
    'a ruleset without a stance form has no stance',
  );

  const mobile = unitLook(arm, SHOOTER_UNIT, 'striker-child');
  const emplaced = unitEmplacedLook(arm, SHOOTER_UNIT, 'striker-child');
  const standing = unitStanceLook(arm, SHOOTER_UNIT, 'striker-child');
  assert.ok(standing, 'the stance has artwork of its own');
  assert.equal(
    new Set([mobile.id, emplaced!.id, standing.id]).size,
    3,
    'three bodies, three looks',
  );
});

test('the 2.5D renderer opens a stance instead of rearing up a turret', () => {
  const actors = buildActors(enteringTheStance(asVolleyArm(frontline)));
  const mobile = partOf(actors.group, SHOOTER_UNIT, 'mobile');
  const turret = partOf(actors.group, SHOOTER_UNIT, 'stationary-omnidirectional');
  const stance = partOf(actors.group, SHOOTER_UNIT, 'stance-directional');

  actors.update(8, null, false);
  assert.equal(mobile.visible, true, 'mobile before the transition');
  assert.equal(stance.visible, false);

  actors.update(11.5, null, false);
  assert.equal(stance.visible, true, 'the stance body is out');
  assert.equal(turret.visible, false, 'and it is not a turret');
  assert.equal(mobile.visible, false);
  actors.dispose();
});

test('same owner, same tick, contiguous IDs is a volley — and nothing else is', () => {
  const arm = asVolleyArm(frontline);
  const volley = withVolley(arm, ['10', '11', '12'], ['10', '11', '12']);
  const lanes = volleyLanes(volley);
  assert.equal(lanes.size, 3);
  assert.deepEqual(
    ['10', '11', '12'].map((id) => lanes.get(id)?.laneIndex),
    [0, 1, 2],
    'lanes are the ascending ID order the contract guarantees',
  );

  // A gap in the identities is the engine saying these were not one launch.
  assert.equal(volleyLanes(withVolley(arm, ['10', '11', '13'], [])).size, 0);
  // And one bolt is a bolt.
  assert.equal(volleyLanes(withVolley(arm, ['10'], ['10'])).size, 0);
});

test('a volley flies as one run and is cut when a member terminates', () => {
  const arm = asVolleyArm(frontline);
  const intact = volleysAt(
    withVolley(arm, ['10', '11', '12'], ['10', '11', '12']),
    LAUNCH + 0.5,
  );
  assert.equal(intact.length, 1);
  assert.equal(intact[0].laneCount, 3);
  assert.deepEqual(
    intact[0].runs.map((run) => run.map((member) => member.laneIndex)),
    [[0, 1, 2]],
    'one connected glyph while every blade is alive',
  );
  assert.equal(intact[0].broken.length, 0);

  // The middle blade struck something on its launch tick: the survivors must not be
  // joined across the gap, or the arrow would claim a shape the volley no longer has.
  const cut = volleysAt(
    withVolley(arm, ['10', '11', '12'], ['10', '12']),
    LAUNCH + 1.4,
  );
  assert.equal(cut.length, 1);
  assert.deepEqual(
    cut[0].runs.map((run) => run.map((member) => member.laneIndex)),
    [[0], [2]],
    'two segments flying on, separately',
  );
  assert.equal(cut[0].broken.length, 1, 'and one segment breaking off');
  assert.equal(cut[0].broken[0].laneIndex, 1);
  assert.ok(
    cut[0].broken[0].breakAge! >= 0 && cut[0].broken[0].breakAge! <= 1,
  );
});

test('the arrow outline reaches ahead along each blade own heading', () => {
  const members = [
    { id: 'a', laneIndex: 0, x: 4, y: 4, heading: 'north' as const, breakAge: null, breakKind: null },
    { id: 'b', laneIndex: 1, x: 6, y: 4, heading: 'east' as const, breakAge: null, breakKind: null },
  ];
  const outline = volleyArrowOutline(members, 0.5, 0.5);
  assert.equal(outline.leading.length, 2);
  assert.equal(outline.trailing.length, 2);
  // North reaches up, east reaches right — the divergence is what bows the glyph.
  assert.ok(outline.leading[0].y < members[0].y);
  assert.ok(outline.leading[1].x > members[1].x);
  assert.ok(outline.trailing[0].y > members[0].y);

  // A lone survivor still has a width, so both renderers can assume a strip.
  const solo = volleyArrowOutline([members[1]], 0.5, 0.5);
  assert.equal(solo.leading.length, 2);
  assert.equal(solo.nodes.length, 1, 'but it is still one blade');
});

test('a volley is drawn, and its members are not also drawn as bolts', () => {
  const arm = asVolleyArm(frontline);
  const volley = withVolley(arm, ['10', '11', '12'], ['10', '11', '12']);
  // Same three projectiles, same positions, identities the recognizer rejects.
  const loose = withVolley(arm, ['10', '11', '13'], ['10', '11', '13']);
  assert.notEqual(
    frameHash(volley, LAUNCH + 0.5),
    frameHash(loose, LAUNCH + 0.5),
    'the arrow is a different picture from three bolts',
  );
});

test('a generation-3 replay gets the same muzzle flash and death as a duel', () => {
  // The names are the only difference: replay-v1/v2 say `shot`/`destroyed`, replay-v3
  // says `attack`/`destruction`, and the model deliberately keeps each document's own
  // vocabulary. Every presentation surface compared against one spelling, so a class-arm
  // replay played back with no muzzle flash, no kill flare, no recoil and no camera
  // knock — the three skills under review would have been the only things on screen.
  const spelled = structuredClone(asVolleyArm(frontline)) as ReplayModel;
  let renamed = 0;
  for (const tick of spelled.ticks)
    for (const event of tick.events) {
      if (event.type === 'shot') {
        event.type = 'attack';
        renamed++;
      } else if (event.type === 'destroyed') {
        event.type = 'destruction';
        renamed++;
      }
    }
  assert.ok(renamed > 0, 'the fixture has events worth renaming');
  for (const time of [10.5, 10.8, 11.7])
    assert.equal(
      frameHash(spelled, time),
      frameHash(asVolleyArm(frontline), time),
      `both spellings of the same event draw the same frame at ${time}`,
    );
});

test('a deflection is drawn on the guard, and is not a damage impact', () => {
  const arm = asVolleyArm(frontline);
  const guarded = structuredClone(arm) as ReplayModel;
  const tick = guarded.ticks[LAUNCH];
  const template = tick.events.find((event) => event.type === 'shot')!;
  const target = guarded.ticks[LAUNCH].after.actors.find(
    (actor) => actor.actorKey !== SHOOTER,
  )!;
  // `from` stays the shooter-side point from the template so the redirect
  // streak (contact, back along the reversed approach) is part of the frame.
  const deflected = {
    ...structuredClone(template),
    eventId: 'synthetic:deflected',
    type: 'projectile-deflected',
    targetActor: target.identity,
    toFacing: 'west',
    to: target.position,
    toFormId: 'striker-child-volley-stance',
  } as ReplayCausalEvent;
  tick.events.push(deflected);

  // Contacts resolve late in the tick, so before that instant nothing may be showing.
  assert.equal(
    frameHash(guarded, LAUNCH + 0.2),
    frameHash(arm, LAUNCH + 0.2),
    'a deflection does not leak into the first half of its tick',
  );
  assert.notEqual(
    frameHash(guarded, LAUNCH + 0.85),
    frameHash(arm, LAUNCH + 0.85),
    'and it is unmistakably drawn at the instant it happens',
  );

  // The same event as a hit is a different picture: a shockwave and sparks, on the
  // shooter's colour, plus a camera knock. If these ever matched, "the guard turned
  // that" would be rendered as "the guard was hit by that".
  const damaged = structuredClone(arm) as ReplayModel;
  damaged.ticks[LAUNCH].events.push({
    ...structuredClone(deflected),
    type: 'damage',
    amount: 1,
    newHealth: 2,
    from: target.position,
  } as ReplayCausalEvent);
  assert.notEqual(
    frameHash(guarded, LAUNCH + 0.85),
    frameHash(damaged, LAUNCH + 0.85),
  );
});
