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
  'art/class-models/provider-runs/meshy/arc-relay-meshy7-signature-props-retexture-v3/plan.json',
);
const plan = JSON.parse(await readFile(planPath, 'utf8'));
validatePlan(plan);

const entry = plan.entries[0];
const key = readApiKey();
const balanceBefore = await balance(key);
const pendingCap = entry.completedTaskId ? 0 : entry.creditCap;
console.log(`Balance before: ${balanceBefore}; pending cap: ${pendingCap}`);
if (balanceBefore < pendingCap)
  throw new Error(`Meshy balance ${balanceBefore} is below the pending ${pendingCap}-credit cap.`);

const sourcePath = path.join(repository, entry.source.rawModel.file);
const sourceBytes = await readFile(sourcePath);
if (
  sourceBytes.length !== entry.source.rawModel.bytes ||
  sha256(sourceBytes) !== entry.source.rawModel.sha256
)
  throw new Error('Trip Node source GLB drifted from the audited Meshy 7 model.');
const sourceTask = await apiJson(
  `https://api.meshy.ai/openapi/v1/${entry.source.endpoint}/${encodeURIComponent(entry.source.taskId)}`,
  key,
);
if (sourceTask.status !== 'SUCCEEDED' || typeof sourceTask.model_urls?.glb !== 'string')
  throw new Error('Trip Node source task is unavailable or incomplete.');

plan.balanceCheck.checkedAt = new Date().toISOString();
plan.balanceCheck.balance = balanceBefore;
if (check) {
  console.log(
    `Preflight passed: audited source, one pending 2K original-UV retexture, ` +
      `balance ${balanceBefore}; no task submitted.`,
  );
  process.exit(0);
}

if (!entry.completedTaskId) {
  const settings = { model_url: sourceTask.model_urls.glb, ...entry.request };
  const submittedAt = new Date().toISOString();
  const submission = await apiJson('https://api.meshy.ai/openapi/v1/retexture', key, {
    method: 'POST',
    body: JSON.stringify(settings),
  });
  const taskId = submission.result;
  if (typeof taskId !== 'string' || !taskId)
    throw new Error('Meshy did not return a retexture task ID.');
  entry.completedTaskId = taskId;
  entry.submittedAt = submittedAt;
  entry.authorized = false;
  entry.authorizationConsumed = true;
  plan.status = 'provider-running';
  const directory = runPath(entry, taskId);
  await mkdir(directory, { recursive: true });
  await writeJson(path.join(directory, 'request-record.json'), {
    schemaVersion: 1,
    provider: 'Meshy',
    endpoint: 'retexture',
    providerModel: entry.request.ai_model,
    classId: entry.classId,
    signatureId: entry.signatureId,
    taskId,
    submittedAt,
    balanceBefore,
    authorizedCreditCap: entry.creditCap,
    sourcePlan: path.relative(repository, planPath),
    sourceTaskId: entry.source.taskId,
    sourceModel: entry.source.rawModel,
    settings: sanitizeRequest(settings),
    pricingSource: plan.balanceCheck.pricingSource,
    licenseTerms: 'https://www.meshy.ai/terms',
  });
  await writePlan();
  console.log(`Trip Node: submitted ${taskId}`);
}

let task;
let lastSignature = '';
do {
  task = await apiJson(
    `https://api.meshy.ai/openapi/v1/retexture/${encodeURIComponent(entry.completedTaskId)}`,
    key,
  );
  const signature = `${task.status}:${task.progress ?? 0}`;
  if (signature !== lastSignature) {
    console.log(`Trip Node: Meshy ${signature}`);
    lastSignature = signature;
  }
  if (!['SUCCEEDED', 'FAILED', 'CANCELED'].includes(task.status)) await delay(5_000);
} while (!['SUCCEEDED', 'FAILED', 'CANCELED'].includes(task.status));

entry.status = task.status;
entry.consumedCredits = task.consumed_credits ?? 0;
entry.finishedAt = new Date().toISOString();
if (entry.consumedCredits > entry.creditCap)
  throw new Error('Trip Node consumed credits exceed its authorization.');
