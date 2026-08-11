#!/usr/bin/env node

import { spawnSync } from 'node:child_process';
import { createHash } from 'node:crypto';
import {
  existsSync,
  mkdirSync,
  mkdtempSync,
  readFileSync,
  rmSync,
  writeFileSync,
} from 'node:fs';
import { tmpdir } from 'node:os';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import {
  geometryFingerprint,
  inspectModelMemory,
  readGlb,
  triangleCount,
} from './model-memory.mjs';

const repository = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..', '..');
const check = process.argv.includes('--check');
const sourceTaskId = '019fefdd-ac96-7dc8-a61e-f0760b4ae407';
const sourcePath = path.join(
  repository,
  'art/class-models/provider-runs/meshy/arc-relay-meshy7-retexture/kestrel',
  sourceTaskId,
  'raw-model.glb',
);
const expectedSourceSha256 =
  '6daeca9df3f60fd9e7260b3fd7ec3dd5ecba15505a440da4c36700cc98ae31b3';
const tierId = 'meshy7-kestrel-review-v1';
const tierDirectory = path.join(
  repository,
  'art/class-models/runtime-tiers/arc-relay',
  tierId,
);
const auditPath = path.join(tierDirectory, 'audit.json');
const targetPlanformSpan = 0.99;
const facingYawDegrees = 180;
const toolVersion = '4.4.2';
const profiles = [
  {
    id: 'lean',
    file: 'arc-kestrel-lean.glb',
    simplifyRatio: 0.04,
    simplifyError: 0.0025,
    textureSize: 512,
    budgets: { transferBytes: 4_000_000, triangles: 100_000 },
  },
  {
    id: 'rich',
    file: 'arc-kestrel-rich.glb',
    simplifyRatio: 0.08,
    simplifyError: 0.001,
    textureSize: 1024,
    budgets: { transferBytes: 8_000_000, triangles: 180_000 },
  },
];

if (!existsSync(sourcePath))
  throw new Error(`Missing ignored Meshy 7 source master: ${path.relative(repository, sourcePath)}.`);
const source = readFileSync(sourcePath);
const sourceSha256 = sha256(source);
if (sourceSha256 !== expectedSourceSha256)
  throw new Error(
    `Kestrel source SHA-256 mismatch: expected ${expectedSourceSha256}, got ${sourceSha256}.`,
  );

mkdirSync(tierDirectory, { recursive: true });
if (!check) buildProfiles();

const outputs = profiles.map((profile) => inspectProfile(profile));
const audit = {
  schemaVersion: 1,
  id: tierId,
  status: 'owner-review',
  generator: 'scripts/class-models/build-meshy7-kestrel-review-tiers.mjs',
  source: {
    taskId: sourceTaskId,
    file: path.relative(repository, sourcePath),
    bytes: source.length,
    sha256: sourceSha256,
    provider: 'Meshy',
    providerModel: 'meshy-7',
    endpoint: 'retexture',
  },
  toolchain: {
    gltfTransform: toolVersion,
    simplifier: 'meshoptimizer through glTF-Transform',
    textureEncoding: 'WebP review tier; KTX2 is required before shipping',
  },
  normalization: {
    orientation: 'identity',
    facing: '+x',
    up: '+y',
    floorY: 0,
    facingYawDegrees,
    targetPlanformSpan,
  },
  contract: {
    purpose: 'Reversible in-renderer comparison only; does not replace the approved runtime Kestrel',
    geometry: 'lossy review simplification from the pinned retextured provider master',
    materials: 'one fused authored material; team ownership remains renderer-owned',
    tangentSpace:
      'normal-mapped without authored tangents; Three.js derivative tangent-space review required',
  },
  profiles: outputs,
};

if (check) {
  const recorded = JSON.parse(readFileSync(auditPath, 'utf8'));
  if (JSON.stringify(recorded, null, 2) !== JSON.stringify(audit, null, 2))
    throw new Error('Kestrel Meshy 7 review-tier audit drifted from the current artifacts.');
  console.log(`Verified ${outputs.length} ${tierId} artifacts.`);
} else {
  writeFileSync(auditPath, `${JSON.stringify(audit, null, 2)}\n`);
  console.log(`Built ${outputs.length} ${tierId} artifacts.`);
}

