import * as THREE from 'three';
import { defineArcSignatureWebGLPainter } from '../types';
import { targetBrackets, tellPulse } from '../primitives';

export default defineArcSignatureWebGLPainter({
  signatureId: 'target-paint',
  paint({ rig, signature, points, target, time }) {
    const tracked = target ?? points[0];
    if (!tracked) return;
    targetBrackets(rig, tracked, tellPulse(time));
    for (let index = 0; index < Math.min(3, signature.remainingCapacity); index += 1) {
      const panel = rig.panels[index]!;
      panel.visible = true;
      panel.material = rig.solid;
      panel.position.copy(tracked).add(
        new THREE.Vector3((index - 1) * 0.18, 0.11, 0.58),
      );
      panel.scale.set(0.14, 0.12, 0.42);
    }
  },
});
