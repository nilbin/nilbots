import { defineArcSignatureWebGLPainter } from '../types';
import {
  anchorOf,
  fieldBase,
  setEnergyColour,
  setEnergyOpacity,
  tellPulse,
} from '../primitives';

const NULL_VIOLET = '#77618f';
const NULL_DARK = '#100e19';

export default defineArcSignatureWebGLPainter({
  signatureId: 'null-field',
  paint({ rig, points, owner, target, phase, time, reducedMotion, phaseProgress }) {
    const anchor = anchorOf(points, owner, target);
    if (!anchor) return;
    const contraction = phase === 'tell' ? 1.28 - phaseProgress * 0.42 : 1;
    const area = fieldBase(rig, points, anchor, contraction * tellPulse(time));
    // Null is an absence across an area, not an object placed in its centre. The former
    // opaque hub hid bots and made every field look as though it contained a generic prop.
    rig.fill.color.set(NULL_DARK);
    rig.fill.opacity = phase === 'tell' ? 0.075 : 0.24;
    rig.disc.position.y = 0.038;
    setEnergyColour(rig.ring, NULL_VIOLET);
    setEnergyOpacity(rig.ring, phase === 'tell' ? 0.2 : 0.09);
    // Keep this entirely on the floor plane. The filled disc and nested flat rings form
    // the honest shallow lens without introducing a bright, view-dependent blade.
    const waves = reducedMotion ? 1 : 3;
    for (let index = 0; index < waves; index += 1) {
      const wave = rig.rings[index]!;
      const drift = phase === 'tell'
        ? 0.82 + index * 0.16
        : 0.62 + ((time * 0.16 + index / waves) % 1) * 0.56;
      wave.visible = true;
      wave.position.copy(area.centre).setY(0.044 + index * 0.006);
      wave.scale.set(area.width * drift, 1, area.depth * drift);
      setEnergyColour(wave, NULL_VIOLET);
      setEnergyOpacity(wave, phase === 'tell' ? 0.09 : 0.035);
    }
    const ticks = reducedMotion ? 4 : 8;
    for (let index = 0; index < ticks; index += 1) {
      const angle = (index / ticks) * Math.PI * 2;
      const marker = rig.markers[index]!;
      marker.visible = true;
      marker.position.set(
        area.centre.x + Math.cos(angle) * area.width * 0.45 * contraction,
        0.064,
        area.centre.z + Math.sin(angle) * area.depth * 0.45 * contraction,
      );
      marker.scale.set(0.34, 0.09, 0.54);
      marker.rotation.y = -angle;
      setEnergyColour(marker, NULL_VIOLET);
      setEnergyOpacity(marker, phase === 'tell' ? 0.18 : 0.11);
    }
  },
});
