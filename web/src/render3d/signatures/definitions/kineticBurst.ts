import * as THREE from 'three';
import { defineArcSignatureWebGLPainter } from '../types';
import { lineAt, setEnergyColour, setEnergyOpacity } from '../primitives';

const PRESSURE = '#78aeb3';
export default defineArcSignatureWebGLPainter({
  signatureId: 'kinetic-burst',
  paint({ rig, points, owner, target, phaseProgress }) {
    // Kinetic's published positions are the affected radius in row-major order, so
    // points[0] is normally the north-west tile. The tell belongs to the rooted caster.
    const anchor = owner ?? target ?? points[0];
    if (!anchor) return;
    const contraction = 1.72 - phaseProgress * 0.82;
    // Keep the tell on the floor around the real caster. An opaque shared hub in the
    // middle turned this and Null Field into the same generic object-with-aura effect.
    const boundary = rig.rings[0]!;
    boundary.visible = true;
    boundary.position.copy(anchor).setY(0.055);
    boundary.scale.setScalar(contraction * 1.32);
    setEnergyColour(boundary, PRESSURE);
    setEnergyOpacity(boundary, 0.11);
    for (const [index, angle] of [0, Math.PI / 2, Math.PI, Math.PI * 1.5].entries()) {
      const marker = rig.markers[index]!;
      marker.visible = true;
      marker.position.set(
        anchor.x + Math.cos(angle) * contraction,
        0.065,
        anchor.z + Math.sin(angle) * contraction,
      );
      marker.rotation.y = -angle;
      marker.scale.set(0.34, 0.1, 0.58);
      setEnergyColour(marker, PRESSURE);
      setEnergyOpacity(marker, 0.22);
    }
  },
  paintOutcome({ rig, facts, owner, fraction }) {
    if (!facts.some((fact) => fact.kind === 'signature-changed' && fact.reason === 'completed') || !owner)
      return;
    const life = 1 - fraction;
    // One low, broad pressure torus is the resolve verb. There is deliberately no solid
    // expanding disc: that was the white bloom visible in stacked fights.
    rig.torus.visible = true;
    rig.torus.position.copy(owner).setY(0.095);
    rig.torus.scale.setScalar(0.76 + fraction * 3.25);
    setEnergyColour(rig.torus, PRESSURE);
    setEnergyOpacity(rig.torus, life * 0.16);
    let segment = 0;
    for (const fact of facts) {
      if (fact.kind !== 'body-relocated') continue;
      const from = new THREE.Vector3(fact.from.x + 0.5, 0.08, fact.from.y + 0.5);
      const to = new THREE.Vector3(fact.to.x + 0.5, 0.08, fact.to.y + 0.5);
      const wake = rig.segments[segment++];
      if (!wake) break;
      lineAt(wake, from, to, 0.16 * life + 0.05);
      setEnergyColour(wake, PRESSURE);
      setEnergyOpacity(wake, life * 0.3);
    }
  },
});
