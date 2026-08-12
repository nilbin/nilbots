import { defineArcSignatureWebGLPainter } from '../types';
import {
  anchorOf,
  areaBounds,
  operationProgress,
  placeTravelHead,
  targetRing,
} from '../primitives';

export default defineArcSignatureWebGLPainter({
  signatureId: 'smoke-canister',
  paint({ rig, signature, points, owner, target, time, phase, reducedMotion }) {
    if (phase === 'in-flight' && points[0] && points.at(-1)) {
      const from = points[0]!;
      const to = points.at(-1)!;
      placeTravelHead(
        rig.capsule,
        from,
        to,
        operationProgress(signature, time),
        0.38,
        0.78,
      );
      targetRing(rig, to, 0.62);
      return;
    }
    const anchor = anchorOf(points, owner, target);
    if (!anchor) return;
    const area = areaBounds(points.length > 0 ? points : [anchor]);
    targetRing(rig, area.centre, 0.82);
    const count = Math.min(reducedMotion ? 5 : 10, rig.particles.length);
    for (let index = 0; index < count; index += 1) {
      const lobe = rig.particles[index]!;
      lobe.visible = true;
      lobe.material = rig.smoke;
      const angle = index * 2.399963;
      const radius = Math.sqrt((index + 0.5) / count) * 0.38;
      lobe.position.set(
        area.centre.x + Math.cos(angle) * area.width * radius,
        0.1 + (index % 3) * 0.045,
        area.centre.z + Math.sin(angle) * area.depth * radius,
      );
      lobe.scale.setScalar(1.6 + (index % 3) * 0.34);
    }
    rig.smoke.opacity = 0.2;
  },
});
