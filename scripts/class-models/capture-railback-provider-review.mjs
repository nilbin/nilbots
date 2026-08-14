#!/usr/bin/env node

import { createHash } from 'node:crypto';
import { createRequire } from 'node:module';
import { mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { readGlb, triangleCount } from './model-memory.mjs';

const repository = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..', '..');
const requireFromWeb = createRequire(path.join(repository, 'web', 'package.json'));
const { chromium } = requireFromWeb('playwright');
const sharp = requireFromWeb('sharp');
const baseUrl = process.argv[2] ?? 'http://127.0.0.1:9191';
const model = process.argv[3] ??
  'art/class-models/provider-runs/meshy/arc-relay-longshot-railback-v1/' +
  'longshot/019fff75-43b1-72d8-a2c9-f9a8b8fa32f2/model-normalized-identity-review.glb';
const tag = process.argv[4] ?? 'identity';
const outputDirectory = path.join(
  repository,
  'art/reviews/arc-relay-longshot-railback-v1/provider-review',
);
const captureDirectory = path.join(outputDirectory, tag);
const viewerUrl = new URL('/scripts/class-models/model-turntable-review.html', baseUrl);
const tileSize = 440;
const labelHeight = 40;
const titleHeight = 82;
const gap = 8;
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

const cellWidth = tileSize + gap;
const cellHeight = tileSize + labelHeight + gap;
const width = cellWidth * 3 - gap;
const height = titleHeight + cellHeight * 2 - gap;
const composites = [
  {
    input: Buffer.from(
      `<svg width="${width}" height="${titleHeight}" xmlns="http://www.w3.org/2000/svg">` +
        '<rect width="100%" height="100%" fill="#080d12"/>' +
        '<text x="16" y="31" fill="#e8eef2" font-size="20" font-family="monospace" font-weight="700">RAILBACK · PROVIDER GEOMETRY GATE</text>' +
        `<text x="16" y="57" fill="#9fb0bb" font-size="13" font-family="monospace">${tag} · normalized 0.99-tile max span · +Y up · 1.12× deterministic height correction · review only</text>` +
        '</svg>',
    ),
    left: 0,
    top: 0,
  },
];
for (const [index, captured] of captures.entries()) {
  const column = index % 3;
  const row = Math.floor(index / 3);
  const left = column * cellWidth;
  const top = titleHeight + row * cellHeight;
  composites.push(
    {
      input: Buffer.from(
        `<svg width="${tileSize}" height="${labelHeight}" xmlns="http://www.w3.org/2000/svg">` +
          '<rect width="100%" height="100%" fill="#10151a"/>' +
          `<text x="10" y="26" fill="#dbe7ef" font-size="14" font-family="monospace" font-weight="700">${captured.label}</text>` +
          '</svg>',
      ),
      left,
      top,
    },
    { input: readFileSync(path.join(repository, captured.file)), left, top: top + labelHeight },
  );
}
const sheet = path.join(outputDirectory, `railback-${tag}-six-view.png`);
await sharp({ create: { width, height, channels: 4, background: '#080d12' } })
  .composite(composites)
  .png({ compressionLevel: 9 })
  .toFile(sheet);

const modelBytes = readFileSync(path.join(repository, model));
const { document } = readGlb(modelBytes, model);
const audit = {
  schemaVersion: 1,
  status: 'provider-geometry-review',
  capturedAt: new Date().toISOString(),
  renderer: 'Three.js WebGLRenderer through Playwright Chromium',
  sourceViewer: 'scripts/class-models/model-turntable-review.html',
  tag,
  model,
  modelBytes: modelBytes.length,
  modelSha256: sha256(modelBytes),
  triangles: triangleCount(document),
  sheet: path.relative(repository, sheet),
  sheetSha256: sha256(readFileSync(sheet)),
  contract: {
    purpose: 'Normalized provider geometry review only; no runtime promotion',
    targetPlanformSpan: 0.99,
    targetMinimumTransverseSpan: 0.68,
    targetMinimumHeight: 0.26,
    heightScale: 1.12,
    up: '+Y',
    floorY: 0,
  },
  captures,
};
writeFileSync(
  path.join(outputDirectory, `audit-${tag}.json`),
  `${JSON.stringify(audit, null, 2)}\n`,
);
console.log(`Captured ${path.relative(repository, sheet)}.`);

async function capture(view, label) {
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
  const url = new URL(viewerUrl);
  url.searchParams.set('model', `/${model}`);
  url.searchParams.set('view', view);
  await page.goto(url.toString(), { waitUntil: 'domcontentloaded' });
  await page.waitForFunction(() => window.__modelTurntableReady === true, null, {
    timeout: 60_000,
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
  const file = path.join(captureDirectory, `${view}.png`);
  await page.locator('canvas').screenshot({ path: file, animations: 'disabled' });
  await context.close();
  const bytes = readFileSync(file);
  return {
    view,
    label,
    file: path.relative(repository, file),
    sha256: sha256(bytes),
  };
}

function sha256(value) {
  return createHash('sha256').update(value).digest('hex');
}
