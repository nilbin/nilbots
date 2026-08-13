import { defineArcSignatureWebGLPainter } from '../types';
import {
  setEnergyColour,
  setEnergyOpacity,
  targetRing,
  tellPulse,
} from '../primitives';

const CORE_ARC = '#e6e5ef';

export default defineArcSignatureWebGLPainter({
  signatureId: 'arc-toss',
  paint({
    rig,
    points,
    owner,
    target,
    coreFlight,
    time,
  }) {
    const source = coreFlight?.source ?? owner ?? points[0];
    const destination = coreFlight?.destination ?? (source
      ? [...points, ...(target ? [target] : [])]
          .filter((point) => point.distanceToSquared(source) > 0.0001)
          .sort(
            (left, right) =>
              right.distanceToSquared(source) - left.distanceToSquared(source),
          )[0]
      : points.at(-1) ?? target);
    if (!destination) return;
    targetRing(rig, destination, tellPulse(time) * 1.28);
    setEnergyColour(rig.ring, CORE_ARC);
    setEnergyOpacity(rig.ring, 0.34);
    const inner = rig.rings[0]!;
    inner.visible = true;
    inner.position.copy(destination).setY(0.052);
    inner.scale.setScalar(0.72);
    setEnergyColour(inner, CORE_ARC);
    setEnergyOpacity(inner, 0.18);
    // The Arc Relay overlay owns the one authoritative sphere and its complete ballistic
    // motion. Earlier versions followed it with a torus and three orbiting motes; from the
    // gameplay camera that compact cluster read as a second ball performing the same toss.
    // Keep this painter destination-only while the real Core supplies the entire flight.
  },
});
