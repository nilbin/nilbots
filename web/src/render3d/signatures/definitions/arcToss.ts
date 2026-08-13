import * as THREE from 'three';
import { defineArcSignatureWebGLPainter } from '../types';
import {
  setEnergyColour,
  setEnergyOpacity,
  targetRing,
  tellPulse,
} from '../primitives';

const CORE_ARC = '#e6e5ef';

export default defineArcSignatureWebGLPainter({
  signatureId: 'arc-toss',
  paint({
    rig,
    points,
    owner,
    target,
    coreFlight,
    time,
    phase,
    reducedMotion,
  }) {
    const source = coreFlight?.source ?? owner ?? points[0];
    const destination = coreFlight?.destination ?? (source
      ? [...points, ...(target ? [target] : [])]
          .filter((point) => point.distanceToSquared(source) > 0.0001)
          .sort(
            (left, right) =>
              right.distanceToSquared(source) - left.distanceToSquared(source),
          )[0]
      : points.at(-1) ?? target);
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
    if (phase === 'in-flight' && source) {
      // The overlay owns and moves the one real Core. This painter adds a levitation
      // collar around that same derived flight position, never a second orb or full drawn
      // parabola. It reads even on a short toss without turning into a trajectory editor.
      const p = coreFlight?.progress ?? 0;
      const flight = new THREE.Vector3().lerpVectors(source, destination, p);
      flight.y = 0.42 + Math.sin(p * Math.PI) * 1.05;
      const collar = rig.toruses[0]!;
      collar.visible = true;
      collar.position.copy(flight);
      collar.scale.setScalar(0.32 + Math.sin(p * Math.PI) * 0.08);
      setEnergyColour(collar, CORE_ARC);
      setEnergyOpacity(collar, 0.18);
      if (!reducedMotion) {
        for (let index = 0; index < 3; index += 1) {
          const angle = p * Math.PI * 4 + (index * Math.PI * 2) / 3;
          const mote = rig.particles[index]!;
          mote.visible = true;
          mote.position.copy(flight).add(new THREE.Vector3(
            Math.cos(angle) * 0.25,
            Math.sin(angle * 0.5) * 0.07,
            Math.sin(angle) * 0.25,
          ));
          mote.scale.setScalar(0.42);
          setEnergyColour(mote, CORE_ARC);
          setEnergyOpacity(mote, 0.1);
        }
      }
    }
  },
});
