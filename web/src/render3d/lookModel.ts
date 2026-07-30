import * as THREE from 'three';
import { GLTFLoader } from 'three/examples/jsm/loaders/GLTFLoader.js';
import { clone as cloneSkeleton } from 'three/examples/jsm/utils/SkeletonUtils.js';
import { chassisModel } from './chassisModel';
import {
  bindLookAnimation,
  type LookAnimationSpec,
  validateLookAnimationSpec,
} from './lookAnimation';

export type LookFormRole =
  | 'directional-stance'
  | 'omnidirectional-emplacement';

/**
 * The small, renderer-only contract beside an authored GLB.
 *
 * Sprite manifests remain the source of presentation identity. This companion only says
 * whether the WebGL renderer can replace that sprite's derived extrusion with a genuine
 * model, and which special-purpose form the model contains.
 */
export interface LookModelSpec {
  version: 1;
  id: string;
  file: 'model.glb';
  kind: 'bot' | 'projectile';
  /** Complete resolved look, or the one repeatable arm of a fallback turret. */
  part: 'whole' | 'turret-arm';
  facing: '+x';
  up: '+y';
  skillHardware?: 'volley' | 'aegis';
  /** Static fused form represented by this whole model. */
  formRole?: LookFormRole;
  /** Mobile Idle/Walk clips only; static forms use renderer-owned transitions. */
  animation?: LookAnimationSpec;
}

export interface ModelledLook {
  id: string;
  imageUrl: string;
}

/**
 * The semantic job this model is being asked to do.
 *
 * `part` says how much geometry the file contains; purpose says what the
 * renderer is resolving it as. Keeping both explicit prevents a directional
 * Shell from becoming a radial turret merely because both are whole models.
 */
export type LookModelPurpose =
  | { kind: 'mobile' }
  | { kind: 'projectile' }
  | {
      kind: 'directional-stance';
      skillHardware: 'volley' | 'aegis';
    }
  | { kind: 'omnidirectional-emplacement' }
  | { kind: 'sprite-turret-sector' };

export interface LookModelOptions {
  paint?: THREE.Color;
  sector?: 'front';
  teamAccent?: THREE.Color;
  purpose?: LookModelPurpose;
}

interface RegisteredModel {
  spec: LookModelSpec;
  url: string;
}

type AssetKind = LookModelSpec['kind'];
type LookModelSource = 'gltf' | 'fallback';

const MODEL_SOURCE_KEY = 'nilbotsModelSource';

interface LoadedModel {
  scene: THREE.Group;
  animations: readonly THREE.AnimationClip[];
}

/*
 * Keep these imports in render3d. The hosted viewer loads this tree only after WebGL is
 * chosen, while the CLI build replaces the render3d entry with a Canvas-only stub. Moving
 * the GLB URLs into the shared appearance registry would make every self-contained CLI
 * replay carry models it cannot render.
 */
const botModelSpecs = import.meta.glob<unknown>(
  '../assets/bot-looks/*/model3d.json',
  { eager: true, import: 'default' },
);
const botModelUrls = import.meta.glob<string>(
  '../assets/bot-looks/*/model.glb',
  { eager: true, import: 'default', query: '?url' },
);
const classModelSpecs = import.meta.glob<unknown>(
  '../assets/class-looks/*/model3d.json',
  { eager: true, import: 'default' },
);
const classModelUrls = import.meta.glob<string>(
  '../assets/class-looks/*/model.glb',
  { eager: true, import: 'default', query: '?url' },
);
const projectileModelSpecs = import.meta.glob<unknown>(
  '../assets/projectile-looks/*/model3d.json',
  { eager: true, import: 'default' },
);
const projectileModelUrls = import.meta.glob<string>(
  '../assets/projectile-looks/*/model.glb',
  { eager: true, import: 'default', query: '?url' },
);
const classProjectileModelSpecs = import.meta.glob<unknown>(
  '../assets/class-projectile-looks/*/model3d.json',
  { eager: true, import: 'default' },
);
const classProjectileModelUrls = import.meta.glob<string>(
  '../assets/class-projectile-looks/*/model.glb',
  { eager: true, import: 'default', query: '?url' },
);

