import * as THREE from 'three';
import { faceStatusElement } from './statusBib';

/** One persistent cell per authoritative veterancy level. */
export const VETERANCY_RANK_CELL_COUNT = 5;
export const VETERANCY_RANK_RAIL_MAX_LEVEL = 5;
export const VETERANCY_RANK_RAIL_WIDTH = 0.54;
export const VETERANCY_RANK_RAIL_HEIGHT = 0.18;

const CELL_WIDTH = 0.086;
const CELL_HEIGHT = 0.078;
const CELL_GAP = 0.012;
const RAIL_WIDTH = 0.52;
const RAIL_HEIGHT = 0.108;
const RAIL_Y = 0.105;

type Point = readonly [x: number, y: number];

export interface VeterancyRankRailRig {
  group: THREE.Group;
  cells: readonly THREE.Group[];
  cellEnergy: readonly THREE.Mesh[];
  teamRim: THREE.Mesh;
  keylineMaterial: THREE.MeshBasicMaterial;
  cellMetalMaterial: THREE.MeshBasicMaterial;
  faceMaterial: THREE.MeshBasicMaterial;
  dormantCellMaterial: THREE.MeshBasicMaterial;
  teamMaterial: THREE.MeshBasicMaterial;
  disposables: Array<THREE.Material | THREE.BufferGeometry>;
}

function shapeGeometry(points: readonly Point[]): THREE.ShapeGeometry {
  const shape = new THREE.Shape();
  const [first, ...rest] = points;
  if (!first) throw new Error('a veterancy rank rail needs an outline');
  shape.moveTo(first[0], first[1]);
  for (const [x, y] of rest) shape.lineTo(x, y);
  shape.closePath();
  return new THREE.ShapeGeometry(shape);
}

function chamferedRectangle(
  width: number,
  height: number,
  chamfer: number,
): THREE.ShapeGeometry {
  const halfWidth = width / 2;
  const halfHeight = height / 2;
  return shapeGeometry([
    [-halfWidth + chamfer, halfHeight],
    [halfWidth - chamfer, halfHeight],
    [halfWidth, halfHeight - chamfer],
    [halfWidth, -halfHeight + chamfer],
    [halfWidth - chamfer, -halfHeight],
    [-halfWidth + chamfer, -halfHeight],
    [-halfWidth, -halfHeight + chamfer],
    [-halfWidth, halfHeight - chamfer],
  ]);
}

