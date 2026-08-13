import * as THREE from 'three';
import { defineArcSignatureWebGLPainter } from '../types';
import { clamp01, risingSettle, setEnergyColour, setEnergyOpacity } from '../primitives';

const PRISM_GLASS = '#79aeb2';
const PRISM_FRAME = '#566468';

export default defineArcSignatureWebGLPainter({
  signatureId: 'prism-wall',
  paint({ rig, signature, points, time, reducedMotion, phaseStartedTick }) {
    if (points.length === 0) return;
    const deploy = reducedMotion ? 1 : risingSettle(time, phaseStartedTick);
    rig.fill.color.set(PRISM_GLASS);
    rig.fill.opacity = 0.14;
    const direction = points.length > 1
      ? new THREE.Vector3().subVectors(points.at(-1)!, points[0]!)
      : new THREE.Vector3(1, 0, 0);
    const yaw = -Math.atan2(direction.z, direction.x);
    for (const [index, point] of points.slice(0, 3).entries()) {
      const panel = rig.panels[index]!;
      const frame = rig.frames[index]!;
      const delay = reducedMotion ? 1 : clamp01(deploy * 1.35 - index * 0.14);
      frame.visible = true;
      frame.position.copy(point).setY(0.05 + delay * 0.37);
      // Extruded frame geometry is authored in XY; keep Y vertical and rotate only around
      // the arena up-axis. Rotating it onto the floor made the old "wall" read as two
      // orange hoops in normal replays.
      frame.rotation.set(0, yaw, 0);
      frame.scale.set(1.2, Math.max(0.07, delay * 1.82), 0.9);
      panel.visible = true;
      panel.position.copy(point).setY(0.06 + delay * 0.37);
      panel.rotation.set(0, yaw, 0);
      panel.scale.set(1.12, Math.max(0.08, delay * 1.68), 0.68);
      setEnergyColour(panel, PRISM_GLASS);
      setEnergyOpacity(panel, 0.1 + delay * 0.08);
      const frontPost = rig.posts[index * 2]!;
      const backPost = rig.posts[index * 2 + 1]!;
      for (const [post, side] of [[frontPost, -1], [backPost, 1]] as const) {
        post.visible = true;
        post.position.copy(point).add(
          new THREE.Vector3(
            Math.cos(yaw) * side * 0.43,
            0.04 + delay * 0.42,
            -Math.sin(yaw) * side * 0.43,
          ),
        );
        post.scale.set(0.82, Math.max(0.08, delay * 1.82), 0.82);
      }
      const capacity = rig.rings[index]!;
      capacity.visible = index < signature.remainingCapacity;
      capacity.position.copy(point).setY(0.045);
      capacity.scale.setScalar(0.72 + 0.04 * Math.sin(time * Math.PI * 1.2));
      setEnergyColour(capacity, PRISM_GLASS);
      setEnergyOpacity(capacity, 0.2);
    }
    rig.solid.color.set(PRISM_FRAME);
    rig.solid.emissive.set('#162a2d');
    rig.solid.emissiveIntensity = 0.04;
    rig.solid.opacity = 0.96;
  },
});
