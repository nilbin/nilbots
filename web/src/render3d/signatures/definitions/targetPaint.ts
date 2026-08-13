import { defineArcSignatureWebGLPainter } from '../types';
import { setEnergyColour, setEnergyOpacity, targetBrackets, tellPulse } from '../primitives';

const CORAL = '#e78169';
const BRASS = '#caa65a';

export default defineArcSignatureWebGLPainter({
  signatureId: 'target-paint',
  paint({ rig, signature, points, target, time, phase, phaseAge }) {
    const tracked = target ?? points[0];
    if (!tracked) return;
    const acquire = phase === 'tell' ? 1.9 : 1.58;
    targetBrackets(rig, tracked, tellPulse(time) * acquire);
    for (const segment of rig.segments.slice(0, 8)) {
      setEnergyColour(segment, CORAL);
      setEnergyOpacity(segment, 0.38);
    }
    const lock = rig.rings[0]!;
    lock.visible = phase !== 'tell';
    lock.position.copy(tracked).setY(0.055);
    lock.scale.setScalar(1.62 + Math.sin(time * Math.PI * 1.3) * 0.06);
    setEnergyColour(lock, CORAL);
    setEnergyOpacity(lock, 0.24);
    for (let index = 0; index < Math.min(3, signature.remainingCapacity); index += 1) {
      const tally = rig.slabs[index]!;
      tally.visible = true;
      const settle = phase === 'tell' ? 0 : Math.min(1, phaseAge / 0.85);
      // Capacity stays readable without the three long blocks that projected as one huge
      // unexplained line to the target's right. These are a compact crown above the lock.
      tally.position.set(
        tracked.x + (index - 1) * 0.24,
        0.16 + (1 - settle) * 0.32,
        tracked.z - 0.76,
      );
      tally.scale.set(0.13, 0.18, 0.28);
      tally.material = rig.solid;
    }
    rig.solid.color.set(BRASS);
    rig.solid.emissive.set(CORAL).multiplyScalar(0.12);
  },
});
