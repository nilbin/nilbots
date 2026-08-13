import assert from 'node:assert/strict';
import test from 'node:test';
import * as THREE from 'three';
import {
  MODEL_LIGHTING_PROFILE,
  applyModelLighting,
  applyPointLightIsolation,
  injectPointLightIsolation,
  injectModelLighting,
  isolateModelFromPointLights,
} from '../src/render3d/modelLighting.ts';

const shaderAnchor =
  'vec3 outgoingLight = totalDiffuse + totalSpecular + totalEmissiveRadiance;';

test('genuine model lighting adds a bounded fill and cool rim after ordinary PBR light', () => {
  const shader = injectModelLighting(`before\n${shaderAnchor}\nafter`);
  assert.match(shader, /nilbotsModelFacing/);
  assert.match(shader, /material\.diffuseColor \* nilbotsModelFill/);
  assert.match(shader, /vec3\( 0\.22, 0\.28, 0\.36 \)/);
  assert.ok(MODEL_LIGHTING_PROFILE.facingFillMinimum > 0);
  assert.ok(MODEL_LIGHTING_PROFILE.facingFillMaximum <= 0.2);
  assert.ok(MODEL_LIGHTING_PROFILE.rimStrength <= 0.16);
});

test('model lighting is idempotent and owns a distinct shader cache key', () => {
  const material = new THREE.MeshStandardMaterial();
  applyModelLighting(material);
  const firstCompile = material.onBeforeCompile;
  const firstKey = material.customProgramCacheKey();
  applyModelLighting(material);

  assert.equal(material.onBeforeCompile, firstCompile);
  assert.equal(material.customProgramCacheKey(), firstKey);
  assert.match(firstKey, /nilbots-model-lighting-v1/);
  assert.equal(material.userData.nilbotsModelLighting, 'nilbots-model-lighting-v1');
});

test('model lighting fails loudly if Three changes its physical shader seam', () => {
  assert.throws(
    () => injectModelLighting('void main() {}'),
    /no longer exposes the model-lighting anchor/,
  );
});

test('neutral fixture lighting drops only the point-light loop', () => {
  const shader = 'before\n#include <lights_fragment_begin>\nafter';
  const isolated = injectPointLightIsolation(shader);

  assert.doesNotMatch(isolated, /NUM_POINT_LIGHTS/);
  assert.match(isolated, /NUM_SPOT_LIGHTS/);
  assert.match(isolated, /transient point lights disabled/);
});

test('point-light isolation composes once with readable model lighting', () => {
  const material = new THREE.MeshStandardMaterial();
  applyModelLighting(material);
  applyPointLightIsolation(material);
  const firstCompile = material.onBeforeCompile;
  const firstKey = material.customProgramCacheKey();
  applyPointLightIsolation(material);

  assert.equal(material.onBeforeCompile, firstCompile);
  assert.equal(material.customProgramCacheKey(), firstKey);
  assert.match(firstKey, /nilbots-model-lighting-v1/);
  assert.match(firstKey, /nilbots-point-light-isolation-v1/);

  const model = new THREE.Group();
  model.add(new THREE.Mesh(new THREE.BoxGeometry(), material));
  isolateModelFromPointLights(model);
  assert.equal(material.onBeforeCompile, firstCompile);
});

test('point-light isolation fails loudly if Three changes its light block', () => {
  assert.throws(
    () => injectPointLightIsolation('void main() {}'),
    /no longer exposes the point-light block/,
  );
});
