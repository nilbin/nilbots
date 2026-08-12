import { defineArcSignatureWebGLPainter } from '../types';
import {
  anchorOf,
  fieldBase,
  operationProgress,
  placeTravelHead,
  tellPulse,
} from '../primitives';

export default defineArcSignatureWebGLPainter({
  signatureId: 'survey-flare',
  paint({ rig, signature, points, owner, target, time, phase, reducedMotion }) {
    if (phase === 'in-flight' && points[0] && points.at(-1)) {
      const from = points[0]!;
      const to = points.at(-1)!;
      placeTravelHead(
        rig.capsule,
        from,
        to,
        operationProgress(signature, time),
        0.45,
        0.72,
      );
      return;
    }
    const anchor = anchorOf(points, owner, target);
    if (!anchor) return;
    const area = fieldBase(rig, points, anchor, tellPulse(time));
    rig.fill.opacity *= 0.45;
    const ticks = Math.min(reducedMotion ? 4 : 10, rig.markers.length);
    for (let index = 0; index < ticks; index += 1) {
      const angle = (index / ticks) * Math.PI * 2;
      const marker = rig.markers[index]!;
      marker.visible = true;
      marker.position.set(
        area.centre.x + Math.cos(angle) * area.width * 0.43,
        0.065,
        area.centre.z + Math.sin(angle) * area.depth * 0.43,
      );
      marker.scale.set(0.22, 0.08, 0.54);
      marker.rotation.y = -angle;
    }
  },
});
