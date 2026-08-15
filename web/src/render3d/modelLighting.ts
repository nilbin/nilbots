import * as THREE from 'three';

const OUTGOING_LIGHT =
  'vec3 outgoingLight = totalDiffuse + totalSpecular + totalEmissiveRadiance;';
const PROFILE_KEY = 'nilbots-model-lighting-v5';
const TEAM_OUTLINE_PROFILE_KEY = 'nilbots-team-outline-v1';
const POINT_LIGHT_ISOLATION_KEY = 'nilbots-point-light-isolation-v1';
const LIGHTS_FRAGMENT_INCLUDE = '#include <lights_fragment_begin>';
const BEGIN_VERTEX_INCLUDE = '#include <begin_vertex>';
const POINT_LIGHTS_START =
  '#if ( NUM_POINT_LIGHTS > 0 ) && defined( RE_Direct )';
const POINT_LIGHTS_END =
  '#if ( NUM_SPOT_LIGHTS > 0 ) && defined( RE_Direct )';
const APEX_UNIFORMS = `uniform float nilbotsApexStrength;
uniform float nilbotsApexPhase;
uniform vec3 nilbotsApexColor;`;

interface ModelLightingState {
  apexStrength: number;
  apexPhase: number;
  apexColor: THREE.Color;
  strengthUniform: { value: number } | null;
  phaseUniform: { value: number } | null;
  colorUniform: { value: THREE.Color } | null;
}

interface OutlineMaterialState {
  width: number;
  widthUniform: { value: number } | null;
}

export interface ModelTeamOutlineShells {
  readonly core: THREE.Mesh;
  readonly radiance: THREE.Mesh;
}

export interface ModelTeamOutline {
  readonly material: THREE.MeshBasicMaterial;
  readonly radianceMaterial: THREE.MeshBasicMaterial;
  attach: (source: THREE.Mesh) => ModelTeamOutlineShells | null;
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
  apexOutlineWidth: 0.05,
  apexOutlineOpacity: 0.88,
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
  const state: ModelLightingState = {
    apexStrength: 0,
    apexPhase: 0,
    apexColor: new THREE.Color(0xffffff),
    strengthUniform: null,
    phaseUniform: null,
    colorUniform: null,
  };

