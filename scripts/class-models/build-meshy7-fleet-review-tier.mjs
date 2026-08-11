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
const classArgumentIndex = process.argv.indexOf('--class');
const classFilter = classArgumentIndex === -1 ? null : process.argv[classArgumentIndex + 1];
const sourceAuditPath = path.join(
  repository,
  'art/class-models/provider-runs/meshy/arc-relay-meshy7-fleet/fleet-audit.json',
);
const sourceAudit = JSON.parse(readFileSync(sourceAuditPath, 'utf8'));
const tierId = 'meshy7-fleet-review-v1';
const tierDirectory = path.join(
  repository,
  'art/class-models/runtime-tiers/arc-relay',
  tierId,
);
const auditPath = path.join(tierDirectory, 'audit.json');
const kestrelTierPath = path.join(
  repository,
  'art/class-models/runtime-tiers/arc-relay/meshy7-kestrel-review-v1/arc-kestrel-lean.glb',
);
const targetPlanformSpan = 0.99;
const facingYawDegrees = 180;
const toolVersion = '4.4.2';
const profile = {
  simplifyRatio: 0.04,
  simplifyError: 0.0025,
  textureSize: 512,
  budgets: { transferBytes: 4_000_000, triangles: 100_000 },
};
const profileOverrides = new Map([
  [
    'sunder',
    {
      simplifyError: 0.01,
      budgets: { transferBytes: 9_000_000, triangles: 220_000 },
      reason:
        'The disconnected layered shell reaches its topology limit before the shared lean target; retained for owner review rather than applying a second destructive pass.',
    },
  ],
]);
const outputTextureCountOverrides = new Map([
  ['mortar', 3],
]);
const entries = sourceAudit.entries.map((entry) => ({
  ...entry,
  sourcePath: path.join(repository, entry.rawModel.file),
  outputPath: entry.classId === 'kestrel'
    ? kestrelTierPath
    : path.join(tierDirectory, `arc-${entry.classId}-lean.glb`),
  reused: entry.classId === 'kestrel',
}));

if (entries.length !== 16 || new Set(entries.map((entry) => entry.classId)).size !== 16)
  throw new Error('Meshy 7 fleet audit must contain exactly 16 unique Arc Relay classes.');
if (classArgumentIndex !== -1 && !entries.some((entry) => entry.classId === classFilter))
  throw new Error(`Unknown --class ${classFilter ?? 'value'}.`);
for (const entry of entries) validateSource(entry);

mkdirSync(tierDirectory, { recursive: true });
if (!check) buildProfiles();

const classes = entries.map(inspectEntry);
const totals = classes.reduce(
  (sum, entry) => ({
    transferBytes: sum.transferBytes + entry.artifact.bytes,
    triangles: sum.triangles + entry.triangles,
    modelGpuBytesRgba8Fallback:
      sum.modelGpuBytesRgba8Fallback + entry.memory.modelGpuBytesRgba8Fallback,
  }),
  { transferBytes: 0, triangles: 0, modelGpuBytesRgba8Fallback: 0 },
);
const audit = {
  schemaVersion: 1,
  id: tierId,
  status: 'owner-review',
  generator: 'scripts/class-models/build-meshy7-fleet-review-tier.mjs',
  sourceAudit: path.relative(repository, sourceAuditPath),
  toolchain: {
    gltfTransform: toolVersion,
    simplifier: 'meshoptimizer through glTF-Transform',
    textureEncoding: 'WebP review tier; KTX2 is required before shipping',
  },
  normalization: {
    orientation: 'identity',
    sourceFacing: '-x',
    facing: '+x',
    up: '+y',
    floorY: 0,
    facingYawDegrees,
    targetPlanformSpan,
    facingEvidence:
      'Fixed front camera at source -X, matching the ordered front/right/back Meshy inputs; corrected by 180 degrees for the Nilbots +X contract.',
  },
  profile: {
    id: 'lean',
    settings: {
      simplifyRatio: profile.simplifyRatio,
      simplifyError: profile.simplifyError,
      lockBorder: false,
      textureSize: profile.textureSize,
      geometryCompression: 'quantize',
      textureEncoding: 'WebP',
    },
    classOverrides: Object.fromEntries(profileOverrides),
    budgets: profile.budgets,
  },
  contract: {
    purpose:
      'Reversible in-renderer fleet review only; no approved runtime model is replaced or promoted.',
    geometry: 'lossy review simplification from pinned Meshy 7 provider masters',
    materials: 'one fused authored material; team ownership remains renderer-owned',
    tangentSpace:
      'normal-mapped without authored tangents; Three.js derivative tangent-space review required',
  },
  totals,
  classes,
};

if (check) {
  const recorded = JSON.parse(readFileSync(auditPath, 'utf8'));
  if (JSON.stringify(recorded, null, 2) !== JSON.stringify(audit, null, 2))
    throw new Error('Meshy 7 fleet review-tier audit drifted from the current artifacts.');
  console.log(`Verified ${classes.length} ${tierId} artifacts.`);
} else {
  writeFileSync(auditPath, `${JSON.stringify(audit, null, 2)}\n`);
  console.log(`Built ${classes.length} ${tierId} artifacts.`);
}

