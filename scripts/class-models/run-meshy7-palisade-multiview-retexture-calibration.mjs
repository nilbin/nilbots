#!/usr/bin/env node

import { execFileSync } from 'node:child_process';
import { createHash } from 'node:crypto';
import { mkdir, readFile, writeFile } from 'node:fs/promises';
import { userInfo } from 'node:os';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const repository = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..', '..');
const check = process.argv.includes('--check');
const planPath = path.join(
  repository,
  'art/class-models/provider-runs/meshy/arc-relay-meshy7-fleet-retexture-v3-calibration/plan.json',
);
const plan = JSON.parse(await readFile(planPath, 'utf8'));
validatePlan(plan);

const manifestPath = path.join(repository, plan.sourceManifest);
const manifest = JSON.parse(await readFile(manifestPath, 'utf8'));
if (manifest.classId !== plan.classId)
  throw new Error('Calibration plan and source manifest class IDs differ.');
const manifestInputs = new Map(manifest.meshyInputs.map((entry) => [entry.order, entry]));
const inputDirectory = path.dirname(manifestPath);
const inputs = [];
for (const expected of [...plan.styleAuthority.inputs].sort((left, right) => left.order - right.order)) {
  const manifestInput = manifestInputs.get(expected.order);
  if (JSON.stringify(manifestInput) !== JSON.stringify(expected))
    throw new Error(`${expected.view}: calibration input drifted from the owning manifest.`);
  const inputPath = path.join(inputDirectory, expected.file);
  const bytes = await readFile(inputPath);
  if (sha256(bytes) !== expected.sha256)
    throw new Error(`${expected.view}: source image SHA-256 drifted.`);
  const dimensions = pngDimensions(bytes);
  if (dimensions.width !== expected.width || dimensions.height !== expected.height)
    throw new Error(`${expected.view}: source image dimensions drifted.`);
  inputs.push({
    ...expected,
    path: inputPath,
    bytes: bytes.length,
    dataUri: `data:image/png;base64,${bytes.toString('base64')}`,
  });
}
if (inputs[0]?.view !== 'strict-front' || inputs[0]?.primary !== true)
  throw new Error('The primary strict-front image must remain first.');

const sourceModelPath = path.join(repository, plan.source.rawModel.file);
const sourceModel = await readFile(sourceModelPath);
if (
  sourceModel.length !== plan.source.rawModel.bytes ||
  sha256(sourceModel) !== plan.source.rawModel.sha256
)
  throw new Error('Palisade source GLB drifted from the fleet audit.');

const apiKey = readApiKey();
const balanceBefore = await balance(apiKey);
if (balanceBefore < plan.creditCap)
  throw new Error(`Meshy balance ${balanceBefore} is below the ${plan.creditCap}-credit cap.`);
const sourceTask = await apiJson(
  `https://api.meshy.ai/openapi/v1/${plan.source.endpoint}/${encodeURIComponent(plan.source.taskId)}`,
  apiKey,
);
if (sourceTask.status !== 'SUCCEEDED' || typeof sourceTask.model_urls?.glb !== 'string')
  throw new Error('Pinned Palisade source task is unavailable or incomplete.');

const settings = {
  model_url: sourceTask.model_urls.glb,
  multiview_image_urls: inputs.map((entry) => entry.dataUri),
  ...plan.request,
};
if (check) {
  console.log(
    `Preflight passed: Palisade source ${sourceModel.length} bytes, ${inputs.length} ordered ` +
      `style views, balance ${balanceBefore}, ${plan.creditCap}-credit cap; no task submitted.`,
  );
  process.exit(0);
}

const startedAt = new Date().toISOString();
const submission = await apiJson('https://api.meshy.ai/openapi/v1/retexture', apiKey, {
  method: 'POST',
  body: JSON.stringify(settings),
});
const taskId = submission.result;
if (typeof taskId !== 'string' || !taskId)
  throw new Error('Meshy did not return a Palisade calibration task ID.');
