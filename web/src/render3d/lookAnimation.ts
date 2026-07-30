import * as THREE from 'three';

export type LookMotion = 'idle' | 'moving';

/**
 * Named locomotion clips supplied by one authored mobile-bot GLB.
 *
 * Playback rate is deliberately not asset metadata. The arena samples a moving
 * cycle once per authoritative replay tick and an idle cycle every two ticks,
 * so scrubbing to the same replay time always produces the same pose.
 */
export interface LookAnimationSpec {
  idleClip: string;
  movingClip: string;
}

interface LookAnimationController {
  mixer: THREE.AnimationMixer;
  actions: Record<LookMotion, THREE.AnimationAction>;
  clips: Record<LookMotion, THREE.AnimationClip>;
}

const controllers = new WeakMap<THREE.Object3D, LookAnimationController>();
const CLIP_NAME = /^[A-Za-z0-9]+(?:[A-Za-z0-9_.-]*[A-Za-z0-9])?$/;

export function validateLookAnimationSpec(
  input: unknown,
  label: string,
): LookAnimationSpec | undefined {
  if (input === undefined) return undefined;
  if (
    typeof input !== 'object' ||
    input === null ||
    Array.isArray(input)
  )
    throw new Error(`${label} animation must be an object.`);

  const record = input as Record<string, unknown>;
  const idleClip = record.idleClip;
  const movingClip = record.movingClip;
  if (
    typeof idleClip !== 'string' ||
    !CLIP_NAME.test(idleClip) ||
    typeof movingClip !== 'string' ||
    !CLIP_NAME.test(movingClip)
  )
    throw new Error(`${label} animation has invalid clip names.`);
  if (idleClip === movingClip)
    throw new Error(`${label} animation clips must be distinct.`);
  if (Object.keys(record).some(
    (key) => key !== 'idleClip' && key !== 'movingClip',
  ))
    throw new Error(`${label} animation has unknown fields.`);

  return { idleClip, movingClip };
}

/**
 * Bind one independently cloned scene to its authored idle and movement clips.
 *
 * A missing clip is a hard representation failure: callers retain the SVG
 * fallback rather than show a walking chassis sliding in its bind pose.
 */
export function bindLookAnimation(
  root: THREE.Object3D,
  clips: readonly THREE.AnimationClip[],
  spec: LookAnimationSpec,
): boolean {
  releaseLookAnimation(root);
  const idle = clips.find((clip) => clip.name === spec.idleClip);
  const moving = clips.find((clip) => clip.name === spec.movingClip);
  if (!idle || !moving || idle.duration <= 0 || moving.duration <= 0)
    return false;

  const mixer = new THREE.AnimationMixer(root);
  const actions = {
    idle: mixer.clipAction(idle),
    moving: mixer.clipAction(moving),
  };
  for (const action of Object.values(actions)) {
    action.enabled = true;
    action.setLoop(THREE.LoopRepeat, Number.POSITIVE_INFINITY);
    action.play();
    // Arena time is sampled explicitly below. The mixer must never accumulate
    // requestAnimationFrame deltas, or pause/scrub would become history-dependent.
    action.paused = true;
    action.setEffectiveWeight(0);
  }
  controllers.set(root, {
    mixer,
    actions,
    clips: { idle, moving },
  });
  seekLookAnimation(root, 'idle', 0);
  return true;
}

/**
 * Sample a clip at an absolute normalized phase.
 *
 * The result depends only on `(motion, phase)`, not on which frames were drawn
 * before it. This is the renderer half of deterministic replay animation.
 */
export function seekLookAnimation(
  root: THREE.Object3D,
  motion: LookMotion,
  phase: number,
): boolean {
  const controller = controllers.get(root);
  if (!controller || !Number.isFinite(phase)) return false;

  const wrapped = phase - Math.floor(phase);
  for (const [name, action] of Object.entries(controller.actions) as [
    LookMotion,
    THREE.AnimationAction,
  ][])
    action.setEffectiveWeight(name === motion ? 1 : 0);
  controller.actions[motion].time =
    wrapped * controller.clips[motion].duration;
  controller.mixer.update(0);
  return true;
}

export function releaseLookAnimation(root: THREE.Object3D): void {
  const controller = controllers.get(root);
  if (!controller) return;
  controller.mixer.stopAllAction();
  controller.mixer.uncacheRoot(root);
  controllers.delete(root);
}
