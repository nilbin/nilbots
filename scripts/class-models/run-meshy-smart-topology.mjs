#!/usr/bin/env node

import { execFileSync } from 'node:child_process';
import { createHash } from 'node:crypto';
import { mkdir, readFile, writeFile } from 'node:fs/promises';
import { userInfo } from 'node:os';
import { basename, join, relative, resolve } from 'node:path';
import process from 'node:process';

const repositoryRoot = resolve(import.meta.dirname, '../..');
const options = parseArgs(process.argv.slice(2));
const inputPath = resolve(repositoryRoot, options.input);
const inputBytes = await readFile(inputPath);
const inputSha256 = sha256(inputBytes);
const mimeType = inputPath.toLowerCase().endsWith('.png')
  ? 'image/png'
  : 'image/jpeg';
const dataUri = `data:${mimeType};base64,${inputBytes.toString('base64')}`;
const apiKey = readApiKey();
const startedAt = new Date();
const timerStart = process.hrtime.bigint();

const balanceBefore = await balance(apiKey);
console.log(`Balance before: ${balanceBefore}`);

const settings = {
  image_url: dataUri,
  texture_image_url: dataUri,
  model_type: 'smart-topology',
  ai_model: 'meshy-t2',
  target_polycount: options.targetPolycount,
  should_texture: true,
  enable_pbr: true,
  texture_resolution: options.textureResolution,
  target_formats: ['glb'],
  auto_size: true,
  origin_at: 'bottom',
  alpha_thumbnail: true,
  multi_view_thumbnails: true,
};

const submitStart = process.hrtime.bigint();
const submission = await apiJson(
  'https://api.meshy.ai/openapi/v1/image-to-3d',
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
  options.look,
  taskId,
);
await mkdir(runDirectory, { recursive: true });
await writeJson(join(runDirectory, 'request-record.json'), {
  version: 1,
  provider: 'Meshy',
  endpoint: 'image-to-3d',
  lookId: options.look,
  taskId,
  submittedAt: startedAt.toISOString(),
  balanceBefore,
  input: {
    file: relative(repositoryRoot, inputPath),
    bytes: inputBytes.length,
    sha256: inputSha256,
    mimeType,
  },
  settings: sanitizeRequest(settings),
});
console.log(`Submitted ${taskId} in ${submitSeconds.toFixed(2)}s`);

const generationStart = process.hrtime.bigint();
let task;
let lastProgress = -1;
while (true) {
  task = await apiJson(
    `https://api.meshy.ai/openapi/v1/image-to-3d/${encodeURIComponent(taskId)}`,
    apiKey,
  );
  if (task.progress !== lastProgress) {
    lastProgress = task.progress;
    console.log(`Meshy ${task.status}: ${task.progress ?? 0}%`);
  }
  if (task.status === 'SUCCEEDED' || task.status === 'FAILED' || task.status === 'CANCELED')
    break;
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
  });
  throw new Error(`Meshy task ${taskId} ended with ${task.status}`);
}

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
  `# Meshy Smart Topology run: ${options.look}\n\n` +
    `- Task: \`${taskId}\`\n` +
    `- Model: \`meshy-t2\` / \`smart-topology\`\n` +
    `- Target: ${options.targetPolycount.toLocaleString('en-US')} faces, ${options.textureResolution} PBR\n` +
    `- Status: ${task.status}\n` +
    `- Credits: ${task.consumed_credits} (${balanceBefore} before, ${balanceAfter} after)\n` +
    `- API generation: ${generationSeconds.toFixed(2)} seconds\n` +
    `- Submit through complete download: ${totalSeconds.toFixed(2)} seconds\n\n` +
    `This is a raw provider candidate, not an accepted runtime look. Signed URLs, ` +
    `credentials, and input data URIs are not retained.\n`,
);

console.log(`Downloaded ${artifacts.length} artifacts in ${downloadSeconds.toFixed(2)}s`);
console.log(`Credits: ${task.consumed_credits}; balance after: ${balanceAfter}`);
console.log(`Provider stage total: ${totalSeconds.toFixed(2)}s`);
console.log(`Run directory: ${relative(repositoryRoot, runDirectory)}`);

async function downloadArtifact(url, filename) {
  if (typeof url !== 'string' || url.length === 0) return;
  const response = await fetch(url, { signal: AbortSignal.timeout(120_000) });
  if (!response.ok)
    throw new Error(`Download ${filename} failed: HTTP ${response.status}`);
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
  const text = await response.text();
  let result;
  try {
    result = JSON.parse(text);
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
    Object.entries(request).filter(([key]) => key !== 'image_url' && key !== 'texture_image_url'),
  );
}

function sanitizeTask(value, key = '') {
  if (key === 'image_url' || key === 'texture_image_url') return '[redacted-data-uri]';
  if (Array.isArray(value)) return value.map((entry) => sanitizeTask(entry));
  if (value && typeof value === 'object')
    return Object.fromEntries(
      Object.entries(value).map(([childKey, child]) => [
        childKey,
        sanitizeTask(child, childKey),
      ]),
    );
  if (typeof value === 'string' && /^https?:\/\//.test(value)) {
    const url = new URL(value);
    url.search = '';
    return url.toString();
  }
  return value;
}

function parseArgs(args) {
  const values = new Map();
  for (let index = 0; index < args.length; index += 2)
    values.set(args[index], args[index + 1]);
  const look = values.get('--look');
  const input = values.get('--input');
  if (!look || !input)
    throw new Error(
      'Usage: node scripts/class-models/run-meshy-smart-topology.mjs --look <id> --input <png> [--target-polycount 15000] [--texture-resolution 4k]',
    );
  const targetPolycount = Number(values.get('--target-polycount') ?? 15_000);
  if (!Number.isInteger(targetPolycount) || targetPolycount < 100 || targetPolycount > 15_000)
    throw new Error('target-polycount must be an integer from 100 to 15000.');
  const textureResolution = values.get('--texture-resolution') ?? '4k';
  if (!['2k', '4k', '8k'].includes(textureResolution))
    throw new Error('texture-resolution must be 2k, 4k, or 8k.');
  return { look, input, targetPolycount, textureResolution };
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
