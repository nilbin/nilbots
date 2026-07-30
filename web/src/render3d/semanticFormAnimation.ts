import * as THREE from 'three';

export const STANCE_DEPLOY_CLIP = 'Shell_Deploy';
export const EMPLACED_DEPLOY_CLIP = 'Tower_Deploy';

export type SemanticFormTransition = 'stance' | 'emplaced';
export type SemanticRecoil = 'mobile' | 'stance' | 'emplaced';

/**
 * The authored clips that make one mobile chassis a mechanically continuous
 * Shell/Tower family.
 *
 * Deploy clip names are fixed so form semantics do not depend on a provider or
 * an asset-specific alias. Optional clips remain explicit because not every
 * family needs a rest loop or a weapon in every form.
 */
export interface SemanticFormClipContract {
  version: 1;
  stanceDeployClip: typeof STANCE_DEPLOY_CLIP;
  emplacedDeployClip: typeof EMPLACED_DEPLOY_CLIP;
  mobileRestClip?: string;
  mobileRecoilClip?: string;
  stanceRecoilClip?: string;
  emplacedRecoilClip?: string;
}

export interface SemanticFormAnimationCapabilities {
  mobileRest: boolean;
  mobileRecoil: boolean;
  stanceRecoil: boolean;
  emplacedRecoil: boolean;
}

export interface SemanticRecoilSample {
  form: SemanticRecoil;
  /**
   * Absolute normalized progress through the recoil. Values outside 0..1 are
   * clamped, which preserves exact authored endpoints.
   */
  progress: number;
}

export interface SemanticFormAnimationSample {
  /**
   * Selects the one authoritative mechanical path being sampled.
   *
   * `progress = 0` is the shared mobile endpoint and `progress = 1` is the
   * selected stance/emplaced endpoint. Passing decreasing progress samples the
   * exact same clip in reverse; no reverse-only animation or frame history is
   * involved. The renderer maps replay form IDs to these presentation roles.
   */
  transition: SemanticFormTransition;
  progress: number;
  /**
   * Absolute normalized phase through the optional mobile rest loop.
   *
   * The rest loop contributes only at the exact mobile endpoint. Shell and
   * Tower are planted forms and must not inherit a hover/bob overlay.
   */
  mobileRestPhase?: number;
  recoil?: SemanticRecoilSample;
}

export interface SemanticFormAnimationSampler {
  readonly capabilities: SemanticFormAnimationCapabilities;
  /**
   * Pose the rigid hierarchy as a pure function of this sample.
   *
   * The function never consumes a render-frame delta. It returns false after
   * disposal or when a requested optional clip is unavailable.
   */
  sample(sample: SemanticFormAnimationSample): boolean;
  /** Release this instance's mixer and bindings. Safe to call more than once. */
  dispose(): void;
}

const CLIP_NAME = /^[A-Za-z0-9]+(?:[A-Za-z0-9_.-]*[A-Za-z0-9])?$/;
const CONTRACT_FIELDS = new Set([
  'version',
  'stanceDeployClip',
  'emplacedDeployClip',
  'mobileRestClip',
  'mobileRecoilClip',
  'stanceRecoilClip',
  'emplacedRecoilClip',
]);
const OPTIONAL_CLIP_FIELDS = [
  'mobileRestClip',
  'mobileRecoilClip',
  'stanceRecoilClip',
  'emplacedRecoilClip',
] as const;

type ActionRole =
  | 'stanceDeploy'
  | 'emplacedDeploy'
  | 'mobileRest'
  | 'mobileRecoil'
  | 'stanceRecoil'
  | 'emplacedRecoil';

interface BoundAction {
  action: THREE.AnimationAction;
  clip: THREE.AnimationClip;
}

/**
 * Validate manifest metadata before a GLB is allowed to replace the generic
 * representation. An absent contract is valid and leaves the fallback path in
 * charge; a present but malformed contract is an asset error.
 */
