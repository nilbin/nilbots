import * as THREE from 'three';
import type {
  ReplayArcRelayModeState,
  ReplayModel,
  ReplayPosition,
} from '../replayModel';
import { posesAt } from '../render/interpolate';
import { unitAccent } from '../render/unitPresentation';
import { signatureModel } from './lookModel';

type Signature = ReplayArcRelayModeState['visibleSignatures'][number];
type SignatureForm =
  | 'dash'
  | 'prism'
  | 'hook'
  | 'repair'
  | 'survey'
  | 'star'
  | 'trip'
  | 'null'
  | 'arc'
  | 'exchange'
  | 'rail'
  | 'block'
  | 'target'
  | 'burst'
  | 'smoke'
  | 'seed';

export const ARC_SIGNATURE_STYLES = {
  'vector-dash': { form: 'dash', polish: 'simple' },
  'prism-wall': { form: 'prism', polish: 'standard' },
  'tractor-hook': { form: 'hook', polish: 'standard' },
  'repair-beam': { form: 'repair', polish: 'priority' },
  'survey-flare': { form: 'survey', polish: 'priority' },
  'falling-star': { form: 'star', polish: 'standard' },
  'trip-node': { form: 'trip', polish: 'standard' },
  'null-field': { form: 'null', polish: 'standard' },
  'arc-toss': { form: 'arc', polish: 'standard' },
  exchange: { form: 'exchange', polish: 'standard' },
  'rail-line': { form: 'rail', polish: 'standard' },
  'hardlight-block': { form: 'block', polish: 'standard' },
  'target-paint': { form: 'target', polish: 'standard' },
  'kinetic-burst': { form: 'burst', polish: 'simple' },
  'smoke-canister': { form: 'smoke', polish: 'priority' },
  'sentinel-seed': { form: 'seed', polish: 'standard' },
} as const satisfies Record<
  string,
  { form: SignatureForm; polish: 'simple' | 'standard' | 'priority' }
>;

export type ArcSignatureVisualPhase =
  | 'hidden'
  | 'tell'
  | 'active'
  | 'channel'
  | 'in-flight';

/** Guard the renderer against extending or pre-rolling an authoritative telegraph. */
export function arcSignatureVisualPhase(
  signature: Signature,
  time: number,
): ArcSignatureVisualPhase {
  if (time < signature.startedTick) return 'hidden';
  if (signature.endsAtTick !== null && time >= signature.endsAtTick)
    return 'hidden';
  if (signature.phase === 'tell') {
    if (
      signature.completesAtTick !== null &&
      time >= signature.completesAtTick
    )
      return 'hidden';
    return 'tell';
  }
  return signature.phase;
}

interface EffectRig {
  group: THREE.Group;
  main: THREE.MeshBasicMaterial;
  fill: THREE.MeshBasicMaterial;
  smoke: THREE.MeshStandardMaterial;
  disc: THREE.Mesh;
  ring: THREE.Mesh;
  beam: THREE.Mesh;
  segments: THREE.Mesh[];
  panels: THREE.Mesh[];
  particles: THREE.Mesh[];
  markers: THREE.Mesh[];
}

interface SignatureShotYaw {
  tick: number;
  yaw: number;
}

interface SignaturePropSlot {
  group: THREE.Group;
  model: THREE.Group | null;
  emissives: { material: THREE.MeshStandardMaterial; base: number }[];
  ready: boolean;
  disposed: boolean;
}

export const ARC_SIGNATURE_PROP_SCALE = {
  'trip-node': 0.6,
  'sentinel-seed': 0.95,
} as const;

/** The sentry may acknowledge shots that have happened, but never pre-aim from future state. */
export function latestObservedSignatureYaw(
  shots: readonly SignatureShotYaw[],
  time: number,
): number | null {
  let yaw: number | null = null;
  for (const shot of shots) {
    if (shot.tick > Math.floor(time)) break;
    yaw = shot.yaw;
  }
  return yaw;
}

