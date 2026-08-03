#!/usr/bin/env node

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
import { gunzipSync } from 'node:zlib';
import { withCanonicalTeamVision } from '../arc-relay-team-vision.mjs';

const repository = path.resolve(
  path.dirname(fileURLToPath(import.meta.url)),
  '..',
  '..',
);
const manifestPath = path.resolve(repository, process.argv[2] ?? '');
if (!process.argv[2])
  throw new Error(
    'Usage: install-local-replay-gallery.mjs <gallery-sources.json>',
  );
const manifest = JSON.parse(readFileSync(manifestPath, 'utf8'));
if (manifest.schemaVersion !== 1 || !Array.isArray(manifest.entries))
  throw new Error('Replay gallery manifest must use schema version 1.');

const outputDirectory = path.resolve(repository, manifest.outputDirectory);
const archiveDirectory = path.resolve(repository, manifest.archiveDirectory);
if (!existsSync(path.join(outputDirectory, 'index.html')))
  throw new Error(
    `Review build ${outputDirectory} does not exist; build the intended candidate first.`,
  );
rmSync(path.join(outputDirectory, 'replays'), { recursive: true, force: true });
rmSync(archiveDirectory, { recursive: true, force: true });
mkdirSync(path.join(outputDirectory, 'replays'), { recursive: true });
mkdirSync(archiveDirectory, { recursive: true });

const choices = [];
const evidence = [];
for (const entry of manifest.entries) {
  validateEntry(entry);
  if (entry.broadcastOnly) {
    const broadcastPath = path.resolve(repository, entry.broadcastOnly);
    const broadcastGzip = readFileSync(broadcastPath);
    const broadcastJson = gunzipSync(broadcastGzip).toString('utf8');
    const broadcast = JSON.parse(broadcastJson);
    if (
      broadcast.broadcastVersion !== 1 ||
      broadcast.header?.contract?.rules?.gameMode?.kind !== 'arc-relay' ||
      !/^[0-9a-f]{64}$/.test(broadcast.canonicalReplayHash ?? '') ||
      !Array.isArray(broadcast.worlds) ||
      broadcast.worlds.length === 0 ||
      broadcast.result === null
    )
      throw new Error(`${entry.id} is not one complete Arc Relay broadcast.`);
    const archiveFilename = `${entry.id}.broadcast.json.gz`;
    const runtimeFilename = `${entry.id}.json`;
    copyFileSync(
      broadcastPath,
      path.join(archiveDirectory, archiveFilename),
    );
    writeFileSync(
      path.join(outputDirectory, 'replays', runtimeFilename),
      broadcastJson,
    );
    choices.push({
      id: entry.id,
      url: `replays/${runtimeFilename}`,
      map: broadcast.header.contract.map.mapId,
      bots: entry.displayBots,
      ticks: broadcast.worlds.length,
      reason: broadcast.result.completionReason,
    });
    evidence.push({
      id: entry.id,
      sourceReplay: null,
      archivedReplay: null,
      canonicalReplayHash: broadcast.canonicalReplayHash,
      ticks: broadcast.worlds.length,
      map: broadcast.header.contract.map.mapId,
      seed: broadcast.header.seed,
      result: broadcast.result,
      participants: null,
      transport: {
        source: path.relative(repository, broadcastPath),
        archive: path.relative(
          repository,
          path.join(archiveDirectory, archiveFilename),
        ),
        broadcastVersion: broadcast.broadcastVersion,
        gzipBytes: broadcastGzip.length,
        gzipSha256: sha256(broadcastGzip),
      },
    });
    continue;
  }
  const runPath = path.resolve(repository, entry.run);
  const replayPath = path.resolve(repository, entry.replay);
  const run = JSON.parse(readFileSync(runPath, 'utf8'));
  const replayGzip = readFileSync(replayPath);
  const replayJson = gunzipSync(replayGzip).toString('utf8');
  const replay = JSON.parse(replayJson);
  if (
    replay.header?.replayVersion !== 3 ||
    replay.header?.contract?.rules?.gameMode?.kind !== 'arc-relay' ||
    replay.partial !== false ||
    replay.replayHash !== run.Replay?.Hash ||
    replay.ticks?.length !== run.Result?.EndTick + 1
  )
    throw new Error(`${entry.id} is not one complete hash-matched Arc Relay replay.`);

  let runtimeJson = replayJson;
  let transportEvidence = null;
  if (entry.transport) {
    const transportPath = path.resolve(repository, entry.transport);
    const transportGzip = readFileSync(transportPath);
    const transportJson = gunzipSync(transportGzip).toString('utf8');
    const transport = JSON.parse(transportJson);
    if (
      transport.broadcastVersion !== 1 ||
      transport.canonicalReplayHash !== replay.replayHash ||
      transport.worlds?.length !== replay.ticks.length
    )
      throw new Error(`${entry.id} transport does not preserve the canonical replay.`);
    const transportArchive = `${entry.id}.broadcast.json.gz`;
    copyFileSync(
      transportPath,
      path.join(archiveDirectory, transportArchive),
    );
    const runtimeTransport = withCanonicalTeamVision(transport, replay);
    runtimeJson = JSON.stringify(runtimeTransport);
    transportEvidence = {
      source: path.relative(repository, transportPath),
      archive: path.relative(
        repository,
        path.join(archiveDirectory, transportArchive),
      ),
      broadcastVersion: transport.broadcastVersion,
      gzipBytes: transportGzip.length,
      gzipSha256: sha256(transportGzip),
      vision: transport.vision === undefined ? 'canonical-replay' : 'transport',
    };
  }

  const archiveFilename = `${entry.id}.json.gz`;
  const runtimeFilename = `${entry.id}.json`;
  copyFileSync(replayPath, path.join(archiveDirectory, archiveFilename));
  writeFileSync(
    path.join(outputDirectory, 'replays', runtimeFilename),
    runtimeJson,
  );
  choices.push({
    id: entry.id,
    url: `replays/${runtimeFilename}`,
    map: run.MapId,
    bots: entry.displayBots,
    ticks: replay.ticks.length,
    reason: run.Result.Reason,
  });
  evidence.push({
    id: entry.id,
    run: path.relative(repository, runPath),
    sourceReplay: path.relative(repository, replayPath),
    archivedReplay: path.relative(
      repository,
      path.join(archiveDirectory, archiveFilename),
    ),
    canonicalReplayHash: replay.replayHash,
    gzipBytes: replayGzip.length,
    gzipSha256: sha256(replayGzip),
    ticks: replay.ticks.length,
    map: run.MapId,
    seed: run.Seed,
    result: run.Result,
    participants: run.Participants.map((participant) => ({
      name: participant.Name,
      classes: participant.Classes,
    })),
    transport: transportEvidence,
  });
}

