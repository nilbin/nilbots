#!/usr/bin/env node

import { createHash } from 'node:crypto';
import { execFileSync, spawnSync } from 'node:child_process';
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
import { gunzipSync } from 'node:zlib';

const repository = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..', '..');
const replayGalleryRelative =
  'art/class-models/provider-runs/meshy/arc-fleet-review/replay-gallery';
const configuredReview = process.env.ARC_SIGNATURE_REPLAY_GALLERY;
const localReview = path.join(repository, replayGalleryRelative);
const gitCommonDirectory = execFileSync(
  'git',
  ['rev-parse', '--path-format=absolute', '--git-common-dir'],
  { cwd: repository, encoding: 'utf8' },
).trim();
const sharedReview = path.join(path.dirname(gitCommonDirectory), replayGalleryRelative);
const sourceReview = configuredReview
  ? path.resolve(configuredReview)
  : hasPinnedReplay(localReview)
    ? localReview
    : sharedReview;
const sourceManifestPath = path.join(sourceReview, 'manifest.json');
const output = path.join(repository, 'sandbox/arc-signature-props-v3/game-review');
const interactive = path.join(output, 'interactive');
const backup = mkdtempSync(path.join(tmpdir(), 'nilbots-signature-props-review-'));
const candidates = [
  {
    signatureId: 'trip-node',
    runtimeDirectory: 'arc-trip-node',
    candidate:
      'art/class-models/runtime-tiers/arc-relay/meshy7-signature-props-review-v3/' +
      'arc-trip-node-lean.glb',
  },
  {
    signatureId: 'sentinel-seed',
    runtimeDirectory: 'arc-sentinel-seed',
    candidate:
      'art/class-models/runtime-tiers/arc-relay/meshy7-signature-props-review-v3/' +
      'arc-sentinel-seed-lean.glb',
  },
];

const replayPath = path.join(sourceReview, 'stock-a-v-flow-b.json.gz');
const broadcastPath = path.join(sourceReview, 'stock-a-v-flow-b.broadcast.json.gz');
for (const file of [sourceManifestPath, replayPath, broadcastPath])
  if (!existsSync(file)) throw new Error(`Missing review source ${file}.`);
const sourceManifest = JSON.parse(readFileSync(sourceManifestPath, 'utf8'));
const sourceEntry = sourceManifest.entries?.find((entry) => entry.id === 'stock-a-v-flow-b');
if (!sourceEntry) throw new Error('Pinned signature replay metadata is missing.');
const replay = JSON.parse(gunzipSync(readFileSync(replayPath)));
const broadcast = JSON.parse(gunzipSync(readFileSync(broadcastPath)));
const runReceipt = {
  MapId: sourceEntry.map,
  Seed: sourceEntry.seed,
  Replay: { Hash: sourceEntry.canonicalReplayHash },
  Result: sourceEntry.result,
  Participants: sourceEntry.participants.map((participant) => ({
    Name: participant.name,
    Classes: participant.classes,
  })),
};
if (
  replay.header?.replayVersion !== 3 ||
  replay.header?.contract?.rules?.gameMode?.kind !== 'arc-relay' ||
  replay.partial !== false ||
  replay.replayHash !== runReceipt.Replay?.Hash ||
  broadcast.broadcastVersion !== 1 ||
  broadcast.canonicalReplayHash !== replay.replayHash ||
  broadcast.worlds?.length !== replay.ticks.length
)
  throw new Error('Signature review source is not one complete hash-matched Arc Relay replay.');
const signatureTicks = firstSignatureTicks(replay);
if (signatureTicks['trip-node'] === undefined || signatureTicks['sentinel-seed'] === undefined)
  throw new Error('Review replay does not expose both persistent signature props.');