export function buildArcRelayEffects(
  replay: ReplayModel,
  disposables: { dispose: () => void }[],
): { group: THREE.Group; update: (time: number) => void } {
  const group = new THREE.Group();
  group.userData.kind = 'arc-relay-signatures-and-beats';
  const signatureShotYaws = indexSignatureShotYaws(replay);
  const propSlots = new Map<string, SignaturePropSlot>();

  const propAt = (signature: Signature): SignaturePropSlot => {
    const existing = propSlots.get(signature.operationId);
    if (existing) return existing;
    const propGroup = new THREE.Group();
    propGroup.visible = false;
    propGroup.userData.kind = 'arc-relay-signature-prop';
    propGroup.userData.operationId = signature.operationId;
    propGroup.userData.signatureId = signature.signatureId;
    group.add(propGroup);
    const slot: SignaturePropSlot = {
      group: propGroup,
      model: null,
      emissives: [],
      ready: false,
      disposed: false,
    };
    propSlots.set(signature.operationId, slot);
    disposables.push({
      dispose: () => {
        slot.disposed = true;
        if (slot.model) disposeModelMaterials(slot.model);
      },
    });
    void signatureModel(signature.signatureId).then((model) => {
      if (!model) return;
      if (slot.disposed) {
        disposeModelMaterials(model);
        return;
      }
      slot.model = model;
      slot.emissives = signaturePropEmissives(model);
      slot.ready = true;
      propGroup.add(model);
    });
    return slot;
  };

  const discGeometry = new THREE.CircleGeometry(0.5, 40);
  discGeometry.rotateX(-Math.PI / 2);
  const ringGeometry = new THREE.RingGeometry(0.38, 0.5, 40);
  ringGeometry.rotateX(-Math.PI / 2);
  const segmentGeometry = new THREE.BoxGeometry(1, 0.018, 0.055);
  const panelGeometry = new THREE.BoxGeometry(0.78, 0.64, 0.08);
  const particleGeometry = new THREE.SphereGeometry(0.055, 8, 5);
  const markerGeometry = new THREE.TetrahedronGeometry(0.15, 0);
  const beamGeometry = new THREE.CylinderGeometry(0.035, 0.09, 1, 10, 1, true);
  disposables.push(
    discGeometry,
    ringGeometry,
    segmentGeometry,
    panelGeometry,
    particleGeometry,
    markerGeometry,
    beamGeometry,
  );

  const rigs: EffectRig[] = [];
  const rigAt = (index: number) => {
    while (rigs.length <= index) {
      const rigGroup = new THREE.Group();
      const main = new THREE.MeshBasicMaterial({
        transparent: true,
        opacity: 0,
        depthWrite: false,
        blending: THREE.AdditiveBlending,
        side: THREE.DoubleSide,
      });
      const fill = new THREE.MeshBasicMaterial({
        transparent: true,
        opacity: 0,
        depthWrite: false,
        blending: THREE.AdditiveBlending,
        side: THREE.DoubleSide,
      });
      const smoke = new THREE.MeshStandardMaterial({
        color: '#66727c',
        emissive: '#121a20',
        emissiveIntensity: 0.12,
        transparent: true,
        opacity: 0,
        depthWrite: false,
        roughness: 0.95,
        metalness: 0,
      });
      const disc = new THREE.Mesh(discGeometry, fill);
      const ring = new THREE.Mesh(ringGeometry, main);
      const beam = new THREE.Mesh(beamGeometry, main);
      rigGroup.add(disc, ring, beam);
      const segments = Array.from({ length: 24 }, () => {
        const mesh = new THREE.Mesh(segmentGeometry, main);
        rigGroup.add(mesh);
        return mesh;
      });
      const panels = Array.from({ length: 16 }, () => {
        const mesh = new THREE.Mesh(panelGeometry, fill);
        rigGroup.add(mesh);
        return mesh;
      });
      const particles = Array.from({ length: 18 }, () => {
        const mesh = new THREE.Mesh(particleGeometry, main);
        rigGroup.add(mesh);
        return mesh;
      });
      const markers = Array.from({ length: 16 }, () => {
        const mesh = new THREE.Mesh(markerGeometry, main);
        rigGroup.add(mesh);
        return mesh;
      });
      rigGroup.visible = false;
      group.add(rigGroup);
      rigs.push({
        group: rigGroup,
        main,
        fill,
        smoke,
        disc,
        ring,
        beam,
        segments,
        panels,
        particles,
        markers,
      });
      disposables.push(main, fill, smoke);
    }
    return rigs[index]!;
  };

  const pulseMaterial = new THREE.MeshBasicMaterial({
    transparent: true,
    opacity: 0,
    depthWrite: false,
    blending: THREE.AdditiveBlending,
    side: THREE.DoubleSide,
  });
  const pulseGeometry = new THREE.BoxGeometry(0.5, 0.025, 1);
  const pulse = new THREE.Mesh(pulseGeometry, pulseMaterial);
  pulse.visible = false;
  group.add(pulse);
  disposables.push(pulseMaterial, pulseGeometry);

  const beatRingMaterial = new THREE.MeshBasicMaterial({
    transparent: true,
    opacity: 0,
    depthWrite: false,
    blending: THREE.AdditiveBlending,
    side: THREE.DoubleSide,
  });
  const beatRingGeometry = new THREE.RingGeometry(0.18, 0.28, 40);
  beatRingGeometry.rotateX(-Math.PI / 2);
  const beatRings = Array.from({ length: 12 }, () => {
    const mesh = new THREE.Mesh(beatRingGeometry, beatRingMaterial.clone());
    mesh.visible = false;
    group.add(mesh);
    disposables.push(mesh.material as THREE.Material);
    return mesh;
  });
  disposables.push(beatRingMaterial, beatRingGeometry);

  const update = (time: number) => {
    for (const rig of rigs) rig.group.visible = false;
    for (const slot of propSlots.values()) slot.group.visible = false;
    pulse.visible = false;
    for (const ring of beatRings) ring.visible = false;

    const tickIndex = Math.max(
      0,
      Math.min(Math.floor(time), replay.ticks.length - 1),
    );
    const tick = replay.ticks[tickIndex];
    const state =
      tick?.after.mode?.kind === 'arc-relay' &&
      'visibleSignatures' in tick.after.mode
        ? tick.after.mode
        : null;
    if (!state) return;
    const poses = posesAt(replay, time);
    let used = 0;
    for (const signature of state.visibleSignatures) {
      const phase = arcSignatureVisualPhase(signature, time);
      const style = ARC_SIGNATURE_STYLES[
        signature.signatureId as keyof typeof ARC_SIGNATURE_STYLES
      ];
      if (phase === 'hidden' || !style) continue;
      const rig = rigAt(used++);
      resetRig(rig);
      rig.group.visible = true;
      rig.group.userData.signatureId = signature.signatureId;
      rig.group.userData.phase = phase;
      const accent = new THREE.Color(
        unitAccent(replay, signature.ownerActor.unitKey),
      );
      const suppressed = signature.suppressed;
      rig.main.color.copy(suppressed ? new THREE.Color('#dce5eb') : accent);
      rig.fill.color.copy(
        style.form === 'null'
          ? new THREE.Color('#17212a')
          : style.form === 'smoke'
            ? new THREE.Color('#64717c')
            : accent,
      );
      const pulsePhase = 0.5 + 0.5 * Math.sin(time * Math.PI * 3);
      rig.main.opacity =
        (phase === 'tell' ? 0.42 + pulsePhase * 0.2 : 0.72) *
        (suppressed ? 0.55 : 1);
      rig.fill.opacity =
        (phase === 'tell' ? 0.055 : style.form === 'smoke' ? 0.2 : 0.13) *
        (suppressed ? 0.5 : 1);
      const points = signature.positions.map(worldPoint);
      const ownerPose = poses.find(
        (pose) => pose.actorKey === signature.ownerActor.actorKey,
      );
      const owner = ownerPose
        ? new THREE.Vector3(ownerPose.x + 0.5, 0.08, ownerPose.y + 0.5)
        : points[0];
      const targetPose = signature.targetActor
        ? poses.find(
            (pose) => pose.actorKey === signature.targetActor!.actorKey,
          )
        : null;
      const target = targetPose
        ? new THREE.Vector3(targetPose.x + 0.5, 0.08, targetPose.y + 0.5)
        : points[0] ?? owner;
      let persistentPropReady = false;
      if (
        phase === 'active' &&
        (signature.signatureId === 'trip-node' ||
          signature.signatureId === 'sentinel-seed') &&
        points[0]
      ) {
        const slot = propAt(signature);
        slot.group.visible = true;
        slot.group.position.copy(points[0]).setY(0);
        slot.group.scale.setScalar(ARC_SIGNATURE_PROP_SCALE[signature.signatureId]);
        slot.group.rotation.y =
          signature.signatureId === 'sentinel-seed'
            ? latestObservedSignatureYaw(
                signatureShotYaws.get(signature.operationId) ?? [],
                time,
              ) ?? 0
            : 0;
        paintSignaturePropBody(slot.emissives, signature.suppressed, time);
        persistentPropReady = slot.ready;
      }
      paintSignature(
        rig,
        style.form,
        points,
        owner,
        target,
        time,
        phase,
        persistentPropReady,
      );
    }

    paintStoryBeats(
      replay,
      tickIndex,
      time - tickIndex,
      state,
      pulse,
      pulseMaterial,
      beatRings,
    );
  };

  return { group, update };
}