function validateSource(entry) {
  if (!existsSync(entry.sourcePath))
    throw new Error(`${entry.classId}: missing ignored source ${entry.rawModel.file}.`);
  const source = readFileSync(entry.sourcePath);
  const sourceSha256 = sha256(source);
  if (source.length !== entry.rawModel.bytes || sourceSha256 !== entry.rawModel.sha256)
    throw new Error(`${entry.classId}: source bytes or SHA-256 drifted from the fleet audit.`);
}

function buildProfiles() {
  const temporary = mkdtempSync(path.join(tmpdir(), 'nilbots-meshy7-fleet-tier-'));
  try {
    for (const entry of entries) {
      if (classFilter && entry.classId !== classFilter) continue;
      if (entry.reused) {
        if (!existsSync(entry.outputPath))
          throw new Error('The reviewed Kestrel lean artifact is missing.');
        console.log('Reusing reviewed Kestrel lean tier.');
        continue;
      }
      console.log(`Building ${entry.classId} lean review tier...`);
      const settings = { ...profile, ...profileOverrides.get(entry.classId) };
      const normalized = path.join(temporary, `${entry.classId}-normalized.glb`);
      const optimized = path.join(temporary, `${entry.classId}-optimized.glb`);
      run(process.execPath, [
        path.join(repository, 'scripts/class-models/normalize-provider-glb.mjs'),
        entry.sourcePath,
        normalized,
        String(targetPlanformSpan),
        'identity',
      ]);
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
        String(settings.simplifyRatio),
        '--simplify-error',
        String(settings.simplifyError),
        '--simplify-lock-border',
        'false',
        '--weld',
        'true',
      ]);
      run(process.execPath, [
        path.join(repository, 'scripts/class-models/correct-provider-facing.mjs'),
        optimized,
        entry.outputPath,
        String(facingYawDegrees),
      ]);
    }
  } finally {
    rmSync(temporary, { recursive: true, force: true });
  }
}

function inspectEntry(entry) {
  if (!existsSync(entry.outputPath)) throw new Error(`${entry.classId}: output GLB is missing.`);
  const bytes = readFileSync(entry.outputPath);
  const { document } = readGlb(bytes, entry.classId);
  const scene = document.scenes?.[document.scene ?? 0];
  const root = document.nodes?.[scene?.nodes?.[0]];
  const normalization = root?.extras?.nilbotsProviderNormalization;
  const triangles = triangleCount(document);
  const primitives = (document.meshes ?? []).flatMap((mesh) => mesh.primitives ?? []);
  const textureCount = document.textures?.length ?? 0;
  const expectedTextures = outputTextureCountOverrides.get(entry.classId) ?? entry.rawModel.textures;
  if (
    document.cameras ||
    document.skins ||
    document.animations ||
    document.materials?.length !== 1 ||
    textureCount !== expectedTextures ||
    document.images?.length !== expectedTextures ||
    document.meshes?.length !== 1 ||
    primitives.length !== 1
  )
    throw new Error(`${entry.classId}: review artifact changed the monolithic model contract.`);
  if (
    normalization?.facing !== '+x' ||
    normalization?.up !== '+y' ||
    normalization?.floorY !== 0 ||
    normalization?.orientation !== 'identity' ||
    normalization?.targetPlanformSpan !== targetPlanformSpan ||
    normalization?.facingYawDegrees !== facingYawDegrees
  )
    throw new Error(`${entry.classId}: review artifact normalization drifted.`);
  if (
    !(document.extensionsRequired ?? []).includes('KHR_mesh_quantization') ||
    (document.images ?? []).some((image) => image.mimeType !== 'image/webp')
  )
    throw new Error(`${entry.classId}: expected quantized geometry and embedded WebP maps.`);
  const budgets = profileOverrides.get(entry.classId)?.budgets ?? profile.budgets;
  if (bytes.length > budgets.transferBytes || triangles > budgets.triangles)
    throw new Error(`${entry.classId}: lean review budget exceeded.`);
  const positionVertices = primitives.reduce(
    (sum, primitive) =>
      sum + (document.accessors?.[primitive.attributes?.POSITION]?.count ?? 0),
    0,
  );
  return {
    classId: entry.classId,
    taskId: entry.taskId,
    endpoint: entry.endpoint,
    source: {
      file: path.relative(repository, entry.sourcePath),
      bytes: entry.rawModel.bytes,
      sha256: entry.rawModel.sha256,
    },
    reusedReviewedKestrel: entry.reused,
    profileOverrides: profileOverrides.get(entry.classId) ?? null,
    budgets,
    artifact: {
      file: path.relative(repository, entry.outputPath),
      bytes: bytes.length,
      sha256: sha256(bytes),
    },
    triangles,
    positionVertices,
    meshes: document.meshes.length,
    primitives: primitives.length,
    materials: document.materials.length,
    textures: textureCount,
    geometrySha256: geometryFingerprint(bytes, entry.classId),
    memory: inspectModelMemory(bytes, entry.classId),
  };
}

function run(command, arguments_) {
  const result = spawnSync(command, arguments_, {
    cwd: repository,
    encoding: 'utf8',
    stdio: 'inherit',
  });
  if (result.error) throw result.error;
  if (result.status !== 0)
    throw new Error(`${command} failed with exit code ${result.status}.`);
}

function sha256(value) {
  return createHash('sha256').update(value).digest('hex');
}
