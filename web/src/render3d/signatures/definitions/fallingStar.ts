import * as THREE from 'three';
import { defineArcSignatureWebGLPainter } from '../types';
import {
  anchorOf,
  cardinalArms,
  easeInOutCubic,
  lineAt,
  setEnergyColour,
  setEnergyOpacity,
  targetRing,
} from '../primitives';

const IMPACT_AMBER = '#d79a4a';

export default defineArcSignatureWebGLPainter({
  signatureId: 'falling-star',
  paint({ rig, points, owner, target, time, phaseAge }) {
    const anchor = points[0] ?? anchorOf(points, owner, target);
    if (!anchor) return;
    // The retained tell spans two ticks. Keep the descent clock local to its public phase
    // so a seek and continuous playback agree and the capsule visibly crosses the frame.
    const charge = easeInOutCubic(Math.max(0, Math.min(1, phaseAge / 2)));
    targetRing(rig, anchor, 1.08 - charge * 0.2);
    setEnergyOpacity(rig.ring, 0.4);
    cardinalArms(rig, anchor, 0.22 + charge * 0.22, 0.94, 0.16, 0, 0.1);
    for (let index = 0; index < 4; index += 1) {
      const arm = rig.segments[index]!;
      setEnergyColour(arm, IMPACT_AMBER);
      setEnergyOpacity(arm, 0.3 + charge * 0.18);
    }
    const shadow = rig.disc;
    shadow.visible = true;
    shadow.position.copy(anchor).setY(0.038);
    shadow.scale.setScalar(0.5 + charge * 0.52);
    rig.fill.color.set('#17130f');
    rig.fill.opacity = 0.3 + charge * 0.22;
    for (let index = 0; index < 3; index += 1) {
      const halo = rig.toruses[index]!;
      halo.visible = true;
      halo.position.copy(anchor).setY(0.12 + index * 0.035);
      halo.scale.setScalar(1.05 + index * 0.42 - charge * (0.42 + index * 0.08));
      setEnergyColour(halo, IMPACT_AMBER);
      setEnergyOpacity(halo, 0.16 - index * 0.025 + charge * 0.09);
    }
    // A translucent faceted shard preserves the vertical arrival without dropping another
    // opaque generic 3D object into the middle of the aura.
    rig.cloud.visible = true;
    rig.cloud.position.copy(anchor).setY(3.05 - charge * 2.5);
    rig.cloud.scale.set(0.52, 1.48, 0.52);
    rig.cloud.rotation.y = time * 0.42;
    rig.smoke.color.set(IMPACT_AMBER);
    rig.smoke.emissive.set('#6b3918');
    rig.smoke.emissiveIntensity = 0.08;
    rig.smoke.opacity = 0.22;
  },
  paintOutcome({ rig, priorSignature, facts, fraction, owner, reducedMotion }) {
    const completed = facts.some(
      (fact) => fact.kind === 'signature-changed' && fact.reason === 'completed',
    );
    if (!completed) return;
    const points = priorSignature?.positions.map(
      (point) => new THREE.Vector3(point.x + 0.5, 0.065, point.y + 0.5),
    ) ?? [];
    const anchor = points[0] ?? owner;
    if (!anchor) return;
    const life = 1 - fraction;
    rig.cloud.visible = fraction < 0.34;
    rig.cloud.position.copy(anchor).setY(0.28 + Math.max(0, 1 - fraction / 0.34) * 0.42);
    rig.cloud.scale.set(0.74 + fraction * 0.32, 0.72, 0.74 + fraction * 0.32);
    rig.smoke.color.set(IMPACT_AMBER);
    rig.smoke.emissive.set('#6b3918');
    rig.smoke.emissiveIntensity = 0.06;
    rig.smoke.opacity = life * 0.18;
    const impactProgress = Math.min(1, fraction * 1.45);
    for (let index = 1; index < points.length; index += 1) {
      const ray = rig.segments[index - 1]!;
      const end = new THREE.Vector3().lerpVectors(anchor, points[index]!, impactProgress);
      lineAt(ray, anchor, end, 0.42 * life + 0.12, 0.12);
      setEnergyColour(ray, IMPACT_AMBER);
      setEnergyOpacity(ray, life * 0.42);
    }
    const torus = rig.torus;
    torus.visible = true;
    torus.position.copy(anchor).setY(0.09);
    torus.scale.setScalar(0.72 + fraction * 2.6);
    setEnergyColour(torus, IMPACT_AMBER);
    setEnergyOpacity(torus, life * 0.34);
    rig.disc.visible = true;
    rig.disc.position.copy(anchor).setY(0.055);
    rig.disc.scale.setScalar(0.8 + fraction * 2.3);
    rig.fill.color.set(IMPACT_AMBER);
    rig.fill.opacity = life * 0.3;
    const count = reducedMotion ? 4 : 10;
    for (let index = 0; index < count; index += 1) {
      const dust = rig.wisps[index]!;
      const angle = (index / count) * Math.PI * 2;
      const radius = 0.25 + fraction * (0.55 + (index % 3) * 0.14);
      dust.visible = true;
      dust.position.set(
        anchor.x + Math.cos(angle) * radius,
        0.12 + Math.sin(fraction * Math.PI) * 0.18,
        anchor.z + Math.sin(angle) * radius,
      );
      dust.scale.setScalar((0.7 + (index % 3) * 0.18) * life);
    }
    rig.smoke.color.set(IMPACT_AMBER);
    rig.smoke.opacity = life * 0.34;
  },
});
