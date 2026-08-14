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
  'art/class-models/provider-runs/meshy/arc-relay-longshot-railback-palette-retexture-v3/plan.json',
);
const plan = JSON.parse(await readFile(planPath, 'utf8'));
const entry = plan.entries?.[0];
validatePlan(plan, entry);

const key = readApiKey();
const balanceBefore = await balance(key);
console.log(`Balance before: ${balanceBefore}; authorized cap: ${entry.creditCap}`);
if (!entry.completedTaskId && balanceBefore < entry.creditCap)
  throw new Error(`Meshy balance ${balanceBefore} is below the ${entry.creditCap}-credit cap.`);

const sourcePath = path.join(repository, entry.source.rawModel.file);
const sourceBytes = await readFile(sourcePath);
if (
  sourceBytes.length !== entry.source.rawModel.bytes ||
  sha256(sourceBytes) !== entry.source.rawModel.sha256
)
  throw new Error('The pinned Railback source GLB drifted from its audited bytes.');

const sourceTask = await apiJson(
  `https://api.meshy.ai/openapi/v1/${entry.source.endpoint}/${encodeURIComponent(entry.source.taskId)}`,
  key,
);
if (sourceTask.status !== 'SUCCEEDED' || typeof sourceTask.model_urls?.glb !== 'string')
  throw new Error('The pinned Railback source task is unavailable or incomplete.');

if (check) {
  console.log(
    `Preflight passed: source ${entry.source.taskId}, ${sourceBytes.length} bytes, ` +
      `${entry.request.texture_resolution} Meshy 7 PBR retexture, balance ${balanceBefore}; ` +
      'no task submitted.',
  );
  process.exit(0);
}

let taskId = entry.completedTaskId;
let submittedAt = entry.submittedAt;
if (!taskId) {
  const settings = { model_url: sourceTask.model_urls.glb, ...entry.request };
  submittedAt = new Date().toISOString();
  const submission = await apiJson('https://api.meshy.ai/openapi/v1/retexture', key, {
    method: 'POST',
    body: JSON.stringify(settings),
  });
  taskId = submission.result;
  if (typeof taskId !== 'string' || !taskId)
    throw new Error('Meshy did not return a retexture task ID.');
  entry.completedTaskId = taskId;
  entry.submittedAt = submittedAt;
  entry.authorized = false;
  entry.authorizationConsumed = true;
  entry.status = 'SUBMITTED';
  const runDirectory = runPath(taskId);
  await mkdir(runDirectory, { recursive: true });
  await writeJson(path.join(runDirectory, 'request-record.json'), {
    schemaVersion: 1,
    provider: 'Meshy',
    endpoint: 'retexture',
    providerModel: entry.request.ai_model,
    classId: entry.classId,
    candidateId: entry.candidateId,
    taskId,
    submittedAt,
    balanceBefore,
    authorizedCreditCap: entry.creditCap,
    sourcePlan: path.relative(repository, planPath),
    sourceTaskId: entry.source.taskId,
    sourceModel: entry.source.rawModel,
    settings: sanitizeRequest(settings),
    pricingSource: plan.balanceCheck.pricingSource,
    parameterSource: plan.balanceCheck.parameterSource,
    licenseTerms: 'https://www.meshy.ai/terms',
  });
  await writePlan();
  console.log(`Submitted ${taskId}`);
}

const generationStart = process.hrtime.bigint();
let task;
let lastSignature = '';
while (true) {
  task = await apiJson(
    `https://api.meshy.ai/openapi/v1/retexture/${encodeURIComponent(taskId)}`,
    key,
  );
  const signature = `${task.status}:${task.progress ?? 0}`;
  if (signature !== lastSignature) {
    lastSignature = signature;
    console.log(`Meshy ${signature}`);
  }
  if (['SUCCEEDED', 'FAILED', 'CANCELED'].includes(task.status)) break;
  await delay(2_000);
}

const runDirectory = runPath(taskId);
await mkdir(runDirectory, { recursive: true });
await writeJson(path.join(runDirectory, 'task.sanitized.json'), sanitizeTask(task));
entry.status = task.status;
entry.consumedCredits = task.consumed_credits ?? 0;
entry.finishedAt = new Date().toISOString();

if (task.status !== 'SUCCEEDED') {
  plan.status = 'provider-run-incomplete';
  plan.balanceAfter = await balance(key);
  await writePlan();
  throw new Error(`Meshy task ${taskId} ended with ${task.status}.`);
}
if (entry.consumedCredits > entry.creditCap)
  throw new Error(`Meshy consumed ${entry.consumedCredits}, above the authorized cap.`);

