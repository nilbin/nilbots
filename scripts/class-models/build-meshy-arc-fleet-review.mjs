#!/usr/bin/env node

import { createHash } from 'node:crypto';
import { spawnSync } from 'node:child_process';
import {
  copyFileSync,
  existsSync,
  mkdtempSync,
  mkdirSync,
  readFileSync,
  readdirSync,
  rmSync,
  statSync,
  writeFileSync,
} from 'node:fs';
import { tmpdir } from 'node:os';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const repository = path.resolve(
  path.dirname(fileURLToPath(import.meta.url)),
  '..',
  '..',
);
const options = parseOptions(process.argv.slice(2));
const receiptPath = absolute(
  options.receipt ??
    'art/class-models/provider-runs/meshy/arc-fleet-rest-15-2026-08-02.json',
);
const outputDirectory = absolute(
  options.output ??
    'art/class-models/provider-runs/meshy/arc-fleet-review',
);
const identityLooks = new Set(
  (options.identity ?? '')
    .split(',')
    .map((value) => value.trim())
    .filter(Boolean),
);
const receipt = JSON.parse(readFileSync(receiptPath, 'utf8'));
const lookIds = ['arc-mason', ...receipt.looks];
const backupDirectory = mkdtempSync(
  path.join(tmpdir(), 'nilbots-meshy-fleet-review-'),
);
const staged = [];

mkdirSync(outputDirectory, { recursive: true });

try {
  for (const lookId of lookIds) {
    const runtimeModel = path.join(
      repository,
      'web',
      'src',
      'assets',
      'class-looks',
      lookId,
      'model.glb',
    );
    const candidateModel = candidatePath(lookId);
    const backupModel = path.join(backupDirectory, `${lookId}.glb`);
    const before = readFileSync(runtimeModel);
    copyFileSync(runtimeModel, backupModel);
    copyFileSync(candidateModel, runtimeModel);
    const candidate = readFileSync(candidateModel);
    staged.push({
      lookId,
      candidate: path.relative(repository, candidateModel),
      orientation: identityLooks.has(lookId) ? 'identity' : 'lay-flat-x',
      runtimeSha256Before: sha256(before),
      candidateSha256: sha256(candidate),
      candidateBytes: candidate.length,
    });
  }

  const build = spawnSync('npm', ['run', 'build:review'], {
    cwd: path.join(repository, 'web'),
    encoding: 'utf8',
    stdio: 'inherit',
  });
  if (build.error) throw build.error;
  if (build.status !== 0)
    throw new Error(`Review build failed with exit code ${build.status}.`);
} finally {
  for (const entry of staged) {
    const runtimeModel = path.join(
      repository,
      'web',
      'src',
      'assets',
      'class-looks',
      entry.lookId,
      'model.glb',
    );
    const backupModel = path.join(backupDirectory, `${entry.lookId}.glb`);
    if (existsSync(backupModel)) copyFileSync(backupModel, runtimeModel);
  }
  rmSync(backupDirectory, { recursive: true, force: true });
}

for (const entry of staged) {
  const runtimeModel = path.join(
    repository,
    'web',
    'src',
    'assets',
    'class-looks',
    entry.lookId,
    'model.glb',
  );
  const restoredHash = sha256(readFileSync(runtimeModel));
  if (restoredHash !== entry.runtimeSha256Before)
    throw new Error(`${entry.lookId} runtime model was not restored exactly.`);
  entry.runtimeSha256After = restoredHash;
}

const record = {
  schemaVersion: 1,
  builtAt: new Date().toISOString(),
  purpose: 'Unapproved Meshy Arc Relay fleet review build',
  runtimeAssetsPromoted: false,
  receipt: path.relative(repository, receiptPath),
  output: 'web/dist-review',
  staged,
};
writeFileSync(
  path.join(outputDirectory, 'candidate-build.json'),
  `${JSON.stringify(record, null, 2)}\n`,
);
console.log(
  `Built review-only fleet with ${staged.length} candidates; runtime models restored byte-for-byte.`,
);

function candidatePath(lookId) {
  const providerRoot = path.join(
    repository,
    'art',
    'class-models',
    'provider-runs',
    'meshy',
    lookId,
  );
  const runDirectory = readdirSync(providerRoot)
    .map((name) => path.join(providerRoot, name))
    .find(
      (directory) =>
        statSync(directory).isDirectory() &&
        existsSync(path.join(directory, 'timing.json')),
    );
  if (!runDirectory) throw new Error(`No completed provider run for ${lookId}.`);

  const filename = lookId === 'arc-mason'
    ? 'mason-normalized-large-review.glb'
    : identityLooks.has(lookId)
      ? 'model-normalized-identity-review.glb'
      : 'model-normalized-review.glb';
  const candidate = path.join(runDirectory, filename);
  if (!existsSync(candidate))
    throw new Error(`Missing ${lookId} candidate ${filename}.`);
  return candidate;
}

function parseOptions(args) {
  const parsed = {};
  for (let index = 0; index < args.length; index += 2) {
    const name = args[index];
    if (!name?.startsWith('--') || !args[index + 1])
      throw new Error(`Expected --name value, received ${name ?? 'nothing'}.`);
    parsed[name.slice(2)] = args[index + 1];
  }
  return parsed;
}

function absolute(value) {
  return path.resolve(repository, value);
}

function sha256(value) {
  return createHash('sha256').update(value).digest('hex');
}
