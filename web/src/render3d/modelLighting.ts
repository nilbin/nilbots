import * as THREE from 'three';

const OUTGOING_LIGHT =
  'vec3 outgoingLight = totalDiffuse + totalSpecular + totalEmissiveRadiance;';
const PROFILE_KEY = 'nilbots-model-lighting-v3';
const POINT_LIGHT_ISOLATION_KEY = 'nilbots-point-light-isolation-v1';
const LIGHTS_FRAGMENT_INCLUDE = '#include <lights_fragment_begin>';
const POINT_LIGHTS_START =
  '#if ( NUM_POINT_LIGHTS > 0 ) && defined( RE_Direct )';
const POINT_LIGHTS_END =
  '#if ( NUM_SPOT_LIGHTS > 0 ) && defined( RE_Direct )';
const APEX_UNIFORMS = `uniform float nilbotsApexStrength;
uniform float nilbotsApexPhase;`;

interface ModelLightingState {
  apexStrength: number;
  apexPhase: number;
  strengthUniform: { value: number } | null;
  phaseUniform: { value: number } | null;
}

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
  apexCoatStrength: 0.3,
  apexRimStrength: 0.48,
  apexRimPower: 1.3,
  apexSweepStrength: 0.58,
  apexSweepPower: 5.5,
  apexCounterSweepStrength: 0.38,
  apexCounterSweepPower: 8,
} as const;

export function applyModelLighting(
  material: THREE.MeshStandardMaterial,
): void {
  if (material.userData.nilbotsModelLighting === PROFILE_KEY) return;
  const beforeCompile = material.onBeforeCompile.bind(material);
  const beforeCacheKey = material.customProgramCacheKey.bind(material);
  const state: ModelLightingState = {
    apexStrength: 0,
    apexPhase: 0,
    strengthUniform: null,
    phaseUniform: null,
  };

  material.onBeforeCompile = (shader, renderer) => {
    beforeCompile(shader, renderer);
    state.strengthUniform = { value: state.apexStrength };
    state.phaseUniform = { value: state.apexPhase };
    shader.uniforms.nilbotsApexStrength = state.strengthUniform;
    shader.uniforms.nilbotsApexPhase = state.phaseUniform;
    shader.fragmentShader = injectModelLighting(shader.fragmentShader);
  };
  material.customProgramCacheKey = () =>
    `${beforeCacheKey()}|${PROFILE_KEY}`;
  material.userData.nilbotsModelLighting = PROFILE_KEY;
  material.userData.nilbotsModelLightingState = state;
  material.needsUpdate = true;
}

/**
 * Drive the level-five treatment on one actor-owned material.
 *
 * The uniforms modify only this material's final light. No Three light is created, so the
 * treatment cannot touch the floor, nearby actors, fog, Wells, or Reactors. Calling this
 * before the material first compiles is supported: the state is retained and copied into
 * the shader uniforms when Three builds its program.
 */
export function setModelApexTreatment(
  material: THREE.MeshStandardMaterial,
  strength: number,
  phase: number,
): void {
  const state = material.userData.nilbotsModelLightingState as
    | ModelLightingState
    | undefined;
  if (!state) return;
  state.apexStrength = THREE.MathUtils.clamp(strength, 0, 1);
  state.apexPhase = phase;
  if (state.strengthUniform)
    state.strengthUniform.value = state.apexStrength;
  if (state.phaseUniform) state.phaseUniform.value = state.apexPhase;
}

export function injectModelLighting(fragmentShader: string): string {
  if (!fragmentShader.includes(OUTGOING_LIGHT))
    throw new Error('MeshStandardMaterial shader no longer exposes the model-lighting anchor.');
  return `${APEX_UNIFORMS}
${fragmentShader.replace(
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
  ) * nilbotsModelRim;

  // Apex phase coat: a persistent platinum lift, broad Fresnel edge, and two
  // counter-crossing light bands. The broad terms survive phone-scale minification;
  // authored diffuse colour remains in every term instead of being replaced by white.
  float nilbotsApexBreath = 0.90 + 0.10 * sin( nilbotsApexPhase * 0.55 );
  float nilbotsApexCoat =
    ${MODEL_LIGHTING_PROFILE.apexCoatStrength.toFixed(2)} * nilbotsApexBreath;
  float nilbotsApexRim = pow(
    1.0 - nilbotsModelFacing,
    ${MODEL_LIGHTING_PROFILE.apexRimPower.toFixed(1)}
  ) * ${MODEL_LIGHTING_PROFILE.apexRimStrength.toFixed(2)};
  float nilbotsApexCos = cos( nilbotsApexPhase );
  float nilbotsApexSin = sin( nilbotsApexPhase );
  vec3 nilbotsApexSweepDirection = normalize( vec3(
    nilbotsApexCos * 0.72,
    nilbotsApexSin * 0.64,
    0.82
  ) );
  float nilbotsApexSweep = pow(
    saturate( dot( geometryNormal, nilbotsApexSweepDirection ) ),
    ${MODEL_LIGHTING_PROFILE.apexSweepPower.toFixed(1)}
  ) * ${MODEL_LIGHTING_PROFILE.apexSweepStrength.toFixed(2)};
  vec3 nilbotsApexCounterDirection = normalize( vec3(
    -nilbotsApexSin * 0.66,
    nilbotsApexCos * 0.72,
    0.56
  ) );
  float nilbotsApexCounterSweep = pow(
    saturate( dot( geometryNormal, nilbotsApexCounterDirection ) ),
    ${MODEL_LIGHTING_PROFILE.apexCounterSweepPower.toFixed(1)}
  ) * ${MODEL_LIGHTING_PROFILE.apexCounterSweepStrength.toFixed(2)};
  vec3 nilbotsApexPlatinum = mix(
    vec3( 0.72, 0.76, 0.84 ),
    material.diffuseColor,
    0.34
  );
  outgoingLight += nilbotsApexPlatinum
    * (
      nilbotsApexCoat
      + nilbotsApexRim
      + nilbotsApexSweep
      + nilbotsApexCounterSweep
    )
    * nilbotsApexStrength;`,
  )}`;
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
