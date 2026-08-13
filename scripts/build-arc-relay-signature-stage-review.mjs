#!/usr/bin/env node

/** Build a live picker of short, single-signature Arc Relay presentation stages. */

import { spawnSync } from 'node:child_process';
import { mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const repository = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const allSignatures = [
  'vector-dash',
  'prism-wall',
  'tractor-hook',
  'repair-beam',
  'survey-flare',
  'falling-star',
  'trip-node',
  'null-field',
  'arc-toss',
  'exchange',
  'rail-line',
  'hardlight-block',
  'target-paint',
  'kinetic-burst',
  'smoke-canister',
  'sentinel-seed',
];
const args = process.argv.slice(2);
const noBuild = takeFlag(args, '--no-build');
const all = takeFlag(args, '--all');
if (args.some((value) => value.startsWith('--')))
  throw new Error(`Unknown option ${args.find((value) => value.startsWith('--'))}.`);
const signatures = all ? allSignatures : [...new Set(args)];
if (signatures.length === 0)
  throw new Error(
    'Usage: build-arc-relay-signature-stage-review.mjs [--no-build] <signature...> | --all',
  );
for (const signature of signatures) {
  if (!allSignatures.includes(signature))
    throw new Error(`Unknown signature ${signature}.`);
}

const scratch = path.join(repository, 'sandbox', 'arc-relay-signature-stage-review');
const generated = path.join(scratch, 'generated');
mkdirSync(generated, { recursive: true });

run('dotnet', [
  'build',
  'src/BotArena.Cli/BotArena.Cli.csproj',
  '--no-restore',
], repository);
for (const signature of signatures) {
  run('dotnet', [
    'run',
    '--project',
    'src/BotArena.Cli',
    '--no-build',
    '--',
    'experiment',
    'arc-relay-signature-stage',
    '--signature',
    signature,
    '--out',
    path.relative(repository, path.join(generated, signature)),
  ], repository);
}
if (!noBuild)
  run('npm', ['run', 'build:review'], path.join(repository, 'web'));

const manifest = {
  schemaVersion: 1,
  outputDirectory: 'web/dist-review',
  archiveDirectory: path.relative(repository, path.join(scratch, 'archive')),
  entries: signatures.map((signature) => {
    const stagePath = path.join(generated, signature, 'stage.json');
    const stage = JSON.parse(readFileSync(stagePath, 'utf8'));
    return {
      id: `signature-stage-${signature}`,
      broadcastOnly: path.relative(
        repository,
        path.join(generated, signature, 'replay.json.gz'),
      ),
      displayBots: [
        `${title(stage.classId)} — ${title(signature)}`,
        `${stage.attempts} real cast${stage.attempts === 1 ? '' : 's'} · minimal context`,
      ],
    };
  }),
};
const manifestPath = path.join(scratch, 'gallery-sources.json');
writeFileSync(manifestPath, `${JSON.stringify(manifest, null, 2)}\n`);
run('node', [
  'scripts/class-models/install-local-replay-gallery.mjs',
  path.relative(repository, manifestPath),
], repository);
console.log(
  `Focused picker ready in web/dist-review (${signatures.join(', ')}). ` +
  'Serve with: cd web && npm run review -- --no-build --tunnel',
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
