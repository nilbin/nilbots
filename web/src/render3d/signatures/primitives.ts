import * as THREE from 'three';
import type {
  ArcSignature,
  ArcSignatureEffectRig,
} from './types';

export function anchorOf(
  points: readonly THREE.Vector3[],
  owner: THREE.Vector3 | undefined,
  target: THREE.Vector3 | undefined,
): THREE.Vector3 | undefined {
  return points[0] ?? target ?? owner;
}

export function operationProgress(
  signature: ArcSignature,
  time: number,
): number {
  if (signature.completesAtTick === null) return 0;
  const duration = Math.max(1, signature.completesAtTick - signature.startedTick);
  return clamp01((time - signature.startedTick) / duration);
}

export function easeOutCubic(value: number): number {
  const t = clamp01(value);
  return 1 - (1 - t) ** 3;
}

export function easeInOutCubic(value: number): number {
  const t = clamp01(value);
  return t < 0.5 ? 4 * t ** 3 : 1 - (-2 * t + 2) ** 3 / 2;
}

/** A local progress clock that never depends on having rendered a prior frame. */
export function phaseProgress(signature: ArcSignature, time: number): number {
  const end = signature.phase === 'tell' || signature.phase === 'in-flight'
    ? signature.completesAtTick
    : signature.endsAtTick;
  if (end === null) return clamp01(time - Math.floor(time));
  return clamp01(
    (time - signature.startedTick) / Math.max(1, end - signature.startedTick),
  );
}

export function risingSettle(time: number, startedTick: number): number {
  return easeOutCubic(clamp01((time - startedTick) / 1.35));
}

export function tellPulse(time: number): number {
  return 0.92 + 0.08 * Math.sin(time * Math.PI * 3);
}

export function lineAt(
  mesh: THREE.Mesh,
  from: THREE.Vector3,
  to: THREE.Vector3,
  width: number,
  height = 0.07,
): void {
  const dx = to.x - from.x;
  const dz = to.z - from.z;
  const length = Math.hypot(dx, dz);
  mesh.visible = length > 0.001;
  mesh.position.set((from.x + to.x) / 2, height, (from.z + to.z) / 2);
  mesh.rotation.y = -Math.atan2(dz, dx);
  mesh.scale.set(length, 1, width / 0.055);
}

/** Place a matte solid bar along the arena floor. Unlike additive lines this survives fog. */
export function solidBarAt(
  mesh: THREE.Mesh,
  from: THREE.Vector3,
  to: THREE.Vector3,
  width: number,
  height = 0.11,
): void {
  const dx = to.x - from.x;
  const dz = to.z - from.z;
  const length = Math.hypot(dx, dz);
  mesh.visible = length > 0.001;
  mesh.position.set((from.x + to.x) / 2, height, (from.z + to.z) / 2);
  mesh.rotation.y = -Math.atan2(dz, dx);
  mesh.scale.set(length, 1, width);
}

/** A solid bar in full 3D, used for cables and vertical rails rather than floor lanes. */
export function solidBeamAt(
  mesh: THREE.Mesh,
  from: THREE.Vector3,
  to: THREE.Vector3,
  width: number,
): void {
  const direction = new THREE.Vector3().subVectors(to, from);
  const length = direction.length();
  mesh.visible = length > 0.001;
  mesh.position.copy(from).add(to).multiplyScalar(0.5);
  mesh.scale.set(length, Math.max(1, width / 0.018), width / 0.055);
  mesh.quaternion.setFromUnitVectors(
    new THREE.Vector3(1, 0, 0),
    direction.normalize(),
  );
}

export function setEnergyOpacity(mesh: THREE.Mesh, opacity: number): void {
  (mesh.material as THREE.MeshBasicMaterial).opacity = clamp01(opacity);
}

export function setEnergyColour(
  mesh: THREE.Mesh,
  colour: THREE.ColorRepresentation,
): void {
  (mesh.material as THREE.MeshBasicMaterial).color.set(colour);
}

export function polyline(
  rig: ArcSignatureEffectRig,
  points: readonly THREE.Vector3[],
  width: number,
  start = 0,
): number {
  let used = start;
  for (let index = 1; index < points.length; index += 1) {
    const segment = rig.segments[used++];
    if (!segment) break;
    lineAt(segment, points[index - 1]!, points[index]!, width);
  }
  return used;
}

export function targetRing(
  rig: ArcSignatureEffectRig,
  position: THREE.Vector3,
  scale: number,
): void {
  rig.ring.visible = true;
  rig.ring.position.copy(position).setY(0.045);
  rig.ring.scale.setScalar(scale);
}

