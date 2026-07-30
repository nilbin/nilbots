import assert from 'node:assert/strict';
import test from 'node:test';
import * as THREE from 'three';
import type { LookModelSpec } from '../src/render3d/lookModel.ts';
import {
  actorLookMaterial,
  formModelMountCount,
  formTransitionPresentation,
  modelSupportsPurpose,
  stanceLayerPolicy,
  turretSupportYaw,
} from './.harness/harness.entry.js';

const shell: LookModelSpec = {
  version: 1,
  id: 'aegis-tortoise-shell',
  file: 'model.glb',
  kind: 'bot',
  part: 'whole',
  facing: '+x',
  up: '+y',
  skillHardware: 'aegis',
  formRole: 'directional-stance',
};

const wholeTurret: LookModelSpec = {
  version: 1,
  id: 'aegis-tortoise-turret',
  file: 'model.glb',
  kind: 'bot',
  part: 'whole',
  facing: '+x',
  up: '+y',
  formRole: 'omnidirectional-emplacement',
};

const turretArm: LookModelSpec = {
  ...wholeTurret,
  id: 'legacy-turret-arm',
  part: 'turret-arm',
  formRole: undefined,
};

test('a dedicated whole turret mounts once while one arm and every fallback remain four-spoke', () => {
  assert.equal(
    formModelMountCount(
      'omnidirectional-emplacement',
      wholeTurret,
      true,
      true,
    ),
    1,
  );
  assert.equal(
    formModelMountCount(
      'omnidirectional-emplacement',
      turretArm,
      true,
      true,
    ),
    4,
  );
  assert.equal(
    formModelMountCount(
      'omnidirectional-emplacement',
      wholeTurret,
      false,
      true,
    ),
    4,
    'a failed whole GLB returns to the SVG front-sector construction',
  );
  assert.equal(
    formModelMountCount(
      'omnidirectional-emplacement',
      wholeTurret,
      true,
      false,
    ),
    4,
    'a mobile look reused by a legacy form is not mistaken for a whole turret',
  );
});

test('a whole Shell is one directional stance and cannot be accepted as a turret', () => {
  assert.equal(
    formModelMountCount('directional-stance', shell, true, true),
    1,
  );
  assert.equal(
    modelSupportsPurpose(shell, {
      kind: 'directional-stance',
      skillHardware: 'aegis',
    }),
    true,
  );
  assert.equal(
    modelSupportsPurpose(shell, {
      kind: 'omnidirectional-emplacement',
    }),
    false,
  );
  assert.equal(
    modelSupportsPurpose(shell, { kind: 'mobile' }),
    false,
  );
  assert.equal(
    shell.animation,
    undefined,
    'the fused Meshy Shell does not require authored clips',
  );
});

test('form roles and hardware keep whole Turret, Shell and mobile contracts disjoint', () => {
  assert.equal(
    modelSupportsPurpose(wholeTurret, {
      kind: 'omnidirectional-emplacement',
    }),
    true,
  );
  assert.equal(
    modelSupportsPurpose(wholeTurret, {
      kind: 'directional-stance',
      skillHardware: 'aegis',
    }),
    false,
  );
  assert.equal(
    modelSupportsPurpose(wholeTurret, { kind: 'mobile' }),
    false,
  );
  assert.equal(
    modelSupportsPurpose(turretArm, {
      kind: 'omnidirectional-emplacement',
    }),
    true,
  );
  assert.equal(
    modelSupportsPurpose(turretArm, {
      kind: 'directional-stance',
      skillHardware: 'aegis',
    }),
    false,
  );
  assert.equal(
    wholeTurret.animation,
    undefined,
    'the fused Meshy Turret does not require authored clips',
  );
});

test('a static whole model with a missing or wrong role keeps the honest fallback', () => {
  const missingRole = {
    ...wholeTurret,
    formRole: undefined,
  };
  const shellRole = {
    ...wholeTurret,
    formRole: 'directional-stance' as const,
    skillHardware: 'aegis' as const,
  };
  for (const invalid of [missingRole, shellRole]) {
    assert.equal(
      modelSupportsPurpose(invalid, {
        kind: 'omnidirectional-emplacement',
      }),
      false,
    );
    assert.equal(
      formModelMountCount(
        'omnidirectional-emplacement',
        invalid,
        false,
        true,
      ),
      4,
      'an unresolved/invalid GLB returns to the four-spoke SVG path',
    );
  }
});

