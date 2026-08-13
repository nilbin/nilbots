import * as THREE from 'three';
import type { LookModelSpec } from './lookModel';

const MAX_LEAN = 0.1;
const MAX_PITCH = 0.075;
const WHEEL_RADIUS = 0.09;
/** Maximum inner-hull follow-through, in tile-space model units. */
const HULL_LAG = 0.055;

export type ArcSignatureBodyState = 'ready' | 'cooldown' | 'active';

export interface ArcSignatureBodyPose {
  /** Uniform root scale. Kept deliberately small so tile occupancy stays honest. */
  scale: number;
  /** Vertical whole-body travel in chassis widths. */
  rise: number;
  /** Local forward/back travel. Positive is toward the authored nose. */
  forward: number;
  /** Local lateral travel. */
  lateral: number;
  /** Roll about the local forward axis. */
  roll: number;
  /** Pitch about the local lateral axis. */
  pitch: number;
  /** Small local yaw gesture; authoritative chassis facing remains on the parent. */
  yaw: number;
}

const REST_SIGNATURE_POSE: Readonly<ArcSignatureBodyPose> = Object.freeze({
  scale: 1,
  rise: 0,
  forward: 0,
  lateral: 0,
  roll: 0,
  pitch: 0,
  yaw: 0,
});

/**
 * Whole-body acting for Arc signatures.
 *
 * The approved Meshy bodies are monolithic, so the renderer cannot pretend they have
 * joints they do not. It can still give each public cast a recognisable physical verb:
 * brace, recoil, lean, lift, deploy, or fold. Every channel is a pure function of the
 * replay-authored signature ID/phase and phase age; direct seek and played motion match.
 */
export function arcSignatureBodyPose(
  signatureId: string | null,
  phase: 'tell' | 'active' | 'channel' | 'in-flight' | null,
  phaseAge: number,
  reducedMotion = false,
): ArcSignatureBodyPose {
  if (!signatureId || !phase) return { ...REST_SIGNATURE_POSE };
  const motion = reducedMotion ? 0.34 : 1;
  const age = Math.max(0, phaseAge);
  const charge = smoothstep(0, phase === 'tell' ? 0.82 : 0.58, age);
  const opening = Math.sin(Math.PI * clamp01(age / 1.15));
  // A maintained field must not leave its caster permanently crouched or leaning for
  // the next thirty ticks. Tells can hold their loaded pose through resolution; an
  // already-active/in-flight construct gets one visible deploy gesture and then the body
  // returns to its ordinary locomotion rig. Channels are the one honest exception: the
  // caster is still doing work for as long as the replay says the channel is public.
  const commit = phase === 'tell'
    ? charge
    : phase === 'channel'
      ? 0.72 + 0.28 * opening
      : opening;
  const working = phase === 'channel' || phase === 'active'
    ? 0.5 + 0.5 * Math.sin(age * Math.PI * 1.25)
    : opening;

  const pose = { ...REST_SIGNATURE_POSE };
  switch (signatureId) {
    case 'vector-dash':
      pose.scale += 0.06 * opening * motion;
      pose.rise += 0.06 * opening * motion;
      pose.pitch -= 0.12 * commit * motion;
      break;
    case 'prism-wall':
    case 'hardlight-block':
      pose.scale += 0.08 * opening * motion;
      pose.rise -= 0.055 * commit * motion;
      pose.pitch += 0.15 * commit * motion;
      break;
    case 'tractor-hook':
      pose.forward -= 0.14 * commit * motion;
      pose.rise -= 0.035 * commit * motion;
      pose.pitch += 0.2 * commit * motion;
      break;
    case 'repair-beam':
      pose.forward += 0.07 * (0.5 + 0.5 * working) * motion;
      pose.rise += 0.045 * working * motion;
      pose.roll += 0.045 * Math.sin(age * Math.PI * 1.15) * motion;
      break;
    case 'survey-flare':
    case 'smoke-canister':
    case 'arc-toss':
      pose.rise += 0.11 * opening * motion;
      pose.pitch -= 0.18 * commit * motion;
      pose.forward += 0.06 * commit * motion;
      break;
    case 'falling-star':
    case 'rail-line':
      pose.scale += 0.07 * opening * motion;
      pose.rise -= 0.08 * commit * motion;
      pose.forward -= 0.11 * commit * motion;
      pose.pitch += 0.22 * commit * motion;
      break;
    case 'trip-node':
    case 'sentinel-seed':
      pose.rise -= 0.07 * commit * motion;
      pose.forward += 0.1 * commit * motion;
      pose.pitch -= 0.24 * commit * motion;
      break;
    case 'null-field':
      pose.scale -= 0.07 * commit * motion;
      pose.rise -= 0.045 * commit * motion;
      pose.yaw -= 0.13 * commit * motion;
      break;
    case 'exchange':
      pose.rise += 0.09 * opening * motion;
      pose.scale += 0.03 * opening * motion;
      break;
    case 'target-paint':
      pose.rise += 0.12 * commit * motion;
      pose.scale += 0.045 * commit * motion;
      pose.pitch -= 0.08 * commit * motion;
      break;
    case 'kinetic-burst':
      pose.scale += 0.12 * opening * motion;
      pose.rise -= 0.1 * commit * motion;
      pose.pitch += 0.1 * commit * motion;
      break;
  }
  return pose;
}

