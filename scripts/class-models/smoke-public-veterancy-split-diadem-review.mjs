#!/usr/bin/env node

/** Phone-smoke the exact public owner-gallery URL and its linked live replay viewer. */

import { createRequire } from 'node:module';
import { mkdirSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const repository = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..', '..');
const requireFromWeb = createRequire(path.join(repository, 'web/package.json'));
const { chromium } = requireFromWeb('playwright');
const publicUrl = process.argv[2];
if (!publicUrl || !/^https:\/\//.test(publicUrl))
  throw new Error('Pass the exact public HTTPS gallery URL.');
const output = path.join(
  repository,
  'art/reviews/arc-relay-veterancy-modules-meshy7-v1/runtime-game-review',
);
mkdirSync(output, { recursive: true });

const browser = await chromium.launch({ headless: true });
const context = await browser.newContext({
  viewport: { width: 390, height: 844 },
  deviceScaleFactor: 1,
  isMobile: true,
  hasTouch: true,
});
const page = await context.newPage();
const errors = { page: [], console: [], requests: [] };
const replayResponses = [];
page.on('pageerror', (error) => errors.page.push(error.message));
page.on('console', (message) => {
  if (message.type() === 'error') errors.console.push(message.text());
});
page.on('requestfailed', (request) =>
  errors.requests.push(
    `${request.method()} ${request.url()}: ${request.failure()?.errorText ?? 'failed'}`,
  ));
page.on('response', async (response) => {
  const pathname = new URL(response.url()).pathname;
  if (pathname.endsWith('/replays.json') || pathname.includes('/replays/')) {
    replayResponses.push({
      url: response.url(),
      status: response.status(),
      contentEncoding: await response.headerValue('content-encoding'),
    });
  }
});

try {
  await page.goto(publicUrl, { waitUntil: 'networkidle', timeout: 120_000 });
  await page.getByRole('heading', { name: 'Veterancy split diadem' }).waitFor();
  const images = await page.locator('img').evaluateAll((nodes) =>
    nodes.map((node) => ({
      src: node.currentSrc,
      complete: node.complete,
      width: node.naturalWidth,
      height: node.naturalHeight,
    })),
  );
  if (
    images.length !== 6 ||
    images.some((image) => !image.complete || image.width <= 0 || image.height <= 0)
  )
    throw new Error(`Public gallery did not load all six review images: ${JSON.stringify(images)}`);
  await page.screenshot({
    path: path.join(output, 'public-gallery-phone-smoke.png'),
    fullPage: true,
  });

  await Promise.all([
    page.waitForURL(/\/viewer\//, { timeout: 30_000 }),
    page.getByRole('link', { name: 'Open the playable replay viewer' }).click(),
  ]);
  await page.setViewportSize({ width: 844, height: 390 });
  await page.locator('[data-play-overlay="ready"]').waitFor({ timeout: 120_000 });
  await page.getByRole('button', { name: 'Play match' }).click();
  await page.waitForTimeout(1_500);
  const arena = page.getByRole('region', { name: 'Arena' });
  const canvas = arena.locator('canvas');
  const renderer = await canvas.evaluate((node) =>
    node.getContext('2d') ? '2d' : 'webgl',
  );
  if (renderer !== 'webgl') throw new Error(`Public viewer mounted ${renderer}.`);
  await arena.screenshot({ path: path.join(output, 'public-viewer-phone-smoke.png') });

  const replayIndex = replayResponses.find((response) =>
    new URL(response.url).pathname.endsWith('/replays.json'),
  );
  const replayPayload = replayResponses.find((response) =>
    new URL(response.url).pathname.includes('/replays/'),
  );
  if (
    replayIndex?.status !== 200 ||
    replayPayload?.status !== 200 ||
    replayIndex.contentEncoding !== 'gzip' ||
    replayPayload.contentEncoding !== 'gzip'
  )
    throw new Error(`Public replay transfer was not two clean gzip responses: ${JSON.stringify(replayResponses)}`);
  if (errors.page.length || errors.console.length || errors.requests.length)
    throw new Error(`Public review emitted browser errors: ${JSON.stringify(errors)}`);

  writeFileSync(
    path.join(output, 'public-smoke.json'),
    `${JSON.stringify(
      {
        schemaVersion: 1,
        exactPublicGalleryUrl: publicUrl,
        galleryImages: images,
        linkedViewerUrl: page.url(),
        renderer,
        replayResponses,
        errors,
        screenshots: [
          'art/reviews/arc-relay-veterancy-modules-meshy7-v1/runtime-game-review/public-gallery-phone-smoke.png',
          'art/reviews/arc-relay-veterancy-modules-meshy7-v1/runtime-game-review/public-viewer-phone-smoke.png',
        ],
      },
      null,
      2,
    )}\n`,
  );
  console.log(`Phone-smoked gallery and WebGL viewer at ${publicUrl}.`);
} finally {
  await context.close();
  await browser.close();
}
