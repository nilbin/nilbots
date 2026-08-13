import * as THREE from 'three';
import { defineArcSignatureWebGLPainter } from '../types';
import type { ArcSignatureEffectRig } from '../types';
import {
  setEnergyColour,
  setEnergyOpacity,
  solidBeamAt,
} from '../primitives';

const AIM = '#547985';
const LASER_HALO = '#3e9eaa';
const LASER_CORE = '#d1f0eb';
const LASER_HEIGHT = 0.16;

/** Keep the frozen laser just above the board without changing its topology. */
function raisedPath(
  path: readonly THREE.Vector3[],
  height: number,
): THREE.Vector3[] {
  return path.map((point) => point.clone().setY(height));
}

function completeRay(
  points: readonly THREE.Vector3[],
  owner: THREE.Vector3 | undefined,
  target?: THREE.Vector3,
): THREE.Vector3[] {
  const path = [...points];
  if (owner && !path.some((point) => point.distanceToSquared(owner) < 0.0001))
    path.unshift(owner);
  if (path.length < 2 && target) path.push(target);
  return raisedPath(path, LASER_HEIGHT);
}

function laserBeam(
  rig: ArcSignatureEffectRig,
  from: THREE.Vector3,
  to: THREE.Vector3,
  opacity: number,
): void {
  // Two restrained coaxial traces read as a laser at the strategic camera: a broad,
  // low-alpha colour halo and a narrow pale core. Neither is a solid floor slab, and the
  // combined luminance stays far below the old full-accent additive rail.
  const halo = rig.segments[0]!;
  solidBeamAt(halo, from, to, 0.105);
  setEnergyColour(halo, LASER_HALO);
  setEnergyOpacity(halo, opacity * 0.24);
  const core = rig.segments[1]!;
  solidBeamAt(core, from, to, 0.035);
  setEnergyColour(core, LASER_CORE);
  setEnergyOpacity(core, opacity * 0.68);
}

export default defineArcSignatureWebGLPainter({
  signatureId: 'rail-line',
  paint({ rig, points, owner, target, phaseProgress }) {
    const path = completeRay(points, owner, target);
    if (path.length < 2) return;
    const from = path[0]!;
    const to = path.at(-1)!;

    // The wind-up is a thin raised sightline that converges toward the muzzle. It keeps
    // the promised lane readable without pretending the damaging laser already fired.
    for (let index = 0; index < path.length - 1; index += 1) {
      const sight = rig.segments[index]!;
      solidBeamAt(sight, path[index]!, path[index + 1]!, 0.026);
      setEnergyColour(sight, AIM);
      setEnergyOpacity(sight, 0.055 + phaseProgress * 0.055);
    }
    const charge = rig.rings[0]!;
    charge.visible = true;
    charge.position.copy(from).setY(LASER_HEIGHT);
    charge.quaternion.setFromUnitVectors(
      new THREE.Vector3(0, 1, 0),
      new THREE.Vector3().subVectors(to, from).normalize(),
    );
    charge.scale.setScalar(0.19 + phaseProgress * 0.09);
    setEnergyColour(charge, LASER_CORE);
    setEnergyOpacity(charge, 0.08 + phaseProgress * 0.08);
  },
  paintOutcome({ rig, priorSignature, facts, owner, fraction }) {
    if (!facts.some(
      (fact) => fact.kind === 'signature-changed' && fact.reason === 'completed',
    )) return;
    const path = completeRay(
      priorSignature?.positions.map(
        (point) => new THREE.Vector3(point.x + 0.5, 0, point.y + 0.5),
      ) ?? [],
      owner,
    );
    if (path.length < 2) return;
    const life = Math.max(0, 1 - fraction * 1.35);
    const from = path[0]!;
    const to = path.at(-1)!;
    laserBeam(rig, from, to, life);

    const impact = facts.find((fact) => fact.kind === 'signature-damage');
    if (impact?.kind === 'signature-damage') {
      const ring = rig.toruses[0]!;
      ring.visible = true;
      ring.position.set(
        impact.position.x + 0.5,
        LASER_HEIGHT,
        impact.position.y + 0.5,
      );
      ring.quaternion.setFromUnitVectors(
        new THREE.Vector3(0, 1, 0),
        new THREE.Vector3().subVectors(to, from).normalize(),
      );
      ring.scale.setScalar(0.2 + fraction * 0.26);
      setEnergyColour(ring, LASER_CORE);
      setEnergyOpacity(ring, life * 0.18);
    }
  },
});
