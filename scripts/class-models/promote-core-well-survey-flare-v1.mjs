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
  'art/class-models/runtime-tiers/arc-relay/longshot-objectives-review-v1/audit.json',
);
const generator = 'scripts/class-models/promote-core-well-survey-flare-v1.mjs';
const textureTier = 'longshot-objectives-review-v1-webp';
const check = process.argv.includes('--check');
const audit = JSON.parse(readFileSync(reviewAuditPath, 'utf8'));

if (audit.id !== 'longshot-objectives-review-v1' || audit.status !== 'owner-review')
  throw new Error('Well and Survey Flare must come from the pinned owner-review tier.');

const definitions = [
  {
    candidateId: 'core-well',
    runtimeId: 'arc-core-well',
    kind: 'objective',
    runtimeDirectory: 'web/src/assets/objective-models/arc-core-well',
    ledger: 'art/objective-models/arc-relay/core-well-ledger.json',
    textureSize: 512,
    textureSlots: [
      'baseColorTexture',
      'emissiveTexture',
      'metallicRoughnessTexture',
      'normalTexture',
    ],
    modelType: 'neutral-objective-housing-owner-approved-four-anchor',
    approval:
      'Owner approved the flattened, recessed Core Well as the default gameplay housing.',
    rendererOwnedState:
      'source glyph, birth/rearm progress, live Core counter, ownership, and spawn indication',
  },
  {
    candidateId: 'survey-flare',
    runtimeId: 'arc-survey-flare',
    kind: 'signature',
    signature: 'survey-flare',
    runtimeDirectory: 'web/src/assets/signature-models/arc-survey-flare',
    ledger: 'art/signature-models/arc-relay/survey-flare-ledger.json',
    textureSize: 256,
    textureSlots: [
      'baseColorTexture',
      'metallicRoughnessTexture',
      'normalTexture',
    ],
    modelType: 'original-uv-pbr-retexture',
    approval:
      'Owner approved the Meshy Survey Flare as Lantern’s default signature device.',
    rendererOwnedState: 'flight arc, landing pose, scan radius, scan timing, and survey field',
  },
];

for (const definition of definitions) promote(definition);

console.log(
  `${check ? 'Verified' : 'Promoted'} Core Well and Survey Flare ` +
    `(${definitions.map((entry) => entry.candidate.artifact.bytes).join(' + ')} bytes).`,
);