export function validateSemanticFormClipContract(
  input: unknown,
  label: string,
): SemanticFormClipContract | undefined {
  if (input === undefined) return undefined;
  if (
    typeof input !== 'object' ||
    input === null ||
    Array.isArray(input)
  )
    throw new Error(`${label} semantic animation must be an object.`);

  const record = input as Record<string, unknown>;
  if (Object.keys(record).some((field) => !CONTRACT_FIELDS.has(field)))
    throw new Error(`${label} semantic animation has unknown fields.`);
  if (record.version !== 1)
    throw new Error(`${label} semantic animation version must be 1.`);
  if (record.stanceDeployClip !== STANCE_DEPLOY_CLIP)
    throw new Error(
      `${label} semantic animation must use ${STANCE_DEPLOY_CLIP}.`,
    );
  if (record.emplacedDeployClip !== EMPLACED_DEPLOY_CLIP)
    throw new Error(
      `${label} semantic animation must use ${EMPLACED_DEPLOY_CLIP}.`,
    );

  const result: SemanticFormClipContract = {
    version: 1,
    stanceDeployClip: STANCE_DEPLOY_CLIP,
    emplacedDeployClip: EMPLACED_DEPLOY_CLIP,
  };
  const names = new Set<string>([
    STANCE_DEPLOY_CLIP,
    EMPLACED_DEPLOY_CLIP,
  ]);
  for (const field of OPTIONAL_CLIP_FIELDS) {
    const name = record[field];
    if (name === undefined) continue;
    if (typeof name !== 'string' || !CLIP_NAME.test(name))
      throw new Error(
        `${label} semantic animation has an invalid ${field}.`,
      );
    if (names.has(name))
      throw new Error(
        `${label} semantic animation clip names must be distinct.`,
      );
    names.add(name);
    result[field] = name;
  }
  return result;
}

/**
 * Bind one independently cloned rigid hierarchy to its semantic clips.
 *
 * Every successful call owns a new AnimationMixer. Geometry and AnimationClip
 * data may be shared between actors; evaluated transforms and mixer state may
 * not. A declared clip that is missing, duplicated, empty, or non-finite makes
 * the bind fail closed so the existing generic form renderer can remain in
 * charge.
 */
export function createSemanticFormAnimationSampler(
  root: THREE.Object3D,
  clips: readonly THREE.AnimationClip[],
  contract: SemanticFormClipContract,
): SemanticFormAnimationSampler | null {
  const requested = requestedClips(contract);
  const clipsByName = new Map<string, THREE.AnimationClip[]>();
  for (const clip of clips) {
    const matches = clipsByName.get(clip.name);
    if (matches) matches.push(clip);
    else clipsByName.set(clip.name, [clip]);
  }

  const resolved = new Map<ActionRole, THREE.AnimationClip>();
  for (const [role, name] of requested) {
    const matches = clipsByName.get(name);
    if (
      matches?.length !== 1 ||
      !usableClip(matches[0]) ||
      !clipTargetsHierarchy(root, matches[0])
    )
      return null;
    resolved.set(role, matches[0]);
  }

  const mixer = new THREE.AnimationMixer(root);
  const actions = new Map<ActionRole, BoundAction>();
  for (const [role, sourceClip] of resolved) {
    const overlay = isOverlay(role);
    const clip = overlay
      ? THREE.AnimationUtils.makeClipAdditive(sourceClip.clone())
      : sourceClip;
    const action = mixer.clipAction(clip);
    action.enabled = true;
    action.paused = true;
    action.clampWhenFinished = true;
    action.setLoop(THREE.LoopOnce, 1);
    action.setEffectiveWeight(0);
    action.play();
    actions.set(role, { action, clip });
  }

  let disposed = false;
  const capabilities = Object.freeze({
    mobileRest: actions.has('mobileRest'),
    mobileRecoil: actions.has('mobileRecoil'),
    stanceRecoil: actions.has('stanceRecoil'),
    emplacedRecoil: actions.has('emplacedRecoil'),
  });

  const sampler: SemanticFormAnimationSampler = {
    capabilities,
    sample(sample): boolean {
      if (disposed || !validSample(sample, capabilities)) return false;
      const deployProgress = clampUnit(sample.progress);

      for (const { action } of actions.values()) {
        action.enabled = true;
        action.paused = true;
        action.setEffectiveWeight(0);
      }

      // Each deploy clip is self-contained, including shared pod planting and
      // settle. Select exactly one normal-blend action; never average the two
      // mechanical paths together.
      const deployRole =
        sample.transition === 'stance'
          ? 'stanceDeploy'
          : 'emplacedDeploy';
      seekAction(
        actions.get(deployRole)!,
        deployProgress,
        1,
      );

      if (
        deployProgress === 0 &&
        sample.mobileRestPhase !== undefined
      )
        seekAction(
          actions.get('mobileRest')!,
          wrapUnit(sample.mobileRestPhase),
          1,
        );

      if (sample.recoil) {
        const recoilRole = `${sample.recoil.form}Recoil` as ActionRole;
        seekAction(
          actions.get(recoilRole)!,
          clampUnit(sample.recoil.progress),
          1,
        );
      }

      // Zero is intentional. The mixer only evaluates the absolute action
      // times assigned above; it never accumulates requestAnimationFrame time.
      mixer.update(0);
      return true;
    },
    dispose(): void {
      if (disposed) return;
      disposed = true;
      mixer.stopAllAction();
      mixer.uncacheRoot(root);
      actions.clear();
    },
  };

  if (!sampler.sample({ transition: 'stance', progress: 0 })) {
    sampler.dispose();
    return null;
  }
  return sampler;
}

