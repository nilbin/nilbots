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
  'art/class-models/provider-runs/meshy/arc-relay-meshy7-fleet-retexture-v3-multiview/plan.json',
);
const plan = JSON.parse(await readFile(planPath, 'utf8'));
validatePlan(plan);

const prepared = new Map();
for (const entry of plan.entries) {
  const sourcePath = path.join(repository, entry.source.rawModel.file);
  const source = await readFile(sourcePath);
  if (source.length !== entry.source.rawModel.bytes || sha256(source) !== entry.source.rawModel.sha256)
    throw new Error(`${entry.classId}: source GLB drifted.`);
  const inputs = [];
  for (const expected of [...entry.styleAuthority.inputs].sort((left, right) => left.order - right.order)) {
    const inputPath = path.join(repository, expected.file);
    const bytes = await readFile(inputPath);
    if (sha256(bytes) !== expected.sha256)
      throw new Error(`${entry.classId}/${expected.view}: style image drifted.`);
    const dimensions = pngDimensions(bytes);
    if (dimensions.width !== expected.width || dimensions.height !== expected.height)
      throw new Error(`${entry.classId}/${expected.view}: style dimensions drifted.`);
    inputs.push({
      ...expected,
      bytes: bytes.length,
      dataUri: `data:image/png;base64,${bytes.toString('base64')}`,
    });
  }
  if (inputs[0]?.view !== 'strict-front' || inputs[0]?.primary !== true)
    throw new Error(`${entry.classId}: primary strict-front image is not first.`);
  prepared.set(entry.classId, { sourcePath, source, inputs });
}

const apiKey = readApiKey();
const balanceBefore = await balance(apiKey);
const pending = plan.entries.filter((entry) => !entry.completedTaskId);
const pendingCap = pending.reduce((sum, entry) => sum + entry.creditCap, 0);
if (balanceBefore < pendingCap)
  throw new Error(`Meshy balance ${balanceBefore} is below pending cap ${pendingCap}.`);

const sourceTasks = new Map();
await mapLimit(plan.entries, 5, async (entry) => {
  const task = await apiJson(
    `https://api.meshy.ai/openapi/v1/${entry.source.endpoint}/${encodeURIComponent(entry.source.taskId)}`,
    apiKey,
  );
  if (task.status !== 'SUCCEEDED' || typeof task.model_urls?.glb !== 'string')
    throw new Error(`${entry.classId}: source task is unavailable or incomplete.`);
  sourceTasks.set(entry.classId, task);
});

if (check) {
  console.log(
    `Preflight passed: ${plan.entries.length} sources, ${plan.entries.length * 4} brighter ` +
      `ordered style images, ${pending.length} pending tasks, balance ${balanceBefore}, ` +
      `${pendingCap}-credit cap; no tasks submitted.`,
  );
  process.exit(0);
}

for (const entry of pending) {
  const sourceTask = sourceTasks.get(entry.classId);
  const inputs = prepared.get(entry.classId).inputs;
  const settings = {
    model_url: sourceTask.model_urls.glb,
    multiview_image_urls: inputs.map((input) => input.dataUri),
    ...entry.request,
  };
  const submission = await apiJson('https://api.meshy.ai/openapi/v1/retexture', apiKey, {
    method: 'POST',
    body: JSON.stringify(settings),
  });
  const taskId = submission.result;
  if (typeof taskId !== 'string' || !taskId)
    throw new Error(`${entry.classId}: Meshy did not return a task ID.`);
  entry.completedTaskId = taskId;
  entry.submittedAt = new Date().toISOString();
  entry.authorized = false;
  entry.authorizationConsumed = true;
  entry.status = 'SUBMITTED';
  const runDirectory = runPath(entry, taskId);
  await mkdir(runDirectory, { recursive: true });
  await writeJson(path.join(runDirectory, 'request-record.json'), {
    schemaVersion: 1,
    provider: 'Meshy',
    endpoint: 'retexture',
    providerModel: entry.request.ai_model,
    classId: entry.classId,
    taskId,
    submittedAt: entry.submittedAt,
    balanceBeforeBatch: balanceBefore,
    authorizedCreditCap: entry.creditCap,
    sourcePlan: path.relative(repository, planPath),
    sourceTaskId: entry.source.taskId,
    sourceModel: entry.source.rawModel,
    styleAuthority: {
      kind: entry.styleAuthority.kind,
      materialPolicy: plan.materialPolicy,
      valueAudit: entry.styleAuthority.valueAudit,
      inputs: inputs.map(({ dataUri: _dataUri, ...input }) => input),
    },
    settings: sanitizeRequest(settings),
    pricingSource: plan.balanceCheck.pricingSource,
    licenseTerms: 'https://www.meshy.ai/terms',
  });
  await writePlan();
  console.log(`${entry.classId}: submitted ${taskId}`);
  await delay(250);
}

