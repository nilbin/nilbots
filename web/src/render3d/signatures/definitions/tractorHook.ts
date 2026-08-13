import * as THREE from 'three';
import { defineArcSignatureWebGLPainter } from '../types';
import type { ArcSignatureEffectRig } from '../types';
import {
  easeInOutCubic,
  solidBeamAt,
} from '../primitives';

const CABLE_WIDTH = 0.035;
const CABLE_LIFT = new THREE.Color('#dbe7ea');

function paintTeamCable(rig: ArcSignatureEffectRig, opacity: number): void {
  // The pooled main material already carries the owner's exact team accent. Keep enough
  // pale lift to read in fog without turning the cable into a dark grey wire.
  rig.solid.color.copy(rig.main.color).lerp(CABLE_LIFT, 0.12);
  rig.solid.emissive.copy(rig.main.color).multiplyScalar(0.2);
  rig.solid.emissiveIntensity = 0.14;
  rig.solid.opacity = opacity;
}

export default defineArcSignatureWebGLPainter({
  signatureId: 'tractor-hook',
  paint({ rig, points, owner, target, phase, phaseProgress, phaseAge }) {
    const destination = target ?? points.at(-1);
    if (!owner || !destination) return;
    // The public hook promise is a one-tick tell followed by a discrete launched fact; it
    // has no retained in-flight state. Extend only the narrow cable inside that tell. A
    // procedural grapple head was much heavier than Towline's authored winch at gameplay
    // scale, so the live treatment deliberately has no separate front object for now.
    const progress = phase === 'in-flight' || phase === 'active'
      ? easeInOutCubic(phaseProgress)
      : easeInOutCubic(Math.max(0, Math.min(1, phaseAge)));
    const travel = 0.08 + progress * 0.92;
    const cableEnd = new THREE.Vector3().lerpVectors(owner, destination, travel);
    cableEnd.y = 0.22;
    // One physical, team-tinted cable grows along the declared line. The generic
    // red strike aim is suppressed by arenaOverlays while this signature owns the tell;
    // an additive lane or separate hook silhouette would recreate the same visual clutter.
    solidBeamAt(
      rig.cable,
      owner.clone().setY(0.28),
      cableEnd,
      CABLE_WIDTH,
    );
    paintTeamCable(rig, 0.9);
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
    solidBeamAt(
      rig.cable,
      winch.clone().setY(0.28),
      targetAt.clone().setY(0.22),
      CABLE_WIDTH,
    );
    paintTeamCable(rig, life * 0.9);
  },
});
