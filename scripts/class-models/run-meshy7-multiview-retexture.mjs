#!/usr/bin/env node

import { execFileSync } from 'node:child_process';
import { createHash } from 'node:crypto';
import { mkdir, readFile, writeFile } from 'node:fs/promises';
import { userInfo } from 'node:os';
import { dirname, join, relative, resolve } from 'node:path';
import process from 'node:process';

const repositoryRoot = resolve(import.meta.dirname, '../..');
const options = parseArgs(process.argv.slice(2));
const manifestPath = resolve(repositoryRoot, options.manifest);
const manifest = JSON.parse(await readFile(manifestPath, 'utf8'));
const plan = manifest.retexturePlan;
const runCollection = manifest.retextureRunCollection ?? 'arc-relay-meshy7-retexture';

validateManifest(manifest, plan);

const inputs = [];
for (const input of [...manifest.meshyInputs].sort((left, right) => left.order - right.order)) {
  const path = resolve(dirname(manifestPath), input.file);
  const bytes = await readFile(path);
  const hash = sha256(bytes);
  if (hash !== input.sha256)
    throw new Error(`${input.file} SHA-256 mismatch: expected ${input.sha256}, got ${hash}.`);
  const dimensions = pngDimensions(bytes);
  if (dimensions.width !== input.width || dimensions.height !== input.height)
    throw new Error(
      `${input.file} dimensions mismatch: expected ${input.width}x${input.height}, ` +
        `got ${dimensions.width}x${dimensions.height}.`,
    );
  inputs.push({
    order: input.order,
    view: input.view,
    primary: input.primary,
    file: relative(repositoryRoot, path),
    bytes: bytes.length,
    sha256: hash,
    width: dimensions.width,
    height: dimensions.height,
    dataUri: `data:image/png;base64,${bytes.toString('base64')}`,
  });
}

if (inputs[0]?.primary !== true || inputs[0]?.view !== 'strict-front')
  throw new Error('The first Meshy 7 style input must be the primary strict-front view.');

const sourceDirectory = resolve(dirname(manifestPath), manifest.providerRun.directory);
const sourceModelPath = join(sourceDirectory, 'raw-model.glb');
const sourceModelBytes = await readFile(sourceModelPath);
const sourceModelHash = sha256(sourceModelBytes);
if (sourceModelHash !== plan.sourceRawModelSha256)
  throw new Error(
    `Source model SHA-256 mismatch: expected ${plan.sourceRawModelSha256}, got ${sourceModelHash}.`,
  );

const apiKey = readApiKey();
const startedAt = new Date();
const timerStart = process.hrtime.bigint();
const balanceBefore = await balance(apiKey);
console.log(`Balance before: ${balanceBefore}`);
if (balanceBefore < plan.creditCap)
  throw new Error(
    `Meshy balance ${balanceBefore} is below the authorized ${plan.creditCap}-credit cap.`,
  );

const sourceTask = await apiJson(
  `https://api.meshy.ai/openapi/v1/multi-image-to-3d/${encodeURIComponent(plan.sourceTaskId)}`,
  apiKey,
);
if (sourceTask.status !== 'SUCCEEDED')
  throw new Error(`Source task ${plan.sourceTaskId} is ${sourceTask.status}, not SUCCEEDED.`);
if (sourceTask.id !== plan.sourceTaskId)
  throw new Error(`Meshy returned the wrong source task: ${sourceTask.id ?? 'missing id'}.`);
const sourceModelUrl = sourceTask.model_urls?.glb;
if (typeof sourceModelUrl !== 'string' || sourceModelUrl.length === 0)
  throw new Error(`Source task ${plan.sourceTaskId} has no GLB URL.`);

const settings = {
  model_url: sourceModelUrl,
  multiview_image_urls: inputs.map((input) => input.dataUri),
  ai_model: plan.ai_model,
  enable_original_uv: plan.enable_original_uv,
  enable_pbr: plan.enable_pbr,
  texture_resolution: plan.texture_resolution,
  alpha_thumbnail: plan.alpha_thumbnail,
  target_formats: plan.target_formats,
};

if (options.check) {
  console.log(
    `Preflight passed: ${inputs.length} source views, source task ${plan.sourceTaskId}, ` +
      `source GLB ${sourceModelBytes.length} bytes, balance ${balanceBefore}, ` +
      `${plan.creditCap}-credit cap. No task submitted.`,
  );
  process.exit(0);
}

