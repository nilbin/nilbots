#!/usr/bin/env node

import { createHash } from 'node:crypto';
import { createRequire } from 'node:module';
import { mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const repository = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..', '..');
const requireFromWeb = createRequire(path.join(repository, 'web', 'package.json'));
const { chromium } = requireFromWeb('playwright');
const url = process.env.REVIEW_URL ?? 'http://127.0.0.1:4175/?standalone&audio=off';
const output = path.resolve(
  repository,
  process.env.REVIEW_OUTPUT ?? 'art/reviews/arc-relay-model-lighting-v1/candidate',
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
  (process.env.REVIEW_CAPTURE_IDS ?? '')
    .split(',')
    .map((value) => value.trim())
    .filter(Boolean),
);
const captures = requestedCaptures.size
  ? captureCatalog.filter((capture) => requestedCaptures.has(capture.id))
  : captureCatalog;
if (requestedCaptures.size && captures.length !== requestedCaptures.size)
  throw new Error('REVIEW_CAPTURE_IDS contains an unknown capture ID.');

mkdirSync(output, { recursive: true });
const evidencePath = path.join(output, 'evidence.json');
const evidence = loadEvidence();
let capturedThisRun = 0;
const browser = await chromium.launch({ headless: true });
try {
  const context = await browser.newContext({ deviceScaleFactor: 1 });
  for (const mobile of [false, true]) {
    const profileCaptures = captures.filter((capture) => Boolean(capture.mobile) === mobile);
    if (profileCaptures.length === 0) continue;
    const profile = mobile ? 'mobile' : 'desktop';
    console.log(`Loading ${profile} review profile once for ${profileCaptures.length} frame(s)...`);
    const page = await context.newPage();
    await page.setViewportSize(profileCaptures[0].viewport);
    const errors = monitorErrors(page);
    const replayResponses = await preparePage(page, profile);
    for (const capture of profileCaptures) {
      console.log(`Capturing ${capture.id} without reloading models...`);
      evidence.set(capture.id, await takeCapture(page, capture, errors, replayResponses));
      capturedThisRun += 1;
      writeEvidence();
    }
    await page.close();
  }
  await context.close();
} finally {
  await browser.close();
}

writeEvidence();
console.log(
  `Captured ${capturedThisRun} frame(s); evidence retains ${evidence.size} pinned frame(s).`,
);

function writeEvidence() {
  const ordered = captureCatalog
    .map((capture) => evidence.get(capture.id))
    .filter(Boolean);
  writeFileSync(
    evidencePath,
    `${JSON.stringify({ schemaVersion: 1, sourceUrl: url, captures: ordered }, null, 2)}\n`,
  );
}

function loadEvidence() {
  try {
    const prior = JSON.parse(readFileSync(evidencePath, 'utf8'));
    if (prior.sourceUrl !== url)
      throw new Error(
        `Refusing to mix capture sources ${JSON.stringify(prior.sourceUrl)} and ${JSON.stringify(url)}.`,
      );
    return new Map((prior.captures ?? []).map((capture) => [capture.id, capture]));
  } catch (error) {
    if (error?.code === 'ENOENT') return new Map();
    throw error;
  }
}

function monitorErrors(page) {
  const pageErrors = [];
  const consoleErrors = [];
  page.on('pageerror', (error) => pageErrors.push(error.message));
  page.on('console', (message) => {
    if (message.type() === 'error') consoleErrors.push(message.text());
  });
  return { pageErrors, consoleErrors };
}

async function preparePage(page, profile) {
  const replayIndex = page.waitForResponse(
    (response) => new URL(response.url()).pathname.endsWith('/replays.json'),
    { timeout: 120_000 },
  );
  const replayPayload = page.waitForResponse(
    (response) => {
      const pathname = new URL(response.url()).pathname;
      return pathname.endsWith('/replay.json') ||
        (pathname.includes('/replays/') && pathname.endsWith('.json'));
    },
    { timeout: 120_000 },
  );
  await page.goto(`${url}&render-profile=${profile}&modelLightingReview=${profile}`, {
    waitUntil: 'domcontentloaded',
  });
  const replayResponses = await Promise.all(
    [await replayIndex, await replayPayload].map(async (response) => ({
      url: response.url(),
      status: response.status(),
      contentEncoding: await response.headerValue('content-encoding'),
    })),
  );
  if (replayResponses.some(
    (response) => response.status !== 200 || response.contentEncoding !== 'gzip',
  ))
    throw new Error(
      `Replay index and payload must both arrive gzip-encoded: ${JSON.stringify(replayResponses)}`,
    );
  await page.locator('[data-play-overlay="ready"]').waitFor({ timeout: 90_000 });
  await page.getByRole('button', { name: 'Play match' }).click();
  const pause = page.getByRole('button', { name: 'Pause', exact: true });
  await pause.click({ force: true, timeout: 30_000 });
  await page.getByRole('button', { name: 'Play', exact: true }).waitFor({ state: 'attached' });

  const camera = page.getByRole('button').filter({ hasText: /director|overview/i }).first();
  if ((await camera.count()) && (await camera.getAttribute('aria-pressed')) === 'true')
    await camera.click();

  const fullscreen = page.getByRole('button', { name: 'full screen' });
  if (await fullscreen.isVisible()) await fullscreen.click();
  await page.waitForTimeout(1_000);
  return replayResponses;
}

async function takeCapture(page, capture, errors, replayResponses) {
  await page.setViewportSize(capture.viewport);
  const { pageErrors, consoleErrors } = errors;
  // Full-screen chrome fades while watching. Wake it before seeking so the exact tick can
  // be set without rebuilding the page or re-downloading the model fleet.
  await page.mouse.move(capture.viewport.width / 2, capture.viewport.height - 12);

  const timeline = page.locator('[aria-label^="Match timeline"]');
  const thumb = timeline.locator('[role="slider"]');
  const maximum = Number(await thumb.getAttribute('aria-valuemax'));
  const bounds = await timeline.boundingBox();
  if (!bounds || !Number.isFinite(maximum)) throw new Error('Could not measure replay timeline.');
  await page.mouse.click(
    bounds.x + bounds.width * (capture.tick / maximum),
    bounds.y + bounds.height / 2,
  );
  await thumb.focus();
  let actual = Math.round(Number(await thumb.getAttribute('aria-valuenow')));
  for (let step = 0; actual !== capture.tick && step <= maximum + 1; step += 1) {
    if (!Number.isFinite(actual)) throw new Error('Replay playhead did not expose a numeric tick.');
    await page.keyboard.press(actual < capture.tick ? 'ArrowRight' : 'ArrowLeft');
    actual = Math.round(Number(await thumb.getAttribute('aria-valuenow')));
  }
  if (actual !== capture.tick)
    throw new Error(`Could not seek replay from tick ${actual} to ${capture.tick}.`);
  await page.waitForFunction(
    (expected) => {
      const slider = document.querySelector('[aria-label^="Match timeline"] [role="slider"]');
      return Math.round(Number(slider?.getAttribute('aria-valuenow'))) === expected;
    },
    capture.tick,
    { timeout: 30_000 },
  );
  await page.waitForTimeout(1_000);

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
  if (renderer !== 'webgl') throw new Error(`${capture.id} mounted ${renderer}, expected WebGL.`);
  if (pageErrors.length || consoleErrors.length)
    throw new Error(
      `${capture.id} produced browser errors: ${[...pageErrors, ...consoleErrors].join('\n')}`,
    );
  return {
    ...capture,
    renderer,
    canvasPixels,
    image: path.relative(repository, imagePath),
    imageBytes: image.length,
    imageSha256: createHash('sha256').update(image).digest('hex'),
    replayResponses,
    pageErrors,
    consoleErrors,
  };
}