const active = plan.entries.filter(
  (entry) => entry.completedTaskId && !['SUCCEEDED', 'FAILED', 'CANCELED'].includes(entry.status),
);
const statusCache = new Map();
while (active.length) {
  const results = await mapLimit(active, 5, async (entry) => ({
    entry,
    task: await apiJson(
      `https://api.meshy.ai/openapi/v1/retexture/${encodeURIComponent(entry.completedTaskId)}`,
      apiKey,
    ),
  }));
  for (const { entry, task } of results) {
    const signature = `${task.status}:${task.progress ?? 0}`;
    if (statusCache.get(entry.classId) !== signature) {
      statusCache.set(entry.classId, signature);
      console.log(`${entry.classId}: Meshy ${signature}`);
    }
    if (!['SUCCEEDED', 'FAILED', 'CANCELED'].includes(task.status)) continue;
    entry.status = task.status;
    entry.consumedCredits = task.consumed_credits ?? 0;
    entry.finishedAt = new Date().toISOString();
  }
  for (let index = active.length - 1; index >= 0; index -= 1)
    if (['SUCCEEDED', 'FAILED', 'CANCELED'].includes(active[index].status)) active.splice(index, 1);
  await writePlan();
  if (active.length) await delay(5_000);
}

const downloadCandidates = plan.entries.filter(
  (entry) => entry.completedTaskId && entry.status === 'SUCCEEDED' && !entry.artifacts,
);
await mapLimit(downloadCandidates, 3, async (entry) => {
  const task = await apiJson(
    `https://api.meshy.ai/openapi/v1/retexture/${encodeURIComponent(entry.completedTaskId)}`,
    apiKey,
  );
  if (task.status !== 'SUCCEEDED') throw new Error(`${entry.classId}: completed task regressed.`);
  if ((task.consumed_credits ?? 0) > entry.creditCap)
    throw new Error(`${entry.classId}: consumed credits exceed authorization.`);
  const runDirectory = runPath(entry, entry.completedTaskId);
  await writeJson(path.join(runDirectory, 'task.sanitized.json'), sanitizeTask(task));
  const artifacts = [];
  await download(task.model_urls?.glb, 'raw-model.glb');
  await download(task.thumbnail_url, 'provider-preview.png');
  await download(task.alpha_thumbnail_url, 'provider-preview-alpha.png');
  for (const [index, textures] of (task.texture_urls ?? []).entries())
    for (const [kind, url] of Object.entries(textures ?? {}))
      await download(url, `texture-${index}-${safeName(kind)}${imageExtension(url)}`);
  await writeJson(path.join(runDirectory, 'artifacts.json'), artifacts);
  await writeFile(
    path.join(runDirectory, 'RUN.md'),
    `# Meshy 7 brighter multiview fleet retexture: ${entry.classId}\n\n` +
      `- Task: \`${entry.completedTaskId}\`\n` +
      `- Style authority: four ordered brighter-v1 class views\n` +
      `- Original UVs: true\n` +
      `- Texture: 2K PBR\n` +
      `- Credits: ${task.consumed_credits}\n` +
      `- Status: ${task.status}\n\n` +
      `Review-only raw provider result. No runtime asset was promoted.\n`,
  );
  entry.artifacts = artifacts;

  async function download(url, filename) {
    if (typeof url !== 'string' || !url) return;
    const response = await fetch(url, { signal: AbortSignal.timeout(120_000) });
    if (!response.ok) throw new Error(`${entry.classId}: ${filename} HTTP ${response.status}.`);
    const bytes = Buffer.from(await response.arrayBuffer());
    await writeFile(path.join(runDirectory, filename), bytes);
    artifacts.push({ file: filename, bytes: bytes.length, sha256: sha256(bytes) });
  }
});

