#!/usr/bin/env node

/** Build a focused real-renderer review for the motion-safe veterancy split diadem. */

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
import { gzipSync, gunzipSync } from 'node:zlib';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const repository = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..', '..');
const args = process.argv.slice(2);
const noBuild = takeFlag(args, '--no-build');
if (args.length > 0) throw new Error(`Unknown option ${args[0]}.`);

const scratch = path.join(repository, 'sandbox/arc-relay-veterancy-split-diadem-v1');
const generated = path.join(repository, 'sandbox/arc-relay-core-stage-review/generated');
const sourceReplay = path.join(generated, 'replay.json.gz');
const stagePath = path.join(generated, 'stage.json');
const site = path.join(scratch, 'site');
const archive = path.join(scratch, 'archive');
const rankedReplay = path.join(scratch, 'veterancy-ranked-core-stage.broadcast.json.gz');

if (!noBuild) {
  run('dotnet', [
    'build',
    'src/BotArena.Cli/BotArena.Cli.csproj',
    '--no-restore',
  ], repository);
  mkdirSync(generated, { recursive: true });
  run('dotnet', [
    'run',
    '--project',
    'src/BotArena.Cli',
    '--no-build',
    '--',
    'experiment',
    'arc-relay-core-stage',
    '--out',
    path.relative(repository, generated),
  ], repository);
  run('npm', ['run', 'build:review'], path.join(repository, 'web'));
}

const sourceGzip = readFileSync(sourceReplay);
const source = JSON.parse(gunzipSync(sourceGzip).toString('utf8'));
if (
  source.broadcastVersion !== 2 ||
  source.partial !== false ||
  source.header?.contract?.rules?.gameMode?.kind !== 'arc-relay' ||
  source.header.contract.rules.gameMode.veterancyMaxLevel !== 5 ||
  !Array.isArray(source.events?.[0]) ||
  source.events[0].length !== 0
)
  throw new Error('Expected a complete empty-tick-zero Core custody broadcast with level-five tuning.');

const assignments = [
  { teamId: 0, unitId: 0, lifeId: 0, classId: 'relay', level: 5 },
  { teamId: 0, unitId: 1, lifeId: 0, classId: 'kestrel', level: 1 },
  { teamId: 0, unitId: 2, lifeId: 0, classId: 'mason', level: 2 },
  { teamId: 0, unitId: 3, lifeId: 0, classId: 'mortar', level: 3 },
  { teamId: 1, unitId: 0, lifeId: 0, classId: 'nest', level: 4 },
  { teamId: 1, unitId: 1, lifeId: 0, classId: 'palisade', level: 5 },
  { teamId: 1, unitId: 2, lifeId: 0, classId: 'towline', level: 2 },
  { teamId: 1, unitId: 3, lifeId: 0, classId: 'patchbay', level: 4 },
];
const actors = source.worlds[0]?.[4];
if (!Array.isArray(actors)) throw new Error('Core custody broadcast has no initial actors.');
let ordinal = 1;
source.events[0] = assignments
  .filter((assignment) => assignment.level > 1)
  .map((assignment, sourceOrdinal) => {
    const actor = actors.find(
      (candidate) =>
        candidate[0] === assignment.teamId &&
        candidate[1] === assignment.unitId &&
        candidate[2] === assignment.lifeId,
    );
    if (!actor) throw new Error(`Missing ${assignment.classId} actor for rank diagnostic.`);
    return {
      eventHandle:
        `veterancy-presentation:${assignment.teamId}:${assignment.unitId}:${assignment.level}`,
      tick: 0,
      globalOrdinal: String(ordinal++),
      sourceOrdinal,
      kind: 'arc-relay',
      payload: {
        kind: 'arc-relay',
        fact: {
          kind: 'leveled-up',
          actorId: {
            teamId: assignment.teamId,
            unitId: assignment.unitId,
            lifeId: assignment.lifeId,
          },
          level: assignment.level,
          position: { x: actor[6], y: actor[7] },
        },
      },
      audience: { kind: 'public' },
    };
  });
const sourceReplayHash = source.replayHash;
source.replayHash = sha256(
  Buffer.from(
    `${sourceReplayHash}\n` +
      'nilbots-veterancy-split-diadem-presentation-v1\n' +
      JSON.stringify(assignments),
  ),
);
const rankedJson = `${JSON.stringify(source)}\n`;
mkdirSync(scratch, { recursive: true });
writeFileSync(rankedReplay, gzipSync(Buffer.from(rankedJson), { level: 9, mtime: 0 }));