function requestedClips(
  contract: SemanticFormClipContract,
): Map<ActionRole, string> {
  const requested = new Map<ActionRole, string>([
    ['stanceDeploy', contract.stanceDeployClip],
    ['emplacedDeploy', contract.emplacedDeployClip],
  ]);
  if (contract.mobileRestClip)
    requested.set('mobileRest', contract.mobileRestClip);
  if (contract.mobileRecoilClip)
    requested.set('mobileRecoil', contract.mobileRecoilClip);
  if (contract.stanceRecoilClip)
    requested.set('stanceRecoil', contract.stanceRecoilClip);
  if (contract.emplacedRecoilClip)
    requested.set('emplacedRecoil', contract.emplacedRecoilClip);
  return requested;
}

function usableClip(clip: THREE.AnimationClip): boolean {
  if (
    !Number.isFinite(clip.duration) ||
    clip.duration <= 0 ||
    clip.tracks.length === 0
  )
    return false;
  return clip.tracks.every(
    (track) => {
      const times = Array.from(track.times);
      const valueSize = track.getValueSize();
      return (
        times.length > 0 &&
        Number.isInteger(valueSize) &&
        valueSize > 0 &&
        track.values.length === times.length * valueSize &&
        times.every(
          (time, index) =>
            Number.isFinite(time) &&
            (index === 0 || time >= times[index - 1]!),
        ) &&
        Array.from(track.values).every(Number.isFinite)
      );
    },
  );
}

/**
 * Prove every rigid track resolves on this clone before an AnimationMixer is created.
 *
 * Three logs a missing PropertyBinding and continues, which would otherwise make a clip
 * look successfully bound while silently leaving one hatch, shield or tower crown in the
 * wrong form. Semantic form clips intentionally animate only object transforms: provider
 * morph targets and material channels are texture-donor output, not mechanical authority.
 */
function clipTargetsHierarchy(
  root: THREE.Object3D,
  clip: THREE.AnimationClip,
): boolean {
  return clip.tracks.every((track) => {
    try {
      const parsed = THREE.PropertyBinding.parseTrackName(track.name);
      if (
        parsed.objectName ||
        parsed.objectIndex ||
        parsed.propertyIndex ||
        (parsed.propertyName !== 'position' &&
          parsed.propertyName !== 'quaternion' &&
          parsed.propertyName !== 'scale')
      )
        return false;
      return (
        THREE.PropertyBinding.findNode(root, parsed.nodeName) !== null
      );
    } catch {
      return false;
    }
  });
}

function validSample(
  sample: SemanticFormAnimationSample,
  capabilities: SemanticFormAnimationCapabilities,
): boolean {
  if (
    (sample.transition !== 'stance' &&
      sample.transition !== 'emplaced') ||
    !Number.isFinite(sample.progress) ||
    (sample.mobileRestPhase !== undefined &&
      (!capabilities.mobileRest ||
        !Number.isFinite(sample.mobileRestPhase)))
  )
    return false;

  if (!sample.recoil) return true;
  if (
    !Number.isFinite(sample.recoil.progress) ||
    !(
      sample.recoil.form === 'mobile' ||
      sample.recoil.form === 'stance' ||
      sample.recoil.form === 'emplaced'
    )
  )
    return false;
  return capabilities[`${sample.recoil.form}Recoil`];
}

function isOverlay(role: ActionRole): boolean {
  return role !== 'stanceDeploy' && role !== 'emplacedDeploy';
}

function seekAction(
  bound: BoundAction,
  phase: number,
  weight: number,
): void {
  bound.action.enabled = true;
  bound.action.paused = true;
  bound.action.time = phase * bound.clip.duration;
  bound.action.setEffectiveWeight(weight);
}

function clampUnit(value: number): number {
  return Math.min(1, Math.max(0, value));
}

function wrapUnit(value: number): number {
  return value - Math.floor(value);
}
