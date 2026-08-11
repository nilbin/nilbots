#!/usr/bin/env node

import { createHash } from 'node:crypto';
import {
  cpSync,
  existsSync,
  mkdirSync,
  readFileSync,
  readdirSync,
  rmSync,
  writeFileSync,
} from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { gunzipSync, gzipSync } from 'node:zlib';

const repository = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..', '..');
const buildDirectory = path.join(repository, 'web/dist-review');
const reviewDirectory = path.join(repository, 'sandbox/meshy7-pilot/fleet-game-review');
const interactiveDirectory = path.join(reviewDirectory, 'interactive');
const replayPath = path.join(reviewDirectory, 'match/replay.json.gz');
const runPath = path.join(reviewDirectory, 'match/run.json');
const buildRecordPath = path.join(
  repository,
  'art/class-models/provider-runs/meshy/arc-relay-meshy7-fleet/candidate-build.json',
);

for (const file of [
  path.join(buildDirectory, 'index.html'),
  replayPath,
  runPath,
  buildRecordPath,
])
  if (!existsSync(file)) throw new Error(`Missing ${path.relative(repository, file)}.`);

const run = JSON.parse(readFileSync(runPath, 'utf8'));
const build = JSON.parse(readFileSync(buildRecordPath, 'utf8'));
const replayGzip = readFileSync(replayPath);
const replayJson = gunzipSync(replayGzip).toString('utf8');
const replay = JSON.parse(replayJson);
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

const emittedModels = readdirSync(path.join(buildDirectory, 'assets'))
  .filter((file) => file.endsWith('.glb'))
  .map((file) => {
    const bytes = readFileSync(path.join(buildDirectory, 'assets', file));
    return { file: `assets/${file}`, bytes: bytes.length, sha256: sha256(bytes) };
  });
for (const staged of build.staged)
  if (!emittedModels.some((model) => model.sha256 === staged.candidateSha256))
    throw new Error(`${staged.lookId}: candidate is absent from the review bundle.`);

rmSync(interactiveDirectory, { recursive: true, force: true });
cpSync(buildDirectory, interactiveDirectory, { recursive: true });
writeFileSync(path.join(interactiveDirectory, 'replay.json'), replayJson);
writeFileSync(path.join(interactiveDirectory, 'replay.json.gz'), replayGzip);
const catalog = `${JSON.stringify([
  {
    id: 'meshy7-complete-fleet',
    url: 'replay.json',
    map: run.MapId,
    bots: ['Meshy 7 · blue eight', 'Meshy 7 · orange eight'],
    ticks: replay.ticks.length,
    reason: null,
  },
], null, 2)}\n`;
writeFileSync(path.join(interactiveDirectory, 'replays.json'), catalog);
writeFileSync(
  path.join(interactiveDirectory, 'replays.json.gz'),
  gzipSync(Buffer.from(catalog), { level: 9, mtime: 0 }),
);
mkdirSync(reviewDirectory, { recursive: true });
writeFileSync(
  path.join(reviewDirectory, 'interactive-install.json'),
  `${JSON.stringify({
    schemaVersion: 1,
    purpose: 'Unapproved Meshy 7 complete-fleet in-game owner review',
    runtimeAssetsPromoted: false,
    build: path.relative(repository, buildRecordPath),
    replay: path.relative(repository, replayPath),
    canonicalReplayHash: replay.replayHash,
    replayTicks: replay.ticks.length,
    replayGzipBytes: replayGzip.length,
    output: path.relative(repository, interactiveDirectory),
    emittedCandidateModels: build.staged.map((staged) => ({
      lookId: staged.lookId,
      candidateSha256: staged.candidateSha256,
      emitted: emittedModels.find((model) => model.sha256 === staged.candidateSha256),
    })),
  }, null, 2)}\n`,
);
console.log(
  `Installed ${build.staged.length} candidates and ${replay.ticks.length} replay ticks into ${path.relative(repository, interactiveDirectory)}.`,
);

function sha256(value) {
  return createHash('sha256').update(value).digest('hex');
}