rmSync(site, { recursive: true, force: true });
cpSync(path.join(repository, 'web/dist-review'), site, { recursive: true });
const galleryManifest = {
  schemaVersion: 1,
  outputDirectory: path.relative(repository, site),
  archiveDirectory: path.relative(repository, archive),
  entries: [
    {
      id: 'veterancy-rank-lineup-core-motion',
      broadcastOnly: path.relative(repository, rankedReplay),
      displayBots: [
        'Ranks I–V · presentation diagnostic',
        'Relay V carries/tosses Core · both teams and headings',
      ],
    },
    {
      id: 'core-custody-rank-one-control',
      broadcastOnly: path.relative(repository, sourceReplay),
      displayBots: [
        'Rank I control · real Core custody stage',
        '8 handlers · 6 handoffs · 2 drops · 4-tile Arc Toss',
      ],
    },
  ],
};
const galleryManifestPath = path.join(scratch, 'replay-gallery-sources.json');
writeFileSync(galleryManifestPath, `${JSON.stringify(galleryManifest, null, 2)}\n`);
run(process.execPath, [
  'scripts/class-models/install-local-replay-gallery.mjs',
  path.relative(repository, galleryManifestPath),
], repository);

const runtimeManifests = Array.from({ length: 5 }, (_, index) => {
  const rank = index + 1;
  const manifestPath = path.join(
    repository,
    `web/src/assets/ornament-models/arc-veterancy-diadem-rank-${rank}/model3d.json`,
  );
  return JSON.parse(readFileSync(manifestPath, 'utf8'));
});
const emitted = readdirSync(path.join(site, 'assets'))
  .filter((file) => file.endsWith('.glb'))
  .map((file) => {
    const bytes = readFileSync(path.join(site, 'assets', file));
    return { file: `assets/${file}`, bytes: bytes.length, sha256: sha256(bytes) };
  });
const emittedDiadems = runtimeManifests.map((manifest) => {
  const artifact = emitted.find((entry) => entry.sha256 === manifest.ledger.sha256) ?? null;
  if (!artifact)
    throw new Error(`Review bundle does not contain promoted ${manifest.id}.`);
  return { id: manifest.id, artifact };
});

const stage = JSON.parse(readFileSync(stagePath, 'utf8'));
const record = {
  schemaVersion: 1,
  purpose:
    'Focused real-renderer review of five complete yaw-following Meshy diadem ranks and their fixed Core-animation corridor',
  status: 'presentation-diagnostic-owner-review',
  sourceCommit: git('rev-parse', 'HEAD'),
  sourceCoreStage: {
    replay: path.relative(repository, sourceReplay),
    replayHash: sourceReplayHash,
    receipt: path.relative(repository, stagePath),
    choreography: stage.choreography,
  },
  rankedDiagnostic: {
    replay: path.relative(repository, rankedReplay),
    replayHash: source.replayHash,
    mutation:
      'Seven public tick-zero leveled-up presentation facts only; world snapshots, turns, Core custody, movement, and results are byte-for-byte inherited from the real Core stage.',
    assignments,
  },
  runtimeModels: runtimeManifests,
  emittedDiadems,
  output: path.relative(repository, site),
};
writeFileSync(path.join(scratch, 'build-record.json'), `${JSON.stringify(record, null, 2)}\n`);
console.log(`Veterancy review ready at ${path.relative(repository, site)}.`);

function takeFlag(values, flag) {
  const index = values.indexOf(flag);
  if (index < 0) return false;
  values.splice(index, 1);
  return true;
}

function run(command, commandArgs, cwd) {
  const result = spawnSync(command, commandArgs, { cwd, stdio: 'inherit' });
  if (result.error) throw result.error;
  if (result.status !== 0)
    throw new Error(`${command} exited ${result.status ?? 'without a status'}.`);
}

function git(...commandArgs) {
  const result = spawnSync('git', commandArgs, { cwd: repository, encoding: 'utf8' });
  if (result.error) throw result.error;
  if (result.status !== 0) throw new Error(result.stderr || 'git command failed.');
  return result.stdout.trim();
}

function sha256(value) {
  return createHash('sha256').update(value).digest('hex');
}
