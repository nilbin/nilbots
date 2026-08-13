import { defineArcSignatureWebGLPainter } from '../types';
import {
  anchorOf,
  areaBounds,
  easeInOutCubic,
  placeTravelHead,
  setEnergyColour,
  setEnergyOpacity,
  targetRing,
} from '../primitives';

export default defineArcSignatureWebGLPainter({
  signatureId: 'smoke-canister',
  paint({ rig, points, owner, target, time, phase, reducedMotion, phaseProgress, phaseAge }) {
    if (phase === 'in-flight' && points[0] && points.at(-1)) {
      const from = points[0]!;
      const to = points.at(-1)!;
      const progress = easeInOutCubic(phaseProgress);
      placeTravelHead(rig.payload, from, to, progress, 1.25, 1.15);
      rig.payload.scale.set(1.05, 0.72, 1.05);
      rig.solid.color.set('#697178');
      rig.solid.emissive.set('#263139');
      rig.solid.emissiveIntensity = 0.16;
      rig.payload.rotation.x = progress * Math.PI * 5;
      targetRing(rig, to, 1.08);
      setEnergyOpacity(rig.ring, 0.58);
      if (!reducedMotion) {
        for (let index = 0; index < 5; index += 1) {
          const trail = rig.particles[index]!;
          const p = Math.max(0, progress - index * 0.045);
          trail.visible = true;
          trail.position.lerpVectors(from, to, p);
          trail.position.y = 0.18 + Math.sin(p * Math.PI) * 0.72;
          trail.scale.setScalar(1.2 - index * 0.14);
          setEnergyColour(trail, '#96a4ad');
          setEnergyOpacity(trail, 0.24 - index * 0.035);
        }
      }
      return;
    }
    const anchor = anchorOf(points, owner, target);
    if (!anchor) return;
    const area = areaBounds(points.length > 0 ? points : [anchor]);
    const bloom = reducedMotion ? 1 : Math.min(1, phaseAge / 1.15);
    // A handful of overlapping low bodies reads as one rolling bank. Eighteen inflated
    // spheres read as a giant bunch of grapes and overwhelmed nearby bots at overview.
    const count = Math.min(reducedMotion ? 4 : 7, rig.wisps.length);
    for (let index = 0; index < count; index += 1) {
      const lobe = rig.wisps[index]!;
      const angle = index * 2.399963 + Math.sin(time * 0.17 + index) * 0.12;
      const radius = Math.sqrt((index + 0.5) / count) * 0.47 * bloom;
      lobe.visible = true;
      lobe.position.set(
        area.centre.x + Math.cos(angle) * area.width * radius,
        0.16 + (index % 4) * 0.095 + Math.sin(time * 0.55 + index) * 0.025,
        area.centre.z + Math.sin(angle) * area.depth * radius,
      );
      const scale = (0.82 + (index % 3) * 0.15) * (0.34 + bloom * 0.78);
      lobe.scale.set(scale * 2.85, scale * 1.28, scale * 2.85);
    }
    rig.smoke.color.set('#3b4145');
    rig.smoke.emissive.set('#111415');
    rig.smoke.emissiveIntensity = 0.04;
    rig.smoke.opacity = 0.68;
    // One broad matte mass stops the many shared lobe meshes looking like sparse sparks.
    rig.cloud.visible = true;
    rig.cloud.position.copy(area.centre).setY(0.29 + bloom * 0.13);
    rig.cloud.scale.set(
      area.width * (0.3 + bloom * 0.32),
      0.38 + bloom * 0.28,
      area.depth * (0.3 + bloom * 0.32),
    );
    targetRing(rig, area.centre, 1.2);
    setEnergyColour(rig.ring, '#83919a');
    setEnergyOpacity(rig.ring, 0.28);
  },
});