const models = registerModels([
  [botModelSpecs, botModelUrls, 'bot'],
  [classModelSpecs, classModelUrls, 'bot'],
  [projectileModelSpecs, projectileModelUrls, 'projectile'],
  [classProjectileModelSpecs, classProjectileModelUrls, 'projectile'],
]);

const loader = new GLTFLoader();
const rawModels = new Map<string, Promise<LoadedModel | null>>();

/** Return renderer metadata synchronously without starting a model download. */
export function modelSpec(id: string): LookModelSpec | null {
  return models.get(id)?.spec ?? null;
}

/** Whether this representation came from an authored GLB rather than the SVG fallback. */
export function isGenuineLookModel(model: THREE.Object3D): boolean {
  return model.userData[MODEL_SOURCE_KEY] === 'gltf';
}

/**
 * Whether one authored model contract can truthfully fill a renderer role.
 *
 * A static whole form must name its semantic role explicitly, while mobile
 * locomotion clips stay mobile-only.
 * `sprite-turret-sector` intentionally rejects every GLB: that path exists for
 * legacy forms whose only honest emplacement fallback is four cropped SVG
 * noses.
 */
export function modelSupportsPurpose(
  spec: LookModelSpec,
  purpose: LookModelPurpose,
): boolean {
  if (purpose.kind === 'sprite-turret-sector') return false;
  if (purpose.kind === 'projectile')
    return (
      spec.kind === 'projectile' &&
      spec.part === 'whole' &&
      spec.formRole === undefined &&
      spec.skillHardware === undefined &&
      spec.animation === undefined
    );
  if (spec.kind !== 'bot') return false;

  const animation = spec.animation;
  if (purpose.kind === 'mobile')
    return (
      spec.part === 'whole' &&
      spec.skillHardware === undefined &&
      spec.formRole === undefined
    );

  if (purpose.kind === 'directional-stance')
    return (
      spec.part === 'whole' &&
      spec.skillHardware === purpose.skillHardware &&
      spec.formRole === 'directional-stance' &&
      animation === undefined
    );

  return (
    spec.skillHardware === undefined &&
    animation === undefined &&
    ((spec.part === 'turret-arm' && spec.formRole === undefined) ||
      (spec.part === 'whole' &&
        spec.formRole === 'omnidirectional-emplacement'))
  );
}

/**
 * Resolve one look to an independently paintable model.
 *
 * Geometry remains shared with the URL-level raw cache; scene nodes and materials do not.
 * Fog, hit flashes, selection and team paint all mutate materials per actor, so returning
 * a cached material would let one bot dim every other bot wearing the same chassis.
 *
 * A missing, unreadable or malformed GLB falls back to the existing sprite extrusion.
 * The SVG therefore remains the canonical asset for Canvas/mobile/site use and the safe
 * rendering floor for WebGL.
 */
export async function lookModel(
  look: ModelledLook,
  options: LookModelOptions = {},
): Promise<THREE.Group | null> {
  const { paint, sector, teamAccent } = options;
  const purpose =
    options.purpose ??
    (paint === undefined
      ? ({ kind: 'mobile' } as const)
      : ({ kind: 'projectile' } as const));
  const registered = models.get(look.id);
  if (!registered || !modelSupportsPurpose(registered.spec, purpose))
    return fallbackModel(look.imageUrl, paint, sector, teamAccent);

  const raw = await rawModel(registered.url);
  if (!raw)
    return fallbackModel(look.imageUrl, paint, sector, teamAccent);

  try {
    const model = instantiate(
      raw.scene,
      paint,
      teamAccent,
      raw.animations,
      registered.spec.animation,
    );
    markModelSource(model, 'gltf');
    return model;
  } catch {
    return fallbackModel(look.imageUrl, paint, sector, teamAccent);
  }
}

async function fallbackModel(
  url: string,
  paint?: THREE.Color,
  sector?: 'front',
  teamAccent?: THREE.Color,
): Promise<THREE.Group | null> {
  const fallback = await chassisModel(url, paint, sector, teamAccent);
  if (!fallback) return null;
  try {
    // chassisModel owns its URL-level parse cache just like rawModel. Clone here too, so
    // callers can treat both paths identically and never reparent or repaint cached state.
    const model = instantiate(fallback, paint, teamAccent);
    markModelSource(model, 'fallback');
    return model;
  } catch {
    return null;
  }
}

