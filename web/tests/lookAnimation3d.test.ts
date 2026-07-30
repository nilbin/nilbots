import assert from 'node:assert/strict';
import test from 'node:test';
import * as THREE from 'three';
import {
  bindLookAnimation,
  releaseLookAnimation,
  seekLookAnimation,
  validateLookAnimationSpec,
} from './.harness/harness.entry.js';

const animationSpec = {
  idleClip: 'Aegis_Idle',
  movingClip: 'Aegis_Walk',
};

function animatedRoot(): {
  root: THREE.Group;
  part: THREE.Object3D;
  clips: THREE.AnimationClip[];
} {
  const root = new THREE.Group();
  const part = new THREE.Object3D();
  part.name = 'Foot';
  root.add(part);
  const track = (distance: number) =>
    new THREE.VectorKeyframeTrack(
      'Foot.position',
      [0, 1],
      [0, 0, 0, distance, 0, 0],
    );
  return {
    root,
    part,
    clips: [
      new THREE.AnimationClip('Aegis_Idle', 1, [track(1)]),
      new THREE.AnimationClip('Aegis_Walk', 1, [track(2)]),
    ],
  };
}

test('look animation metadata accepts two explicit distinct clip names', () => {
  assert.deepEqual(
    validateLookAnimationSpec(animationSpec, 'fixture'),
    animationSpec,
  );
  assert.equal(validateLookAnimationSpec(undefined, 'fixture'), undefined);
  assert.throws(
    () =>
      validateLookAnimationSpec(
        { idleClip: 'same', movingClip: 'same' },
        'fixture',
      ),
    /must be distinct/,
  );
  assert.throws(
    () =>
      validateLookAnimationSpec(
        { ...animationSpec, speed: 2 },
        'fixture',
      ),
    /unknown fields/,
  );
});

test('animation sampling is an absolute function of motion and phase', () => {
  const { root, part, clips } = animatedRoot();
  assert.equal(bindLookAnimation(root, clips, animationSpec), true);

  assert.equal(seekLookAnimation(root, 'idle', 0.25), true);
  assert.equal(part.position.x, 0.25);
  assert.equal(seekLookAnimation(root, 'moving', 0.25), true);
  assert.equal(part.position.x, 0.5);

  // Sampling elsewhere and returning to the same replay phase must not retain
  // any frame-to-frame animation history.
  assert.equal(seekLookAnimation(root, 'moving', 0.9), true);
  assert.equal(seekLookAnimation(root, 'idle', 0.25), true);
  assert.equal(part.position.x, 0.25);

  // Normalized phases wrap in both directions.
  assert.equal(seekLookAnimation(root, 'moving', 1.25), true);
  assert.equal(part.position.x, 0.5);
  assert.equal(seekLookAnimation(root, 'moving', -0.75), true);
  assert.equal(part.position.x, 0.5);

  releaseLookAnimation(root);
  assert.equal(seekLookAnimation(root, 'idle', 0), false);
});

test('a declared rig with a missing clip cannot replace the fallback', () => {
  const { root, clips } = animatedRoot();
  assert.equal(
    bindLookAnimation(root, clips.slice(0, 1), animationSpec),
    false,
  );
  assert.equal(seekLookAnimation(root, 'idle', 0), false);
});
