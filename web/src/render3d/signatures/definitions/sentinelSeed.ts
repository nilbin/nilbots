import * as THREE from 'three';
import { defineArcSignatureWebGLPainter } from '../types';
import {
  hasConstructRemoval,
  paintPropFallback,
  setEnergyOpacity,
  targetRing,
} from '../primitives';

export default defineArcSignatureWebGLPainter({
  signatureId: 'sentinel-seed',
  paint({ rig, signature, points, time, phase, persistentPropReady, reducedMotion, phaseProgress, phaseAge }) {
    paintPropFallback(
      rig,
      points,
      time,
      persistentPropReady,
      new THREE.Vector3(0.38, 0.5, 0.38),
    );
    const anchor = points[0];
    if (!anchor) return;
    const deploy = phase === 'tell'
      ? phaseProgress
      : reducedMotion
        ? 1
        : Math.min(1, phaseAge / 1.1);
    targetRing(rig, anchor, 0.72 + (1 - deploy) * 0.18);
    setEnergyOpacity(rig.ring, 0.1);
    if (phase === 'tell') {
      // The active Meshy sentry itself descends on deployment. During the public tell this
      // small floor socket is the promise; an oversized procedural pod used to fall first
      // and then be replaced by a second object rising from below.
      const socket = rig.rings[0]!;
      socket.visible = true;
      socket.position.copy(anchor).setY(0.052);
      socket.scale.setScalar(0.44 + phaseProgress * 0.12);
      setEnergyOpacity(socket, 0.14);
    }
    if (phase === 'active') {
      // The Meshy sentry is already the authored object. Three dim socket rails close
      // beneath it; bulky procedural posts hid the turret and read as unrelated rubble.
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
    }
    const status = rig.rings[0]!;
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