function buildProfiles() {
  const temporary = mkdtempSync(path.join(tmpdir(), 'nilbots-meshy7-kestrel-tiers-'));
  try {
    const normalized = path.join(temporary, 'source-normalized.glb');
    run(process.execPath, [
      path.join(repository, 'scripts/class-models/normalize-provider-glb.mjs'),
      sourcePath,
      normalized,
      String(targetPlanformSpan),
      'identity',
    ]);
    for (const profile of profiles) {
      console.log(`Building Kestrel ${profile.id} review tier...`);
      const optimized = path.join(temporary, `${profile.id}-optimized.glb`);
      run('npx', [
        '--yes',
        `@gltf-transform/cli@${toolVersion}`,
        'optimize',
        normalized,
        optimized,
        '--compress',
        'quantize',
        '--texture-compress',
        'webp',
        '--texture-size',
        String(profile.textureSize),
        '--flatten',
        'false',
        '--join',
        'false',
        '--instance',
        'false',
        '--palette',
        'false',
        '--simplify',
        'true',
        '--simplify-ratio',
        String(profile.simplifyRatio),
        '--simplify-error',
        String(profile.simplifyError),
        '--simplify-lock-border',
        'false',
        '--weld',
        'true',
      ]);
      run(process.execPath, [
        path.join(repository, 'scripts/class-models/correct-provider-facing.mjs'),
        optimized,
        path.join(tierDirectory, profile.file),
        String(facingYawDegrees),
      ]);
    }
  } finally {
    rmSync(temporary, { recursive: true, force: true });
  }
}

function inspectProfile(profile) {
  const outputPath = path.join(tierDirectory, profile.file);
  if (!existsSync(outputPath)) throw new Error(`${profile.id}: output GLB is missing.`);
  const bytes = readFileSync(outputPath);
  const { document } = readGlb(bytes, `Kestrel ${profile.id}`);
  const scene = document.scenes?.[document.scene ?? 0];
  const root = document.nodes?.[scene?.nodes?.[0]];
  const normalization = root?.extras?.nilbotsProviderNormalization;
  const triangles = triangleCount(document);
  const positionVertices = (document.meshes ?? [])
    .flatMap((mesh) => mesh.primitives ?? [])
    .reduce(
      (sum, primitive) =>
        sum + (document.accessors?.[primitive.attributes?.POSITION]?.count ?? 0),
      0,
    );
  const memory = inspectModelMemory(bytes, `Kestrel ${profile.id}`);
  const primitives = (document.meshes ?? []).flatMap((mesh) => mesh.primitives ?? []);
  if (
    document.cameras ||
    document.skins ||
    document.animations ||
    document.materials?.length !== 1 ||
    document.textures?.length !== 3 ||
    document.images?.length !== 3 ||
    document.meshes?.length !== 1 ||
    primitives.length !== 1
  )
    throw new Error(`${profile.id}: review artifact changed the monolithic Kestrel contract.`);
  if (
    normalization?.facing !== '+x' ||
    normalization?.up !== '+y' ||
    normalization?.floorY !== 0 ||
    normalization?.orientation !== 'identity' ||
    normalization?.targetPlanformSpan !== targetPlanformSpan ||
    normalization?.facingYawDegrees !== facingYawDegrees
  )
    throw new Error(`${profile.id}: review artifact normalization drifted.`);
  if (
    !(document.extensionsRequired ?? []).includes('KHR_mesh_quantization') ||
    (document.images ?? []).some((image) => image.mimeType !== 'image/webp')
  )
    throw new Error(`${profile.id}: review artifact must use quantized geometry and WebP maps.`);
  if (
    memory.textures.some(
      (texture) => texture.width !== profile.textureSize || texture.height !== profile.textureSize,
    )
  )
    throw new Error(`${profile.id}: texture dimensions drifted from ${profile.textureSize}.`);
  if (bytes.length > profile.budgets.transferBytes)
    throw new Error(
      `${profile.id}: ${bytes.length} bytes exceeds ${profile.budgets.transferBytes}-byte budget.`,
    );
  if (triangles > profile.budgets.triangles)
    throw new Error(
      `${profile.id}: ${triangles} triangles exceeds ${profile.budgets.triangles}-triangle budget.`,
    );
  return {
    id: profile.id,
    settings: {
      simplifyRatio: profile.simplifyRatio,
      simplifyError: profile.simplifyError,
      lockBorder: false,
      textureSize: profile.textureSize,
      geometryCompression: 'quantize',
      textureEncoding: 'WebP',
    },
    budgets: profile.budgets,
    artifact: {
      file: path.relative(repository, outputPath),
      bytes: bytes.length,
      sha256: sha256(bytes),
    },
    triangles,
    positionVertices,
    meshes: document.meshes.length,
    primitives: primitives.length,
    materials: document.materials.length,
    textures: document.textures.length,
    geometrySha256: geometryFingerprint(bytes, `Kestrel ${profile.id}`),
    memory,
  };
}

function run(command, args) {
  const result = spawnSync(command, args, { cwd: repository, stdio: 'inherit' });
  if (result.error) throw result.error;
  if (result.status !== 0)
    throw new Error(`${command} failed with exit code ${result.status}.`);
}

function sha256(value) {
  return createHash('sha256').update(value).digest('hex');
}