mkdirSync(output, { recursive: true });
const runPath = path.join(output, 'source-run.json');
writeFileSync(runPath, `${JSON.stringify(runReceipt, null, 2)}\n`);
const staged = [];
try {
  for (const candidate of candidates) {
    const runtimeModel = path.join(
      repository,
      'web/src/assets/signature-models',
      candidate.runtimeDirectory,
      'model.glb',
    );
    const candidateModel = path.join(repository, candidate.candidate);
    const backupModel = path.join(backup, `${candidate.signatureId}.glb`);
    if (!existsSync(runtimeModel) || !existsSync(candidateModel))
      throw new Error(`${candidate.signatureId}: runtime or candidate model is missing.`);
    const before = readFileSync(runtimeModel);
    const after = readFileSync(candidateModel);
    copyFileSync(runtimeModel, backupModel);
    copyFileSync(candidateModel, runtimeModel);
    staged.push({
      ...candidate,
      runtimeModel: path.relative(repository, runtimeModel),
      runtimeSha256Before: sha256(before),
      candidateBytes: after.length,
      candidateSha256: sha256(after),
    });
  }
  run('npm', ['run', 'build:review'], path.join(repository, 'web'));
} finally {
  for (const entry of staged) {
    const runtimeModel = path.join(repository, entry.runtimeModel);
    const backupModel = path.join(backup, `${entry.signatureId}.glb`);
    if (existsSync(backupModel)) copyFileSync(backupModel, runtimeModel);
  }
  rmSync(backup, { recursive: true, force: true });
}

for (const entry of staged) {
  const restored = readFileSync(path.join(repository, entry.runtimeModel));
  entry.runtimeSha256After = sha256(restored);
  if (entry.runtimeSha256After !== entry.runtimeSha256Before)
    throw new Error(`${entry.signatureId}: live runtime model was not restored exactly.`);
}

rmSync(interactive, { recursive: true, force: true });
cpSync(path.join(repository, 'web/dist-review'), interactive, { recursive: true });
const manifestPath = path.join(output, 'replay-gallery-sources.json');
writeFileSync(
  manifestPath,
  `${JSON.stringify({
    schemaVersion: 1,
    outputDirectory: path.relative(repository, interactive),
    archiveDirectory: path.relative(repository, path.join(output, 'replay-gallery-archive')),
    entries: [
      {
        id: 'meshy7-signature-props-v3',
        displayBots: ['Trip Node · matte Meshy 7', 'Sentinel · split yaw head'],
        run: path.relative(repository, runPath),
        replay: path.relative(repository, replayPath),
        transport: path.relative(repository, broadcastPath),
      },
    ],
  }, null, 2)}\n`,
);
run('node', ['scripts/class-models/install-local-replay-gallery.mjs', path.relative(repository, manifestPath)], repository);

const emittedGlbs = findGlbs(path.join(interactive, 'assets'));
for (const entry of staged) {
  entry.emitted = emittedGlbs.find((model) => model.sha256 === entry.candidateSha256) ?? null;
  if (!entry.emitted) throw new Error(`${entry.signatureId}: candidate is absent from build output.`);
}
writeFileSync(
  path.join(output, 'build-record.json'),
  `${JSON.stringify({
    schemaVersion: 1,
    purpose: 'Reversible real-renderer review of the Meshy 7 Trip Node and split Sentinel',
    runtimeAssetsPromoted: false,
    replay: path.relative(repository, replayPath),
    canonicalReplayHash: replay.replayHash,
    replayTicks: replay.ticks.length,
    signatureTicks,
    output: path.relative(repository, interactive),
    staged,
  }, null, 2)}\n`,
);
console.log(
  `Built reversible signature-prop review at ${path.relative(repository, interactive)}; ` +
    `live models restored byte-for-byte.`,
);

function firstSignatureTicks(value) {
  const result = {};
  for (const tick of value.ticks ?? []) {
    const mode = tick.after?.mode ?? tick.postState?.mode;
    for (const signature of mode?.visibleSignatures ?? [])
      result[signature.signatureId] ??= tick.tick;
  }
  return result;
}

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

function sha256(value) {
  return createHash('sha256').update(value).digest('hex');
}

function hasPinnedReplay(directory) {
  return ['manifest.json', 'stock-a-v-flow-b.json.gz', 'stock-a-v-flow-b.broadcast.json.gz']
    .every((file) => existsSync(path.join(directory, file)));
}
