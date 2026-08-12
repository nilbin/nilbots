import * as THREE from 'three';
import { defineArcSignatureWebGLPainter } from '../types';
import { lineAt, tellPulse } from '../primitives';

export default defineArcSignatureWebGLPainter({
  signatureId: 'vector-dash',
  paint({ rig, points, owner, target, time, reducedMotion }) {
    const destination = points.at(-1) ?? target;
    if (!owner || !destination) return;
    lineAt(rig.segments[0]!, owner, destination, 0.065);
    const limit = Math.min(points.length, reducedMotion ? 2 : 5);
    const pulse = tellPulse(time);
    for (let index = 0; index < limit; index += 1) {
      const marker = rig.markers[index]!;
      marker.visible = true;
      marker.position.copy(points[index]!).setY(0.075);
      marker.scale.set(0.42 * pulse, 0.14, 0.72 * pulse);
    }
  },
  paintOutcome({ rig, facts, fraction, reducedMotion }) {
    const relocation = facts.find((fact) => fact.kind === 'body-relocated');
    if (!relocation || relocation.kind !== 'body-relocated') return;
    const from = new THREE.Vector3(relocation.from.x + 0.5, 0.07, relocation.from.y + 0.5);
    const to = new THREE.Vector3(relocation.to.x + 0.5, 0.07, relocation.to.y + 0.5);
    const life = 1 - fraction;
    rig.main.opacity *= life * 0.58;
    lineAt(rig.segments[0]!, from, to, 0.16 * life + 0.045);
    const count = reducedMotion ? 1 : 3;
    for (let index = 0; index < count; index += 1) {
      const marker = rig.markers[index]!;
      const progress = Math.max(0, fraction - index * 0.13);
      marker.visible = true;
      marker.position.lerpVectors(from, to, progress).setY(0.08);
      marker.scale.set(0.34 * life, 0.12, 0.62 * life);
    }
  },
});
