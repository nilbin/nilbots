import * as THREE from 'three';
import { defineArcSignatureWebGLPainter } from '../types';
import { lineAt, polyline, setEnergyColour, setEnergyOpacity, solidBarAt } from '../primitives';

const PRESSURE = '#78aeb3';

export default defineArcSignatureWebGLPainter({
  signatureId: 'rail-line',
  paint({ rig, points, owner, target, phaseProgress }) {
    const path = points.length > 1 ? points : owner && target ? [owner, target] : [];
    if (path.length < 2) return;
    polyline(rig, path, 0.075);
    for (let index = 0; index < path.length - 1; index += 1) {
      const segment = rig.segments[index]!;
      setEnergyColour(segment, PRESSURE);
      setEnergyOpacity(segment, 0.08 + phaseProgress * 0.06);
    }
    const from = path[0]!;
    const to = path.at(-1)!;
    // The body is the frozen pressure rail: broad, matte and low. The additive trace is
    // only an inset seam, so a stacked volley never turns this signature into a white
    // laser. During wind-up the rail grows out from Longshot rather than merely existing.
    const chargedTo = new THREE.Vector3().lerpVectors(
      from,
      to,
      0.16 + phaseProgress * 0.84,
    );
    solidBarAt(rig.bars[0]!, from, chargedTo, 0.14, 0.075);
    rig.solid.color.set('#3e565c');
    rig.solid.emissive.set('#10282d');
    rig.solid.emissiveIntensity = 0.05;
    rig.solid.opacity = 0.54;
    // A compact travelling notch gives the charge direction; it should never become a
    // second projectile sitting on top of the lane.
    const pressureHead = rig.payload;
    pressureHead.visible = true;
    pressureHead.position.copy(chargedTo);
    pressureHead.position.y = 0.18;
    pressureHead.rotation.z = Math.PI / 2;
    pressureHead.rotation.y = -Math.atan2(to.z - from.z, to.x - from.x);
    pressureHead.scale.set(0.42, 0.3, 0.42);
    const dx = to.x - from.x;
    const dz = to.z - from.z;
    const length = Math.hypot(dx, dz) || 1;
    const normal = new THREE.Vector3(-dz / length, 0, dx / length);
    for (const [index, progress] of [0.34, 0.68].entries()) {
      const centre = new THREE.Vector3().lerpVectors(from, to, progress);
      const notch = rig.segments[path.length - 1 + index]!;
      lineAt(
        notch,
        centre.clone().addScaledVector(normal, -0.22),
        centre.clone().addScaledVector(normal, 0.22),
        0.13,
      );
      setEnergyColour(notch, PRESSURE);
      setEnergyOpacity(notch, progress <= 0.16 + phaseProgress * 0.84 ? 0.2 : 0.07);
    }
  },
  paintOutcome({ rig, priorSignature, facts, fraction }) {
    if (!facts.some((fact) => fact.kind === 'signature-changed' && fact.reason === 'completed'))
      return;
    const path = priorSignature?.positions.map(
      (point) => new THREE.Vector3(point.x + 0.5, 0.07, point.y + 0.5),
    ) ?? [];
    if (path.length < 2) return;
    const life = 1 - fraction;
    const from = path[0]!;
    const to = path.at(-1)!;
    const sweep = Math.min(1, fraction * 1.45);
    const pressureTo = new THREE.Vector3().lerpVectors(from, to, sweep);
    solidBarAt(rig.bars[0]!, from, pressureTo, 0.22 * life + 0.09, 0.08);
    rig.solid.color.set('#3e565c');
    rig.solid.emissive.set('#10282d');
    rig.solid.emissiveIntensity = 0.05;
    rig.solid.opacity = life * 0.48;
    const afterimage = rig.segments[0]!;
    lineAt(afterimage, from, pressureTo, 0.12, 0.095);
    setEnergyColour(afterimage, PRESSURE);
    setEnergyOpacity(afterimage, life * 0.1);
    const head = rig.payload;
    head.visible = true;
    head.position.copy(pressureTo);
    head.position.y = 0.18;
    head.rotation.z = Math.PI / 2;
    head.rotation.y = -Math.atan2(to.z - from.z, to.x - from.x);
    head.scale.set(0.58, 0.38, 0.58);
  },
});
