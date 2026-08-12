import { defineArcSignatureWebGLPainter } from '../types';
import { anchorOf, fieldBase, tellPulse } from '../primitives';

export default defineArcSignatureWebGLPainter({
  signatureId: 'null-field',
  paint({ rig, points, owner, target, phase, time, reducedMotion }) {
    const anchor = anchorOf(points, owner, target);
    if (!anchor) return;
    const scale = phase === 'tell' ? tellPulse(time) : 1;
    const area = fieldBase(rig, points, anchor, scale);
    rig.fill.opacity *= 1.65;
    rig.disc.scale.y *= 0.96;
    const ticks = reducedMotion ? 4 : 8;
    for (let index = 0; index < ticks; index += 1) {
      const angle = (index / ticks) * Math.PI * 2;
      const marker = rig.markers[index]!;
      marker.visible = true;
      marker.position.set(
        area.centre.x + Math.cos(angle) * area.width * 0.32,
        0.052,
        area.centre.z + Math.sin(angle) * area.depth * 0.32,
      );
      marker.scale.set(0.3, 0.08, 0.45);
      marker.rotation.y = -angle;
    }
  },
});
