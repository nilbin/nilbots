#!/usr/bin/env node

import { createHash } from 'node:crypto';
import { spawnSync } from 'node:child_process';
import {
  copyFileSync,
  cpSync,
  existsSync,
  mkdirSync,
  mkdtempSync,
  readFileSync,
  readdirSync,
  rmSync,
  writeFileSync,
} from 'node:fs';
import { tmpdir } from 'node:os';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const repository = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..', '..');
const tierAuditPath = path.join(
  repository,
  'art/class-models/runtime-tiers/arc-relay/meshy7-longshot-railback-review-v1/audit.json',
);
const replayRecordPath = path.join(
  repository,
  'sandbox/longshot-railback-v1/game-review/replay-generation.json',
);
const evidenceDirectory = path.join(
  repository,
  'art/reviews/arc-relay-longshot-railback-v1/gameplay-review',
);
const output = path.join(repository, 'sandbox/longshot-railback-v1/game-review');
const interactive = path.join(output, 'site');
const backup = mkdtempSync(path.join(tmpdir(), 'nilbots-railback-game-review-'));
const tier = JSON.parse(readFileSync(tierAuditPath, 'utf8'));
const replayRecord = JSON.parse(readFileSync(replayRecordPath, 'utf8'));
const lean = tier.profiles?.find((profile) => profile.id === 'lean');
if (!lean || tier.status !== 'owner-review')
  throw new Error('Expected the pinned Railback lean owner-review tier.');
if (
  replayRecord.match?.longshotsByTeam?.length !== 2 ||
  replayRecord.match.longshotsByTeam.some((entry) => entry.count < 1)
)
  throw new Error('Review replay must contain at least one Longshot on both teams.');

const runtimeModel = path.join(repository, 'web/src/assets/class-looks/arc-longshot/model.glb');
const runtimeManifest = path.join(
  repository,
  'web/src/assets/class-looks/arc-longshot/model3d.json',
);
const candidateModel = path.join(repository, lean.artifact.file);
const backupModel = path.join(backup, 'arc-longshot.glb');
const backupManifest = path.join(backup, 'arc-longshot.model3d.json');
const before = readFileSync(runtimeModel);
const beforeManifest = readFileSync(runtimeManifest);
const candidate = readFileSync(candidateModel);
if (sha256(candidate) !== lean.artifact.sha256)
  throw new Error('Railback lean artifact drifted from its tier audit.');

const priorManifest = JSON.parse(beforeManifest.toString('utf8'));
const stagedManifest = {
  version: 1,
  id: 'arc-longshot',
  file: 'model.glb',
  kind: 'bot',
  part: 'whole',
  facing: '+x',
  up: '+y',
  source: {
    generator: 'scripts/class-models/build-railback-game-review.mjs',
    recipe: path.relative(repository, tierAuditPath),
    artifact: lean.artifact.file,
    sourceSha256: lean.artifact.sha256,
    provider: tier.source.provider,
    model: tier.source.providerModel,
    endpoint: tier.source.textureDonor.endpoint,
    modelType: 'original-uv-pbr-retexture',
    taskId: tier.source.textureDonor.taskId,
    geometryTaskId: tier.source.geometry.taskId,
    retextureTaskId: tier.source.textureDonor.taskId,
    orientation: tier.normalization.orientation,
    sourceFacing: tier.normalization.sourceFacing,
    facingYawDegrees: tier.normalization.facingYawDegrees,
    heightScale: tier.normalization.heightScale,
    originalUV: true,
    matteRoughnessFloor: lean.settings.matteRoughnessFloor,
  },
  ...(priorManifest.motion ? { motion: priorManifest.motion } : {}),
  ...(priorManifest.signature ? { signature: priorManifest.signature } : {}),
  ledger: {
    bytes: lean.artifact.bytes,
    sha256: lean.artifact.sha256,
    triangles: lean.triangles,
    materials: lean.materials,
    textureCount: lean.textures,
    geometrySha256: lean.geometrySha256,
    geometryGpuBytes: lean.memory.geometryGpuBytes,
    textureGpuBytesCompressedTarget: lean.memory.textureGpuBytesCompressedTarget,
    textureGpuBytesRgba8Fallback: lean.memory.textureGpuBytesRgba8Fallback,
    modelGpuBytesCompressedTarget: lean.memory.modelGpuBytesCompressedTarget,
    modelGpuBytesRgba8Fallback: lean.memory.modelGpuBytesRgba8Fallback,
    textureTier: 'meshy7-longshot-railback-review-v1-512-webp',
  },
};