writeFileSync(
  path.join(outputDirectory, 'replays.json'),
  `${JSON.stringify(choices, null, 2)}\n`,
);
copyFileSync(
  path.join(outputDirectory, 'replays', `${choices[0].id}.json`),
  path.join(outputDirectory, 'replay.json'),
);
writeFileSync(
  path.join(archiveDirectory, 'manifest.json'),
  `${JSON.stringify({ schemaVersion: 1, installedAt: new Date().toISOString(), entries: evidence }, null, 2)}\n`,
);
console.log(
  `Installed ${choices.length} canonical Arc Relay replays into ${path.relative(repository, outputDirectory)}.`,
);

function validateEntry(entry) {
  if (
    typeof entry?.id !== 'string' ||
    !/^[a-z0-9]+(?:-[a-z0-9]+)*$/.test(entry.id) ||
    (entry.broadcastOnly === undefined && typeof entry.run !== 'string') ||
    (entry.broadcastOnly === undefined && typeof entry.replay !== 'string') ||
    (entry.broadcastOnly !== undefined && typeof entry.broadcastOnly !== 'string') ||
    (entry.transport !== undefined && typeof entry.transport !== 'string') ||
    !Array.isArray(entry.displayBots) ||
    entry.displayBots.length !== 2 ||
    entry.displayBots.some((value) => typeof value !== 'string')
  )
    throw new Error('Every replay gallery entry needs an ID, run, replay, and two labels.');
}

function sha256(value) {
  return createHash('sha256').update(value).digest('hex');
}