const artifacts = [];
await download(task.model_urls?.glb, 'raw-model.glb');
await download(task.thumbnail_url, 'provider-preview.png');
await download(task.alpha_thumbnail_url, 'provider-preview-alpha.png');
for (const [view, url] of Object.entries(task.thumbnail_urls ?? {}))
  await download(url, `provider-${safeName(view)}.png`);
for (const [index, textures] of (task.texture_urls ?? []).entries())
  for (const [kind, url] of Object.entries(textures ?? {}))
    await download(url, `texture-${index}-${safeName(kind)}${imageExtension(url)}`);

const balanceAfter = await balance(key);
entry.artifacts = artifacts;
plan.balanceAfter = balanceAfter;
plan.totalConsumedCredits = entry.consumedCredits;
plan.status = 'owner-review';
await writeJson(path.join(runDirectory, 'artifacts.json'), artifacts);
await writeJson(path.join(runDirectory, 'timing.json'), {
  submittedAt,
  finishedAt: entry.finishedAt,
  generationSeconds: elapsedSeconds(generationStart),
  status: task.status,
  consumedCredits: entry.consumedCredits,
  balanceBefore,
  balanceAfter,
});
await writeFile(
  path.join(runDirectory, 'RUN.md'),
    `# Meshy 7 Railback muted-blue palette retexture\n\n` +
    `- Task: \`${taskId}\`\n` +
    `- Source task: \`${entry.source.taskId}\`\n` +
    `- Texture: 2K PBR, original UVs retained\n` +
    `- Credits: ${entry.consumedCredits} (${balanceBefore} before, ${balanceAfter} after)\n` +
    `- Status: ${task.status}\n\n` +
    `Review-only raw provider result. No runtime asset was promoted.\n`,
);
await writePlan();
console.log(
  `Retexture ${task.status}; downloaded ${artifacts.length} artifacts; ` +
    `consumed ${entry.consumedCredits}; balance ${balanceAfter}.`,
);
console.log(`Run directory: ${path.relative(repository, runDirectory)}`);

async function download(url, filename) {
  if (typeof url !== 'string' || !url) return;
  const response = await fetch(url, { signal: AbortSignal.timeout(120_000) });
  if (!response.ok) throw new Error(`${filename}: HTTP ${response.status}.`);
  const bytes = Buffer.from(await response.arrayBuffer());
  await writeFile(path.join(runDirectory, filename), bytes);
  artifacts.push({ file: filename, bytes: bytes.length, sha256: sha256(bytes) });
}

function validatePlan(value, candidate) {
  if (
    value.schemaVersion !== 1 ||
    value.endpoint !== '/openapi/v1/retexture' ||
    value.runtimeAssetsPromoted !== false ||
    !Array.isArray(value.entries) ||
    value.entries.length !== 1 ||
    candidate?.classId !== 'longshot' ||
    candidate?.candidateId !== 'railback-muted-blue-v3' ||
    candidate?.source?.endpoint !== 'multi-image-to-3d' ||
    !candidate?.source?.taskId ||
    !candidate?.source?.rawModel?.file ||
    !candidate?.source?.rawModel?.sha256 ||
    candidate?.request?.ai_model !== 'meshy-7' ||
    typeof candidate?.request?.text_style_prompt !== 'string' ||
    candidate.request.text_style_prompt.length === 0 ||
    candidate.request.text_style_prompt.length > 600 ||
    candidate.request.enable_original_uv !== true ||
    candidate.request.enable_pbr !== true ||
    candidate.request.texture_resolution !== '2k' ||
    candidate.request.alpha_thumbnail !== true ||
    candidate.request.target_formats?.length !== 1 ||
    candidate.request.target_formats[0] !== 'glb' ||
    candidate.creditCap !== 10 ||
    candidate.autoRigging !== false ||
    (!candidate.completedTaskId && candidate.authorized !== true) ||
    (!candidate.completedTaskId && candidate.authorizationConsumed !== false)
  )
    throw new Error('Invalid or unauthorized Railback palette retexture plan.');
}

function runPath(id) {
  return path.join(path.dirname(planPath), entry.classId, id);
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

function sanitizeTask(value, key = '') {
  if (key === 'image_url' || key === 'image_style_url' || key === 'multiview_image_urls')
    return '[redacted-input]';
  if (Array.isArray(value)) return value.map((item) => sanitizeTask(item));
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

function elapsedSeconds(start) {
  return Number(process.hrtime.bigint() - start) / 1_000_000_000;
}

function delay(milliseconds) {
  return new Promise((resolveDelay) => setTimeout(resolveDelay, milliseconds));
}

async function writeJson(file, value) {
  await mkdir(path.dirname(file), { recursive: true });
  await writeFile(file, `${JSON.stringify(value, null, 2)}\n`);
}
