import * as THREE from 'three';
import { faceStatusElement } from './statusBib';

/** One exact rank cell per authoritative veterancy level. */
export const VETERANCY_RANK_CELL_COUNT = 5;
export const VETERANCY_POWER_FRAME_MAX_LEVEL = 5;
/** Maximum authored frame footprint, including the dark clearance keyline. */
export const VETERANCY_POWER_FRAME_WIDTH = 1;
export const VETERANCY_POWER_FRAME_HEIGHT = 0.44;

const CELL_WIDTH = 0.086;
const CELL_HEIGHT = 0.078;
const CELL_GAP = 0.012;
const CELL_Y = 0.105;

type Point = readonly [x: number, y: number];

interface SeamSpec {
  readonly x: number;
  readonly y: number;
  readonly width: number;
  readonly height: number;
  readonly rotation?: number;
}

interface PowerFrameSpec {
  readonly id: 'veteran' | 'stepped' | 'shouldered' | 'command' | 'apex';
  readonly outline: readonly Point[];
  readonly seams: readonly SeamSpec[];
}

/**
 * Five camera-facing command silhouettes inferred from the v3 concept. Exact rank is
 * carried independently by the cells; these outlines are free to communicate danger.
 */
const POWER_FRAMES: readonly PowerFrameSpec[] = [
  {
    id: 'veteran',
    outline: [
      [-0.28, -0.075], [0.28, -0.075], [0.32, -0.035], [0.32, 0.135],
      [0.285, 0.17], [-0.285, 0.17], [-0.32, 0.135], [-0.32, -0.035],
    ],
    seams: [],
  },
  {
    id: 'stepped',
    outline: [
      [-0.3, -0.075], [0.3, -0.075], [0.35, -0.025], [0.35, 0.15],
      [0.31, 0.205], [0.25, 0.205], [0.225, 0.17], [-0.225, 0.17],
      [-0.25, 0.205], [-0.31, 0.205], [-0.35, 0.15], [-0.35, -0.025],
    ],
    seams: [
      { x: -0.323, y: 0.045, width: 0.014, height: 0.075 },
      { x: 0.323, y: 0.045, width: 0.014, height: 0.075 },
    ],
  },
  {
    id: 'shouldered',
    outline: [
      [-0.33, -0.075], [0.33, -0.075], [0.38, -0.025], [0.38, 0.17],
      [0.34, 0.22], [0.22, 0.22], [0.17, 0.245], [-0.17, 0.245],
      [-0.22, 0.22], [-0.34, 0.22], [-0.38, 0.17], [-0.38, -0.025],
    ],
    seams: [
      { x: -0.35, y: 0.055, width: 0.014, height: 0.11 },
      { x: 0.35, y: 0.055, width: 0.014, height: 0.11 },
      { x: 0, y: 0.216, width: 0.075, height: 0.014 },
    ],
  },
  {
    id: 'command',
    outline: [
      [-0.36, -0.075], [0.36, -0.075], [0.42, -0.02], [0.42, 0.22],
      [0.38, 0.285], [0.315, 0.285], [0.285, 0.235], [0.215, 0.235],
      [0.18, 0.205], [-0.18, 0.205], [-0.215, 0.235], [-0.285, 0.235],
      [-0.315, 0.285], [-0.38, 0.285], [-0.42, 0.22], [-0.42, -0.02],
    ],
    seams: [
      { x: -0.39, y: 0.07, width: 0.015, height: 0.15 },
      { x: 0.39, y: 0.07, width: 0.015, height: 0.15 },
      { x: -0.31, y: 0.25, width: 0.055, height: 0.014, rotation: -0.45 },
      { x: 0.31, y: 0.25, width: 0.055, height: 0.014, rotation: 0.45 },
    ],
  },
  {
    id: 'apex',
    outline: [
      [-0.39, -0.075], [0.39, -0.075], [0.46, -0.015], [0.46, 0.235],
      [0.415, 0.305], [0.35, 0.305], [0.315, 0.26], [0.265, 0.29],
      [0.215, 0.245], [0.15, 0.265], [0.095, 0.33], [-0.095, 0.33],
      [-0.15, 0.265], [-0.215, 0.245], [-0.265, 0.29], [-0.315, 0.26],
      [-0.35, 0.305], [-0.415, 0.305], [-0.46, 0.235], [-0.46, -0.015],
    ],
    seams: [
      { x: -0.43, y: 0.075, width: 0.016, height: 0.17 },
      { x: 0.43, y: 0.075, width: 0.016, height: 0.17 },
      { x: -0.295, y: 0.267, width: 0.06, height: 0.014, rotation: -0.48 },
      { x: 0.295, y: 0.267, width: 0.06, height: 0.014, rotation: 0.48 },
      { x: 0, y: 0.282, width: 0.018, height: 0.075 },
    ],
  },
];

