import * as THREE from 'three';
import { defineArcSignatureWebGLPainter } from '../types';
import {
  hasConstructRemoval,
  paintPropFallback,
  setEnergyColour,
  setEnergyOpacity,
  targetRing,
} from '../primitives';

const SEED = '#78aeb3';

export default defineArcSignatureWebGLPainter({
  signatureId: 'sentinel-seed',
  paint({ rig, signature, points, time, phase, persistentPropReady, reducedMotion, phaseProgress, phaseAge }) {
    if (phase === 'active') {
      paintPropFallback(
        rig,
        points,
        time,
        persistentPropReady,
        new THREE.Vector3(0.38, 0.5, 0.38),
      );
    }
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
      // Keep the compact seed read at its destination. Nest's separate floor-safe body
      // pose now fixes the real nose clipping without deleting this approved treatment.
      const socket = rig.rings[0]!;
      socket.visible = true;
      socket.position.copy(anchor).setY(0.052);
      socket.scale.setScalar(0.44 + phaseProgress * 0.12);
      setEnergyOpacity(socket, 0.14);
      for (let index = 0; index < 3; index += 1) {
        const mote = rig.particles[index]!;
        const angle = index * Math.PI * 2 / 3 + phaseProgress * 0.8;
        mote.visible = !reducedMotion;
        mote.position.set(
          anchor.x + Math.cos(angle) * (0.24 - phaseProgress * 0.08),
          0.12 + phaseProgress * (0.16 + index * 0.035),
          anchor.z + Math.sin(angle) * (0.24 - phaseProgress * 0.08),
        );
        mote.scale.setScalar(0.38);
        setEnergyColour(mote, SEED);
        setEnergyOpacity(mote, 0.1);
      }
    }
    if (phase === 'active') {
      // The Meshy sentry is already the authored object. Three dim socket rails close
      // beneath it; bulky procedural posts hide the turret and read as unrelated rubble.
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
      if (deploy < 1) {
        const seedShell = rig.pod;
        seedShell.visible = true;
        seedShell.position.copy(anchor).setY(0.15 - deploy * 0.08);
        seedShell.scale.set(
          0.42 * (1 - deploy),
          Math.max(0.035, 0.26 * (1 - deploy)),
          0.42 * (1 - deploy),
        );
        rig.solid.color.set('#4d5c60');
        rig.solid.emissive.set(SEED);
        rig.solid.emissiveIntensity = 0.04;
        rig.solid.opacity = 0.9 * (1 - deploy);
      }
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
    // The ordinary attack renderer already owns the authoritative Sentinel traversal.
    // On removal, recall the floor socket upward instead of manufacturing another shot.
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