plan.completedTaskId = taskId;
plan.submittedAt = startedAt;
plan.authorized = false;
plan.authorizationConsumed = true;
plan.status = 'submitted';
await writeJson(planPath, plan);

const runDirectory = path.join(path.dirname(planPath), plan.classId, taskId);
await mkdir(runDirectory, { recursive: true });
await writeJson(path.join(runDirectory, 'request-record.json'), {
  schemaVersion: 1,
  provider: 'Meshy',
  endpoint: 'retexture',
  providerModel: plan.request.ai_model,
  classId: plan.classId,
  taskId,
  submittedAt: startedAt,
  balanceBefore,
  authorizedCreditCap: plan.creditCap,
  sourcePlan: path.relative(repository, planPath),
  sourceManifest: plan.sourceManifest,
  sourceTaskId: plan.source.taskId,
  sourceModel: plan.source.rawModel,
  styleAuthority: {
    kind: plan.styleAuthority.kind,
    referenceRun: plan.styleAuthority.referenceRun,
    inputs: inputs.map(({ dataUri: _dataUri, path: inputPath, ...entry }) => ({
      ...entry,
      file: path.relative(repository, inputPath),
    })),
  },
  settings: sanitizeRequest(settings),
  pricingSource: plan.pricingSource,
  licenseTerms: 'https://www.meshy.ai/terms',
});
console.log(`Palisade calibration submitted: ${taskId}`);

let task;
let previous = '';
while (true) {
  task = await apiJson(
    `https://api.meshy.ai/openapi/v1/retexture/${encodeURIComponent(taskId)}`,
    apiKey,
  );
  const status = `${task.status}:${task.progress ?? 0}`;
  if (status !== previous) console.log(`Meshy ${status}`);
  previous = status;
  if (['SUCCEEDED', 'FAILED', 'CANCELED'].includes(task.status)) break;
  await delay(5_000);
}
await writeJson(path.join(runDirectory, 'task.sanitized.json'), sanitizeTask(task));

const consumedCredits = task.consumed_credits ?? 0;
if (consumedCredits > plan.creditCap)
  throw new Error(`Meshy consumed ${consumedCredits}, above the ${plan.creditCap}-credit cap.`);
const artifacts = [];
if (task.status === 'SUCCEEDED') {
  await download(task.model_urls?.glb, 'raw-model.glb');
  await download(task.thumbnail_url, 'provider-preview.png');
  await download(task.alpha_thumbnail_url, 'provider-preview-alpha.png');
  for (const [index, textures] of (task.texture_urls ?? []).entries())
    for (const [kind, url] of Object.entries(textures ?? {}))
      await download(url, `texture-${index}-${safeName(kind)}${imageExtension(url)}`);
}
await writeJson(path.join(runDirectory, 'artifacts.json'), artifacts);
const balanceAfter = await balance(apiKey);
plan.status = task.status === 'SUCCEEDED' ? 'owner-review' : 'provider-run-incomplete';
plan.finishedAt = new Date().toISOString();
plan.consumedCredits = consumedCredits;
plan.balanceBefore = balanceBefore;
plan.balanceAfter = balanceAfter;
plan.artifacts = artifacts;
await writeJson(planPath, plan);
await writeFile(
  path.join(runDirectory, 'RUN.md'),
  `# Meshy 7 Palisade multiview retexture calibration\n\n` +
    `- Task: \`${taskId}\`\n` +
    `- Style authority: four ordered Palisade views, matching the successful Kestrel route\n` +
    `- Original UVs: true\n` +
    `- Texture: 2K PBR\n` +
    `- Credits: ${consumedCredits} (${balanceBefore} before, ${balanceAfter} after)\n` +
    `- Status: ${task.status}\n\n` +
    `Review-only raw provider result. No runtime asset was promoted.\n`,
);
console.log(
  `Palisade calibration ${task.status}; consumed ${consumedCredits}; balance ${balanceAfter}; ` +
    `${artifacts.length} artifacts downloaded.`,
);
if (task.status !== 'SUCCEEDED') process.exitCode = 1;

