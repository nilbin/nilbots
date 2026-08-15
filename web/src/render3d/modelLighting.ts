import * as THREE from 'three';

const OUTGOING_LIGHT =
  'vec3 outgoingLight = totalDiffuse + totalSpecular + totalEmissiveRadiance;';
const PROFILE_KEY = 'nilbots-model-lighting-v4';
const APEX_OUTLINE_PROFILE_KEY = 'nilbots-apex-outline-v1';
const POINT_LIGHT_ISOLATION_KEY = 'nilbots-point-light-isolation-v1';
const LIGHTS_FRAGMENT_INCLUDE = '#include <lights_fragment_begin>';
const BEGIN_VERTEX_INCLUDE = '#include <begin_vertex>';
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

interface ApexOutlineState {
  width: number;
  widthUniform: { value: number } | null;
}

export interface ModelApexOutline {
  readonly material: THREE.MeshBasicMaterial;
  attach: (source: THREE.Mesh) => THREE.Mesh | null;
  set: (strength: number, phase: number, visibility: number) => void;
  dispose: () => void;
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
  apexRimStrength: 0.035,
  apexRimPower: 3,
  apexSweepStrength: 0.14,
  apexSweepPower: 30,
  apexOutlineWidth: 0.045,
  apexOutlineOpacity: 0.9,
} as const;

const APEX_OUTLINE_LOW = new THREE.Color(0x7650c7);
const APEX_OUTLINE_HIGH = new THREE.Color(0xa982ef);

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

/**
 * Build one actor-owned outline material and attach it to any genuine static GLB meshes.
 *
 * The back-face shell expands along authored vertex normals, so it remains a crisp visible
 * silhouette outside the model instead of bleaching the texture to make an internal
 * Fresnel edge survive minification. One material is shared by every shell on the actor;
 * geometry remains shared with the source model and no extra asset bytes are introduced.
 */
export function createModelApexOutline(): ModelApexOutline {
  const material = new THREE.MeshBasicMaterial({
    color: APEX_OUTLINE_LOW,
    transparent: true,
    opacity: 0,
    side: THREE.BackSide,
    depthWrite: false,
    depthTest: true,
    toneMapped: false,
  });
  const beforeCompile = material.onBeforeCompile.bind(material);
  const beforeCacheKey = material.customProgramCacheKey.bind(material);
  const state: ApexOutlineState = { width: 0, widthUniform: null };
  const shells = new Set<THREE.Mesh>();

  material.onBeforeCompile = (shader, renderer) => {
    beforeCompile(shader, renderer);
    state.widthUniform = { value: state.width };
    shader.uniforms.nilbotsApexOutlineWidth = state.widthUniform;
    if (!shader.vertexShader.includes(BEGIN_VERTEX_INCLUDE))
      throw new Error('MeshBasicMaterial shader no longer exposes the Apex outline anchor.');
    shader.vertexShader = `uniform float nilbotsApexOutlineWidth;\n${shader.vertexShader.replace(
      BEGIN_VERTEX_INCLUDE,
      `${BEGIN_VERTEX_INCLUDE}\n\ttransformed += normalize( normal ) * nilbotsApexOutlineWidth;`,
    )}`;
  };
  material.customProgramCacheKey = () =>
    `${beforeCacheKey()}|${APEX_OUTLINE_PROFILE_KEY}`;
  material.userData.nilbotsApexOutline = APEX_OUTLINE_PROFILE_KEY;
  material.userData.nilbotsApexOutlineState = state;
  material.needsUpdate = true;

  return {
    material,
    attach: (source) => {
      // Meshy bots are deliberately unrigged. Fail soft if a future look introduces skinning
      // rather than leaving a rigid outline behind while the authored body deforms.
      if ((source as THREE.SkinnedMesh).isSkinnedMesh) return null;
      const shell = new THREE.Mesh(source.geometry, material);
      shell.name = `${source.name || 'mesh'}-apex-outline`;
      shell.userData.kind = 'apex-outline';
      shell.visible = false;
      shell.castShadow = false;
      shell.receiveShadow = false;
      shell.renderOrder = source.renderOrder + 0.25;
      source.add(shell);
      shells.add(shell);
      return shell;
    },
    set: (strength, phase, visibility) => {
      const amount = THREE.MathUtils.clamp(strength, 0, 1);
      const visibleAmount = amount * THREE.MathUtils.clamp(visibility, 0, 1);
      const pulse = 0.5 + 0.5 * Math.sin(phase * 1.7);
      state.width = MODEL_LIGHTING_PROFILE.apexOutlineWidth
        * amount
        * (0.9 + pulse * 0.1);
      if (state.widthUniform) state.widthUniform.value = state.width;
      material.opacity = MODEL_LIGHTING_PROFILE.apexOutlineOpacity
        * visibleAmount
        * (0.86 + pulse * 0.14);
      material.color.copy(APEX_OUTLINE_LOW).lerp(
        APEX_OUTLINE_HIGH,
        0.28 + pulse * 0.34,
      );
      for (const shell of shells) shell.visible = material.opacity > 0.01;
    },
    dispose: () => material.dispose(),
  };
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

  // Apex surface scan: the separate back-face shell owns zoomed-out readability, so this
  // term can stay narrow and coloured instead of lifting the entire texture toward white.
  float nilbotsApexRim = pow(
    1.0 - nilbotsModelFacing,
    ${MODEL_LIGHTING_PROFILE.apexRimPower.toFixed(1)}
  ) * ${MODEL_LIGHTING_PROFILE.apexRimStrength.toFixed(2)};
  vec3 nilbotsApexSweepDirection = normalize( vec3(
    cos( nilbotsApexPhase ) * 0.58,
    sin( nilbotsApexPhase ) * 0.5,
    1.0
  ) );
  float nilbotsApexSweep = pow(
    saturate( dot( geometryNormal, nilbotsApexSweepDirection ) ),
    ${MODEL_LIGHTING_PROFILE.apexSweepPower.toFixed(1)}
  ) * ${MODEL_LIGHTING_PROFILE.apexSweepStrength.toFixed(2)};
  vec3 nilbotsApexViolet = mix(
    vec3( 0.38, 0.12, 0.72 ),
    material.diffuseColor,
    0.58
  );
  outgoingLight += nilbotsApexViolet
    * ( nilbotsApexRim + nilbotsApexSweep )
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
