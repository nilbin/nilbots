import * as THREE from 'three';
import { defineArcSignatureWebGLPainter } from '../types';
import {
  anchorOf,
  easeInOutCubic,
  fieldBase,
  hasConstructRemoval,
  placeTravelHead,
  setEnergyColour,
  setEnergyOpacity,
  solidBarAt,
} from '../primitives';

const SURVEY = '#86bec0';

export default defineArcSignatureWebGLPainter({
  signatureId: 'survey-flare',
  paint({ rig, points, owner, target, time, phase, persistentPropReady, reducedMotion, phaseProgress, phaseAge }) {
    if (phase === 'in-flight' && points[0] && points.at(-1)) {
      const from = points[0]!;
      const to = points.at(-1)!;
      const progress = easeInOutCubic(phaseProgress);
      if (!persistentPropReady) {
        placeTravelHead(rig.payload, from, to, progress, 1.15, 1.05);
        rig.payload.scale.set(0.92, 0.72, 0.92);
        rig.solid.color.set('#59676b');
        rig.solid.emissive.set('#213437');
        rig.solid.emissiveIntensity = 0.06;
        rig.payload.rotation.x = progress * Math.PI * 4;
      }
      if (!reducedMotion) {
        for (let index = 0; index < 4; index += 1) {
          const echo = rig.particles[index]!;
          const p = Math.max(0, progress - index * 0.055);
          echo.visible = true;
          echo.position.lerpVectors(from, to, p);
          echo.position.y = 0.18 + Math.sin(p * Math.PI) * 0.78;
          echo.scale.setScalar(1.25 - index * 0.17);
          setEnergyOpacity(echo, 0.36 - index * 0.06);
        }
      }
      return;
    }
    const anchor = anchorOf(points, owner, target);
    if (!anchor) return;
    const bloom = reducedMotion ? 1 : Math.min(1, phaseAge / 1.15);
    const area = fieldBase(rig, points, anchor, 0.72 + bloom * 0.28);
    rig.fill.color.set('#18353a');
    rig.fill.opacity = 0.024;
    setEnergyColour(rig.ring, SURVEY);
    setEnergyOpacity(rig.ring, 0.09);
    const horizon = rig.rings[0]!;
    horizon.visible = true;
    horizon.position.copy(area.centre).setY(0.06);
    horizon.scale.set(area.width * (0.5 + bloom * 0.5), 1, area.depth * (0.5 + bloom * 0.5));
    setEnergyColour(horizon, SURVEY);
    setEnergyOpacity(horizon, 0.065);
    const ticks = Math.min(reducedMotion ? 6 : 12, rig.markers.length);
    for (let index = 0; index < ticks; index += 1) {
      const angle = (index / ticks) * Math.PI * 2;
      const marker = rig.markers[index]!;
      marker.visible = true;
      marker.position.set(
        area.centre.x + Math.cos(angle) * area.width * 0.48 * bloom,
        0.075,
        area.centre.z + Math.sin(angle) * area.depth * 0.48 * bloom,
      );
      marker.scale.set(0.5, 0.18, 1.05);
      marker.rotation.y = -angle;
      setEnergyColour(marker, SURVEY);
      setEnergyOpacity(marker, 0.16);
    }
    // The landed flare is a small survey instrument: socket, mast, three rotating arms
    // and a dim lens. The former tall additive beam made it look like an objective beacon
    // and still failed to show an actual deployment at the gameplay camera.
    if (!persistentPropReady) {
      rig.hub.visible = true;
      rig.hub.position.copy(area.centre).setY(-0.08 + bloom * 0.18);
      rig.hub.scale.setScalar(0.62 + bloom * 0.64);
      rig.payload.visible = true;
      rig.payload.position.copy(area.centre).setY(-0.32 + bloom * 0.86);
      rig.payload.scale.set(0.66, 1.2 + bloom * 0.7, 0.66);
      rig.payload.rotation.y = time * 0.28;
      rig.solid.color.set('#526166');
      rig.solid.emissive.set('#1b3438');
      rig.solid.emissiveIntensity = 0.06;
      rig.solid.opacity = 0.96;
      for (const [index, angleOffset] of [0, Math.PI * 2 / 3, Math.PI * 4 / 3].entries()) {
        const angle = angleOffset + time * 0.24;
        const from = area.centre.clone().add(
          new THREE.Vector3(Math.cos(angle) * 0.18, 0, Math.sin(angle) * 0.18),
        );
        const to = area.centre.clone().add(
          new THREE.Vector3(
            Math.cos(angle) * (0.36 + bloom * 0.34),
            0,
            Math.sin(angle) * (0.36 + bloom * 0.34),
          ),
        );
        solidBarAt(rig.bars[index]!, from, to, 0.13, 0.38 + index * 0.025);
      }
      const lens = rig.toruses[0]!;
      lens.visible = true;
      lens.position.copy(area.centre).setY(0.92);
      lens.scale.setScalar(0.48 + Math.sin(time * 0.9) * 0.025);
      setEnergyColour(lens, SURVEY);
      setEnergyOpacity(lens, 0.11);
    }
  },
  paintOutcome({ rig, priorSignature, facts, fraction, reducedMotion }) {
    if (!hasConstructRemoval(facts)) return;
    const positions = priorSignature?.positions.map(
      (point) => new THREE.Vector3(point.x + 0.5, 0.065, point.y + 0.5),
    ) ?? [];
    const anchor = positions[0];
    if (!anchor) return;
    const life = 1 - fraction;
    const area = fieldBase(rig, positions, anchor, 1 - fraction * 0.58);
    rig.fill.color.set('#18353a');
    rig.fill.opacity = life * 0.018;
    setEnergyColour(rig.ring, SURVEY);
    setEnergyOpacity(rig.ring, life * 0.07);
    // Fold the survey arms back into the socket and lower the mast. The operation is
    // already gone mechanically; this translucent after-motion only explains the exit.
    rig.hub.visible = true;
    rig.hub.position.copy(area.centre).setY(0.1 - fraction * 0.16);
    rig.hub.scale.setScalar(Math.max(0.08, 1.26 * life));
    rig.payload.visible = true;
    rig.payload.position.copy(area.centre).setY(0.54 - fraction * 0.7);
    rig.payload.scale.set(0.66 * life, 1.9 * Math.max(0.08, life), 0.66 * life);
    rig.solid.color.set('#526166');
    rig.solid.emissive.set('#1b3438');
    rig.solid.emissiveIntensity = 0.025;
    rig.solid.opacity = life * 0.78;
    const arms = reducedMotion ? 2 : 3;
    for (let index = 0; index < arms; index += 1) {
      const angle = index * Math.PI * 2 / arms + fraction * 0.35;
      const reach = 0.7 * life;
      solidBarAt(
        rig.bars[index]!,
        area.centre.clone().add(new THREE.Vector3(
          Math.cos(angle) * 0.16,
          0,
          Math.sin(angle) * 0.16,
        )),
        area.centre.clone().add(new THREE.Vector3(
          Math.cos(angle) * (0.16 + reach),
          0,
          Math.sin(angle) * (0.16 + reach),
        )),
        0.13,
        0.38 - fraction * 0.24,
      );
    }
  },
});
