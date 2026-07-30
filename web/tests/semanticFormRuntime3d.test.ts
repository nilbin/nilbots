import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { join } from 'node:path';
import test from 'node:test';
import * as THREE from 'three';
import type {
  ReplayModel,
  ReplayStableUnitKey,
} from '../src/replayModel.ts';
import type { SemanticFormClipContract } from '../src/render3d/semanticFormAnimation.ts';
import {
  EMPLACED_DEPLOY_CLIP,
  STANCE_DEPLOY_CLIP,
} from '../src/render3d/semanticFormAnimation.ts';
import { loadReplayJson } from '../src/replayIngress.ts';
import {
  bindSemanticLookAnimation,
  buildActors,
  installMobileModel,
} from './.harness/harness.entry.js';

const frontline = loadReplayJson(
  readFileSync(
    join(import.meta.dirname, 'fixtures', 'frontline-replay-v2.json'),
    'utf8',
  ),
).replay;

const MOBILE_UNIT: ReplayStableUnitKey = 'frontline:0:unit:0';
const TRANSFORMING_UNIT: ReplayStableUnitKey = 'frontline:0:unit:1';
const TRANSFORMING_ACTOR = 'frontline:0:unit:1:life:0';

const semanticContract: SemanticFormClipContract = {
  version: 1,
  stanceDeployClip: STANCE_DEPLOY_CLIP,
  emplacedDeployClip: EMPLACED_DEPLOY_CLIP,
  mobileRestClip: 'Mobile_Rest',
  mobileRecoilClip: 'Mobile_Recoil',
  stanceRecoilClip: 'Shell_Recoil',
  emplacedRecoilClip: 'Tower_Recoil',
};

interface SemanticRig {
  model: THREE.Group;
  shell: THREE.Object3D;
  tower: THREE.Object3D;
  recoil: THREE.Object3D;
}

function semanticClips(): THREE.AnimationClip[] {
  const shell = new THREE.VectorKeyframeTrack(
    'ShellMechanism.position',
    [0, 0.5, 1],
    [
      0, 0, 0,
      5, 1, 0,
      10, 2, 0,
    ],
  );
  const tower = new THREE.VectorKeyframeTrack(
    'TowerMechanism.position',
    [0, 0.5, 1],
    [
      0, 0, 0,
      0, 6, 0,
      0, 12, 0,
    ],
  );
  const rest = new THREE.VectorKeyframeTrack(
    'RecoilMechanism.position',
    [0, 0.5, 1],
    [
      0, 0, 0,
      0, 0.1, 0,
      0, 0, 0,
    ],
  );
  const recoil = (name: string, kick: number) =>
    new THREE.AnimationClip(
      name,
      1,
      [
        new THREE.VectorKeyframeTrack(
          'RecoilMechanism.position',
          [0, 0.5, 1],
          [
            0, 0, 0,
            kick, 0, 0,
            0, 0, 0,
          ],
        ),
      ],
    );
  return [
    new THREE.AnimationClip(STANCE_DEPLOY_CLIP, 1, [shell]),
    new THREE.AnimationClip(EMPLACED_DEPLOY_CLIP, 1, [tower]),
    new THREE.AnimationClip('Mobile_Rest', 1, [rest]),
    recoil('Mobile_Recoil', -1),
    recoil('Shell_Recoil', -2),
    recoil('Tower_Recoil', -3),
  ];
}

function semanticRig(
  clips: readonly THREE.AnimationClip[] = semanticClips(),
  contract: SemanticFormClipContract = semanticContract,
): SemanticRig {
  const model = new THREE.Group();
  const shell = new THREE.Object3D();
  shell.name = 'ShellMechanism';
  const tower = new THREE.Object3D();
  tower.name = 'TowerMechanism';
  const recoil = new THREE.Object3D();
  recoil.name = 'RecoilMechanism';
  model.add(shell, tower, recoil);
  assert.equal(
    bindSemanticLookAnimation(model, clips, contract),
    true,
    'fixture semantic rig binds',
  );
  return { model, shell, tower, recoil };
}

