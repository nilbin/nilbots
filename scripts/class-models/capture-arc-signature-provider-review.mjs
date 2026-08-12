#!/usr/bin/env node

import { createHash } from 'node:crypto';
import { createRequire } from 'node:module';
import { mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const repository = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..', '..');
const requireFromWeb = createRequire(path.join(repository, 'web/package.json'));
const { chromium } = requireFromWeb('playwright');
const sharp = requireFromWeb('sharp');
const baseUrl = process.argv[2] ?? 'http://127.0.0.1:9088';
const conceptDirectory = path.join(
  repository,
  'art/signature-models/arc-relay/gameplay-first-concepts-v2',
);
const scratchDirectory = path.join(repository, 'sandbox/arc-signature-provider-review-v3');
const viewerUrl = new URL('/scripts/class-models/model-turntable-review.html', baseUrl);
const tileSize = 480;
const labelHeight = 42;
const titleHeight = 76;
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
    id: 'trip-node',
    title: 'TRIP NODE · RAW MESHY 7 BASE MESH',
    model:
      'art/class-models/provider-runs/meshy/arc-relay-meshy7-signature-props-v3/' +
      'arc-trip-node-v3/019ff5c4-af6d-7bee-bfac-591b78b34c52/' +
      'model-normalized-identity-facing-review.glb',
    output: 'trip-node-meshy7-six-view.png',
    canonicalTop: 'trip-node-top-east.png',
    overlay: 'trip-node-top-silhouette-overlay.png',
  },
  {
    id: 'sentinel-seed',
    title: 'SENTINEL SEED · RAW MESHY 7 BASE MESH',
    model:
      'art/class-models/provider-runs/meshy/arc-relay-meshy7-signature-props-v3/' +
      'arc-sentinel-seed-v3/019ff5c8-f17f-7083-a432-71f16c9f1c05/' +
      'model-normalized-identity-review.glb',
    output: 'sentinel-seed-meshy7-six-view.png',
    canonicalTop: 'sentinel-seed-top-east.png',
    overlay: 'sentinel-seed-top-silhouette-overlay.png',
  },
];

mkdirSync(scratchDirectory, { recursive: true });
const browser = await chromium.launch({ headless: true });
const records = [];
try {
  for (const candidate of candidates) records.push(await captureCandidate(candidate));
} finally {
  await browser.close();
}

