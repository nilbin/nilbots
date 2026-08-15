#!/usr/bin/env node

/** Promote the audited lean whole-rank diadem set into the WebGL-only asset tree. */

import { createHash } from 'node:crypto';
import {
  copyFileSync,
  existsSync,
  mkdirSync,
  readFileSync,
  rmSync,
  writeFileSync,
} from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const repository = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..', '..');
const check = process.argv.includes('--check');
const tierRoot = path.join(
  repository,
  'art/class-models/runtime-tiers/arc-relay/meshy7-veterancy-split-diadem-whole-v1',
);
const auditPath = path.join(tierRoot, 'audit.json');
const assetRoot = path.join(repository, 'web/src/assets/ornament-models');
const oldAsset = path.join(assetRoot, 'arc-veterancy-split-feather');
const audit = JSON.parse(readFileSync(auditPath, 'utf8'));
if (audit.status !== 'runtime-review' || audit.ranks?.length !== 5)
  throw new Error('Whole-diadem runtime audit is incomplete.');

const promotions = audit.ranks.map((rank) => promoteRank(rank));
const record = {
  schemaVersion: 1,
  status: 'runtime-promoted-for-gameplay-review',
  generator: 'scripts/class-models/promote-veterancy-whole-diadem-v1.mjs',
  supersedes: 'arc-veterancy-split-feather renderer-composed rank geometry',
  audit: path.relative(repository, auditPath),
  assets: promotions,
};
const recordPath = path.join(tierRoot, 'promotion.json');
if (check) {
  const recorded = JSON.parse(readFileSync(recordPath, 'utf8'));
  if (JSON.stringify(recorded, null, 2) !== JSON.stringify(record, null, 2))
    throw new Error('Whole-diadem promotion record drifted.');
  if (existsSync(oldAsset)) throw new Error('Superseded runtime feather asset still exists.');
  console.log('Verified five promoted whole-diadem runtime assets.');
} else {
  // This exact generated runtime directory is superseded. Provider masters and review
  // tiers remain retained; only the eager WebGL bundle entry is removed.
  rmSync(oldAsset, { recursive: true, force: true });
  writeFileSync(recordPath, `${JSON.stringify(record, null, 2)}\n`);
  console.log('Promoted five whole-diadem runtime assets and removed the superseded feather bundle.');
}

function promoteRank(rank) {
  const profile = rank.profiles.find((entry) => entry.id === 'lean-256');
  if (!profile) throw new Error(`Rank ${rank.rank} has no lean-256 profile.`);
  const id = `arc-veterancy-diadem-rank-${rank.rank}`;
  const directory = path.join(assetRoot, id);
  const source = path.join(repository, profile.artifact.file);
  const destination = path.join(directory, 'model.glb');
  const manifestPath = path.join(directory, 'model3d.json');
  const sourceBytes = readFileSync(source);
  if (sha256(sourceBytes) !== profile.artifact.sha256)
    throw new Error(`Rank ${rank.rank} lean source SHA-256 drifted.`);
  const manifest = {
    version: 1,
    id,
    file: 'model.glb',
    kind: 'ornament',
    part: 'whole',
    facing: '+x',
    up: '+y',
    source: {
      generator: 'scripts/class-models/promote-veterancy-whole-diadem-v1.mjs',
      recipe: path.relative(repository, auditPath),
      artifact: profile.artifact.file,
      sourceSha256: profile.artifact.sha256,
      approvedArtifact: profile.artifact.file,
      approvedSha256: profile.artifact.sha256,
      textureTier: 'meshy7-veterancy-split-diadem-whole-v1-lean-256-webp',
      provider: 'Meshy',
      model: 'meshy-7',
      endpoint: 'multi-image-to-3d',
      modelType: 'complete-bilateral-rank-ornament',
      taskId: rank.source.taskId,
      orientation: 'identity',
      facingYawDegrees: rank.normalization.facingYawDegrees,
      rank: rank.rank,
      targetPlanformSpan: rank.normalization.targetPlanformSpan,
      targetCentreGap: rank.normalization.targetCentreGap,
      normalizedHeight: rank.normalization.normalizedHeight,
      splitGapRatio: audit.contract.targetGapRatio,
      matteRoughnessFloor: profile.settings.matteRoughnessFloor,
    },
    ledger: {
      bytes: profile.artifact.bytes,
      sha256: profile.artifact.sha256,
      triangles: profile.triangles,
      materials: profile.materials,
      textureCount: profile.textures,
      geometrySha256: profile.geometrySha256,
      geometryGpuBytes: profile.memory.geometryGpuBytes,
      textureGpuBytesCompressedTarget: profile.memory.textureGpuBytesCompressedTarget,
      textureGpuBytesRgba8Fallback: profile.memory.textureGpuBytesRgba8Fallback,
      modelGpuBytesCompressedTarget: profile.memory.modelGpuBytesCompressedTarget,
      modelGpuBytesRgba8Fallback: profile.memory.modelGpuBytesRgba8Fallback,
      textureTier: 'meshy7-veterancy-split-diadem-whole-v1-lean-256-webp',
    },
  };

  if (check) {
    const liveBytes = readFileSync(destination);
    const liveManifest = JSON.parse(readFileSync(manifestPath, 'utf8'));
    if (
      !liveBytes.equals(sourceBytes) ||
      JSON.stringify(liveManifest, null, 2) !== JSON.stringify(manifest, null, 2)
    )
      throw new Error(`Rank ${rank.rank} promoted runtime asset drifted.`);
  } else {
    mkdirSync(directory, { recursive: true });
    copyFileSync(source, destination);
    writeFileSync(manifestPath, `${JSON.stringify(manifest, null, 2)}\n`);
  }
  return {
    rank: rank.rank,
    id,
    model: path.relative(repository, destination),
    manifest: path.relative(repository, manifestPath),
    bytes: sourceBytes.length,
    sha256: sha256(sourceBytes),
    triangles: profile.triangles,
  };
}

function sha256(value) {
  return createHash('sha256').update(value).digest('hex');
}