try {
  copyFileSync(runtimeModel, backupModel);
  copyFileSync(runtimeManifest, backupManifest);
  copyFileSync(candidateModel, runtimeModel);
  writeFileSync(runtimeManifest, `${JSON.stringify(stagedManifest, null, 2)}\n`);
  run('npm', ['run', 'build:review'], path.join(repository, 'web'));
} finally {
  if (existsSync(backupModel)) copyFileSync(backupModel, runtimeModel);
  if (existsSync(backupManifest)) copyFileSync(backupManifest, runtimeManifest);
  rmSync(backup, { recursive: true, force: true });
}

const restored = readFileSync(runtimeModel);
const restoredManifest = readFileSync(runtimeManifest);
if (!restored.equals(before) || !restoredManifest.equals(beforeManifest))
  throw new Error('Approved runtime Longshot was not restored byte-for-byte.');

rmSync(interactive, { recursive: true, force: true });
mkdirSync(output, { recursive: true });
cpSync(path.join(repository, 'web/dist-review'), interactive, { recursive: true });
const galleryPath = path.join(output, 'replay-gallery-sources.json');
writeFileSync(
  galleryPath,
  `${JSON.stringify({
    schemaVersion: 1,
    outputDirectory: path.relative(repository, interactive),
    archiveDirectory: path.relative(repository, path.join(output, 'replay-gallery-archive')),
    entries: [
      {
        id: replayRecord.focusedStage.id,
        displayBots: ['Railback · Rail Line focus', '4 real casts · current renderer'],
        broadcastOnly: replayRecord.focusedStage.replay,
      },
      {
        id: replayRecord.match.id,
        displayBots: ['Railback teal · Hunter', 'Railback orange · Hunter'],
        broadcastOnly: replayRecord.match.broadcast,
      },
    ],
  }, null, 2)}\n`,
);
run(
  process.execPath,
  [
    'scripts/class-models/install-local-replay-gallery.mjs',
    path.relative(repository, galleryPath),
  ],
  repository,
);

const emitted = findGlbs(path.join(interactive, 'assets'));
const emittedCandidate = emitted.find((asset) => asset.sha256 === lean.artifact.sha256) ?? null;
if (!emittedCandidate) throw new Error('Railback candidate is absent from review build output.');

const buildRecord = {
  schemaVersion: 1,
  purpose:
    'Reversible current-build WebGL review of Railback Longshot at normal desktop and phone gameplay scale, on both teams and during Rail Line',
  sourceCommit: git('rev-parse', 'HEAD'),
  runtimeAssetsPromoted: { longshot: false },
  tierAudit: path.relative(repository, tierAuditPath),
  selectedProfile: lean.id,
  output: path.relative(repository, interactive),
  replayRecord: path.relative(repository, replayRecordPath),
  candidate: {
    ...lean.artifact,
    triangles: lean.triangles,
    textureSize: lean.settings.textureSize,
    liveMaximumSpan: tier.normalization.liveMaximumSpan,
    liveTransverseSpan: tier.normalization.liveTransverseSpan,
    liveHeight: tier.normalization.liveHeight,
  },
  emittedCandidate,
  runtimeSha256Before: sha256(before),
  runtimeSha256After: sha256(restored),
  runtimeManifestSha256Before: sha256(beforeManifest),
  runtimeManifestSha256After: sha256(restoredManifest),
  approvedRuntimeRestoredExactly: restored.equals(before),
  approvedRuntimeManifestRestoredExactly: restoredManifest.equals(beforeManifest),
};
const serializedBuildRecord = `${JSON.stringify(buildRecord, null, 2)}\n`;
writeFileSync(path.join(output, 'build-record.json'), serializedBuildRecord);
mkdirSync(evidenceDirectory, { recursive: true });
writeFileSync(path.join(evidenceDirectory, 'build-record.json'), serializedBuildRecord);
console.log(
  `Built Railback review at ${path.relative(repository, interactive)}; ` +
    'approved runtime Longshot restored byte-for-byte.',
);

function findGlbs(directory) {
  return readdirSync(directory)
    .filter((file) => file.endsWith('.glb'))
    .map((file) => {
      const bytes = readFileSync(path.join(directory, file));
      return { file: `assets/${file}`, bytes: bytes.length, sha256: sha256(bytes) };
    });
}

function run(command, arguments_, cwd) {
  const result = spawnSync(command, arguments_, { cwd, encoding: 'utf8', stdio: 'inherit' });
  if (result.error) throw result.error;
  if (result.status !== 0)
    throw new Error(`${command} failed with exit code ${result.status}.`);
}

function git(...arguments_) {
  const result = spawnSync('git', arguments_, { cwd: repository, encoding: 'utf8' });
  if (result.error) throw result.error;
  if (result.status !== 0) throw new Error(result.stderr || 'git command failed.');
  return result.stdout.trim();
}

function sha256(value) {
  return createHash('sha256').update(value).digest('hex');
}
