import * as THREE from 'three';
import { defineArcSignatureWebGLPainter } from '../types';
import { anchorOf, lineAt, setEnergyColour, setEnergyOpacity, solidBarAt } from '../primitives';

const PRESSURE = '#78aeb3';
const IRON = '#5e5549';

export default defineArcSignatureWebGLPainter({
  signatureId: 'kinetic-burst',
  paint({ rig, points, owner, target, phaseProgress }) {
    const anchor = anchorOf(points, owner, target);
    if (!anchor) return;
    const contraction = 1.72 - phaseProgress * 0.82;
    rig.hub.visible = true;
    rig.hub.position.copy(anchor).setY(0.12);
    rig.hub.scale.setScalar(1.36 + phaseProgress * 0.18);
    rig.solid.color.set(IRON);
    rig.solid.emissive.set('#182b2d');
    rig.solid.emissiveIntensity = 0.05;
    rig.solid.opacity = 0.94;
    // The tell is a mechanical iris, not a family of additive circles. A single dim
    // boundary locates it while four chunky petals slide into the rooted caster.
    const boundary = rig.rings[0]!;
    boundary.visible = true;
    boundary.position.copy(anchor).setY(0.055);
    boundary.scale.setScalar(contraction * 1.32);
    setEnergyColour(boundary, PRESSURE);
    setEnergyOpacity(boundary, 0.11);
    for (const [index, angle] of [0, Math.PI / 2, Math.PI, Math.PI * 1.5].entries()) {
      const fin = rig.fins[index]!;
      fin.visible = true;
      fin.position.set(
        anchor.x + Math.cos(angle) * contraction,
        0.29,
        anchor.z + Math.sin(angle) * contraction,
      );
      fin.rotation.y = -angle;
      fin.rotation.z = Math.PI / 2;
      fin.scale.set(1.18, 1.42, 1.18);
      solidBarAt(
        rig.bars[index]!,
        anchor.clone().add(new THREE.Vector3(Math.cos(angle) * 0.5, 0, Math.sin(angle) * 0.5)),
        anchor.clone().add(new THREE.Vector3(Math.cos(angle) * contraction, 0, Math.sin(angle) * contraction)),
        0.2,
        0.12,
      );
    }
  },
  paintOutcome({ rig, facts, owner, fraction }) {
    if (!facts.some((fact) => fact.kind === 'signature-changed' && fact.reason === 'completed') || !owner)
      return;
    const life = 1 - fraction;
    rig.hub.visible = true;
    rig.hub.position.copy(owner).setY(0.12);
    rig.hub.scale.setScalar(1.54 - fraction * 0.28);
    rig.solid.color.set(IRON);
    rig.solid.emissive.set('#182b2d');
    rig.solid.emissiveIntensity = 0.05;
    rig.solid.opacity = 0.94 * life;
    // One low, broad pressure torus is the resolve verb. There is deliberately no solid
    // expanding disc: that was the white bloom visible in stacked fights.
    rig.torus.visible = true;
    rig.torus.position.copy(owner).setY(0.095);
    rig.torus.scale.setScalar(0.76 + fraction * 3.25);
    setEnergyColour(rig.torus, PRESSURE);
    setEnergyOpacity(rig.torus, life * 0.16);
    let segment = 0;
    for (const fact of facts) {
      if (fact.kind !== 'body-relocated') continue;
      const from = new THREE.Vector3(fact.from.x + 0.5, 0.08, fact.from.y + 0.5);
      const to = new THREE.Vector3(fact.to.x + 0.5, 0.08, fact.to.y + 0.5);
      const wake = rig.segments[segment++];
      if (!wake) break;
      lineAt(wake, from, to, 0.16 * life + 0.05);
      setEnergyOpacity(wake, life * 0.58);
      solidBarAt(rig.bars[segment - 1]!, from, to, 0.16 * life + 0.07, 0.1);
      const head = rig.fins[segment - 1]!;
      head.visible = true;
      head.position.lerpVectors(from, to, fraction).setY(0.28);
      head.rotation.z = Math.PI / 2;
      head.rotation.y = -Math.atan2(to.z - from.z, to.x - from.x);
      head.scale.setScalar(1.05 * life + 0.22);
    }
    rig.solid.opacity = life * 0.78;
  },
});
