import * as THREE from 'three';
import type {
  ReplayArcRelayModeState,
  ReplayModel,
  ReplayPosition,
} from '../replayModel';
import { posesAt, type BotPose } from '../render/interpolate';
import type { ArcVeterancyIndex } from '../replayArcVeterancy';
import { unitAccent } from '../render/unitPresentation';
import { arenaTheme } from '../render/arenaThemes';
import {
  resolveArcSignaturePresentation,
  type ArcSignatureCanvasMarker,
} from '../render/arcSignaturePresentation';
import { ARC_SIGNATURE_PRESENTATION_ID } from '../render/arcSignaturePresentationId';
import { ARC_TOSS_RELEASE_TICK_OFFSET } from '../render/signatures/definitions/arcToss';
import { signatureModel } from './lookModel';
import {
  arcVeterancyFor,
  LEVEL_UP_BEAT_TICKS,
} from '../replayArcVeterancy';
import {
  arcSignatureWebGLPainter,
  arcSignatureWebGLPainterIds,
} from './signatures/registry';
import type {
  ArcSignatureEffectRig,
  ArcSignatureOutcomeFact,
  ArcSignatureWebGLPhase,
} from './signatures/types';
import {
  areaBounds as signatureAreaBounds,
  phaseProgress as signaturePhaseProgress,
  SURVEY_FLARE_IDLE_YAW_PER_TICK,
  SURVEY_FLARE_LANDED_HEIGHT,
  surveyFlareFlightEndpoints,
  surveyFlareFlightHeight,
  surveyFlareFlightRotation,
  surveyFlareLandedYaw,
} from './signatures/primitives';

export {
  SURVEY_FLARE_FLIGHT_HEIGHT,
  SURVEY_FLARE_IDLE_YAW_PER_TICK,
  SURVEY_FLARE_LANDED_HEIGHT,
  SURVEY_FLARE_LANDED_ROLL,
  surveyFlareFlightEndpoints,
  surveyFlareFlightHeight,
  surveyFlareFlightRotation,
  surveyFlareLandedYaw,
} from './signatures/primitives';
import {
  createStatusHealthBar,
  orientStatusBib,
  paintStatusHealthBar,
  placeStatusBib,
  type StatusHealthBarRig,
} from './statusBib';
import { configurePersistentConstructRendering } from './occlusionLayers';

export {
  configurePersistentConstructRendering,
  PERSISTENT_CONSTRUCT_RENDER_ORDER,
} from './occlusionLayers';

type Signature = ReplayArcRelayModeState['visibleSignatures'][number];
const SIGNATURE_OUTCOME_LINGER_TICKS = 1.8;
const LEGACY_WEBGL_PAINTERS: Readonly<Record<string, ArcSignatureCanvasMarker>> =
  Object.freeze({
    'vector-dash': 'arrow',
    'prism-wall': 'panels',
    'tractor-hook': 'tether',
    'repair-beam': 'braid',
    'survey-flare': 'calibration-field',
    'falling-star': 'cardinal-target',
    'trip-node': 'prop',
    'null-field': 'dark-field',
    'arc-toss': 'objective-arc',
    exchange: 'paired-fold',
    'rail-line': 'pressure-lane',
    'hardlight-block': 'block',
    'target-paint': 'target-brackets',
    'kinetic-burst': 'pressure-ring',
    'smoke-canister': 'smoke-field',
    'sentinel-seed': 'prop',
  });

/** Stable painter IDs exposed for conformance tests and capture tooling. */
export const ARC_SIGNATURE_WEBGL_PAINTERS = Object.freeze(
  Object.fromEntries(
    arcSignatureWebGLPainterIds().map((id) => [id, id]),
  ) as Readonly<Record<string, string>>,
);

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

interface SignatureShotYaw {
  tick: number;
  yaw: number;
}

interface SurveyFlareLanding {
  position: THREE.Vector3;
  yaw: number;
}

export const SENTINEL_YAW_SETTLE_TICKS = 0.58;

interface SignaturePropSlot {
  group: THREE.Group;
  model: THREE.Group | null;
  yawHead: THREE.Object3D | null;
  emissives: { material: THREE.MeshStandardMaterial; base: number }[];
  materials: {
    material: THREE.MeshStandardMaterial;
    opacity: number;
    transparent: boolean;
  }[];
  ready: boolean;
  disposed: boolean;
}

export const ARC_SIGNATURE_PROP_SCALE = {
  'trip-node': 0.94,
  'sentinel-seed': 0.72,
  // The provider source is normalized to only 0.3 tile. Standing it on end at 1.35
  // left a roughly quarter-tile footprint that vanished at the strategic camera. This
  // is bot-like in height but still occupies only about half a tile on the floor.
  'survey-flare': 2.5,
} as const;
/** Stable air between a construct's scaled mesh and its independently bill-boarded bib. */
export const CONSTRUCT_STATUS_BIB_GAP = 0.11;
export const CONSTRUCT_STATUS_BIB_FALLBACK_HEIGHT = 0.38;

/** Derive status placement from the actual loaded model, including its live deploy scale. */
export function constructStatusBibHeight(
  propGroup: THREE.Object3D,
  model: THREE.Object3D | null,
): number {
  if (!model) return CONSTRUCT_STATUS_BIB_FALLBACK_HEIGHT;
  propGroup.updateMatrixWorld(true);
  const bounds = new THREE.Box3().setFromObject(model);
  return Number.isFinite(bounds.max.y)
    ? bounds.max.y + CONSTRUCT_STATUS_BIB_GAP
    : CONSTRUCT_STATUS_BIB_FALLBACK_HEIGHT;
}

/** Older broadcasts publish construct hull in the rules catalogue, not each frame. */
function signatureConstructHullById(replay: ReplayModel): Map<string, number> {
  if (
    replay.contract.kind !== 'v3-generic' ||
    replay.contract.rawContract.rules.gameMode.kind !== 'arc-relay'
  ) return new Map();
  const signatures = replay.contract.rawContract.rules.gameMode.signatures;
  if (!Array.isArray(signatures)) return new Map();
  return new Map(
    signatures.flatMap((signature) => {
      const hull = signature.hull;
      return typeof hull === 'number' && Number.isInteger(hull) && hull > 0
        ? [[signature.signatureId, hull] as const]
        : [];
    }),
  );
}

/** The sentry may acknowledge shots that have happened, but never pre-aim from future state. */
export function latestObservedSignatureYaw(
  shots: readonly SignatureShotYaw[],
  time: number,
): number | null {
  let previousYaw: number | null = null;
  for (const shot of shots) {
    if (shot.tick > time) break;
    const age = time - shot.tick;
    if (age < SENTINEL_YAW_SETTLE_TICKS) {
      const progress = easeOutDeploy(age / SENTINEL_YAW_SETTLE_TICKS);
      const start = previousYaw ?? 0;
      return start + shortestYaw(start, shot.yaw) * progress;
    }
    previousYaw = shot.yaw;
  }
  return previousYaw;
}