export interface ArcMotionInput {
  time: number;
  fraction: number;
  facingAngle: number;
  motionX: number;
  motionY: number;
  /** Already-revealed displacement from the preceding tick. */
  previousMotionX: number;
  /** Already-revealed displacement from the preceding tick. */
  previousMotionY: number;
  previousSpeed: number;
  turnDelta: number;
  signedTravel: number;
  braced: boolean;
  signatureState: ArcSignatureBodyState;
}

export interface ArcMotionFrame {
  bank: number;
  pitch: number;
  counterSteer: number;
  hardwareYaw: number;
  wheelRotation: number;
  wakeYaw: number;
  wakeStrength: number;
  hullLagForward: number;
  hullLagLateral: number;
  idleGain: number;
  emissiveGain: number;
  ventStrength: number;
}

/**
 * Renderer-only pose channels derived entirely from the replay sample and stable time.
 * There is deliberately no previous-frame state here: scrubbing to a moment produces the
 * same pose as playing into it, while facing remains untouched on the parent chassis.
 */
export function arcMotionFrame(
  spec: LookModelSpec,
  input: ArcMotionInput,
): ArcMotionFrame {
  const tuning = spec.motion;
  if (!tuning)
    return {
      bank: 0,
      pitch: 0,
      counterSteer: 0,
      hardwareYaw: 0,
      wheelRotation: 0,
      wakeYaw: 0,
      wakeStrength: 0,
      hullLagForward: 0,
      hullLagLateral: 0,
      idleGain: 0,
      emissiveGain: 1,
      ventStrength: 0,
    };

  const cos = Math.cos(input.facingAngle);
  const sin = Math.sin(input.facingAngle);
  // Blend from the last revealed displacement into this tick's. The chassis root still
  // lands on the exact recorded position; only the inner hull carries a few centimetres
  // of follow-through, which softens a slow move/hold cadence without making the occupied
  // tile unclear or anticipating a future action.
  const inertia = easeInOut(clamp01(input.fraction));
  const inertialMotionX =
    input.previousMotionX + (input.motionX - input.previousMotionX) * inertia;
  const inertialMotionY =
    input.previousMotionY + (input.motionY - input.previousMotionY) * inertia;
  // The sprung body leans through the revealed momentum change; the exhaust does not.
  // Blending inertia into the wake made a right-angle move briefly thrust along a
  // direction the root was not actually travelling.
  const motionX = input.motionX;
  const motionY = input.motionY;
  const localForward = inertialMotionX * cos + inertialMotionY * sin;
  const localLateral = -inertialMotionX * sin + inertialMotionY * cos;
  const speed = Math.hypot(inertialMotionX, inertialMotionY);
  const currentLocalForward = motionX * cos + motionY * sin;
  const currentLocalLateral = -motionX * sin + motionY * cos;
  const currentSpeed = Math.hypot(motionX, motionY);
  const stepSpeed = Math.hypot(input.motionX, input.motionY);
  const normal = speed > 0 ? 1 / speed : 0;
  const forward = localForward * normal;
  const lateral = localLateral * normal;
  const movementGain = clamp01(speed);
  const startStop = clamp01(
    Math.abs(stepSpeed - input.previousSpeed),
  );

  let bank = 0;
  let pitch = 0;
  let counterSteer = 0;
  if (tuning.locomotion === 'low-hover') {
    bank = lateral * MAX_LEAN * movementGain;
    pitch = -forward * MAX_PITCH * movementGain;
  } else if (tuning.locomotion === 'treads' || tuning.locomotion === 'wheels') {
    // The floor contact remains planted. Only the sprung body nods, so tile occupancy is
    // never blurred by a chassis lifting or sliding outside its authoritative footprint.
    pitch = -MAX_PITCH * 0.72 * startStop * Math.sin(Math.PI * clamp01(input.fraction));
    pitch += -forward * MAX_PITCH * 0.28 * movementGain;
  } else {
    bank = lateral * MAX_LEAN * 0.45 * movementGain;
    pitch = -forward * MAX_PITCH * 0.42 * movementGain;
    counterSteer = -input.turnDelta * 0.12 * driftEnvelope(input.fraction);
  }

  const turnProgress = easeInOut(clamp01(input.fraction));
  const catchUp = smoothstep(0, tuning.hardwareLagTicks, input.fraction);
  let hardwareYaw = input.turnDelta * turnProgress * (1 - catchUp);
  if (tuning.hardwareOvershoot > 0 && input.fraction > tuning.hardwareLagTicks) {
    const overshootAge = clamp01(
      (input.fraction - tuning.hardwareLagTicks) /
        Math.max(0.12, 0.34 - tuning.hardwareLagTicks * 0.25),
    );
    hardwareYaw -=
      input.turnDelta *
      tuning.hardwareOvershoot *
      Math.sin(overshootAge * Math.PI) *
      (1 - overshootAge);
  }

  const wakeYaw =
    currentSpeed > 0
      ? -Math.atan2(currentLocalLateral, currentLocalForward)
      : 0;
  const signatureGain =
    input.signatureState === 'cooldown'
      ? 0.24
      : input.signatureState === 'active'
        ? 1.32
        : 1;
  const breath = 0.965 + 0.035 * Math.sin(input.time * 1.15 + 0.7);

  return {
    bank,
    pitch,
    counterSteer,
    hardwareYaw,
    wheelRotation: -input.signedTravel / WHEEL_RADIUS,
    wakeYaw,
    wakeStrength: clamp01(currentSpeed),
    hullLagForward:
      -(inertialMotionX * cos + inertialMotionY * sin) * HULL_LAG,
    hullLagLateral:
      -(-inertialMotionX * sin + inertialMotionY * cos) * HULL_LAG,
    idleGain: (input.braced ? 0.18 : 1) * (speed > 0 ? 0.25 : 1),
    emissiveGain: signatureGain * breath,
    ventStrength:
      input.signatureState === 'cooldown'
        ? 0.28 + 0.16 * (0.5 + 0.5 * Math.sin(input.time * 2.1))
        : 0,
  };
}

