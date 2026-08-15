import * as THREE from 'three';
import { mergeGeometries } from 'three/examples/jsm/utils/BufferGeometryUtils.js';
import { ornamentModel } from './lookModel';
import { BOT_BODY_RENDER_ORDER } from './occlusionLayers';

const MAX_RANK = 5;
const CORE_CLEARANCE_RADIUS = 0.26;
const DIADEM_BASE_HEIGHT = 0.37;
const DIADEM_REAR_OFFSET = 0.48;
const DIADEM_DEPTH_SCALE = 1.08;
const DIADEM_VERTICAL_SCALE = 1.3;
const MODEL_IDS = [
  'arc-veterancy-diadem-rank-1',
  'arc-veterancy-diadem-rank-2',
  'arc-veterancy-diadem-rank-3',
  'arc-veterancy-diadem-rank-4',
  'arc-veterancy-diadem-rank-5',
] as const;

interface RankPresentation {
  container: THREE.Group;
  fallback: THREE.Mesh;
  model: THREE.Group | null;
  loading: Promise<void> | null;
}

export interface VeterancyDiademRig {
  group: THREE.Group;
  setRank: (rank: number, visible: boolean) => void;
  fade: (factor: number) => void;
  dispose: () => void;
}

let fallbackGeometries: readonly THREE.BufferGeometry[] | null = null;

/**
 * Build the shared Arc Relay veterancy ornament.
 *
 * Each level is one complete Meshy-authored bilateral model. The renderer only chooses
 * the rank; it does not reconstruct the approved antler silhouette from repeated parts.
 * The group is parented directly to the chassis so it follows authoritative yaw without
 * inheriting recoil, hover bob, signature lean, or any Core animation state.
 */
export function createVeterancyDiadem(
  unitKey: string,
  size: number,
): VeterancyDiademRig {
  const group = new THREE.Group();
  group.userData.cue = 'veterancy-split-diadem';
  group.userData.forUnitKey = unitKey;
  group.userData.rankValue = 1;
  group.userData.featherCount = 0;
  group.userData.coreClearanceRadius = CORE_CLEARANCE_RADIUS;
  group.userData.yawSource = 'chassis';
  group.userData.worldUp = true;
  group.userData.modelSource = 'procedural-fallback';
  group.userData.modelArchitecture = 'complete-rank-model';
  group.userData.rankModelId = MODEL_IDS[0];
  group.userData.gameplayReadabilityScale = {
    depth: DIADEM_DEPTH_SCALE,
    vertical: DIADEM_VERTICAL_SCALE,
    lateral: 1,
  };
  group.position.set(-DIADEM_REAR_OFFSET * size, DIADEM_BASE_HEIGHT, 0);
  group.scale.set(
    size * DIADEM_DEPTH_SCALE,
    size * DIADEM_VERTICAL_SCALE,
    size,
  );
  group.visible = false;

  const fallbackMaterial = ornamentMaterial();
  const ownedModelMaterials = new Set<THREE.Material>();
  const ranks = sharedFallbackGeometries().map((geometry, index): RankPresentation => {
    const rank = index + 1;
    const container = new THREE.Group();
    container.userData.cue = 'veterancy-rank-container';
    container.userData.rankValue = rank;
    container.visible = false;
    const fallback = new THREE.Mesh(geometry, fallbackMaterial);
    fallback.userData.cue = 'veterancy-rank-model';
    fallback.userData.rankValue = rank;
    fallback.userData.featherCount = (rank - 1) * 2;
    fallback.userData.modelSource = 'procedural-fallback';
    fallback.castShadow = true;
    fallback.receiveShadow = true;
    fallback.renderOrder = BOT_BODY_RENDER_ORDER;
    container.add(fallback);
    group.add(container);
    return { container, fallback, model: null, loading: null };
  });

  let live = true;
  let currentRank = 1;
  let currentVisible = false;
  let currentFade = 1;

  const paintRank = () => {
    group.visible = currentVisible;
    group.userData.rankValue = currentRank;
    group.userData.featherCount = (currentRank - 1) * 2;
    group.userData.rankModelId = MODEL_IDS[currentRank - 1];
    const selected = ranks[currentRank - 1]!;
    group.userData.modelSource = selected.model ? 'gltf' : 'procedural-fallback';
    for (const [index, presentation] of ranks.entries()) {
      const selectedRank = currentVisible && index + 1 === currentRank;
      presentation.container.visible = selectedRank;
      presentation.fallback.visible = selectedRank && !presentation.model;
      if (presentation.model) presentation.model.visible = selectedRank;
    }
  };

  const ensureRank = (rank: number) => {
    const presentation = ranks[rank - 1]!;
    if (presentation.model || presentation.loading) return;
    presentation.loading = ornamentModel(MODEL_IDS[rank - 1]).then((model) => {
      if (!live || !model) return;
      model.userData.cue = 'veterancy-rank-model';
      model.userData.rankValue = rank;
      model.userData.featherCount = (rank - 1) * 2;
      model.userData.modelSource = 'gltf';
      model.userData.modelId = MODEL_IDS[rank - 1];
      model.traverse((node) => {
        const mesh = node as THREE.Mesh;
        if (!mesh.isMesh) return;
        mesh.userData.cue = 'veterancy-rank-model';
        mesh.userData.rankValue = rank;
        mesh.userData.featherCount = (rank - 1) * 2;
        mesh.userData.modelSource = 'gltf';
        mesh.castShadow = true;
        mesh.receiveShadow = true;
        mesh.renderOrder = BOT_BODY_RENDER_ORDER;
        const materials = Array.isArray(mesh.material)
          ? mesh.material
          : [mesh.material];
        for (const material of materials) {
          ownedModelMaterials.add(material);
          prepareMaterial(material);
          applyFade(material, currentFade);
        }
      });
      presentation.model = model;
      presentation.container.add(model);
      paintRank();
    });
  };

  return {
    group,
    setRank: (rank, visible) => {
      currentRank = Math.max(1, Math.min(MAX_RANK, Math.floor(rank)));
      currentVisible = visible;
      if (visible) ensureRank(currentRank);
      paintRank();
    },
    fade: (factor) => {
      currentFade = Math.max(0, Math.min(1, factor));
      applyFade(fallbackMaterial, currentFade);
      for (const material of ownedModelMaterials) applyFade(material, currentFade);
    },
    dispose: () => {
      live = false;
      fallbackMaterial.dispose();
      for (const material of ownedModelMaterials) material.dispose();
    },
  };
}