function shortestYaw(from: number, to: number): number {
  let delta = (to - from) % (Math.PI * 2);
  if (delta > Math.PI) delta -= Math.PI * 2;
  if (delta < -Math.PI) delta += Math.PI * 2;
  return delta;
}

/** Prefer the Sentinel's authored bearing split, while retaining old-asset fallback. */
export function sentinelYawTarget(
  propGroup: THREE.Group,
  model: THREE.Group | null,
): THREE.Object3D {
  return model?.getObjectByName('yaw-head') ?? propGroup;
}

export function buildArcRelayEffects(
  replay: ReplayModel,
  disposables: { dispose: () => void }[],
): { group: THREE.Group; update: (time: number) => void } {
  const group = new THREE.Group();
  group.userData.kind = 'arc-relay-signatures-and-beats';
  const theme = arenaTheme(replay.map.presentation?.themeId ?? undefined);
  let signatureShotYaws = indexSignatureShotYaws(replay);
  let signaturePhaseStarts = indexSignaturePhaseStarts(replay);
  let surveyFlareLandings = indexSurveyFlareLandings(replay);
  let signatureOutcomes = indexSignatureOutcomes(replay);
  let constructHull = signatureConstructHullById(replay);
  let veterancy = arcVeterancyFor(replay);
  let indexedTickCount = replay.ticks.length;
  const reducedMotionQuery =
    typeof window !== 'undefined' && window.matchMedia
      ? window.matchMedia('(prefers-reduced-motion: reduce)')
      : null;
  const propPools = new Map<string, SignaturePropSlot[]>();

  const propAt = (signature: Signature, poolIndex: number): SignaturePropSlot => {
    const pool = propPools.get(signature.signatureId) ?? [];
    propPools.set(signature.signatureId, pool);
    const existing = pool[poolIndex];
    if (existing) return existing;
    const propGroup = new THREE.Group();
    propGroup.visible = false;
    propGroup.userData.kind = 'arc-relay-signature-prop';
    propGroup.userData.signatureId = signature.signatureId;
    group.add(propGroup);
    const slot: SignaturePropSlot = {
      group: propGroup,
      model: null,
      yawHead: null,
      emissives: [],
      materials: [],
      ready: false,
      disposed: false,
    };
    pool[poolIndex] = slot;
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
      slot.yawHead = model.getObjectByName('yaw-head') ?? null;
      slot.emissives = signaturePropEmissives(model);
      configurePersistentConstructRendering(model);
      slot.materials = signaturePropMaterials(model);
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
  const panelFrameShape = new THREE.Shape();
  panelFrameShape.moveTo(-0.43, -0.36);
  panelFrameShape.lineTo(0.43, -0.36);
  panelFrameShape.lineTo(0.43, 0.36);
  panelFrameShape.lineTo(-0.43, 0.36);
  panelFrameShape.closePath();
  const panelFrameHole = new THREE.Path();
  panelFrameHole.moveTo(-0.34, -0.27);
  panelFrameHole.lineTo(-0.34, 0.27);
  panelFrameHole.lineTo(0.34, 0.27);
  panelFrameHole.lineTo(0.34, -0.27);
  panelFrameHole.closePath();
  panelFrameShape.holes.push(panelFrameHole);
  const panelFrameGeometry = new THREE.ExtrudeGeometry(panelFrameShape, {
    depth: 0.06,
    bevelEnabled: false,
  });
  panelFrameGeometry.translate(0, 0, -0.03);
  const slabGeometry = new THREE.BoxGeometry(0.42, 0.62, 0.42);
  // A Hardlight block is one tile-sized obstacle, not four tiny cubes. It needs a pooled
  // body geometry of its own so its gameplay silhouette survives the normal camera.
  const blockGeometry = new THREE.BoxGeometry(0.88, 0.22, 0.88);
  const postGeometry = new THREE.CylinderGeometry(0.055, 0.075, 0.78, 8);
  const particleGeometry = new THREE.SphereGeometry(0.055, 8, 5);
  const wispGeometry = new THREE.SphereGeometry(0.16, 8, 6);
  const markerGeometry = new THREE.TetrahedronGeometry(0.15, 0);
  const beamGeometry = new THREE.CylinderGeometry(0.035, 0.09, 1, 10, 1, true);
  // Outcome waves live on the floor. A torus is born in the XY plane; rotating -90°
  // around X maps its normal from +Z to +Y and makes its scale axes match the rest of the
  // floor primitives (X width, Y thickness, Z depth).
  const torusGeometry = new THREE.TorusGeometry(0.42, 0.09, 8, 40);
  torusGeometry.rotateX(-Math.PI / 2);
  const capsuleGeometry = new THREE.CapsuleGeometry(0.08, 0.22, 4, 8);
  const payloadGeometry = new THREE.CapsuleGeometry(0.19, 0.48, 5, 10);
  const podGeometry = new THREE.CylinderGeometry(0.32, 0.42, 0.72, 10, 1);
  const spoolGeometry = new THREE.TorusGeometry(0.3, 0.13, 8, 20);
  spoolGeometry.rotateX(Math.PI / 2);
  const hubGeometry = new THREE.CylinderGeometry(0.5, 0.58, 0.2, 12, 1);
  const cloudGeometry = new THREE.IcosahedronGeometry(0.36, 1);
  const finGeometry = new THREE.ConeGeometry(0.2, 0.62, 3);
  const plateGeometry = new THREE.CylinderGeometry(0.5, 0.56, 0.07, 24);
  const barGeometry = new THREE.BoxGeometry(1, 0.065, 1);
  disposables.push(
    discGeometry,
    ringGeometry,
    segmentGeometry,
    panelGeometry,
    panelFrameGeometry,
    slabGeometry,
    blockGeometry,
    postGeometry,
    particleGeometry,
    wispGeometry,
    markerGeometry,
    beamGeometry,
    torusGeometry,
    capsuleGeometry,
    payloadGeometry,
    podGeometry,
    spoolGeometry,
    hubGeometry,
    cloudGeometry,
    finGeometry,
    plateGeometry,
    barGeometry,
  );

  const rigs: ArcSignatureEffectRig[] = [];
  const capacityBars: Array<{
    bib: THREE.Group;
    bar: StatusHealthBarRig;
  }> = [];
  const rigAt = (index: number) => {
    while (rigs.length <= index) {
      const rigGroup = new THREE.Group();
      const energyMaterials: THREE.Material[] = [];
      const makeEnergy = (geometry: THREE.BufferGeometry) => {
        const material = new THREE.MeshBasicMaterial({
          transparent: true,
          opacity: 0,
          depthWrite: false,
          blending: THREE.AdditiveBlending,
          side: THREE.DoubleSide,
        });
        energyMaterials.push(material);
        return new THREE.Mesh(geometry, material);
      };
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
      const solid = new THREE.MeshStandardMaterial({
        color: '#eef8fc',
        emissive: '#14212a',
        emissiveIntensity: 0.18,
        transparent: true,
        opacity: 0.82,
        depthWrite: true,
        roughness: 0.92,
        metalness: 0.06,
      });
      const inset = new THREE.MeshStandardMaterial({
        color: '#1d2529',
        emissive: '#070b0d',
        emissiveIntensity: 0.02,
        transparent: true,
        opacity: 0.96,
        depthWrite: true,
        roughness: 1,
        metalness: 0.02,
      });
      const disc = new THREE.Mesh(discGeometry, fill);
      const ring = makeEnergy(ringGeometry);
      const rings = Array.from({ length: 8 }, () => {
        const mesh = makeEnergy(ringGeometry);
        rigGroup.add(mesh);
        return mesh;
      });
      const beam = makeEnergy(beamGeometry);
      const torus = makeEnergy(torusGeometry);
      const toruses = Array.from({ length: 3 }, () => {
        const mesh = makeEnergy(torusGeometry);
        rigGroup.add(mesh);
        return mesh;
      });
      const capsule = new THREE.Mesh(capsuleGeometry, solid);
      const payload = new THREE.Mesh(payloadGeometry, solid);
      const pod = new THREE.Mesh(podGeometry, solid);
      const spool = new THREE.Mesh(spoolGeometry, solid);
      const hub = new THREE.Mesh(hubGeometry, solid);
      const cloud = new THREE.Mesh(cloudGeometry, smoke);
      const plate = new THREE.Mesh(plateGeometry, solid);
      const cable = new THREE.Mesh(segmentGeometry, solid);
      rigGroup.add(
        disc,
        ring,
        beam,
        torus,
        capsule,
        payload,
        pod,
        spool,
        hub,
        cloud,
        plate,
        cable,
      );
      const segments = Array.from({ length: 24 }, () => {
        const mesh = makeEnergy(segmentGeometry);
        rigGroup.add(mesh);
        return mesh;
      });
      const panels = Array.from({ length: 16 }, () => {
        const mesh = new THREE.Mesh(panelGeometry, fill);
        rigGroup.add(mesh);
        return mesh;
      });
      const frames = Array.from({ length: 16 }, () => {
        const mesh = new THREE.Mesh(panelFrameGeometry, solid);
        rigGroup.add(mesh);
        return mesh;
      });
      const slabs = Array.from({ length: 16 }, (_, index) => {
        const mesh = new THREE.Mesh(index < 5 ? blockGeometry : slabGeometry, solid);
        rigGroup.add(mesh);
        return mesh;
      });
      const particles = Array.from({ length: 18 }, () => {
        const mesh = makeEnergy(particleGeometry);
        rigGroup.add(mesh);
        return mesh;
      });
      const wisps = Array.from({ length: 18 }, () => {
        const mesh = new THREE.Mesh(wispGeometry, smoke);
        rigGroup.add(mesh);
        return mesh;
      });
      const markers = Array.from({ length: 16 }, () => {
        const mesh = makeEnergy(markerGeometry);
        rigGroup.add(mesh);
        return mesh;
      });
      const posts = Array.from({ length: 16 }, () => {
        const mesh = new THREE.Mesh(postGeometry, solid);
        rigGroup.add(mesh);
        return mesh;
      });
      const fins = Array.from({ length: 16 }, () => {
        const mesh = new THREE.Mesh(finGeometry, solid);
        rigGroup.add(mesh);
        return mesh;
      });
      const bars = Array.from({ length: 16 }, () => {
        const mesh = new THREE.Mesh(barGeometry, solid);
        rigGroup.add(mesh);
        return mesh;
      });
      const insets = Array.from({ length: 12 }, () => {
        const mesh = new THREE.Mesh(barGeometry, inset);
        rigGroup.add(mesh);
        return mesh;
      });
      rigGroup.visible = false;
      group.add(rigGroup);
      rigs.push({
        group: rigGroup,
        main,
        fill,
        solid,
        inset,
        smoke,
        disc,
        ring,
        rings,
        torus,
        toruses,
        capsule,
        payload,
        pod,
        spool,
        hub,
        cloud,
        plate,
        beam,
        segments,
        panels,
        frames,
        slabs,
        particles,
        wisps,
        markers,
        posts,
        fins,
        bars,
        insets,
        cable,
      });
      const capacityBib = new THREE.Group();
      capacityBib.visible = false;
      capacityBib.userData.cue = 'health-bar';
      orientStatusBib(
        capacityBib,
        (theme.environment3d.camera.yawDegrees * Math.PI) / 180,
        (theme.environment3d.camera.pitchDegrees * Math.PI) / 180,
      );
      const capacityBar = createStatusHealthBar('#f8fafc');
      capacityBib.add(capacityBar.group);
      group.add(capacityBib);
      capacityBars.push({ bib: capacityBib, bar: capacityBar });
      disposables.push(...capacityBar.disposables);
      disposables.push(main, fill, solid, inset, smoke, ...energyMaterials);
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

  const levelMaterial = new THREE.MeshStandardMaterial({
    color: '#cda04a',
    emissive: '#5c4218',
    emissiveIntensity: 0.22,
    transparent: true,
    opacity: 0,
    roughness: 0.88,
    metalness: 0.08,
    depthWrite: false,
  });
  const levelRingGeometry = new THREE.RingGeometry(0.34, 0.42, 40);
  levelRingGeometry.rotateX(-Math.PI / 2);
  const levelFacetGeometry = new THREE.ConeGeometry(0.16, 0.38, 3);
  const levelBeats = Array.from({ length: 8 }, () => {
    const beat = new THREE.Group();
    beat.visible = false;
    beat.userData.kind = 'arc-relay-level-up-beat';
    const ring = new THREE.Mesh(levelRingGeometry, levelMaterial.clone());
    beat.add(ring);
    const facets = Array.from({ length: 3 }, (_, index) => {
      const facet = new THREE.Mesh(levelFacetGeometry, levelMaterial.clone());
      facet.rotation.x = Math.PI;
      facet.rotation.y = (index * Math.PI * 2) / 3;
      beat.add(facet);
      return facet;
    });
    group.add(beat);
    disposables.push(
      ring.material as THREE.Material,
      ...facets.map((facet) => facet.material as THREE.Material),
    );
    return { group: beat, ring, facets };
  });
  disposables.push(levelMaterial, levelRingGeometry, levelFacetGeometry);

  const update = (time: number) => {
    if (indexedTickCount !== replay.ticks.length) {
      indexedTickCount = replay.ticks.length;
      signatureShotYaws = indexSignatureShotYaws(replay);
      signaturePhaseStarts = indexSignaturePhaseStarts(replay);
      surveyFlareLandings = indexSurveyFlareLandings(replay);
      signatureOutcomes = indexSignatureOutcomes(replay);
      constructHull = signatureConstructHullById(replay);
      veterancy = arcVeterancyFor(replay);
    }
    for (const rig of rigs) rig.group.visible = false;
    for (const capacity of capacityBars) capacity.bib.visible = false;
    for (const pool of propPools.values())
      for (const slot of pool) {
        slot.group.visible = false;
      }
    pulse.visible = false;
    for (const ring of beatRings) ring.visible = false;
    for (const beat of levelBeats) beat.group.visible = false;

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
    const reducedMotion = reducedMotionQuery?.matches === true;
    const proofOperation = proofOperationAt();
    const usedPropSlots = new Map<string, number>();
    let used = 0;
    for (const signature of state.visibleSignatures) {
      const phase = arcSignatureVisualPhase(signature, time);
      const phaseStartedTick = signaturePhaseStarts.get(
        `${signature.operationId}:${phase}`,
      ) ?? signature.startedTick;
      const releaseOffset =
        replay.signaturePresentationId === ARC_SIGNATURE_PRESENTATION_ID &&
        signature.signatureId === 'arc-toss' && phase === 'in-flight'
          ? ARC_TOSS_RELEASE_TICK_OFFSET
          : 0;
      if (time < phaseStartedTick + releaseOffset) continue;
      const resolution = resolveArcSignaturePresentation(
        replay,
        signature.signatureId,
      );
      const authoredPainter = resolution.kind === 'authored'
        ? arcSignatureWebGLPainter(resolution.definition.webglPainterId)
        : undefined;
      const legacyPainter = resolution.kind === 'legacy'
        ? LEGACY_WEBGL_PAINTERS[signature.signatureId]
        : undefined;
      const marker = resolution.kind === 'authored'
        ? resolution.definition.canvasMarker
        : resolution.kind === 'legacy'
          ? legacyPainter
          : undefined;
      if (phase === 'hidden') continue;
      const rigIndex = used++;
      const rig = rigAt(rigIndex);
      const capacityBar = capacityBars[rigIndex]!;
      resetRig(rig);
      rig.group.visible = true;
      rig.group.userData.operationId = signature.operationId;
      rig.group.userData.signatureId = signature.signatureId;
      rig.group.userData.phase = phase;
      if (proofOperation !== null)
        rig.group.visible = signature.operationId === proofOperation;
      const accent = new THREE.Color(
        unitAccent(replay, signature.ownerActor.unitKey),
      );
      const suppressed = signature.suppressed;
      const pulsePhase = 0.5 + 0.5 * Math.sin(time * Math.PI * 3);
      rig.main.color.copy(suppressed ? new THREE.Color('#dce5eb') : accent);
      prepareEnergyMeshes(
        rig,
        suppressed ? new THREE.Color('#dce5eb') : accent,
        (phase === 'tell' ? 0.42 + pulsePhase * 0.2 : 0.72) *
          (suppressed ? 0.55 : 1),
      );
      rig.fill.color.copy(
        marker === 'dark-field'
          ? new THREE.Color('#17212a')
          : marker === 'smoke-field'
            ? new THREE.Color('#64717c')
            : accent,
      );
      rig.solid.color.copy(accent).lerp(new THREE.Color('#dbe7ea'), 0.3);
      rig.solid.emissive.copy(accent).multiplyScalar(0.16);
      rig.solid.emissiveIntensity = 0.18;
      rig.solid.roughness = 0.92;
      rig.solid.metalness = 0.06;
      rig.solid.opacity = suppressed ? 0.48 : 0.82;
      rig.inset.color.set('#1d2529');
      rig.inset.emissive.set('#070b0d');
      rig.inset.emissiveIntensity = 0.02;
      rig.inset.roughness = 1;
      rig.inset.metalness = 0.02;
      rig.inset.opacity = suppressed ? 0.4 : 0.96;
      rig.smoke.color.set('#66727c');
      rig.smoke.emissive.set('#121a20');
      rig.smoke.emissiveIntensity = 0.12;
      rig.main.opacity =
        (phase === 'tell' ? 0.42 + pulsePhase * 0.2 : 0.72) *
        (suppressed ? 0.55 : 1);
      rig.fill.opacity =
        (phase === 'tell' ? 0.055 : marker === 'smoke-field' ? 0.2 : 0.13) *
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
      const visualPhaseStartedTick = phaseStartedTick + releaseOffset;
      const phaseAge = Math.max(0, time - visualPhaseStartedTick);
      const coreFlightState = signature.signatureId === 'arc-toss' &&
          phase === 'in-flight'
        ? state.visibleCores.find((core) => {
            if (core.disposition !== 'in-flight' || !core.flightTarget)
              return false;
            const endpoints = signature.positions;
            return endpoints.some(
              (point) =>
                point.x === core.position.x && point.y === core.position.y,
            ) && endpoints.some(
              (point) =>
                point.x === core.flightTarget!.x &&
                point.y === core.flightTarget!.y,
            );
          })
        : undefined;
      const coreFlight = coreFlightState?.flightTarget
        ? {
            source: worldPoint(coreFlightState.position),
            destination: worldPoint(coreFlightState.flightTarget),
            progress: Math.max(0, Math.min(
              1,
              phaseAge / Math.max(
                1,
                (signature.completesAtTick ?? visualPhaseStartedTick + 1)
                  - visualPhaseStartedTick,
              ),
            )),
          }
        : undefined;
      let persistentPropReady = false;
      let capacityBibHeight = 0.78;
      let capacityMaximum = signature.maximumCapacity ?? null;
      if (
        (phase === 'active' ||
          (signature.signatureId === 'survey-flare' && phase === 'in-flight')) &&
        resolution.kind !== 'generic' &&
        (signature.signatureId === 'trip-node' ||
          signature.signatureId === 'sentinel-seed' ||
          signature.signatureId === 'survey-flare') &&
        points[0]
      ) {
        const poolIndex = usedPropSlots.get(signature.signatureId) ?? 0;
        usedPropSlots.set(signature.signatureId, poolIndex + 1);
        const slot = propAt(signature, poolIndex);
        slot.group.visible = true;
        slot.group.userData.operationId = signature.operationId;
        const deploy = reducedMotion
          ? 1
          : easeOutDeploy(Math.min(1, phaseAge / 0.9));
        const propScale = ARC_SIGNATURE_PROP_SCALE[signature.signatureId];
        // Persistent props occupy their replay-owned point. Survey Flare uniquely reuses
        // one rigid model in flight and upright after landing; the scan field remains a
        // separate authoritative overlay. Sentinel instead grows upward from its planted
        // seed/socket rather than dropping from the sky.
        const sentinel = signature.signatureId === 'sentinel-seed';
        const surveyFlare = signature.signatureId === 'survey-flare';
        const fromY = sentinel || surveyFlare ? 0 : -0.24;
        if (surveyFlare && phase === 'in-flight') {
          const { from, to } = surveyFlareFlightEndpoints(points, owner)!;
          const landing = surveyFlareLandings.get(signature.operationId);
          const phaseProgress = signaturePhaseProgress(signature, time);
          const progress = easeOutDeploy(phaseProgress);
          slot.group.position.lerpVectors(from, to, progress);
          slot.group.position.y = surveyFlareFlightHeight(progress);
          slot.group.rotation.y = landing?.yaw ??
            -Math.atan2(to.z - from.z, to.x - from.x);
          if (slot.model) {
            const rotation = surveyFlareFlightRotation(
              phaseProgress,
              reducedMotion,
            );
            slot.model.rotation.x = rotation.tumbleX;
            slot.model.rotation.z = rotation.uprightRollZ;
          }
        } else {
          // Preserve Survey's recorded flight destination when available. A replay
          // that begins after landing has only the reveal footprint, whose centre is
          // the truthful fallback (rather than its canonically sorted first tile).
          const landing = surveyFlare
            ? surveyFlareLandings.get(signature.operationId)
            : undefined;
          const propPosition = surveyFlare
            ? landing?.position ?? signatureAreaBounds(points).centre
            : points[0]!;
          slot.group.position.copy(propPosition).setY(
            surveyFlare ? SURVEY_FLARE_LANDED_HEIGHT : fromY * (1 - deploy),
          );
          slot.group.rotation.y = surveyFlare
            ? landing
              ? surveyFlareLandedYaw(landing.yaw, phaseAge)
              : time * SURVEY_FLARE_IDLE_YAW_PER_TICK
            : 0;
          if (slot.model) {
            slot.model.rotation.x = 0;
            slot.model.rotation.z = surveyFlare ? Math.PI / 2 : 0;
          }
        }
        if (sentinel) {
          const width = 0.3 + deploy * 0.7;
          slot.group.scale.set(
            propScale * width,
            propScale * Math.max(0.045, deploy),
            propScale * width,
          );
        } else {
          const scaleSettle = reducedMotion
            ? 1
            : 1 + Math.sin(Math.PI * Math.min(1, phaseAge / 0.9)) * 0.06;
          slot.group.scale.setScalar(propScale * scaleSettle);
        }
        const propYaw =
          signature.signatureId === 'sentinel-seed'
            ? latestObservedSignatureYaw(
                signatureShotYaws.get(signature.operationId) ?? [],
                time,
              ) ?? 0
            : 0;
        const yawTarget = sentinelYawTarget(slot.group, slot.model);
        if (signature.signatureId !== 'survey-flare')
          slot.group.rotation.y = yawTarget === slot.group ? propYaw : 0;
        if (slot.yawHead) slot.yawHead.rotation.y = propYaw;
        paintSignaturePropBody(
          slot.emissives,
          slot.materials,
          signature.suppressed,
          time,
          deploy,
        );
        capacityBibHeight = constructStatusBibHeight(slot.group, slot.model);
        capacityMaximum ??= constructHull.get(signature.signatureId) ?? null;
        persistentPropReady = slot.ready;
      }
      if (authoredPainter) {
        authoredPainter.paint({
          rig,
          signature,
          points,
          owner,
          target,
          coreFlight,
          arenaTheme: theme,
          time,
          phase: phase as ArcSignatureWebGLPhase,
          persistentPropReady,
          reducedMotion,
          phaseProgress: signaturePhaseProgress(signature, time),
          phaseStartedTick,
          phaseAge,
          tickFraction: time - Math.floor(time),
        });
      } else if (legacyPainter)
        paintLegacySignature(
          rig,
          legacyPainter,
          points,
          owner,
          target,
          time,
          phase,
          persistentPropReady,
        );
      else paintGenericSignature(rig, points, owner, target, phase);
      if (capacityMaximum !== null && points[0]) {
        capacityBar.bib.visible = rig.group.visible;
        capacityBar.bib.userData.signatureId = signature.signatureId;
        capacityBar.bib.userData.forSignatureOperationId = signature.operationId;
        capacityBar.bar.frameMaterial.color.copy(accent);
        paintStatusHealthBar(
          capacityBar.bar,
          signature.remainingCapacity,
          capacityMaximum,
        );
        capacityBar.bib.userData.currentHealth = signature.remainingCapacity;
        capacityBar.bib.userData.maximumHealth = capacityMaximum;
        capacityBar.bib.userData.remainingFraction =
          capacityBar.bar.group.userData.remainingFraction;
        placeStatusBib(
          capacityBar.bib,
          points[0].x,
          points[0].z,
          capacityBibHeight,
        );
      }
      // Some public events share an operation ID before they carry enough
      // topology for a particular painter. Do not publish an empty effect
      // group merely because the operation is present.
      rig.group.visible = rig.group.visible &&
        rig.group.children.some((child) => child.visible);
    }

    for (const outcome of signatureOutcomesAt(signatureOutcomes, time)) {
      const { operationId, facts, fraction, priorSignature, sourceTick } = outcome;
      const first = facts[0]!;
      const resolution = resolveArcSignaturePresentation(
        replay,
        first.signatureId,
      );
      if (resolution.kind !== 'authored') continue;
      const painter = arcSignatureWebGLPainter(
        resolution.definition.webglPainterId,
      );
      if (!painter?.paintOutcome) continue;
      const rig = rigAt(used++);
      resetRig(rig);
      rig.group.visible = true;
      rig.group.userData.signatureId = first.signatureId;
      rig.group.userData.operationId = operationId;
      rig.group.userData.phase = 'outcome';
      if (proofOperation !== null)
        rig.group.visible = operationId === proofOperation;
      const accent = new THREE.Color(
        unitAccent(replay, first.ownerActor.unitKey),
      );
      rig.main.color.copy(accent);
      rig.fill.color.copy(accent);
      rig.solid.color.copy(accent).lerp(new THREE.Color('#dbe7ea'), 0.3);
      rig.solid.emissive.copy(accent).multiplyScalar(0.16);
      rig.solid.emissiveIntensity = 0.18;
      rig.solid.roughness = 0.92;
      rig.solid.metalness = 0.06;
      rig.inset.color.set('#1d2529');
      rig.inset.emissive.set('#070b0d');
      rig.inset.emissiveIntensity = 0.02;
      rig.inset.roughness = 1;
      rig.inset.metalness = 0.02;
      prepareEnergyMeshes(rig, accent, 0.72);
      rig.smoke.color.set('#66727c');
      rig.smoke.emissive.set('#121a20');
      rig.smoke.emissiveIntensity = 0.12;
      rig.main.opacity = 0.72;
      rig.fill.opacity = 0.12;
      rig.solid.opacity = 0.82;
      rig.inset.opacity = 0.96;
      const ownerPose = poses.find(
        (pose) => pose.actorKey === first.ownerActor.actorKey,
      );
      const outcomeTargetActor = facts.find(
        (fact) => 'targetActor' in fact,
      )?.targetActor ?? priorSignature?.targetActor;
      const outcomeTargetPose = outcomeTargetActor
        ? poses.find((pose) => pose.actorKey === outcomeTargetActor.actorKey)
        : undefined;
      painter.paintOutcome({
        rig,
        signatureId: first.signatureId,
        priorSignature,
        facts,
        owner: ownerPose
          ? new THREE.Vector3(ownerPose.x + 0.5, 0.08, ownerPose.y + 0.5)
          : priorSignature?.positions[0]
            ? worldPoint(priorSignature.positions[0])
            : undefined,
        target: outcomeTargetPose
          ? new THREE.Vector3(
              outcomeTargetPose.x + 0.5,
              0.08,
              outcomeTargetPose.y + 0.5,
            )
          : undefined,
        priorTarget: priorSignature?.targetActor
          ? (() => {
              const pose = poses.find(
                (candidate) =>
                  candidate.actorKey === priorSignature.targetActor!.actorKey,
              );
              return pose
                ? new THREE.Vector3(pose.x + 0.5, 0.08, pose.y + 0.5)
                : undefined;
            })()
          : undefined,
        arenaTheme: theme,
        time,
        sourceTick,
        fraction,
        reducedMotion,
      });
      rig.group.visible = rig.group.visible &&
        rig.group.children.some((child) => child.visible);
    }

    paintLevelUpBeats(veterancy, poses, time, levelBeats);

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

function easeOutDeploy(value: number): number {
  const clamped = Math.max(0, Math.min(1, value));
  return 1 - (1 - clamped) ** 3;
}

/** Dynamic so one review page can retain decoded models while it records every operation. */
function proofOperationAt(): string | null {
  if (typeof window === 'undefined') return null;
  const query = new URLSearchParams(window.location.search);
  return query.has('proof') ? query.get('proof-operation') : null;
}

function prepareEnergyMeshes(
  rig: ArcSignatureEffectRig,
  colour: THREE.Color,
  opacity: number,
): void {
  for (const mesh of [
    rig.ring,
    ...rig.rings,
    rig.torus,
    ...rig.toruses,
    rig.beam,
    ...rig.segments,
    ...rig.particles,
    ...rig.markers,
  ]) {
    const material = mesh.material as THREE.MeshBasicMaterial;
    material.color.copy(colour);
    material.opacity = opacity;
  }
}

function resetRig(rig: ArcSignatureEffectRig): void {
  // Rigs are pooled by draw order, not permanently owned by one signature. Reset the
  // complete transform as well as visibility: Survey's airborne payload once left four
  // turns of X rotation on the same mesh its landed instrument borrowed, so a direct seek
  // and a played landing produced different frames. A pure painter must always start from
  // the same identity transform, regardless of which phase or signature used this slot in
  // the previous rendered frame.
  for (const mesh of [
    rig.disc,
    rig.ring,
    ...rig.rings,
    rig.torus,
    ...rig.toruses,
    rig.capsule,
    rig.payload,
    rig.pod,
    rig.spool,
    rig.hub,
    rig.cloud,
    rig.plate,
    rig.beam,
    rig.cable,
    ...rig.segments,
    ...rig.panels,
    ...rig.frames,
    ...rig.slabs,
    ...rig.particles,
    ...rig.wisps,
    ...rig.markers,
    ...rig.posts,
    ...rig.fins,
    ...rig.bars,
    ...rig.insets,
  ]) {
    mesh.visible = false;
    mesh.position.set(0, 0, 0);
    mesh.rotation.set(0, 0, 0);
    mesh.scale.set(1, 1, 1);
  }
  for (const panel of rig.panels) panel.material = rig.fill;
  for (const particle of rig.particles) particle.material = rig.main;
  for (const wisp of rig.wisps) wisp.material = rig.smoke;
  rig.smoke.opacity = 0;
}

function paintLegacySignature(
  rig: ArcSignatureEffectRig,
  painter: ArcSignatureCanvasMarker,
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

  if (
    painter === 'calibration-field' ||
    painter === 'dark-field' ||
    painter === 'smoke-field'
  ) {
    const area = areaBounds(points.length > 0 ? points : [anchor]);
    rig.disc.visible = true;
    rig.disc.position.copy(area.centre).setY(0.032);
    rig.disc.scale.set(area.width, 1, area.depth);
    rig.ring.visible = true;
    rig.ring.position.copy(area.centre).setY(0.04);
    rig.ring.scale.set(area.width * tellScale, 1, area.depth * tellScale);
    rig.ring.rotation.y = painter === 'calibration-field' ? time * 0.35 : -time * 0.12;
    if (painter === 'calibration-field') {
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
    } else if (painter === 'dark-field') {
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

  if (painter === 'panels' || painter === 'block') {
    for (const [index, point] of points.entries()) {
      const panel = rig.panels[index];
      if (!panel) break;
      panel.visible = true;
      panel.position.copy(point).setY(painter === 'panels' ? 0.33 : 0.24);
      panel.scale.setScalar(tellScale * (painter === 'panels' ? 1 : 0.82));
      if (painter === 'panels' && points[index + 1])
        panel.rotation.y = -Math.atan2(
          points[index + 1]!.z - point.z,
          points[index + 1]!.x - point.x,
        );
    }
    return;
  }

  if (painter === 'cardinal-target') {
    targetRing(rig, anchor, tellScale);
    lineAt(rig.segments[0]!, anchor.clone().add(new THREE.Vector3(-0.42, 0, 0)), anchor.clone().add(new THREE.Vector3(0.42, 0, 0)), 0.055);
    lineAt(rig.segments[1]!, anchor.clone().add(new THREE.Vector3(0, 0, -0.42)), anchor.clone().add(new THREE.Vector3(0, 0, 0.42)), 0.055);
    rig.beam.visible = true;
    rig.beam.position.copy(anchor).setY(0.75);
    rig.beam.scale.y = 1.4;
    return;
  }

  if (painter === 'prop') {
    if (!persistentPropReady) {
      for (const [index, point] of points.entries()) {
        const marker = rig.markers[index];
        if (!marker) break;
        marker.visible = true;
        marker.position.copy(point).setY(0.14);
        marker.rotation.y = time * 0.22 + index;
        if (index > 0)
          lineAt(rig.segments[index - 1]!, points[index - 1]!, point, 0.035);
      }
    }
    targetRing(rig, anchor, tellScale * 0.62);
    if (phase === 'active') rig.main.opacity *= 0.58;
    return;
  }

  if (painter === 'objective-arc' && owner && target) {
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

  if (
    (painter === 'tether' ||
      painter === 'braid' ||
      painter === 'paired-fold') &&
    owner && target
  ) {
    lineAt(rig.segments[0]!, owner, target, painter === 'braid' ? 0.085 : 0.055);
    if (painter === 'braid') {
      const offset = new THREE.Vector3(0, 0.04, 0.08);
      lineAt(rig.segments[1]!, owner.clone().add(offset), target.clone().add(offset), 0.025);
      for (let index = 0; index < 8; index += 1) {
        const particle = rig.particles[index]!;
        const progress = (time * 0.55 + index / 8) % 1;
        particle.visible = true;
        particle.position.lerpVectors(owner, target, progress).setY(0.16 + Math.sin(progress * Math.PI) * 0.12);
        particle.scale.setScalar(0.55);
      }
    } else if (painter === 'paired-fold') {
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

  if (painter === 'pressure-lane') {
    const path = points.length > 1 ? points : owner && target ? [owner, target] : [anchor];
    for (let index = 1; index < path.length; index += 1)
      lineAt(rig.segments[index - 1]!, path[index - 1]!, path[index]!, 0.11);
    return;
  }

  if (painter === 'target-brackets') {
    targetBrackets(rig, target ?? anchor, tellScale);
    return;
  }

  if (painter === 'pressure-ring') {
    targetRing(rig, anchor, tellScale * (phase === 'tell' ? 1 : 1.4));
    return;
  }

  if (painter === 'arrow') {
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

/** Truthful fallback for a package or signature this build cannot decorate. */
function paintGenericSignature(
  rig: ArcSignatureEffectRig,
  points: THREE.Vector3[],
  owner: THREE.Vector3 | undefined,
  target: THREE.Vector3 | undefined,
  phase: Exclude<ArcSignatureVisualPhase, 'hidden'>,
): void {
  const anchors = points.length > 0
    ? points
    : [target ?? owner].filter((point): point is THREE.Vector3 => point !== undefined);
  for (const [index, point] of anchors.slice(0, rig.markers.length).entries()) {
    const marker = rig.markers[index]!;
    marker.visible = true;
    marker.position.copy(point).setY(0.11);
    marker.scale.setScalar(phase === 'tell' ? 0.75 : 0.58);
  }
  for (let index = 1; index < anchors.length && index <= rig.segments.length; index++)
    lineAt(rig.segments[index - 1]!, anchors[index - 1]!, anchors[index]!, 0.04);
}

function targetRing(
  rig: ArcSignatureEffectRig,
  position: THREE.Vector3,
  scale: number,
): void {
  rig.ring.visible = true;
  rig.ring.position.copy(position).setY(0.045);
  rig.ring.scale.setScalar(scale);
}

function targetBrackets(
  rig: ArcSignatureEffectRig,
  position: THREE.Vector3,
  scale: number,
): void {
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

function arcStateBefore(
  replay: ReplayModel,
  tickIndex: number,
): ReplayArcRelayModeState | null {
  const immediate = replay.ticks[tickIndex]?.before.mode;
  if (immediate?.kind === 'arc-relay' && 'visibleSignatures' in immediate)
    return immediate;
  const previous = replay.ticks[tickIndex - 1]?.after.mode;
  return previous?.kind === 'arc-relay' && 'visibleSignatures' in previous
    ? previous
    : null;
}

interface IndexedSignatureOutcome {
  sourceTick: number;
  operationId: string;
  facts: ArcSignatureOutcomeFact[];
  priorSignature: Signature | null;
}

interface SignatureOutcomeSample extends IndexedSignatureOutcome {
  /** Normalized age through the renderer-only linger; zero at the causal fact. */
  fraction: number;
}

function indexSignatureOutcomes(replay: ReplayModel): IndexedSignatureOutcome[][] {
  return replay.ticks.map((source, sourceTick) => {
    const grouped = new Map<string, ArcSignatureOutcomeFact[]>();
    for (const event of [...source.lifecycleEvents, ...source.events]) {
      const fact = event.arcRelayFact;
      if (!fact || !('operationId' in fact)) continue;
      const facts = grouped.get(fact.operationId) ?? [];
      facts.push(fact);
      grouped.set(fact.operationId, facts);
    }
    const priorState = arcStateBefore(replay, sourceTick);
    const afterState =
      source.after.mode?.kind === 'arc-relay' &&
      'visibleSignatures' in source.after.mode
        ? source.after.mode
        : null;
    // Format-2 spectator broadcasts can omit expiry events while still publishing the
    // exact before/after operation removal. Synthesize only the already-public removal
    // fact shape needed by painters; this is presentation indexing, not replay mutation.
    for (const prior of priorState?.visibleSignatures ?? []) {
      if (afterState?.visibleSignatures.some(
        (candidate) => candidate.operationId === prior.operationId,
      )) continue;
      const existing = grouped.get(prior.operationId);
      if (existing?.some(
        (fact) => fact.kind === 'signature-changed' && fact.phase === null,
      )) continue;
      const facts = existing ?? [];
      facts.push({
        kind: 'signature-changed',
        operationId: prior.operationId,
        signatureId: prior.signatureId,
        ownerActor: prior.ownerActor,
        phase: null,
        reason: prior.endsAtTick !== null && sourceTick >= prior.endsAtTick
          ? 'expired'
          : 'removed',
      });
      grouped.set(prior.operationId, facts);
    }
    return [...grouped].map(([operationId, facts]) => ({
      sourceTick,
      operationId,
      facts,
      priorSignature: priorState?.visibleSignatures.find(
        (signature) => signature.operationId === operationId,
      ) ?? null,
    }));
  });
}

/**
 * Retain one-shot signature outcomes long enough to read at ordinary playback speed.
 *
 * The authoritative fact remains on exactly one tick. This only samples that already-
 * public fact for 1.8 visual ticks, and derives every frame from `time`, so pause, seek and
 * continuous playback are identical. A later retained tick never reveals future state.
 */
function signatureOutcomesAt(
  index: readonly (readonly IndexedSignatureOutcome[])[],
  time: number,
): SignatureOutcomeSample[] {
  const tickIndex = Math.max(0, Math.min(Math.floor(time), index.length - 1));
  const firstTick = Math.max(0, Math.ceil(time - SIGNATURE_OUTCOME_LINGER_TICKS));
  const result: SignatureOutcomeSample[] = [];
  for (let sourceTick = firstTick; sourceTick <= tickIndex; sourceTick += 1) {
    const age = time - sourceTick;
    if (age < 0 || age >= SIGNATURE_OUTCOME_LINGER_TICKS) continue;
    for (const outcome of index[sourceTick] ?? []) {
      result.push({
        ...outcome,
        fraction: age / SIGNATURE_OUTCOME_LINGER_TICKS,
      });
    }
  }
  return result;
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
    for (const traversal of tick.projectileTraversals) {
      if (
        traversal.attackProfileId !== 'sentinel-bolt' ||
        traversal.trigger !== 'attack-launch' ||
        traversal.path.length === 0
      ) continue;
      const signature = state.visibleSignatures.find(
        (candidate) =>
          candidate.signatureId === 'sentinel-seed' &&
          candidate.ownerActor.actorKey === traversal.ownerActor.actorKey &&
          candidate.positions.some(
            (anchor) =>
              anchor.x === traversal.from.x && anchor.y === traversal.from.y,
          ),
      );
      if (!signature) continue;
      const first = traversal.path[0]!;
      const shots = result.get(signature.operationId) ?? [];
      recordSignatureShotYaw(
        shots,
        tickIndex,
        -Math.atan2(
          first.y - traversal.from.y,
          first.x - traversal.from.x,
        ),
      );
      result.set(signature.operationId, shots);
    }
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
      // Modern replays publish both the launch traversal and the matching damage fact.
      // They describe one shot, so retain the launch bearing and use damage only as the
      // compatibility fallback for older broadcasts.
      recordSignatureShotYaw(shots, tickIndex, -Math.atan2(dz, dx));
      result.set(fact.operationId, shots);
    }
  }
  return result;
}

function recordSignatureShotYaw(
  shots: SignatureShotYaw[],
  tick: number,
  yaw: number,
): void {
  if (shots.some((shot) => shot.tick === tick)) return;
  shots.push({ tick, yaw });
}

function indexSignaturePhaseStarts(replay: ReplayModel): Map<string, number> {
  const result = new Map<string, number>();
  for (const [tickIndex, tick] of replay.ticks.entries()) {
    const mode = tick.after.mode;
    if (mode?.kind !== 'arc-relay' || !('visibleSignatures' in mode)) continue;
    for (const signature of mode.visibleSignatures) {
      const key = `${signature.operationId}:${signature.phase}`;
      if (!result.has(key)) result.set(key, tickIndex);
    }
  }
  return result;
}

/**
 * Remember each Survey Flare's authoritative flight destination and bearing so the
 * same rigid prop can cross the discrete in-flight/active replay boundary without
 * changing its transform. Replays that begin after landing retain the old footprint
 * centre fallback.
 */
function indexSurveyFlareLandings(
  replay: ReplayModel,
): Map<string, SurveyFlareLanding> {
  const result = new Map<string, SurveyFlareLanding>();
  for (const [tickIndex, tick] of replay.ticks.entries()) {
    const mode = tick.after.mode;
    if (mode?.kind !== 'arc-relay' || !('visibleSignatures' in mode)) continue;
    const candidates = mode.visibleSignatures.filter(
      (signature) =>
        signature.signatureId === 'survey-flare' &&
        signature.phase === 'in-flight' &&
        !result.has(signature.operationId),
    );
    if (candidates.length === 0) continue;
    const poses = posesAt(replay, tickIndex);
    for (const signature of candidates) {
      const points = signature.positions.map(worldPoint);
      const ownerPose = poses.find(
        (pose) => pose.actorKey === signature.ownerActor.actorKey,
      );
      const owner = ownerPose
        ? new THREE.Vector3(ownerPose.x + 0.5, 0.08, ownerPose.y + 0.5)
        : points[0];
      const flight = surveyFlareFlightEndpoints(points, owner);
      if (!flight) continue;
      result.set(signature.operationId, {
        position: flight.to.clone(),
        yaw: -Math.atan2(
          flight.to.z - flight.from.z,
          flight.to.x - flight.from.x,
        ),
      });
    }
  }
  return result;
}

function paintSignaturePropBody(
  emissives: readonly { material: THREE.MeshStandardMaterial; base: number }[],
  materials: readonly {
    material: THREE.MeshStandardMaterial;
    opacity: number;
    transparent: boolean;
  }[],
  suppressed: boolean,
  time: number,
  deploy: number,
): void {
  const breathing = 0.95 + Math.sin(time * Math.PI * 0.42) * 0.05;
  for (const { material, base } of emissives)
    material.emissiveIntensity = base * (suppressed ? 0.22 : breathing);
  for (const { material, opacity, transparent } of materials) {
    const reveal = 0.24 + deploy * 0.76;
    material.transparent = transparent || reveal < 0.999;
    material.opacity = opacity * reveal * (suppressed ? 0.58 : 1);
  }
}

function signaturePropMaterials(
  model: THREE.Group,
): {
  material: THREE.MeshStandardMaterial;
  opacity: number;
  transparent: boolean;
}[] {
  const materials = new Set<THREE.MeshStandardMaterial>();
  model.traverse((node) => {
    const mesh = node as THREE.Mesh;
    if (!mesh.isMesh) return;
    for (const material of Array.isArray(mesh.material)
      ? mesh.material
      : [mesh.material]) {
      if (material instanceof THREE.MeshStandardMaterial) materials.add(material);
    }
  });
  return [...materials].map((material) => ({
    material,
    opacity: material.opacity,
    transparent: material.transparent,
  }));
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

interface LevelUpBeatRig {
  group: THREE.Group;
  ring: THREE.Mesh;
  facets: THREE.Mesh[];
}

/** Pure playhead sampling: direct seek and continuous playback paint identically. */
function paintLevelUpBeats(
  veterancy: ArcVeterancyIndex,
  poses: readonly BotPose[],
  time: number,
  rigs: readonly LevelUpBeatRig[],
): void {
  let used = 0;
  for (const pose of poses) {
    const event = veterancy.levelUpAt(
      time,
      pose.teamId,
      pose.unitId,
      pose.lifeId,
    );
    if (!event) continue;
    const rig = rigs[used++];
    if (!rig) break;
    const progress = event.age / LEVEL_UP_BEAT_TICKS;
    const ringProgress = Math.min(1, event.age / 0.42);
    const facetProgress = Math.max(0, Math.min(1, (event.age - 0.16) / 0.64));
    const alpha = Math.max(0, 1 - progress ** 1.45);
    rig.group.visible = true;
    rig.group.position.set(pose.x + 0.5, 0.09, pose.y + 0.5);
    rig.ring.scale.setScalar(0.82 + ringProgress * 2.15);
    (rig.ring.material as THREE.MeshStandardMaterial).opacity = alpha * 0.78;
    for (const [index, facet] of rig.facets.entries()) {
      const angle = -Math.PI / 2 + (index * Math.PI * 2) / 3;
      const radius = 0.92 - facetProgress * 0.42;
      facet.position.set(
        Math.cos(angle) * radius,
        0.18 + facetProgress * 0.78,
        Math.sin(angle) * radius,
      );
      facet.rotation.y = -angle;
      (facet.material as THREE.MeshStandardMaterial).opacity = alpha * 0.82;
    }
  }
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
