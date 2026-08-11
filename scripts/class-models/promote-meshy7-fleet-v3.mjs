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
  'art/class-models/runtime-tiers/arc-relay/meshy7-fleet-review-v3/audit.json',
);
const providerPlanPath = join(
  repository,
  'art/class-models/provider-runs/meshy/arc-relay-meshy7-fleet-retexture-v3-multiview/plan.json',
);
const ledgerPath = join(repository, 'art/class-models/arc-relay/ledger.json');
const generator = 'scripts/class-models/promote-meshy7-fleet-v3.mjs';
const textureTier = 'meshy7-fleet-review-v3-webp';
const check = process.argv.includes('--check');
const audit = JSON.parse(readFileSync(reviewAuditPath, 'utf8'));
const plan = JSON.parse(readFileSync(providerPlanPath, 'utf8'));
const classes = [...audit.classes].sort((left, right) =>
  left.classId.localeCompare(right.classId),
);

if (audit.id !== 'meshy7-fleet-review-v3' || audit.status !== 'owner-review')
  throw new Error('The promoted fleet must come from the pinned Meshy 7 V3 owner-review tier.');
if (classes.length !== 16 || new Set(classes.map((entry) => entry.classId)).size !== 16)
  throw new Error('The Meshy 7 V3 review audit must contain sixteen unique classes.');
if (plan.provider !== 'Meshy' || plan.endpoint !== '/openapi/v1/retexture')
  throw new Error('The pinned provider plan is not a Meshy retexture run.');

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

for (const entry of classes) {
  const id = `arc-${entry.classId}`;
  const sourcePath = join(repository, entry.artifact.file);
  const runtimeDirectory = join(repository, 'web/src/assets/class-looks', id);
  const runtimePath = join(runtimeDirectory, 'model.glb');
  const manifestPath = join(runtimeDirectory, 'model3d.json');
  const candidate = readFileSync(sourcePath);
  const hash = sha256(candidate);

  if (candidate.length !== entry.artifact.bytes || hash !== entry.artifact.sha256)
    throw new Error(`${id}: V3 review bytes or SHA-256 drifted.`);
  if (candidate.length > entry.budgets.transferBytes)
    throw new Error(`${id}: V3 review artifact exceeds its approved transfer budget.`);

  const { document } = readGlb(candidate, id);
  const root = document.nodes?.[document.scenes?.[0]?.nodes?.[0]];
  const normalization = root?.extras?.nilbotsProviderNormalization;
  const triangles = triangleCount(document);
  const materials = document.materials?.length ?? 0;
  const textures = document.textures?.length ?? 0;
  const primitives = (document.meshes ?? []).flatMap((mesh) => mesh.primitives ?? []);
  const memory = inspectModelMemory(candidate, id);

  if (document.cameras || document.skins || document.animations)
    throw new Error(`${id}: runtime provider model contains a camera, skin, or animation.`);
  if ((document.nodes ?? []).some((node) => node.camera !== undefined || node.skin !== undefined))
    throw new Error(`${id}: runtime provider node contains a camera or skin.`);
  if ((document.meshes?.length ?? 0) !== 1 || primitives.length !== 1 || materials !== 1)
    throw new Error(`${id}: runtime contract is one mesh, primitive, and material.`);
  if (textures !== 3 || (document.images?.length ?? 0) !== 3)
    throw new Error(`${id}: runtime contract is three embedded PBR textures.`);
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
    root?.name !== 'chassis' ||
    normalization?.facing !== '+x' ||
    normalization?.up !== '+y' ||
    normalization?.floorY !== 0 ||
    normalization?.orientation !== 'identity' ||
    normalization?.targetPlanformSpan !== audit.normalization.targetPlanformSpan ||
    (normalization?.facingYawDegrees ?? 0) !== entry.facing.yawDegrees ||
    (entry.facing.yawDegrees !== 0 && normalization?.facingCorrectionVersion !== 1)
  )
    throw new Error(`${id}: top-down +X-facing normalization drifted.`);
  if (triangles !== entry.triangles || triangles > entry.budgets.triangles)
    throw new Error(`${id}: triangle ledger or approved triangle budget drifted.`);
  if (geometryFingerprint(candidate, id) !== entry.geometrySha256)
    throw new Error(`${id}: audited V3 geometry fingerprint drifted.`);
  if (
    memory.textures.length !== 3 ||
    memory.textures.some(
      (texture) =>
        texture.mimeType !== 'image/webp' ||
        texture.width !== 512 ||
        texture.height !== 512,
    ) ||
    memory.textures.flatMap((texture) => texture.slots).sort().join(',') !==
      'baseColorTexture,metallicRoughnessTexture,normalTexture'
  )
    throw new Error(`${id}: embedded 512 px PBR texture slots drifted.`);

  const previous = JSON.parse(readFileSync(manifestPath, 'utf8'));
  const manifest = {
    version: 1,
    id,
    file: 'model.glb',
    kind: 'bot',
    part: 'whole',
    facing: '+x',
    up: '+y',
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
      endpoint: 'retexture',
      modelType: 'original-uv-pbr-retexture',
      taskId: entry.taskId,
      orientation: 'identity',
      ...(entry.facing.yawDegrees
        ? { facingYawDegrees: entry.facing.yawDegrees }
        : {}),
      originalUV: true,
      ...(entry.matteRoughness
        ? { matteRoughnessFloor: entry.matteRoughness.floor }
        : {}),
    },
    ...(previous.motion ? { motion: previous.motion } : {}),
    ...(previous.signature ? { signature: previous.signature } : {}),
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
  looks.push({
    id,
    classId: entry.classId,
    taskId: entry.taskId,
    endpoint: entry.endpoint,
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
    memory,
    budgets: entry.budgets,
    targetPlanformSpan: normalization.targetPlanformSpan,
    floorY: normalization.floorY,
  });
}

const ledger = {
  version: 4,
  status: 'owner-approved',
  approval: 'Owner approved the Meshy 7 V3 fleet and requested replacement of the matching top-down east-facing web icons.',
  generator,
  sourceAudit: audit.sourceAudit,
  runtimeTier: relative(repository, reviewAuditPath),
  textureTier,
  provider: 'Meshy',
  endpoint: 'retexture',
  model: 'meshy-7',
  modelType: 'original-uv-pbr-retexture',
  originalUV: true,
  meshContract: 'one monolithic mesh, one material, and three embedded 512 px WebP PBR textures',
  modelOwnedTeamGlow: false,
  normalization: audit.normalization,
  finish: audit.contract.matteFinish,
  budgets: {
    default: audit.profile.budgets,
    classOverrides: audit.profile.classOverrides,
  },
  totals,
  looks,
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
  if (actual !== expected) throw new Error(`Promoted V3 fleet ${label} drifted from audit.`);

if (check) assertText(ledgerPath, json(ledger));
else writeFileSync(ledgerPath, json(ledger));

console.log(
  `${check ? 'Verified' : 'Promoted'} ${classes.length} Meshy 7 V3 Arc Relay models ` +
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
