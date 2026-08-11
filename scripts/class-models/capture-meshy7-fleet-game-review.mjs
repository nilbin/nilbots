#!/usr/bin/env node

import { createHash } from 'node:crypto';
import { createRequire } from 'node:module';
import { mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { gunzipSync } from 'node:zlib';

const repository = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..', '..');
const requireFromWeb = createRequire(path.join(repository, 'web/package.json'));
const { chromium } = requireFromWeb('playwright');
const sharp = requireFromWeb('sharp');
const options = parseOptions(process.argv.slice(2));
const reviewUrl = options.url ?? 'http://127.0.0.1:8947/?standalone&audio=off';
const outputDirectory = path.resolve(
  repository,
  options.output ?? 'art/reviews/arc-relay-meshy7-fleet-v1',
);
const replayPath = path.join(
  repository,
  'sandbox/meshy7-pilot/fleet-game-review/match/replay.json.gz',
);
const installPath = path.join(
  repository,
  'sandbox/meshy7-pilot/fleet-game-review/interactive-install.json',
);
const replay = JSON.parse(gunzipSync(readFileSync(replayPath)));
const install = JSON.parse(readFileSync(installPath, 'utf8'));
const expectedModelFiles = new Set(
  install.emittedCandidateModels.map((entry) => path.basename(entry.emitted.file)),
);
const activeLookIds = new Set(
  replay.initialFrame.state.activeLives.map((life) =>
    replay.header.presentation.forms.find((form) => form.formId === life.formId)?.lookId,
  ),
);
if (activeLookIds.size !== 16 || expectedModelFiles.size !== 16)
  throw new Error('Fleet capture requires all 16 distinct classes at the opening tick.');

const viewports = [
  { id: 'desktop', width: 1440, height: 900 },
  { id: 'phone', width: 844, height: 390 },
];
const records = [];
mkdirSync(outputDirectory, { recursive: true });

const browser = await chromium.launch({ headless: true });
try {
  for (const viewport of viewports) records.push(await capture(viewport));
} finally {
  await browser.close();
}

const cardWidth = 720;
const labelHeight = 42;
const desktopHeight = Math.round(cardWidth * 900 / 1440);
const phoneHeight = Math.round(cardWidth * 390 / 844);
const gap = 12;
const background = '#080d12';
const composites = [];
let top = 0;
for (const [id, height] of [
  ['desktop', desktopHeight],
  ['phone', phoneHeight],
]) {
  const label = Buffer.from(
    `<svg width="${cardWidth}" height="${labelHeight}" xmlns="http://www.w3.org/2000/svg">` +
      `<rect width="100%" height="100%" fill="${background}"/>` +
      `<text x="14" y="28" fill="#dbe7ef" font-size="17" font-family="monospace">` +
      `${id.toUpperCase()} · TICK 000 · ALL 16 MESHY 7 BOTS</text></svg>`,
  );
  const frame = await sharp(path.join(outputDirectory, `${id}-arena.png`))
    .resize(cardWidth, height)
    .png()
    .toBuffer();
  composites.push({ input: label, left: 0, top });
  composites.push({ input: frame, left: 0, top: top + labelHeight });
  top += labelHeight + height + gap;
}
const overviewPath = path.join(outputDirectory, 'gameplay-overview.png');
await sharp({
  create: { width: cardWidth, height: top - gap, channels: 4, background },
})
  .composite(composites)
  .png()
  .toFile(overviewPath);

writeFileSync(
  path.join(outputDirectory, 'evidence.json'),
  `${JSON.stringify({
    schemaVersion: 1,
    purpose: 'Unapproved Meshy 7 complete-fleet real-game owner review',
    url: reviewUrl,
    canonicalReplay: path.relative(repository, replayPath),
    canonicalReplayHash: replay.replayHash,
    tick: 0,
    activeLookIds: [...activeLookIds].sort(),
    expectedCandidateModels: [...expectedModelFiles].sort(),
    captures: records,
    overview: {
      file: path.relative(repository, overviewPath),
      sha256: sha256(readFileSync(overviewPath)),
    },
  }, null, 2)}\n`,
);
console.log(`Captured ${records.length} complete-fleet WebGL review frames.`);

async function capture(viewport) {
  const context = await browser.newContext({
    viewport: { width: viewport.width, height: viewport.height },
    deviceScaleFactor: 1,
  });
  const page = await context.newPage();
  const pageErrors = [];
  const consoleErrors = [];
  const failedRequests = [];
  const modelResponses = [];
  page.on('pageerror', (error) => pageErrors.push(error.message));
  page.on('console', (message) => {
    if (message.type() === 'error') consoleErrors.push(message.text());
  });
  page.on('requestfailed', (request) =>
    failedRequests.push(`${request.method()} ${request.url()} ${request.failure()?.errorText ?? ''}`),
  );
  page.on('response', (response) => {
    if (response.url().endsWith('.glb'))
      modelResponses.push({ url: response.url(), status: response.status() });
  });

  const replayIndexResponsePromise = page.waitForResponse(
    (response) => new URL(response.url()).pathname.endsWith('/replays.json'),
    { timeout: 120_000 },
  );
  const replayPayloadResponsePromise = page.waitForResponse(
    (response) => {
      const pathname = new URL(response.url()).pathname;
      return (
        pathname.endsWith('/replay.json') ||
        (pathname.includes('/replays/') && pathname.endsWith('.json'))
      );
    },
    { timeout: 120_000 },
  );

  const started = Date.now();
  await page.goto(reviewUrl, { waitUntil: 'domcontentloaded' });
  const replayResponses = await Promise.all(
    [await replayIndexResponsePromise, await replayPayloadResponsePromise].map(
      async (response) => ({
        url: response.url(),
        status: response.status(),
        contentEncoding: await response.headerValue('content-encoding'),
      }),
    ),
  );
  if (
    replayResponses.some(
      (response) => response.status !== 200 || response.contentEncoding !== 'gzip',
    )
  )
    throw new Error(
      `${viewport.id}: replay index and payload must both arrive gzip-encoded: ` +
        JSON.stringify(replayResponses),
    );
  await page.locator('[data-play-overlay="ready"]').waitFor({ timeout: 120_000 });
  const readyMilliseconds = Date.now() - started;
  await page.getByRole('button', { name: 'Play match' }).click();
  await page.locator('[data-play-overlay]').waitFor({ state: 'detached' });
  await page.getByRole('button', { name: 'Pause' }).evaluate((button) => button.click());
  await page.getByRole('button', { name: 'Restart' }).evaluate((button) => button.click());
  await page.getByRole('button', { name: 'Pause' }).evaluate((button) => button.click());
  for (let attempts = 0; attempts < 120 && (await readTick(page)) > 0; attempts += 1)
    await page
      .getByRole('button', { name: 'Step back one tick' })
      .evaluate((button) => button.click());
  if ((await readTick(page)) !== 0) throw new Error(`${viewport.id}: replay escaped tick 000.`);

  const fullscreen = page.getByRole('button', { name: 'full screen' });
  if (await fullscreen.count()) await fullscreen.click();
  const arena = page.getByRole('region', { name: 'Arena' });
  await page.waitForFunction(
    ({ width, height }) => {
      const region = document.querySelector('section[aria-label="Arena"]');
      const bounds = region?.getBoundingClientRect();
      return Math.round(bounds?.width ?? 0) === width && Math.round(bounds?.height ?? 0) === height;
    },
    { width: viewport.width, height: viewport.height },
  );
  const cameraToggle = page.getByRole('button', { name: /director|overview/i });
  if ((await cameraToggle.textContent())?.toLowerCase().includes('director'))
    await cameraToggle.click();
  await page.getByRole('button', { name: /overview/i }).waitFor();
  await page.waitForTimeout(5_000);

  const canvasKind = await arena.locator('canvas').evaluate((canvas) =>
    canvas.getContext('2d') ? '2d' : 'webgl',
  );
  if (canvasKind !== 'webgl') throw new Error(`${viewport.id}: renderer fell back to Canvas2D.`);
  const loadedModelFiles = new Set(
    modelResponses
      .filter((response) => response.status === 200)
      .map((response) => path.basename(new URL(response.url).pathname)),
  );
  const missingModels = [...expectedModelFiles].filter((file) => !loadedModelFiles.has(file));
  if (missingModels.length)
    throw new Error(`${viewport.id}: missing candidate GLBs ${missingModels.join(', ')}.`);
  if (
    modelResponses.some((response) => response.status !== 200) ||
    pageErrors.length ||
    consoleErrors.length ||
    failedRequests.length
  )
    throw new Error(
      `${viewport.id}: review emitted errors:\n${[
        ...pageErrors,
        ...consoleErrors,
        ...failedRequests,
      ].join('\n')}`,
    );

  const framePath = path.join(outputDirectory, `${viewport.id}-arena.png`);
  await arena.screenshot({ path: framePath, animations: 'disabled' });
  const arenaBounds = await arena.boundingBox();
  await context.close();
  return {
    viewport: viewport.id,
    viewportPixels: { width: viewport.width, height: viewport.height },
    arenaPixels: arenaBounds && {
      width: Math.round(arenaBounds.width),
      height: Math.round(arenaBounds.height),
    },
    readyMilliseconds,
    canvasKind,
    loadedCandidateModels: [...expectedModelFiles].sort(),
    glbResponses: modelResponses,
    replayResponses,
    screenshot: path.relative(repository, framePath),
    screenshotSha256: sha256(readFileSync(framePath)),
    pageErrors,
    consoleErrors,
    failedRequests,
  };
}

async function readTick(page) {
  const label = await page
    .getByRole('region', { name: 'Arena' })
    .locator('p')
    .filter({ hasText: /^\s*\d{3}\s*\/\s*\d{3}\s*$/ })
    .first()
    .textContent();
  const match = label?.match(/^\s*(\d+)\s*\//);
  if (!match) throw new Error(`Could not read replay tick from ${label ?? 'nothing'}.`);
  return Number(match[1]);
}

function parseOptions(args) {
  const parsed = {};
  for (let index = 0; index < args.length; index += 2) {
    const name = args[index];
    const value = args[index + 1];
    if (!name?.startsWith('--') || !value)
      throw new Error(`Expected --name value, received ${name ?? 'nothing'}.`);
    parsed[name.slice(2)] = value;
  }
  return parsed;
}

function sha256(value) {
  return createHash('sha256').update(value).digest('hex');
}
