import { defineArcSignatureWebGLPainter } from '../types';
import { lineAt, operationProgress, placeTravelHead, targetRing } from '../primitives';

export default defineArcSignatureWebGLPainter({
  signatureId: 'tractor-hook',
  paint({ rig, signature, points, owner, target, phase, time }) {
    const destination = target ?? points.at(-1);
    if (!owner || !destination) return;
    const progress = phase === 'in-flight'
      ? operationProgress(signature, time)
      : phase === 'tell'
        ? 0
        : 1;
    lineAt(rig.segments[0]!, owner, destination, phase === 'active' ? 0.035 : 0.052);
    const head = rig.markers[0]!;
    placeTravelHead(head, owner, destination, progress, 0.1, 0.82);
    head.scale.set(1.2, 0.55, 0.85);
    targetRing(rig, destination, 0.62);
  },
});