async function download(url, filename) {
  if (typeof url !== 'string' || !url) return;
  const response = await fetch(url, { signal: AbortSignal.timeout(120_000) });
  if (!response.ok) throw new Error(`${filename}: Meshy download HTTP ${response.status}.`);
  const bytes = Buffer.from(await response.arrayBuffer());
  await writeFile(path.join(runDirectory, filename), bytes);
  artifacts.push({ file: filename, bytes: bytes.length, sha256: sha256(bytes) });
}

function validatePlan(value) {
  if (
    value.schemaVersion !== 1 ||
    value.status !== 'paid-calibration-authorized' ||
    value.classId !== 'palisade' ||
    value.styleAuthority?.kind !== 'ordered-multiview' ||
    value.styleAuthority.inputs?.length !== 4 ||
    value.request?.ai_model !== 'meshy-7' ||
    value.request.enable_original_uv !== true ||
    value.request.enable_pbr !== true ||
    value.request.texture_resolution !== '2k' ||
    value.request.alpha_thumbnail !== true ||
    value.request.target_formats?.length !== 1 ||
    value.request.target_formats[0] !== 'glb' ||
    value.creditCap !== 10 ||
    value.authorized !== true ||
    value.runtimeAssetsPromoted !== false
  )
    throw new Error('Invalid or already-consumed Palisade calibration authorization.');
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
  const body = await response.text();
  let result;
  try {
    result = JSON.parse(body);
  } catch {
    throw new Error(`Meshy returned non-JSON HTTP ${response.status}.`);
  }
  if (!response.ok) throw new Error(`Meshy HTTP ${response.status}: ${JSON.stringify(result)}`);
  return result;
}

function readApiKey() {
  const value = execFileSync(
    '/usr/bin/security',
    ['find-generic-password', '-s', 'nilbots.meshy.api', '-a', userInfo().username, '-w'],
    { encoding: 'utf8', stdio: ['ignore', 'pipe', 'ignore'] },
  ).trim();
  if (!value) throw new Error('The Meshy API key is missing from macOS Keychain.');
  return value;
}

function sanitizeRequest(request) {
  return Object.fromEntries(
    Object.entries(request)
      .filter(([name]) => name !== 'multiview_image_urls')
      .map(([name, value]) => [name, name === 'model_url' ? sanitizeUrl(value) : value]),
  );
}

function sanitizeTask(value, key = '') {
  if (['image_url', 'image_style_url', 'multiview_image_urls', 'texture_image_url'].includes(key))
    return '[redacted-input]';
  if (Array.isArray(value)) return value.map((entry) => sanitizeTask(entry));
  if (value && typeof value === 'object')
    return Object.fromEntries(
      Object.entries(value).map(([childKey, child]) => [childKey, sanitizeTask(child, childKey)]),
    );
  if (typeof value === 'string' && /^https?:\/\//.test(value)) return sanitizeUrl(value);
  return value;
}

function sanitizeUrl(value) {
  const url = new URL(value);
  url.search = '';
  return url.toString();
}

function pngDimensions(bytes) {
  const signature = Buffer.from([137, 80, 78, 71, 13, 10, 26, 10]);
  if (bytes.length < 24 || !bytes.subarray(0, 8).equals(signature))
    throw new Error('Expected a valid PNG input.');
  return { width: bytes.readUInt32BE(16), height: bytes.readUInt32BE(20) };
}

function imageExtension(url) {
  const pathname = new URL(url).pathname.toLowerCase();
  return pathname.endsWith('.jpg') || pathname.endsWith('.jpeg') ? '.jpg' : '.png';
}

function safeName(value) {
  return String(value).replace(/[^a-z0-9_-]+/gi, '-').toLowerCase();
}

function sha256(value) {
  return createHash('sha256').update(value).digest('hex');
}

function delay(milliseconds) {
  return new Promise((resolveDelay) => setTimeout(resolveDelay, milliseconds));
}

async function writeJson(file, value) {
  await mkdir(path.dirname(file), { recursive: true });
  await writeFile(file, `${JSON.stringify(value, null, 2)}\n`);
}
