import * as THREE from 'three';
import { defineArcSignatureWebGLPainter } from '../types';
import { lineAt, setEnergyOpacity, tellPulse } from '../primitives';

export default defineArcSignatureWebGLPainter({
  signatureId: 'vector-dash',
  paint({ rig, points, owner, target, time, reducedMotion, phaseProgress }) {
    const destination = points.at(-1) ?? target;
    if (!owner || !destination) return;
    const promise = rig.segments[0]!;
    lineAt(promise, owner, destination, 0.075);
    setEnergyOpacity(promise, 0.42 + phaseProgress * 0.2);
    const limit = Math.min(points.length, reducedMotion ? 2 : 5);
    const pulse = tellPulse(time);
    for (let index = 0; index < limit; index += 1) {
      const marker = rig.markers[index]!;
      marker.visible = true;
      marker.position.copy(points[index]!).setY(0.075);
      marker.scale.set(0.46 * pulse, 0.16, 0.82 * pulse);
      marker.rotation.y = -Math.atan2(destination.z - owner.z, destination.x - owner.x);
      setEnergyOpacity(marker, 0.58);
    }
  },
  paintOutcome({ rig, facts, fraction, reducedMotion }) {
    const relocation = facts.find((fact) => fact.kind === 'body-relocated');
    if (!relocation || relocation.kind !== 'body-relocated') return;
    const from = new THREE.Vector3(relocation.from.x + 0.5, 0.08, relocation.from.y + 0.5);
    const to = new THREE.Vector3(relocation.to.x + 0.5, 0.08, relocation.to.y + 0.5);
    const life = 1 - fraction;
    const wake = rig.segments[0]!;
    lineAt(wake, from, to, 0.24 * life + 0.055);
    setEnergyOpacity(wake, life * 0.72);
    const count = reducedMotion ? 1 : 4;
    for (let index = 0; index < count; index += 1) {
      const marker = rig.markers[index]!;
      const progress = Math.max(0, fraction - index * 0.11);
      marker.visible = true;
      marker.position.lerpVectors(from, to, progress).setY(0.11);
      marker.scale.set(0.48 * life, 0.16, 0.86 * life);
      setEnergyOpacity(marker, life * (0.72 - index * 0.1));
    }
  },
});
