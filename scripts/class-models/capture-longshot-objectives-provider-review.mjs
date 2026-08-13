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
const baseUrl = process.argv[2] ?? 'http://127.0.0.1:9098';
const outputDirectory = path.join(
  repository,
  'art/reviews/arc-relay-longshot-objectives-meshy7-v1/corrective-provider-review',
);
const tileDirectory = path.join(outputDirectory, 'captures');
const viewerUrl = new URL('/scripts/class-models/model-turntable-review.html', baseUrl);
const tileSize = 420;
const labelHeight = 38;
const titleHeight = 70;
const gap = 8;
const views = [
  ['front-three-quarter', 'GAMEPLAY 3/4'],
  ['positive-x', '+X CAMERA'],
  ['negative-x', '-X CAMERA'],
  ['positive-z', '+Z CAMERA'],
  ['negative-z', '-Z CAMERA'],
  ['top-down', 'STRICT TOP'],
];
const candidates = [
  {
    id: 'longshot',
    title: 'LONGSHOT · CORRECTIVE RETEXTURE',
    targetSpan: 0.99,
    model:
      'art/class-models/provider-runs/meshy/arc-relay-longshot-objectives-v1-corrective-retextures/' +
      'longshot/019ffbca-f9c4-757e-9588-c718abfe1538/' +
      'model-normalized-identity-review.glb',
  },
  {
    id: 'survey-flare',
    title: 'SURVEY FLARE · CORRECTIVE RETEXTURE',
    targetSpan: 0.3,
    model:
      'art/class-models/provider-runs/meshy/arc-relay-longshot-objectives-v1-corrective-retextures/' +
      'survey-flare/019ffbcd-1911-75e8-a822-90a439c88415/' +
      'model-normalized-identity-review.glb',
  },
  {
    id: 'core-well',
    title: 'CORE WELL · CORRECTIVE GEOMETRY',
    targetSpan: 1,
    model:
      'art/class-models/provider-runs/meshy/arc-relay-longshot-objectives-v1-corrective-geometry/' +
      'core-well-v2/019ffbd2-0fcf-7716-bba6-8f920b78fcc9/' +
      'model-normalized-identity-review.glb',
  },
  {
    id: 'team-reactor',
    title: 'TEAM REACTOR · CORRECTIVE GEOMETRY',
    targetSpan: 1.5,
    model:
      'art/class-models/provider-runs/meshy/arc-relay-longshot-objectives-v1-corrective-geometry/' +
      'team-reactor-v2/019ffbd5-9d7f-72d9-a6d0-6d79c058801c/' +
      'model-normalized-identity-review.glb',
  },
];

mkdirSync(tileDirectory, { recursive: true });
const browser = await chromium.launch({ headless: true });
const records = [];
try {
  for (const candidate of candidates) records.push(await captureCandidate(candidate));
} finally {
  await browser.close();
}

writeFileSync(
  path.join(outputDirectory, 'audit.json'),
  `${JSON.stringify({
    schemaVersion: 1,
    status: 'geometry-and-material-owner-review',
    capturedAt: new Date().toISOString(),
    renderer: 'Three.js WebGLRenderer through Playwright Chromium',
    sourceViewer: 'scripts/class-models/model-turntable-review.html',
    contract: {
      purpose: 'Normalized provider geometry review only; no runtime promotion',
      up: '+Y',
      floorY: 0,
      materialGate: 'corrective pass: inspect matte finish and approved palette',
    },
    records,
  }, null, 2)}\n`,
);
console.log(`Captured provider geometry review for ${records.length} assets.`);

async function captureCandidate(candidate) {
  const captures = [];
  for (const [view, label] of views) {
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
    url.searchParams.set('model', `/${candidate.model}`);
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
        `${candidate.id}/${view}: render failed ` +
          `${JSON.stringify({ canvasKind, modelResponses, pageErrors, failedRequests })}.`,
      );
    const file = path.join(tileDirectory, `${candidate.id}-${view}.png`);
    await page.locator('canvas').screenshot({ path: file, animations: 'disabled' });
    captures.push({
      view,
      label,
      file: path.relative(repository, file),
      sha256: sha256(readFileSync(file)),
    });
    await context.close();
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
          `<text x="16" y="29" fill="#e8eef2" font-size="19" font-family="monospace" font-weight="700">${candidate.title}</text>` +
          `<text x="16" y="53" fill="#9fb0bb" font-size="13" font-family="monospace">normalized ${candidate.targetSpan}-tile maximum planform · +Y up · corrective owner-review candidate</text></svg>`,
      ),
      left: 0,
      top: 0,
    },
  ];
  for (const [index, capture] of captures.entries()) {
    const column = index % 3;
    const row = Math.floor(index / 3);
    const left = column * cellWidth;
    const top = titleHeight + row * cellHeight;
    composites.push({
      input: Buffer.from(
        `<svg width="${tileSize}" height="${labelHeight}" xmlns="http://www.w3.org/2000/svg">` +
          '<rect width="100%" height="100%" fill="#10151a"/>' +
          `<text x="10" y="25" fill="#dbe7ef" font-size="14" font-family="monospace" font-weight="700">${capture.label}</text></svg>`,
      ),
      left,
      top,
    });
    composites.push({
      input: readFileSync(path.join(repository, capture.file)),
      left,
      top: top + labelHeight,
    });
  }
  const sheet = path.join(outputDirectory, `${candidate.id}-six-view.png`);
  await sharp({ create: { width, height, channels: 4, background: '#080d12' } })
    .composite(composites)
    .png({ compressionLevel: 9 })
    .toFile(sheet);
  const modelBytes = readFileSync(path.join(repository, candidate.model));
  const { document } = readGlb(modelBytes, candidate.model);
  return {
    id: candidate.id,
    targetPlanformSpan: candidate.targetSpan,
    model: candidate.model,
    modelBytes: modelBytes.length,
    modelSha256: sha256(modelBytes),
    triangles: triangleCount(document),
    sheet: path.relative(repository, sheet),
    sheetSha256: sha256(readFileSync(sheet)),
    captures,
  };
}

function sha256(value) {
  return createHash('sha256').update(value).digest('hex');
}
