import assert from 'node:assert/strict';
import test from 'node:test';
import * as THREE from 'three';
import {
  WALKABLE_OBJECTIVE_RENDER_ORDER,
  configureWalkableObjectiveRendering,
} from '../src/render3d/occlusionLayers.ts';

test('walkable objective housings yield their overlap to actor silhouettes', () => {
  const material = new THREE.MeshStandardMaterial();
  const mesh = new THREE.Mesh(new THREE.BoxGeometry(), material);
  const model = new THREE.Group();
  model.add(mesh);

  configureWalkableObjectiveRendering(model);

  assert.equal(mesh.renderOrder, WALKABLE_OBJECTIVE_RENDER_ORDER);
  assert.equal(material.depthTest, true);
  assert.equal(material.stencilWrite, true);
  assert.equal(material.stencilWriteMask, 0x00);
  assert.equal(material.stencilFunc, THREE.NotEqualStencilFunc);
  assert.equal(material.stencilRef, 1);
  assert.equal(material.stencilZPass, THREE.KeepStencilOp);
});
