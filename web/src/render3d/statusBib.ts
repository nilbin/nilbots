import * as THREE from 'three';
import { CAMERA_PITCH } from './arenaScene';
import {
  HEALTH_BAR_RELATIVE_WIDTH,
  healthBarState,
} from '../presentation/healthBar';

/**
 * One world-space grammar for compact status attached to a body or construct.
 *
 * These values used to live only in `arenaActors`, while signature constructs
 * independently chose larger spheres, a model-relative height, and model-relative
 * scaling. Keeping the geometry here makes "has health" one visual statement no
 * matter whether the thing with health is a bot, a Trip Node, or a Sentinel.
 */
export const STATUS_BIB_HEIGHT = 0.72;
export const STATUS_BIB_SETBACK = 0.55;
export const STATUS_PIP_RADIUS = 0.06;
export const STATUS_PIP_SPACING = 0.17;
export const STATUS_BIB_RENDER_ORDER = 10;
/** Health and cooldown are one fixed-width stack; only their fill changes. */
export const STATUS_TRACK_WIDTH = HEALTH_BAR_RELATIVE_WIDTH;
export const STATUS_HEALTH_BAR_HEIGHT = 0.072;
const STATUS_HEALTH_BAR_INNER_WIDTH = 0.49;
const STATUS_HEALTH_BAR_INNER_HEIGHT = 0.048;

export interface StatusHealthBarRig {
  group: THREE.Group;
  fill: THREE.Mesh;
  fillMaterial: THREE.MeshBasicMaterial;
  frameMaterial: THREE.MeshBasicMaterial;
  trackMaterial: THREE.MeshBasicMaterial;
  disposables: Array<THREE.Material | THREE.BufferGeometry>;
}

/**
 * Fixed-width health bar with a restrained team-colour rim. Health itself uses
 * the universal green/amber/red language so the same wound state reads across teams.
 */
export function createStatusHealthBar(
  teamAccent: THREE.ColorRepresentation,
): StatusHealthBarRig {
  const group = new THREE.Group();
  group.userData.cue = 'health-bar-track';
  group.userData.width = STATUS_TRACK_WIDTH;
  group.userData.height = STATUS_HEALTH_BAR_HEIGHT;

  const frameGeometry = new THREE.PlaneGeometry(
    STATUS_TRACK_WIDTH,
    STATUS_HEALTH_BAR_HEIGHT,
  );
  const innerGeometry = new THREE.PlaneGeometry(
    STATUS_HEALTH_BAR_INNER_WIDTH,
    STATUS_HEALTH_BAR_INNER_HEIGHT,
  );
  const frameMaterial = new THREE.MeshBasicMaterial({
    color: teamAccent,
    transparent: true,
    opacity: 0.9,
    depthWrite: false,
    depthTest: false,
  });
  const trackMaterial = new THREE.MeshBasicMaterial({
    color: '#071017',
    transparent: true,
    opacity: 0.9,
    depthWrite: false,
    depthTest: false,
  });
  const fillMaterial = new THREE.MeshBasicMaterial({
    color: '#4ade80',
    transparent: true,
    opacity: 0.98,
    depthWrite: false,
    depthTest: false,
  });
  const frame = new THREE.Mesh(frameGeometry, frameMaterial);
  frame.userData.cue = 'health-bar-frame';
  const track = new THREE.Mesh(innerGeometry, trackMaterial);
  track.userData.cue = 'health-bar-backing';
  track.position.z = 0.002;
  const fill = new THREE.Mesh(innerGeometry, fillMaterial);
  fill.userData.cue = 'health-bar-fill';
  fill.position.z = 0.004;
  for (const element of [frame, track, fill]) faceStatusElement(element);
  group.add(frame, track, fill);
  return {
    group,
    fill,
    fillMaterial,
    frameMaterial,
    trackMaterial,
    disposables: [
      frameGeometry,
      innerGeometry,
      frameMaterial,
      trackMaterial,
      fillMaterial,
    ],
  };
}

/** Paint a left-anchored proportional fill without changing the bar's footprint. */
export function paintStatusHealthBar(
  rig: StatusHealthBarRig,
  currentHealth: number,
  maximumHealth: number,
): void {
  const state = healthBarState(currentHealth, maximumHealth);
  rig.fillMaterial.color.set(state.color);
  rig.fill.visible = state.fraction > 0;
  rig.fill.scale.x = Math.max(state.fraction, 0.001);
  rig.fill.position.x = -STATUS_HEALTH_BAR_INNER_WIDTH * (1 - state.fraction) / 2;
  rig.group.userData.currentHealth = state.current;
  rig.group.userData.maximumHealth = state.maximum;
  rig.group.userData.remainingFraction = state.fraction;
  rig.group.userData.healthTone = state.tone;
}

export function createStatusPipGeometry(): THREE.CircleGeometry {
  return new THREE.CircleGeometry(STATUS_PIP_RADIUS, 12);
}

export function createStatusPipMaterial(
  color: THREE.ColorRepresentation,
  lost = false,
): THREE.MeshBasicMaterial {
  return new THREE.MeshBasicMaterial({
    color,
    transparent: true,
    opacity: lost ? 0.35 : 1,
    depthWrite: false,
    // Status is information, not scenery. It remains legible where the raised camera
    // projects it over the thing it belongs to.
    depthTest: false,
  });
}

/** Square one flat status element to the viewer's baseline camera. */
export function faceStatusElement(element: THREE.Object3D): void {
  element.rotation.x = -CAMERA_PITCH;
  element.renderOrder = STATUS_BIB_RENDER_ORDER;
}

/** Centre the first `count` pips and hide any allocation retained for other forms. */
export function centreStatusPips(
  pips: readonly THREE.Mesh[],
  count: number,
): void {
  for (const [index, pip] of pips.entries()) {
    pip.visible = index < count;
    if (pip.visible)
      pip.position.x = (index - (count - 1) / 2) * STATUS_PIP_SPACING;
  }
}

/** Place a status bib independently of its owner's body/model transform. */
export function placeStatusBib(
  bib: THREE.Object3D,
  worldX: number,
  worldZ: number,
  worldY = STATUS_BIB_HEIGHT,
): void {
  bib.position.set(
    worldX,
    worldY,
    worldZ - STATUS_BIB_SETBACK,
  );
}

/**
 * A map theme may bias the otherwise fixed camera. Rotate the whole status stack by
 * that difference; its children retain the baseline pitch authored above.
 */
export function orientStatusBib(
  bib: THREE.Object3D,
  cameraYaw: number,
  cameraPitch: number,
): void {
  bib.rotation.order = 'YXZ';
  bib.rotation.y = cameraYaw;
  bib.rotation.x = CAMERA_PITCH - cameraPitch;
}
