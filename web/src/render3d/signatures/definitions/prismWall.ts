import { defineArcSignatureWebGLPainter } from '../types';

export default defineArcSignatureWebGLPainter({
  signatureId: 'prism-wall',
  paint({ rig, signature, points }) {
    rig.fill.opacity *= 0.62;
    for (const [index, point] of points.entries()) {
      const panel = rig.panels[index];
      if (!panel) break;
      panel.visible = true;
      panel.material = rig.solid;
      panel.position.copy(point).setY(0.28);
      panel.scale.set(0.94, 0.82, 0.72);
      if (points[index + 1])
        panel.rotation.y = -Math.atan2(
          points[index + 1]!.z - point.z,
          points[index + 1]!.x - point.x,
        );
      const post = rig.markers[index];
      if (post) {
        post.visible = true;
        post.position.copy(point).setY(0.08);
        post.scale.setScalar(index < signature.remainingCapacity ? 0.42 : 0.22);
      }
    }
  },
});
