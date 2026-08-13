import { defineArcSignatureWebGLPainter } from '../types';
import { setEnergyColour, setEnergyOpacity, targetRing, tellPulse } from '../primitives';

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
      // The replay already moves the one real Core. A complete drawn parabola duplicated
      // that truth and looked like a decorative trajectory editor; two faint motes are
      // enough to give the airborne Core a little lift without drawing its future.
      for (let index = 0; index < 2; index += 1) {
        const p = Math.max(0, phaseProgress - index * 0.07);
        const mote = rig.particles[index]!;
        mote.visible = true;
        mote.position.lerpVectors(source, destination, p);
        mote.position.y = 0.12 + Math.sin(p * Math.PI) * 1.05;
        mote.scale.setScalar(0.78 - index * 0.18);
        setEnergyColour(mote, CORE_ARC);
        setEnergyOpacity(mote, 0.16 - index * 0.045);
      }
    }
  },
});
