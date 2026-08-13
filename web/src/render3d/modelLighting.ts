import * as THREE from 'three';

const OUTGOING_LIGHT =
  'vec3 outgoingLight = totalDiffuse + totalSpecular + totalEmissiveRadiance;';
const PROFILE_KEY = 'nilbots-model-lighting-v1';
const POINT_LIGHT_ISOLATION_KEY = 'nilbots-point-light-isolation-v1';
const LIGHTS_FRAGMENT_INCLUDE = '#include <lights_fragment_begin>';
const POINT_LIGHTS_START =
  '#if ( NUM_POINT_LIGHTS > 0 ) && defined( RE_Direct )';
const POINT_LIGHTS_END =
  '#if ( NUM_SPOT_LIGHTS > 0 ) && defined( RE_Direct )';

/**
 * A restrained, model-local fill and rim for genuine GLB actors.
 *
 * Raising the arena lights or exposure also raises the floor, walls, fog and effects — the
 * exact background the small models need to separate from. This treatment instead adds a
 * camera-facing softbox and a weak cool edge after the ordinary PBR lights have resolved.
 * It retains the authored base colour, roughness, metalness, normal maps, emission and
 * shadows; only the final model material receives the extra readability light.
 */
export const MODEL_LIGHTING_PROFILE = {
  facingFillMinimum: 0.12,
  facingFillMaximum: 0.2,
  rimStrength: 0.16,
  rimPower: 2.2,
} as const;

export function applyModelLighting(
  material: THREE.MeshStandardMaterial,
): void {
  if (material.userData.nilbotsModelLighting === PROFILE_KEY) return;
  const beforeCompile = material.onBeforeCompile.bind(material);
  const beforeCacheKey = material.customProgramCacheKey.bind(material);

  material.onBeforeCompile = (shader, renderer) => {
    beforeCompile(shader, renderer);
    shader.fragmentShader = injectModelLighting(shader.fragmentShader);
  };
  material.customProgramCacheKey = () =>
    `${beforeCacheKey()}|${PROFILE_KEY}`;
  material.userData.nilbotsModelLighting = PROFILE_KEY;
  material.needsUpdate = true;
}

export function injectModelLighting(fragmentShader: string): string {
  if (!fragmentShader.includes(OUTGOING_LIGHT))
    throw new Error('MeshStandardMaterial shader no longer exposes the model-lighting anchor.');
  return fragmentShader.replace(
    OUTGOING_LIGHT,
    `${OUTGOING_LIGHT}

  // Model-local readability light. All terms are in Three's linear working space.
  float nilbotsModelFacing = saturate( dot( geometryNormal, geometryViewDir ) );
  float nilbotsModelFill = mix(
    ${MODEL_LIGHTING_PROFILE.facingFillMinimum.toFixed(2)},
    ${MODEL_LIGHTING_PROFILE.facingFillMaximum.toFixed(2)},
    nilbotsModelFacing
  );
  float nilbotsModelRim = pow(
    1.0 - nilbotsModelFacing,
    ${MODEL_LIGHTING_PROFILE.rimPower.toFixed(1)}
  ) * ${MODEL_LIGHTING_PROFILE.rimStrength.toFixed(2)};
  outgoingLight += material.diffuseColor * nilbotsModelFill;
  outgoingLight += mix(
    vec3( 0.22, 0.28, 0.36 ),
    material.diffuseColor,
    0.22
  ) * nilbotsModelRim;`,
  );
}

/**
 * Keep a neutral floor fixture stable when replay-owned point lights pass over it.
 *
 * Cores are real point lights because they should illuminate carriers and nearby combat.
 * A Reactor housing is different: its team read comes from a separate self-lit aperture,
 * so letting those transient lights recolour the neutral metal makes one ally standing on
 * it look like an extra Reactor state. Directional, ambient, shadow, authored emission,
 * and the model-local readability profile remain intact; only the point-light loop is
 * removed from this material's compiled shader.
 */
export function applyPointLightIsolation(
  material: THREE.MeshStandardMaterial,
): void {
  if (material.userData.nilbotsPointLightIsolation === POINT_LIGHT_ISOLATION_KEY)
    return;
  const beforeCompile = material.onBeforeCompile.bind(material);
  const beforeCacheKey = material.customProgramCacheKey.bind(material);

  material.onBeforeCompile = (shader, renderer) => {
    beforeCompile(shader, renderer);
    shader.fragmentShader = injectPointLightIsolation(shader.fragmentShader);
  };
  material.customProgramCacheKey = () =>
    `${beforeCacheKey()}|${POINT_LIGHT_ISOLATION_KEY}`;
  material.userData.nilbotsPointLightIsolation = POINT_LIGHT_ISOLATION_KEY;
  material.needsUpdate = true;
}

/** Apply point-light isolation once per distinct standard material in a model. */
export function isolateModelFromPointLights(model: THREE.Object3D): void {
  const materials = new Set<THREE.MeshStandardMaterial>();
  model.traverse((node) => {
    const mesh = node as THREE.Mesh;
    if (!mesh.isMesh) return;
    for (const material of Array.isArray(mesh.material)
      ? mesh.material
      : [mesh.material]) {
      if (material instanceof THREE.MeshStandardMaterial) materials.add(material);
    }
  });
  for (const material of materials) applyPointLightIsolation(material);
}

export function injectPointLightIsolation(fragmentShader: string): string {
  const pointIsolatedChunk = removePointLightBlock(
    THREE.ShaderChunk.lights_fragment_begin,
  );
  if (fragmentShader.includes(LIGHTS_FRAGMENT_INCLUDE))
    return fragmentShader.replace(LIGHTS_FRAGMENT_INCLUDE, pointIsolatedChunk);
  return removePointLightBlock(fragmentShader);
}

function removePointLightBlock(fragmentShader: string): string {
  const start = fragmentShader.indexOf(POINT_LIGHTS_START);
  const end = fragmentShader.indexOf(POINT_LIGHTS_END, start + POINT_LIGHTS_START.length);
  if (start < 0 || end < 0)
    throw new Error('MeshStandardMaterial shader no longer exposes the point-light block.');
  return `${fragmentShader.slice(0, start)}// Nilbots: transient point lights disabled for this neutral fixture.\n${fragmentShader.slice(end)}`;
}
