import * as THREE from 'three';
import { faceStatusElement } from './statusBib';

/**
 * One reserved footprint beside health. The visible crest grows inside this
 * allocation, so gaining a level never makes the health bar jump sideways.
 */
export const VETERANCY_PLATE_WIDTH = 0.305;
export const VETERANCY_PLATE_HEIGHT = 0.24;
export const VETERANCY_PLATE_GAP = 0.045;
export const VETERANCY_PLATE_MAX_RANK = 5;

type Point = readonly [x: number, y: number];

interface InlaySpec {
  readonly x: number;
  readonly y: number;
  readonly width: number;
  readonly height: number;
  readonly rotation?: number;
}

interface CrestSpec {
  readonly id: 'seed' | 'kindled' | 'proven' | 'elite' | 'apex';
  readonly outline: readonly Point[];
  readonly inlays: readonly InlaySpec[];
}

/**
 * Five silhouettes inferred from the owner-approved 2D board. They are drawn
 * as actual flat vector geometry rather than shipping the generated raster.
 * Every successive rank adds planform, height, and controlled energy detail.
 */
const CRESTS: readonly CrestSpec[] = [
  {
    id: 'seed',
    outline: [
      [-0.072, 0.058], [0.072, 0.058], [0.09, 0.03], [0.09, -0.03],
      [0.072, -0.058], [-0.072, -0.058], [-0.09, -0.03], [-0.09, 0.03],
    ],
    inlays: [],
  },
  {
    id: 'kindled',
    outline: [
      [-0.08, 0.07], [0.08, 0.07], [0.105, 0.05], [0.11, 0.015],
      [0.11, -0.04], [0.08, -0.07], [-0.08, -0.07], [-0.11, -0.04],
      [-0.11, 0.015], [-0.105, 0.05],
    ],
    inlays: [
      { x: 0, y: -0.004, width: 0.058, height: 0.014 },
    ],
  },
  {
    id: 'proven',
    outline: [
      [-0.12, -0.075], [-0.125, -0.005], [-0.098, 0.055],
      [-0.062, 0.052], [-0.046, 0.09], [0.046, 0.09],
      [0.062, 0.052], [0.098, 0.055], [0.125, -0.005], [0.12, -0.075],
    ],
    inlays: [
      { x: -0.058, y: -0.006, width: 0.014, height: 0.052, rotation: -0.5 },
      { x: 0.058, y: -0.006, width: 0.014, height: 0.052, rotation: 0.5 },
    ],
  },
  {
    id: 'elite',
    outline: [
      [-0.126, -0.082], [-0.13, 0.005], [-0.106, 0.09],
      [-0.072, 0.075], [-0.06, 0.105], [-0.03, 0.1],
      [-0.018, 0.03], [0.018, 0.03], [0.03, 0.1], [0.06, 0.105],
      [0.072, 0.075], [0.106, 0.09], [0.13, 0.005], [0.126, -0.082],
    ],
    inlays: [
      { x: -0.062, y: 0.003, width: 0.014, height: 0.068, rotation: -0.34 },
      { x: 0.062, y: 0.003, width: 0.014, height: 0.068, rotation: 0.34 },
      { x: 0, y: -0.048, width: 0.044, height: 0.012 },
    ],
  },
  {
    id: 'apex',
    outline: [
      [-0.13, -0.09], [-0.133, 0], [-0.113, 0.084],
      [-0.086, 0.065], [-0.075, 0.105], [-0.049, 0.088],
      [-0.033, 0.118], [0, 0.088], [0.033, 0.118], [0.049, 0.088],
      [0.075, 0.105], [0.086, 0.065], [0.113, 0.084],
      [0.133, 0], [0.13, -0.09],
    ],
    inlays: [
      { x: -0.104, y: -0.004, width: 0.011, height: 0.044, rotation: -0.3 },
      { x: -0.058, y: 0.006, width: 0.013, height: 0.066, rotation: -0.46 },
      { x: 0, y: 0.027, width: 0.014, height: 0.078 },
      { x: 0.058, y: 0.006, width: 0.013, height: 0.066, rotation: 0.46 },
      { x: 0.104, y: -0.004, width: 0.011, height: 0.044, rotation: 0.3 },
    ],
  },
];

export interface VeterancyPlateRig {
  group: THREE.Group;
  ranks: readonly THREE.Group[];
  shadowMaterial: THREE.MeshBasicMaterial;
  rimMaterial: THREE.MeshBasicMaterial;
  faceMaterial: THREE.MeshBasicMaterial;
  energyMaterial: THREE.MeshBasicMaterial;
  disposables: Array<THREE.Material | THREE.BufferGeometry>;
}

function crestGeometry(points: readonly Point[]): THREE.ShapeGeometry {
  const shape = new THREE.Shape();
  const [first, ...rest] = points;
  if (!first) throw new Error('a veterancy crest needs an outline');
  shape.moveTo(first[0], first[1]);
  for (const [x, y] of rest) shape.lineTo(x, y);
  shape.closePath();
  return new THREE.ShapeGeometry(shape);
}

