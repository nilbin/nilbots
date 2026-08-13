import * as THREE from 'three';
import { defineArcSignatureWebGLPainter } from '../types';
import {
  easeInOutCubic,
  lineAt,
  placeTravelHead,
  setEnergyColour,
  setEnergyOpacity,
  solidBarAt,
  solidBeamAt,
  targetRing,
} from '../primitives';

const CABLE = '#514b45';

export default defineArcSignatureWebGLPainter({
  signatureId: 'tractor-hook',
  paint({ rig, points, owner, target, phase, phaseProgress, phaseAge }) {
    const destination = target ?? points.at(-1);
    if (!owner || !destination) return;
    // The public hook promise is a one-tick tell followed by a discrete launched fact; it
    // has no retained in-flight state. Travel the head down the frozen line inside that
    // tell, then let the outcome fact decide whether any cable/latch survives.
    const progress = phase === 'in-flight' || phase === 'active'
      ? easeInOutCubic(phaseProgress)
      : easeInOutCubic(Math.max(0, Math.min(1, phaseAge)));
    const line = rig.segments[0]!;
    // The declared lane visibly narrows behind the travelling head. A fully lit static
    // line looked like an ordinary beam and hid the one animation this signature owns.
    const laneEnd = new THREE.Vector3().lerpVectors(
      owner,
      destination,
      0.18 + progress * 0.82,
    );
    lineAt(line, owner, laneEnd, phase === 'tell' ? 0.18 : 0.12, 0.2);
    setEnergyColour(line, phase === 'tell' ? '#78aeb3' : CABLE);
    setEnergyOpacity(line, phase === 'tell' ? 0.2 : 0.1);
    solidBarAt(rig.bars[0]!, owner, laneEnd, phase === 'tell' ? 0.15 : 0.11, 0.13);
    solidBeamAt(
      rig.cable,
      owner.clone().setY(0.34),
      laneEnd.clone().setY(0.34),
      0.13,
    );
    rig.spool.visible = true;
    rig.spool.position.copy(owner).setY(0.46);
    rig.spool.scale.setScalar(1.55);
    rig.spool.rotation.z = progress * Math.PI * 2;
    const head = rig.payload;
    const at = placeTravelHead(head, owner, destination, progress, 0.22, 0.82);
    head.scale.set(1.35, 0.86, 1.35);
    rig.solid.color.set('#4c4b47');
    rig.solid.emissive.set('#203439');
    rig.solid.emissiveIntensity = 0.16;
    rig.solid.opacity = 0.96;
    for (const [index, side] of [-1, 1].entries()) {
      const jaw = rig.segments[index + 1]!;
      const yaw = -Math.atan2(destination.z - owner.z, destination.x - owner.x);
      const forward = new THREE.Vector3(Math.cos(-yaw), 0, -Math.sin(-yaw));
      const normal = new THREE.Vector3(-forward.z, 0, forward.x);
      lineAt(
        jaw,
        at.clone().addScaledVector(normal, side * 0.05),
        at.clone().addScaledVector(forward, 0.18).addScaledVector(normal, side * 0.14),
        0.2,
        at.y,
      );
      setEnergyColour(jaw, '#78aeb3');
      setEnergyOpacity(jaw, 0.3);
    }
    targetRing(rig, destination, 1.42);
    setEnergyColour(rig.ring, '#78aeb3');
    setEnergyOpacity(rig.ring, 0.26);
    rig.plate.visible = true;
    rig.plate.position.copy(destination).setY(0.055);
    rig.plate.scale.setScalar(0.88);
    rig.solid.opacity = 0.9;
  },
  paintOutcome({ rig, facts, fraction, priorSignature, owner }) {
    const relocation = facts.find((fact) => fact.kind === 'body-relocated');
    const launch = facts.find(
      (fact) => fact.kind === 'signature-changed' && fact.reason === 'launched',
    );
    if ((!relocation || relocation.kind !== 'body-relocated') && !launch) return;
    const from = relocation && relocation.kind === 'body-relocated'
      ? new THREE.Vector3(relocation.from.x + 0.5, 0.16, relocation.from.y + 0.5)
      : owner;
    const to = relocation && relocation.kind === 'body-relocated'
      ? new THREE.Vector3(relocation.to.x + 0.5, 0.16, relocation.to.y + 0.5)
      : priorSignature?.positions[0]
        ? new THREE.Vector3(
            priorSignature.positions[0].x + 0.5,
            0.16,
            priorSignature.positions[0].y + 0.5,
          )
        : undefined;
    if (!from || !to) return;
    const life = 1 - fraction;
    const headAt = new THREE.Vector3().lerpVectors(from, to, easeInOutCubic(fraction));
    const cable = rig.segments[0]!;
    lineAt(cable, from, headAt, 0.17 + life * 0.08, 0.2);
    setEnergyColour(cable, CABLE);
    setEnergyOpacity(cable, life * 0.12);
    solidBarAt(rig.bars[0]!, from, headAt, 0.14 + life * 0.06, 0.13);
    solidBeamAt(
      rig.cable,
      from.clone().setY(0.34),
      headAt.clone().setY(0.34),
      0.13,
    );
    rig.spool.visible = true;
    rig.spool.position.copy(to).setY(0.46);
    rig.spool.scale.setScalar(1.55);
    const head = rig.payload;
    placeTravelHead(head, from, to, easeInOutCubic(fraction), 0.14, 0.82);
    head.scale.set(1.35, 0.86, 1.35);
    rig.solid.color.set('#4c4b47');
    rig.solid.emissive.set('#203439');
    rig.solid.emissiveIntensity = 0.16;
    rig.solid.opacity = life * 0.96;
  },
});
