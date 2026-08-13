import * as THREE from 'three';
import { defineArcSignatureWebGLPainter } from '../types';
import {
  hasConstructRemoval,
  paintPropFallback,
  setEnergyOpacity,
} from '../primitives';

export default defineArcSignatureWebGLPainter({
  signatureId: 'sentinel-seed',
  paint({ rig, signature, points, time, phase, persistentPropReady, reducedMotion, phaseAge }) {
    const anchor = points[0];
    // Nest's authored whole-body plant pose is the complete tell. The destination is
    // adjacent in the real stage, yet even the old floor ring/socket reached back under
    // the caster at overview scale and the pale seed shell then replaced that overlap.
    // Publish no extra tell mesh at all: once the replay says active, the one real sentry
    // grows from floor contact and becomes the destination read.
    if (!anchor || phase !== 'active') return;
    const deploy = reducedMotion ? 1 : Math.min(1, phaseAge / 1.1);
    paintPropFallback(
      rig,
      points,
      time,
      persistentPropReady,
      new THREE.Vector3(0.38, 0.5, 0.38),
    );
    // The Meshy sentry is already the authored object. Three dim socket rails close
    // beneath it; bulky procedural posts or a second seed body hide the turret.
    for (const [index, angle] of [0, Math.PI * 2 / 3, Math.PI * 4 / 3].entries()) {
      const rail = rig.segments[index]!;
      const outer = 0.66 - deploy * 0.08;
      const inner = 0.36;
      rail.visible = true;
      rail.position.set(
        anchor.x + Math.cos(angle) * (inner + outer) * 0.5,
        0.06,
        anchor.z + Math.sin(angle) * (inner + outer) * 0.5,
      );
      rail.rotation.y = -angle;
      rail.scale.set(outer - inner, 1, 1.3);
      setEnergyOpacity(rail, 0.08);
    }
    const status = rig.rings[1]!;
    status.visible = true;
    status.position.copy(anchor).setY(0.055);
    status.scale.setScalar(0.62 + Math.sin(time * 0.65) * (reducedMotion ? 0 : 0.022));
    setEnergyOpacity(status, signature.suppressed ? 0.035 : 0.075);
  },
  paintOutcome({ rig, priorSignature, facts, fraction }) {
    const origin = priorSignature?.positions[0];
    if (!hasConstructRemoval(facts) || !origin) return;
    // The ordinary projectile renderer already owns the one real Sentinel bolt. On
    // removal, recall the floor socket upward instead of manufacturing another shot.
    const anchor = new THREE.Vector3(origin.x + 0.5, 0.06, origin.y + 0.5);
    const life = 1 - fraction;
    const socket = rig.toruses[0]!;
    socket.visible = true;
    socket.position.copy(anchor).setY(0.06 + fraction * 1.45);
    socket.scale.setScalar(0.62 * (0.35 + life * 0.65));
    setEnergyOpacity(socket, life * 0.13);
    for (let index = 0; index < 3; index += 1) {
      const mote = rig.particles[index]!;
      const angle = index * Math.PI * 2 / 3 + fraction * Math.PI;
      mote.visible = true;
      mote.position.set(
        anchor.x + Math.cos(angle) * (0.34 * life),
        0.16 + fraction * (0.72 + index * 0.12),
        anchor.z + Math.sin(angle) * (0.34 * life),
      );
      mote.scale.setScalar(0.46 * life);
      setEnergyOpacity(mote, life * 0.16);
    }
  },
});
