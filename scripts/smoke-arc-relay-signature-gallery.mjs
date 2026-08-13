#!/usr/bin/env node

/** Prove the public Arc signature gallery and its linked phone replay surface. */

import { createHash } from 'node:crypto';
import { createRequire } from 'node:module';
import { mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const repository = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const requireFromWeb = createRequire(path.join(repository, 'web', 'package.json'));
const { chromium } = requireFromWeb('playwright');
const options = parseOptions(process.argv.slice(2));
if (!options.url) throw new Error('Missing --url for the exact public gallery URL.');
const galleryUrl = new URL(options.url).href;
const output = path.resolve(repository, options.output ??
  'art/reviews/arc-relay-presentation-batch-v1/runtime-proof/captures');
mkdirSync(output, { recursive: true });
const screenshotPath = path.join(output, 'public-gallery-phone.webp');
const evidencePath = path.join(output, 'public-gallery-proof.json');
const errors = [];
const responses = [];

const browser = await chromium.launch({ headless: true });
let evidence;
try {
  const context = await browser.newContext({
    viewport: { width: 390, height: 844 },
    deviceScaleFactor: 1,
    isMobile: true,
    hasTouch: true,
  });
  const page = await context.newPage();
  page.on('pageerror', (error) => errors.push(`page: ${error.message}`));
  page.on('console', (message) => {
    if (message.type() === 'error') errors.push(`console: ${message.text()}`);
  });
  page.on('requestfailed', (request) =>
    errors.push(`request: ${request.url()} ${request.failure()?.errorText ?? ''}`));
  page.on('response', async (response) => {
    const pathname = new URL(response.url()).pathname;
    if (pathname.endsWith('/replays.json') ||
      (pathname.includes('/replays/') && pathname.endsWith('.json'))) {
      responses.push({
        url: response.url(),
        status: response.status(),
        contentEncoding: await response.headerValue('content-encoding'),
      });
    }
  });

  const galleryResponse = await page.goto(galleryUrl, { waitUntil: 'networkidle' });
  await page.getByRole('heading', { name: 'Watch every signature move.' }).waitFor();
  await page.locator('[data-motion-card]').nth(15).waitFor();
  const motionCards = await page.locator('[data-motion-card]').count();
  const videos = await page.locator('[data-motion-card] video').count();
  if (motionCards !== 16 || videos !== 32)
    throw new Error(`Expected 16 motion cards and 32 videos; got ${motionCards} and ${videos}.`);
  if (!galleryResponse?.ok()) throw new Error(`Gallery returned HTTP ${galleryResponse?.status()}.`);
  await page.locator('[data-motion-card]').first().scrollIntoViewIfNeeded();
  await page.waitForTimeout(250);
  await page.screenshot({ path: screenshotPath });

  const viewerUrl = await page.getByRole('link', { name: /watch the two live replays/i })
    .evaluate((node) => node.href);
  await page.goto(viewerUrl, { waitUntil: 'domcontentloaded' });
  await page.locator('[data-play-overlay="ready"]').waitFor({ timeout: 90_000 });
  const canvas = page.getByRole('region', { name: 'Arena' }).locator('canvas');
  await canvas.waitFor({ timeout: 90_000 });
  const renderer = await canvas.evaluate((node) => node.getContext('2d') ? '2d' : 'webgl');
  if (renderer !== 'webgl') throw new Error(`Expected WebGL, mounted ${renderer}.`);
  await page.waitForFunction(() => Boolean(window.__BOTARENA_RENDER_PROOF__?.sample()));
  await page.waitForTimeout(250);

  const replayResponses = responses.filter((entry) =>
    new URL(entry.url).pathname.endsWith('/replays.json') ||
    new URL(entry.url).pathname.includes('/replays/'));
  if (replayResponses.length < 2 || replayResponses.some((entry) =>
    entry.status !== 200 || entry.contentEncoding !== 'gzip')) {
    throw new Error(`Replay list and payload were not both gzip encoded: ${JSON.stringify(replayResponses)}`);
  }
  if (errors.length) throw new Error(errors.join('\n'));
  evidence = {
    schema: 'arc-relay-signature-public-gallery-proof-v1',
    capturedAt: new Date().toISOString(),
    exactPublicGalleryUrl: galleryUrl,
    galleryStatus: galleryResponse.status(),
    galleryContentEncoding: await galleryResponse.headerValue('content-encoding'),
    viewport: { width: 390, height: 844, mobile: true, touch: true },
    motionCards,
    videos,
    viewerUrl,
    renderer,
    replayResponses,
    errors,
    screenshot: relative(screenshotPath),
    screenshotSha256: sha256(readFileSync(screenshotPath)),
    passed: true,
  };
  await context.close();
} catch (error) {
  evidence = {
    schema: 'arc-relay-signature-public-gallery-proof-v1',
    capturedAt: new Date().toISOString(),
    exactPublicGalleryUrl: galleryUrl,
    errors,
    failure: String(error?.stack ?? error),
    passed: false,
  };
  throw error;
} finally {
  writeFileSync(evidencePath, `${JSON.stringify(evidence, null, 2)}\n`);
  await browser.close();
}

console.log(`Passed public phone gallery smoke: ${galleryUrl}`);

function parseOptions(args) {
  const parsed = {};
  for (let index = 0; index < args.length; index += 2) {
    const key = args[index];
    const value = args[index + 1];
    if (!key?.startsWith('--') || value === undefined) throw new Error(`Invalid option ${key ?? ''}.`);
    parsed[key.slice(2)] = value;
  }
  return parsed;
}

function relative(value) {
  return path.relative(repository, value).split(path.sep).join('/');
}

function sha256(value) {
  return createHash('sha256').update(value).digest('hex');
}
