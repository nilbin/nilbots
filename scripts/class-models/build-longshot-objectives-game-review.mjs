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
  'art/class-models/runtime-tiers/arc-relay/longshot-objectives-review-v1/audit.json',
);
const tier = JSON.parse(readFileSync(tierAuditPath, 'utf8'));
const evidenceDirectory = path.join(
  repository,
  'art/reviews/arc-relay-longshot-objectives-meshy7-v1/combined-gameplay-review',
);
const output = path.join(repository, 'sandbox/longshot-objectives-meshy7-v1/combined-review');
const interactive = path.join(output, 'site');
const backup = mkdtempSync(path.join(tmpdir(), 'nilbots-longshot-objectives-review-'));
const longshot = tier.candidates.find((entry) => entry.id === 'longshot');
const surveyFlare = tier.candidates.find((entry) => entry.id === 'survey-flare');
const coreWell = tier.candidates.find((entry) => entry.id === 'core-well');
if (!longshot || !surveyFlare || !coreWell || tier.status !== 'owner-review')
  throw new Error('Expected the pinned Longshot/objectives owner-review tier.');

const runtimeLongshot = path.join(
  repository,
  'web/src/assets/class-looks/arc-longshot/model.glb',
);
const runtimeLongshotManifest = path.join(
  repository,
  'web/src/assets/class-looks/arc-longshot/model3d.json',
);
const candidateLongshot = path.join(repository, longshot.artifact.file);
const backupLongshot = path.join(backup, 'arc-longshot.glb');
const backupLongshotManifest = path.join(backup, 'arc-longshot.model3d.json');
const before = readFileSync(runtimeLongshot);
const beforeManifest = readFileSync(runtimeLongshotManifest);
const candidate = readFileSync(candidateLongshot);
if (sha256(candidate) !== longshot.artifact.sha256)
  throw new Error('Longshot review artifact drifted from its tier audit.');

const stagedDirectories = [
  {
    id: 'survey-flare',
    directory: path.join(repository, 'web/src/assets/signature-models/arc-survey-flare'),
    model: path.join(repository, surveyFlare.artifact.file),
    manifest: path.join(
      repository,
      'art/class-models/runtime-tiers/arc-relay/longshot-objectives-review-v1/' +
        'arc-survey-flare.model3d.json',
    ),
  },
  {
    id: 'core-well',
    directory: path.join(repository, 'web/src/assets/objective-models/arc-core-well'),
    model: path.join(repository, coreWell.artifact.file),
    manifest: path.join(
      repository,
      'art/class-models/runtime-tiers/arc-relay/longshot-objectives-review-v1/' +
        'arc-core-well.model3d.json',
    ),
  },
];
for (const entry of stagedDirectories) {
  if (existsSync(entry.directory))
    throw new Error(`Review staging refuses to overwrite runtime directory ${entry.directory}.`);
  const audited = tier.candidates.find((candidateEntry) => candidateEntry.id === entry.id);
  if (!audited || sha256(readFileSync(entry.model)) !== audited.artifact.sha256)
    throw new Error(`${entry.id} review artifact drifted from its tier audit.`);
}

const runtimeReactorDirectory = path.join(
  repository,
  'web/src/assets/objective-models/arc-team-reactor',
);
const runtimeReactorManifest = JSON.parse(
  readFileSync(path.join(runtimeReactorDirectory, 'model3d.json'), 'utf8'),
);
const runtimeReactor = readFileSync(path.join(runtimeReactorDirectory, 'model.glb'));
if (
  runtimeReactorManifest.source?.generator !==
    'scripts/class-models/promote-reactor-recess-v1.mjs' ||
  sha256(runtimeReactor) !== runtimeReactorManifest.ledger?.sha256
)
  throw new Error('Combined review requires the promoted low Reactor recess.');

const priorLongshotManifest = JSON.parse(beforeManifest.toString('utf8'));
const stagedLongshotManifest = {
  version: 1,
  id: 'arc-longshot',
  file: 'model.glb',
  kind: 'bot',
  part: 'whole',
  facing: '+x',
  up: '+y',
  source: {
    generator: 'scripts/class-models/build-longshot-objectives-game-review.mjs',
    recipe: path.relative(repository, tierAuditPath),
    artifact: longshot.artifact.file,
    sourceSha256: longshot.artifact.sha256,
    provider: 'Meshy',
    model: 'meshy-7',
    endpoint: longshot.endpoint,
    modelType: 'original-uv-pbr-retexture',
    taskId: longshot.taskId,
    orientation: 'identity',
    facingYawDegrees: longshot.facing.yawDegrees,
    originalUV: true,
    matteRoughnessFloor: longshot.matteRoughness.floor,
  },
  ...(priorLongshotManifest.motion ? { motion: priorLongshotManifest.motion } : {}),
  ...(priorLongshotManifest.signature ? { signature: priorLongshotManifest.signature } : {}),
  ledger: {
    bytes: longshot.artifact.bytes,
    sha256: longshot.artifact.sha256,
    triangles: longshot.triangles,
    materials: longshot.materials,
    textureCount: longshot.textures,
    geometrySha256: longshot.geometrySha256,
    geometryGpuBytes: longshot.memory.geometryGpuBytes,
    textureGpuBytesCompressedTarget: longshot.memory.textureGpuBytesCompressedTarget,
    textureGpuBytesRgba8Fallback: longshot.memory.textureGpuBytesRgba8Fallback,
    modelGpuBytesCompressedTarget: longshot.memory.modelGpuBytesCompressedTarget,
    modelGpuBytesRgba8Fallback: longshot.memory.modelGpuBytesRgba8Fallback,
    textureTier: 'longshot-objectives-review-v1-webp',
  },
};

