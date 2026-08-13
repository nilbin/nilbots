import * as THREE from 'three';
import { defineArcSignatureWebGLPainter } from '../types';
import { lineAt, setEnergyColour, setEnergyOpacity } from '../primitives';

const HEAL = '#83caa1';

export default defineArcSignatureWebGLPainter({
  signatureId: 'repair-beam',
  paint({ rig, owner, target, time, reducedMotion }) {
    if (!owner || !target) return;
    const dx = target.x - owner.x;
    const dz = target.z - owner.z;
    const length = Math.hypot(dx, dz) || 1;
    const normal = new THREE.Vector3(-dz / length, 0, dx / length);
    // Three segmented strands visibly braid around one another. A single solid cable plus
    // three straight overlays collapsed into an ordinary white attack line at overview.
    const samples = 6;
    for (let strand = 0; strand < 3; strand += 1) {
      for (let sample = 0; sample < samples; sample += 1) {
        const p0 = sample / samples;
        const p1 = (sample + 1) / samples;
        const wave0 = Math.sin(p0 * Math.PI * 3 + time * 2.1 + strand * Math.PI * 2 / 3);
        const wave1 = Math.sin(p1 * Math.PI * 3 + time * 2.1 + strand * Math.PI * 2 / 3);
        const from = new THREE.Vector3().lerpVectors(owner, target, p0)
          .addScaledVector(normal, wave0 * 0.18);
        const to = new THREE.Vector3().lerpVectors(owner, target, p1)
          .addScaledVector(normal, wave1 * 0.18);
        from.y = 0.25 + Math.cos(p0 * Math.PI * 3 + strand * 2.1) * 0.09;
        to.y = 0.25 + Math.cos(p1 * Math.PI * 3 + strand * 2.1) * 0.09;
        const segment = rig.segments[strand * samples + sample]!;
        lineAt(segment, from, to, 0.11, (from.y + to.y) / 2);
        setEnergyColour(segment, strand === 1 ? '#a9d8b7' : HEAL);
        setEnergyOpacity(segment, strand === 1 ? 0.22 : 0.16);
      }
    }
    const packetCount = reducedMotion ? 2 : 5;
    for (let index = 0; index < packetCount; index += 1) {
      const packet = rig.particles[index]!;
      const progress = (time * 0.72 + index / packetCount) % 1;
      packet.visible = true;
      packet.position.lerpVectors(owner, target, progress);
      packet.position.addScaledVector(
        normal,
        Math.sin(progress * Math.PI * 4 + index) * 0.11,
      );
      packet.position.y = 0.22 + Math.sin(progress * Math.PI) * 0.14;
      packet.scale.setScalar(0.64 + Math.sin(progress * Math.PI) * 0.32);
      setEnergyColour(packet, HEAL);
      setEnergyOpacity(packet, 0.2);
    }
    for (let index = 0; index < 3; index += 1) {
      const seam = rig.rings[index]!;
      seam.visible = true;
      seam.position.copy(target).setY(0.12 + index * 0.16);
      seam.scale.setScalar(0.9 + index * 0.3);
      setEnergyColour(seam, index === 2 ? '#d9bd76' : HEAL);
      setEnergyOpacity(seam, 0.13 + index * 0.035);
    }
  },
});