  material.onBeforeCompile = (shader, renderer) => {
    beforeCompile(shader, renderer);
    state.strengthUniform = { value: state.apexStrength };
    state.phaseUniform = { value: state.apexPhase };
    state.colorUniform = { value: state.apexColor.clone() };
    shader.uniforms.nilbotsApexStrength = state.strengthUniform;
    shader.uniforms.nilbotsApexPhase = state.phaseUniform;
    shader.uniforms.nilbotsApexColor = state.colorUniform;
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
  color: THREE.Color,
): void {
  const state = material.userData.nilbotsModelLightingState as
    | ModelLightingState
    | undefined;
  if (!state) return;
  state.apexStrength = THREE.MathUtils.clamp(strength, 0, 1);
  state.apexPhase = phase;
  state.apexColor.copy(color);
  if (state.strengthUniform)
    state.strengthUniform.value = state.apexStrength;
  if (state.phaseUniform) state.phaseUniform.value = state.apexPhase;
  if (state.colorUniform) state.colorUniform.value.copy(state.apexColor);
}

/**
 * Build one actor-owned team-coloured Apex radiance and attach it to genuine GLBs.
 *
 * Both back-face shells expand along authored vertex normals. Level five reveals a crisp
 * normal-blended team silhouette and a wider additive shell which breathes outside it.
 * Lower levels show neither shell. Materials are shared per actor and both shells reuse
 * source geometry, so no model or texture bytes are copied.
 */
export function createModelTeamOutline(color: THREE.Color): ModelTeamOutline {
  const material = createOutlineMaterial(color, 'core', THREE.NormalBlending);
  const radianceMaterial = createOutlineMaterial(
    color,
    'radiance',
    THREE.AdditiveBlending,
  );
  const materialState = material.userData.nilbotsTeamOutlineState as
    OutlineMaterialState;
  const radianceState = radianceMaterial.userData.nilbotsTeamOutlineState as
    OutlineMaterialState;
  const coreShells = new Set<THREE.Mesh>();
  const radianceShells = new Set<THREE.Mesh>();

  return {
    material,
    radianceMaterial,
    attach: (source) => {
      // Meshy bots are deliberately unrigged. Fail soft if a future look introduces skinning
      // rather than leaving rigid shells behind while the authored body deforms.
      if ((source as THREE.SkinnedMesh).isSkinnedMesh) return null;
      const radiance = outlineShell(
        source,
        radianceMaterial,
        'team-radiance',
        0.2,
      );
      const core = outlineShell(
        source,
        material,
        'team-outline',
        0.25,
      );
      radiance.visible = false;
      core.visible = false;
      source.add(radiance, core);
      radianceShells.add(radiance);
      coreShells.add(core);
      return { core, radiance };
    },
    set: (strength, phase, visibility) => {
      const amount = THREE.MathUtils.clamp(strength, 0, 1);
      const visibleAmount = THREE.MathUtils.clamp(visibility, 0, 1);
      const pulse = 0.5 + 0.5 * Math.sin(phase * 1.7);
      materialState.width = MODEL_LIGHTING_PROFILE.apexOutlineWidth
        * (0.94 + pulse * 0.06)
        * amount;
      radianceState.width = THREE.MathUtils.lerp(
        MODEL_LIGHTING_PROFILE.apexRadianceWidthMinimum,
        MODEL_LIGHTING_PROFILE.apexRadianceWidthMaximum,
        pulse,
      ) * amount;
      if (materialState.widthUniform)
        materialState.widthUniform.value = materialState.width;
      if (radianceState.widthUniform)
        radianceState.widthUniform.value = radianceState.width;

      material.opacity = MODEL_LIGHTING_PROFILE.apexOutlineOpacity
        * (0.9 + pulse * 0.1)
        * amount
        * visibleAmount;
      radianceMaterial.opacity = THREE.MathUtils.lerp(
        MODEL_LIGHTING_PROFILE.apexRadianceOpacityMinimum,
        MODEL_LIGHTING_PROFILE.apexRadianceOpacityMaximum,
        pulse,
      ) * amount * visibleAmount;
      for (const shell of coreShells) shell.visible = material.opacity > 0.01;
      for (const shell of radianceShells)
        shell.visible = radianceMaterial.opacity > 0.01;
    },
    dispose: () => {
      material.dispose();
      radianceMaterial.dispose();
    },
  };
}

function createOutlineMaterial(
  color: THREE.Color,
  kind: 'core' | 'radiance',
  blending: THREE.Blending,
): THREE.MeshBasicMaterial {
  const material = new THREE.MeshBasicMaterial({
    color,
    transparent: true,
    opacity: 0,
    side: THREE.BackSide,
    depthWrite: false,
    depthTest: true,
    toneMapped: false,
    blending,
  });
  const beforeCompile = material.onBeforeCompile.bind(material);
  const beforeCacheKey = material.customProgramCacheKey.bind(material);
  const state: OutlineMaterialState = { width: 0, widthUniform: null };

  material.onBeforeCompile = (shader, renderer) => {
    beforeCompile(shader, renderer);
    state.widthUniform = { value: state.width };
    shader.uniforms.nilbotsTeamOutlineWidth = state.widthUniform;
    if (!shader.vertexShader.includes(BEGIN_VERTEX_INCLUDE))
      throw new Error('MeshBasicMaterial shader no longer exposes the team-outline anchor.');
    shader.vertexShader = `uniform float nilbotsTeamOutlineWidth;\n${shader.vertexShader.replace(
      BEGIN_VERTEX_INCLUDE,
      `${BEGIN_VERTEX_INCLUDE}\n\ttransformed += normalize( normal ) * nilbotsTeamOutlineWidth;`,
    )}`;
  };
  material.customProgramCacheKey = () =>
    `${beforeCacheKey()}|${TEAM_OUTLINE_PROFILE_KEY}-${kind}`;
  material.userData.nilbotsTeamOutline = TEAM_OUTLINE_PROFILE_KEY;
  material.userData.nilbotsTeamOutlineState = state;
  material.needsUpdate = true;
  return material;
}

function outlineShell(
  source: THREE.Mesh,
  material: THREE.MeshBasicMaterial,
  kind: 'team-outline' | 'team-radiance',
  renderOffset: number,
): THREE.Mesh {
  const shell = new THREE.Mesh(source.geometry, material);
  shell.name = `${source.name || 'mesh'}-${kind}`;
  shell.userData.kind = kind;
  shell.castShadow = false;
  shell.receiveShadow = false;
  shell.renderOrder = source.renderOrder + renderOffset;
  return shell;
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
  vec3 nilbotsApexTint = mix(
    nilbotsApexColor,
    material.diffuseColor,
    0.58
  );
  outgoingLight += nilbotsApexTint
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
