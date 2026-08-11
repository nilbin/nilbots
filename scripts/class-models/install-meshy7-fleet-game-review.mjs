#!/usr/bin/env node

import { createHash } from 'node:crypto';
import { spawnSync } from 'node:child_process';
import {
  cpSync,
  existsSync,
  readFileSync,
  readdirSync,
  rmSync,
  writeFileSync,
} from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { gunzipSync } from 'node:zlib';

const repository = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..', '..');
const buildDirectory = path.join(repository, 'web/dist-review');
const reviewDirectory = path.join(repository, 'sandbox/meshy7-pilot/fleet-game-review');
const interactiveDirectory = path.join(reviewDirectory, 'interactive');
const replayPath = path.join(reviewDirectory, 'match/replay.json.gz');
const broadcastPath = path.join(reviewDirectory, 'match/broadcast.json.gz');
const runPath = path.join(reviewDirectory, 'match/run.json');
const manifestPath = path.join(reviewDirectory, 'replay-gallery-sources.json');
const archiveDirectory = path.join(reviewDirectory, 'replay-gallery-archive');
const buildRecordPath = path.join(
  repository,
  'art/class-models/provider-runs/meshy/arc-relay-meshy7-fleet/candidate-build.json',
);

for (const file of [path.join(buildDirectory, 'index.html'), replayPath, runPath, buildRecordPath])
  if (!existsSync(file)) throw new Error(`Missing ${path.relative(repository, file)}.`);

const run = JSON.parse(readFileSync(runPath, 'utf8'));
const build = JSON.parse(readFileSync(buildRecordPath, 'utf8'));
const replayGzip = readFileSync(replayPath);
const replay = JSON.parse(gunzipSync(replayGzip));
if (
  build.runtimeAssetsPromoted !== false ||
  build.staged?.length !== 16 ||
  replay.header?.replayVersion !== 3 ||
  replay.header?.contract?.rules?.gameMode?.kind !== 'arc-relay' ||
  replay.partial !== false ||
  replay.replayHash !== run.Replay?.Hash ||
  new Set(run.Participants?.flatMap((participant) => participant.Classes ?? [])).size !== 16
)
  throw new Error('Review build or replay is not the complete reversible Meshy 7 fleet review.');

runCommand('python3', [
  'scripts/arc-relay-broadcast.py',
  path.relative(repository, replayPath),
  '--output',
  path.relative(repository, broadcastPath),
]);

const broadcastGzip = readFileSync(broadcastPath);
const broadcast = JSON.parse(gunzipSync(broadcastGzip));
if (
  broadcast.broadcastVersion !== 1 ||
  broadcast.canonicalReplayHash !== replay.replayHash ||
  broadcast.worlds?.length !== replay.ticks.length ||
  broadcastGzip.length > 300 * 1024
)
  throw new Error('Compact broadcast does not preserve the canonical fleet replay.');

rmSync(interactiveDirectory, { recursive: true, force: true });
cpSync(buildDirectory, interactiveDirectory, { recursive: true });
const manifest = {
  schemaVersion: 1,
  outputDirectory: path.relative(repository, interactiveDirectory),
  archiveDirectory: path.relative(repository, archiveDirectory),
  entries: [
    {
      id: 'meshy7-complete-fleet',
      displayBots: ['Meshy 7 · blue eight', 'Meshy 7 · orange eight'],
      run: path.relative(repository, runPath),
      replay: path.relative(repository, replayPath),
      transport: path.relative(repository, broadcastPath),
    },
  ],
};
writeFileSync(manifestPath, `${JSON.stringify(manifest, null, 2)}\n`);
runCommand('node', [
  'scripts/class-models/install-local-replay-gallery.mjs',
  path.relative(repository, manifestPath),
]);

const emittedModels = readdirSync(path.join(interactiveDirectory, 'assets'))
  .filter((file) => file.endsWith('.glb'))
  .map((file) => {
    const bytes = readFileSync(path.join(interactiveDirectory, 'assets', file));
    return { file: `assets/${file}`, bytes: bytes.length, sha256: sha256(bytes) };
  });
for (const staged of build.staged)
  if (!emittedModels.some((model) => model.sha256 === staged.candidateSha256))
    throw new Error(`${staged.lookId}: candidate is absent from the review bundle.`);

const runtimeReplayPath = path.join(interactiveDirectory, 'replay.json');
const runtimeReplayGzipPath = `${runtimeReplayPath}.gz`;
const runtimeReplayBytes = readFileSync(runtimeReplayPath).length;
const runtimeReplayGzipBytes = readFileSync(runtimeReplayGzipPath).length;
writeFileSync(
  path.join(reviewDirectory, 'interactive-install.json'),
  `${JSON.stringify({
    schemaVersion: 2,
    purpose: 'Unapproved Meshy 7 complete-fleet in-game owner review',
    runtimeAssetsPromoted: false,
    build: path.relative(repository, buildRecordPath),
    replay: path.relative(repository, replayPath),
    canonicalReplayHash: replay.replayHash,
    replayTicks: replay.ticks.length,
    canonicalReplayGzipBytes: replayGzip.length,
    transport: path.relative(repository, broadcastPath),
    transportGzipBytes: broadcastGzip.length,
    runtimeReplayBytes,
    runtimeReplayGzipBytes,
    output: path.relative(repository, interactiveDirectory),
    installer: 'scripts/class-models/install-local-replay-gallery.mjs',
    emittedCandidateModels: build.staged.map((staged) => ({
      lookId: staged.lookId,
      candidateSha256: staged.candidateSha256,
      emitted: emittedModels.find((model) => model.sha256 === staged.candidateSha256),
    })),
  }, null, 2)}\n`,
);
console.log(
  `Installed ${build.staged.length} candidates and ${replay.ticks.length} replay ticks from a ` +
    `${broadcastGzip.length}-byte compact broadcast into ${path.relative(repository, interactiveDirectory)}.`,
);

function runCommand(command, arguments_) {
  const result = spawnSync(command, arguments_, {
    cwd: repository,
    encoding: 'utf8',
    stdio: 'inherit',
  });
  if (result.error) throw result.error;
  if (result.status !== 0)
    throw new Error(`${command} failed with exit code ${result.status}.`);
}

function sha256(value) {
  return createHash('sha256').update(value).digest('hex');
}