try {
  copyFileSync(runtimeLongshot, backupLongshot);
  copyFileSync(runtimeLongshotManifest, backupLongshotManifest);
  copyFileSync(candidateLongshot, runtimeLongshot);
  writeFileSync(runtimeLongshotManifest, `${JSON.stringify(stagedLongshotManifest, null, 2)}\n`);
  for (const entry of stagedDirectories) {
    mkdirSync(entry.directory, { recursive: true });
    copyFileSync(entry.model, path.join(entry.directory, 'model.glb'));
    copyFileSync(entry.manifest, path.join(entry.directory, 'model3d.json'));
  }
  run('npm', ['run', 'build:review'], path.join(repository, 'web'));
} finally {
  if (existsSync(backupLongshot)) copyFileSync(backupLongshot, runtimeLongshot);
  if (existsSync(backupLongshotManifest))
    copyFileSync(backupLongshotManifest, runtimeLongshotManifest);
  for (const entry of stagedDirectories)
    rmSync(entry.directory, { recursive: true, force: true });
  rmSync(backup, { recursive: true, force: true });
}

const restored = readFileSync(runtimeLongshot);
const restoredManifest = readFileSync(runtimeLongshotManifest);
if (sha256(restored) !== sha256(before) || !restoredManifest.equals(beforeManifest))
  throw new Error('Approved runtime Longshot was not restored exactly.');

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
        id: 'lantern-survey-flare-focus',
        displayBots: ['Lantern · Survey Flare close review', '5 real casts · minimal context'],
        broadcastOnly:
          'sandbox/longshot-objectives-meshy7-v1/combined-review/matches/' +
          'lantern-survey-flare-focus/replay.json.gz',
      },
      {
        id: 'all-four-hunter-west',
        displayBots: ['All four · Longshot west', 'Lantern east · live objectives'],
        broadcastOnly:
          'sandbox/longshot-objectives-meshy7-v1/combined-review/matches/' +
          'all-four-hunter-west/broadcast.json.gz',
      },
      {
        id: 'all-four-hunter-east',
        displayBots: ['All four · Lantern west', 'Longshot east · live objectives'],
        broadcastOnly:
          'sandbox/longshot-objectives-meshy7-v1/combined-review/matches/' +
          'all-four-hunter-east/broadcast.json.gz',
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
const expected = [longshot, surveyFlare, coreWell].map((entry) => ({
  id: entry.id,
  sha256: entry.artifact.sha256,
  emitted: emitted.find((asset) => asset.sha256 === entry.artifact.sha256) ?? null,
}));
expected.push({
  id: 'team-reactor',
  sha256: sha256(runtimeReactor),
  emitted: emitted.find((asset) => asset.sha256 === sha256(runtimeReactor)) ?? null,
});
if (expected.some((entry) => !entry.emitted))
  throw new Error(
    `Review output is missing candidate GLBs: ${expected.filter((entry) => !entry.emitted).map((entry) => entry.id).join(', ')}`,
  );

const buildRecord = {
  schemaVersion: 1,
  purpose:
    'Current-build combined real-renderer review of Longshot, Survey Flare, Team Reactor, and Core Well, with a focused Lantern stage',
  sourceCommit: git('rev-parse', 'HEAD'),
  runtimeAssetsPromoted: {
    teamReactor: true,
    longshot: false,
    surveyFlare: false,
    coreWell: false,
  },
  tierAudit: path.relative(repository, tierAuditPath),
  output: path.relative(repository, interactive),
  longshotRuntimeSha256Before: sha256(before),
  longshotCandidateSha256: sha256(candidate),
  longshotRuntimeSha256After: sha256(restored),
  approvedRuntimeRestoredExactly: sha256(restored) === sha256(before),
  approvedRuntimeManifestRestoredExactly: restoredManifest.equals(beforeManifest),
  stagedRuntimeDirectoriesRemoved: stagedDirectories.every(
    (entry) => !existsSync(entry.directory),
  ),
  emittedCandidates: expected,
};
const serializedBuildRecord = `${JSON.stringify(buildRecord, null, 2)}\n`;
writeFileSync(path.join(output, 'build-record.json'), serializedBuildRecord);
mkdirSync(evidenceDirectory, { recursive: true });
writeFileSync(path.join(evidenceDirectory, 'build-record.json'), serializedBuildRecord);
console.log(
  `Built current combined Longshot/objectives review at ${path.relative(repository, interactive)}; ` +
    'approved Longshot restored byte-for-byte.',
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
  if (result.status !== 0) throw new Error(`${command} failed with exit code ${result.status}.`);
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
