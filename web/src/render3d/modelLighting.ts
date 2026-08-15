import * as THREE from 'three';

const OUTGOING_LIGHT =
  'vec3 outgoingLight = totalDiffuse + totalSpecular + totalEmissiveRadiance;';
const PROFILE_KEY = 'nilbots-model-lighting-v6';
const APEX_RADIANCE_PROFILE_KEY = 'nilbots-apex-radiance-v1';
const POINT_LIGHT_ISOLATION_KEY = 'nilbots-point-light-isolation-v1';
const LIGHTS_FRAGMENT_INCLUDE = '#include <lights_fragment_begin>';
const BEGIN_VERTEX_INCLUDE = '#include <begin_vertex>';
const POINT_LIGHTS_START =
  '#if ( NUM_POINT_LIGHTS > 0 ) && defined( RE_Direct )';
const POINT_LIGHTS_END =
  '#if ( NUM_SPOT_LIGHTS > 0 ) && defined( RE_Direct )';
interface RadianceMaterialState {
  width: number;
  widthUniform: { value: number } | null;
}

export interface ModelApexRadiance {
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
  apexRadianceWidthMinimum: 0.072,
  apexRadianceWidthMaximum: 0.096,
  apexRadianceOpacityMinimum: 0.12,
  apexRadianceOpacityMaximum: 0.27,
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

/**
 * Build one actor-owned, team-coloured Apex aura and attach it to genuine GLBs.
 *
 * One low-opacity additive back-face shell expands along authored vertex normals and
 * breathes outside the body. There is no crisp contour shell and no shader treatment on
 * the authored surface. The actor shares one material across source geometry, so the aura
 * introduces no model or texture bytes.
 */
export function createModelApexRadiance(color: THREE.Color): ModelApexRadiance {
  const material = createRadianceMaterial(color);
  const state = material.userData.nilbotsApexRadianceState as
    RadianceMaterialState;
  const shells = new Set<THREE.Mesh>();

  return {
    material,
    attach: (source) => {
      // Meshy bots are deliberately unrigged. Fail soft if a future look introduces skinning
      // rather than leaving a rigid aura behind while the authored body deforms.
      if ((source as THREE.SkinnedMesh).isSkinnedMesh) return null;
      const shell = radianceShell(source, material);
      shell.visible = false;
      source.add(shell);
      shells.add(shell);
      return shell;
    },
    set: (strength, phase, visibility) => {
      const amount = THREE.MathUtils.clamp(strength, 0, 1);
      const visibleAmount = THREE.MathUtils.clamp(visibility, 0, 1);
      const pulse = 0.5 + 0.5 * Math.sin(phase * 1.7);
      state.width = THREE.MathUtils.lerp(
        MODEL_LIGHTING_PROFILE.apexRadianceWidthMinimum,
        MODEL_LIGHTING_PROFILE.apexRadianceWidthMaximum,
        pulse,
      ) * amount;
      if (state.widthUniform) state.widthUniform.value = state.width;
      material.opacity = THREE.MathUtils.lerp(
        MODEL_LIGHTING_PROFILE.apexRadianceOpacityMinimum,
        MODEL_LIGHTING_PROFILE.apexRadianceOpacityMaximum,
        pulse,
      ) * amount * visibleAmount;
      for (const shell of shells) shell.visible = material.opacity > 0.01;
    },
    dispose: () => material.dispose(),
  };
}

function createRadianceMaterial(
  color: THREE.Color,
): THREE.MeshBasicMaterial {
  const material = new THREE.MeshBasicMaterial({
    color,
    transparent: true,
    opacity: 0,
    side: THREE.BackSide,
    depthWrite: false,
    depthTest: true,
    toneMapped: false,
    blending: THREE.AdditiveBlending,
  });
  const beforeCompile = material.onBeforeCompile.bind(material);
  const beforeCacheKey = material.customProgramCacheKey.bind(material);
  const state: RadianceMaterialState = { width: 0, widthUniform: null };

  material.onBeforeCompile = (shader, renderer) => {
    beforeCompile(shader, renderer);
    state.widthUniform = { value: state.width };
    shader.uniforms.nilbotsApexRadianceWidth = state.widthUniform;
    if (!shader.vertexShader.includes(BEGIN_VERTEX_INCLUDE))
      throw new Error('MeshBasicMaterial shader no longer exposes the Apex-radiance anchor.');
    shader.vertexShader = `uniform float nilbotsApexRadianceWidth;\n${shader.vertexShader.replace(
      BEGIN_VERTEX_INCLUDE,
      `${BEGIN_VERTEX_INCLUDE}\n\ttransformed += normalize( normal ) * nilbotsApexRadianceWidth;`,
    )}`;
  };
  material.customProgramCacheKey = () =>
    `${beforeCacheKey()}|${APEX_RADIANCE_PROFILE_KEY}`;
  material.userData.nilbotsApexRadiance = APEX_RADIANCE_PROFILE_KEY;
  material.userData.nilbotsApexRadianceState = state;
  material.needsUpdate = true;
  return material;
}

function radianceShell(
  source: THREE.Mesh,
  material: THREE.MeshBasicMaterial,
): THREE.Mesh {
  const shell = new THREE.Mesh(source.geometry, material);
  shell.name = `${source.name || 'mesh'}-apex-radiance`;
  shell.userData.kind = 'apex-radiance';
  shell.castShadow = false;
  shell.receiveShadow = false;
  shell.renderOrder = source.renderOrder + 0.2;
  return shell;
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
