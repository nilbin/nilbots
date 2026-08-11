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
const outputDirectory = path.join(
  repository,
  'sandbox/meshy7-pilot/fleet-game-review/facing-source-audit',
);
const tileDirectory = path.join(outputDirectory, 'tiles');
const viewerUrl = new URL('/scripts/class-models/model-turntable-review.html', baseUrl);
const sourceRoot = path.join(
  repository,
  'art/class-look-concepts/arc-relay/meshy7-hover-roster-v1/meshy7-input-candidates',
);
const tileWidth = 320;
const tileHeight = 240;
const labelHeight = 32;
const titleHeight = 64;
const gap = 6;
mkdirSync(tileDirectory, { recursive: true });

const browser = await chromium.launch({ headless: true });
const records = [];
try {
  for (const entry of audit.classes) {
    const manifestPath = path.join(sourceRoot, `${entry.classId}-v1/manifest.json`);
    const manifest = JSON.parse(readFileSync(manifestPath, 'utf8'));
    const sourceFront = manifest.meshyInputs.find((input) => input.view === 'strict-front');
    const sourceBack = manifest.meshyInputs.find((input) => input.view === 'strict-back');
    if (!sourceFront || !sourceBack)
      throw new Error(`${entry.classId}: strict source front/back are missing.`);

    const context = await browser.newContext({
      viewport: { width: tileWidth, height: tileHeight },
      deviceScaleFactor: 1,
    });
    const page = await context.newPage();
    const pageErrors = [];
    const failedRequests = [];
    page.on('pageerror', (error) => pageErrors.push(error.message));
    page.on('requestfailed', (request) =>
      failedRequests.push(`${request.method()} ${request.url()} ${request.failure()?.errorText ?? ''}`),
    );
    const captures = {};
    for (const view of ['positive-x', 'negative-x', 'positive-z', 'negative-z']) {
      const url = new URL(viewerUrl);
      url.searchParams.set('model', `/${entry.artifact.file}`);
      url.searchParams.set('view', view);
      await page.goto(url.toString(), { waitUntil: 'domcontentloaded' });
      await page.waitForFunction(() => window.__modelTurntableReady === true, null, {
        timeout: 60_000,
      });
      const file = path.join(tileDirectory, `${entry.classId}-${view}.png`);
      await page.locator('canvas').screenshot({ path: file, animations: 'disabled' });
      captures[view] = {
        file: path.relative(repository, file),
        sha256: sha256(readFileSync(file)),
      };
    }
    const canvasKind = await page.locator('canvas').evaluate((canvas) =>
      canvas.getContext('2d') ? '2d' : 'webgl',
    );
    if (canvasKind !== 'webgl' || pageErrors.length || failedRequests.length)
      throw new Error(
        `${entry.classId}: facing audit failed ` +
          `${JSON.stringify({ canvasKind, pageErrors, failedRequests })}.`,
      );
    records.push({
      classId: entry.classId,
      artifact: entry.artifact.file,
      sourceFront: {
        file: path.relative(repository, path.join(path.dirname(manifestPath), sourceFront.file)),
        sha256: sourceFront.sha256,
      },
      sourceBack: {
        file: path.relative(repository, path.join(path.dirname(manifestPath), sourceBack.file)),
        sha256: sourceBack.sha256,
      },
      captures,
    });
    await context.close();
  }
} finally {
  await browser.close();
}

for (let sheetIndex = 0; sheetIndex < 4; sheetIndex += 1) {
  const entries = records.slice(sheetIndex * 4, sheetIndex * 4 + 4);
  const columns = [
    'SOURCE FRONT',
    'CURRENT +X CAMERA',
    'CURRENT -X CAMERA',
    'CURRENT +Z CAMERA',
    'CURRENT -Z CAMERA',
    'SOURCE BACK',
  ];
  const cellWidth = tileWidth + gap;
  const cellHeight = tileHeight + labelHeight + gap;
  const width = cellWidth * columns.length - gap;
  const height = titleHeight + cellHeight * entries.length - gap;
  const composites = [
    {
      input: Buffer.from(
        `<svg width="${width}" height="${titleHeight}" xmlns="http://www.w3.org/2000/svg">` +
          '<rect width="100%" height="100%" fill="#080d12"/>' +
          '<text x="16" y="27" fill="#e8eef2" font-size="19" font-family="monospace" font-weight="700">' +
          `MESHY 7 FACING AUDIT ${sheetIndex + 1}/4</text>` +
          '<text x="16" y="50" fill="#9fb0bb" font-size="13" font-family="monospace">' +
          'pinned strict source views vs current review artifact cardinal cameras</text></svg>',
      ),
      left: 0,
      top: 0,
    },
  ];
  for (const [row, entry] of entries.entries()) {
    const sourceFront = await sourceTile(path.join(repository, entry.sourceFront.file));
    const sourceBack = await sourceTile(path.join(repository, entry.sourceBack.file));
    const tiles = [
      sourceFront,
      readFileSync(path.join(repository, entry.captures['positive-x'].file)),
      readFileSync(path.join(repository, entry.captures['negative-x'].file)),
      readFileSync(path.join(repository, entry.captures['positive-z'].file)),
      readFileSync(path.join(repository, entry.captures['negative-z'].file)),
      sourceBack,
    ];
    for (const [column, tile] of tiles.entries()) {
      const left = column * cellWidth;
      const top = titleHeight + row * cellHeight;
      composites.push({
        input: Buffer.from(
          `<svg width="${tileWidth}" height="${labelHeight}" xmlns="http://www.w3.org/2000/svg">` +
            '<rect width="100%" height="100%" fill="#10151a"/>' +
            `<text x="9" y="21" fill="#dbe7ef" font-size="14" font-family="monospace" font-weight="700">${entry.classId.toUpperCase()}</text>` +
            `<text x="311" y="21" text-anchor="end" fill="#81939f" font-size="11" font-family="monospace">${columns[column]}</text></svg>`,
        ),
        left,
        top,
      });
      composites.push({ input: tile, left, top: top + labelHeight });
    }
  }
  await sharp({ create: { width, height, channels: 4, background: '#080d12' } })
    .composite(composites)
    .png()
    .toFile(path.join(outputDirectory, `facing-audit-${sheetIndex + 1}.png`));
}

writeFileSync(
  path.join(outputDirectory, 'capture-record.json'),
  `${JSON.stringify({ schemaVersion: 1, records }, null, 2)}\n`,
);
console.log(`Captured source-facing comparison for ${records.length} classes.`);

async function sourceTile(file) {
  return sharp(file)
    .resize(tileWidth, tileHeight, { fit: 'contain', background: '#17191c' })
    .png()
    .toBuffer();
}

function sha256(value) {
  return createHash('sha256').update(value).digest('hex');
}