const directory = runPath(entry, entry.completedTaskId);
await writeJson(path.join(directory, 'task.sanitized.json'), sanitizeTask(task));
if (task.status === 'SUCCEEDED') {
  const artifacts = [];
  await download(task.model_urls?.glb, 'raw-model.glb');
  await download(task.thumbnail_url, 'provider-preview.png');
  await download(task.alpha_thumbnail_url, 'provider-preview-alpha.png');
  for (const [index, textures] of (task.texture_urls ?? []).entries())
    for (const [kind, url] of Object.entries(textures ?? {}))
      await download(url, `texture-${index}-${safeName(kind)}${imageExtension(url)}`);
  await writeJson(path.join(directory, 'artifacts.json'), artifacts);
  await writeFile(
    path.join(directory, 'RUN.md'),
    `# Meshy 7 Trip Node matte retexture\n\n` +
      `- Task: \`${entry.completedTaskId}\`\n` +
      `- Source task: \`${entry.source.taskId}\`\n` +
      `- Texture: 2K PBR, original UVs retained\n` +
      `- Credits: ${task.consumed_credits}\n` +
      `- Status: ${task.status}\n\n` +
      `Review-only provider result. No runtime asset was promoted.\n`,
  );
  entry.artifacts = artifacts;

  async function download(url, filename) {
    if (typeof url !== 'string' || !url) return;
    const response = await fetch(url, { signal: AbortSignal.timeout(120_000) });
    if (!response.ok) throw new Error(`${filename} HTTP ${response.status}.`);
    const bytes = Buffer.from(await response.arrayBuffer());
    await writeFile(path.join(directory, filename), bytes);
    artifacts.push({ file: filename, bytes: bytes.length, sha256: sha256(bytes) });
  }
}

const balanceAfter = await balance(key);
plan.balanceAfter = balanceAfter;
plan.totalConsumedCredits = entry.consumedCredits;
plan.status = task.status === 'SUCCEEDED' ? 'owner-review' : 'provider-run-incomplete';
await writePlan();
console.log(`Retexture ${plan.status}; consumed ${entry.consumedCredits}; balance ${balanceAfter}.`);

function validatePlan(value) {
  const entryValue = value.entries?.[0];
  if (
    value.schemaVersion !== 1 ||
    value.endpoint !== '/openapi/v1/retexture' ||
    value.runtimeAssetsPromoted !== false ||
    value.entries?.length !== 1 ||
    entryValue?.classId !== 'arc-trip-node-v3' ||
    entryValue?.signatureId !== 'trip-node' ||
    entryValue.request?.ai_model !== 'meshy-7' ||
    entryValue.request?.enable_original_uv !== true ||
    entryValue.request?.enable_pbr !== true ||
    entryValue.request?.texture_resolution !== '2k' ||
    entryValue.request?.target_formats?.length !== 1 ||
    entryValue.request.target_formats[0] !== 'glb' ||
    entryValue.request.text_style_prompt?.length > 600 ||
    entryValue.creditCap !== 10 ||
    (!entryValue.completedTaskId && entryValue.authorized !== true)
  )
    throw new Error('Invalid or unauthorized Trip Node retexture plan.');
}

function runPath(value, taskId) {
  return path.join(path.dirname(planPath), value.classId, taskId);
}

async function writePlan() {
  await writeJson(planPath, plan);
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

function sanitizeTask(value) {
  if (Array.isArray(value)) return value.map((child) => sanitizeTask(child));
  if (value && typeof value === 'object')
    return Object.fromEntries(
      Object.entries(value).map(([key, child]) => [key, sanitizeTask(child)]),
    );
  if (typeof value === 'string' && /^https?:\/\//.test(value)) return sanitizeUrl(value);
  return value;
}

function sanitizeUrl(value) {
  const url = new URL(value);
  url.search = '';
  return url.toString();
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
  return new Promise((resolve) => setTimeout(resolve, milliseconds));
}

async function writeJson(file, value) {
  await mkdir(path.dirname(file), { recursive: true });
  await writeFile(file, `${JSON.stringify(value, null, 2)}\n`);
}