function resetRig(rig: EffectRig): void {
  rig.disc.visible = false;
  rig.ring.visible = false;
  rig.beam.visible = false;
  for (const mesh of [
    ...rig.segments,
    ...rig.panels,
    ...rig.particles,
    ...rig.markers,
  ])
    mesh.visible = false;
  for (const particle of rig.particles) particle.material = rig.main;
  rig.smoke.opacity = 0;
}

function paintSignature(
  rig: EffectRig,
  form: SignatureForm,
  points: THREE.Vector3[],
  owner: THREE.Vector3 | undefined,
  target: THREE.Vector3 | undefined,
  time: number,
  phase: Exclude<ArcSignatureVisualPhase, 'hidden'>,
  persistentPropReady = false,
): void {
  const anchor = points[0] ?? target ?? owner;
  if (!anchor) return;
  const tellScale = phase === 'tell' ? 0.92 + 0.08 * Math.sin(time * Math.PI * 3) : 1;

  if (form === 'survey' || form === 'null' || form === 'smoke') {
    const area = areaBounds(points.length > 0 ? points : [anchor]);
    rig.disc.visible = true;
    rig.disc.position.copy(area.centre).setY(0.032);
    rig.disc.scale.set(area.width, area.depth, 1);
    rig.ring.visible = true;
    rig.ring.position.copy(area.centre).setY(0.04);
    rig.ring.scale.set(area.width * tellScale, area.depth * tellScale, 1);
    rig.ring.rotation.y = form === 'survey' ? time * 0.35 : -time * 0.12;
    if (form === 'survey') {
      rig.beam.visible = true;
      rig.beam.position.copy(area.centre).setY(0.75);
      rig.beam.scale.y = 1.4;
      for (let index = 0; index < 12; index += 1) {
        const angle = (index / 12) * Math.PI * 2 + time * 0.22;
        const particle = rig.particles[index]!;
        particle.visible = true;
        particle.position.set(
          area.centre.x + Math.cos(angle) * area.width * 0.45,
          0.12 + (index % 3) * 0.055,
          area.centre.z + Math.sin(angle) * area.depth * 0.45,
        );
      }
    } else if (form === 'null') {
      rig.fill.opacity *= 1.8;
      for (let index = 0; index < 8; index += 1) {
        const angle = (index / 8) * Math.PI * 2 - time * 0.18;
        const particle = rig.particles[index]!;
        particle.visible = true;
        particle.position.set(
          area.centre.x + Math.cos(angle) * area.width * 0.31,
          0.055,
          area.centre.z + Math.sin(angle) * area.depth * 0.31,
        );
        particle.scale.setScalar(0.42);
      }
    } else {
      rig.disc.visible = false;
      for (let index = 0; index < 18; index += 1) {
        const particle = rig.particles[index]!;
        particle.visible = true;
        particle.material = rig.smoke;
        const angle = index * 2.399963 + time * 0.045;
        const radius = Math.sqrt((index + 0.5) / 18) * 0.46;
        particle.position.set(
          area.centre.x + Math.cos(angle) * area.width * radius,
          0.12 + (index % 5) * 0.07 + 0.025 * Math.sin(time + index),
          area.centre.z + Math.sin(angle) * area.depth * radius,
        );
        particle.scale.setScalar(1.9 + (index % 4) * 0.45);
      }
      rig.smoke.opacity = phase === 'tell' ? 0.1 : 0.24;
    }
    return;
  }

  if (form === 'prism' || form === 'block') {
    for (const [index, point] of points.entries()) {
      const panel = rig.panels[index];
      if (!panel) break;
      panel.visible = true;
      panel.position.copy(point).setY(form === 'prism' ? 0.33 : 0.24);
      panel.scale.setScalar(tellScale * (form === 'prism' ? 1 : 0.82));
      if (form === 'prism' && points[index + 1])
        panel.rotation.y = -Math.atan2(
          points[index + 1]!.z - point.z,
          points[index + 1]!.x - point.x,
        );
    }
    return;
  }

  if (form === 'star') {
    targetRing(rig, anchor, tellScale);
    lineAt(rig.segments[0]!, anchor.clone().add(new THREE.Vector3(-0.42, 0, 0)), anchor.clone().add(new THREE.Vector3(0.42, 0, 0)), 0.055);
    lineAt(rig.segments[1]!, anchor.clone().add(new THREE.Vector3(0, 0, -0.42)), anchor.clone().add(new THREE.Vector3(0, 0, 0.42)), 0.055);
    rig.beam.visible = true;
    rig.beam.position.copy(anchor).setY(0.75);
    rig.beam.scale.y = 1.4;
    return;
  }

  if (form === 'trip' || form === 'seed') {
    if (!persistentPropReady) {
      for (const [index, point] of points.entries()) {
        const marker = rig.markers[index];
        if (!marker) break;
        marker.visible = true;
        marker.position.copy(point).setY(form === 'seed' ? 0.17 : 0.12);
        marker.rotation.y = time * (form === 'seed' ? 0.35 : -0.2) + index;
        if (form === 'seed') marker.scale.set(0.82, 1.15, 0.82);
        if (index > 0)
          lineAt(rig.segments[index - 1]!, points[index - 1]!, point, 0.035);
      }
    }
    targetRing(rig, anchor, tellScale * (form === 'seed' ? 0.72 : 0.54));
    if (phase === 'active') rig.main.opacity *= 0.58;
    return;
  }

  if (form === 'arc' && owner && target) {
    for (let index = 0; index < 14; index += 1) {
      const progress = index / 13;
      const particle = rig.particles[index]!;
      particle.visible = true;
      particle.position.lerpVectors(owner, target, progress);
      particle.position.y = 0.12 + Math.sin(progress * Math.PI) * 0.72;
      particle.scale.setScalar(0.75 + Math.sin(progress * Math.PI) * 0.5);
    }
    targetRing(rig, target, tellScale * 0.7);
    return;
  }

  if ((form === 'hook' || form === 'repair' || form === 'exchange') && owner && target) {
    lineAt(rig.segments[0]!, owner, target, form === 'repair' ? 0.085 : 0.055);
    if (form === 'repair') {
      const offset = new THREE.Vector3(0, 0.04, 0.08);
      lineAt(rig.segments[1]!, owner.clone().add(offset), target.clone().add(offset), 0.025);
      for (let index = 0; index < 8; index += 1) {
        const particle = rig.particles[index]!;
        const progress = (time * 0.55 + index / 8) % 1;
        particle.visible = true;
        particle.position.lerpVectors(owner, target, progress).setY(0.16 + Math.sin(progress * Math.PI) * 0.12);
        particle.scale.setScalar(0.55);
      }
    } else if (form === 'exchange') {
      lineAt(rig.segments[1]!, target, owner, 0.025);
      targetRing(rig, owner, tellScale * 0.65);
    } else {
      const marker = rig.markers[0]!;
      marker.visible = true;
      marker.position.copy(target).setY(0.14);
      marker.rotation.z = Math.PI / 2;
    }
    targetRing(rig, target, tellScale * 0.72);
    return;
  }

  if (form === 'rail') {
    const path = points.length > 1 ? points : owner && target ? [owner, target] : [anchor];
    for (let index = 1; index < path.length; index += 1)
      lineAt(rig.segments[index - 1]!, path[index - 1]!, path[index]!, 0.11);
    return;
  }

  if (form === 'target') {
    targetBrackets(rig, target ?? anchor, tellScale);
    return;
  }

  if (form === 'burst') {
    targetRing(rig, anchor, tellScale * (phase === 'tell' ? 1 : 1.4));
    return;
  }

  if (form === 'dash') {
    const destination = points.at(-1) ?? target;
    if (owner && destination) lineAt(rig.segments[0]!, owner, destination, 0.07);
    for (let index = 0; index < Math.min(points.length, 5); index += 1) {
      const marker = rig.markers[index]!;
      marker.visible = true;
      marker.position.copy(points[index]!).setY(0.08);
      marker.scale.set(0.42, 0.18, 0.75);
    }
  }
}

