#!/usr/bin/env node

import { createHash } from 'node:crypto';
import {
  copyFileSync,
  mkdirSync,
  readFileSync,
  writeFileSync,
} from 'node:fs';
import { dirname, join, relative, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import {
  geometryFingerprint,
  inspectModelMemory,
  readGlb,
  triangleCount,
} from './model-memory.mjs';

const repository = resolve(dirname(fileURLToPath(import.meta.url)), '..', '..');
const reviewAuditPath = join(
  repository,
  'art/class-models/runtime-tiers/arc-relay/reactor-recess-review-v1/audit.json',
);
const runtimeDirectory = join(
  repository,
  'web/src/assets/objective-models/arc-team-reactor',
);
const runtimePath = join(runtimeDirectory, 'model.glb');
const manifestPath = join(runtimeDirectory, 'model3d.json');
const ledgerPath = join(repository, 'art/objective-models/arc-relay/ledger.json');
const generator = 'scripts/class-models/promote-reactor-recess-v1.mjs';
const textureTier = 'reactor-recess-review-v1-webp';
const check = process.argv.includes('--check');
const audit = JSON.parse(readFileSync(reviewAuditPath, 'utf8'));
const profile = audit.profiles?.find((entry) => entry.id === 'lean');

if (audit.id !== 'reactor-recess-review-v1' || audit.status !== 'owner-review' || !profile)
  throw new Error('The promoted Reactor must come from the pinned owner-reviewed lean tier.');

const sourcePath = join(repository, profile.artifact.file);
const candidate = readFileSync(sourcePath);
const hash = sha256(candidate);
if (candidate.length !== profile.artifact.bytes || hash !== profile.artifact.sha256)
  throw new Error('The approved Reactor bytes or SHA-256 drifted.');
if (candidate.length > profile.budgets.transferBytes)
  throw new Error('The approved Reactor exceeds its transfer budget.');

const { document } = readGlb(candidate, 'approved Reactor recess');
const scene = document.scenes?.[document.scene ?? 0];
const root = document.nodes?.[scene?.nodes?.[0]];
const normalization = root?.extras?.nilbotsProviderNormalization;
const matte = document.asset?.extras?.nilbotsMatteRoughness;
const triangles = triangleCount(document);
const materials = document.materials?.length ?? 0;
const textures = document.textures?.length ?? 0;
const primitives = (document.meshes ?? []).flatMap((mesh) => mesh.primitives ?? []);
const memory = inspectModelMemory(candidate, 'approved Reactor recess');

if (document.cameras || document.skins || document.animations)
  throw new Error('The runtime Reactor contains a camera, skin, or animation.');
if (
  document.meshes?.length !== 1 ||
  primitives.length !== 1 ||
  materials !== 1 ||
  textures !== 3 ||
  document.images?.length !== 3
)
  throw new Error('The approved monolithic Reactor mesh/material contract drifted.');
if (
  normalization?.facing !== '+x' ||
  normalization?.up !== '+y' ||
  normalization?.floorY !== 0 ||
  normalization?.orientation !== 'identity' ||
  normalization?.targetPlanformSpan !== 1.5 ||
  normalization?.facingYawDegrees !== 90
)
  throw new Error('The Reactor normalization drifted from the approved 1.5-tile +X contract.');
if (
  matte?.floor !== 0.82 ||
  matte?.minimumAfter < 0.82 ||
  matte?.baseColorChanged !== false ||
  matte?.metallicChanged !== false ||
  matte?.normalChanged !== false
)
  throw new Error('The approved deterministic matte treatment drifted.');
if (
  !(document.extensionsRequired ?? []).includes('KHR_mesh_quantization') ||
  !(document.extensionsRequired ?? []).includes('EXT_texture_webp') ||
  (document.images ?? []).some((image) => image.mimeType !== 'image/webp')
)
  throw new Error('The runtime Reactor must keep quantized geometry and embedded WebP maps.');
if ((document.materials ?? []).some((material) => material.extras?.nilbotsRole === 'team-accent'))
  throw new Error('The neutral Reactor model must not own team paint.');
if (triangles !== profile.triangles || triangles > profile.budgets.triangles)
  throw new Error('The Reactor triangle ledger or budget drifted.');
if (geometryFingerprint(candidate, 'approved Reactor recess') !== profile.geometrySha256)
  throw new Error('The Reactor geometry fingerprint drifted.');
if (
  memory.textures.length !== 3 ||
  memory.textures.some(
    (texture) =>
      texture.mimeType !== 'image/webp' ||
      texture.width !== 512 ||
      texture.height !== 512,
  )
)
  throw new Error('The Reactor embedded 512 px PBR texture contract drifted.');
for (const key of [
  'transferBytes',
  'geometryGpuBytes',
  'textureGpuBytesCompressedTarget',
  'textureGpuBytesRgba8Fallback',
  'modelGpuBytesCompressedTarget',
  'modelGpuBytesRgba8Fallback',
])
  if (memory[key] !== profile.memory[key])
    throw new Error(`The audited Reactor ${key} drifted.`);

const manifest = {
  version: 1,
  id: 'arc-team-reactor',
  file: 'model.glb',
  kind: 'objective',
  part: 'whole',
  facing: '+x',
  up: '+y',
  source: {
    generator,
    recipe: relative(repository, reviewAuditPath),
    artifact: profile.artifact.file,
    sourceSha256: hash,
    approvedArtifact: profile.artifact.file,
    approvedSha256: hash,
    textureTier,
    provider: 'Meshy',
    model: 'meshy-7',
    endpoint: audit.sources.textureDonor.endpoint,
    modelType: 'original-uv-pbr-retexture',
    taskId: audit.sources.textureDonor.taskId,
    orientation: 'identity',
    facingYawDegrees: 90,
  },
  ledger: {
    bytes: candidate.length,
    sha256: hash,
    triangles,
    materials,
    textureCount: textures,
    geometrySha256: profile.geometrySha256,
    geometryGpuBytes: memory.geometryGpuBytes,
    textureGpuBytesCompressedTarget: memory.textureGpuBytesCompressedTarget,
    textureGpuBytesRgba8Fallback: memory.textureGpuBytesRgba8Fallback,
    modelGpuBytesCompressedTarget: memory.modelGpuBytesCompressedTarget,
    modelGpuBytesRgba8Fallback: memory.modelGpuBytesRgba8Fallback,
    textureTier,
  },
};

const ledger = {
  version: 1,
  status: 'owner-approved',
  approval:
    'Owner approved the low ground-recess Reactor and requested renderer-owned team glow from below.',
  generator,
  runtimeTier: relative(repository, reviewAuditPath),
  textureTier,
  provider: 'Meshy',
  providerModel: 'meshy-7',
  meshContract:
    'One neutral matte 1.5-tile ring with an open aperture; charge, integrity, crest, pulse, damage, destruction, and team glow remain renderer-owned.',
  modelOwnedTeamGlow: false,
  rendererOwnedTeamGlow:
    'low self-lit additive aperture pool; neutral housing excludes transient point lights',
  model: {
    id: 'arc-team-reactor',
    taskId: audit.sources.textureDonor.taskId,
    endpoint: audit.sources.textureDonor.endpoint,
    artifact: profile.artifact.file,
    bytes: candidate.length,
    sha256: hash,
    triangles,
    materials,
    textures,
    geometrySha256: profile.geometrySha256,
    matteRoughnessFloor: 0.82,
    memory,
    budgets: profile.budgets,
    targetPlanformSpan: normalization.targetPlanformSpan,
    floorY: normalization.floorY,
  },
};

if (check) {
  assertBytes(runtimePath, candidate);
  assertText(manifestPath, json(manifest));
  assertText(ledgerPath, json(ledger));
} else {
  mkdirSync(runtimeDirectory, { recursive: true });
  copyFileSync(sourcePath, runtimePath);
  writeFileSync(manifestPath, json(manifest));
  writeFileSync(ledgerPath, json(ledger));
}

console.log(`${check ? 'Verified' : 'Promoted'} Reactor recess (${candidate.length} bytes).`);

function sha256(bytes) {
  return createHash('sha256').update(bytes).digest('hex');
}

function json(value) {
  return `${JSON.stringify(value, null, 2)}\n`;
}

function assertBytes(path, expected) {
  const actual = readFileSync(path);
  if (!actual.equals(expected)) throw new Error(`${relative(repository, path)} is not promoted.`);
}

function assertText(path, expected) {
  if (readFileSync(path, 'utf8') !== expected)
    throw new Error(`${relative(repository, path)} is not the deterministic promoted output.`);
}