function promote(definition) {
  const candidate = audit.candidates.find((entry) => entry.id === definition.candidateId);
  if (!candidate || candidate.kind !== definition.kind)
    throw new Error(`${definition.candidateId}: missing or mismatched review candidate.`);
  definition.candidate = candidate;

  const sourcePath = join(repository, candidate.artifact.file);
  const runtimeDirectory = join(repository, definition.runtimeDirectory);
  const runtimePath = join(runtimeDirectory, 'model.glb');
  const manifestPath = join(runtimeDirectory, 'model3d.json');
  const ledgerPath = join(repository, definition.ledger);
  const bytes = readFileSync(sourcePath);
  const hash = sha256(bytes);

  if (bytes.length !== candidate.artifact.bytes || hash !== candidate.artifact.sha256)
    throw new Error(`${definition.candidateId}: approved bytes or SHA-256 drifted.`);
  if (bytes.length > candidate.profile.budgets.transferBytes)
    throw new Error(`${definition.candidateId}: approved transfer budget exceeded.`);

  const { document } = readGlb(bytes, definition.candidateId);
  const scene = document.scenes?.[document.scene ?? 0];
  const root = document.nodes?.[scene?.nodes?.[0]];
  const normalization = root?.extras?.nilbotsProviderNormalization;
  const matte = document.asset?.extras?.nilbotsMatteRoughness;
  const triangles = triangleCount(document);
  const materials = document.materials?.length ?? 0;
  const textures = document.textures?.length ?? 0;
  const primitives = (document.meshes ?? []).flatMap((mesh) => mesh.primitives ?? []);
  const memory = inspectModelMemory(bytes, definition.candidateId);

  if (document.cameras || document.skins || document.animations)
    throw new Error(`${definition.candidateId}: runtime model contains rigging or a camera.`);
  if ((document.nodes ?? []).some((node) => node.camera !== undefined || node.skin !== undefined))
    throw new Error(`${definition.candidateId}: runtime node contains rigging or a camera.`);
  if (document.meshes?.length !== 1 || primitives.length !== 1 || materials !== 1)
    throw new Error(`${definition.candidateId}: monolithic mesh/material contract drifted.`);
  if (
    textures !== candidate.textures ||
    document.images?.length !== candidate.textures ||
    (document.images ?? []).some((image) => image.mimeType !== 'image/webp')
  )
    throw new Error(`${definition.candidateId}: embedded WebP texture contract drifted.`);
  if (
    !(document.extensionsRequired ?? []).includes('EXT_texture_webp') ||
    !(document.extensionsRequired ?? []).includes('KHR_mesh_quantization') ||
    (document.textures ?? []).some(
      (texture) => texture.extensions?.EXT_texture_webp?.source === undefined,
    )
  )
    throw new Error(`${definition.candidateId}: runtime extensions drifted.`);
  if ((document.materials ?? []).some((material) => material.extras?.nilbotsRole === 'team-accent'))
    throw new Error(`${definition.candidateId}: authored model must not own team paint.`);
  if (
    root?.name !== 'chassis' ||
    normalization?.facing !== '+x' ||
    normalization?.up !== '+y' ||
    normalization?.floorY !== 0 ||
    normalization?.orientation !== 'identity' ||
    normalization?.targetPlanformSpan !== candidate.normalization.targetPlanformSpan ||
    (normalization?.heightScale ?? 1) !== candidate.normalization.heightScale ||
    normalization?.facingYawDegrees !== candidate.facing.yawDegrees ||
    normalization?.facingCorrectionVersion !== 1
  )
    throw new Error(`${definition.candidateId}: +X/+Y gameplay normalization drifted.`);
  if (
    matte?.floor !== candidate.matteRoughness.floor ||
    matte?.minimumAfter < candidate.matteRoughness.floor ||
    matte?.baseColorChanged !== false ||
    matte?.metallicChanged !== false ||
    matte?.normalChanged !== false
  )
    throw new Error(`${definition.candidateId}: deterministic matte treatment drifted.`);
  if (triangles !== candidate.triangles || triangles > candidate.profile.budgets.triangles)
    throw new Error(`${definition.candidateId}: triangle ledger or budget drifted.`);
  if (geometryFingerprint(bytes, definition.candidateId) !== candidate.geometrySha256)
    throw new Error(`${definition.candidateId}: geometry fingerprint drifted.`);
  if (
    memory.textures.length !== candidate.textures ||
    memory.textures.some(
      (texture) =>
        texture.mimeType !== 'image/webp' ||
        texture.width !== definition.textureSize ||
        texture.height !== definition.textureSize,
    ) ||
    memory.textures.flatMap((texture) => texture.slots).sort().join(',') !==
      [...definition.textureSlots].sort().join(',')
  )
    throw new Error(`${definition.candidateId}: texture dimensions or slots drifted.`);
  for (const key of [
    'transferBytes',
    'geometryGpuBytes',
    'textureGpuBytesCompressedTarget',
    'textureGpuBytesRgba8Fallback',
    'modelGpuBytesCompressedTarget',
    'modelGpuBytesRgba8Fallback',
  ])
    if (memory[key] !== candidate.memory[key])
      throw new Error(`${definition.candidateId}: audited ${key} drifted.`);

  const manifest = {
    version: 1,
    id: definition.runtimeId,
    file: 'model.glb',
    kind: definition.kind,
    part: 'whole',
    facing: '+x',
    up: '+y',
    ...(definition.signature ? { signature: definition.signature } : {}),
    source: {
      generator,
      recipe: relative(repository, reviewAuditPath),
      artifact: candidate.artifact.file,
      sourceSha256: hash,
      approvedArtifact: candidate.artifact.file,
      approvedSha256: hash,
      textureTier,
      provider: 'Meshy',
      model: 'meshy-7',
      endpoint: candidate.endpoint,
      modelType: definition.modelType,
      taskId: candidate.taskId,
      orientation: 'identity',
      facingYawDegrees: candidate.facing.yawDegrees,
      ...(candidate.endpoint === 'retexture' ? { originalUV: true } : {}),
      targetPlanformSpan: candidate.normalization.targetPlanformSpan,
      ...(candidate.normalization.heightScale !== 1
        ? { heightScale: candidate.normalization.heightScale }
        : {}),
      matteRoughnessFloor: candidate.matteRoughness.floor,
    },
    ledger: {
      bytes: bytes.length,
      sha256: hash,
      triangles,
      materials,
      textureCount: textures,
      geometrySha256: candidate.geometrySha256,
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
    approval: definition.approval,
    generator,
    runtimeTier: relative(repository, reviewAuditPath),
    textureTier,
    provider: 'Meshy',
    providerModel: 'meshy-7',
    modelOwnedTeamGlow: false,
    rendererOwnedState: definition.rendererOwnedState,
    model: {
      id: definition.runtimeId,
      ...(definition.signature ? { signature: definition.signature } : {}),
      taskId: candidate.taskId,
      endpoint: candidate.endpoint,
      modelType: definition.modelType,
      artifact: candidate.artifact.file,
      bytes: bytes.length,
      sha256: hash,
      triangles,
      materials,
      textures,
      geometrySha256: candidate.geometrySha256,
      matteRoughnessFloor: candidate.matteRoughness.floor,
      memory,
      budgets: candidate.profile.budgets,
      targetPlanformSpan: normalization.targetPlanformSpan,
      heightScale: normalization.heightScale ?? 1,
      floorY: normalization.floorY,
    },
  };

  if (check) {
    assertBytes(runtimePath, bytes);
    assertText(manifestPath, json(manifest));
    assertText(ledgerPath, json(ledger));
  } else {
    mkdirSync(runtimeDirectory, { recursive: true });
    mkdirSync(dirname(ledgerPath), { recursive: true });
    copyFileSync(sourcePath, runtimePath);
    writeFileSync(manifestPath, json(manifest));
    writeFileSync(ledgerPath, json(ledger));
  }
}

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