writeFileSync(
  path.join(conceptDirectory, 'provider-render-review.json'),
  `${JSON.stringify({
    schemaVersion: 1,
    status: 'owner-review',
    capturedAt: '2026-08-12',
    renderer: 'Three.js WebGLRenderer through Playwright Chromium',
    sourceViewer: 'scripts/class-models/model-turntable-review.html',
    normalizedContract: {
      up: '+Y',
      floorY: 0,
      planformSpan: 0.99,
      facing: '+X',
    },
    records,
  }, null, 2)}\n`,
);
console.log(`Captured six-view provider review for ${records.length} signature props.`);

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
    const capturePath = path.join(scratchDirectory, `${candidate.id}-${view}.png`);
    await page.locator('canvas').screenshot({ path: capturePath, animations: 'disabled' });
    captures.push({
      view,
      label,
      file: path.relative(repository, capturePath),
      sha256: sha256(readFileSync(capturePath)),
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
          `<text x="18" y="31" fill="#e8eef2" font-size="20" font-family="monospace" font-weight="700">${candidate.title}</text>` +
          '<text x="18" y="58" fill="#9fb0bb" font-size="14" font-family="monospace">normalized to 0.99 planform · +X front · +Y up · floor Y=0</text></svg>',
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
          `<text x="12" y="27" fill="#dbe7ef" font-size="16" font-family="monospace" font-weight="700">${capture.label}</text></svg>`,
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
  const sheetPath = path.join(conceptDirectory, candidate.output);
  await sharp({ create: { width, height, channels: 4, background: '#080d12' } })
    .composite(composites)
    .png()
    .toFile(sheetPath);
  const topCapture = captures.find((capture) => capture.view === 'top-down');
  const silhouette = await buildSilhouetteOverlay(candidate, topCapture);
  return {
    id: candidate.id,
    model: candidate.model,
    modelSha256: sha256(readFileSync(path.join(repository, candidate.model))),
    sheet: path.relative(repository, sheetPath),
    sheetSha256: sha256(readFileSync(sheetPath)),
    silhouette,
    captures,
  };
}

async function buildSilhouetteOverlay(candidate, topCapture) {
  const concept = await sharp(path.join(conceptDirectory, candidate.canonicalTop))
    .ensureAlpha()
    .raw()
    .toBuffer({ resolveWithObject: true });
  const rendered = await sharp(path.join(repository, topCapture.file))
    .ensureAlpha()
    .raw()
    .toBuffer({ resolveWithObject: true });
  const conceptMask = alphaMask(concept.data, concept.info.width, concept.info.height);
  const renderedMask = backgroundMask(
    rendered.data,
    rendered.info.width,
    rendered.info.height,
    [23, 25, 28],
  );
  const size = 512;
  const normalizedConcept = await normalizeMask(
    conceptMask,
    concept.info.width,
    concept.info.height,
    size,
  );
  const normalizedRendered = await normalizeMask(
    renderedMask,
    rendered.info.width,
    rendered.info.height,
    size,
  );
  let intersection = 0;
  let union = 0;
  const pixels = Buffer.alloc(size * size * 4);
  for (let index = 0; index < size * size; index += 1) {
    const inConcept = normalizedConcept[index] > 127;
    const inRendered = normalizedRendered[index] > 127;
    if (inConcept && inRendered) intersection += 1;
    if (inConcept || inRendered) union += 1;
    const color = inConcept && inRendered
      ? [231, 235, 231]
      : inConcept
        ? [220, 105, 166]
        : inRendered
          ? [80, 190, 210]
          : [12, 17, 21];
    const offset = index * 4;
    pixels[offset] = color[0];
    pixels[offset + 1] = color[1];
    pixels[offset + 2] = color[2];
    pixels[offset + 3] = 255;
  }
  const overlayPath = path.join(conceptDirectory, candidate.overlay);
  await sharp(pixels, { raw: { width: size, height: size, channels: 4 } })
    .png()
    .toFile(overlayPath);
  return {
    method:
      'Binary planform masks in canonical East/+X registration; each maximum span normalized to 420 px and centered.',
    legend: {
      overlap: 'light gray',
      canonicalOnly: 'magenta',
      providerOnly: 'cyan',
    },
    intersectionOverUnion: Number((intersection / union).toFixed(5)),
    file: path.relative(repository, overlayPath),
    sha256: sha256(readFileSync(overlayPath)),
  };
}

function alphaMask(data, width, height) {
  const mask = Buffer.alloc(width * height);
  for (let index = 0; index < width * height; index += 1)
    mask[index] = data[index * 4 + 3] > 16 ? 255 : 0;
  return mask;
}

function backgroundMask(data, width, height, background) {
  const mask = Buffer.alloc(width * height);
  for (let index = 0; index < width * height; index += 1) {
    const offset = index * 4;
    const distance = Math.max(
      Math.abs(data[offset] - background[0]),
      Math.abs(data[offset + 1] - background[1]),
      Math.abs(data[offset + 2] - background[2]),
    );
    mask[index] = distance > 10 ? 255 : 0;
  }
  return mask;
}

async function normalizeMask(mask, width, height, outputSize) {
  const bounds = maskBounds(mask, width, height);
  const croppedWidth = bounds.maxX - bounds.minX + 1;
  const croppedHeight = bounds.maxY - bounds.minY + 1;
  const cropped = Buffer.alloc(croppedWidth * croppedHeight);
  for (let y = 0; y < croppedHeight; y += 1)
    mask.copy(
      cropped,
      y * croppedWidth,
      (bounds.minY + y) * width + bounds.minX,
      (bounds.minY + y) * width + bounds.maxX + 1,
    );
  const scale = 420 / Math.max(croppedWidth, croppedHeight);
  const resizedWidth = Math.max(1, Math.round(croppedWidth * scale));
  const resizedHeight = Math.max(1, Math.round(croppedHeight * scale));
  const resized = await sharp(cropped, {
    raw: { width: croppedWidth, height: croppedHeight, channels: 1 },
  })
    .resize(resizedWidth, resizedHeight, { kernel: 'nearest' })
    .greyscale()
    .raw()
    .toBuffer({ resolveWithObject: true });
  const normalized = Buffer.alloc(outputSize * outputSize);
  const left = Math.round((outputSize - resizedWidth) / 2);
  const top = Math.round((outputSize - resizedHeight) / 2);
  for (let y = 0; y < resizedHeight; y += 1)
    for (let x = 0; x < resizedWidth; x += 1)
      normalized[(top + y) * outputSize + left + x] =
        resized.data[(y * resizedWidth + x) * resized.info.channels];
  return normalized;
}

function maskBounds(mask, width, height) {
  let minX = width;
  let minY = height;
  let maxX = -1;
  let maxY = -1;
  for (let y = 0; y < height; y += 1)
    for (let x = 0; x < width; x += 1)
      if (mask[y * width + x]) {
        minX = Math.min(minX, x);
        minY = Math.min(minY, y);
        maxX = Math.max(maxX, x);
        maxY = Math.max(maxY, y);
      }
  if (maxX < minX || maxY < minY) throw new Error('Silhouette mask is empty.');
  return { minX, minY, maxX, maxY };
}

function sha256(value) {
  return createHash('sha256').update(value).digest('hex');
}
