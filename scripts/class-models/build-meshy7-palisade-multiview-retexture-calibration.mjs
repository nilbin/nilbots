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
const planPath = path.join(
  repository,
  'art/class-models/provider-runs/meshy/arc-relay-meshy7-fleet-retexture-v3-calibration/plan.json',
);
const plan = JSON.parse(readFileSync(planPath, 'utf8'));
if (
  plan.status !== 'owner-review' ||
  plan.classId !== 'palisade' ||
  plan.consumedCredits !== 10 ||
  plan.runtimeAssetsPromoted !== false
)
  throw new Error('Palisade multiview retexture calibration is not complete.');

const runDirectory = path.join(path.dirname(planPath), plan.classId, plan.completedTaskId);
const geometrySourcePath = path.join(repository, plan.source.rawModel.file);
const textureDonorPath = path.join(runDirectory, 'raw-model.glb');
const outputDirectory = path.join(
  repository,
  'art/class-models/runtime-tiers/arc-relay/meshy7-palisade-multiview-retexture-calibration-v1',
);
const outputPath = path.join(outputDirectory, 'arc-palisade-lean.glb');
const auditPath = path.join(outputDirectory, 'audit.json');
const geometryReferencePath = path.join(
  repository,
  'art/class-models/runtime-tiers/arc-relay/meshy7-fleet-review-v1/arc-palisade-lean.glb',
);
const targetPlanformSpan = 0.99;
const yawDegrees = 180;
const profile = {
  simplifyRatio: 0.04,
  simplifyError: 0.0025,
  textureSize: 512,
  transferBytes: 4_000_000,
  triangles: 100_000,
};

validateFile(geometrySourcePath, plan.source.rawModel);
validateFile(
  textureDonorPath,
  plan.artifacts.find((artifact) => artifact.file === 'raw-model.glb'),
);
if (!existsSync(geometryReferencePath)) throw new Error('V1 Palisade geometry reference is missing.');
mkdirSync(outputDirectory, { recursive: true });
if (!check) build();

const output = inspect();
const audit = {
  schemaVersion: 1,
  id: 'meshy7-palisade-multiview-retexture-calibration-v1',
  status: 'owner-review',
  purpose:
    'Single Palisade material calibration against the successful Kestrel ordered-multiview route; no runtime promotion',
  sourcePlan: path.relative(repository, planPath),
  sourceTaskId: plan.source.taskId,
  retextureTaskId: plan.completedTaskId,
  styleAuthority: plan.styleAuthority,
  credits: {
    consumed: plan.consumedCredits,
    balanceBefore: plan.balanceBefore,
    balanceAfter: plan.balanceAfter,
  },
  geometry: {
    source: plan.source.rawModel,
    textureDonor: {
      file: path.relative(repository, textureDonorPath),
      ...plan.artifacts.find((artifact) => artifact.file === 'raw-model.glb'),
    },
    originalUvsPreserved: true,
    providerVertexRewriteUsed: false,
    geometryReference: path.relative(repository, geometryReferencePath),
    exactReviewGeometryPreserved: output.geometrySha256 === output.referenceGeometrySha256,
  },
  normalization: {
    facing: '+x',
    up: '+y',
    floorY: 0,
    targetPlanformSpan,
    yawDegrees,
  },
  profile: {
    ...profile,
    gltfTransform: '4.4.2',
    geometryCompression: 'quantize',
    textureEncoding: 'WebP',
  },
  runtimeAssetsPromoted: false,
  output,
};
if (!audit.geometry.exactReviewGeometryPreserved)
  throw new Error('Palisade calibration review geometry differs from the V1 reference.');

if (check) {
  const recorded = JSON.parse(readFileSync(auditPath, 'utf8'));
  if (JSON.stringify(recorded, null, 2) !== JSON.stringify(audit, null, 2))
    throw new Error('Palisade multiview calibration audit drifted.');
  console.log('Verified Palisade multiview retexture calibration.');
} else {
  writeFileSync(auditPath, `${JSON.stringify(audit, null, 2)}\n`);
  console.log('Built Palisade multiview retexture calibration.');
}

function build() {
  const temporary = mkdtempSync(path.join(tmpdir(), 'nilbots-palisade-multiview-retexture-'));
  try {
    const transplanted = path.join(temporary, 'palisade-transplanted.glb');
    const normalized = path.join(temporary, 'palisade-normalized.glb');
    const optimized = path.join(temporary, 'palisade-optimized.glb');
    run(process.execPath, [
      path.join(repository, 'scripts/class-models/transplant-meshy-retexture.mjs'),
      geometrySourcePath,
      textureDonorPath,
      transplanted,
    ]);
    run(process.execPath, [
      path.join(repository, 'scripts/class-models/normalize-provider-glb.mjs'),
      transplanted,
      normalized,
      String(targetPlanformSpan),
      'identity',
      '--strip-authored-tangents',
    ]);
    run('npx', [
      '--yes',
      '@gltf-transform/cli@4.4.2',
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
      outputPath,
      String(yawDegrees),
    ]);
  } finally {
    rmSync(temporary, { recursive: true, force: true });
  }
}

function inspect() {
  if (!existsSync(outputPath)) throw new Error('Palisade calibration GLB is missing.');
  const bytes = readFileSync(outputPath);
  const { document } = readGlb(bytes, 'Palisade calibration');
  const scene = document.scenes?.[document.scene ?? 0];
  const root = document.nodes?.[scene?.nodes?.[0]];
  const normalization = root?.extras?.nilbotsProviderNormalization;
  const primitives = (document.meshes ?? []).flatMap((mesh) => mesh.primitives ?? []);
  const triangles = triangleCount(document);
  if (
    document.cameras ||
    document.skins ||
    document.animations ||
    document.meshes?.length !== 1 ||
    primitives.length !== 1 ||
    document.materials?.length !== 1 ||
    document.textures?.length !== 3 ||
    document.images?.length !== 3 ||
    primitives[0].attributes?.TANGENT !== undefined
  )
    throw new Error('Palisade calibration changed the lean monolithic model contract.');
  if (
    normalization?.facing !== '+x' ||
    normalization?.up !== '+y' ||
    normalization?.floorY !== 0 ||
    normalization?.targetPlanformSpan !== targetPlanformSpan ||
    normalization?.facingYawDegrees !== yawDegrees
  )
    throw new Error('Palisade calibration normalization drifted.');
  if (
    bytes.length > profile.transferBytes ||
    triangles > profile.triangles ||
    (document.images ?? []).some((image) => image.mimeType !== 'image/webp')
  )
    throw new Error('Palisade calibration exceeded its review budget or texture contract.');
  const reference = readFileSync(geometryReferencePath);
  return {
    artifact: {
      file: path.relative(repository, outputPath),
      bytes: bytes.length,
      sha256: sha256(bytes),
    },
    triangles,
    positionVertices: document.accessors?.[primitives[0].attributes.POSITION]?.count ?? 0,
    meshes: document.meshes.length,
    primitives: primitives.length,
    materials: document.materials.length,
    textures: document.textures.length,
    geometrySha256: geometryFingerprint(bytes, 'Palisade calibration'),
    referenceGeometrySha256: geometryFingerprint(reference, 'Palisade V1 reference'),
    memory: inspectModelMemory(bytes, 'Palisade calibration'),
  };
}

function validateFile(file, record) {
  if (!record || !existsSync(file)) throw new Error(`Missing audited file ${file}.`);
  const bytes = readFileSync(file);
  if (bytes.length !== record.bytes || sha256(bytes) !== record.sha256)
    throw new Error(`Audited file drifted: ${file}.`);
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
