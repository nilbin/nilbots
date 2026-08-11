#!/usr/bin/env node

import { createHash } from 'node:crypto';
import { spawnSync } from 'node:child_process';
import {
  copyFileSync,
  existsSync,
  mkdirSync,
  mkdtempSync,
  readFileSync,
  rmSync,
  writeFileSync,
} from 'node:fs';
import { tmpdir } from 'node:os';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const repository = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..', '..');
const tierAuditPath = path.join(
  repository,
  'art/class-models/runtime-tiers/arc-relay/meshy7-fleet-review-v1/audit.json',
);
const outputDirectory = path.join(
  repository,
  'art/class-models/provider-runs/meshy/arc-relay-meshy7-fleet',
);
const buildRecordPath = path.join(outputDirectory, 'candidate-build.json');
const tierAudit = JSON.parse(readFileSync(tierAuditPath, 'utf8'));
const backupDirectory = mkdtempSync(path.join(tmpdir(), 'nilbots-meshy7-fleet-review-'));
const staged = [];

if (tierAudit.status !== 'owner-review' || tierAudit.classes?.length !== 16)
  throw new Error('Expected a complete owner-review Meshy 7 fleet tier.');
mkdirSync(outputDirectory, { recursive: true });

try {
  for (const entry of tierAudit.classes) {
    const lookId = `arc-${entry.classId}`;
    const runtimeModel = path.join(
      repository,
      'web/src/assets/class-looks',
      lookId,
      'model.glb',
    );
    const candidateModel = path.join(repository, entry.artifact.file);
    const backupModel = path.join(backupDirectory, `${lookId}.glb`);
    if (!existsSync(runtimeModel) || !existsSync(candidateModel))
      throw new Error(`${lookId}: runtime or candidate model is missing.`);
    const before = readFileSync(runtimeModel);
    const candidate = readFileSync(candidateModel);
    copyFileSync(runtimeModel, backupModel);
    copyFileSync(candidateModel, runtimeModel);
    staged.push({
      lookId,
      candidate: path.relative(repository, candidateModel),
      runtimeSha256Before: sha256(before),
      candidateSha256: sha256(candidate),
      candidateBytes: candidate.length,
    });
  }

  run('npm', ['run', 'build:review'], path.join(repository, 'web'));
} finally {
  for (const entry of staged) {
    const runtimeModel = path.join(
      repository,
      'web/src/assets/class-looks',
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
    'web/src/assets/class-looks',
    entry.lookId,
    'model.glb',
  );
  const restoredHash = sha256(readFileSync(runtimeModel));
  if (restoredHash !== entry.runtimeSha256Before)
    throw new Error(`${entry.lookId}: runtime model was not restored exactly.`);
  entry.runtimeSha256After = restoredHash;
}

const record = {
  schemaVersion: 1,
  builtAt: new Date().toISOString(),
  purpose: 'Unapproved Meshy 7 Arc Relay fleet review build',
  runtimeAssetsPromoted: false,
  tierAudit: path.relative(repository, tierAuditPath),
  output: 'web/dist-review',
  staged,
};
writeFileSync(buildRecordPath, `${JSON.stringify(record, null, 2)}\n`);
console.log(
  `Built review-only fleet with ${staged.length} candidates; runtime models restored byte-for-byte.`,
);

function run(command, arguments_, cwd) {
  const result = spawnSync(command, arguments_, {
    cwd,
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