function targetRing(rig: EffectRig, position: THREE.Vector3, scale: number): void {
  rig.ring.visible = true;
  rig.ring.position.copy(position).setY(0.045);
  rig.ring.scale.setScalar(scale);
}

function targetBrackets(rig: EffectRig, position: THREE.Vector3, scale: number): void {
  const inner = 0.28 * scale;
  const outer = 0.46 * scale;
  let segment = 0;
  for (const [sx, sz] of [[-1, -1], [1, -1], [1, 1], [-1, 1]] as const) {
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
}

function lineAt(
  mesh: THREE.Mesh,
  from: THREE.Vector3,
  to: THREE.Vector3,
  width: number,
): void {
  const dx = to.x - from.x;
  const dz = to.z - from.z;
  const length = Math.hypot(dx, dz);
  mesh.visible = length > 0.001;
  mesh.position.set((from.x + to.x) / 2, 0.07, (from.z + to.z) / 2);
  mesh.rotation.y = -Math.atan2(dz, dx);
  mesh.scale.set(length, 1, width / 0.055);
}

function areaBounds(points: THREE.Vector3[]): {
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

function worldPoint(position: ReplayPosition): THREE.Vector3 {
  return new THREE.Vector3(position.x + 0.5, 0.065, position.y + 0.5);
}

function indexSignatureShotYaws(
  replay: ReplayModel,
): Map<string, SignatureShotYaw[]> {
  const result = new Map<string, SignatureShotYaw[]>();
  for (const [tickIndex, tick] of replay.ticks.entries()) {
    const state =
      tick.after.mode?.kind === 'arc-relay' &&
      'visibleSignatures' in tick.after.mode
        ? tick.after.mode
        : null;
    if (!state) continue;
    for (const event of [...tick.lifecycleEvents, ...tick.events]) {
      const fact = event.arcRelayFact;
      if (
        fact?.kind !== 'signature-damage' ||
        fact.signatureId !== 'sentinel-seed'
      )
        continue;
      const signature = state.visibleSignatures.find(
        (candidate) => candidate.operationId === fact.operationId,
      );
      const anchor = signature?.positions[0];
      if (!anchor) continue;
      const dx = fact.position.x - anchor.x;
      const dz = fact.position.y - anchor.y;
      if (Math.hypot(dx, dz) < 0.001) continue;
      const shots = result.get(fact.operationId) ?? [];
      shots.push({ tick: tickIndex, yaw: -Math.atan2(dz, dx) });
      result.set(fact.operationId, shots);
    }
  }
  return result;
}

function paintSignaturePropBody(
  emissives: readonly { material: THREE.MeshStandardMaterial; base: number }[],
  suppressed: boolean,
  time: number,
): void {
  const breathing = 0.95 + Math.sin(time * Math.PI * 0.42) * 0.05;
  for (const { material, base } of emissives)
    material.emissiveIntensity = base * (suppressed ? 0.22 : breathing);
}

function signaturePropEmissives(
  model: THREE.Group,
): { material: THREE.MeshStandardMaterial; base: number }[] {
  const materials = new Set<THREE.MeshStandardMaterial>();
  model.traverse((node) => {
    const mesh = node as THREE.Mesh;
    if (!mesh.isMesh) return;
    for (const material of Array.isArray(mesh.material)
      ? mesh.material
      : [mesh.material]) {
      if (!(material instanceof THREE.MeshStandardMaterial)) continue;
      materials.add(material);
    }
  });
  return [...materials].map((material) => ({
    material,
    base: material.emissiveIntensity,
  }));
}

function disposeModelMaterials(model: THREE.Group): void {
  const materials = new Set<THREE.Material>();
  model.traverse((node) => {
    const mesh = node as THREE.Mesh;
    if (!mesh.isMesh) return;
    for (const material of Array.isArray(mesh.material)
      ? mesh.material
      : [mesh.material])
      materials.add(material);
  });
  for (const material of materials) material.dispose();
}

function paintStoryBeats(
  replay: ReplayModel,
  tickIndex: number,
  fraction: number,
  state: ReplayArcRelayModeState,
  pulse: THREE.Mesh,
  pulseMaterial: THREE.MeshBasicMaterial,
  beatRings: THREE.Mesh[],
): void {
  let used = 0;
  const borrowRing = (
    position: ReplayPosition,
    colour: THREE.ColorRepresentation,
    inward: boolean,
  ) => {
    const ring = beatRings[used++];
    if (!ring) return;
    ring.visible = true;
    ring.position.set(position.x + 0.5, 0.085, position.y + 0.5);
    ring.scale.setScalar(inward ? 2.1 - fraction * 1.35 : 0.7 + fraction * 2.2);
    const material = ring.material as THREE.MeshBasicMaterial;
    material.color.set(colour);
    material.opacity = Math.sin(fraction * Math.PI) * 0.86;
  };

  const tickModel = replay.ticks[tickIndex];
  for (const event of [
    ...(tickModel?.lifecycleEvents ?? []),
    ...(tickModel?.events ?? []),
  ]) {
    const fact = event.arcRelayFact;
    if (!fact) continue;
    if (fact.kind === 'signature-damage') {
      const owner = replay.units.find(
        (unit) => unit.teamId === fact.ownerActor.teamId,
      );
      const accent = owner ? unitAccent(replay, owner.unitKey) : '#eef8fc';
      borrowRing(fact.position, accent, false);
      if (fact.signatureId === 'sentinel-seed') {
        const muzzle = state.visibleSignatures.find(
          (candidate) => candidate.operationId === fact.operationId,
        )?.positions[0];
        if (muzzle) borrowRing(muzzle, accent, true);
      }
      continue;
    }
    if (fact.kind === 'signature-repair') {
      borrowRing(fact.position, '#6ee7a8', true);
      continue;
    }
    if (fact.kind === 'core-born') borrowRing(fact.position, '#eef8fc', false);
    else if (fact.kind === 'core-picked-up') {
      const previousTeam = coreOwnerTeamBefore(replay, tickIndex, fact.coreId);
      if (previousTeam !== null && previousTeam !== fact.carrierActor.teamId) {
        const previousUnit = replay.units.find((unit) => unit.teamId === previousTeam);
        borrowRing(
          fact.position,
          previousUnit ? unitAccent(replay, previousUnit.unitKey) : '#eef8fc',
          false,
        );
      }
      borrowRing(
        fact.position,
        unitAccent(replay, fact.carrierActor.unitKey),
        true,
      );
    } else if (fact.kind === 'core-dropped')
      borrowRing(fact.position, '#eef8fc', false);
    else if (fact.kind === 'core-banked')
      borrowRing(
        fact.position,
        replay.units.find((unit) => unit.teamId === fact.teamId)
          ? unitAccent(
              replay,
              replay.units.find((unit) => unit.teamId === fact.teamId)!.unitKey,
            )
          : '#eef8fc',
        false,
      );
  }

  if (
    state.latestPulseTick === tickIndex &&
    state.latestPulseTeamId !== null
  ) {
    const reactor = state.reactors.find(
      (candidate) => candidate.teamId === state.latestPulseTeamId,
    );
    const fromLeft = (reactor?.position.x ?? 0) < replay.map.width / 2;
    const progress = fromLeft ? fraction : 1 - fraction;
    pulse.visible = true;
    pulse.position.set(
      replay.map.width * progress,
      0.09,
      replay.map.height / 2,
    );
    pulse.scale.set(1 + Math.sin(fraction * Math.PI) * 1.4, 1, replay.map.height);
    pulseMaterial.color.set(
      replay.units.find((unit) => unit.teamId === state.latestPulseTeamId)
        ? unitAccent(
            replay,
            replay.units.find((unit) => unit.teamId === state.latestPulseTeamId)!.unitKey,
          )
        : '#eef8fc',
    );
    pulseMaterial.opacity = Math.sin(fraction * Math.PI) * 0.26;
  }
}

function coreOwnerTeamBefore(
  replay: ReplayModel,
  tickIndex: number,
  coreId: { sourceWellId: string; sourceOrdinal: number },
): number | null {
  const key = `${coreId.sourceWellId}:${coreId.sourceOrdinal}`;
  for (let index = tickIndex - 1; index >= 0; index -= 1) {
    for (const event of [
      ...(replay.ticks[index]?.lifecycleEvents ?? []),
      ...(replay.ticks[index]?.events ?? []),
    ].reverse()) {
      const fact = event.arcRelayFact;
      if (!fact || !('coreId' in fact)) continue;
      if (`${fact.coreId.sourceWellId}:${fact.coreId.sourceOrdinal}` !== key) continue;
      if (fact.kind === 'core-picked-up') return fact.carrierActor.teamId;
      if (fact.kind === 'core-handed-off') return fact.targetActor.teamId;
      if (fact.kind === 'core-relocated') return fact.carrierActor?.teamId ?? null;
      if (fact.kind === 'core-dropped') return fact.sourceActor.teamId;
      if (fact.kind === 'core-banked') return fact.teamId;
    }
  }
  return null;
}
