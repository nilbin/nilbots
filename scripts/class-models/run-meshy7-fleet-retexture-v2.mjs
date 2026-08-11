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
  'art/class-models/provider-runs/meshy/arc-relay-meshy7-fleet-retexture-v2/plan.json',
);
const plan = JSON.parse(await readFile(planPath, 'utf8'));
validatePlan(plan);

const key = readApiKey();
const balanceBefore = await balance(key);
const pending = plan.entries.filter((entry) => !entry.completedTaskId);
const pendingCap = pending.reduce((sum, entry) => sum + entry.creditCap, 0);
console.log(`Balance before: ${balanceBefore}; pending cap: ${pendingCap}`);
if (balanceBefore < pendingCap)
  throw new Error(`Meshy balance ${balanceBefore} is below the pending ${pendingCap}-credit cap.`);

const sourceTasks = new Map();
for (const entry of plan.entries) {
  const modelPath = path.join(repository, entry.source.rawModel.file);
  const model = await readFile(modelPath);
  if (model.length !== entry.source.rawModel.bytes || sha256(model) !== entry.source.rawModel.sha256)
    throw new Error(`${entry.classId}: source GLB drifted from the audited Meshy 7 model.`);
  const task = await apiJson(
    `https://api.meshy.ai/openapi/v1/${entry.source.endpoint}/${encodeURIComponent(entry.source.taskId)}`,
    key,
  );
  if (task.status !== 'SUCCEEDED' || typeof task.model_urls?.glb !== 'string')
    throw new Error(`${entry.classId}: source task is unavailable or incomplete.`);
  sourceTasks.set(entry.classId, task);
}

if (check) {
  console.log(
    `Preflight passed: ${plan.entries.length} source models, ${pending.length} pending 2K ` +
      `Meshy 7 retextures, balance ${balanceBefore}; no tasks submitted.`,
  );
  process.exit(0);
}

for (const entry of pending) {
  const sourceTask = sourceTasks.get(entry.classId);
  const settings = { model_url: sourceTask.model_urls.glb, ...entry.request };
  const startedAt = new Date();
  const submission = await apiJson('https://api.meshy.ai/openapi/v1/retexture', key, {
    method: 'POST',
    body: JSON.stringify(settings),
  });
  const taskId = submission.result;
  if (typeof taskId !== 'string' || !taskId)
    throw new Error(`${entry.classId}: Meshy did not return a retexture task ID.`);
  entry.completedTaskId = taskId;
  entry.submittedAt = startedAt.toISOString();
  entry.authorized = false;
  entry.authorizationConsumed = true;
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
      key,
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
    entry._task = task;
  }
  for (let index = active.length - 1; index >= 0; index -= 1)
    if (['SUCCEEDED', 'FAILED', 'CANCELED'].includes(active[index].status)) active.splice(index, 1);
  await writePlan();
  if (active.length) await delay(5_000);
}

const completed = plan.entries.filter((entry) => entry._task);
await mapLimit(completed, 3, async (entry) => {
  const task = entry._task;
  const runDirectory = runPath(entry, entry.completedTaskId);
  await writeJson(path.join(runDirectory, 'task.sanitized.json'), sanitizeTask(task));
  if (task.status !== 'SUCCEEDED') return;
  if ((task.consumed_credits ?? 0) > entry.creditCap)
    throw new Error(`${entry.classId}: consumed credits exceed its authorization.`);
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
    `# Meshy 7 restrained fleet retexture: ${entry.classId}\n\n` +
      `- Task: \`${entry.completedTaskId}\`\n` +
      `- Source task: \`${entry.source.taskId}\`\n` +
      `- Texture: 2K PBR, original UVs retained\n` +
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

for (const entry of plan.entries) delete entry._task;
const balanceAfter = await balance(key);
plan.balanceAfter = balanceAfter;
plan.totalConsumedCredits = plan.entries.reduce(
  (sum, entry) => sum + (entry.consumedCredits ?? 0),
  0,
);
plan.status = plan.entries.every((entry) => entry.status === 'SUCCEEDED')
  ? 'owner-review'
  : 'provider-run-incomplete';
await writePlan();
console.log(`Retexture batch ${plan.status}; consumed ${plan.totalConsumedCredits}; balance ${balanceAfter}.`);

function validatePlan(value) {
  if (
    value.schemaVersion !== 1 ||
    value.endpoint !== '/openapi/v1/retexture' ||
    value.runtimeAssetsPromoted !== false ||
    !Array.isArray(value.entries) ||
    value.entries.length !== 15 ||
    new Set(value.entries.map((entry) => entry.classId)).size !== 15 ||
    value.entries.some((entry) => entry.classId === 'kestrel')
  )
    throw new Error('Retexture plan must contain the fifteen unique non-Kestrel classes.');
  for (const entry of value.entries) {
    if (
      entry.request.ai_model !== 'meshy-7' ||
      entry.request.enable_original_uv !== true ||
      entry.request.enable_pbr !== true ||
      entry.request.texture_resolution !== '2k' ||
      entry.request.alpha_thumbnail !== true ||
      entry.request.target_formats?.length !== 1 ||
      entry.request.target_formats[0] !== 'glb' ||
      entry.request.text_style_prompt?.length > 600 ||
      entry.creditCap !== 10 ||
      (!entry.completedTaskId && entry.authorized !== true)
    )
      throw new Error(`${entry.classId}: invalid or unauthorized retexture request.`);
  }
}

function runPath(entry, taskId) {
  return path.join(path.dirname(planPath), entry.classId, taskId);
}

async function writePlan() {
  const clean = structuredClone(plan);
  for (const entry of clean.entries) delete entry._task;
  await writeJson(planPath, clean);
}

async function balance(apiKey) {
  const result = await apiJson('https://api.meshy.ai/openapi/v1/balance', apiKey);
  const value = result.balance ?? result.result;
  if (!Number.isFinite(value)) throw new Error('Meshy returned an invalid balance.');
  return value;
}

async function apiJson(url, apiKey, init = {}) {
  const response = await fetch(url, {
    ...init,
    headers: {
      Authorization: `Bearer ${apiKey}`,
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
  return { ...request, model_url: sanitizeUrl(request.model_url) };
}

function sanitizeTask(value, key = '') {
  if (key === 'image_url' || key === 'image_style_url' || key === 'multiview_image_urls')
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

function safeName(value) {
  return String(value).replace(/[^a-z0-9_-]+/gi, '-').toLowerCase();
}

function imageExtension(url) {
  if (typeof url !== 'string') return '.png';
  const pathname = new URL(url).pathname.toLowerCase();
  return pathname.endsWith('.jpg') || pathname.endsWith('.jpeg') ? '.jpg' : '.png';
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
