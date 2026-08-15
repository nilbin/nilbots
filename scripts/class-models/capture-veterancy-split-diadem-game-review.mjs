#!/usr/bin/env node

/** Capture the split-diadem at real gameplay scale without reloading its model per frame. */

import { createHash } from 'node:crypto';
import { createRequire } from 'node:module';
import { mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const repository = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..', '..');
const requireFromWeb = createRequire(path.join(repository, 'web', 'package.json'));
const { chromium } = requireFromWeb('playwright');
const sharp = requireFromWeb('sharp');
const url =
  process.env.REVIEW_URL ??
  'http://127.0.0.1:9201/sandbox/arc-relay-veterancy-split-diadem-v1/site/?standalone&audio=off';
const output = path.resolve(
  repository,
  process.env.REVIEW_OUTPUT ??
    'art/reviews/arc-relay-veterancy-modules-meshy7-v1/runtime-game-review',
);
const shotCatalog = [
  { id: 'overview-tick-08', tick: 8, camera: 'overview', beat: 'Relay V carries the Core' },
  { id: 'director-tick-08', tick: 8, camera: 'director', beat: 'Relay V carries the Core' },
  {
    id: 'director-tick-10',
    tick: 10,
    camera: 'director',
    beat: 'Relay V hands the Core to Kestrel I',
  },
  {
    id: 'director-tick-34',
    tick: 34,
    camera: 'director',
    beat: 'Nest IV hands the Core to Palisade V',
  },
  {
    id: 'director-tick-50',
    tick: 50,
    camera: 'director',
    beat: 'Relay V launches the four-tile Arc Toss',
  },
];
const profileCatalog = [
  { id: 'desktop', viewport: { width: 1440, height: 900 }, mobile: false },
  { id: 'phone-landscape', viewport: { width: 844, height: 390 }, mobile: true },
];
const requestedTicks = new Set(
  (process.env.REVIEW_TICKS ?? '')
    .split(',')
    .map((value) => value.trim())
    .filter(Boolean)
    .map(Number)
    .filter(Number.isFinite),
);
const requestedProfiles = new Set(
  (process.env.REVIEW_PROFILES ?? '').split(',').map((value) => value.trim()).filter(Boolean),
);
const shots = requestedTicks.size
  ? shotCatalog.filter((shot) => requestedTicks.has(shot.tick))
  : shotCatalog;
const profiles = requestedProfiles.size
  ? profileCatalog.filter((profile) => requestedProfiles.has(profile.id))
  : profileCatalog;
if (!shots.length || !profiles.length) throw new Error('Capture selection matched no shots or profiles.');

mkdirSync(output, { recursive: true });
const browser = await chromium.launch({ headless: true });
const evidence = [];
try {
  for (const profile of profiles) {
    console.log(`Loading ${profile.id} once for ${shots.length} pinned frames...`);
    const context = await browser.newContext({
      viewport: profile.viewport,
      deviceScaleFactor: 1,
      isMobile: profile.mobile,
      hasTouch: profile.mobile,
    });
    const page = await context.newPage();
    const errors = monitorErrors(page);
    const replayResponses = await preparePage(page, profile);
    for (const shot of shots) {
      console.log(
        `Capturing ${profile.id} ${shot.camera} tick ${shot.tick} without a reload...`,
      );
      evidence.push(await captureBeat(page, profile, shot, errors, replayResponses));
    }
    await context.close();
  }
} finally {
  await browser.close();
}

const sheets = [];
for (const profile of profiles) sheets.push(await contactSheet(profile));
const record = {
  schemaVersion: 1,
  purpose:
    'Gameplay-scale review of rank I-V split diadem during real Core custody, handoff, and Arc Toss motion',
  sourceUrl: url,
  sourceReplay:
    'sandbox/arc-relay-veterancy-split-diadem-v1/veterancy-ranked-core-stage.broadcast.json.gz',
  modelLoadPolicy: 'one page and one model load per render profile; all ticks sought in place',
  captures: evidence,
  contactSheets: sheets,
};
writeFileSync(path.join(output, 'evidence.json'), `${JSON.stringify(record, null, 2)}\n`);
console.log(`Captured ${evidence.length} pinned frames and ${sheets.length} contact sheets.`);

function monitorErrors(page) {
  const pageErrors = [];
  const consoleErrors = [];
  const failedRequests = [];
  page.on('pageerror', (error) => pageErrors.push(error.message));
  page.on('console', (message) => {
    if (message.type() === 'error') consoleErrors.push(message.text());
  });
  page.on('requestfailed', (request) => {
    failedRequests.push(`${request.method()} ${request.url()}: ${request.failure()?.errorText ?? 'failed'}`);
  });
  return { pageErrors, consoleErrors, failedRequests };
}

async function preparePage(page, profile) {
  const replayResponses = [];
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
  const separator = url.includes('?') ? '&' : '?';
  await page.goto(
    `${url}${separator}render-profile=${profile.mobile ? 'mobile' : 'desktop'}&veterancyReview=${profile.id}`,
    { waitUntil: 'domcontentloaded' },
  );
  await page.locator('[data-play-overlay="ready"]').waitFor({ timeout: 120_000 });
  await page.getByRole('button', { name: 'Play match' }).click();
  const pause = page.getByRole('button', { name: 'Pause', exact: true });
  await pause.waitFor({ state: 'visible', timeout: 30_000 });
  // Automatic phone immersion can remount the transport as the overlay click lands.
  // Blur the old button and pause through the viewer's global transport shortcut so the
  // action targets playback state rather than whichever transport instance survived.
  await page.evaluate(() => (document.activeElement instanceof HTMLElement
    ? document.activeElement.blur()
    : undefined));
  await page.keyboard.press(' ');
  await page.keyboard.press('Tab');
  await page.getByRole('button', { name: 'Play', exact: true }).waitFor({
    state: 'visible',
    timeout: 30_000,
  });

  const fullscreen = page.getByRole('button', { name: 'full screen' });
  if (await fullscreen.isVisible()) await fullscreen.click();
  await page.waitForTimeout(1_200);
  return replayResponses;
}

async function captureBeat(page, profile, shot, errors, replayResponses) {
  // Tab explicitly wakes immersive chrome. Mouse movement cannot do that on the coarse
  // pointer path used by phone-landscape review profiles.
  await page.keyboard.press('Tab');
  await page.mouse.move(profile.viewport.width / 2, profile.viewport.height - 10);
  const camera = page.getByRole('button').filter({ hasText: /director|overview/i }).first();
  const desiredAutoFit = shot.camera === 'director';
  const currentAutoFit = (await camera.getAttribute('aria-pressed')) === 'true';
  if (currentAutoFit !== desiredAutoFit) {
    await camera.click();
    await page.waitForFunction(
      (expected) => {
        const candidate = [...document.querySelectorAll('button')].find((button) =>
          /director|overview/i.test(button.textContent ?? ''),
        );
        return candidate?.getAttribute('aria-pressed') === String(expected);
      },
      desiredAutoFit,
    );
    await page.waitForTimeout(1_200);
  }
  const timeline = page.locator('[aria-label^="Match timeline"]');
  const thumb = timeline.locator('[role="slider"]');
  await timeline.waitFor({ state: 'visible', timeout: 30_000 });
  const maximum = Number(await thumb.getAttribute('aria-valuemax'));
  const bounds = await timeline.boundingBox();
  if (!bounds || !Number.isFinite(maximum)) throw new Error('Could not measure replay timeline.');
  await page.mouse.click(
    bounds.x + bounds.width * (shot.tick / maximum),
    bounds.y + bounds.height / 2,
  );
  await thumb.focus();
  let actual = Math.round(Number(await thumb.getAttribute('aria-valuenow')));
  for (let step = 0; actual !== shot.tick && step <= maximum + 1; step += 1) {
    if (!Number.isFinite(actual)) throw new Error('Replay playhead did not expose a numeric tick.');
    await page.keyboard.press(actual < shot.tick ? 'ArrowRight' : 'ArrowLeft');
    actual = Math.round(Number(await thumb.getAttribute('aria-valuenow')));
  }
  if (actual !== shot.tick)
    throw new Error(`Could not seek replay from tick ${actual} to ${shot.tick}.`);
  await page.waitForTimeout(900);

  const arena = page.getByRole('region', { name: 'Arena' });
  const canvas = arena.locator('canvas');
  const renderer = await canvas.evaluate((node) => (node.getContext('2d') ? '2d' : 'webgl'));
  const canvasPixels = await canvas.evaluate((node) => ({
    width: node.width,
    height: node.height,
    clientWidth: node.clientWidth,
    clientHeight: node.clientHeight,
  }));
  const id = `${profile.id}-${shot.id}`;
  const imagePath = path.join(output, `${id}.png`);
  await arena.screenshot({ path: imagePath });
  const image = readFileSync(imagePath);
  if (renderer !== 'webgl') throw new Error(`${id} mounted ${renderer}, expected WebGL.`);
  const allErrors = [
    ...errors.pageErrors,
    ...errors.consoleErrors,
    ...errors.failedRequests,
  ];
  if (allErrors.length) throw new Error(`${id} produced browser errors:\n${allErrors.join('\n')}`);
  return {
    id,
    profile: profile.id,
    viewport: profile.viewport,
    tick: shot.tick,
    beat: shot.beat,
    camera: shot.camera,
    renderer,
    canvasPixels,
    image: path.relative(repository, imagePath),
    imageBytes: image.length,
    imageSha256: sha256(image),
    replayResponses,
    pageErrors: [],
    consoleErrors: [],
    failedRequests: [],
  };
}

async function contactSheet(profile) {
  const captures = evidence.filter((capture) => capture.profile === profile.id);
  const width = 720;
  const labelHeight = 48;
  const rows = [];
  let top = 0;
  for (const capture of captures) {
    const source = readFileSync(path.join(repository, capture.image));
    const resized = await sharp(source).resize({ width }).png().toBuffer();
    const metadata = await sharp(resized).metadata();
    const label = await sharp(Buffer.from(labelSvg(width, labelHeight, capture))).png().toBuffer();
    rows.push({ input: label, left: 0, top });
    top += labelHeight;
    rows.push({ input: resized, left: 0, top });
    top += metadata.height ?? 0;
  }
  const sheetPath = path.join(output, `${profile.id}-gameplay-scale.webp`);
  await sharp({
    create: { width, height: top, channels: 4, background: '#070b10' },
  })
    .composite(rows)
    .webp({ quality: 90 })
    .toFile(sheetPath);
  const bytes = readFileSync(sheetPath);
  return {
    profile: profile.id,
    image: path.relative(repository, sheetPath),
    imageBytes: bytes.length,
    imageSha256: sha256(bytes),
  };
}

function labelSvg(width, height, capture) {
  const text = escapeXml(
    `${capture.camera.toUpperCase()} · TICK ${capture.tick}  ·  ${capture.beat}`,
  );
  return `<svg xmlns="http://www.w3.org/2000/svg" width="${width}" height="${height}">
    <rect width="100%" height="100%" fill="#111923"/>
    <text x="18" y="31" fill="#f0d99a" font-family="Arial, sans-serif" font-size="17" font-weight="700">${text}</text>
  </svg>`;
}

function escapeXml(value) {
  return value.replaceAll('&', '&amp;').replaceAll('<', '&lt;').replaceAll('>', '&gt;');
}

function sha256(value) {
  return createHash('sha256').update(value).digest('hex');
}
