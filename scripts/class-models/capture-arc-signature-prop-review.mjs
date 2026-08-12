#!/usr/bin/env node

import { createHash } from 'node:crypto';
import { createRequire } from 'node:module';
import { mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const repository = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..', '..');
const requireFromWeb = createRequire(path.join(repository, 'web', 'package.json'));
const { chromium } = requireFromWeb('playwright');
const sharp = requireFromWeb('sharp');
const url = process.env.REVIEW_URL ?? 'http://127.0.0.1:4175/?standalone&audio=off';
const output = path.resolve(
  repository,
  process.env.REVIEW_OUTPUT ?? 'art/reviews/arc-relay-signature-props',
);
const captureCatalog = [
  { id: 'desktop-overview-tick-60', viewport: { width: 1440, height: 900 }, tick: 60 },
  { id: 'desktop-overview-tick-90', viewport: { width: 1440, height: 900 }, tick: 90 },
  {
    id: 'phone-landscape-overview-tick-60',
    viewport: { width: 844, height: 390 },
    tick: 60,
    mobile: true,
  },
];
const requestedCaptures = new Set(
  (process.env.REVIEW_CAPTURE_IDS ?? '').split(',').map((value) => value.trim()).filter(Boolean),
);
const captures = requestedCaptures.size
  ? captureCatalog.filter((capture) => requestedCaptures.has(capture.id))
  : captureCatalog;
if (requestedCaptures.size && captures.length !== requestedCaptures.size)
  throw new Error('REVIEW_CAPTURE_IDS contains an unknown capture ID.');

mkdirSync(output, { recursive: true });
const browser = await chromium.launch({ headless: true });
const evidence = [];
try {
  for (const capture of captures) evidence.push(await takeCapture(capture));
} finally {
  await browser.close();
}

const images = evidence.map((entry) => readFileSync(path.join(repository, entry.image)));
const metadata = await Promise.all(images.map((image) => sharp(image).metadata()));
const width = Math.max(...metadata.map((item) => item.width ?? 0));
const rows = [];
let top = 0;
for (const [index, image] of images.entries()) {
  rows.push({ input: image, left: 0, top });
  top += metadata[index]?.height ?? 0;
}
const sheetPath = path.join(output, 'gameplay-scale-review.webp');
await sharp({
  create: { width, height: top, channels: 4, background: '#070b10' },
})
  .composite(rows)
  .webp({ quality: 90 })
  .toFile(sheetPath);

const record = {
  schemaVersion: 1,
  sourceUrl: url,
  replay: 'web/dist-review/replay.json',
  captures: evidence,
  contactSheet: path.relative(repository, sheetPath),
  contactSheetSha256: sha256(readFileSync(sheetPath)),
};
writeFileSync(path.join(output, 'evidence.json'), `${JSON.stringify(record, null, 2)}\n`);
console.log(`Captured ${evidence.length} gameplay-scale signature-prop reviews.`);

async function takeCapture(capture) {
  const context = await browser.newContext({
    viewport: capture.viewport,
    deviceScaleFactor: 1,
    isMobile: capture.mobile ?? false,
    hasTouch: capture.mobile ?? false,
  });
  const page = await context.newPage();
  const pageErrors = [];
  const consoleErrors = [];
  page.on('pageerror', (error) => pageErrors.push(error.message));
  page.on('console', (message) => {
    if (message.type() === 'error') consoleErrors.push(message.text());
  });
  await page.goto(`${url}&signatureReview=${capture.id}`, { waitUntil: 'domcontentloaded' });
  await page.locator('[data-play-overlay="ready"]').waitFor({ timeout: 60_000 });
  await page.getByRole('button', { name: 'Play match' }).click();
  await page.waitForTimeout(120);
  const pause = page.getByRole('button', { name: 'Pause match' });
  if (await pause.isVisible()) await pause.click();

  // The review is intentionally captured at gameplay scale. Viewer starts in overview;
  // keep it there rather than switching on the close-following director camera.
  const camera = page.getByRole('button').filter({ hasText: /director|overview/i }).first();
  if ((await camera.count()) && (await camera.getAttribute('aria-pressed')) === 'true') {
    await camera.click();
  }

  const timeline = page.locator('[aria-label^="Match timeline"]');
  const thumb = timeline.locator('[role="slider"]');
  const maximum = Number(await thumb.getAttribute('aria-valuemax'));
  const bounds = await timeline.boundingBox();
  if (!bounds || !Number.isFinite(maximum)) throw new Error('Could not measure replay timeline.');
  await page.mouse.click(
    bounds.x + bounds.width * (capture.tick / maximum),
    bounds.y + bounds.height / 2,
  );
  await page.waitForFunction(
    (expected) => {
      const slider = document.querySelector('[aria-label^="Match timeline"] [role="slider"]');
      return Math.round(Number(slider?.getAttribute('aria-valuenow'))) === expected;
    },
    capture.tick,
  );
  const fullscreen = page.getByRole('button', { name: 'full screen' });
  if (await fullscreen.isVisible()) await fullscreen.click();
  await page.waitForTimeout(800);

  const arena = page.getByRole('region', { name: 'Arena' });
  const canvas = arena.locator('canvas');
  const renderer = await canvas.evaluate((node) => (node.getContext('2d') ? '2d' : 'webgl'));
  const canvasPixels = await canvas.evaluate((node) => ({
    width: node.width,
    height: node.height,
    clientWidth: node.clientWidth,
    clientHeight: node.clientHeight,
  }));
  const imagePath = path.join(output, `${capture.id}.png`);
  await arena.screenshot({ path: imagePath });
  const image = readFileSync(imagePath);
  await context.close();
  if (renderer !== 'webgl') throw new Error(`${capture.id} mounted ${renderer}, expected WebGL.`);
  if (pageErrors.length || consoleErrors.length)
    throw new Error(`${capture.id} produced browser errors: ${[...pageErrors, ...consoleErrors].join('\n')}`);
  return {
    ...capture,
    renderer,
    canvasPixels,
    image: path.relative(repository, imagePath),
    imageBytes: image.length,
    imageSha256: sha256(image),
    pageErrors,
    consoleErrors,
  };
}

function sha256(value) {
  return createHash('sha256').update(value).digest('hex');
}
