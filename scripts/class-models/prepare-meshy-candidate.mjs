#!/usr/bin/env node

import { existsSync, readFileSync } from 'node:fs';
import { spawnSync } from 'node:child_process';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const repository = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..', '..');
const options = parseOptions(process.argv.slice(2));
const lookId = required('look');
const taskId = required('task');
const orientation = options.orientation ?? 'lay-flat-x';
const facingYaw = Number(options['facing-yaw'] ?? 0);
const targetSpan = Number(options['target-span'] ?? 0.99);
const textureSize = Number(options['texture-size'] ?? 1024);
if (!['identity', 'lay-flat-x'].includes(orientation))
  throw new Error('orientation must be identity or lay-flat-x.');
if (!Number.isFinite(facingYaw) || !Number.isFinite(targetSpan) || targetSpan <= 0)
  throw new Error('facing-yaw and target-span must be finite; target-span must be positive.');
if (!Number.isInteger(textureSize) || textureSize < 256 || textureSize > 4096)
  throw new Error('texture-size must be an integer from 256 to 4096.');

const runDirectory = path.join(
  repository,
  'art',
  'class-models',
  'provider-runs',
  'meshy',
  lookId,
  taskId,
);
const requestPath = path.join(runDirectory, 'request-record.json');
const rawModel = path.join(runDirectory, 'raw-model.glb');
if (!existsSync(requestPath) || !existsSync(rawModel))
  throw new Error(`Meshy task ${lookId}/${taskId} is missing its request or raw GLB.`);
const request = JSON.parse(readFileSync(requestPath, 'utf8'));
if (request.lookId !== lookId || request.taskId !== taskId)
  throw new Error('Meshy request provenance does not match --look and --task.');

const identity = orientation === 'identity';
const normalizedRaw = path.join(
  runDirectory,
  identity ? 'model-normalized-identity-raw.glb' : 'model-normalized-raw.glb',
);
const normalizedReview = path.join(
  runDirectory,
  identity ? 'model-normalized-identity-review.glb' : 'model-normalized-review.glb',
);
const facingReview = path.join(
  runDirectory,
  identity
    ? 'model-normalized-identity-facing-review.glb'
    : 'model-normalized-facing-review.glb',
);

run(process.execPath, [
  path.join(repository, 'scripts/class-models/normalize-provider-glb.mjs'),
  rawModel,
  normalizedRaw,
  String(targetSpan),
  orientation,
]);
// Do not flatten the normalization wrapper: it owns the audited axes, floor, centering,
// and provenance. The default optimize preset flattens it into the mesh and makes a later
// facing correction impossible to verify.
run('npx', [
  '--yes',
  '@gltf-transform/cli@4.4.2',
  'optimize',
  normalizedRaw,
  normalizedReview,
  '--compress',
  'quantize',
  '--texture-compress',
  'webp',
  '--texture-size',
  String(textureSize),
  '--flatten',
  'false',
  '--join',
  'false',
]);
if (facingYaw !== 0)
  run(process.execPath, [
    path.join(repository, 'scripts/class-models/correct-provider-facing.mjs'),
    normalizedReview,
    facingReview,
    String(facingYaw),
  ]);

console.log(
  `Prepared ${lookId}/${taskId}: ${path.relative(repository, facingYaw === 0 ? normalizedReview : facingReview)}`,
);

function run(command, args) {
  const result = spawnSync(command, args, { cwd: repository, stdio: 'inherit' });
  if (result.error) throw result.error;
  if (result.status !== 0)
    throw new Error(`${command} failed with exit code ${result.status}.`);
}

function required(name) {
  const value = options[name];
  if (!value)
    throw new Error(
      'Usage: prepare-meshy-candidate.mjs --look <id> --task <id> ' +
      '[--orientation identity|lay-flat-x] [--facing-yaw degrees] ' +
      '[--target-span 0.99] [--texture-size 1024]',
    );
  return value;
}

function parseOptions(args) {
  const parsed = {};
  for (let index = 0; index < args.length; index += 2) {
    const name = args[index];
    const value = args[index + 1];
    if (!name?.startsWith('--') || !value)
      throw new Error(`Expected --name value, received ${name ?? 'nothing'}.`);
    parsed[name.slice(2)] = value;
  }
  return parsed;
}
