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
const baseUrl =
  process.env.REVIEW_URL ??
  'http://127.0.0.1:9192/' +
    '?standalone&audio=off&camera=director&proof=railback-review&proof-camera=director';
const output = path.join(
  repository,
  'art/reviews/arc-relay-longshot-railback-v1/gameplay-review',
);
const reviewRoot = path.join(repository, 'sandbox/longshot-railback-v1/game-review/site');
const buildRecord = JSON.parse(
  readFileSync(path.join(output, 'build-record.json'), 'utf8'),
);
const candidateFilename = path.basename(buildRecord.emittedCandidate.file);
const allCaptures = [
  {
    id: 'desktop-rail-line-windup',
    replay: 'rail-line-focus',
    time: 0.55,
    width: 1440,
    height: 900,
    reviewOperation: 'arc-signature-0',
  },
  {
    id: 'desktop-normal-match',
    replay: 'railback-current-tapline3',
    time: 5,
    width: 1440,
    height: 900,
  },
  {
    id: 'desktop-teal-longshot',
    replay: 'railback-current-tapline3',
    time: 5,
    width: 1440,
    height: 900,
    unitIndex: 1,
    teamId: 1,
    team: 'teal',
  },
  {
    id: 'desktop-orange-longshot',
    replay: 'railback-current-tapline3',
    time: 5,
    width: 1440,
    height: 900,
    unitIndex: 0,
    teamId: 0,
    team: 'orange',
  },
  {
    id: 'phone-rail-line-windup',
    replay: 'rail-line-focus',
    time: 0.55,
    width: 844,
    height: 390,
    mobile: true,
    reviewOperation: 'arc-signature-0',
  },
  {
    id: 'phone-normal-match',
    replay: 'railback-current-tapline3',
    time: 5,
    width: 844,
    height: 390,
    mobile: true,
  },
  {
    id: 'phone-teal-longshot',
    replay: 'railback-current-tapline3',
    time: 5,
    width: 844,
    height: 390,
    mobile: true,
    unitIndex: 1,
    teamId: 1,
    team: 'teal',
  },
  {
    id: 'phone-orange-longshot',
    replay: 'railback-current-tapline3',
    time: 5,
    width: 844,
    height: 390,
    mobile: true,
    unitIndex: 0,
    teamId: 0,
    team: 'orange',
  },
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
  throw new Error('REVIEW_CAPTURE_IDS did not match a configured Railback capture.');

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
  console.log(`Captured ${records.length} targeted Railback review frame(s).`);
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
      'Current-build real WebGL proof of Railback Longshot at normal scale, selected scale, on teal and orange teams, and during Rail Line windup',
    sourceUrl: baseUrl,
    runtimeAssetsPromoted: buildRecord.runtimeAssetsPromoted,
    buildRecord:
      'art/reviews/arc-relay-longshot-railback-v1/gameplay-review/build-record.json',
    expectedCandidateModel: {
      filename: candidateFilename,
      sha256: buildRecord.emittedCandidate.sha256,
    },
    captures: records,
    contactSheets: sheets,
  };
  writeFileSync(path.join(output, 'evidence.json'), `${JSON.stringify(evidence, null, 2)}\n`);
  console.log(`Captured ${records.length} Railback gameplay review frames.`);
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
  await closeLongshotSelection(page);
  await seekAndSettle(page, capture.time);
  let selectedUnitLabel = null;
  if (Number.isInteger(capture.unitIndex)) {
    // Immersive phone layout intentionally hides the roster, but the mounted controls
    // retain their React handlers. Dispatching their click keeps capture on the exact
    // fullscreen path a player sees without introducing a review-only game hook.
    const inspect = page.locator('button[aria-label^="Inspect Longshot"]');
    if ((await inspect.count()) !== 4)
      throw new Error(`${capture.id}: expected four Longshots in two mirrored Hunter rosters.`);
    selectedUnitLabel = await inspect.nth(capture.unitIndex).getAttribute('aria-label');
    await inspect.nth(capture.unitIndex).evaluate((button) => button.click());
  }
  if (capture.reviewOperation) {
    const framed = await page.evaluate(
      (operationId) => window.__BOTARENA_RENDER_PROOF__?.frameSignature(operationId),
      capture.reviewOperation,
    );
    if (!framed)
      throw new Error(`${capture.id}: could not frame ${capture.reviewOperation}.`);
  }
  await page.waitForTimeout(1600);
  const arena = page.getByRole('region', { name: 'Arena' });
  const canvas = arena.locator('canvas');
  const renderer = await canvas.evaluate((node) => (node.getContext('2d') ? '2d' : 'webgl'));
  const proof = await page.evaluate(() => window.__BOTARENA_RENDER_PROOF__?.sample());
  const imagePath = path.join(output, `${capture.id}.png`);
  await canvas.screenshot({ path: imagePath });
  const image = readFileSync(imagePath);
  const loadedCandidate = session.modelResponses.some(
    (entry) => entry.status === 200 && entry.filename === candidateFilename,
  );
  if (renderer !== 'webgl') throw new Error(`${capture.id}: expected WebGL, received ${renderer}.`);
  if (session.errors.length) throw new Error(`${capture.id}: ${session.errors.join('\n')}`);
  if (!loadedCandidate) throw new Error(`${capture.id}: Railback candidate did not load.`);
  if (
    capture.reviewOperation &&
    !proof?.visibleSignatures?.some(
      (signature) =>
        signature.operationId === capture.reviewOperation &&
        signature.signatureId === 'rail-line' &&
        signature.visibleMeshes > 0,
    )
  )
    throw new Error(`${capture.id}: Rail Line windup is not visibly rendered.`);
  return {
    ...capture,
    mobile: capture.mobile ?? false,
    renderer,
    proofTime: proof?.time ?? null,
    replaySeed: proof?.replaySeed ?? null,
    tickCount: proof?.tickCount ?? null,
    cameraFrame: proof?.cameraFrame ?? null,
    visibleSignatures: proof?.visibleSignatures ?? [],
    candidateFilename,
    candidateLoaded: loadedCandidate,
    selectedUnitLabel,
    image: path.relative(repository, imagePath),
    imageBytes: image.length,
    imageSha256: sha256(image),
  };
}

async function closeLongshotSelection(page) {
  const close = page.locator('button[aria-label^="Close Longshot"]');
  if (await close.count()) await close.first().evaluate((button) => button.click());
}

async function selectReplay(page, id) {
  const choices = JSON.parse(readFileSync(path.join(reviewRoot, 'replays.json'), 'utf8'));
  const choiceIndex = choices.findIndex((entry) => entry.id === id);
  if (choiceIndex < 0) throw new Error(`Unknown review replay ${id}.`);
  const choice = choices[choiceIndex];
  const button = page
    .getByRole('navigation', { name: 'Choose a replay to review' })
    .getByRole('button')
    .nth(choiceIndex);
  if ((await button.getAttribute('aria-current')) !== 'true')
    await button.evaluate((node) => node.click());
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
