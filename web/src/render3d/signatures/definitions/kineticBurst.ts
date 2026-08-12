import * as THREE from 'three';
import { defineArcSignatureWebGLPainter } from '../types';
import { anchorOf, lineAt, targetRing, tellPulse } from '../primitives';

export default defineArcSignatureWebGLPainter({
  signatureId: 'kinetic-burst',
  paint({ rig, points, owner, target, time, phase }) {
    const anchor = anchorOf(points, owner, target);
    if (!anchor) return;
    if (phase === 'tell') {
      targetRing(rig, anchor, tellPulse(time));
      return;
    }
    rig.torus.visible = true;
    rig.torus.position.copy(anchor).setY(0.11);
    rig.torus.scale.setScalar(1.18);
  },
  paintOutcome({ rig, facts, owner, fraction, reducedMotion }) {
    const completed = facts.some(
      (fact) =>
        fact.kind === 'signature-changed' && fact.reason === 'completed',
    );
    if (!completed || !owner) return;
    const life = 1 - fraction;
    rig.main.opacity *= life * 0.58;
    rig.torus.visible = true;
    rig.torus.position.copy(owner).setY(0.1);
    rig.torus.scale.setScalar(0.72 + fraction * 1.45);
    if (reducedMotion) return;
    let segment = 0;
    for (const fact of facts) {
      if (fact.kind !== 'body-relocated') continue;
      const from = new THREE.Vector3(fact.from.x + 0.5, 0.07, fact.from.y + 0.5);
      const to = new THREE.Vector3(fact.to.x + 0.5, 0.07, fact.to.y + 0.5);
      const mesh = rig.segments[segment++];
      if (!mesh) break;
      lineAt(mesh, from, to, 0.12 * life + 0.035);
    }
  },
});