const submitStart = process.hrtime.bigint();
const submission = await apiJson(
  'https://api.meshy.ai/openapi/v1/retexture',
  apiKey,
  { method: 'POST', body: JSON.stringify(settings) },
);
const submitSeconds = elapsedSeconds(submitStart);
const taskId = submission.result;
if (typeof taskId !== 'string' || taskId.length === 0)
  throw new Error(`Meshy did not return a task id: ${JSON.stringify(submission)}`);

const runDirectory = resolve(
  repositoryRoot,
  'art/class-models/provider-runs/meshy',
  runCollection,
  manifest.classId,
  taskId,
);
await mkdir(runDirectory, { recursive: true });
await writeJson(join(runDirectory, 'request-record.json'), {
  version: 1,
  provider: 'Meshy',
  endpoint: 'retexture',
  providerModel: plan.ai_model,
  classId: manifest.classId,
  taskId,
  submittedAt: startedAt.toISOString(),
  balanceBefore,
  authorizedCreditCap: plan.creditCap,
  sourceManifest: relative(repositoryRoot, manifestPath),
  sourceTaskId: plan.sourceTaskId,
  sourceModel: {
    file: relative(repositoryRoot, sourceModelPath),
    bytes: sourceModelBytes.length,
    sha256: sourceModelHash,
  },
  inputs: inputs.map(({ dataUri: _dataUri, ...input }) => input),
  settings: sanitizeRequest(settings),
  pricingSource: manifest.creditCheck?.pricingSource,
  licenseTerms: 'https://www.meshy.ai/terms',
});
console.log(`Submitted ${taskId} in ${submitSeconds.toFixed(2)}s`);

const generationStart = process.hrtime.bigint();
let task;
let lastProgress = -1;
let lastStatus = '';
while (true) {
  task = await apiJson(
    `https://api.meshy.ai/openapi/v1/retexture/${encodeURIComponent(taskId)}`,
    apiKey,
  );
  if (task.progress !== lastProgress || task.status !== lastStatus) {
    lastProgress = task.progress;
    lastStatus = task.status;
    console.log(`Meshy ${task.status}: ${task.progress ?? 0}%`);
  }
  if (['SUCCEEDED', 'FAILED', 'CANCELED'].includes(task.status)) break;
  await delay(2_000);
}
const generationSeconds = elapsedSeconds(generationStart);
await writeJson(join(runDirectory, 'task.sanitized.json'), sanitizeTask(task));

if (task.status !== 'SUCCEEDED') {
  await writeJson(join(runDirectory, 'timing.json'), {
    submittedAt: startedAt.toISOString(),
    finishedAt: new Date().toISOString(),
    submitSeconds,
    generationSeconds,
    totalSeconds: elapsedSeconds(timerStart),
    status: task.status,
    consumedCredits: task.consumed_credits ?? 0,
    balanceBefore,
    balanceAfter: await balance(apiKey),
  });
  throw new Error(`Meshy task ${taskId} ended with ${task.status}.`);
}

if (task.consumed_credits > plan.creditCap)
  throw new Error(
    `Meshy reported ${task.consumed_credits} consumed credits, exceeding the ` +
      `${plan.creditCap}-credit authorization.`,
  );

const downloadStart = process.hrtime.bigint();
const artifacts = [];
await downloadArtifact(task.model_urls?.glb, 'raw-model.glb');
await downloadArtifact(task.thumbnail_url, 'provider-preview.png');
await downloadArtifact(task.alpha_thumbnail_url, 'provider-preview-alpha.png');
for (const [view, url] of Object.entries(task.thumbnail_urls ?? {}))
  await downloadArtifact(url, `provider-${safeName(view)}.png`);
for (const [index, textures] of (task.texture_urls ?? []).entries())
  for (const [kind, url] of Object.entries(textures ?? {}))
    await downloadArtifact(url, `texture-${index}-${safeName(kind)}${imageExtension(url)}`);
