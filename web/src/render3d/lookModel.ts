import * as THREE from 'three';
import { GLTFLoader } from 'three/examples/jsm/loaders/GLTFLoader.js';
import { KTX2Loader } from 'three/examples/jsm/loaders/KTX2Loader.js';
import { beginAsset } from '../render/assetReadiness';
import { chassisModel } from './chassisModel';

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
  part: 'whole' | 'turret-arm';
  facing: '+x';
  up: '+y';
  skillHardware?: 'volley' | 'aegis';
  /** Optional authored node contract. The renderer never guesses part names. */
  nodes?: {
    locomotion: string;
    chassis: string;
    hardware: string;
    teamAccents: string;
    emissives: string;
    idle: string[];
  };
  /** Presentation-only movement tuning carried beside the model. */
  motion?: {
    locomotion: 'low-hover' | 'treads' | 'wheels' | 'skids';
    handling: 'swift' | 'standard' | 'deliberate';
    hardwareLagTicks: number;
    hardwareOvershoot: number;
  };
  /** Arc Relay signature whose body hardware this companion depicts. */
  signature?: string;
  source?: {
    generator: string;
    recipe: string;
    sourceSha256: string;
    /** Deterministic/vector sources use a layered source; provider assets name the approved artifact. */
    layeredSource?: string;
    artifact?: string;
    provider?: string;
    model?: string;
    endpoint?: string;
    modelType?: string;
    taskId?: string;
    orientation?: 'identity' | 'lay-flat-x';
    facingYawDegrees?: number;
    /** Provider artifact whose appearance/orientation was approved before runtime encoding. */
    approvedArtifact?: string;
    approvedSha256?: string;
    textureTier?: string;
  };
  ledger?: {
    bytes: number;
    sha256: string;
    triangles: number;
    materials: number;
    textureCount: number;
    geometrySha256?: string;
    geometryGpuBytes?: number;
    textureGpuBytesCompressedTarget?: number;
    textureGpuBytesRgba8Fallback?: number;
    modelGpuBytesCompressedTarget?: number;
    modelGpuBytesRgba8Fallback?: number;
    textureTier?: string;
  };
}

export interface ModelledLook {
  id: string;
  imageUrl: string;
}

interface RegisteredModel {
  spec: LookModelSpec;
  url: string;
}

type AssetKind = LookModelSpec['kind'];
type LookModelSource = 'gltf' | 'fallback';

const MODEL_SOURCE_KEY = 'nilbotsModelSource';

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

/**
 * One manager across every GLB, so every request a model makes is visible in one place.
 *
 * A GLB is not one request: glTF pulls its own buffers and textures, and the loader routes
 * those through the manager it was given. The manager is what makes those sub-requests
 * countable at all.
 *
 * **It is not, however, what holds the gate.** `LoadingManager` fires `onLoad` the moment
 * `itemsLoaded === itemsTotal`, which is true at every lull — including the gap between
 * the `.glb` arriving and its textures being requested. Hanging the hold off `onStart` and
 * `onLoad` therefore released it mid-load and reported the arena ready with the striker
 * still missing, which is measurably what happened: the play button lit five seconds into
 * an eight-second model fetch. The authoritative hold is per load, below, and spans the
 * whole of `loadAsync` — which resolves only once glTF has parsed every dependency.
 */
const loadingManager = new THREE.LoadingManager();

const loader = new GLTFLoader(loadingManager);
const ktx2Loader = new KTX2Loader(loadingManager);
loader.setKTX2Loader(ktx2Loader);
const rawModels = new Map<string, Promise<THREE.Group | null>>();

/**
 * Select the device-native target for Basis/KTX2 textures before model loading begins.
 *
 * The transcoder remains inside the lazy WebGL tree, and Vite resolves Three's pinned
 * worker assets from the loader module. Canvas2D and the self-contained CLI viewer do not
 * carry it. Existing WebP GLBs are unaffected because they never use this loader.
 */
