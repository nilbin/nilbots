import * as THREE from 'three';
import { defineArcSignatureWebGLPainter } from '../types';
import { lineAt, targetBrackets, tellPulse } from '../primitives';

export default defineArcSignatureWebGLPainter({
  signatureId: 'exchange',
  paint({ rig, owner, target, time }) {
    if (!owner || !target) return;
    const pulse = tellPulse(time);
    const dx = target.x - owner.x;
    const dz = target.z - owner.z;
    const length = Math.hypot(dx, dz) || 1;
    const normal = new THREE.Vector3(-dz / length, 0, dx / length);
    lineAt(
      rig.segments[0]!,
      owner.clone().addScaledVector(normal, 0.11),
      target.clone().addScaledVector(normal, -0.11),
      0.045,
      0.12,
    );
    lineAt(
      rig.segments[1]!,
      target.clone().addScaledVector(normal, 0.11),
      owner.clone().addScaledVector(normal, -0.11),
      0.045,
      0.16,
    );
    targetBrackets(rig, owner, pulse * 0.72, 2);
    targetBrackets(rig, target, pulse * 0.72, 10);
  },
  paintOutcome({ rig, facts, fraction }) {
    const relocations = facts.filter(
      (fact) => fact.kind === 'body-relocated',
    );
    if (relocations.length === 0) return;
    const life = 1 - fraction;
    rig.main.opacity *= life * 0.52;
    for (const [index, fact] of relocations.entries()) {
      if (fact.kind !== 'body-relocated') continue;
      const from = new THREE.Vector3(fact.from.x + 0.5, 0.1, fact.from.y + 0.5);
      const to = new THREE.Vector3(fact.to.x + 0.5, 0.1, fact.to.y + 0.5);
      lineAt(rig.segments[index]!, from, to, 0.075, 0.13 + index * 0.035);
      const marker = rig.markers[index]!;
      marker.visible = true;
      marker.position.lerpVectors(from, to, fraction).setY(0.13);
      marker.scale.setScalar(0.46 * life + 0.18);
    }
  },
});