function chassisOf(
  group: THREE.Object3D,
  unitKey: ReplayStableUnitKey,
): THREE.Object3D {
  let found: THREE.Object3D | null = null;
  group.traverse((node) => {
    if (node.userData.unitKey === unitKey && node.parent)
      found = node.parent;
  });
  assert.ok(found, `no chassis for ${unitKey}`);
  return found;
}

function formPart(
  chassis: THREE.Object3D,
  form:
    | 'mobile'
    | 'stationary-omnidirectional'
    | 'stance-directional',
): THREE.Group {
  const part = chassis.children.find(
    (child) => child.userData.renderForm === form,
  );
  assert.ok(part, `no ${form} form`);
  assert.ok(part instanceof THREE.Group);
  return part;
}

function install(
  actors: ReturnType<typeof buildActors>,
  unitKey: ReplayStableUnitKey,
  rig: SemanticRig,
): {
  body: THREE.Group;
  turret: THREE.Group;
  stance: THREE.Group;
} {
  const chassis = chassisOf(actors.group, unitKey);
  const body = formPart(chassis, 'mobile');
  installMobileModel(body, rig.model, {
    hull: new THREE.Object3D(),
    lid: new THREE.Object3D(),
    facingMarker: new THREE.Object3D(),
  });
  return {
    body,
    turret: formPart(chassis, 'stationary-omnidirectional'),
    stance: formPart(chassis, 'stance-directional'),
  };
}

function close(actual: number, expected: number): void {
  assert.ok(
    Math.abs(actual - expected) < 1e-6,
    `${actual} was not close to ${expected}`,
  );
}

/**
 * Continue the fixture's same life through a reversible Tower → Mobile transition.
 * The renderer reads only these authoritative event/snapshot fields.
 */
function withMobilize(source: ReplayModel): ReplayModel {
  const model = structuredClone(source) as ReplayModel;
  const anchor = model.ticks[9].events.find(
    (event) =>
      event.type === 'form-transition-started' &&
      event.sourceActor?.actorKey === TRANSFORMING_ACTOR,
  );
  assert.ok(anchor);
  model.ticks[10].events.push({
    ...structuredClone(anchor),
    eventId: 'synthetic:semantic-mobilize',
    tick: 10,
    fromFormId: 'turret',
    toFormId: 'child-mobile',
    formTransitionStartedAtTick: 10,
    formTransitionCompletesAtTick: 10,
  });
  for (const [index, tick] of model.ticks.entries()) {
    for (const actor of index > 10 ? tick.before.actors : [])
      if (actor.actorKey === TRANSFORMING_ACTOR)
        actor.formId = 'child-mobile';
    for (const actor of index >= 10 ? tick.after.actors : [])
      if (actor.actorKey === TRANSFORMING_ACTOR)
        actor.formId = 'child-mobile';
  }
  return model;
}

/** Redirect the fixture's Anchor into Bulwark's directional Shell stance. */
function withShellStance(source: ReplayModel): ReplayModel {
  const model = structuredClone(source) as ReplayModel;
  const shellFormId = 'child-mobile-aegis-shell';
  const mobile = model.forms.find(
    (form) => form.formId === 'child-mobile',
  );
  assert.ok(mobile);
  model.forms.push({
    ...mobile,
    formId: shellFormId,
    canMove: false,
  });
  for (const tick of model.ticks) {
    for (const event of tick.events)
      if (
        event.sourceActor?.actorKey === TRANSFORMING_ACTOR &&
        (event.type === 'form-transition-started' ||
          event.type === 'form-changed') &&
        event.toFormId === 'turret'
      )
        event.toFormId = shellFormId;
    for (const state of [tick.before, tick.after])
      for (const actor of state.actors)
        if (
          actor.actorKey === TRANSFORMING_ACTOR &&
          actor.formId === 'turret'
        )
          actor.formId = shellFormId;
  }
  return model;
}

