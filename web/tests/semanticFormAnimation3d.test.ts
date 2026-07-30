import assert from 'node:assert/strict';
import test from 'node:test';
import * as THREE from 'three';
import {
  createSemanticFormAnimationSampler,
  EMPLACED_DEPLOY_CLIP,
  STANCE_DEPLOY_CLIP,
  validateSemanticFormClipContract,
  type SemanticFormClipContract,
} from '../src/render3d/semanticFormAnimation.ts';

const contract: SemanticFormClipContract = {
  version: 1,
  stanceDeployClip: STANCE_DEPLOY_CLIP,
  emplacedDeployClip: EMPLACED_DEPLOY_CLIP,
};

interface RigFixture {
  root: THREE.Group;
  plate: THREE.Object3D;
  crown: THREE.Object3D;
  pod: THREE.Object3D;
  clips: THREE.AnimationClip[];
}

function rigidRig(): RigFixture {
  const root = new THREE.Group();
  const chassis = new THREE.Group();
  chassis.name = 'Chassis';
  root.add(chassis);

  const plate = new THREE.Object3D();
  plate.name = 'ShellPlate';
  plate.position.set(2, 0, 0);
  chassis.add(plate);

  const crown = new THREE.Object3D();
  crown.name = 'TowerCrown';
  crown.position.set(0, 1, 0);
  chassis.add(crown);

  const pod = new THREE.Object3D();
  pod.name = 'LockPod';
  chassis.add(pod);

  const shellPosition = new THREE.VectorKeyframeTrack(
    'ShellPlate.position',
    [0, 0.4, 1],
    [
      2, 0, 0,
      4, 0.5, 0,
      8, 1, 0,
    ],
  );
  const shellRotation = new THREE.QuaternionKeyframeTrack(
    'ShellPlate.quaternion',
    [0, 1],
    [
      0, 0, 0, 1,
      0, 0, Math.SQRT1_2, Math.SQRT1_2,
    ],
  );
  const towerPosition = new THREE.VectorKeyframeTrack(
    'TowerCrown.position',
    [0, 0.6, 1],
    [
      0, 1, 0,
      0, 2.5, 0,
      0, 4, 0,
    ],
  );
  const shellPodPlant = new THREE.VectorKeyframeTrack(
    'LockPod.position',
    [0, 1],
    [
      0, 0, 0,
      0, -1, 0,
    ],
  );
  const towerPodPlant = new THREE.VectorKeyframeTrack(
    'LockPod.position',
    [0, 1],
    [
      0, 0, 0,
      0, -2, 0,
    ],
  );

  return {
    root,
    plate,
    crown,
    pod,
    clips: [
      new THREE.AnimationClip(
        STANCE_DEPLOY_CLIP,
        1,
        [shellPosition, shellRotation, shellPodPlant],
      ),
      new THREE.AnimationClip(
        EMPLACED_DEPLOY_CLIP,
        1,
        [towerPosition, towerPodPlant],
      ),
    ],
  };
}

function close(actual: number, expected: number): void {
  assert.ok(
    Math.abs(actual - expected) < 1e-6,
    `${actual} was not close to ${expected}`,
  );
}

test('semantic form metadata pins required names and validates optional clips', () => {
  const input = {
    ...contract,
    mobileRestClip: 'Mobile_Rest',
    stanceRecoilClip: 'Shell_Recoil',
  };
  assert.deepEqual(
    validateSemanticFormClipContract(input, 'fixture'),
    input,
  );
  assert.equal(
    validateSemanticFormClipContract(undefined, 'fixture'),
    undefined,
  );
  assert.throws(
    () =>
      validateSemanticFormClipContract(
        { ...contract, stanceDeployClip: 'ShellDeploy' },
        'fixture',
      ),
    /must use Shell_Deploy/,
  );
  assert.throws(
    () =>
      validateSemanticFormClipContract(
        { ...contract, mobileRestClip: 'Shell_Deploy' },
        'fixture',
      ),
    /must be distinct/,
  );
  assert.throws(
    () =>
      validateSemanticFormClipContract(
        { ...contract, fps: 30 },
        'fixture',
      ),
    /unknown fields/,
  );
});

test('deploy poses use exact endpoints and the same clip in reverse', () => {
  const { root, plate, crown, pod, clips } = rigidRig();
  const sampler = createSemanticFormAnimationSampler(
    root,
    clips,
    contract,
  );
  assert.ok(sampler);

  assert.equal(
    sampler.sample({ transition: 'stance', progress: 0 }),
    true,
  );
  close(plate.position.x, 2);
  close(plate.rotation.z, 0);

  assert.equal(
    sampler.sample({ transition: 'stance', progress: 0.4 }),
    true,
  );
  close(plate.position.x, 4);

  assert.equal(
    sampler.sample({ transition: 'stance', progress: 1 }),
    true,
  );
  close(plate.position.x, 8);
  close(plate.position.y, 1);
  close(plate.rotation.z, Math.PI / 2);
  close(pod.position.y, -1);

  // A decreasing authoritative progress value is the retract animation.
  assert.equal(
    sampler.sample({ transition: 'stance', progress: 0.4 }),
    true,
  );
  close(plate.position.x, 4);
  assert.equal(
    sampler.sample({ transition: 'stance', progress: 0 }),
    true,
  );
  close(plate.position.x, 2);
  close(plate.position.y, 0);
  close(plate.rotation.z, 0);
  close(pod.position.y, 0);

  assert.equal(
    sampler.sample({ transition: 'emplaced', progress: 1 }),
    true,
  );
  close(crown.position.y, 4);
  close(plate.position.x, 2);
  close(pod.position.y, -2);

  assert.equal(
    sampler.sample({ transition: 'emplaced', progress: -2 }),
    true,
  );
  close(crown.position.y, 1);
  assert.equal(
    sampler.sample({ transition: 'emplaced', progress: 7 }),
    true,
  );
  close(crown.position.y, 4);
});

