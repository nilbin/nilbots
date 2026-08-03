#!/usr/bin/env node

import { createHash } from 'node:crypto';
import {
  copyFileSync,
  readFileSync,
  writeFileSync,
} from 'node:fs';
import { dirname, join, relative, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import {
  geometryFingerprint,
  inspectModelMemory,
  triangleCount,
} from './model-memory.mjs';

const repository = resolve(dirname(fileURLToPath(import.meta.url)), '..', '..');
const auditPath = join(
  repository,
  'art/class-models/provider-runs/meshy/arc-fleet-review/fleet-audit.json',
);
const ledgerPath = join(repository, 'art/class-models/arc-relay/ledger.json');
const tierAuditPath = join(
  repository,
  'art/class-models/runtime-tiers/arc-relay/ktx2-selective-v1/audit.json',
);
const check = process.argv.includes('--check');
const audit = JSON.parse(readFileSync(auditPath, 'utf8'));
const tierAudit = JSON.parse(readFileSync(tierAuditPath, 'utf8'));
const entries = [audit.pilot, ...audit.generated].sort((left, right) =>
  left.lookId.localeCompare(right.lookId),
);

if (entries.length !== 16 || new Set(entries.map((entry) => entry.lookId)).size !== 16)
  throw new Error('The Meshy fleet audit must contain exactly sixteen unique looks.');

const tierLooks = new Map(tierAudit.looks.map((look) => [look.id, look]));
const totals = {
  bytes: 0,
  triangles: 0,
  materials: 0,
  textures: 0,
  geometryGpuBytes: 0,
  textureGpuBytesCompressedTarget: 0,
  textureGpuBytesRgba8Fallback: 0,
  modelGpuBytesCompressedTarget: 0,
  modelGpuBytesRgba8Fallback: 0,
};
const looks = [];

for (const entry of entries) {
  const tier = tierLooks.get(entry.lookId);
  if (!tier) throw new Error(`${entry.lookId}: selective KTX2 tier entry is missing.`);
  if (
    tier.approvedCandidate.file !== entry.candidate.file ||
    tier.approvedCandidate.bytes !== entry.candidate.bytes ||
    tier.approvedCandidate.sha256 !== entry.candidate.sha256
  )
    throw new Error(`${entry.lookId}: texture tier is not derived from visual approval.`);
  const candidatePath = join(repository, tier.runtime.file);
  const runtimeDirectory = join(repository, 'web/src/assets/class-looks', entry.lookId);
  const runtimePath = join(runtimeDirectory, 'model.glb');
  const manifestPath = join(runtimeDirectory, 'model3d.json');
  const candidate = readFileSync(candidatePath);
  const hash = sha256(candidate);
  if (candidate.length !== tier.runtime.bytes || hash !== tier.runtime.sha256)
    throw new Error(`${entry.lookId}: audited runtime tier bytes or SHA-256 changed.`);

  const document = readGlb(candidate, entry.lookId);
  const normalization = document.nodes?.[document.scenes?.[0]?.nodes?.[0]]?.extras
    ?.nilbotsProviderNormalization;
  const triangles = triangleCount(document);
  const materials = document.materials?.length ?? 0;
  const textures = document.textures?.length ?? 0;
  const memory = inspectModelMemory(candidate, entry.lookId);
  if (document.cameras || document.skins || document.animations)
    throw new Error(`${entry.lookId}: provider model contains a camera, skin, or animation.`);
  if (triangles !== entry.candidate.triangles || materials !== entry.candidate.materials ||
      textures !== entry.candidate.textures)
    throw new Error(`${entry.lookId}: provider model ledger no longer matches the audit.`);
  if (materials !== 1 || textures !== 4)
    throw new Error(`${entry.lookId}: approved Meshy contract is one material/four textures.`);
  if (
    !(document.extensionsRequired ?? []).includes('KHR_texture_basisu') ||
    (document.images ?? []).some((image) => image.mimeType !== 'image/ktx2') ||
    geometryFingerprint(candidate, entry.lookId) !== tier.geometrySha256
  )
    throw new Error(`${entry.lookId}: audited KTX2 or approved geometry contract drifted.`);
  if ((document.materials ?? []).some((material) => material.extras?.nilbotsRole === 'team-accent'))
    throw new Error(`${entry.lookId}: approved Meshy model must not own team paint.`);
  if (
    normalization?.facing !== '+x' ||
    normalization?.up !== '+y' ||
    normalization?.floorY !== 0 ||
    normalization?.orientation !== entry.orientation ||
    (normalization?.facingYawDegrees ?? 0) !== (entry.facingYawDegrees ?? 0) ||
    ((entry.facingYawDegrees ?? 0) !== 0 && normalization?.facingCorrectionVersion !== 1) ||
    normalization?.targetPlanformSpan !== audit.reviewContract.targetPlanformSpan
  )
    throw new Error(`${entry.lookId}: provider normalization no longer matches the audit.`);

  const previous = JSON.parse(readFileSync(manifestPath, 'utf8'));
  const manifest = {
    version: 1,
    id: entry.lookId,
    file: 'model.glb',
    kind: 'bot',
    part: 'whole',
    facing: '+x',
    up: '+y',
    source: {
      generator: 'scripts/class-models/promote-meshy-arc-fleet.mjs',
      recipe: relative(repository, tierAuditPath),
      artifact: tier.runtime.file,
      sourceSha256: hash,
      approvedArtifact: entry.candidate.file,
      approvedSha256: entry.candidate.sha256,
      textureTier: tierAudit.id,
      provider: audit.provider,
      model: audit.model,
      endpoint: audit.endpoint,
      modelType: audit.modelType,
      taskId: entry.taskId,
      orientation: entry.orientation,
      ...(entry.facingYawDegrees ? { facingYawDegrees: entry.facingYawDegrees } : {}),
    },
    ...(previous.motion ? { motion: previous.motion } : {}),
    ...(previous.signature ? { signature: previous.signature } : {}),
    ledger: {
      bytes: candidate.length,
      sha256: hash,
      triangles,
      materials,
      textureCount: textures,
      geometrySha256: tier.geometrySha256,
      geometryGpuBytes: memory.geometryGpuBytes,
      textureGpuBytesCompressedTarget: memory.textureGpuBytesCompressedTarget,
      textureGpuBytesRgba8Fallback: memory.textureGpuBytesRgba8Fallback,
      modelGpuBytesCompressedTarget: memory.modelGpuBytesCompressedTarget,
      modelGpuBytesRgba8Fallback: memory.modelGpuBytesRgba8Fallback,
      textureTier: tierAudit.id,
    },
  };

  if (check) {
    assertBytes(runtimePath, candidate);
    assertText(manifestPath, json(manifest));
  } else {
    copyFileSync(candidatePath, runtimePath);
    writeFileSync(manifestPath, json(manifest));
  }

  totals.bytes += candidate.length;
  totals.triangles += triangles;
  totals.materials += materials;
  totals.textures += textures;
  totals.geometryGpuBytes += memory.geometryGpuBytes;
  totals.textureGpuBytesCompressedTarget += memory.textureGpuBytesCompressedTarget;
  totals.textureGpuBytesRgba8Fallback += memory.textureGpuBytesRgba8Fallback;
  totals.modelGpuBytesCompressedTarget += memory.modelGpuBytesCompressedTarget;
  totals.modelGpuBytesRgba8Fallback += memory.modelGpuBytesRgba8Fallback;
  looks.push({
    id: entry.lookId,
    taskId: entry.taskId,
    artifact: tier.runtime.file,
    approvedArtifact: entry.candidate.file,
    orientation: entry.orientation,
    facingYawDegrees: entry.facingYawDegrees ?? 0,
    bytes: candidate.length,
    sha256: hash,
    triangles,
    materials,
    textures,
    geometrySha256: tier.geometrySha256,
    memory,
    targetPlanformSpan: normalization.targetPlanformSpan,
    floorY: normalization.floorY,
  });
}

const ledger = {
  version: 3,
  generator: 'scripts/class-models/promote-meshy-arc-fleet.mjs',
  sourceAudit: relative(repository, auditPath),
  runtimeTier: relative(repository, tierAuditPath),
  textureTier: tierAudit.id,
  provider: audit.provider,
  endpoint: audit.endpoint,
  model: audit.model,
  modelType: audit.modelType,
  meshContract: audit.reviewContract.meshContract,
  modelOwnedTeamGlow: audit.reviewContract.modelOwnedTeamGlow,
  budgetBytesPerLook: 1048576,
  budgets: tierAudit.budgets,
  totals,
  looks,
};

for (const [actual, expected, label] of [
  [totals.bytes, tierAudit.totals.runtimeTransferBytes, 'transfer bytes'],
  [totals.geometryGpuBytes, tierAudit.totals.geometryGpuBytes, 'geometry GPU bytes'],
  [
    totals.textureGpuBytesCompressedTarget,
    tierAudit.totals.textureGpuBytesCompressedTarget,
    'compressed texture GPU bytes',
  ],
  [
    totals.textureGpuBytesRgba8Fallback,
    tierAudit.totals.textureGpuBytesRgba8Fallback,
    'fallback texture GPU bytes',
  ],
])
  if (actual !== expected) throw new Error(`Promoted fleet ${label} drifted from tier audit.`);

if (check) assertText(ledgerPath, json(ledger));
else writeFileSync(ledgerPath, json(ledger));

console.log(`${check ? 'Verified' : 'Promoted'} ${entries.length} Meshy Arc Relay models (${totals.bytes} bytes).`);

function readGlb(bytes, id) {
  if (bytes.toString('ascii', 0, 4) !== 'glTF' || bytes.readUInt32LE(4) !== 2 ||
      bytes.readUInt32LE(8) !== bytes.length || bytes.readUInt32LE(16) !== 0x4e4f534a)
    throw new Error(`${id}: invalid GLB header.`);
  const jsonLength = bytes.readUInt32LE(12);
  return JSON.parse(bytes.subarray(20, 20 + jsonLength).toString('utf8').trim());
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
