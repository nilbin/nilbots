#!/usr/bin/env node

import { createHash } from 'node:crypto';
import { createRequire } from 'node:module';
import { existsSync, mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const repository = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..', '..');
const requireFromWeb = createRequire(path.join(repository, 'web/package.json'));
const { chromium } = requireFromWeb('playwright');
const sharp = requireFromWeb('sharp');
const baseUrl =
  process.env.REVIEW_URL ??
  'http://127.0.0.1:9164/' +
    '?standalone&audio=off&camera=director&proof=longshot-objectives-review&proof-camera=director';
const output = path.join(
  repository,
  'art/reviews/arc-relay-longshot-objectives-meshy7-v1/combined-gameplay-review',
);
const buildRecord = JSON.parse(
  readFileSync(
    path.join(
      repository,
      'art/reviews/arc-relay-longshot-objectives-meshy7-v1/combined-gameplay-review/build-record.json',
    ),
    'utf8',
  ),
);
const expectedModels = new Map(
  buildRecord.emittedCandidates.map((entry) => [path.basename(entry.emitted.file), entry.id]),
);
const allCaptures = [
  { id: 'desktop-lantern-flare-focus', replay: 'lantern-survey-flare-focus', time: 2.35, width: 1440, height: 900, requiredCandidates: ['survey-flare'], reviewOperation: 'arc-signature-0' },
  { id: 'desktop-reactor-walkover-west', replay: 'all-four-hunter-west', time: 1.35, width: 1440, height: 900 },
  { id: 'desktop-flare-flight-west', replay: 'all-four-hunter-west', time: 10.7, width: 1440, height: 900 },
  { id: 'desktop-well-overlap-west', replay: 'all-four-hunter-west', time: 20.35, width: 1440, height: 900 },
  { id: 'desktop-longshot-reactor-west', replay: 'all-four-hunter-west', time: 88.35, width: 1440, height: 900 },
  { id: 'desktop-well-overlap-east', replay: 'all-four-hunter-east', time: 20.35, width: 1440, height: 900 },
  { id: 'phone-lantern-flare-focus', replay: 'lantern-survey-flare-focus', time: 2.35, width: 844, height: 390, mobile: true, requiredCandidates: ['survey-flare'], reviewOperation: 'arc-signature-0' },
  { id: 'phone-flare-flight-east', replay: 'all-four-hunter-east', time: 10.7, width: 844, height: 390, mobile: true },
  { id: 'phone-longshot-reactor-east', replay: 'all-four-hunter-east', time: 90.35, width: 844, height: 390, mobile: true },
];
const requestedCaptureIds = new Set(
  (process.env.REVIEW_CAPTURE_IDS ?? '')
    .split(',')
    .map((value) => value.trim())
    .filter(Boolean),
);
const targeted = requestedCaptureIds.size > 0;
const captures = targeted
  ? allCaptures.filter((capture) => requestedCaptureIds.has(capture.id))
  : allCaptures;
if (captures.length === 0)
  throw new Error('REVIEW_CAPTURE_IDS did not match a configured capture.');

mkdirSync(output, { recursive: true });
const browser = await chromium.launch({ headless: true });
const records = [];
try {
  for (const mobile of [false, true]) {
    const grouped = captures.filter((capture) => (capture.mobile ?? false) === mobile);
    if (grouped.length === 0) continue;
    const context = await browser.newContext({
      viewport: { width: grouped[0].width, height: grouped[0].height },
      deviceScaleFactor: 1,
      isMobile: mobile,
      hasTouch: mobile,
    });
    try {
      const page = await context.newPage();
      const session = await preparePage(page);
      for (const capture of grouped)
        records.push(await captureFrame(page, session, capture));
    } finally {
      await context.close();
    }
  }
} finally {
  await browser.close();
}

if (targeted) {
  const evidencePath = path.join(output, 'promoted-defaults-evidence.json');
  const previous = existsSync(evidencePath)
    ? JSON.parse(readFileSync(evidencePath, 'utf8'))
    : null;
  const capturesById = new Map(
    [...(previous?.captures ?? []), ...records].map((record) => [record.id, record]),
  );
  const mergedCaptures = allCaptures
    .map((capture) => capturesById.get(capture.id))
    .filter(Boolean);
  const evidence = {
    schemaVersion: 1,
    purpose:
      'Fast targeted phone/desktop proof of the promoted Railback, Survey Flare, Team Reactor, and Core Well defaults',
    sourceUrl: baseUrl,
    targeted: true,
    capturedIds: mergedCaptures.map((record) => record.id),
    runtimeAssetsPromoted: buildRecord.runtimeAssetsPromoted,
    buildRecord:
      'art/reviews/arc-relay-longshot-objectives-meshy7-v1/combined-gameplay-review/build-record.json',
    expectedCandidateModels: Object.fromEntries(expectedModels),
    captures: mergedCaptures,
  };
  writeFileSync(
    evidencePath,
    `${JSON.stringify(evidence, null, 2)}\n`,
  );
  console.log(`Captured ${records.length} targeted Longshot/objectives review frame(s).`);
} else {
  const desktop = records.filter((entry) => !entry.mobile);
  const phone = records.filter((entry) => entry.mobile);
  const sheets = {
    desktop: await contactSheet(desktop, 720, 'desktop-gameplay-review.webp'),
    phone: await contactSheet(phone, 720, 'phone-gameplay-review.webp'),
  };
  const evidence = {
    schemaVersion: 1,
    purpose:
      'Current-build real WebGL review of Longshot, Survey Flare, Team Reactor, and Core Well together, plus a focused Lantern stage',
    sourceUrl: baseUrl,
    runtimeAssetsPromoted: buildRecord.runtimeAssetsPromoted,
    buildRecord:
      'art/reviews/arc-relay-longshot-objectives-meshy7-v1/combined-gameplay-review/build-record.json',
    expectedCandidateModels: Object.fromEntries(expectedModels),
    captures: records,
    contactSheets: sheets,
  };
  writeFileSync(path.join(output, 'evidence.json'), `${JSON.stringify(evidence, null, 2)}\n`);
  console.log(`Captured ${records.length} Longshot/objectives gameplay review frames.`);
}

async function preparePage(page) {
  const errors = [];
  const modelResponses = [];
  page.on('pageerror', (error) => errors.push(`page: ${error.message}`));
  page.on('console', (message) => {
    if (message.type() === 'error') errors.push(`console: ${message.text()}`);
  });
  page.on('requestfailed', (request) =>
    errors.push(`request: ${request.url()} ${request.failure()?.errorText ?? ''}`),
  );
  page.on('response', (response) => {
    const filename = path.basename(new URL(response.url()).pathname);
    if (filename.endsWith('.glb')) modelResponses.push({ filename, status: response.status() });
  });
  await page.addInitScript(() => {
    localStorage.setItem('nilbots.soundtrack.enabled.v1', 'false');
    try {
      Object.defineProperty(Element.prototype, 'requestFullscreen', {
        configurable: true,
        value: undefined,
      });
    } catch {
      // CSS immersive mode is sufficient for deterministic review capture.
    }
  });
  await page.goto(baseUrl, { waitUntil: 'domcontentloaded' });
  await page.locator('[data-play-overlay="ready"]').waitFor({ timeout: 120_000 });
  await page.getByRole('button', { name: 'Play match' }).click();
  await page.getByRole('button', { name: /Pause/ }).evaluate((button) => button.click());
  await page.waitForFunction(
    () => window.__BOTARENA_REVIEW_CONTROL__?.playing === false,
    undefined,
    { timeout: 10_000 },
  );
  const fullscreen = page.getByRole('button', { name: 'full screen' });
  if (await fullscreen.count()) await fullscreen.click();
  if (errors.length) throw new Error(`initial renderer load: ${errors.join('\n')}`);
  return { errors, modelResponses };
}

async function captureFrame(page, session, capture) {
  await selectReplay(page, capture.replay);
  await seekAndSettle(page, capture.time);
  if (capture.reviewOperation) {
    const framed = await page.evaluate(
      (operationId) => window.__BOTARENA_RENDER_PROOF__?.frameSignature(operationId),
      capture.reviewOperation,
    );
    if (!framed)
      throw new Error(`${capture.id}: could not frame ${capture.reviewOperation}.`);
  }
  await page.waitForTimeout(1800);
  const arena = page.getByRole('region', { name: 'Arena' });
  const canvas = arena.locator('canvas');
  const renderer = await canvas.evaluate((node) => (node.getContext('2d') ? '2d' : 'webgl'));
  const proof = await page.evaluate(() => window.__BOTARENA_RENDER_PROOF__?.sample());
  const imagePath = path.join(output, `${capture.id}.png`);
  await canvas.screenshot({ path: imagePath });
  const image = readFileSync(imagePath);
  const loadedCandidates = [...new Set(
    session.modelResponses
      .filter((entry) => entry.status === 200 && expectedModels.has(entry.filename))
      .map((entry) => expectedModels.get(entry.filename)),
  )].sort();
  if (renderer !== 'webgl') throw new Error(`${capture.id}: expected WebGL, received ${renderer}.`);
  if (session.errors.length) throw new Error(`${capture.id}: ${session.errors.join('\n')}`);
  for (const required of capture.requiredCandidates ?? ['longshot', 'team-reactor', 'core-well'])
    if (!loadedCandidates.includes(required))
      throw new Error(`${capture.id}: candidate ${required} did not load.`);
  if (capture.id.includes('flare') && !loadedCandidates.includes('survey-flare'))
    throw new Error(`${capture.id}: Survey Flare candidate did not load in its live phase.`);
  return {
    ...capture,
    mobile: capture.mobile ?? false,
    renderer,
    proofTime: proof?.time ?? null,
    replaySeed: proof?.replaySeed ?? null,
    visibleSignatures: proof?.visibleSignatures ?? [],
    loadedCandidates,
    image: path.relative(repository, imagePath),
    imageBytes: image.length,
    imageSha256: sha256(image),
  };
}

async function selectReplay(page, id) {
  const choices = JSON.parse(
    readFileSync(
      path.join(
        repository,
        'sandbox/longshot-objectives-meshy7-v1/combined-review/site/replays.json',
      ),
      'utf8',
    ),
  );
  const choice = choices.find((entry) => entry.id === id);
  if (!choice) throw new Error(`Unknown review replay ${id}.`);
  const button = page
    .getByRole('navigation', { name: 'Choose a replay to review' })
    .getByRole('button')
    .filter({ has: page.locator(`span:text-is("${choice.bots.join(' v ')}")`) });
  if ((await button.getAttribute('aria-current')) !== 'true') {
    await button.evaluate((node) => node.click());
  }
  await page.waitForFunction(
    (ticks) => {
      const transport = window.__BOTARENA_REVIEW_CONTROL__;
      const proof = window.__BOTARENA_RENDER_PROOF__?.sample();
      return transport?.tickCount === ticks &&
        proof?.tickCount === ticks &&
        transport?.replaySeed === proof?.replaySeed;
    },
    choice.ticks,
    { timeout: 60_000 },
  );
  const overlay = page.locator('[data-play-overlay]');
  if (await overlay.isVisible()) {
    await page.getByRole('button', { name: 'Play match' }).click();
    await page.getByRole('button', { name: /Pause/ }).evaluate((button) => button.click());
  }
}

async function seekAndSettle(page, expected) {
  const deadline = Date.now() + 60_000;
  while (Date.now() < deadline) {
    await page.evaluate((time) => window.__BOTARENA_REVIEW_CONTROL__?.seek(time), expected);
    await page.waitForTimeout(100);
    const actual = await page.evaluate(() => window.__BOTARENA_RENDER_PROOF__?.sample().time);
    if (Number.isFinite(actual) && Math.abs(actual - expected) < 0.025) {
      await page.waitForTimeout(80);
      return;
    }
  }
  throw new Error(`WebGL review did not settle at ${expected}.`);
}

async function contactSheet(entries, width, filename) {
  const labelHeight = 40;
  const gap = 8;
  const composites = [];
  let top = 0;
  for (const entry of entries) {
    const source = readFileSync(path.join(repository, entry.image));
    const metadata = await sharp(source).metadata();
    const height = Math.round(width * (metadata.height ?? 1) / (metadata.width ?? 1));
    const label = Buffer.from(
      `<svg width="${width}" height="${labelHeight}" xmlns="http://www.w3.org/2000/svg">` +
        '<rect width="100%" height="100%" fill="#080d12"/>' +
        `<text x="12" y="26" fill="#dbe7ef" font-size="15" font-family="monospace">` +
        `${entry.id.toUpperCase()} · T ${entry.time.toFixed(2)}</text></svg>`,
    );
    composites.push({ input: label, left: 0, top });
    composites.push({
      input: await sharp(source).resize(width, height).png().toBuffer(),
      left: 0,
      top: top + labelHeight,
    });
    top += labelHeight + height + gap;
  }
  const destination = path.join(output, filename);
  await sharp({ create: { width, height: top - gap, channels: 4, background: '#080d12' } })
    .composite(composites)
    .webp({ quality: 90 })
    .toFile(destination);
  const bytes = readFileSync(destination);
  return { file: path.relative(repository, destination), bytes: bytes.length, sha256: sha256(bytes) };
}

function sha256(value) {
  return createHash('sha256').update(value).digest('hex');
}