export interface ArcModelMotionRig {
  wake: THREE.Group;
  vents: THREE.Group;
  bind: (model: THREE.Object3D) => void;
  update: (
    input: ArcMotionInput,
    visibility: number,
  ) => ArcMotionFrame;
}

/** Bind the manifest-named mechanical nodes after the lazy GLB resolves. */
export function createArcModelMotionRig(
  spec: LookModelSpec | null,
  size: number,
  accent: THREE.Color,
  disposables: { dispose: () => void }[],
): ArcModelMotionRig | null {
  if (!spec?.motion) return null;

  const wake = new THREE.Group();
  wake.name = `${spec.id}-displacement-wake`;
  const vents = new THREE.Group();
  vents.name = `${spec.id}-cooldown-vents`;
  const wakeMaterial = new THREE.MeshBasicMaterial({
    color:
      spec.motion.locomotion === 'low-hover'
        ? accent.clone().lerp(new THREE.Color('#d9f8ff'), 0.42)
        : new THREE.Color('#927a61'),
    transparent: true,
    opacity: 0,
    depthWrite: false,
    blending:
      spec.motion.locomotion === 'low-hover'
        ? THREE.AdditiveBlending
        : THREE.NormalBlending,
  });
  const wakeGeometry =
    spec.motion.locomotion === 'low-hover'
      ? new THREE.ConeGeometry(size * 0.07, size * 0.34, 8, 1, true)
      : new THREE.SphereGeometry(size * 0.065, 8, 5);
  wakeGeometry.rotateZ(Math.PI / 2);
  for (let index = 0; index < 3; index += 1) {
    const mote = new THREE.Mesh(wakeGeometry, wakeMaterial);
    mote.position.set(-size * (0.28 + index * 0.13), size * (0.055 + index * 0.01), 0);
    mote.scale.setScalar(1 - index * 0.22);
    wake.add(mote);
  }
  const ventGeometry = new THREE.SphereGeometry(size * 0.035, 7, 5);
  const ventMaterial = new THREE.MeshBasicMaterial({
    color: new THREE.Color('#b9d7dd'),
    transparent: true,
    opacity: 0,
    depthWrite: false,
  });
  for (let index = 0; index < 3; index += 1) {
    const mote = new THREE.Mesh(ventGeometry, ventMaterial);
    mote.userData.ventIndex = index;
    vents.add(mote);
  }
  disposables.push(wakeGeometry, wakeMaterial, ventGeometry, ventMaterial);

  let hardware: THREE.Object3D | null = null;
  const wheels: THREE.Object3D[] = [];
  const idle: THREE.Object3D[] = [];
  const emissives: { material: THREE.MeshStandardMaterial; base: number }[] = [];

  const bind = (model: THREE.Object3D) => {
    const nodes = spec.nodes;
    if (nodes) {
      hardware = model.getObjectByName(nodes.hardware) ?? null;
      const locomotion = model.getObjectByName(nodes.locomotion);
      const wheelNodes: THREE.Object3D[] = [];
      locomotion?.traverse((node) => {
        if (node.name.startsWith('wheel-')) wheelNodes.push(node);
      });
      for (const node of wheelNodes) wheels.push(pivotAtGeometryCentre(node));
      for (const name of nodes.idle) {
        const node = model.getObjectByName(name);
        if (node)
          idle.push(name.includes('lantern-dish') ? pivotAtGeometryCentre(node) : node);
      }
    }
    model.traverse((node) => {
      const mesh = node as THREE.Mesh;
      if (!mesh.isMesh) return;
      const materials = Array.isArray(mesh.material) ? mesh.material : [mesh.material];
      for (const material of materials)
        if (
          material instanceof THREE.MeshStandardMaterial &&
          material.name === 'Arc Emissive' &&
          !emissives.some((entry) => entry.material === material)
        )
          emissives.push({ material, base: material.emissiveIntensity });
    });
  };

  const update = (input: ArcMotionInput, visibility: number) => {
    const frame = arcMotionFrame(spec, input);
    if (hardware) hardware.rotation.y = frame.hardwareYaw;
    for (const wheel of wheels) wheel.rotation.z = frame.wheelRotation;
    for (const [index, node] of idle.entries()) {
      if (node.name.includes('lantern-dish'))
        node.rotation.y = input.time * 0.18 * frame.idleGain;
      else {
        node.position.x =
          Math.sin(input.time * 0.75 + index * Math.PI) * 0.018 * frame.idleGain;
        node.position.y =
          Math.sin(input.time * 0.55 + index * Math.PI) * 0.008 * frame.idleGain;
      }
    }
    for (const entry of emissives)
      entry.material.emissiveIntensity = entry.base * frame.emissiveGain;

    wake.rotation.y = frame.wakeYaw;
    wakeMaterial.opacity = frame.wakeStrength * visibility * 0.34;
    wake.visible = wakeMaterial.opacity > 0.002;
    ventMaterial.opacity = frame.ventStrength * visibility;
    vents.visible = ventMaterial.opacity > 0.002;
    for (const [index, child] of vents.children.entries()) {
      const phase = (input.time * 0.42 + index / vents.children.length) % 1;
      child.position.set(
        size * (0.02 - index * 0.045),
        size * (0.31 + phase * 0.26),
        size * (index - 1) * 0.12,
      );
      child.scale.setScalar(0.55 + phase * 0.65);
    }
    return frame;
  };

  return { wake, vents, bind, update };
}

