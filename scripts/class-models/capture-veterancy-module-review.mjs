#!/usr/bin/env node

import { createHash } from 'node:crypto';
import { createRequire } from 'node:module';
import { mkdirSync, readFileSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const repository = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..', '..');
const requireFromWeb = createRequire(path.join(repository, 'web', 'package.json'));
const { chromium } = requireFromWeb('playwright');
const sharp = requireFromWeb('sharp');
const moduleId = process.argv[2];
const baseUrl = process.argv[3] ?? 'http://127.0.0.1:9201';
if (!/^[a-z][a-z0-9-]*$/.test(moduleId ?? ''))
  throw new Error(
    'Usage: node scripts/class-models/capture-veterancy-module-review.mjs ' +
      '<module-id> [base-url]',
  );

const manifestPath = path.join(
  repository,
  'art/class-models/provider-runs/meshy/arc-veterancy-modules-v1/inputs',
  moduleId,
  'manifest.json',
);
const manifest = JSON.parse(readFileSync(manifestPath, 'utf8'));
const taskId = manifest.providerRun?.taskId;
if (!taskId) throw new Error(`${moduleId} has no completed provider task.`);
const model =
  `art/class-models/provider-runs/meshy/arc-veterancy-modules-v1/${moduleId}/` +
  `${taskId}/model-normalized-rich-review.glb`;
const outputDirectory = path.join(
  repository,
  'art/reviews/arc-relay-veterancy-modules-meshy7-v1/gate-c',
  moduleId,
);
const captureDirectory = path.join(outputDirectory, 'captures');
const viewerUrl = new URL('/scripts/class-models/model-turntable-review.html', baseUrl);
const tileSize = 420;
const views = [
  ['front-three-quarter', 'GAMEPLAY 3/4'],
  ['positive-x', '+X CAMERA'],
  ['negative-x', '-X CAMERA'],
  ['positive-z', '+Z CAMERA'],
  ['negative-z', '-Z CAMERA'],
  ['top-down', 'STRICT TOP'],
];

mkdirSync(captureDirectory, { recursive: true });
const browser = await chromium.launch({ headless: true });
const captures = [];
try {
  for (const [view, label] of views) captures.push(await capture(view, label));
} finally {
  await browser.close();
}

const labelHeight = 38;
const titleHeight = 82;
const gap = 8;
const columns = 3;
const rows = 2;
const cellWidth = tileSize + gap;
const cellHeight = tileSize + labelHeight + gap;
const width = cellWidth * columns - gap;
const height = titleHeight + cellHeight * rows - gap;
const title = moduleId.replace(/^veterancy-/, '').replaceAll('-', ' ').toUpperCase();
const composites = [
  {
    input: Buffer.from(
      `<svg width="${width}" height="${titleHeight}" xmlns="http://www.w3.org/2000/svg">` +
        '<rect width="100%" height="100%" fill="#080d12"/>' +
        `<text x="16" y="31" fill="#e8eef2" font-size="20" font-family="monospace" font-weight="700">VETERANCY ${title} · MESHY 7</text>` +
        '<text x="16" y="57" fill="#9fb0bb" font-size="13" font-family="monospace">normalized rich GLB · fixed orientation · static art-side review</text>' +
        '</svg>',
    ),
    left: 0,
    top: 0,
  },
];
for (const [index, captured] of captures.entries()) {
  const left = (index % columns) * cellWidth;
  const top = titleHeight + Math.floor(index / columns) * cellHeight;
  composites.push(
    {
      input: Buffer.from(
        `<svg width="${tileSize}" height="${labelHeight}" xmlns="http://www.w3.org/2000/svg">` +
          '<rect width="100%" height="100%" fill="#10151a"/>' +
          `<text x="10" y="25" fill="#dbe7ef" font-size="14" font-family="monospace" font-weight="700">${captured.label}</text>` +
          '</svg>',
      ),
      left,
      top,
    },
    { input: readFileSync(captured.path), left, top: top + labelHeight },
  );
}
const sheetPath = path.join(outputDirectory, `${moduleId}-six-view.png`);
await sharp({ create: { width, height, channels: 4, background: '#080d12' } })
  .composite(composites)
  .png({ compressionLevel: 9 })
  .toFile(sheetPath);
const sheetBytes = readFileSync(sheetPath);
console.log(
  JSON.stringify(
    {
      moduleId,
      taskId,
      model,
      sheet: path.relative(repository, sheetPath),
      bytes: sheetBytes.length,
      sha256: sha256(sheetBytes),
      captures: captures.map(({ path: capturePath, ...capture }) => ({
        ...capture,
        file: path.relative(repository, capturePath),
      })),
    },
    null,
    2,
  ),
);

async function capture(view, label) {
  const url = new URL(viewerUrl);
  url.searchParams.set('model', `/${model}`);
  url.searchParams.set('view', view);
  const context = await browser.newContext({
    viewport: { width: tileSize, height: tileSize },
    deviceScaleFactor: 1,
  });
  const page = await context.newPage();
  const pageErrors = [];
  const failedRequests = [];
  const modelResponses = [];
  page.on('pageerror', (error) => pageErrors.push(error.message));
  page.on('requestfailed', (request) =>
    failedRequests.push(`${request.method()} ${request.url()} ${request.failure()?.errorText ?? ''}`),
  );
  page.on('response', (response) => {
    if (new URL(response.url()).pathname.endsWith('.glb'))
      modelResponses.push({ url: response.url(), status: response.status() });
  });
  await page.goto(url.toString(), { waitUntil: 'domcontentloaded' });
  await page.waitForFunction(() => window.__modelTurntableReady === true, null, {
    timeout: 120_000,
  });
  const canvasKind = await page.locator('canvas').evaluate((canvas) =>
    canvas.getContext('2d') ? '2d' : 'webgl',
  );
  if (
    canvasKind !== 'webgl' ||
    modelResponses.length !== 1 ||
    modelResponses[0].status !== 200 ||
    pageErrors.length ||
    failedRequests.length
  )
    throw new Error(
      `${view}: render failed ${JSON.stringify({ canvasKind, modelResponses, pageErrors, failedRequests })}.`,
    );
  const output = path.join(captureDirectory, `${view}.png`);
  await page.locator('canvas').screenshot({ path: output, animations: 'disabled' });
  await context.close();
  const bytes = readFileSync(output);
  return { view, label, path: output, bytes: bytes.length, sha256: sha256(bytes) };
}

function sha256(value) {
  return createHash('sha256').update(value).digest('hex');
}
