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
    rig.solid.opacity = 0.9;
  },
  paintOutcome({
    rig,
    facts,
    fraction,
    priorSignature,
    owner,
    target,
    priorTarget,
    time,
    sourceTick,
  }) {
    const relocation = facts.find((fact) => fact.kind === 'body-relocated');
    const launch = facts.find(
      (fact) => fact.kind === 'signature-changed' && fact.reason === 'launched',
    );
    if ((!relocation || relocation.kind !== 'body-relocated') && !launch) return;
    // The launch owns the one-tick latch hold and the relocation owns the one-tick
    // winch. Retaining either as a generic 1.8-tick afterimage leaves a cable attached
    // after the pulled body has resumed ordinary movement.
    if (time - sourceTick >= 1) return;
    const from = relocation && relocation.kind === 'body-relocated'
      ? new THREE.Vector3(relocation.from.x + 0.5, 0.16, relocation.from.y + 0.5)
      : owner;
    const to = relocation && relocation.kind === 'body-relocated'
      ? new THREE.Vector3(relocation.to.x + 0.5, 0.16, relocation.to.y + 0.5)
      : priorTarget
        ? priorTarget.clone().setY(0.16)
        : priorSignature?.positions.at(-1)
        ? new THREE.Vector3(
            priorSignature.positions.at(-1)!.x + 0.5,
            0.16,
            priorSignature.positions.at(-1)!.y + 0.5,
          )
        : undefined;
    if (!from || !to) return;
    const life = 1 - fraction;
    const targetAt = relocation && relocation.kind === 'body-relocated'
      ? target ?? new THREE.Vector3().lerpVectors(
          from,
          to,
          easeInOutCubic(Math.max(0, Math.min(1, time - sourceTick))),
        )
      : to;
    const winch = owner ?? to;
    const cable = rig.segments[0]!;
    lineAt(cable, winch, targetAt, 0.17 + life * 0.08, 0.2);
    setEnergyColour(cable, CABLE);
    setEnergyOpacity(cable, life * 0.12);
    solidBarAt(rig.bars[0]!, winch, targetAt, 0.14 + life * 0.06, 0.13);
    solidBeamAt(
      rig.cable,
      winch.clone().setY(0.34),
      targetAt.clone().setY(0.34),
      0.13,
    );
    const head = rig.payload;
    head.visible = true;
    head.position.copy(targetAt).setY(0.22);
    head.scale.set(1.35, 0.86, 1.35);
    rig.solid.color.set('#4c4b47');
    rig.solid.emissive.set('#203439');
    rig.solid.emissiveIntensity = 0.16;
    rig.solid.opacity = life * 0.96;
  },
});
