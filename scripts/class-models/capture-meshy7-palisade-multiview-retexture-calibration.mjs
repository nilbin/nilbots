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
const baseUrl = process.argv[2] ?? 'http://127.0.0.1:9074';
const outputDirectory = path.join(
  repository,
  'art/reviews/arc-relay-meshy7-palisade-multiview-retexture-calibration-v1',
);
const tileDirectory = path.join(
  repository,
  'sandbox/meshy7-pilot/palisade-multiview-retexture-calibration-v1',
);
const sourceImage = path.join(
  repository,
  'art/class-look-concepts/arc-relay/meshy7-hover-roster-v1/meshy7-input-candidates/palisade-v1/04-front-right-three-quarter.png',
);
const panels = [
  {
    id: 'approved-2d',
    label: 'APPROVED 2D AUTHORITY',
    kind: 'image',
    file: sourceImage,
  },
  {
    id: 'original-v1',
    label: 'ORIGINAL 3D TEXTURE',
    kind: 'model',
    file: path.join(
      repository,
      'art/class-models/runtime-tiers/arc-relay/meshy7-fleet-review-v1/arc-palisade-lean.glb',
    ),
  },
  {
    id: 'text-v2-rejected',
    label: 'TEXT V2 · REJECTED',
    kind: 'model',
    file: path.join(
      repository,
      'art/class-models/runtime-tiers/arc-relay/meshy7-fleet-review-v2/arc-palisade-lean.glb',
    ),
  },
  {
    id: 'multiview-v3',
    label: 'MULTIVIEW V3 · CALIBRATION',
    kind: 'model',
    file: path.join(
      repository,
      'art/class-models/runtime-tiers/arc-relay/meshy7-palisade-multiview-retexture-calibration-v1/arc-palisade-lean.glb',
    ),
  },
];
const tileSize = 420;
const labelHeight = 42;
const titleHeight = 76;
const gap = 8;
mkdirSync(outputDirectory, { recursive: true });
mkdirSync(tileDirectory, { recursive: true });

const browser = await chromium.launch({ headless: true });
const records = [];
try {
  for (const panel of panels) {
    const tilePath = path.join(tileDirectory, `${panel.id}.png`);
    if (panel.kind === 'image') {
      await sharp(panel.file)
        .resize(tileSize, tileSize, { fit: 'contain', background: '#171b1f' })
        .png()
        .toFile(tilePath);
      records.push(record(panel, tilePath, { render: 'approved source image' }));
      continue;
    }
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
    const url = new URL('/scripts/class-models/model-turntable-review.html', baseUrl);
    url.searchParams.set('model', `/${path.relative(repository, panel.file)}`);
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
        `${panel.id}: comparison render failed ` +
          JSON.stringify({ canvasKind, modelResponses, pageErrors, failedRequests }),
      );
    await page.locator('canvas').screenshot({ path: tilePath, animations: 'disabled' });
    records.push(
      record(panel, tilePath, {
        render: 'neutral WebGL turntable',
        camera: '+x/+y/-z front-right three-quarter',
        facing: '+x',
        canvasKind,
        modelResponse: modelResponses[0],
      }),
    );
    await context.close();
  }
} finally {
  await browser.close();
}

const cellWidth = tileSize + gap;
const width = cellWidth * panels.length - gap;
const height = titleHeight + labelHeight + tileSize;
const composites = [
  {
    input: Buffer.from(
      `<svg width="${width}" height="${titleHeight}" xmlns="http://www.w3.org/2000/svg">` +
        '<rect width="100%" height="100%" fill="#080d12"/>' +
        '<text x="18" y="31" fill="#e8eef2" font-size="20" font-family="monospace" font-weight="700">' +
        'PALISADE · RETEXTURE ROUTE CALIBRATION</text>' +
        '<text x="18" y="58" fill="#9fb0bb" font-size="15" font-family="monospace">' +
        'approved source + same neutral fixed camera for every 3D derivative</text></svg>',
    ),
    left: 0,
    top: 0,
  },
];
for (const [index, panel] of panels.entries()) {
  const left = index * cellWidth;
  composites.push({
    input: Buffer.from(
      `<svg width="${tileSize}" height="${labelHeight}" xmlns="http://www.w3.org/2000/svg">` +
        '<rect width="100%" height="100%" fill="#10151a"/>' +
        `<text x="12" y="27" fill="#dbe7ef" font-size="16" font-family="monospace" font-weight="700">${panel.label}</text></svg>`,
    ),
    left,
    top: titleHeight,
  });
  composites.push({
    input: readFileSync(path.join(tileDirectory, `${panel.id}.png`)),
    left,
    top: titleHeight + labelHeight,
  });
}
const comparisonPath = path.join(outputDirectory, 'comparison.png');
await sharp({ create: { width, height, channels: 4, background: '#080d12' } })
  .composite(composites)
  .png()
  .toFile(comparisonPath);
writeFileSync(
  path.join(outputDirectory, 'evidence.json'),
  `${JSON.stringify({
    schemaVersion: 1,
    retextureTaskId: '019ff0d7-b4d8-759a-ae34-6bb6fe42e0a9',
    sourceAudit:
      'art/class-models/runtime-tiers/arc-relay/meshy7-palisade-multiview-retexture-calibration-v1/audit.json',
    comparison: {
      file: path.relative(repository, comparisonPath),
      sha256: sha256(readFileSync(comparisonPath)),
    },
    panels: records,
    runtimeAssetsPromoted: false,
  }, null, 2)}\n`,
);
console.log('Captured Palisade multiview retexture calibration comparison.');

function record(panel, tilePath, extra) {
  return {
    id: panel.id,
    label: panel.label,
    source: path.relative(repository, panel.file),
    sourceSha256: sha256(readFileSync(panel.file)),
    tile: path.relative(repository, tilePath),
    tileSha256: sha256(readFileSync(tilePath)),
    ...extra,
  };
}

function sha256(value) {
  return createHash('sha256').update(value).digest('hex');
}
