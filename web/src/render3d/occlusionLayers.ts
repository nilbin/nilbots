import * as THREE from 'three';

/** A depth-tested, colourless actor silhouette written before persistent constructs. */
export const ACTOR_OCCLUSION_MASK_RENDER_ORDER = 0.5;
/** Persistent mine/sentry solids sit above terrain and immediately below actor solids. */
export const PERSISTENT_CONSTRUCT_RENDER_ORDER = 1;
/** Walkable objective hardware uses the same actor-first overlap grammar. */
export const WALKABLE_OBJECTIVE_RENDER_ORDER = 1;
/** Ordinary actor solids finish the three-layer overlap grammar. */
export const BOT_BODY_RENDER_ORDER = 2;

const ACTOR_OCCLUSION_STENCIL_REF = 1;

/** Clone one actor material into the invisible stencil pre-pass. */
export function createActorOcclusionMaskMaterial(
  source: THREE.Material,
): THREE.Material {
  const material = source.clone();
  material.name = `${source.name || source.type}-actor-occlusion-mask`;
  material.colorWrite = false;
  material.depthTest = true;
  material.depthWrite = false;
  // The mask must stay in the ordered opaque pass even when the actor itself is fading.
  // Alpha-test textures are retained by the clone, so hard silhouette cut-outs remain.
  material.transparent = false;
  material.opacity = 1;
  material.stencilWrite = true;
  material.stencilWriteMask = 0xff;
  material.stencilFunc = THREE.AlwaysStencilFunc;
  material.stencilRef = ACTOR_OCCLUSION_STENCIL_REF;
  material.stencilFuncMask = 0xff;
  material.stencilFail = THREE.KeepStencilOp;
  material.stencilZFail = THREE.KeepStencilOp;
  material.stencilZPass = THREE.ReplaceStencilOp;
  material.needsUpdate = true;
  return material;
}

/** Make a mine/sentry keep normal depth while declining pixels masked by visible actors. */
export function configurePersistentConstructRendering(
  model: THREE.Object3D,
): void {
  configureActorOccludedRendering(model, PERSISTENT_CONSTRUCT_RENDER_ORDER);
}

/**
 * Make a floor fixture yield to a bot silhouette without disabling ordinary depth.
 *
 * Wells and Reactors are intentionally walkable. Their thin physical rims still need to
 * look solid against the floor, but never as if they pass through a hovering bot that is
 * mechanically allowed to cross them.
 */
export function configureWalkableObjectiveRendering(
  model: THREE.Object3D,
): void {
  configureActorOccludedRendering(model, WALKABLE_OBJECTIVE_RENDER_ORDER);
}

function configureActorOccludedRendering(
  model: THREE.Object3D,
  renderOrder: number,
): void {
  model.traverse((node) => {
    const mesh = node as THREE.Mesh;
    if (!mesh.isMesh) return;
    mesh.renderOrder = renderOrder;
    for (const material of Array.isArray(mesh.material)
      ? mesh.material
      : [mesh.material]) {
      material.depthTest = true;
      material.stencilWrite = true;
      // Enable the stencil test without changing the bot silhouette already in it.
      material.stencilWriteMask = 0x00;
      material.stencilFunc = THREE.NotEqualStencilFunc;
      material.stencilRef = ACTOR_OCCLUSION_STENCIL_REF;
      material.stencilFuncMask = 0xff;
      material.stencilFail = THREE.KeepStencilOp;
      material.stencilZFail = THREE.KeepStencilOp;
      material.stencilZPass = THREE.KeepStencilOp;
    }
  });
}