/**
 * Four broad floor arms around a point. These survive the strategic camera where a thin
 * circular reticle becomes indistinguishable from ordinary selection and objective rings.
 */
export function cardinalArms(
  rig: ArcSignatureEffectRig,
  position: THREE.Vector3,
  inner: number,
  outer: number,
  width: number,
  start = 0,
  height = 0.075,
): number {
  let used = start;
  for (const angle of [0, Math.PI / 2, Math.PI, Math.PI * 1.5]) {
    const from = position.clone().add(
      new THREE.Vector3(Math.cos(angle) * inner, 0, Math.sin(angle) * inner),
    );
    const to = position.clone().add(
      new THREE.Vector3(Math.cos(angle) * outer, 0, Math.sin(angle) * outer),
    );
    lineAt(rig.segments[used++]!, from, to, width, height);
  }
  return used;
}

export function targetBrackets(
  rig: ArcSignatureEffectRig,
  position: THREE.Vector3,
  scale: number,
  start = 0,
): number {
  const inner = 0.28 * scale;
  const outer = 0.46 * scale;
  let segment = start;
  for (const [sx, sz] of [
    [-1, -1],
    [1, -1],
    [1, 1],
    [-1, 1],
  ] as const) {
    lineAt(
      rig.segments[segment++]!,
      position.clone().add(new THREE.Vector3(sx * inner, 0, sz * outer)),
      position.clone().add(new THREE.Vector3(sx * outer, 0, sz * outer)),
      0.045,
    );
    lineAt(
      rig.segments[segment++]!,
      position.clone().add(new THREE.Vector3(sx * outer, 0, sz * outer)),
      position.clone().add(new THREE.Vector3(sx * outer, 0, sz * inner)),
      0.045,
    );
  }
  return segment;
}

export function areaBounds(points: readonly THREE.Vector3[]): {
  centre: THREE.Vector3;
  width: number;
  depth: number;
} {
  const minX = Math.min(...points.map((point) => point.x));
  const maxX = Math.max(...points.map((point) => point.x));
  const minZ = Math.min(...points.map((point) => point.z));
  const maxZ = Math.max(...points.map((point) => point.z));
  return {
    centre: new THREE.Vector3((minX + maxX) / 2, 0, (minZ + maxZ) / 2),
    width: Math.max(0.92, maxX - minX + 0.88),
    depth: Math.max(0.92, maxZ - minZ + 0.88),
  };
}

export function fieldBase(
  rig: ArcSignatureEffectRig,
  points: readonly THREE.Vector3[],
  anchor: THREE.Vector3,
  scale: number,
): ReturnType<typeof areaBounds> {
  const area = areaBounds(points.length > 0 ? points : [anchor]);
  rig.disc.visible = true;
  rig.disc.position.copy(area.centre).setY(0.032);
  rig.disc.scale.set(area.width, 1, area.depth);
  rig.ring.visible = true;
  rig.ring.position.copy(area.centre).setY(0.04);
  rig.ring.scale.set(area.width * scale, 1, area.depth * scale);
  return area;
}

export function placeTravelHead(
  mesh: THREE.Mesh,
  from: THREE.Vector3,
  to: THREE.Vector3,
  progress: number,
  arcHeight: number,
  scale = 1,
): THREE.Vector3 {
  const position = new THREE.Vector3().lerpVectors(from, to, clamp01(progress));
  position.y = 0.14 + Math.sin(clamp01(progress) * Math.PI) * arcHeight;
  mesh.visible = true;
  mesh.position.copy(position);
  mesh.scale.setScalar(scale);
  mesh.rotation.z = Math.PI / 2;
  mesh.rotation.y = -Math.atan2(to.z - from.z, to.x - from.x);
  return position;
}

export function paintPropFallback(
  rig: ArcSignatureEffectRig,
  points: readonly THREE.Vector3[],
  time: number,
  persistentPropReady: boolean,
  markerScale: THREE.Vector3,
): void {
  if (!persistentPropReady) {
    for (const [index, point] of points.entries()) {
      const marker = rig.markers[index];
      if (!marker) break;
      marker.visible = true;
      marker.position.copy(point).setY(0.14);
      marker.rotation.y = time * 0.22 + index;
      marker.scale.copy(markerScale);
    }
  }
  const anchor = points[0];
  if (anchor) targetRing(rig, anchor, 0.58);
}

export function clamp01(value: number): number {
  return Math.max(0, Math.min(1, value));
}
