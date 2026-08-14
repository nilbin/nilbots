import * as THREE from 'three';
import { defineArcSignatureWebGLPainter } from '../types';
import {
  hasConstructRemoval,
  risingSettle,
  setEnergyColour,
  setEnergyOpacity,
} from '../primitives';
import type { ArcSignatureEffectRig } from '../types';

const HARDLIGHT_HEIGHT = 0.88;
const HARDLIGHT_HALF_WIDTH = 0.43;
const HARDLIGHT_FRAME_WIDTH = 0.84;
const HARDLIGHT_PANEL_WIDTH = 0.72;
const HARDLIGHT_FRAME = new THREE.Color('#4f5d60');
const HARDLIGHT_ENERGY = new THREE.Color('#87d6d8');

const SIDE_FACES = [
  { x: 0, z: -1, yaw: 0 },
  { x: 1, z: 0, yaw: Math.PI / 2 },
  { x: 0, z: 1, yaw: 0 },
  { x: -1, z: 0, yaw: Math.PI / 2 },
] as const;

function placeEdge(
  mesh: THREE.Mesh,
  point: THREE.Vector3,
  fromX: number,
  fromZ: number,
  toX: number,
  toZ: number,
  height: number,
  footprintScale: number,
): void {
  const dx = (toX - fromX) * footprintScale;
  const dz = (toZ - fromZ) * footprintScale;
  mesh.visible = true;
  mesh.position.set(
    point.x + ((fromX + toX) / 2) * footprintScale,
    height,
    point.z + ((fromZ + toZ) / 2) * footprintScale,
  );
  mesh.rotation.y = -Math.atan2(dz, dx);
  mesh.scale.set(Math.hypot(dx, dz), 1, 0.82);
}

