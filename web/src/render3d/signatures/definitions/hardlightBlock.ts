import * as THREE from 'three';
import { defineArcSignatureWebGLPainter } from '../types';

export default defineArcSignatureWebGLPainter({
  signatureId: 'hardlight-block',
  paint({ rig, signature, points }) {
    const point = points[0];
    if (!point) return;
    rig.fill.opacity *= 0.52;
    const offsets = [
      [-0.22, -0.22],
      [0.22, -0.22],
      [0.22, 0.22],
      [-0.22, 0.22],
    ] as const;
    for (const [index, [x, z]] of offsets.entries()) {
      const panel = rig.panels[index]!;
      panel.visible = true;
      panel.material = rig.solid;
      panel.position.copy(point).add(new THREE.Vector3(x, 0.19, z));
      panel.scale.set(0.46, 0.58, 0.46);
      panel.rotation.y = index * Math.PI / 2;
      if (index >= signature.remainingCapacity) panel.scale.y *= 0.72;
    }
  },
});
