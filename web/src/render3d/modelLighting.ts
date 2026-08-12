import * as THREE from 'three';

const OUTGOING_LIGHT =
  'vec3 outgoingLight = totalDiffuse + totalSpecular + totalEmissiveRadiance;';
const PROFILE_KEY = 'nilbots-model-lighting-v1';

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
