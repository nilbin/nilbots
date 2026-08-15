import assert from 'node:assert/strict';
import test from 'node:test';
import * as THREE from 'three';
import {
  MODEL_LIGHTING_PROFILE,
  applyModelLighting,
  applyPointLightIsolation,
  createModelTeamOutline,
  injectPointLightIsolation,
  injectModelLighting,
  isolateModelFromPointLights,
  setModelApexTreatment,
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

test('apex surface treatment stays narrow, team-coloured, and texture-preserving', () => {
  const shader = injectModelLighting(`before\n${shaderAnchor}\nafter`);
  assert.match(shader, /uniform float nilbotsApexStrength/);
  assert.match(shader, /uniform vec3 nilbotsApexColor/);
  assert.match(shader, /nilbotsApexRim/);
  assert.match(shader, /nilbotsApexSweepDirection/);
  assert.match(shader, /nilbotsApexColor/);
  assert.match(shader, /material\.diffuseColor/);
  assert.doesNotMatch(shader, /0\.38, 0\.12, 0\.72/);
  assert.doesNotMatch(shader, /nilbotsApexCoat/);
  assert.doesNotMatch(shader, /nilbotsApexCounterSweep/);
  assert.doesNotMatch(shader, /PointLight/);
  assert.ok(MODEL_LIGHTING_PROFILE.apexRimStrength <= 0.04);
  assert.ok(MODEL_LIGHTING_PROFILE.apexSweepStrength <= 0.15);
  assert.ok(MODEL_LIGHTING_PROFILE.apexSweepPower >= 30);
  assert.ok(MODEL_LIGHTING_PROFILE.apexOutlineWidth <= 0.05);
});

test('team-coloured outline and radiance remain exclusive to Apex', () => {
  const sourceMaterial = new THREE.MeshStandardMaterial({ color: 0x6c7d52 });
  const source = new THREE.Mesh(new THREE.BoxGeometry(), sourceMaterial);
  const teamColor = new THREE.Color('#38bdf8');
  const outline = createModelTeamOutline(teamColor);
  const shells = outline.attach(source);
  assert.ok(shells);
  assert.equal(shells.core.parent, source);
  assert.equal(shells.radiance.parent, source);
  assert.equal(shells.core.material, outline.material);
  assert.equal(shells.radiance.material, outline.radianceMaterial);
  assert.equal(outline.material.side, THREE.BackSide);
  assert.equal(outline.material.depthWrite, false);
  assert.equal(outline.material.toneMapped, false);
  assert.equal(outline.material.blending, THREE.NormalBlending);
  assert.equal(outline.radianceMaterial.blending, THREE.AdditiveBlending);
  assert.equal(outline.material.color.getHex(), teamColor.getHex());
  assert.equal(outline.radianceMaterial.color.getHex(), teamColor.getHex());
  assert.equal(sourceMaterial.color.getHex(), 0x6c7d52);

  outline.set(0, 0.5, 1);
  assert.equal(shells.core.visible, false);
  assert.equal(shells.radiance.visible, false);
  assert.equal(outline.material.opacity, 0);
  assert.equal(outline.radianceMaterial.opacity, 0);

  const coreShader = {
    vertexShader: `before\n#include <begin_vertex>\nafter`,
    fragmentShader: 'void main() {}',
    uniforms: {},
  } as Parameters<typeof outline.material.onBeforeCompile>[0];
  outline.material.onBeforeCompile(
    coreShader,
    {} as Parameters<typeof outline.material.onBeforeCompile>[1],
  );
  assert.match(coreShader.vertexShader, /uniform float nilbotsTeamOutlineWidth/);
  assert.match(coreShader.vertexShader, /normalize\( normal \)/);
  assert.equal(
    coreShader.uniforms.nilbotsTeamOutlineWidth!.value,
    0,
  );

  const radianceShader = {
    vertexShader: `before\n#include <begin_vertex>\nafter`,
    fragmentShader: 'void main() {}',
    uniforms: {},
  } as Parameters<typeof outline.radianceMaterial.onBeforeCompile>[0];
  outline.radianceMaterial.onBeforeCompile(
    radianceShader,
    {} as Parameters<typeof outline.radianceMaterial.onBeforeCompile>[1],
  );

  outline.set(1, 1, 1);
  assert.equal(shells.core.visible, true);
  assert.equal(shells.radiance.visible, true);
  assert.ok(outline.material.opacity > 0.7);
  assert.ok(outline.radianceMaterial.opacity > 0.1);
  assert.ok(
    radianceShader.uniforms.nilbotsTeamOutlineWidth!.value >
      coreShader.uniforms.nilbotsTeamOutlineWidth!.value,
  );
  outline.dispose();
  source.geometry.dispose();
  sourceMaterial.dispose();
});

test('apex treatment retains precompile state and updates compiled uniforms', () => {
  const material = new THREE.MeshStandardMaterial();
  const teal = new THREE.Color('#38bdf8');
  const orange = new THREE.Color('#fb923c');
  applyModelLighting(material);
  setModelApexTreatment(material, 2, 1.25, teal);

  const state = material.userData.nilbotsModelLightingState as {
    apexStrength: number;
    apexPhase: number;
    apexColor: THREE.Color;
  };
  assert.equal(state.apexStrength, 1);
  assert.equal(state.apexPhase, 1.25);
  assert.equal(state.apexColor.getHex(), teal.getHex());

  const shader = {
    fragmentShader: `before\n${shaderAnchor}\nafter`,
    uniforms: {},
  } as Parameters<typeof material.onBeforeCompile>[0];
  material.onBeforeCompile(
    shader,
    {} as Parameters<typeof material.onBeforeCompile>[1],
  );
  assert.equal(shader.uniforms.nilbotsApexStrength!.value, 1);
  assert.equal(shader.uniforms.nilbotsApexPhase!.value, 1.25);
  assert.equal(shader.uniforms.nilbotsApexColor!.value.getHex(), teal.getHex());

  setModelApexTreatment(material, 0.35, 2.5, orange);
  assert.equal(shader.uniforms.nilbotsApexStrength!.value, 0.35);
  assert.equal(shader.uniforms.nilbotsApexPhase!.value, 2.5);
  assert.equal(shader.uniforms.nilbotsApexColor!.value.getHex(), orange.getHex());
});

test('model lighting is idempotent and owns a distinct shader cache key', () => {
  const material = new THREE.MeshStandardMaterial();
  applyModelLighting(material);
  const firstCompile = material.onBeforeCompile;
  const firstKey = material.customProgramCacheKey();
  applyModelLighting(material);

  assert.equal(material.onBeforeCompile, firstCompile);
  assert.equal(material.customProgramCacheKey(), firstKey);
  assert.match(firstKey, /nilbots-model-lighting-v5/);
  assert.equal(material.userData.nilbotsModelLighting, 'nilbots-model-lighting-v5');
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
  assert.match(firstKey, /nilbots-model-lighting-v5/);
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
