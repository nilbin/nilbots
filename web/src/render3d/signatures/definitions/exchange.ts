import * as THREE from 'three';
import { defineArcSignatureWebGLPainter } from '../types';
import { lineAt, setEnergyOpacity, tellPulse } from '../primitives';

export default defineArcSignatureWebGLPainter({
  signatureId: 'exchange',
  paint({ rig, owner, target, time, phaseProgress, reducedMotion }) {
    if (!owner || !target) return;
    const pulse = tellPulse(time);
    const dx = target.x - owner.x;
    const dz = target.z - owner.z;
    const length = Math.hypot(dx, dz) || 1;
    const normal = new THREE.Vector3(-dz / length, 0, dx / length);
    const samples = reducedMotion ? 1 : 2;
    for (let index = 0; index < samples; index += 1) {
      const p0 = index / samples;
      const p1 = (index + 1) / samples;
      const wave0 = Math.sin((p0 + phaseProgress) * Math.PI * 2) * 0.26;
      const wave1 = Math.sin((p1 + phaseProgress) * Math.PI * 2) * 0.26;
      const from = new THREE.Vector3().lerpVectors(owner, target, p0).addScaledVector(normal, wave0);
      const to = new THREE.Vector3().lerpVectors(owner, target, p1).addScaledVector(normal, wave1);
      const ribbon = rig.segments[index]!;
      lineAt(ribbon, from, to, 0.11, 0.14);
      setEnergyOpacity(ribbon, 0.14);
      const mirror = rig.segments[index + samples]!;
      lineAt(
        mirror,
        new THREE.Vector3().lerpVectors(target, owner, p0).addScaledVector(normal, -wave0),
        new THREE.Vector3().lerpVectors(target, owner, p1).addScaledVector(normal, -wave1),
        0.09,
        0.15,
      );
      setEnergyOpacity(mirror, 0.11);
    }
    for (const [index, position] of [owner, target].entries()) {
      const endpoint = rig.toruses[index]!;
      endpoint.visible = true;
      endpoint.position.copy(position).setY(0.1);
      endpoint.scale.setScalar((1.18 - phaseProgress * 0.28) * pulse);
      setEnergyOpacity(endpoint, 0.2);
    }
  },
  paintOutcome({ rig, facts, fraction }) {
    const relocations = facts.filter((fact) => fact.kind === 'body-relocated');
    if (relocations.length === 0) return;
    const life = 1 - fraction;
    for (const [index, fact] of relocations.entries()) {
      if (fact.kind !== 'body-relocated') continue;
      const to = new THREE.Vector3(fact.to.x + 0.5, 0.14, fact.to.y + 0.5);
      // The bodies already perform the authoritative swap. Decorative shuttles and bowed
      // trails made that real relocation look like a force pushing them apart.
      const endpoint = rig.toruses[index]!;
      endpoint.visible = true;
      endpoint.position.copy(to).setY(0.09);
      endpoint.scale.setScalar(0.76 + fraction * 0.46);
      setEnergyOpacity(endpoint, life * 0.2);
    }
  },
});
