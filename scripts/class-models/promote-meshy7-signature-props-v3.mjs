#!/usr/bin/env node

import { createHash } from 'node:crypto';
import { copyFileSync, readFileSync, writeFileSync } from 'node:fs';
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
  'art/class-models/runtime-tiers/arc-relay/meshy7-signature-props-review-v3/audit.json',
);
const ledgerPath = join(repository, 'art/signature-models/arc-relay/ledger.json');
const generator = 'scripts/class-models/promote-meshy7-signature-props-v3.mjs';
const textureTier = 'meshy7-signature-props-review-v3-webp';
const check = process.argv.includes('--check');
const audit = JSON.parse(readFileSync(reviewAuditPath, 'utf8'));
const signatures = [...audit.classes].sort((left, right) =>
  left.signatureId.localeCompare(right.signatureId),
);

if (audit.id !== 'meshy7-signature-props-review-v3' || audit.status !== 'owner-review')
  throw new Error('The promoted props must come from the pinned Meshy 7 owner-review tier.');
if (
  signatures.length !== 2 ||
  signatures.map((entry) => entry.signatureId).join(',') !== 'sentinel-seed,trip-node'
)
  throw new Error('The Meshy 7 review audit must contain Trip Node and Sentinel Seed.');

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
const models = [];

for (const entry of signatures) {
  const id = `arc-${entry.signatureId}`;
  const sourcePath = join(repository, entry.artifact.file);
  const runtimeDirectory = join(repository, 'web/src/assets/signature-models', id);
  const runtimePath = join(runtimeDirectory, 'model.glb');
  const manifestPath = join(runtimeDirectory, 'model3d.json');
  const candidate = readFileSync(sourcePath);
  const hash = sha256(candidate);

  if (candidate.length !== entry.artifact.bytes || hash !== entry.artifact.sha256)
    throw new Error(`${id}: approved review bytes or SHA-256 drifted.`);
  if (candidate.length > entry.budgets.transferBytes)
    throw new Error(`${id}: approved artifact exceeds its transfer budget.`);

  const { document } = readGlb(candidate, id);
  const normalization = document.nodes?.find(
    (node) => node.extras?.nilbotsProviderNormalization,
  )?.extras?.nilbotsProviderNormalization;
  const triangles = triangleCount(document);
  const materials = document.materials?.length ?? 0;
  const textures = document.textures?.length ?? 0;
  const primitives = (document.meshes ?? []).flatMap((mesh) => mesh.primitives ?? []);
  const memory = inspectModelMemory(candidate, id);
  const expectedParts = entry.signatureId === 'sentinel-seed' ? 2 : 1;
  const expectedTextures = entry.signatureId === 'sentinel-seed' ? 4 : 3;
  const expectedSlots = entry.signatureId === 'sentinel-seed'
    ? 'baseColorTexture,emissiveTexture,metallicRoughnessTexture,normalTexture'
    : 'baseColorTexture,metallicRoughnessTexture,normalTexture';

  if (document.cameras || document.skins || document.animations)
    throw new Error(`${id}: runtime provider model contains a camera, skin, or animation.`);
  if ((document.nodes ?? []).some((node) => node.camera !== undefined || node.skin !== undefined))
    throw new Error(`${id}: runtime provider node contains a camera or skin.`);
  if (
    (document.meshes?.length ?? 0) !== expectedParts ||
    primitives.length !== expectedParts ||
    materials !== 1
  )
    throw new Error(`${id}: approved mesh, primitive, or material count drifted.`);
  if (textures !== expectedTextures || (document.images?.length ?? 0) !== expectedTextures)
    throw new Error(`${id}: embedded PBR texture count drifted.`);
  if ((document.images ?? []).some((image) => image.mimeType !== 'image/webp'))
    throw new Error(`${id}: runtime textures must all be embedded WebP.`);
  if (
    !(document.extensionsRequired ?? []).includes('EXT_texture_webp') ||
    !(document.extensionsRequired ?? []).includes('KHR_mesh_quantization') ||
    (document.textures ?? []).some(
      (texture) => texture.extensions?.EXT_texture_webp?.source === undefined,
    )
  )
    throw new Error(`${id}: required WebP or quantized-geometry extension drifted.`);
  if ((document.materials ?? []).some((material) => material.extras?.nilbotsRole === 'team-accent'))
    throw new Error(`${id}: model must not own team paint.`);
  if (
    normalization?.facing !== '+x' ||
    normalization?.up !== '+y' ||
    normalization?.floorY !== 0 ||
    normalization?.orientation !== 'identity' ||
    normalization?.targetPlanformSpan !== audit.normalization.targetPlanformSpan ||
    (normalization?.facingYawDegrees ?? 0) !== entry.facing.yawDegrees
  )
    throw new Error(`${id}: top-down +X-facing normalization drifted.`);
  if (triangles !== entry.triangles || triangles > entry.budgets.triangles)
    throw new Error(`${id}: triangle ledger or budget drifted.`);
  if (geometryFingerprint(candidate, id) !== entry.geometrySha256)
    throw new Error(`${id}: audited geometry fingerprint drifted.`);
  if (
    memory.textures.length !== expectedTextures ||
    memory.textures.some(
      (texture) =>
        texture.mimeType !== 'image/webp' ||
        texture.width !== 512 ||
        texture.height !== 512,
    ) ||
    memory.textures.flatMap((texture) => texture.slots).sort().join(',') !== expectedSlots
  )
    throw new Error(`${id}: embedded 512 px PBR texture slots drifted.`);
  for (const key of [
    'transferBytes',
    'geometryGpuBytes',
    'textureGpuBytesCompressedTarget',
    'textureGpuBytesRgba8Fallback',
    'modelGpuBytesCompressedTarget',
    'modelGpuBytesRgba8Fallback',
  ])
    if (memory[key] !== entry.memory[key])
      throw new Error(`${id}: audited ${key} drifted.`);

  if (entry.signatureId === 'trip-node') {
    const transplant = document.asset?.extras?.nilbotsRetextureTransplant;
    const matte = document.asset?.extras?.nilbotsMatteRoughness;
    if (
      transplant?.preservedOriginalUvs !== true ||
      transplant?.donorImages !== 3 ||
      matte?.floor !== 0.72 ||
      matte?.minimumAfter < 0.72 ||
      matte?.baseColorChanged !== false ||
      matte?.metallicChanged !== false ||
      matte?.normalChanged !== false
    )
      throw new Error(`${id}: approved original-UV matte retexture contract drifted.`);
  } else {
    const split = document.asset?.extras?.nilbotsSentinelYawSplit;
    const yawHead = document.nodes?.find((node) => node.name === entry.nodes.yawPivot);
    const yawMesh = document.nodes?.find((node) => node.name === entry.nodes.yawMesh);
    const stationary = document.nodes?.find((node) => node.name === entry.nodes.stationary);
    if (
      JSON.stringify(split) !== JSON.stringify(entry.split) ||
      split?.partitionUnionVerified !== true ||
      split?.sourceTriangles !== split?.stationaryTriangles + split?.yawHeadTriangles ||
      stationary?.mesh === undefined ||
      yawHead?.extras?.yawAxis !== '+y' ||
      JSON.stringify(yawHead?.extras?.pivot) !== '[0,0,0]' ||
      !yawHead?.children?.some((index) => document.nodes?.[index] === yawMesh) ||
      yawMesh?.mesh === undefined
    )
      throw new Error(`${id}: approved stationary-base/yaw-head split contract drifted.`);
  }

  const modelType = entry.signatureId === 'trip-node'
    ? 'original-uv-pbr-retexture'
    : 'component-split-pbr';
  const manifest = {
    version: 1,
    id,
    file: 'model.glb',
    kind: 'signature',
    part: 'whole',
    facing: '+x',
    up: '+y',
    signature: entry.signatureId,
    source: {
      generator,
      recipe: relative(repository, reviewAuditPath),
      artifact: entry.artifact.file,
      sourceSha256: hash,
      approvedArtifact: entry.artifact.file,
      approvedSha256: hash,
      textureTier,
      provider: 'Meshy',
      model: 'meshy-7',
      endpoint: entry.endpoint,
      modelType,
      taskId: entry.providerTaskId,
      orientation: 'identity',
      ...(entry.facing.yawDegrees
        ? { facingYawDegrees: entry.facing.yawDegrees }
        : {}),
    },
    ledger: {
      bytes: candidate.length,
      sha256: hash,
      triangles,
      materials,
      textureCount: textures,
      geometrySha256: entry.geometrySha256,
      geometryGpuBytes: memory.geometryGpuBytes,
      textureGpuBytesCompressedTarget: memory.textureGpuBytesCompressedTarget,
      textureGpuBytesRgba8Fallback: memory.textureGpuBytesRgba8Fallback,
      modelGpuBytesCompressedTarget: memory.modelGpuBytesCompressedTarget,
      modelGpuBytesRgba8Fallback: memory.modelGpuBytesRgba8Fallback,
      textureTier,
    },
  };

  if (check) {
    assertBytes(runtimePath, candidate);
    assertText(manifestPath, json(manifest));
  } else {
    copyFileSync(sourcePath, runtimePath);
    writeFileSync(manifestPath, json(manifest));
  }

  for (const key of Object.keys(totals)) {
    if (key === 'bytes') totals[key] += candidate.length;
    else if (key === 'triangles') totals[key] += triangles;
    else if (key === 'materials') totals[key] += materials;
    else if (key === 'textures') totals[key] += textures;
    else totals[key] += memory[key];
  }
  models.push({
    id,
    signatureId: entry.signatureId,
    taskId: entry.providerTaskId,
    endpoint: entry.endpoint,
    modelType,
    artifact: entry.artifact.file,
    source: entry.source,
    facing: entry.facing,
    bytes: candidate.length,
    sha256: hash,
    triangles,
    materials,
    textures,
    geometrySha256: entry.geometrySha256,
    matteRoughnessFloor: entry.matteRoughness?.floor ?? null,
    ...(entry.split ? { split: entry.split, nodes: entry.nodes } : {}),
    memory,
    budgets: entry.budgets,
    targetPlanformSpan: normalization.targetPlanformSpan,
    floorY: normalization.floorY,
  });
}

