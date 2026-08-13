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
  paint({ rig, points, owner, target, phase, reducedMotion, phaseProgress, phaseAge }) {
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
    // One translucent, squashed volume says "vision is obscured here" without building a
    // bunch of opaque grapes or hiding the bots the spectator is trying to follow.
    rig.smoke.color.set('#68747b');
    rig.smoke.emissive.set('#171d20');
    rig.smoke.emissiveIntensity = 0.025;
    rig.smoke.opacity = phase === 'tell' ? 0.09 : 0.16;
    rig.cloud.visible = true;
    rig.cloud.position.copy(area.centre).setY(0.3 + bloom * 0.08);
    rig.cloud.scale.set(
      area.width * (0.25 + bloom * 0.43),
      0.34 + bloom * 0.42,
      area.depth * (0.25 + bloom * 0.43),
    );
    targetRing(rig, area.centre, 1.2);
    setEnergyColour(rig.ring, '#83919a');
    setEnergyOpacity(rig.ring, 0.14);
  },
});
