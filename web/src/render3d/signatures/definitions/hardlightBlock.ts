import * as THREE from 'three';
import { defineArcSignatureWebGLPainter } from '../types';
import { risingSettle, setEnergyColour, setEnergyOpacity } from '../primitives';

export default defineArcSignatureWebGLPainter({
  signatureId: 'hardlight-block',
  paint({ rig, signature, points, time, reducedMotion, phaseStartedTick }) {
    const point = points[0];
    if (!point) return;
    const deploy = reducedMotion ? 1 : risingSettle(time, phaseStartedTick);
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
      setEnergyColour(edge, '#78aeb3');
      setEnergyOpacity(edge, index < signature.remainingCapacity + 1 ? 0.46 : 0.16);
    }
    const body = rig.slabs[0]!;
    body.visible = true;
    body.position.copy(point).setY(-0.42 + deploy * 0.98);
    body.scale.set(0.9, Math.max(0.12, deploy * 3.45), 0.9);
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
      post.visible = true;
      post.position.copy(point).add(new THREE.Vector3(x, 0.08 + deploy * 0.64, z));
      post.scale.set(0.58, Math.max(0.1, deploy * 2.05), 0.58);
    }
    const cap = rig.slabs[1]!;
    cap.visible = true;
    cap.position.copy(point).setY(-0.3 + deploy * 1.2);
    cap.scale.set(0.7, Math.max(0.08, deploy * 0.26), 0.7);
    cap.rotation.y = 0;
    // Dark seams break the broad body into the same stacked armored courses as the board.
    // They are shallow matte bars, not emission, so they add form without adding glare.
    for (let index = 0; index < 2; index += 1) {
      const seam = rig.insets[index]!;
      seam.visible = true;
      seam.position.copy(point).setY(0.28 + deploy * (0.18 + index * 0.33));
      seam.scale.set(0.84, 0.45, 0.045);
    }
    rig.inset.color.set('#181e21');
    rig.inset.opacity = 0.98;
    rig.solid.color.set('#4c5559');
    rig.solid.emissive.set('#112126');
    rig.solid.emissiveIntensity = 0.04;
    rig.solid.opacity = 0.98;
  },
});
