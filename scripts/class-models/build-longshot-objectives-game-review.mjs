#!/usr/bin/env node

import { createHash } from 'node:crypto';
import { spawnSync } from 'node:child_process';
import {
  cpSync,
  mkdirSync,
  readFileSync,
  readdirSync,
  rmSync,
  writeFileSync,
} from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const repository = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..', '..');
const tierAuditPath = path.join(
  repository,
  'art/class-models/runtime-tiers/arc-relay/longshot-objectives-review-v1/audit.json',
);
const railbackAuditPath = path.join(
  repository,
  'art/class-models/runtime-tiers/arc-relay/' +
    'meshy7-longshot-railback-muted-blue-review-v3/audit.json',
);
const tier = JSON.parse(readFileSync(tierAuditPath, 'utf8'));
const railbackAudit = JSON.parse(readFileSync(railbackAuditPath, 'utf8'));
const railback = railbackAudit.profiles?.find((entry) => entry.id === 'lean');
const surveyFlare = tier.candidates.find((entry) => entry.id === 'survey-flare');
const coreWell = tier.candidates.find((entry) => entry.id === 'core-well');
if (
  tier.status !== 'owner-review' ||
  railbackAudit.status !== 'owner-review' ||
  !railback ||
  !surveyFlare ||
  !coreWell
)
  throw new Error('Expected the pinned owner-reviewed Railback, Flare, and Well tiers.');

const evidenceDirectory = path.join(
  repository,
  'art/reviews/arc-relay-longshot-objectives-meshy7-v1/combined-gameplay-review',
);
const output = path.join(repository, 'sandbox/longshot-objectives-meshy7-v1/combined-review');
const interactive = path.join(output, 'site');

run(process.execPath, ['scripts/class-models/promote-meshy7-fleet-v3.mjs', '--check'], repository);
run(
  process.execPath,
  ['scripts/class-models/promote-core-well-survey-flare-v1.mjs', '--check'],
  repository,
);
run(process.execPath, ['scripts/class-models/promote-reactor-recess-v1.mjs', '--check'], repository);

const reactorManifest = JSON.parse(
  readFileSync(
    path.join(repository, 'web/src/assets/objective-models/arc-team-reactor/model3d.json'),
    'utf8',
  ),
);
const runtimeAssets = [
  {
    id: 'longshot',
    runtime: 'web/src/assets/class-looks/arc-longshot/model.glb',
    artifact: railback.artifact.file,
    sha256: railback.artifact.sha256,
  },
  {
    id: 'survey-flare',
    runtime: 'web/src/assets/signature-models/arc-survey-flare/model.glb',
    artifact: surveyFlare.artifact.file,
    sha256: surveyFlare.artifact.sha256,
  },
  {
    id: 'team-reactor',
    runtime: 'web/src/assets/objective-models/arc-team-reactor/model.glb',
    artifact: reactorManifest.source.approvedArtifact,
    sha256: reactorManifest.source.approvedSha256,
  },
  {
    id: 'core-well',
    runtime: 'web/src/assets/objective-models/arc-core-well/model.glb',
    artifact: coreWell.artifact.file,
    sha256: coreWell.artifact.sha256,
  },
];
for (const asset of runtimeAssets) {
  const runtime = readFileSync(path.join(repository, asset.runtime));
  const approved = readFileSync(path.join(repository, asset.artifact));
  if (!runtime.equals(approved) || sha256(runtime) !== asset.sha256)
    throw new Error(`${asset.id}: runtime bytes differ from the approved review artifact.`);
}

run('npm', ['run', 'build:review'], path.join(repository, 'web'));
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
        displayBots: ['All defaults · Longshot west', 'Lantern east · live objectives'],
        broadcastOnly:
          'sandbox/longshot-objectives-meshy7-v1/combined-review/matches/' +
          'all-four-hunter-west/broadcast.json.gz',
      },
      {
        id: 'all-four-hunter-east',
        displayBots: ['All defaults · Lantern west', 'Longshot east · live objectives'],
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
const emittedCandidates = runtimeAssets.map((asset) => ({
  id: asset.id,
  sha256: asset.sha256,
  emitted: emitted.find((candidate) => candidate.sha256 === asset.sha256) ?? null,
}));
if (emittedCandidates.some((entry) => !entry.emitted))
  throw new Error(
    'Review output is missing promoted GLBs: ' +
      emittedCandidates.filter((entry) => !entry.emitted).map((entry) => entry.id).join(', '),
  );

const buildRecord = {
  schemaVersion: 2,
  purpose:
    'Current-build combined real-renderer proof of the promoted Railback, Survey Flare, Team Reactor, and Core Well defaults',
  sourceCommit: git('rev-parse', 'HEAD'),
  runtimeAssetsPromoted: {
    teamReactor: true,
    longshot: true,
    surveyFlare: true,
    coreWell: true,
  },
  tierAudits: {
    longshot: path.relative(repository, railbackAuditPath),
    objectives: path.relative(repository, tierAuditPath),
  },
  output: path.relative(repository, interactive),
  runtimeAssets,
  emittedCandidates,
};
const serializedBuildRecord = `${JSON.stringify(buildRecord, null, 2)}\n`;
writeFileSync(path.join(output, 'build-record.json'), serializedBuildRecord);
mkdirSync(evidenceDirectory, { recursive: true });
writeFileSync(path.join(evidenceDirectory, 'build-record.json'), serializedBuildRecord);
console.log(
  `Built promoted Longshot/objectives review at ${path.relative(repository, interactive)}.`,
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
