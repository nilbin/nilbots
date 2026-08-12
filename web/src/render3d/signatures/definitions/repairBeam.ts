import * as THREE from 'three';
import { defineArcSignatureWebGLPainter } from '../types';
import { lineAt } from '../primitives';

export default defineArcSignatureWebGLPainter({
  signatureId: 'repair-beam',
  paint({ rig, owner, target, time, reducedMotion }) {
    if (!owner || !target) return;
    const dx = target.x - owner.x;
    const dz = target.z - owner.z;
    const length = Math.hypot(dx, dz) || 1;
    const normal = new THREE.Vector3(-dz / length, 0, dx / length);
    for (const [index, offset] of [-0.07, 0, 0.07].entries()) {
      lineAt(
        rig.segments[index]!,
        owner.clone().addScaledVector(normal, offset),
        target.clone().addScaledVector(normal, offset),
        index === 1 ? 0.042 : 0.026,
        0.13,
      );
    }
    const packetCount = reducedMotion ? 2 : 6;
    for (let index = 0; index < packetCount; index += 1) {
      const packet = rig.particles[index]!;
      const progress = (time * 0.5 + index / packetCount) % 1;
      packet.visible = true;
      packet.position.lerpVectors(owner, target, progress);
      packet.position.y = 0.14 + Math.sin(progress * Math.PI) * 0.08;
      packet.scale.setScalar(0.5);
    }
  },
});
