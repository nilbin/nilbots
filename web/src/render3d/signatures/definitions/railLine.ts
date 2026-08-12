import * as THREE from 'three';
import { defineArcSignatureWebGLPainter } from '../types';
import { polyline, tellPulse } from '../primitives';

export default defineArcSignatureWebGLPainter({
  signatureId: 'rail-line',
  paint({ rig, points, owner, target, phase, time }) {
    const path = points.length > 1
      ? points
      : owner && target
        ? [owner, target]
        : [];
    if (path.length < 2) return;
    rig.main.opacity *= phase === 'tell' ? 0.42 : 0.3;
    const width = phase === 'tell' ? 0.075 : 0.26;
    polyline(rig, path, width);
    if (phase === 'tell') {
      const pulse = tellPulse(time);
      for (const [index, pathIndex] of [
        Math.floor(path.length / 3),
        Math.floor((path.length * 2) / 3),
      ].entries()) {
        const marker = rig.markers[index]!;
        marker.visible = true;
        marker.position.copy(path[pathIndex]!).setY(0.075);
        marker.scale.set(0.2, 0.08, 0.68 * pulse);
      }
    }
  },
  paintOutcome({ rig, priorSignature, facts, fraction }) {
    const completed = facts.some(
      (fact) =>
        fact.kind === 'signature-changed' && fact.reason === 'completed',
    );
    if (!completed) return;
    const path = priorSignature?.positions.map(
      (point) => new THREE.Vector3(point.x + 0.5, 0.065, point.y + 0.5),
    ) ?? [];
    if (path.length < 2) return;
    const life = 1 - fraction;
    rig.main.opacity *= life * 0.32;
    polyline(rig, path, 0.27 - fraction * 0.1);
  },
});