test('Shell replaces only fallback physical hardware and never the semantic guard field', () => {
  assert.deepEqual(stanceLayerPolicy(shell, true, 'aegis'), {
    hideFallbackPhysicalHardware: true,
    preserveSemanticGuardOverlay: true,
  });
  assert.deepEqual(stanceLayerPolicy(shell, false, 'aegis'), {
    hideFallbackPhysicalHardware: false,
    preserveSemanticGuardOverlay: true,
  });
});

test('static form transition phases are deterministic and reverse without animation history', () => {
  const start = formTransitionPresentation(0);
  const middle = formTransitionPresentation(0.5);
  const end = formTransitionPresentation(1);

  assert.deepEqual(start, formTransitionPresentation(-1));
  assert.deepEqual(end, formTransitionPresentation(2));
  assert.deepEqual(middle, formTransitionPresentation(0.5));
  assert.equal(start.modelOpacity, 0);
  assert.equal(start.energyOpacity, 0);
  assert.equal(end.modelOpacity, 1);
  assert.ok(Math.abs(end.energyOpacity) < 1e-12);
  assert.ok(middle.modelScale > start.modelScale);
  assert.ok(middle.modelScale < end.modelScale);
  assert.ok(middle.energyOpacity > 0);

  // Mobilizing samples the same curve in reverse; no previous rendered frame
  // can change the answer at the same authoritative progress.
  assert.deepEqual(
    formTransitionPresentation(1 - 0.5),
    middle,
  );
});

test('a whole Turret remains planted while legacy spokes retain their scan-time rotation', () => {
  assert.equal(turretSupportYaw(true, true, true, 3.5), 0);
  assert.equal(turretSupportYaw(true, true, true, 99), 0);
  assert.equal(turretSupportYaw(false, false, true, 3.5), 0);
  assert.equal(turretSupportYaw(false, true, false, 3.5), 0);
  assert.notEqual(turretSupportYaw(false, true, true, 3.5), 0);
});

test('owner tint cannot contaminate fixed class PBR surfaces', () => {
  const fixed = new THREE.MeshStandardMaterial({
    color: '#14d9e7',
    emissive: '#063a45',
    emissiveIntensity: 0.42,
    roughness: 0.37,
    metalness: 0.68,
  });
  const owner = new THREE.Color('#f97316');
  const untouched = actorLookMaterial(
    fixed,
    undefined,
    owner,
  ) as THREE.MeshStandardMaterial;

  assert.notEqual(untouched, fixed, 'actor still owns its material clone');
  assert.equal(untouched.color.getHex(), fixed.color.getHex());
  assert.equal(untouched.emissive.getHex(), fixed.emissive.getHex());
  assert.equal(untouched.emissiveIntensity, fixed.emissiveIntensity);
  assert.equal(untouched.roughness, fixed.roughness);
  assert.equal(untouched.metalness, fixed.metalness);

  const tagged = fixed.clone();
  tagged.userData.nilbotsRole = 'team-accent';
  const accented = actorLookMaterial(
    tagged,
    undefined,
    owner,
  ) as THREE.MeshStandardMaterial;
  assert.equal(accented.color.getHex(), owner.getHex());
  assert.equal(accented.emissive.getHex(), owner.getHex());
  assert.ok(accented.emissiveIntensity >= 1.2);

  const projectile = actorLookMaterial(
    fixed,
    owner,
    new THREE.Color('#ffffff'),
  ) as THREE.MeshStandardMaterial;
  assert.equal(projectile.color.getHex(), owner.getHex());
  assert.equal(projectile.emissive.getHex(), owner.getHex());

  fixed.dispose();
  untouched.dispose();
  tagged.dispose();
  accented.dispose();
  projectile.dispose();
});
