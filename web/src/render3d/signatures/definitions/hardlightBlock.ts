import * as THREE from 'three';
import { defineArcSignatureWebGLPainter } from '../types';
import {
  hasConstructRemoval,
  risingSettle,
  setEnergyColour,
  setEnergyOpacity,
} from '../primitives';
import type { ArenaTheme } from '../../../render/arenaThemes';

function wallStyle(arenaTheme: ArenaTheme) {
  const wallFamily = arenaTheme.walls.families.get(
    arenaTheme.walls.defaults.interior,
  );
  const wallDetails = wallFamily?.geometry3d.details;
  const bastion = wallFamily?.geometry3d.bastionCover;
  const trees = wallFamily?.geometry3d.treeCover;
  const authoredWallHeight = wallFamily
    ? wallFamily.geometry3d.height +
      (wallFamily.geometry3d.upperProfile?.height ?? 0)
    : 0;
  return {
    wallFamily,
    bodyColour: bastion?.bodyColor
      ?? trees?.canopyColor
      ?? wallFamily?.material3d.colorMultiplier
      ?? arenaTheme.palette.frame,
    seamColour: bastion?.trimColor
      ?? trees?.trunkColor
      ?? wallDetails?.panelColor
      ?? arenaTheme.palette.canvas,
    energyColour: bastion?.heatColor
      ?? trees?.canopyAccentColor
      ?? wallDetails?.ventColor
      ?? arenaTheme.palette.zone,
    wallHeight: Math.max(
      0.66,
      authoredWallHeight ||
        ((trees?.trunkHeight ?? 0) + (trees?.canopyHeight ?? 0)),
    ),
  };
}

