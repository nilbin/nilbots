import * as THREE from 'three';
import { CAMERA_PITCH } from './arenaScene';

/**
 * One world-space grammar for compact status attached to a body or construct.
 *
 * These values used to live only in `arenaActors`, while signature constructs
 * independently chose larger spheres, a model-relative height, and model-relative
 * scaling. Keeping the geometry here makes "has health" one visual statement no
 * matter whether the thing with health is a bot, a Trip Node, or a Sentinel.
 */
export const STATUS_BIB_HEIGHT = 0.72;
/** Low-profile constructs sit closer to the floor, so their bib follows them down. */
export const CONSTRUCT_STATUS_BIB_HEIGHT = 0.64;
export const STATUS_BIB_SETBACK = 0.55;
export const STATUS_PIP_RADIUS = 0.06;
export const STATUS_PIP_SPACING = 0.17;
export const STATUS_BIB_RENDER_ORDER = 10;

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