/** Create one fixed-footprint, team-colored, camera-facing five-cell rank rail. */
export function createVeterancyRankRail(
  teamAccent: THREE.ColorRepresentation,
): VeterancyRankRailRig {
  const group = new THREE.Group();
  group.userData.cue = 'veterancy-rank-rail';
  group.userData.width = VETERANCY_RANK_RAIL_WIDTH;
  group.userData.height = VETERANCY_RANK_RAIL_HEIGHT;
  faceStatusElement(group);
  group.visible = false;

  const keylineMaterial = new THREE.MeshBasicMaterial({
    color: '#010609',
    transparent: true,
    opacity: 0.98,
    depthWrite: false,
    depthTest: false,
  });
  const cellMetalMaterial = new THREE.MeshBasicMaterial({
    color: '#d8e8e8',
    transparent: true,
    depthWrite: false,
    depthTest: false,
  });
  const faceMaterial = new THREE.MeshBasicMaterial({
    color: '#07131c',
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
  const teamMaterial = new THREE.MeshBasicMaterial({
    color: teamAccent,
    transparent: true,
    depthWrite: false,
    depthTest: false,
  });
  group.userData.teamColor = `#${teamMaterial.color.getHexString()}`;

  // One unchanging outer silhouette makes the rail quiet and immediately learnable.
  // Its rim and energized cells repeat the health frame's authoritative team accent;
  // exact power remains the count, so color never has two competing meanings.
  const railGeometry = chamferedRectangle(RAIL_WIDTH, RAIL_HEIGHT, 0.018);
  const railKeyline = new THREE.Mesh(railGeometry, keylineMaterial);
  railKeyline.userData.cue = 'veterancy-rank-rail-keyline';
  railKeyline.position.set(0, RAIL_Y, 0);
  railKeyline.scale.set(1.08, 1.14, 1);
  railKeyline.renderOrder = 10;
  const teamRim = new THREE.Mesh(railGeometry, teamMaterial);
  teamRim.userData.cue = 'veterancy-rank-team-rim';
  teamRim.position.set(0, RAIL_Y, 0.003);
  teamRim.renderOrder = 11;
  const railFace = new THREE.Mesh(railGeometry, faceMaterial);
  railFace.userData.cue = 'veterancy-rank-rail-face';
  railFace.position.set(0, RAIL_Y, 0.006);
  railFace.scale.set(0.94, 0.82, 1);
  railFace.renderOrder = 12;
  group.add(railKeyline, teamRim, railFace);

  const cellGeometry = chamferedRectangle(CELL_WIDTH, CELL_HEIGHT, 0.012);
  const cellEnergy: THREE.Mesh[] = [];
  const cells = Array.from({ length: VETERANCY_RANK_CELL_COUNT }, (_, index) => {
    const cell = new THREE.Group();
    cell.userData.cue = 'veterancy-rank-cell';
    cell.userData.cellIndex = index + 1;
    cell.position.set(
      (index - (VETERANCY_RANK_CELL_COUNT - 1) / 2) * (CELL_WIDTH + CELL_GAP),
      RAIL_Y,
      0,
    );
    // A nested Group resets Three's inherited groupOrder. Preserve the status row's
    // order so the rail face cannot sort over and hide its own cells in WebGL.
    cell.renderOrder = group.renderOrder;

    const keyline = new THREE.Mesh(cellGeometry, keylineMaterial);
    keyline.userData.cue = 'veterancy-rank-cell-keyline';
    keyline.scale.setScalar(1.16);
    keyline.position.z = 0.009;
    keyline.renderOrder = 13;
    const metal = new THREE.Mesh(cellGeometry, cellMetalMaterial);
    metal.userData.cue = 'veterancy-rank-cell-metal';
    metal.position.z = 0.012;
    metal.renderOrder = 14;
    const dormant = new THREE.Mesh(cellGeometry, dormantCellMaterial);
    dormant.userData.cue = 'veterancy-rank-cell-dormant';
    dormant.scale.setScalar(0.72);
    dormant.position.z = 0.015;
    dormant.renderOrder = 15;
    const energy = new THREE.Mesh(cellGeometry, teamMaterial);
    energy.userData.cue = 'veterancy-rank-cell-energy';
    energy.scale.setScalar(0.72);
    energy.position.z = 0.018;
    energy.renderOrder = 16;
    energy.visible = false;
    cell.add(keyline, metal, dormant, energy);
    cellEnergy.push(energy);
    group.add(cell);
    return cell;
  });

  return {
    group,
    cells,
    cellEnergy,
    teamRim,
    keylineMaterial,
    cellMetalMaterial,
    faceMaterial,
    dormantCellMaterial,
    teamMaterial,
    disposables: [
      railGeometry,
      cellGeometry,
      keylineMaterial,
      cellMetalMaterial,
      faceMaterial,
      dormantCellMaterial,
      teamMaterial,
    ],
  };
}

/** Keep shape and team color fixed, energizing exactly one cell per level. */
export function paintVeterancyRankRail(
  rig: VeterancyRankRailRig,
  level: number,
  visible: boolean,
): void {
  const rank = Math.max(1, Math.floor(level));
  const visualRank = Math.min(VETERANCY_RANK_RAIL_MAX_LEVEL, rank);
  rig.group.visible = visible;
  rig.group.userData.rankValue = rank;
  rig.group.userData.visualRankValue = visualRank;
  for (const [index, energy] of rig.cellEnergy.entries()) {
    energy.visible = visible && index < visualRank;
    rig.cells[index]!.userData.energized = energy.visible;
  }
}
