import assert from 'node:assert/strict';
import test from 'node:test';
import * as THREE from 'three';
import {
  MODEL_LIGHTING_PROFILE,
  applyModelLighting,
  injectModelLighting,
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
