import * as THREE from 'three';
import { defineArcSignatureWebGLPainter } from '../types';
import { anchorOf, lineAt, operationProgress, targetRing, tellPulse } from '../primitives';

export default defineArcSignatureWebGLPainter({
  signatureId: 'falling-star',
  paint({ rig, signature, points, owner, target, time, phase }) {
    const anchor = points[0] ?? anchorOf(points, owner, target);
    if (!anchor) return;
    const pulse = tellPulse(time);
    targetRing(rig, anchor, pulse);
    for (const [index, offset] of [
      new THREE.Vector3(0.56, 0, 0),
      new THREE.Vector3(-0.56, 0, 0),
      new THREE.Vector3(0, 0, 0.56),
      new THREE.Vector3(0, 0, -0.56),
    ].entries()) {
      const inner = offset.clone().multiplyScalar(0.42);
      lineAt(
        rig.segments[index]!,
        anchor.clone().add(inner),
        anchor.clone().add(offset),
        0.065,
      );
    }
    if (phase !== 'tell') return;
    const descent = operationProgress(signature, time);
    rig.capsule.visible = true;
    rig.capsule.position.copy(anchor).setY(1.45 - descent * 0.98);
      rig.capsule.scale.set(0.75, 1.05, 0.75);
  },
  paintOutcome({ rig, priorSignature, facts, fraction, owner }) {
    const completed = facts.some(
      (fact) =>
        fact.kind === 'signature-changed' && fact.reason === 'completed',
    );
    if (!completed) return;
    const points = priorSignature?.positions.map(
      (point) => new THREE.Vector3(point.x + 0.5, 0.065, point.y + 0.5),
    ) ?? [];
    const anchor = points[0] ?? owner;
    if (!anchor) return;
    const life = 1 - fraction;
    rig.main.opacity *= life * 0.68;
    rig.capsule.visible = fraction < 0.28;
    rig.capsule.position.copy(anchor).setY(0.13 + (1 - fraction / 0.28) * 0.34);
    rig.capsule.scale.set(0.88, 0.78, 0.88);
    for (let index = 1; index < points.length; index += 1)
      lineAt(rig.segments[index - 1]!, anchor, points[index]!, 0.2 * life + 0.04);
    for (const [index, point] of points.entries()) {
      const marker = rig.markers[index];
      if (!marker) break;
      marker.visible = true;
      marker.position.copy(point).setY(0.07);
      marker.scale.setScalar(0.42 + fraction * 0.7);
    }
  },
});