const downloadSeconds = elapsedSeconds(downloadStart);
const balanceAfter = await balance(apiKey);
const totalSeconds = elapsedSeconds(timerStart);
const timing = {
  submittedAt: startedAt.toISOString(),
  finishedAt: new Date().toISOString(),
  submitSeconds,
  generationSeconds,
  downloadSeconds,
  totalSeconds,
  status: task.status,
  consumedCredits: task.consumed_credits,
  balanceBefore,
  balanceAfter,
};
await writeJson(join(runDirectory, 'timing.json'), timing);
await writeJson(join(runDirectory, 'artifacts.json'), artifacts);
await writeFile(
  join(runDirectory, 'RUN.md'),
  `# Meshy 7 multiview retexture: ${manifest.classId}\n\n` +
    `- Task: \`${taskId}\`\n` +
    `- Endpoint: \`retexture\`\n` +
    `- Model: \`${plan.ai_model}\`\n` +
    `- Source task: \`${plan.sourceTaskId}\`\n` +
    `- Style inputs: ${inputs.length}, primary view \`${inputs[0].view}\`\n` +
    `- Original UVs: ${plan.enable_original_uv}\n` +
    `- Textures: ${plan.texture_resolution} PBR\n` +
    `- Status: ${task.status}\n` +
    `- Credits: ${task.consumed_credits} (${balanceBefore} before, ${balanceAfter} after)\n` +
    `- API generation: ${generationSeconds.toFixed(2)} seconds\n` +
    `- Submit through complete download: ${totalSeconds.toFixed(2)} seconds\n\n` +
    `This is a raw retextured base-mesh candidate, not an accepted runtime look. ` +
    `Signed URLs, credentials, and input data URIs are not retained.\n`,
);

const rawModel = artifacts.find((artifact) => artifact.file === 'raw-model.glb');
plan.authorized = false;
plan.authorizationConsumed = true;
plan.completedTaskId = taskId;
plan.consumedCredits = task.consumed_credits;
manifest.status = 'owner-review';
manifest.retextureRun = {
  taskId,
  directory: relative(dirname(manifestPath), runDirectory),
  status: task.status,
  consumedCredits: task.consumed_credits,
  balanceBefore,
  balanceAfter,
  rawModelSha256: rawModel?.sha256,
};
manifest.nextGate = 'owner review of corrected Meshy 7 material pass; no runtime promotion';
await writeJson(manifestPath, manifest);

console.log(`Downloaded ${artifacts.length} artifacts in ${downloadSeconds.toFixed(2)}s`);
console.log(`Credits: ${task.consumed_credits}; balance after: ${balanceAfter}`);
console.log(`Provider stage total: ${totalSeconds.toFixed(2)}s`);
console.log(`Run directory: ${relative(repositoryRoot, runDirectory)}`);

async function downloadArtifact(url, filename) {
  if (typeof url !== 'string' || url.length === 0) return;
  const response = await fetch(url, { signal: AbortSignal.timeout(120_000) });
  if (!response.ok)
    throw new Error(`Download ${filename} failed: HTTP ${response.status}.`);
  const bytes = Buffer.from(await response.arrayBuffer());
  await writeFile(join(runDirectory, filename), bytes);
  artifacts.push({ file: filename, bytes: bytes.length, sha256: sha256(bytes) });
}

async function balance(key) {
  const result = await apiJson('https://api.meshy.ai/openapi/v1/balance', key);
  const value = result.balance ?? result.result;
  if (!Number.isFinite(value)) throw new Error('Meshy returned an invalid balance.');
  return value;
}

async function apiJson(url, key, init = {}) {
  const response = await fetch(url, {
    ...init,
    headers: {
      Authorization: `Bearer ${key}`,
      'Content-Type': 'application/json',
      ...(init.headers ?? {}),
    },
    signal: AbortSignal.timeout(120_000),
  });
  const responseText = await response.text();
  let result;
  try {
    result = JSON.parse(responseText);
  } catch {
    throw new Error(`Meshy returned non-JSON HTTP ${response.status}.`);
  }
  if (!response.ok)
    throw new Error(`Meshy HTTP ${response.status}: ${JSON.stringify(result)}`);
  return result;
}

function readApiKey() {
  const account = userInfo().username;
  const value = execFileSync(
    '/usr/bin/security',
    ['find-generic-password', '-s', 'nilbots.meshy.api', '-a', account, '-w'],
    { encoding: 'utf8', stdio: ['ignore', 'pipe', 'ignore'] },
  ).trim();
  if (!value) throw new Error('The Meshy API key is missing from macOS Keychain.');
  return value;
}

function sanitizeRequest(request) {
  return Object.fromEntries(
    Object.entries(request)
      .filter(([key]) => key !== 'multiview_image_urls')
      .map(([key, value]) => [key, key === 'model_url' ? sanitizeUrl(value) : value]),
  );
}