function markModelSource(
  model: THREE.Object3D,
  source: LookModelSource,
): void {
  model.userData[MODEL_SOURCE_KEY] = source;
}

function rawModel(url: string): Promise<LoadedModel | null> {
  const existing = rawModels.get(url);
  if (existing) return existing;

  const loaded = loader
    .loadAsync(url)
    .then(({ scene, animations }) => {
      let hasMesh = false;
      scene.traverse((node) => {
        if ((node as THREE.Mesh).isMesh) hasMesh = true;
      });
      return hasMesh ? { scene, animations } : null;
    })
    .catch(() => null);
  rawModels.set(url, loaded);
  return loaded;
}

function instantiate(
  source: THREE.Group,
  paint?: THREE.Color,
  teamAccent?: THREE.Color,
  animations: readonly THREE.AnimationClip[] = [],
  animationSpec?: LookAnimationSpec,
): THREE.Group {
  // SkeletonUtils repairs the bone bindings that Object3D.clone(true) cannot
  // preserve. Static looks follow the same path so adding a rig does not create
  // a second representation cache. Geometry remains shared; actor-local
  // materials and animation state do not.
  const instance = cloneSkeleton(source) as THREE.Group;
  const materials = new Map<THREE.Material, THREE.Material>();

  instance.traverse((node) => {
    const mesh = node as THREE.Mesh;
    if (!mesh.isMesh) return;
    mesh.castShadow = true;
    mesh.receiveShadow = true;
    mesh.material = Array.isArray(mesh.material)
      ? mesh.material.map((material) =>
          instanceMaterial(material, materials, paint, teamAccent),
        )
      : instanceMaterial(mesh.material, materials, paint, teamAccent);
  });

  if (
    animationSpec &&
    !bindLookAnimation(instance, animations, animationSpec)
  ) {
    for (const material of materials.values()) material.dispose();
    throw new Error('Authored look model is missing its declared animation clips.');
  }

  return instance;
}

function instanceMaterial(
  source: THREE.Material,
  materials: Map<THREE.Material, THREE.Material>,
  paint?: THREE.Color,
  teamAccent?: THREE.Color,
): THREE.Material {
  const existing = materials.get(source);
  if (existing) return existing;

  const material = actorLookMaterial(source, paint, teamAccent);
  materials.set(source, material);
  return material;
}

/**
 * Clone one authored material for an actor without contaminating its class art.
 *
 * `paint` is the explicit projectile-energy path and recolours every standard
 * surface. Bot owner colour is narrower: only a glTF material carrying the
 * semantic `nilbotsRole: "team-accent"` extra may change. Fixed cyan trims,
 * authored PBR paint, and every untagged surface stay exactly as authored.
 */
export function actorLookMaterial(
  source: THREE.Material,
  paint?: THREE.Color,
  teamAccent?: THREE.Color,
): THREE.Material {
  const material = source.clone();
  if (!(material instanceof THREE.MeshStandardMaterial)) return material;

  if (paint) {
    // Passing paint means "projectile", matching chassisModel and Canvas2D's source-in
    // treatment: every authored surface becomes the owner's energy colour.
    material.color.copy(paint);
    material.emissive.copy(paint);
    material.emissiveIntensity = 1.8;
    material.roughness = 0.35;
    material.metalness = 0.1;
  } else if (
    teamAccent &&
    source.userData.nilbotsRole === 'team-accent'
  ) {
    material.color.copy(teamAccent);
    material.emissive.copy(teamAccent);
    material.emissiveIntensity = Math.max(material.emissiveIntensity, 1.2);
  }

  return material;
}

function registerModels(
  collections: readonly [
    Record<string, unknown>,
    Record<string, string>,
    AssetKind,
  ][],
): Map<string, RegisteredModel> {
  const result = new Map<string, RegisteredModel>();
  for (const [specs, urls, expectedKind] of collections) {
    for (const [path, input] of Object.entries(specs)) {
      const directory = path.slice(0, path.lastIndexOf('/'));
      const directoryId = directory.slice(directory.lastIndexOf('/') + 1);
      const spec = validateSpec(input, path, directoryId, expectedKind);
      const url = urls[`${directory}/${spec.file}`];
      if (!url)
        throw new Error(
          `3D look '${spec.id}' references missing '${spec.file}'.`,
        );
      if (result.has(spec.id))
        throw new Error(`Duplicate 3D look ID '${spec.id}'.`);
      result.set(spec.id, { spec, url });
    }
  }
  return result;
}

