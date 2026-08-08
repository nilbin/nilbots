import assert from 'node:assert/strict';
import test from 'node:test';
import * as THREE from 'three';
import {
  installMobileModel,
  isGenuineLookModel,
} from './.harness/harness.entry.js';

function mobilePlaceholder(): {
  body: THREE.Group;
  hull: THREE.Object3D;
  lid: THREE.Object3D;
  facingMarker: THREE.Object3D;
} {
  const body = new THREE.Group();
  const hull = new THREE.Object3D();
  const lid = new THREE.Object3D();
  const facingMarker = new THREE.Object3D();
  body.add(hull, lid, facingMarker);
  return { body, hull, lid, facingMarker };
}

test('a genuine GLB replaces the loading solids and its redundant facing wedge', () => {
  const { body, hull, lid, facingMarker } = mobilePlaceholder();
  const model = new THREE.Group();
  model.userData.nilbotsModelSource = 'gltf';

  assert.equal(isGenuineLookModel(model), true);
  installMobileModel(body, model, { hull, lid, facingMarker });

  assert.deepEqual(body.children, [model]);
});

test('an SVG fallback replaces the loading solids but keeps the facing wedge', () => {
  const { body, hull, lid, facingMarker } = mobilePlaceholder();
  const model = new THREE.Group();
  model.userData.nilbotsModelSource = 'fallback';

  assert.equal(isGenuineLookModel(model), false);
  installMobileModel(body, model, { hull, lid, facingMarker });

  assert.deepEqual(body.children, [facingMarker, model]);
});

test('an untagged representation conservatively keeps the facing wedge', () => {
  const { body, hull, lid, facingMarker } = mobilePlaceholder();
  const model = new THREE.Group();

  assert.equal(isGenuineLookModel(model), false);
  installMobileModel(body, model, { hull, lid, facingMarker });

  assert.deepEqual(body.children, [facingMarker, model]);
});