function paintBarrier(
  rig: ArcSignatureEffectRig,
  point: THREE.Vector3,
  envelopeHeight: number,
  faceHeight: number,
  remainingCapacity: number,
  energyOpacity: number,
  frameOpacity: number,
  footprintScale = 1,
): void {
  const baseY = 0.045;
  const halfWidth = HARDLIGHT_HALF_WIDTH * footprintScale;
  const panelHeight = Math.max(0.045, faceHeight - 0.1);

  // Four Palisade-like framed faces make one grid-aligned solid-energy block. The slim
  // posts and full-height edge cage are visible immediately, preserving the honest
  // collision envelope while the translucent faces rise inside it.
  for (const [index, face] of SIDE_FACES.entries()) {
    const frame = rig.frames[index]!;
    frame.visible = true;
    frame.position.set(
      point.x + face.x * halfWidth,
      baseY + faceHeight / 2,
      point.z + face.z * halfWidth,
    );
    frame.rotation.set(0, face.yaw, 0);
    frame.scale.set(
      (HARDLIGHT_FRAME_WIDTH * footprintScale) / 0.86,
      Math.max(0.06, faceHeight / 0.72),
      0.72,
    );

    const panel = rig.panels[index]!;
    panel.visible = true;
    panel.position.set(
      point.x + face.x * (halfWidth - 0.012),
      baseY + 0.05 + panelHeight / 2,
      point.z + face.z * (halfWidth - 0.012),
    );
    panel.rotation.set(0, face.yaw, 0);
    panel.scale.set(
      (HARDLIGHT_PANEL_WIDTH * footprintScale) / 0.78,
      panelHeight / 0.64,
      0.54,
    );
  }

  // The fifth pane closes the silhouette into a block instead of a four-sided wall cage.
  // It rises with the faces, while the brighter top outline remains at the true height.
  const capFrame = rig.frames[4]!;
  capFrame.visible = true;
  capFrame.position.copy(point).setY(baseY + faceHeight);
  capFrame.rotation.x = -Math.PI / 2;
  capFrame.scale.set(
    (HARDLIGHT_FRAME_WIDTH * footprintScale) / 0.86,
    (HARDLIGHT_FRAME_WIDTH * footprintScale) / 0.72,
    0.72,
  );
  const capPanel = rig.panels[4]!;
  capPanel.visible = true;
  capPanel.position.copy(point).setY(baseY + faceHeight + 0.004);
  capPanel.rotation.x = -Math.PI / 2;
  capPanel.scale.set(
    (HARDLIGHT_PANEL_WIDTH * footprintScale) / 0.78,
    (HARDLIGHT_PANEL_WIDTH * footprintScale) / 0.64,
    0.54,
  );

  for (const [index, [x, z]] of [
    [-1, -1],
    [1, -1],
    [1, 1],
    [-1, 1],
  ].entries()) {
    const post = rig.posts[index]!;
    post.visible = true;
    post.position.set(
      point.x + x * halfWidth,
      baseY + envelopeHeight / 2,
      point.z + z * halfWidth,
    );
    post.scale.set(0.66, Math.max(0.05, envelopeHeight / 0.78), 0.66);
  }

  const edges = [
    [-1, -1, 1, -1],
    [1, -1, 1, 1],
    [1, 1, -1, 1],
    [-1, 1, -1, -1],
  ] as const;
  for (const [index, edge] of edges.entries()) {
    const bottom = rig.segments[index]!;
    placeEdge(
      bottom,
      point,
      edge[0] * HARDLIGHT_HALF_WIDTH,
      edge[1] * HARDLIGHT_HALF_WIDTH,
      edge[2] * HARDLIGHT_HALF_WIDTH,
      edge[3] * HARDLIGHT_HALF_WIDTH,
      baseY,
      footprintScale,
    );
    setEnergyColour(bottom, HARDLIGHT_ENERGY);
    setEnergyOpacity(bottom, energyOpacity * 0.78);

    const top = rig.segments[index + 4]!;
    placeEdge(
      top,
      point,
      edge[0] * HARDLIGHT_HALF_WIDTH,
      edge[1] * HARDLIGHT_HALF_WIDTH,
      edge[2] * HARDLIGHT_HALF_WIDTH,
      edge[3] * HARDLIGHT_HALF_WIDTH,
      baseY + envelopeHeight,
      footprintScale,
    );
    setEnergyColour(top, HARDLIGHT_ENERGY);
    setEnergyOpacity(top, energyOpacity);
  }

  // Three quiet bars on the cap are the barrier's three rules-owned hull steps. Lost
  // bars make damage legible without wrapping the block in another oversized status UI.
  for (let index = 0; index < 3; index += 1) {
    const capacity = rig.segments[index + 8]!;
    capacity.visible = index < remainingCapacity;
    capacity.position.set(
      point.x,
      baseY + envelopeHeight + 0.026,
      point.z + (index - 1) * 0.18 * footprintScale,
    );
    capacity.scale.set(0.18 * footprintScale, 1, 0.72);
    setEnergyColour(capacity, rig.main.color);
    setEnergyOpacity(capacity, energyOpacity * 1.25);
  }

  rig.fill.color.copy(HARDLIGHT_ENERGY);
  rig.fill.opacity = energyOpacity;
  rig.solid.color.copy(HARDLIGHT_FRAME).lerp(rig.main.color, 0.08);
  rig.solid.emissive.copy(HARDLIGHT_ENERGY).multiplyScalar(0.12);
  rig.solid.emissiveIntensity = 0.08;
  rig.solid.roughness = 0.9;
  rig.solid.metalness = 0.08;
  rig.solid.opacity = frameOpacity;
}

export default defineArcSignatureWebGLPainter({
  signatureId: 'hardlight-block',
  paint({ rig, signature, points, time, reducedMotion, phaseStartedTick }) {
    const point = points[0];
    if (!point) return;
    const deploy = reducedMotion ? 1 : risingSettle(time, phaseStartedTick);
    paintBarrier(
      rig,
      point,
      HARDLIGHT_HEIGHT,
      Math.max(0.065, HARDLIGHT_HEIGHT * deploy),
      signature.remainingCapacity,
      0.11 + deploy * 0.045,
      0.96,
    );
  },
  paintOutcome({ rig, priorSignature, facts, fraction }) {
    if (!hasConstructRemoval(facts)) return;
    const position = priorSignature?.positions[0];
    if (!position) return;
    const point = new THREE.Vector3(position.x + 0.5, 0, position.y + 0.5);
    const life = 1 - fraction;
    const visibleHeight = Math.max(0.04, HARDLIGHT_HEIGHT * life);
    paintBarrier(
      rig,
      point,
      visibleHeight,
      visibleHeight,
      priorSignature.remainingCapacity,
      life * 0.14,
      life * 0.9,
      0.86 + life * 0.14,
    );
  },
});