function validateSpec(
  input: unknown,
  path: string,
  directoryId: string,
  expectedKind: AssetKind,
): LookModelSpec {
  if (!isRecord(input))
    throw new Error(`3D look manifest '${path}' must be an object.`);

  const version = input.version;
  const id = input.id;
  const file = input.file;
  const kind = input.kind;
  const part = input.part;
  const facing = input.facing;
  const up = input.up;
  const skillHardware = input.skillHardware;
  const formRole = input.formRole;
  const animation = validateLookAnimationSpec(
    input.animation,
    `3D look '${String(id)}'`,
  );

  if (version !== 1)
    throw new Error(`3D look manifest '${path}' has unsupported version.`);
  if (
    typeof id !== 'string' ||
    !/^[a-z0-9]+(?:-[a-z0-9]+)*$/.test(id) ||
    id !== directoryId
  )
    throw new Error(`3D look manifest '${path}' has an invalid ID.`);
  if (file !== 'model.glb')
    throw new Error(`3D look '${id}' must reference 'model.glb'.`);
  if (kind !== expectedKind)
    throw new Error(
      `3D look '${id}' must declare kind '${expectedKind}'.`,
    );
  if (part !== 'whole' && part !== 'turret-arm')
    throw new Error(`3D look '${id}' has unknown part '${String(part)}'.`);
  if (facing !== '+x')
    throw new Error(`3D look '${id}' must face '+x'.`);
  if (up !== '+y')
    throw new Error(`3D look '${id}' must use '+y' as up.`);
  if (
    skillHardware !== undefined &&
    skillHardware !== 'volley' &&
    skillHardware !== 'aegis'
  )
    throw new Error(
      `3D look '${id}' has unknown skill hardware '${String(skillHardware)}'.`,
    );
  if (kind === 'projectile' && part !== 'whole')
    throw new Error(`3D projectile '${id}' must be a whole model.`);
  if (kind === 'projectile' && skillHardware !== undefined)
    throw new Error(`3D projectile '${id}' cannot declare skill hardware.`);
  if (part === 'turret-arm' && skillHardware !== undefined)
    throw new Error(`3D turret arm '${id}' cannot declare skill hardware.`);
  if (
    formRole !== undefined &&
    formRole !== 'directional-stance' &&
    formRole !== 'omnidirectional-emplacement'
  )
    throw new Error(
      `3D look '${id}' has unknown form role '${String(formRole)}'.`,
    );
  if (formRole !== undefined && (kind !== 'bot' || part !== 'whole'))
    throw new Error(
      `Only a whole 3D bot '${id}' can declare a form role.`,
    );
  if (animation !== undefined && (kind !== 'bot' || part !== 'whole'))
    throw new Error(
      `Only a whole 3D bot '${id}' can declare animation.`,
    );
  if (animation !== undefined && formRole !== undefined)
    throw new Error(
      `Static form '${id}' cannot declare locomotion animation.`,
    );
  if (animation !== undefined && skillHardware !== undefined)
    throw new Error(
      `Locomotion animation on '${id}' cannot supply stance hardware.`,
    );
  if (
    formRole === 'directional-stance' &&
    skillHardware === undefined
  )
    throw new Error(
      `Directional form '${id}' requires skill hardware.`,
    );
  if (
    formRole === 'omnidirectional-emplacement' &&
    skillHardware !== undefined
  )
    throw new Error(
      `Omnidirectional form '${id}' cannot declare directional skill hardware.`,
    );
  if (skillHardware !== undefined && formRole !== 'directional-stance')
    throw new Error(
      `3D stance hardware on '${id}' requires a directional form role.`,
    );

  return {
    version,
    id,
    file,
    kind: expectedKind,
    part,
    facing,
    up,
    ...(skillHardware === undefined ? {} : { skillHardware }),
    ...(formRole === undefined
      ? {}
      : { formRole: formRole as LookFormRole }),
    ...(animation === undefined ? {} : { animation }),
  };
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null && !Array.isArray(value);
}
