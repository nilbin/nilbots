import * as THREE from 'three';
import { defineArcSignatureWebGLPainter } from '../types';
import {
  paintPropFallback,
  risingSettle,
  setEnergyOpacity,
  targetRing,
} from '../primitives';

export default defineArcSignatureWebGLPainter({
  signatureId: 'trip-node',
  paint({ rig, signature, points, time, persistentPropReady, reducedMotion, phaseStartedTick }) {
    const deploy = reducedMotion ? 1 : risingSettle(time, phaseStartedTick);
    paintPropFallback(
      rig,
      points,
      time,
      persistentPropReady,
      new THREE.Vector3(0.34, 0.19, 0.34),
    );
    const anchor = points[0];
    if (!anchor) return;
    if (persistentPropReady) {
      // The approved prop is the silhouette. Three short floor rails draw inward to its
      // socket during deployment without covering it in generic grey geometry.
      for (const [index, angle] of [0, Math.PI * 2 / 3, Math.PI * 4 / 3].entries()) {
        const rail = rig.segments[index]!;
        const outer = 0.62 - deploy * 0.08;
        const inner = 0.34;
        rail.visible = true;
        rail.position.set(
          anchor.x + Math.cos(angle) * (inner + outer) * 0.5,
          0.06,
          anchor.z + Math.sin(angle) * (inner + outer) * 0.5,
        );
        rail.rotation.y = -angle;
        rail.scale.set(outer - inner, 1, 1.25);
        setEnergyOpacity(rail, 0.08);
      }
    }
    targetRing(rig, anchor, 0.66);
    setEnergyOpacity(rig.ring, 0.09);
    const spokes = reducedMotion ? 2 : 3;
    for (let index = 0; index < spokes; index += 1) {
      const marker = rig.markers[index]!;
      const angle = (index / spokes) * Math.PI * 2;
      marker.visible = true;
      marker.position.set(
        anchor.x + Math.cos(angle) * (0.58 - deploy * 0.1),
        0.055,
        anchor.z + Math.sin(angle) * (0.58 - deploy * 0.1),
      );
      marker.scale.set(0.22, 0.08, 0.34);
      marker.rotation.y = -angle;
      setEnergyOpacity(marker, signature.suppressed ? 0.05 : 0.1);
    }
  },
  paintOutcome({ rig, priorSignature, facts, fraction }) {
    if (!facts.some((fact) => fact.kind === 'signature-damage')) return;
    const point = priorSignature?.positions[0];
    if (!point) return;
    const anchor = new THREE.Vector3(point.x + 0.5, 0.08, point.y + 0.5);
    rig.torus.visible = true;
    rig.torus.position.copy(anchor);
    rig.torus.scale.setScalar(0.55 + fraction * 1.35);
    setEnergyOpacity(rig.torus, (1 - fraction) * 0.7);
  },
});