function ornamentMaterial(): THREE.MeshStandardMaterial {
  return new THREE.MeshStandardMaterial({
    color: '#b58a49',
    emissive: '#765021',
    emissiveIntensity: 0.32,
    roughness: 0.78,
    metalness: 0.5,
    side: THREE.DoubleSide,
  });
}

function prepareMaterial(material: THREE.Material): void {
  if (!(material instanceof THREE.MeshStandardMaterial)) return;
  material.side = THREE.DoubleSide;
  material.roughness = Math.max(0.78, material.roughness);
  material.metalness = Math.min(0.62, material.metalness);
  // Provider emission maps are almost black outside hairline channels. That is sound for
  // an isolated turntable, but the complete ornament disappeared against the arena at
  // phone/overview scale. A restrained whole-surface warm contribution keeps the matte
  // brass readable without adding a team-coloured light or glossy toy finish.
  material.emissiveMap = null;
  material.emissive.set('#765021');
  material.emissiveIntensity = Math.max(0.32, material.emissiveIntensity);
  material.needsUpdate = true;
}

function applyFade(material: THREE.Material, factor: number): void {
  const transparent = factor < 1;
  if (material.transparent !== transparent) material.needsUpdate = true;
  material.opacity = factor;
  material.transparent = transparent;
}

/** Cheap immediate silhouettes while a lazily requested rank GLB resolves or degrades. */
function sharedFallbackGeometries(): readonly THREE.BufferGeometry[] {
  if (fallbackGeometries) return fallbackGeometries;
  fallbackGeometries = Array.from({ length: MAX_RANK }, (_, index) => {
    const rank = index + 1;
    const parts: THREE.BufferGeometry[] = [];
    for (const side of [-1, 1]) {
      const curve = new THREE.CatmullRomCurve3([
        new THREE.Vector3(-0.1, 0, side * 0.145),
        new THREE.Vector3(-0.04, 0.012, side * 0.22),
        new THREE.Vector3(0.02, 0.022, side * (0.31 + index * 0.012)),
      ]);
      parts.push(new THREE.TubeGeometry(curve, 10, 0.018, 5, false));
      for (let blade = 0; blade < rank - 1; blade += 1) {
        const height = 0.29 - blade * 0.047 + index * 0.012;
        const geometry = new THREE.BoxGeometry(0.028, height, 0.065);
        const placement = new THREE.Object3D();
        placement.position.set(
          0,
          0.05 + height / 2,
          side * (0.215 + blade * 0.07),
        );
        placement.rotation.x = side * (0.12 + blade * 0.12);
        placement.updateMatrix();
        geometry.applyMatrix4(placement.matrix);
        parts.push(geometry);
      }
    }
    const merged = mergeGeometries(parts, false);
    for (const part of parts) part.dispose();
    if (!merged) throw new Error(`Could not build rank ${rank} diadem fallback.`);
    merged.computeBoundingSphere();
    return merged;
  });
  return fallbackGeometries;
}
