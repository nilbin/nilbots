import * as THREE from 'three';
import { defineArcSignatureWebGLPainter } from '../types';
import { paintPropFallback, setEnergyOpacity, targetRing } from '../primitives';

export default defineArcSignatureWebGLPainter({
  signatureId: 'sentinel-seed',
  paint({ rig, signature, points, time, phase, persistentPropReady, reducedMotion, phaseProgress, phaseAge }) {
    paintPropFallback(
      rig,
      points,
      time,
      persistentPropReady,
      new THREE.Vector3(0.82, 1.15, 0.82),
    );
    const anchor = points[0];
    if (!anchor) return;
    const deploy = phase === 'tell'
      ? phaseProgress
      : reducedMotion
        ? 1
        : Math.min(1, phaseAge / 1.1);
    targetRing(rig, anchor, 1.3 + (1 - deploy) * 0.48);
    setEnergyOpacity(rig.ring, 0.12);
    if (phase === 'tell') {
      // A physical seed descends into the target socket; the full sentry replaces it only
      // when the replay says deployment is active.
      rig.pod.visible = true;
      rig.pod.position.copy(anchor).setY(3.05 - phaseProgress * 2.38);
      rig.pod.scale.set(1.1, 1.35, 1.1);
      rig.pod.rotation.y = phaseProgress * Math.PI * 2.5;
      rig.solid.color.set('#515955');
      rig.solid.emissive.copy(rig.main.color).multiplyScalar(0.12);
      rig.solid.opacity = 0.94;
    }
    if (phase === 'active') {
      // The Meshy sentry is already the authored object. Three dim socket rails close
      // beneath it; bulky procedural posts hid the turret and read as unrelated rubble.
      for (const [index, angle] of [0, Math.PI * 2 / 3, Math.PI * 4 / 3].entries()) {
        const rail = rig.segments[index]!;
        const outer = 1.18 - deploy * 0.34;
        const inner = 0.52;
        rail.visible = true;
        rail.position.set(
          anchor.x + Math.cos(angle) * (inner + outer) * 0.5,
          0.06,
          anchor.z + Math.sin(angle) * (inner + outer) * 0.5,
        );
        rail.rotation.y = -angle;
        rail.scale.set(outer - inner, 1, 2.1);
        setEnergyOpacity(rail, 0.11);
      }
    }
    const status = rig.rings[0]!;
    status.visible = true;
    status.position.copy(anchor).setY(0.055);
    status.scale.setScalar(0.92 + Math.sin(time * 0.65) * (reducedMotion ? 0 : 0.035));
    setEnergyOpacity(status, signature.suppressed ? 0.035 : 0.075);
  },
  paintOutcome({ rig, priorSignature, facts, fraction }) {
    const shot = facts.find((fact) => fact.kind === 'signature-damage');
    const origin = priorSignature?.positions[0];
    if (!shot || shot.kind !== 'signature-damage' || !origin) return;
    const from = new THREE.Vector3(origin.x + 0.5, 0.42, origin.y + 0.5);
    const to = new THREE.Vector3(shot.position.x + 0.5, 0.18, shot.position.y + 0.5);
    const projectile = rig.capsule;
    projectile.visible = true;
    projectile.position.lerpVectors(from, to, Math.min(1, fraction * 1.6));
    projectile.scale.setScalar(0.8);
  },
});
