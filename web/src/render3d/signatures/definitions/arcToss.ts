import * as THREE from 'three';
import { defineArcSignatureWebGLPainter } from '../types';
import { lineAt, setEnergyColour, setEnergyOpacity, targetRing, tellPulse } from '../primitives';

const CORE_ARC = '#e6e5ef';

export default defineArcSignatureWebGLPainter({
  signatureId: 'arc-toss',
  paint({ rig, points, target, time, phase, reducedMotion, phaseProgress }) {
    const destination = points[0] ?? target;
    const source = points.at(-1);
    if (!destination) return;
    targetRing(rig, destination, tellPulse(time) * 1.28);
    setEnergyColour(rig.ring, CORE_ARC);
    setEnergyOpacity(rig.ring, 0.34);
    const inner = rig.rings[0]!;
    inner.visible = true;
    inner.position.copy(destination).setY(0.052);
    inner.scale.setScalar(0.72);
    setEnergyColour(inner, CORE_ARC);
    setEnergyOpacity(inner, 0.18);
    if (phase === 'in-flight' && source && !reducedMotion) {
      const samples = 8;
      for (let index = 0; index < samples; index += 1) {
        const p0 = index / samples;
        const p1 = (index + 1) / samples;
        const from = new THREE.Vector3().lerpVectors(source, destination, p0);
        const to = new THREE.Vector3().lerpVectors(source, destination, p1);
        from.y = 0.09 + Math.sin(p0 * Math.PI) * 1.05;
        to.y = 0.09 + Math.sin(p1 * Math.PI) * 1.05;
        const segment = rig.segments[index]!;
        lineAt(segment, from, to, 0.075, (from.y + to.y) / 2);
        setEnergyColour(segment, CORE_ARC);
        setEnergyOpacity(segment, 0.1 + index * 0.014);
      }
      for (let index = 0; index < 4; index += 1) {
        const p = Math.max(0, phaseProgress - index * 0.07);
        const mote = rig.particles[index]!;
        mote.visible = true;
        mote.position.lerpVectors(source, destination, p);
        mote.position.y = 0.12 + Math.sin(p * Math.PI) * 1.05;
        mote.scale.setScalar(1.45 - index * 0.22);
        setEnergyColour(mote, CORE_ARC);
        setEnergyOpacity(mote, 0.27 - index * 0.04);
      }
    }
  },
});