export function configureModelTextureSupport(renderer: THREE.WebGLRenderer): void {
  ktx2Loader.detectSupport(renderer);
}

/** Return renderer metadata synchronously without starting a model download. */
export function modelSpec(id: string): LookModelSpec | null {
  return models.get(id)?.spec ?? null;
}

/**
 * Which source `lookModel` will draw this request from, decided without fetching anything.
 *
 * Exists so the sector rule can be asserted rather than eyeballed: both paths return a
 * `THREE.Group`, and the difference between "the striker's nose" and "four whole strikers"
 * is invisible to a caller and — as it turned out — to a reviewer as well.
 */
export function lookModelSource(
  id: string,
  sector?: 'front',
): LookModelSource {
  const registered = models.get(id);
  if (!registered) return 'fallback';
  // Only a model authored *as* the arm already is the sector. Anything else would have to
  // be cropped, and a triangulated mesh cannot be — the layered SVG can.
  if (sector !== undefined && registered.spec.part !== 'turret-arm')
    return 'fallback';
  return 'gltf';
}

/** Whether this representation came from an authored GLB rather than the SVG fallback. */
export function isGenuineLookModel(model: THREE.Object3D): boolean {
  return model.userData[MODEL_SOURCE_KEY] === 'gltf';
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
 *
 * **A `sector` a model cannot serve is also a fallback.** `sector: 'front'` asks for the
 * chassis' forward section — the piece the turret builder repeats around an axis to make
 * an emplacement. A layered SVG can be cropped to it; a triangulated GLB authored as
 * `part: 'whole'` cannot. Ignoring the argument and handing back the whole body was
 * silently wrong in a way that only showed up on the one look with a model: the striker's
 * turret came out as four entire strikers tipped on their noses and splayed around the
 * unit — a boxy cage of hardware where a compact emplacement belonged, at four times the
 * triangles. Only a model that declares itself a `turret-arm` is already the sector.
 */
export async function lookModel(
  look: ModelledLook,
  paint?: THREE.Color,
  sector?: 'front',
  teamAccent?: THREE.Color,
): Promise<THREE.Group | null> {
  const registered = models.get(look.id);
  if (!registered || lookModelSource(look.id, sector) === 'fallback')
    return fallbackModel(look.imageUrl, paint, sector, teamAccent);

  const raw = await rawModel(registered.url);
  if (!raw)
    return fallbackModel(look.imageUrl, paint, sector, teamAccent);

  try {
    const model = instantiate(raw, paint, teamAccent);
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

function rawModel(url: string): Promise<THREE.Group | null> {
  const existing = rawModels.get(url);
  if (existing) return existing;

  // Held for the whole load, released however it ends. A model that 404s falls back to the
  // sprite extrusion, and a viewer must not sit behind a loading screen waiting for a file
  // that is never coming — degraded is not the same as hung.
  const release = beginAsset();
  const loaded = loader
    .loadAsync(url)
    .then(({ scene }) => {
      let hasMesh = false;
      scene.traverse((node) => {
        if ((node as THREE.Mesh).isMesh) hasMesh = true;
      });
      return hasMesh ? scene : null;
    })
    .catch(() => null)
    .finally(release);
  rawModels.set(url, loaded);
  return loaded;
}

function instantiate(
  source: THREE.Group,
  paint?: THREE.Color,
  teamAccent?: THREE.Color,
): THREE.Group {
  // Object3D.clone(true) recursively clones transforms and nodes while leaving BufferGeometry
  // shared. That is the intended split: vertices are immutable and expensive, materials
  // carry actor-local state.
  const instance = source.clone(true);
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

  const material = source.clone();
  materials.set(source, material);
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
  const nodes = input.nodes;
  const motion = input.motion;
  const signature = input.signature;
  const source = input.source;
  const ledger = input.ledger;

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

  const optionalNodes = validateNodes(nodes, id);
  const optionalMotion = validateMotion(motion, id);
  if (signature !== undefined && (
    typeof signature !== 'string' ||
    !/^[a-z0-9]+(?:-[a-z0-9]+)*$/.test(signature)
  ))
    throw new Error(`3D look '${id}' has an invalid signature.`);
  const optionalSource = validateSource(source, id);
  const optionalLedger = validateLedger(ledger, id);
  // Motion is also valid for a monolithic provider mesh. Root lean, pitch,
  // follow-through, wake and cooldown vents do not need mounted part names;
  // only per-hardware animation does.
  if (optionalNodes !== undefined && optionalMotion === undefined)
    throw new Error(`3D look '${id}' authored nodes need motion tuning.`);
  if (signature !== undefined && optionalMotion === undefined)
    throw new Error(`3D look '${id}' signature needs motion tuning.`);

  return {
    version,
    id,
    file,
    kind: expectedKind,
    part,
    facing,
    up,
    ...(skillHardware === undefined ? {} : { skillHardware }),
    ...(optionalNodes === undefined ? {} : { nodes: optionalNodes }),
    ...(optionalMotion === undefined ? {} : { motion: optionalMotion }),
    ...(signature === undefined ? {} : { signature }),
    ...(optionalSource === undefined ? {} : { source: optionalSource }),
    ...(optionalLedger === undefined ? {} : { ledger: optionalLedger }),
  };
}

function validateNodes(
  input: unknown,
  id: string,
): LookModelSpec['nodes'] | undefined {
  if (input === undefined) return undefined;
  if (!isRecord(input))
    throw new Error(`3D look '${id}' nodes must be an object.`);
  const values = [
    input.locomotion,
    input.chassis,
    input.hardware,
    input.teamAccents,
    input.emissives,
  ];
  if (values.some((value) => typeof value !== 'string' || value.length === 0))
    throw new Error(`3D look '${id}' has invalid authored node names.`);
  if (!Array.isArray(input.idle) || input.idle.some((value) => typeof value !== 'string'))
    throw new Error(`3D look '${id}' idle nodes must be strings.`);
  return {
    locomotion: input.locomotion as string,
    chassis: input.chassis as string,
    hardware: input.hardware as string,
    teamAccents: input.teamAccents as string,
    emissives: input.emissives as string,
    idle: input.idle as string[],
  };
}

function validateMotion(
  input: unknown,
  id: string,
): LookModelSpec['motion'] | undefined {
  if (input === undefined) return undefined;
  if (!isRecord(input))
    throw new Error(`3D look '${id}' motion must be an object.`);
  const locomotion = input.locomotion;
  const handling = input.handling;
  const hardwareLagTicks = input.hardwareLagTicks;
  const hardwareOvershoot = input.hardwareOvershoot;
  if (!['low-hover', 'treads', 'wheels', 'skids'].includes(String(locomotion)))
    throw new Error(`3D look '${id}' has invalid model locomotion.`);
  if (!['swift', 'standard', 'deliberate'].includes(String(handling)))
    throw new Error(`3D look '${id}' has invalid handling.`);
  if (
    typeof hardwareLagTicks !== 'number' ||
    !Number.isFinite(hardwareLagTicks) ||
    hardwareLagTicks <= 0 ||
    hardwareLagTicks > 1 ||
    typeof hardwareOvershoot !== 'number' ||
    !Number.isFinite(hardwareOvershoot) ||
    hardwareOvershoot < 0 ||
    hardwareOvershoot > 0.25
  )
    throw new Error(`3D look '${id}' has invalid hardware lag tuning.`);
  return {
    locomotion: locomotion as NonNullable<LookModelSpec['motion']>['locomotion'],
    handling: handling as NonNullable<LookModelSpec['motion']>['handling'],
    hardwareLagTicks,
    hardwareOvershoot,
  };
}

function validateSource(
  input: unknown,
  id: string,
): LookModelSpec['source'] | undefined {
  if (input === undefined) return undefined;
  if (!isRecord(input))
    throw new Error(`3D look '${id}' source must be an object.`);
  for (const key of ['generator', 'recipe', 'sourceSha256'])
    if (typeof input[key] !== 'string' || input[key].length === 0)
      throw new Error(`3D look '${id}' has invalid source ${key}.`);
  if (!/^[0-9a-f]{64}$/.test(input.sourceSha256 as string))
    throw new Error(`3D look '${id}' has invalid source hash.`);
  const hasLayeredSource =
    typeof input.layeredSource === 'string' && input.layeredSource.length > 0;
  const hasArtifact = typeof input.artifact === 'string' && input.artifact.length > 0;
  if (hasLayeredSource === hasArtifact)
    throw new Error(
      `3D look '${id}' source must name exactly one layered source or provider artifact.`,
    );
  if (hasArtifact) {
    for (const key of ['provider', 'model', 'endpoint', 'modelType', 'taskId'])
      if (typeof input[key] !== 'string' || input[key].length === 0)
        throw new Error(`3D look '${id}' has invalid provider source ${key}.`);
    if (input.orientation !== 'identity' && input.orientation !== 'lay-flat-x')
      throw new Error(`3D look '${id}' has invalid provider orientation.`);
    if (input.facingYawDegrees !== undefined && !Number.isFinite(input.facingYawDegrees))
      throw new Error(`3D look '${id}' has invalid provider facing correction.`);
    const hasApprovedArtifact =
      typeof input.approvedArtifact === 'string' && input.approvedArtifact.length > 0;
    const hasTextureTier = typeof input.textureTier === 'string' && input.textureTier.length > 0;
    if (hasApprovedArtifact !== hasTextureTier)
      throw new Error(`3D look '${id}' runtime tier needs approval and tier provenance.`);
    if (hasApprovedArtifact) {
      if (
        typeof input.approvedSha256 !== 'string' ||
        !/^[0-9a-f]{64}$/.test(input.approvedSha256)
      )
        throw new Error(`3D look '${id}' has invalid approved provider hash.`);
    }
  }
  return input as NonNullable<LookModelSpec['source']>;
}

function validateLedger(
  input: unknown,
  id: string,
): LookModelSpec['ledger'] | undefined {
  if (input === undefined) return undefined;
  if (!isRecord(input))
    throw new Error(`3D look '${id}' ledger must be an object.`);
  for (const key of ['bytes', 'triangles', 'materials', 'textureCount'])
    if (!Number.isInteger(input[key]) || (input[key] as number) <= 0)
      throw new Error(`3D look '${id}' has invalid ledger ${key}.`);
  if (typeof input.sha256 !== 'string' || !/^[0-9a-f]{64}$/.test(input.sha256))
    throw new Error(`3D look '${id}' has invalid ledger hash.`);
  const memoryFields = [
    'geometryGpuBytes',
    'textureGpuBytesCompressedTarget',
    'textureGpuBytesRgba8Fallback',
    'modelGpuBytesCompressedTarget',
    'modelGpuBytesRgba8Fallback',
  ];
  const presentMemoryFields = memoryFields.filter((key) => input[key] !== undefined);
  if (
    presentMemoryFields.length !== 0 &&
    (presentMemoryFields.length !== memoryFields.length ||
      presentMemoryFields.some(
        (key) => !Number.isInteger(input[key]) || (input[key] as number) <= 0,
      ))
  )
    throw new Error(`3D look '${id}' has an incomplete GPU-memory ledger.`);
  if (presentMemoryFields.length > 0) {
    if (
      typeof input.geometrySha256 !== 'string' ||
      !/^[0-9a-f]{64}$/.test(input.geometrySha256) ||
      typeof input.textureTier !== 'string' ||
      input.textureTier.length === 0
    )
      throw new Error(`3D look '${id}' has invalid runtime texture provenance.`);
  }
  return input as NonNullable<LookModelSpec['ledger']>;
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null && !Array.isArray(value);
}