function outlineBounds(points: readonly Point[]): {
  width: number;
  height: number;
} {
  const xs = points.map(([x]) => x);
  const ys = points.map(([, y]) => y);
  return {
    width: Math.max(...xs) - Math.min(...xs),
    height: Math.max(...ys) - Math.min(...ys),
  };
}

/** Create one camera-facing, fixed-slot veterancy plate with five rank states. */
export function createVeterancyPlate(): VeterancyPlateRig {
  const group = new THREE.Group();
  group.userData.cue = 'veterancy-rank-plate';
  group.userData.width = VETERANCY_PLATE_WIDTH;
  group.userData.height = VETERANCY_PLATE_HEIGHT;
  faceStatusElement(group);
  group.visible = false;

  // The oversized near-black keyline separates the plate from bright floor detail,
  // while a cool-metal rim stays legible over Tapline's dark copper surfaces. Amber
  // is reserved for rank energy, so the plate cannot be mistaken for scenery trim.
  const shadowMaterial = new THREE.MeshBasicMaterial({
    color: '#01060a',
    transparent: true,
    opacity: 0.94,
    depthWrite: false,
    depthTest: false,
  });
  const rimMaterial = new THREE.MeshBasicMaterial({
    color: '#cfe8ea',
    transparent: true,
    depthWrite: false,
    depthTest: false,
  });
  const faceMaterial = new THREE.MeshBasicMaterial({
    color: '#07131c',
    transparent: true,
    opacity: 0.97,
    depthWrite: false,
    depthTest: false,
  });
  const energyMaterial = new THREE.MeshBasicMaterial({
    color: '#ffd36a',
    transparent: true,
    opacity: 0.96,
    depthWrite: false,
    depthTest: false,
  });
  const inlayGeometry = new THREE.PlaneGeometry(1, 1);
  const geometries: THREE.BufferGeometry[] = [inlayGeometry];

  const ranks = CRESTS.map((spec, index) => {
    const rank = index + 1;
    const rankGroup = new THREE.Group();
    rankGroup.userData.cue = 'veterancy-rank-crest';
    rankGroup.userData.rankValue = rank;
    rankGroup.userData.designId = spec.id;
    const bounds = outlineBounds(spec.outline);
    rankGroup.userData.silhouetteWidth = bounds.width;
    rankGroup.userData.silhouetteHeight = bounds.height;
    rankGroup.userData.energyMarkCount = spec.inlays.length;
    rankGroup.renderOrder = group.renderOrder;
    rankGroup.visible = false;

    const geometry = crestGeometry(spec.outline);
    geometries.push(geometry);
    const shadow = new THREE.Mesh(geometry, shadowMaterial);
    shadow.userData.cue = 'veterancy-rank-shadow';
    shadow.scale.setScalar(1.12);
    shadow.position.set(0, -0.007, 0);
    shadow.renderOrder = 9;
    const rim = new THREE.Mesh(geometry, rimMaterial);
    rim.userData.cue = 'veterancy-rank-rim';
    rim.position.z = 0.002;
    rim.renderOrder = 10;
    const face = new THREE.Mesh(geometry, faceMaterial);
    face.userData.cue = 'veterancy-rank-face';
    face.scale.setScalar(0.8);
    face.position.z = 0.004;
    face.renderOrder = 11;
    rankGroup.add(shadow, rim, face);

    for (const inlay of spec.inlays) {
      const mark = new THREE.Mesh(inlayGeometry, energyMaterial);
      mark.userData.cue = 'veterancy-rank-inlay';
      mark.position.set(inlay.x, inlay.y, 0.007);
      mark.rotation.z = inlay.rotation ?? 0;
      mark.scale.set(inlay.width, inlay.height, 1);
      mark.renderOrder = 12;
      rankGroup.add(mark);
    }
    group.add(rankGroup);
    return rankGroup;
  });

  return {
    group,
    ranks,
    shadowMaterial,
    rimMaterial,
    faceMaterial,
    energyMaterial,
    disposables: [
      ...geometries,
      shadowMaterial,
      rimMaterial,
      faceMaterial,
      energyMaterial,
    ],
  };
}

/** Show exactly one truthful rank state; ranks above the authored five use Apex. */
export function paintVeterancyPlate(
  rig: VeterancyPlateRig,
  level: number,
  visible: boolean,
): void {
  const rank = Math.max(1, Math.floor(level));
  const visualRank = Math.min(VETERANCY_PLATE_MAX_RANK, rank);
  rig.group.visible = visible;
  rig.group.userData.rankValue = rank;
  rig.group.userData.visualRankValue = visualRank;
  for (const crest of rig.ranks)
    crest.visible = visible && crest.userData.rankValue === visualRank;
}