const balanceAfter = await balance(apiKey);
plan.balanceBefore = plan.balanceBefore ?? balanceBefore;
plan.balanceAfter = balanceAfter;
plan.totalConsumedCredits = plan.entries.reduce(
  (sum, entry) => sum + (entry.consumedCredits ?? 0),
  0,
);
plan.status = plan.entries.every((entry) => entry.status === 'SUCCEEDED' && entry.artifacts)
  ? 'owner-review'
  : 'provider-run-incomplete';
await writePlan();
console.log(
  `Multiview fleet batch ${plan.status}; consumed ${plan.totalConsumedCredits}; ` +
    `balance ${balanceAfter}.`,
);

function validatePlan(value) {
  if (
    value.schemaVersion !== 1 ||
    value.endpoint !== '/openapi/v1/retexture' ||
    value.runtimeAssetsPromoted !== false ||
    !Array.isArray(value.entries) ||
    value.entries.length !== 14 ||
    value.materialPolicy?.finish !== 'matte/satin high-roughness industrial surfaces' ||
    !value.materialPolicy?.sourceDirection?.includes('no toy gloss') ||
    !value.materialPolicy?.enforcement?.includes('no competing text-style input') ||
    new Set(value.entries.map((entry) => entry.classId)).size !== 14 ||
    value.entries.some((entry) => ['kestrel', 'palisade'].includes(entry.classId))
  )
    throw new Error('Multiview V3 plan must contain fourteen unique remaining classes.');
  for (const entry of value.entries) {
    if (
      entry.styleAuthority?.kind !== 'ordered-multiview-brighter-v1' ||
      entry.styleAuthority.inputs?.length !== 4 ||
      entry.styleAuthority.inputs[0]?.view !== 'strict-front' ||
      entry.styleAuthority.inputs[0]?.primary !== true ||
      entry.request.ai_model !== 'meshy-7' ||
      entry.request.enable_original_uv !== true ||
      entry.request.enable_pbr !== true ||
      entry.request.texture_resolution !== '2k' ||
      entry.request.alpha_thumbnail !== true ||
      entry.request.target_formats?.length !== 1 ||
      entry.request.target_formats[0] !== 'glb' ||
      entry.creditCap !== 10 ||
      (!entry.completedTaskId && entry.authorized !== true)
    )
      throw new Error(`${entry.classId}: invalid or unauthorized multiview request.`);
  }
}

function runPath(entry, taskId) {
  return path.join(path.dirname(planPath), entry.classId, taskId);
}

async function writePlan() {
  await writeJson(planPath, plan);
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

async function mapLimit(values, limit, operation) {
  const results = new Array(values.length);
  let next = 0;
  await Promise.all(
    Array.from({ length: Math.min(limit, values.length) }, async () => {
      while (next < values.length) {
        const index = next;
        next += 1;
        results[index] = await operation(values[index], index);
      }
    }),
  );
  return results;
}

async function writeJson(file, value) {
  await mkdir(path.dirname(file), { recursive: true });
  await writeFile(file, `${JSON.stringify(value, null, 2)}\n`);
}
