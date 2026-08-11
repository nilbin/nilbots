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
const audit = JSON.parse(
  readFileSync(
    path.join(repository, 'art/class-models/runtime-tiers/arc-relay/meshy7-fleet-review-v1/audit.json'),
    'utf8',
  ),
);
const baseUrl = process.argv[2] ?? 'http://127.0.0.1:9074';
const reviewDirectory = path.join(repository, 'art/reviews/arc-relay-meshy7-fleet-v1');
const tileDirectory = path.join(repository, 'sandbox/meshy7-pilot/fleet-game-review/fixed-orientation');
const viewerUrl = new URL('/scripts/class-models/model-turntable-review.html', baseUrl);
const tileSize = 420;
const labelHeight = 38;
const titleHeight = 70;
const gap = 8;
mkdirSync(reviewDirectory, { recursive: true });
mkdirSync(tileDirectory, { recursive: true });

const browser = await chromium.launch({ headless: true });
const records = [];
try {
  for (const entry of audit.classes) {
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
    url.searchParams.set('model', `/${entry.artifact.file}`);
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
        `${entry.classId}: fixed-orientation render failed ` +
          `${JSON.stringify({ canvasKind, modelResponses, pageErrors, failedRequests })}.`,
      );
    const tilePath = path.join(tileDirectory, `${entry.classId}.png`);
    await page.locator('canvas').screenshot({ path: tilePath, animations: 'disabled' });
    records.push({
      classId: entry.classId,
      artifact: entry.artifact.file,
      sourceFacing: entry.facing.sourceFacing,
      facingYawDegrees: entry.facing.yawDegrees,
      facingEvidence: entry.facing.evidence,
      facing: '+x',
      camera: '+x/+y/-z front-right three-quarter',
      canvasKind,
      modelResponse: modelResponses[0],
      tile: path.relative(repository, tilePath),
      tileSha256: sha256(readFileSync(tilePath)),
    });
    await context.close();
  }
} finally {
  await browser.close();
}

const cellWidth = tileSize + gap;
const cellHeight = tileSize + labelHeight + gap;
const width = cellWidth * 4 - gap;
const height = titleHeight + cellHeight * 4 - gap;
const composites = [
  {
    input: Buffer.from(
      `<svg width="${width}" height="${titleHeight}" xmlns="http://www.w3.org/2000/svg">` +
        '<rect width="100%" height="100%" fill="#080d12"/>' +
        '<text x="18" y="31" fill="#e8eef2" font-size="20" font-family="monospace" font-weight="700">' +
        'MESHY 7 ARC RELAY FLEET · FIXED ORIENTATION</text>' +
        '<text x="18" y="56" fill="#9fb0bb" font-size="15" font-family="monospace">' +
        'same front-right 3/4 camera · every normalized chassis faces +X</text></svg>',
    ),
    left: 0,
    top: 0,
  },
];
for (const [index, entry] of audit.classes.entries()) {
  const column = index % 4;
  const row = Math.floor(index / 4);
  const left = column * cellWidth;
  const top = titleHeight + row * cellHeight;
  composites.push({
    input: Buffer.from(
      `<svg width="${tileSize}" height="${labelHeight}" xmlns="http://www.w3.org/2000/svg">` +
        '<rect width="100%" height="100%" fill="#10151a"/>' +
        `<text x="12" y="25" fill="#dbe7ef" font-size="17" font-family="monospace" font-weight="700">${entry.classId.toUpperCase()}</text>` +
        '<text x="396" y="25" text-anchor="end" fill="#81939f" font-size="14" font-family="monospace">+X FRONT</text></svg>',
    ),
    left,
    top,
  });
  composites.push({
    input: readFileSync(path.join(tileDirectory, `${entry.classId}.png`)),
    left,
    top: top + labelHeight,
  });
}
const sheetPath = path.join(reviewDirectory, 'fixed-orientation-sheet.png');
await sharp({
  create: { width, height, channels: 4, background: '#080d12' },
})
  .composite(composites)
  .png()
  .toFile(sheetPath);
writeFileSync(
  path.join(reviewDirectory, 'orientation-evidence.json'),
  `${JSON.stringify({
    schemaVersion: 1,
    sourceAudit:
      'art/class-models/runtime-tiers/arc-relay/meshy7-fleet-review-v1/audit.json',
    facing: '+x',
    up: '+y',
    floorY: 0,
    camera: '+x/+y/-z front-right three-quarter',
    sheet: {
      file: path.relative(repository, sheetPath),
      sha256: sha256(readFileSync(sheetPath)),
    },
    captures: records,
  }, null, 2)}\n`,
);
console.log(`Captured fixed-orientation sheet for ${records.length} classes.`);

function sha256(value) {
  return createHash('sha256').update(value).digest('hex');
}
