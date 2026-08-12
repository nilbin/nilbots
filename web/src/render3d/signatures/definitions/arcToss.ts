import { defineArcSignatureWebGLPainter } from '../types';
import { targetRing, tellPulse } from '../primitives';

export default defineArcSignatureWebGLPainter({
  signatureId: 'arc-toss',
  paint({ rig, points, target, time }) {
    // The objective renderer owns the one authoritative Core and its flight.
    // This painter only publishes the selected destination.
    const destination = points.at(-1) ?? target;
    if (!destination) return;
    targetRing(rig, destination, tellPulse(time) * 0.7);
    const marker = rig.markers[0]!;
    marker.visible = true;
    marker.position.copy(destination).setY(0.06);
    marker.scale.set(0.46, 0.11, 0.46);
  },
});
