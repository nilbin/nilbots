#!/usr/bin/env node

import { createHash } from 'node:crypto';
import { spawnSync } from 'node:child_process';
import {
  existsSync,
  mkdirSync,
  readFileSync,
  readdirSync,
  statSync,
  writeFileSync,
} from 'node:fs';
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
const outputPath = absolute(
  options.output ??
    'art/class-models/provider-runs/meshy/arc-fleet-review/fleet-audit.json',
);
const validator = options.validator ? absolute(options.validator) : null;
const receipt = JSON.parse(readFileSync(receiptPath, 'utf8'));
const orientations = new Map([
  ['arc-lantern', 'identity'],
  ['arc-mortar', 'identity'],
]);
const generated = receipt.looks.map((lookId) => audit(lookId));
const mason = audit('arc-mason');
const start = Math.min(...generated.map((entry) => Date.parse(entry.submittedAt)));
const finish = Math.max(...generated.map((entry) => Date.parse(entry.finishedAt)));
const credits = generated.reduce((sum, entry) => sum + entry.consumedCredits, 0);
const providerStageSeconds = generated.reduce(
  (sum, entry) => sum + entry.providerStageSeconds,
  0,
);
const bytes = generated.map((entry) => entry.candidate.bytes);
const candidateBuildPath = path.join(
  path.dirname(outputPath),
  'candidate-build.json',
);
const candidateBuild = existsSync(candidateBuildPath)
  ? JSON.parse(readFileSync(candidateBuildPath, 'utf8'))
  : null;
const runtimeModelsRestored =
  candidateBuild?.staged?.length === 16 &&
  candidateBuild.staged.every(
    (entry) => entry.runtimeSha256Before === entry.runtimeSha256After,
  );

const result = {
  schemaVersion: 1,
  auditedAt: new Date().toISOString(),
  authorization: path.relative(repository, receiptPath),
  provider: receipt.provider,
  endpoint: receipt.endpoint,
  model: receipt.model,
  modelType: receipt.modelType,
  batch: {
    requestedCalls: receipt.authorizedCalls,
    succeededCalls: generated.length,
    failedCalls: 0,
    rerolls: 0,
    expectedCredits: receipt.expectedMaximumCredits,
    consumedCredits: credits,
    balanceBefore: generated[0].balanceBefore,
    balanceAfter: generated.at(-1).balanceAfter,
    submittedAt: new Date(start).toISOString(),
    finishedAt: new Date(finish).toISOString(),
    elapsedSeconds: (finish - start) / 1000,
    providerStageSeconds,
    averageProviderStageSeconds: providerStageSeconds / generated.length,
    candidateBytes: {
      total: bytes.reduce((sum, value) => sum + value, 0),
      minimum: Math.min(...bytes),
      maximum: Math.max(...bytes),
      average: bytes.reduce((sum, value) => sum + value, 0) / bytes.length,
    },
  },
  reviewContract: {
    targetPlanformSpan: receipt.normalization.targetPlanformSpan,
    modelOwnedTeamGlow: false,
    meshContract: receipt.normalization.meshContract,
    cameraPitchDegrees: 58,
    realReplayScale: true,
    orientationOverrides: Object.fromEntries(orientations),
    runtimeAssetsPromoted: false,
    runtimeModelsRestored,
  },
  pilot: mason,
  generated,
};

mkdirSync(path.dirname(outputPath), { recursive: true });
writeFileSync(outputPath, `${JSON.stringify(result, null, 2)}\n`);
console.log(
  `Audited ${generated.length} batch models (${credits} credits, ${((finish - start) / 60_000).toFixed(2)} minutes elapsed).`,
);

function audit(lookId) {
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

  const orientation = orientations.get(lookId) ?? 'lay-flat-x';
  const filename = lookId === 'arc-mason'
    ? 'mason-normalized-large-review.glb'
    : orientation === 'identity'
      ? 'model-normalized-identity-review.glb'
      : 'model-normalized-review.glb';
  const candidatePath = path.join(runDirectory, filename);
  const candidateBytes = readFileSync(candidatePath);
  const document = glbJson(candidateBytes);
  const normalization = document.nodes?.find(
    (node) => node.extras?.nilbotsProviderNormalization,
  )?.extras?.nilbotsProviderNormalization;
  if (!normalization)
    throw new Error(`${lookId} candidate lacks normalization provenance.`);
  if (
    normalization.orientation !== orientation ||
    normalization.targetPlanformSpan !== receipt.normalization.targetPlanformSpan
  )
    throw new Error(`${lookId} candidate normalization does not match review contract.`);

  const timing = JSON.parse(
    readFileSync(path.join(runDirectory, 'timing.json'), 'utf8'),
  );
  const request = JSON.parse(
    readFileSync(path.join(runDirectory, 'request-record.json'), 'utf8'),
  );
  const validation = validate(candidatePath);
  return {
    lookId,
    taskId: request.taskId,
    submittedAt: timing.submittedAt,
    finishedAt: timing.finishedAt,
    providerStageSeconds: timing.totalSeconds,
    consumedCredits: timing.consumedCredits,
    balanceBefore: timing.balanceBefore,
    balanceAfter: timing.balanceAfter,
    input: request.input,
    orientation,
    candidate: {
      file: path.relative(repository, candidatePath),
      bytes: candidateBytes.length,
      sha256: sha256(candidateBytes),
      triangles: triangleCount(document),
      materials: document.materials?.length ?? 0,
      textures: document.textures?.length ?? 0,
      sourceBounds: normalization.sourceBounds,
    },
    validation,
  };
}

function validate(filename) {
  if (!validator) return { performed: false };
  const result = spawnSync(validator, ['validate', filename], {
    encoding: 'utf8',
  });
  const output = `${result.stdout ?? ''}\n${result.stderr ?? ''}`;
  if (result.error) throw result.error;
  if (result.status !== 0 || !output.includes('No errors found.'))
    throw new Error(`GLB validation failed for ${filename}.`);
  const warnings = [
    ...output.matchAll(/MESH_PRIMITIVE_[A-Z_]+/g),
  ].map((match) => match[0]);
  return {
    performed: true,
    errors: 0,
    warnings: [...new Set(warnings)],
  };
}

function glbJson(bytes) {
  if (bytes.readUInt32LE(0) !== 0x46546c67 || bytes.readUInt32LE(4) !== 2)
    throw new Error('Expected a glTF 2.0 binary.');
  let offset = 12;
  while (offset < bytes.length) {
    const length = bytes.readUInt32LE(offset);
    const type = bytes.readUInt32LE(offset + 4);
    if (type === 0x4e4f534a)
      return JSON.parse(
        bytes.subarray(offset + 8, offset + 8 + length).toString('utf8').trim(),
      );
    offset += 8 + length;
  }
  throw new Error('GLB has no JSON chunk.');
}

function triangleCount(document) {
  return (document.meshes ?? [])
    .flatMap((mesh) => mesh.primitives ?? [])
    .reduce(
      (sum, primitive) =>
        sum + (document.accessors?.[primitive.indices]?.count ?? 0) / 3,
      0,
    );
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