/** Add the defensive contact that actually drives a Shell's authored recoil. */
function withShellDeflection(source: ReplayModel): ReplayModel {
  const model = withShellStance(source);
  const tick = model.ticks[10];
  const template = tick.events[0];
  const target = tick.before.actors.find(
    (actor) => actor.actorKey === TRANSFORMING_ACTOR,
  );
  assert.ok(template);
  assert.ok(target);
  tick.events.push({
    ...structuredClone(template),
    eventId: 'synthetic:semantic-shell-deflection',
    tick: 10,
    type: 'projectile-deflected',
    sourceActor: null,
    targetActor: target.identity,
    fromFormId: null,
    toFormId: 'child-mobile-aegis-shell',
  });
  return model;
}

/** Defensive-looking contacts on ordinary Mobile/Tower forms must not borrow Shell recoil. */
function withNonShellDeflections(source: ReplayModel): ReplayModel {
  const model = structuredClone(source) as ReplayModel;
  const tick = model.ticks[10];
  tick.events = tick.events.filter(
    (event) => event.sourceActor?.actorKey !== TRANSFORMING_ACTOR,
  );
  const template = tick.events[0];
  const mobile = tick.before.actors.find(
    (actor) => actor.unitKey === MOBILE_UNIT,
  );
  const tower = tick.before.actors.find(
    (actor) => actor.actorKey === TRANSFORMING_ACTOR,
  );
  assert.ok(template);
  assert.ok(mobile);
  assert.ok(tower);
  for (const [ordinal, target, formId] of [
    [0, mobile, 'child-mobile'],
    [1, tower, 'turret'],
  ] as const)
    tick.events.push({
      ...structuredClone(template),
      eventId: `synthetic:non-shell-deflection:${ordinal}`,
      tick: 10,
      type: 'projectile-deflected',
      sourceActor: null,
      targetActor: target.identity,
      fromFormId: null,
      toFormId: formId,
    });
  return model;
}

test('semantic Tower deploy is replay-driven and isolated per bot instance', () => {
  const actors = buildActors(frontline);
  const mobileRig = semanticRig();
  const towerRig = semanticRig();
  const mobileParts = install(actors, MOBILE_UNIT, mobileRig);
  const towerParts = install(actors, TRANSFORMING_UNIT, towerRig);

  actors.update(9.75, null, false);

  // Tick 9's 1.5-tick Anchor is exactly halfway through here. A different stable bot at
  // the same playhead remains at the shared Mobile endpoint.
  close(towerRig.tower.position.y, 6);
  close(mobileRig.tower.position.y, 0);
  assert.equal(towerParts.body.visible, true);
  assert.equal(
    towerParts.turret.visible,
    false,
    'the generic turret stands down only after semantic binding',
  );
  assert.equal(towerParts.stance.visible, false);
  assert.equal(mobileParts.body.visible, true);

  actors.update(10.7, null, false);
  close(towerRig.tower.position.y, 12);
  assert.ok(
    towerRig.recoil.position.x < -1,
    `Tower recoil comes from the authored overlay (${towerRig.recoil.position.x})`,
  );
  close(towerParts.body.position.x, 0);

  // The authored recovery persists into the next replay tick. At 5 ticks/second this is
  // the difference between a readable ~230 ms action and crushing 34 source frames into
  // the generic renderer's 90 ms impulse.
  actors.update(11.3, null, false);
  assert.ok(
    towerRig.recoil.position.x < -0.5,
    `Tower is still recovering cross-tick (${towerRig.recoil.position.x})`,
  );
  const crossTickRecovery = towerRig.recoil.position.x;
  actors.update(10.7, null, false);
  actors.update(11.3, null, false);
  close(towerRig.recoil.position.x, crossTickRecovery);

  actors.update(11.61, null, false);
  close(towerRig.recoil.position.x, 0);

  actors.dispose();
});

