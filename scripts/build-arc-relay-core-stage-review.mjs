#!/usr/bin/env node

/** Build one focused eight-body Core-custody replay in the 3D review viewer. */

import { spawnSync } from 'node:child_process';
import { mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const repository = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const args = process.argv.slice(2);
const noBuild = takeFlag(args, '--no-build');
if (args.length > 0)
  throw new Error(`Unknown option ${args[0]}.`);

const scratch = path.join(repository, 'sandbox', 'arc-relay-core-stage-review');
const generated = path.join(scratch, 'generated');
mkdirSync(generated, { recursive: true });

run('dotnet', [
  'build',
  'src/BotArena.Cli/BotArena.Cli.csproj',
  '--no-restore',
], repository);
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
if (!noBuild)
  run('npm', ['run', 'build:review'], path.join(repository, 'web'));

const stage = JSON.parse(
  readFileSync(path.join(generated, 'stage.json'), 'utf8'),
);
const choreography = stage.choreography;
const classes = stage.classes.map((entry) => title(entry.ClassId ?? entry.classId));
const manifest = {
  schemaVersion: 1,
  outputDirectory: 'web/dist-review',
  archiveDirectory: path.relative(repository, path.join(scratch, 'archive')),
  entries: [
    {
      id: 'core-custody-stage',
      broadcastOnly: path.relative(
        repository,
        path.join(generated, 'replay.json.gz'),
      ),
      displayBots: [
        `Core custody lab — ${classes.join(', ')}`,
        `${choreography.handoffs} handoffs · ${choreography.voluntaryDrops} drops · ${choreography.longestArcTossTiles}-tile Arc Toss`,
      ],
    },
  ],
};
const manifestPath = path.join(scratch, 'gallery-sources.json');
writeFileSync(manifestPath, `${JSON.stringify(manifest, null, 2)}\n`);
run('node', [
  'scripts/class-models/install-local-replay-gallery.mjs',
  path.relative(repository, manifestPath),
], repository);
console.log(
  'Core custody picker ready in web/dist-review. ' +
  'It contains one presentation diagnostic and makes no balance claim.',
);

function takeFlag(values, flag) {
  const index = values.indexOf(flag);
  if (index < 0) return false;
  values.splice(index, 1);
  return true;
}

function title(value) {
  return value
    .split('-')
    .map((part) => part[0].toUpperCase() + part.slice(1))
    .join(' ');
}

function run(command, commandArgs, cwd) {
  const result = spawnSync(command, commandArgs, {
    cwd,
    stdio: 'inherit',
  });
  if (result.status !== 0)
    throw new Error(`${command} exited ${result.status ?? 'without a status'}.`);
}
