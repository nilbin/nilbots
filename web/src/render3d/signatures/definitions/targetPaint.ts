import * as THREE from 'three';
import { defineArcSignatureWebGLPainter } from '../types';
import { lineAt, setEnergyColour, setEnergyOpacity, targetBrackets, tellPulse } from '../primitives';

const CORAL = '#e78169';
const BRASS = '#caa65a';

export default defineArcSignatureWebGLPainter({
  signatureId: 'target-paint',
  paint({ rig, signature, points, target, time, phase, phaseProgress, phaseAge }) {
    const tracked = target ?? points[0];
    if (!tracked) return;
    rig.pod.visible = true;
    rig.pod.position.copy(tracked).setY(1.72 + (phase === 'tell' ? (1 - phaseProgress) * 0.9 : 0));
    rig.pod.scale.set(0.68, 0.42, 0.68);
    rig.pod.rotation.y = phase === 'tell' ? time * 1.1 : Math.PI / 4;
    const acquire = phase === 'tell' ? 2.7 - phaseProgress * 1.02 : 1.68;
    targetBrackets(rig, tracked, tellPulse(time) * acquire);
    for (const segment of rig.segments.slice(0, 8)) {
      setEnergyColour(segment, CORAL);
      setEnergyOpacity(segment, 0.38);
    }
    // A rotating acquisition needle crossing the target reads at the fixed overview where
    // a gently scaling ring does not. It is still only a target cue: no hitbox or ray is
    // implied, and the two-sided sweep disappears the instant acquisition resolves.
    if (phase === 'tell') {
      const angle = phaseProgress * Math.PI * 1.75;
      const axis = new THREE.Vector3(Math.cos(angle), 0, Math.sin(angle));
      const sweep = rig.segments[8]!;
      const counter = rig.segments[9]!;
      const sweepLength = 2.05;
      const head = tracked.clone().addScaledVector(axis, 0.46 - phaseProgress * 0.18);
      const tail = tracked.clone().addScaledVector(axis, -sweepLength * 0.52);
      const opposite = tracked.clone().addScaledVector(axis, sweepLength * 0.52);
      const opposingTail = tracked.clone().addScaledVector(axis, -0.46 + phaseProgress * 0.18);
      lineAt(sweep, tail, head, 0.15, 0.28);
      lineAt(counter, opposite, opposingTail, 0.1, 0.34);
      setEnergyColour(sweep, CORAL);
      setEnergyColour(counter, BRASS);
      setEnergyOpacity(sweep, 0.42);
      setEnergyOpacity(counter, 0.3);
      const seeker = rig.pod;
      seeker.position.copy(head).setY(1.06 + Math.sin(phaseProgress * Math.PI) * 0.42);
      seeker.scale.set(0.68, 0.42, 0.68);
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
      tally.position.copy(tracked).add(
        new THREE.Vector3(
          1.08 + (1 - settle) * 0.62,
          0.42 + (1 - settle) * 0.52,
          (index - 1) * 0.36,
        ),
      );
      tally.scale.set(0.42, 0.22, 0.96);
      tally.material = rig.solid;
    }
    rig.solid.color.set(BRASS);
    rig.solid.emissive.set(CORAL).multiplyScalar(0.12);
  },
});