/** Reparent one baked mesh under a pivot without mutating its shared geometry. */
function pivotAtGeometryCentre(node: THREE.Object3D): THREE.Object3D {
  const parent = node.parent;
  if (!parent) return node;
  const mesh = node as THREE.Mesh;
  if (!mesh.isMesh || !mesh.geometry) return node;
  mesh.geometry.computeBoundingBox();
  const centre = mesh.geometry.boundingBox?.getCenter(new THREE.Vector3());
  if (!centre) return node;
  const pivot = new THREE.Group();
  pivot.name = `${node.name}-pivot`;
  parent.add(pivot);
  pivot.position.copy(centre);
  parent.remove(node);
  pivot.add(node);
  node.position.sub(centre);
  return pivot;
}

function clamp01(value: number): number {
  return Math.max(0, Math.min(value, 1));
}

function easeInOut(t: number): number {
  return t < 0.5 ? 2 * t * t : 1 - (-2 * t + 2) ** 2 / 2;
}

function smoothstep(from: number, to: number, value: number): number {
  if (to <= from) return value >= to ? 1 : 0;
  const t = clamp01((value - from) / (to - from));
  return t * t * (3 - 2 * t);
}

function driftEnvelope(fraction: number): number {
  return Math.sin(Math.PI * clamp01(fraction));
}