const ledger = {
  version: 1,
  status: 'owner-approved',
  approval:
    'Owner approved the larger Trip Node and split Sentinel Seed after review in the real renderer.',
  generator,
  runtimeTier: relative(repository, reviewAuditPath),
  textureTier,
  provider: 'Meshy',
  model: 'meshy-7',
  meshContract:
    'Trip is one matte original-UV mesh; Sentinel is a stationary base plus an independently yawing head.',
  modelOwnedTeamGlow: false,
  normalization: audit.normalization,
  runtimeScale: {
    'trip-node': 0.75,
    'sentinel-seed': 0.95,
  },
  budgets: audit.profile.budgets,
  totals,
  models,
};

for (const [actual, expected, label] of [
  [totals.bytes, audit.totals.transferBytes, 'transfer bytes'],
  [totals.triangles, audit.totals.triangles, 'triangles'],
  [
    totals.modelGpuBytesRgba8Fallback,
    audit.totals.modelGpuBytesRgba8Fallback,
    'fallback model GPU bytes',
  ],
])
  if (actual !== expected) throw new Error(`Promoted signature-prop ${label} drifted from audit.`);

if (check) assertText(ledgerPath, json(ledger));
else writeFileSync(ledgerPath, json(ledger));

console.log(
  `${check ? 'Verified' : 'Promoted'} ${signatures.length} Meshy 7 signature props ` +
    `(${totals.bytes} bytes).`,
);

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