export default defineArcSignatureWebGLPainter({
  signatureId: 'hardlight-block',
  paint({
    rig,
    signature,
    points,
    arenaTheme,
    time,
    reducedMotion,
    phaseStartedTick,
  }) {
    const point = points[0];
    if (!point) return;
    const deploy = reducedMotion ? 1 : risingSettle(time, phaseStartedTick);
    const {
      wallFamily,
      bodyColour,
      seamColour,
      energyColour,
      wallHeight,
    } = wallStyle(arenaTheme);
    // Mason borrows the active board's interior-cover material family. Ember Forge gets
    // refractory armour and heat seams; an organic board gets canopy, trunk and leaf
    // accents from that same contract instead of inheriting Tapline's grey construction.
    // An immediate complete outline tells the honest blocked envelope; one heavy central
    // body then rises inside a four-post cage. A stack of four flat plates projected as a
    // grey comb from the gameplay camera, not the one-tile cube in the approved row.
    for (const [index, [fromX, fromZ, toX, toZ]] of [
      [-0.44, -0.44, 0.44, -0.44],
      [0.44, -0.44, 0.44, 0.44],
      [0.44, 0.44, -0.44, 0.44],
      [-0.44, 0.44, -0.44, -0.44],
    ].entries()) {
      const edge = rig.segments[index]!;
      const from = point.clone().add(new THREE.Vector3(fromX, 0, fromZ));
      const to = point.clone().add(new THREE.Vector3(toX, 0, toZ));
      edge.visible = true;
      const dx = to.x - from.x;
      const dz = to.z - from.z;
      edge.position.set((from.x + to.x) / 2, 0.055, (from.z + to.z) / 2);
      edge.rotation.y = -Math.atan2(dz, dx);
      edge.scale.set(Math.hypot(dx, dz), 1, 1.8);
      setEnergyColour(edge, energyColour);
      setEnergyOpacity(edge, index < signature.remainingCapacity + 1 ? 0.46 : 0.16);
    }
    const body = rig.slabs[0]!;
    body.visible = true;
    body.position.copy(point).setY(-wallHeight / 2 + deploy * wallHeight);
    body.scale.set(0.9, wallHeight / 0.22, 0.9);
    body.rotation.y = 0;
    // Four corner pylons lock the obstacle's corners without protruding past its top-down
    // envelope. Oversized posts made the one-tile block look like a grey plus sign.
    for (const [index, [x, z]] of [
      [-0.39, -0.39],
      [0.39, -0.39],
      [0.39, 0.39],
      [-0.39, 0.39],
    ].entries()) {
      const post = rig.posts[index]!;
      const postHeight = wallHeight * 0.88;
      post.visible = true;
      post.position.copy(point).add(new THREE.Vector3(
        x,
        -postHeight / 2 + deploy * postHeight,
        z,
      ));
      post.scale.set(0.54, postHeight / 0.78, 0.54);
    }
    const cap = rig.slabs[1]!;
    const capHeight = 0.07;
    cap.visible = true;
    cap.position.copy(point).setY(
      -capHeight / 2 + deploy * (wallHeight + capHeight),
    );
    cap.scale.set(0.7, capHeight / 0.22, 0.7);
    cap.rotation.y = 0;
    // Dark seams break the broad body into the same stacked armored courses as the board.
    // They are shallow matte bars, not emission, so they add form without adding glare.
    for (let index = 0; index < 2; index += 1) {
      const seam = rig.insets[index]!;
      seam.visible = true;
      seam.position.copy(point).setY(
        deploy * wallHeight * (0.34 + index * 0.32),
      );
      seam.scale.set(0.84, 0.45, 0.045);
    }
    rig.inset.color.set(seamColour);
    rig.inset.opacity = 0.98;
    rig.solid.color.set(bodyColour);
    rig.solid.emissive.set(energyColour).multiplyScalar(0.1);
    rig.solid.emissiveIntensity = 0.04;
    rig.solid.roughness = wallFamily?.material3d.roughness ?? 0.94;
    rig.solid.metalness = wallFamily?.material3d.metalness ?? 0.08;
    rig.solid.opacity = 0.98;
  },
  paintOutcome({ rig, priorSignature, facts, arenaTheme, fraction }) {
    if (!hasConstructRemoval(facts)) return;
    const position = priorSignature?.positions[0];
    if (!position) return;
    const point = new THREE.Vector3(position.x + 0.5, 0, position.y + 0.5);
    const {
      wallFamily,
      bodyColour,
      seamColour,
      energyColour,
      wallHeight,
    } = wallStyle(arenaTheme);
    const life = 1 - fraction;
    const visibleHeight = wallHeight * life;
    const body = rig.slabs[0]!;
    body.visible = true;
    body.position.copy(point).setY(visibleHeight / 2 - fraction * 0.22);
    body.scale.set(
      0.9 * (0.8 + life * 0.2),
      Math.max(0.05, visibleHeight / 0.22),
      0.9 * (0.8 + life * 0.2),
    );
    for (const [index, [x, z]] of [
      [-0.39, -0.39],
      [0.39, -0.39],
      [0.39, 0.39],
      [-0.39, 0.39],
    ].entries()) {
      const post = rig.posts[index]!;
      const postHeight = visibleHeight * 0.88;
      post.visible = true;
      post.position.copy(point).add(new THREE.Vector3(
        x * (0.82 + life * 0.18),
        Math.max(0.025, postHeight / 2 - fraction * 0.2),
        z * (0.82 + life * 0.18),
      ));
      post.scale.set(0.54, Math.max(0.05, postHeight / 0.78), 0.54);
    }
    const floor = rig.ring;
    floor.visible = true;
    floor.position.copy(point).setY(0.045);
    floor.scale.setScalar(0.82 - fraction * 0.4);
    setEnergyColour(floor, energyColour);
    setEnergyOpacity(floor, life * 0.18);
    rig.inset.color.set(seamColour);
    rig.inset.opacity = life * 0.86;
    rig.solid.color.set(bodyColour);
    rig.solid.emissive.set(energyColour).multiplyScalar(0.1);
    rig.solid.emissiveIntensity = 0.025;
    rig.solid.roughness = wallFamily?.material3d.roughness ?? 0.94;
    rig.solid.metalness = wallFamily?.material3d.metalness ?? 0.08;
    rig.solid.opacity = life * 0.9;
  },
});