export interface VeterancyPowerFrameRig {
  group: THREE.Group;
  states: readonly THREE.Group[];
  cells: readonly THREE.Group[];
  cellEnergy: readonly THREE.Mesh[];
  keylineMaterial: THREE.MeshBasicMaterial;
  metalMaterial: THREE.MeshBasicMaterial;
  faceMaterial: THREE.MeshBasicMaterial;
  dormantCellMaterial: THREE.MeshBasicMaterial;
  energyMaterial: THREE.MeshBasicMaterial;
  disposables: Array<THREE.Material | THREE.BufferGeometry>;
}

function shapeGeometry(points: readonly Point[]): THREE.ShapeGeometry {
  const shape = new THREE.Shape();
  const [first, ...rest] = points;
  if (!first) throw new Error('a veterancy power frame needs an outline');
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

/** Create the fixed five-cell rank rail and its five progressively stronger frames. */
export function createVeterancyPowerFrame(): VeterancyPowerFrameRig {
  const group = new THREE.Group();
  group.userData.cue = 'veterancy-power-frame';
  group.userData.width = VETERANCY_POWER_FRAME_WIDTH;
  group.userData.height = VETERANCY_POWER_FRAME_HEIGHT;
  faceStatusElement(group);
  group.visible = false;

  // Concept brass is intentionally not copied into the arena. Unlit pale metal stays
  // visible over Tapline's copper-black floor; amber is reserved for rank and power.
  const keylineMaterial = new THREE.MeshBasicMaterial({
    color: '#010609',
    transparent: true,
    opacity: 0.98,
    depthWrite: false,
    depthTest: false,
  });
  const metalMaterial = new THREE.MeshBasicMaterial({
    color: '#e1f2f0',
    transparent: true,
    depthWrite: false,
    depthTest: false,
  });
  const faceMaterial = new THREE.MeshBasicMaterial({
    color: '#061119',
    transparent: true,
    opacity: 0.98,
    depthWrite: false,
    depthTest: false,
  });
  const dormantCellMaterial = new THREE.MeshBasicMaterial({
    color: '#172832',
    transparent: true,
    opacity: 0.98,
    depthWrite: false,
    depthTest: false,
  });
  const energyMaterial = new THREE.MeshBasicMaterial({
    color: '#ffc247',
    transparent: true,
    depthWrite: false,
    depthTest: false,
  });
  const seamGeometry = new THREE.PlaneGeometry(1, 1);
  const geometries: THREE.BufferGeometry[] = [seamGeometry];

  const states = POWER_FRAMES.map((spec, index) => {
    const rank = index + 1;
    const state = new THREE.Group();
    state.userData.cue = 'veterancy-power-frame-state';
    state.userData.rankValue = rank;
    state.userData.designId = spec.id;
    const bounds = outlineBounds(spec.outline);
    state.userData.silhouetteWidth = bounds.width;
    state.userData.silhouetteHeight = bounds.height;
    state.userData.energySeamCount = spec.seams.length;
    state.renderOrder = group.renderOrder;
    state.visible = false;

    const geometry = shapeGeometry(spec.outline);
    geometries.push(geometry);
    const keyline = new THREE.Mesh(geometry, keylineMaterial);
    keyline.userData.cue = 'veterancy-frame-keyline';
    keyline.scale.setScalar(1.08);
    keyline.position.set(0, -0.004, 0);
    keyline.renderOrder = 7;
    const metal = new THREE.Mesh(geometry, metalMaterial);
    metal.userData.cue = 'veterancy-frame-metal';
    metal.position.z = 0.002;
    metal.renderOrder = 8;
    const face = new THREE.Mesh(geometry, faceMaterial);
    face.userData.cue = 'veterancy-frame-face';
    face.scale.setScalar(0.9);
    face.position.z = 0.004;
    face.renderOrder = 9;
    state.add(keyline, metal, face);

    for (const seam of spec.seams) {
      const mark = new THREE.Mesh(seamGeometry, energyMaterial);
      mark.userData.cue = 'veterancy-frame-energy';
      mark.position.set(seam.x, seam.y, 0.007);
      mark.rotation.z = seam.rotation ?? 0;
      mark.scale.set(seam.width, seam.height, 1);
      mark.renderOrder = 9;
      state.add(mark);
    }
    group.add(state);
    return state;
  });

  const halfCellWidth = CELL_WIDTH / 2;
  const halfCellHeight = CELL_HEIGHT / 2;
  const chamfer = 0.012;
  const cellGeometry = shapeGeometry([
    [-halfCellWidth + chamfer, halfCellHeight],
    [halfCellWidth - chamfer, halfCellHeight],
    [halfCellWidth, halfCellHeight - chamfer],
    [halfCellWidth, -halfCellHeight + chamfer],
    [halfCellWidth - chamfer, -halfCellHeight],
    [-halfCellWidth + chamfer, -halfCellHeight],
    [-halfCellWidth, -halfCellHeight + chamfer],
    [-halfCellWidth, halfCellHeight - chamfer],
  ]);
  geometries.push(cellGeometry);
  const cellEnergy: THREE.Mesh[] = [];
  const cells = Array.from({ length: VETERANCY_RANK_CELL_COUNT }, (_, index) => {
    const cell = new THREE.Group();
    cell.userData.cue = 'veterancy-rank-cell';
    cell.userData.cellIndex = index + 1;
    cell.position.set(
      (index - (VETERANCY_RANK_CELL_COUNT - 1) / 2) * (CELL_WIDTH + CELL_GAP),
      CELL_Y,
      0,
    );
    cell.renderOrder = group.renderOrder;

    const keyline = new THREE.Mesh(cellGeometry, keylineMaterial);
    keyline.userData.cue = 'veterancy-rank-cell-keyline';
    keyline.scale.setScalar(1.18);
    keyline.renderOrder = 11;
    const metal = new THREE.Mesh(cellGeometry, metalMaterial);
    metal.userData.cue = 'veterancy-rank-cell-metal';
    metal.position.z = 0.009;
    metal.renderOrder = 12;
    const dormant = new THREE.Mesh(cellGeometry, dormantCellMaterial);
    dormant.userData.cue = 'veterancy-rank-cell-dormant';
    dormant.scale.setScalar(0.72);
    dormant.position.z = 0.012;
    dormant.renderOrder = 13;
    const energy = new THREE.Mesh(cellGeometry, energyMaterial);
    energy.userData.cue = 'veterancy-rank-cell-energy';
    energy.scale.setScalar(0.72);
    energy.position.z = 0.015;
    energy.renderOrder = 14;
    energy.visible = false;
    cell.add(keyline, metal, dormant, energy);
    cellEnergy.push(energy);
    group.add(cell);
    return cell;
  });

  return {
    group,
    states,
    cells,
    cellEnergy,
    keylineMaterial,
    metalMaterial,
    faceMaterial,
    dormantCellMaterial,
    energyMaterial,
    disposables: [
      ...geometries,
      keylineMaterial,
      metalMaterial,
      faceMaterial,
      dormantCellMaterial,
      energyMaterial,
    ],
  };
}

/** Show one truthful power silhouette and energize exactly one cell per level. */
export function paintVeterancyPowerFrame(
  rig: VeterancyPowerFrameRig,
  level: number,
  visible: boolean,
): void {
  const rank = Math.max(1, Math.floor(level));
  const visualRank = Math.min(VETERANCY_POWER_FRAME_MAX_LEVEL, rank);
  rig.group.visible = visible;
  rig.group.userData.rankValue = rank;
  rig.group.userData.visualRankValue = visualRank;
  for (const state of rig.states)
    state.visible = visible && state.userData.rankValue === visualRank;
  for (const [index, energy] of rig.cellEnergy.entries()) {
    energy.visible = visible && index < visualRank;
    rig.cells[index]!.userData.energized = energy.visible;
  }
}