function sanitizeTask(value, key = '') {
  if (
    key === 'image_urls' ||
    key === 'image_url' ||
    key === 'multiview_image_urls' ||
    key === 'texture_image_url'
  )
    return '[redacted-input]';
  if (Array.isArray(value)) return value.map((entry) => sanitizeTask(entry));
  if (value && typeof value === 'object')
    return Object.fromEntries(
      Object.entries(value).map(([childKey, child]) => [
        childKey,
        sanitizeTask(child, childKey),
      ]),
    );
  if (typeof value === 'string' && /^https?:\/\//.test(value)) return sanitizeUrl(value);
  return value;
}

function sanitizeUrl(value) {
  const url = new URL(value);
  url.search = '';
  return url.toString();
}

function validateManifest(value, requestPlan) {
  if (!/^[a-z][a-z0-9-]*$/.test(value?.classId ?? ''))
    throw new Error('The manifest classId is missing or unsafe.');
  if (!Array.isArray(value.meshyInputs) || value.meshyInputs.length !== 4)
    throw new Error('Exactly four Meshy style inputs are required.');
  if (!requestPlan?.authorized)
    throw new Error('The manifest does not authorize a paid Meshy retexture.');
  if (requestPlan.completedTaskId || requestPlan.authorizationConsumed)
    throw new Error('The paid Meshy retexture authorization has already been consumed.');
  if (requestPlan.endpoint !== '/openapi/v1/retexture')
    throw new Error('The authorized endpoint must be Meshy retexture.');
  if (requestPlan.sourceTaskId !== value.providerRun?.taskId)
    throw new Error('The retexture source task does not match the recorded provider run.');
  if (requestPlan.sourceRawModelSha256 !== value.providerRun?.rawModelSha256)
    throw new Error('The retexture source model hash does not match the recorded provider run.');
  if (requestPlan.ai_model !== 'meshy-7') throw new Error('ai_model must be meshy-7.');
  if (requestPlan.texture_resolution !== '2k')
    throw new Error('The authorized retexture resolution is 2k.');
  if (requestPlan.enable_original_uv !== true || requestPlan.enable_pbr !== true)
    throw new Error('The authorized retexture requires original UVs and PBR texturing.');
  if (requestPlan.alpha_thumbnail !== true)
    throw new Error('The authorized retexture requests an alpha thumbnail.');
  if (requestPlan.creditCap !== 10)
    throw new Error('The authorized retexture credit cap must be exactly 10.');
  if (
    !Array.isArray(requestPlan.target_formats) ||
    requestPlan.target_formats.length !== 1 ||
    requestPlan.target_formats[0] !== 'glb'
  )
    throw new Error('The retexture requests GLB output only.');
}

function parseArgs(args) {
  let manifestArg;
  let check = false;
  for (let index = 0; index < args.length; index += 1) {
    if (args[index] === '--check') {
      check = true;
      continue;
    }
    if (args[index] === '--manifest') {
      manifestArg = args[index + 1];
      index += 1;
      continue;
    }
    throw new Error(`Unknown argument: ${args[index]}`);
  }
  if (!manifestArg)
    throw new Error(
      'Usage: node scripts/class-models/run-meshy7-multiview-retexture.mjs ' +
        '--manifest <manifest.json> [--check]',
    );
  return { manifest: manifestArg, check };
}

function pngDimensions(bytes) {
  const signature = Buffer.from([137, 80, 78, 71, 13, 10, 26, 10]);
  if (bytes.length < 24 || !bytes.subarray(0, 8).equals(signature))
    throw new Error('Input is not a valid PNG.');
  return { width: bytes.readUInt32BE(16), height: bytes.readUInt32BE(20) };
}

function imageExtension(url) {
  if (typeof url !== 'string') return '.png';
  const pathname = new URL(url).pathname.toLowerCase();
  return pathname.endsWith('.jpg') || pathname.endsWith('.jpeg') ? '.jpg' : '.png';
}

function safeName(value) {
  return String(value).replace(/[^a-z0-9_-]+/gi, '-').toLowerCase();
}

function sha256(bytes) {
  return createHash('sha256').update(bytes).digest('hex');
}

function elapsedSeconds(start) {
  return Number(process.hrtime.bigint() - start) / 1_000_000_000;
}

function delay(milliseconds) {
  return new Promise((resolveDelay) => setTimeout(resolveDelay, milliseconds));
}

async function writeJson(path, value) {
  await writeFile(path, `${JSON.stringify(value, null, 2)}\n`);
}