test('rescrubbing is deterministic and mixers are isolated per actor', () => {
  const first = rigidRig();
  const second = rigidRig();
  const firstSampler = createSemanticFormAnimationSampler(
    first.root,
    first.clips,
    contract,
  );
  const secondSampler = createSemanticFormAnimationSampler(
    second.root,
    second.clips,
    contract,
  );
  assert.ok(firstSampler);
  assert.ok(secondSampler);

  assert.equal(
    firstSampler.sample({ transition: 'stance', progress: 0.25 }),
    true,
  );
  const pinnedPosition = first.plate.position.clone();
  const pinnedQuaternion = first.plate.quaternion.clone();

  assert.equal(
    firstSampler.sample({ transition: 'emplaced', progress: 0.9 }),
    true,
  );
  assert.equal(
    firstSampler.sample({ transition: 'stance', progress: 0.8 }),
    true,
  );
  assert.equal(
    firstSampler.sample({ transition: 'stance', progress: 0.25 }),
    true,
  );
  close(first.plate.position.distanceTo(pinnedPosition), 0);
  for (const component of ['x', 'y', 'z', 'w'] as const)
    close(
      first.plate.quaternion[component],
      pinnedQuaternion[component],
    );

  assert.equal(
    secondSampler.sample({ transition: 'stance', progress: 0.75 }),
    true,
  );
  assert.notEqual(first.plate.position.x, second.plate.position.x);
  close(first.plate.position.distanceTo(pinnedPosition), 0);
});

test('optional rest and recoil clips are absolute additive overlays', () => {
  const { root, plate, clips } = rigidRig();
  const rest = new THREE.VectorKeyframeTrack(
    'ShellPlate.position',
    [0, 0.5, 1],
    [
      2, 0, 0,
      2, 0.2, 0,
      2, 0, 0,
    ],
  );
  const recoil = new THREE.VectorKeyframeTrack(
    'ShellPlate.position',
    [0, 0.5, 1],
    [
      2, 0, 0,
      1.5, 0, 0,
      2, 0, 0,
    ],
  );
  const overlayContract: SemanticFormClipContract = {
    ...contract,
    mobileRestClip: 'Mobile_Rest',
    stanceRecoilClip: 'Shell_Recoil',
  };
  const sampler = createSemanticFormAnimationSampler(
    root,
    [
      ...clips,
      new THREE.AnimationClip('Mobile_Rest', 1, [rest]),
      new THREE.AnimationClip('Shell_Recoil', 1, [recoil]),
    ],
    overlayContract,
  );
  assert.ok(sampler);
  assert.deepEqual(sampler.capabilities, {
    mobileRest: true,
    mobileRecoil: false,
    stanceRecoil: true,
    emplacedRecoil: false,
  });

  assert.equal(
    sampler.sample({
      transition: 'stance',
      progress: 0,
      mobileRestPhase: 0.5,
    }),
    true,
  );
  close(plate.position.x, 2);
  close(plate.position.y, 0.2);

  // Planted forms deliberately suppress the mobile hover/rest overlay.
  assert.equal(
    sampler.sample({
      transition: 'stance',
      progress: 1,
      mobileRestPhase: 0.5,
      recoil: { form: 'stance', progress: 0.5 },
    }),
    true,
  );
  close(plate.position.x, 7.5);
  close(plate.position.y, 1);

  // Optional means a family can omit it; asking for an omitted clip is
  // rejected without mutating the last valid pose.
  const before = plate.position.clone();
  assert.equal(
    sampler.sample({
      transition: 'emplaced',
      progress: 0.5,
      recoil: { form: 'emplaced', progress: 0.5 },
    }),
    false,
  );
  close(plate.position.distanceTo(before), 0);

  assert.equal(
    sampler.sample({ transition: 'stance', progress: Number.NaN }),
    false,
  );
  close(plate.position.distanceTo(before), 0);
});

test('missing or unusable declared clips fail closed for generic fallback', () => {
  const missing = rigidRig();
  assert.equal(
    createSemanticFormAnimationSampler(
      missing.root,
      missing.clips.slice(0, 1),
      contract,
    ),
    null,
  );

  const duplicate = rigidRig();
  assert.equal(
    createSemanticFormAnimationSampler(
      duplicate.root,
      [
        ...duplicate.clips,
        duplicate.clips[0].clone(),
      ],
      contract,
    ),
    null,
  );

  const empty = rigidRig();
  assert.equal(
    createSemanticFormAnimationSampler(
      empty.root,
      [
        empty.clips[0],
        new THREE.AnimationClip(EMPLACED_DEPLOY_CLIP, 1, []),
      ],
      contract,
    ),
    null,
  );
});

test('disposal is idempotent and rejects later samples', () => {
  const { root, clips } = rigidRig();
  const sampler = createSemanticFormAnimationSampler(
    root,
    clips,
    contract,
  );
  assert.ok(sampler);
  sampler.dispose();
  sampler.dispose();
  assert.equal(
    sampler.sample({ transition: 'stance', progress: 0.5 }),
    false,
  );
});