test('mobilize samples the exact Tower clip backward without a body swap', () => {
  const actors = buildActors(withMobilize(frontline));
  const rig = semanticRig();
  const parts = install(actors, TRANSFORMING_UNIT, rig);

  actors.update(9.75, null, false);
  const forwardHalf = rig.tower.position.y;
  close(forwardHalf, 6);

  // The tick-10 mobilize has the same 1.5-tick span in reverse.
  actors.update(10.75, null, false);
  close(rig.tower.position.y, forwardHalf);
  close(rig.recoil.position.x, 0);
  assert.equal(parts.body.visible, true);
  assert.equal(parts.turret.visible, false);

  // The tick-10 shot fixture overlaps this synthetic mobilize. Once the reverse
  // transition interrupts its recoil, the tail must not reappear after deploy completes.
  actors.update(11.55, null, false);
  close(rig.recoil.position.x, 0);
  actors.update(11.6, null, false);
  close(rig.tower.position.y, 0);
  assert.equal(parts.body.visible, true);
  assert.equal(parts.turret.visible, false);

  actors.dispose();
});

test('Shell deploy uses its authored path while the exact guard sector remains visible', () => {
  const actors = buildActors(withShellStance(frontline));
  const rig = semanticRig();
  const parts = install(actors, TRANSFORMING_UNIT, rig);
  const guard = parts.stance.children.find(
    (child) => child.userData.cue === 'aegis-guard-arc',
  );
  const genericPlate = parts.stance.children.find(
    (child) => child.userData.cue === 'aegis-plate',
  );
  assert.ok(guard);
  assert.ok(genericPlate);

  actors.update(9.9, null, false);
  assert.ok(rig.shell.position.x > 5, 'Shell has crossed its authored midpoint');
  close(rig.tower.position.y, 0);
  assert.equal(parts.body.visible, true);
  assert.equal(parts.stance.visible, true, 'sector overlay keeps its parent');
  assert.equal(genericPlate.visible, false, 'generic Shell body is suppressed');
  assert.equal(guard.visible, true, 'rules-owned ±45° sector remains');

  actors.dispose();
});

test('Shell recoil follows an incoming deflection and preserves authored recovery', () => {
  const actors = buildActors(withShellDeflection(frontline));
  const rig = semanticRig();
  install(actors, TRANSFORMING_UNIT, rig);

  actors.update(10.2, null, false);
  close(rig.recoil.position.x, 0);

  actors.update(10.7, null, false);
  const progress = (10.7 - 10.45) / 1.15;
  close(rig.recoil.position.x, -4 * progress);
  assert.ok(
    rig.recoil.position.x < -0.5,
    `Shell contact drives its authored recoil (${rig.recoil.position.x})`,
  );
  const recovering = rig.recoil.position.x;
  actors.update(10.2, null, false);
  actors.update(10.7, null, false);
  close(rig.recoil.position.x, recovering);

  actors.update(11.61, null, false);
  close(rig.recoil.position.x, 0);

  actors.dispose();
});

test('deflection does not trigger Mobile or Tower firing recoil', () => {
  const actors = buildActors(withNonShellDeflections(frontline));
  const mobileRig = semanticRig();
  const towerRig = semanticRig();
  install(actors, MOBILE_UNIT, mobileRig);
  install(actors, TRANSFORMING_UNIT, towerRig);

  actors.update(10.7, null, false);
  close(mobileRig.recoil.position.x, 0);
  close(towerRig.recoil.position.x, 0);

  actors.dispose();
});

test('an incomplete semantic rig fails closed to the generic form renderer', () => {
  const actors = buildActors(frontline);
  const chassis = chassisOf(actors.group, TRANSFORMING_UNIT);
  const body = formPart(chassis, 'mobile');
  const model = new THREE.Group();
  const shell = new THREE.Object3D();
  shell.name = 'ShellMechanism';
  model.add(shell);
  const onlyShell = semanticClips().filter(
    (clip) => clip.name !== EMPLACED_DEPLOY_CLIP,
  );
  assert.equal(
    bindSemanticLookAnimation(model, onlyShell, semanticContract),
    false,
    'missing required Tower clip is not partially installed',
  );
  installMobileModel(body, model, {
    hull: new THREE.Object3D(),
    lid: new THREE.Object3D(),
    facingMarker: new THREE.Object3D(),
  });

  actors.update(10.7, null, false);
  assert.equal(body.visible, false);
  assert.equal(
    formPart(chassis, 'stationary-omnidirectional').visible,
    true,
    'the proven generic fallback remains in charge',
  );

  actors.dispose();
});
