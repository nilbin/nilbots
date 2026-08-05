#!/usr/bin/env node

import { createHash } from 'node:crypto';
import { createRequire } from 'node:module';
import {
  copyFileSync,
  mkdirSync,
  readFileSync,
  rmSync,
  writeFileSync,
} from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const repository = path.resolve(
  path.dirname(fileURLToPath(import.meta.url)),
  '..',
  '..',
);
const requireFromWeb = createRequire(path.join(repository, 'web', 'package.json'));
const { chromium } = requireFromWeb('playwright');
const sharp = requireFromWeb('sharp');
const url =
  process.env.REVIEW_URL ??
  'http://127.0.0.1:4174/?standalone&audio=off';
const output = path.resolve(
  repository,
  process.env.REVIEW_OUTPUT ??
    'art/class-models/provider-runs/meshy/arc-fleet-v4-pilot-review/stills',
);
const servedOutput = path.resolve(
  repository,
  process.env.REVIEW_SERVED_OUTPUT ?? 'web/dist-review/pilot-v4-stills',
);

// Tick 18 is the first clean frame where both spawned hardware pieces are active.
// Minesmith is at 7,13 beside its Trip Node at 8,12; Nest is at 17,13 beside its
// Sentinel at 16,12. Capturing the carrier and payload together is the actual review
// question: whether the silhouettes remain distinct after the game camera and lighting.
const captures = [
  {
    id: 'desktop-minesmith-trip-node-tick-18',
    title: 'Minesmith + active Trip Node',
    viewport: { width: 1440, height: 900 },
    tick: 18,
    target: { x: 8, y: 13 },
    zoom: 2.3,
    baseFrameHeight: 17,
    fullscreen: true,
  },
  {
    id: 'desktop-nest-sentinel-tick-18',
    title: 'Nest + active Sentinel',
    viewport: { width: 1440, height: 900 },
    tick: 18,
    target: { x: 17, y: 13 },
    zoom: 2.3,
    baseFrameHeight: 17,
    fullscreen: true,
  },
  {
    id: 'desktop-pilot-payloads-tick-18',
    title: 'Both pilot bodies and spawned payloads',
    viewport: { width: 1440, height: 900 },
    tick: 18,
    target: { x: 12.5, y: 13 },
    zoom: 1.65,
    baseFrameHeight: 17,
    fullscreen: true,
  },
  {
    id: 'phone-landscape-pilot-payloads-tick-18',
    title: 'Phone landscape gameplay scale',
    viewport: { width: 844, height: 390 },
    tick: 18,
    target: { x: 12.5, y: 13 },
    zoom: 1.45,
    // This aspect frames the full 31x23 map plus the renderer margin.
    baseFrameHeight: 16.2,
    mobile: true,
  },
];

mkdirSync(output, { recursive: true });
const browser = await chromium.launch({ headless: true });
const evidence = [];
try {
  for (const capture of captures) evidence.push(await takeCapture(capture));
} finally {
  await browser.close();
}

await buildContactSheet();
const record = {
  schemaVersion: 1,
  sourceUrl: url,
  replay: 'web/dist-review/replays/minesmith-a-v-nest-b.json',
  authoritativeReviewTick: 18,
  reviewFacts: {
    minesmith: { position: { x: 7, y: 13 }, facing: 'east' },
    tripNode: { position: { x: 8, y: 12 }, phase: 'active' },
    nest: { position: { x: 17, y: 13 }, facing: 'west' },
    sentinel: { position: { x: 16, y: 12 }, phase: 'active' },
  },
  captures: evidence,
  contactSheet: path.relative(repository, path.join(output, 'pilot-gameplay-review.webp')),
  contactSheetSha256: sha256(
    readFileSync(path.join(output, 'pilot-gameplay-review.webp')),
  ),
};
writeFileSync(
  path.join(output, 'evidence.json'),
  `${JSON.stringify(record, null, 2)}\n`,
);
writeGallery(record);
publishGallery();
console.log(`Captured ${evidence.length} real-replay pilot reviews in ${output}.`);

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
  const failedRequests = [];
  const glbResponses = [];
  page.on('pageerror', (error) => pageErrors.push(error.message));
  page.on('console', (message) => {
    if (message.type() === 'error') consoleErrors.push(message.text());
  });
  page.on('requestfailed', (request) =>
    failedRequests.push(
      `${request.method()} ${request.url()} ${request.failure()?.errorText ?? ''}`,
    ),
  );
  page.on('response', (response) => {
    if (response.url().endsWith('.glb'))
      glbResponses.push({ url: response.url(), status: response.status() });
  });

  await page.goto(`${url}&pilotCapture=${capture.id}`, {
    waitUntil: 'domcontentloaded',
  });
  await page.locator('[data-play-overlay="ready"]').waitFor({ timeout: 60_000 });
  await page.getByRole('button', { name: 'Play match' }).click();
  await page.locator('[data-play-overlay]').waitFor({ state: 'detached' });
  await page.getByRole('button', { name: 'Pause', exact: true }).click();

  const timeline = page.locator('[aria-label^="Match timeline"]');
  const thumb = timeline.locator('[role="slider"]');
  const maximum = Number(await thumb.getAttribute('aria-valuemax'));
  const timelineBounds = await timeline.boundingBox();
  if (!timelineBounds || !Number.isFinite(maximum))
    throw new Error(`${capture.id} could not measure the replay timeline.`);
  await page.mouse.click(
    timelineBounds.x + timelineBounds.width * (capture.tick / maximum),
    timelineBounds.y + timelineBounds.height / 2,
  );
  await page.waitForFunction(
    (expected) =>
      document
        .querySelector('[aria-label^="Match timeline"] [role="slider"]')
        ?.getAttribute('aria-valuenow') === String(expected),
    capture.tick,
  );

  // Enter the renderer-owned manual overview before applying review camera input.
  // Doing this first matters: changing the React camera mode after a wheel event would
  // intentionally replace that event with the strategic overview frame.
  const director = page.getByRole('button', { name: /director/i });
  if (await director.count()) await director.evaluate((button) => button.click());
  const fullscreen = page.getByRole('button', { name: 'full screen' });
  if (capture.fullscreen && (await fullscreen.count())) await fullscreen.click();
  await page.waitForTimeout(1_200);

  const arena = page.getByRole('region', { name: 'Arena' });
  const bounds = await arena.boundingBox();
  if (!bounds) throw new Error(`${capture.id} has no arena bounds.`);
  const scale = bounds.height / capture.baseFrameHeight;
  const dx = -(capture.target.x - 15.5) * scale;
  const dy = -(capture.target.y - 11.5) * scale;
  const centre = {
    x: bounds.x + bounds.width / 2,
    y: bounds.y + bounds.height / 2,
  };
  await page.mouse.move(centre.x - dx / 2, centre.y - dy / 2);
  await page.mouse.down();
  await page.mouse.move(centre.x + dx / 2, centre.y + dy / 2, { steps: 16 });
  await page.mouse.up();
  await page.waitForTimeout(700);
  await page.mouse.move(centre.x, centre.y);
  await page.mouse.wheel(0, -Math.log(capture.zoom) / 0.0016);
  await page.waitForTimeout(2_800);

  const canvas = arena.locator('canvas');
  const renderer = await canvas.evaluate((node) =>
    node.getContext('2d') ? '2d' : 'webgl',
  );
  const canvasPixels = await canvas.evaluate((node) => ({
    width: node.width,
    height: node.height,
    clientWidth: node.clientWidth,
    clientHeight: node.clientHeight,
  }));
  const browserScale = await page.evaluate(() => ({
    devicePixelRatio: window.devicePixelRatio,
    visualViewportScale: window.visualViewport?.scale ?? 1,
  }));
  const imagePath = path.join(output, `${capture.id}.png`);
  await arena.screenshot({ path: imagePath, animations: 'disabled' });
  const image = readFileSync(imagePath);
  await context.close();

  if (renderer !== 'webgl')
    throw new Error(`${capture.id} mounted ${renderer}, expected WebGL.`);
  if (browserScale.devicePixelRatio !== 1 || browserScale.visualViewportScale !== 1)
    throw new Error(`${capture.id} changed browser scale.`);
  if (pageErrors.length || consoleErrors.length || failedRequests.length)
    throw new Error(
      `${capture.id} emitted browser errors:\n${[
        ...pageErrors,
        ...consoleErrors,
        ...failedRequests,
      ].join('\n')}`,
    );
  const uniqueGlbs = new Set(glbResponses.map((response) => response.url));
  if (uniqueGlbs.size < 18 || glbResponses.some((response) => response.status !== 200))
    throw new Error(`${capture.id} loaded only ${uniqueGlbs.size} successful GLBs.`);

  return {
    id: capture.id,
    title: capture.title,
    viewport: capture.viewport,
    mobile: capture.mobile ?? false,
    tick: capture.tick,
    cameraInput: {
      kind: 'renderer-pan-and-wheel',
      target: capture.target,
      requestedZoomFactor: capture.zoom,
    },
    renderer,
    canvasPixels,
    browserScale,
    successfulGlbs: uniqueGlbs.size,
    image: path.relative(repository, imagePath),
    imageBytes: image.length,
    imageSha256: sha256(image),
    pageErrors,
    consoleErrors,
    failedRequests,
  };
}

async function buildContactSheet() {
  const desktopWidth = 720;
  const labelHeight = 42;
  const gap = 12;
  const prepared = [];
  for (const capture of captures) {
    const imagePath = path.join(output, `${capture.id}.png`);
    const image = sharp(imagePath);
    const metadata = await image.metadata();
    const width = capture.mobile ? desktopWidth : desktopWidth;
    const height = Math.round(width * (metadata.height ?? 1) / (metadata.width ?? 1));
    const resized = await image.resize(width, height).png().toBuffer();
    const label = Buffer.from(
      `<svg width="${width}" height="${labelHeight}" xmlns="http://www.w3.org/2000/svg">` +
        '<rect width="100%" height="100%" fill="#080d12"/>' +
        `<text x="12" y="27" fill="#dbe7ef" font-size="17" font-family="monospace">${escapeXml(capture.title)} — tick ${capture.tick}</text>` +
        '</svg>',
    );
    prepared.push({ resized, label, width, height });
  }
  const width = desktopWidth;
  const height = prepared.reduce(
    (sum, item) => sum + labelHeight + item.height + gap,
    0,
  ) - gap;
  const composites = [];
  let top = 0;
  for (const item of prepared) {
    composites.push({ input: item.label, left: 0, top });
    top += labelHeight;
    composites.push({ input: item.resized, left: 0, top });
    top += item.height + gap;
  }
  await sharp({
    create: { width, height, channels: 4, background: '#080d12' },
  })
    .composite(composites)
    .webp({ quality: 90 })
    .toFile(path.join(output, 'pilot-gameplay-review.webp'));
}

function writeGallery(record) {
  const cards = record.captures
    .map(
      (capture) => `
      <article>
        <h2>${escapeXml(capture.title)}</h2>
        <p>Authoritative tick ${capture.tick} · ${capture.renderer} · ${capture.cameraInput.kind} · ${capture.viewport.width}×${capture.viewport.height}</p>
        <a href="${capture.id}.png"><img src="${capture.id}.png" alt="${escapeXml(capture.title)}"></a>
      </article>`,
    )
    .join('');
  writeFileSync(
    path.join(output, 'index.html'),
    `<!doctype html>
<html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>Arc Relay v4 two-model pilot</title>
<style>
body{margin:0;background:#070b10;color:#dbe7ef;font:15px ui-monospace,SFMono-Regular,Menlo,monospace}main{max-width:1480px;margin:auto;padding:24px}header{margin-bottom:24px}h1{font-size:24px}h2{font-size:17px;margin:0 0 4px}p{color:#8ea1ad;line-height:1.5}a{color:#66d9ef}article{margin:0 0 28px;padding:14px;background:#0d141b;border:1px solid #273641;border-radius:4px}img{display:block;width:100%;height:auto;margin-top:12px;background:#000}.links{display:flex;gap:18px;flex-wrap:wrap}
</style></head><body><main><header><h1>Arc Relay v4 — Minesmith / Nest pilot</h1>
<p>Real replay, WebGL renderer, renderer-owned pan and zoom, browser scale 100%. Tick 18 shows both spawned payloads active.</p>
<p class="links"><a href="/">Open the live replay gallery</a><a href="pilot-gameplay-review.webp">Open one contact sheet</a></p></header>${cards}</main></body></html>`,
  );
}

function publishGallery() {
  rmSync(servedOutput, { recursive: true, force: true });
  mkdirSync(servedOutput, { recursive: true });
  for (const filename of [
    ...captures.map((capture) => `${capture.id}.png`),
    'pilot-gameplay-review.webp',
    'evidence.json',
    'index.html',
  ])
    copyFileSync(path.join(output, filename), path.join(servedOutput, filename));
}

function sha256(value) {
  return createHash('sha256').update(value).digest('hex');
}

function escapeXml(value) {
  return value
    .replaceAll('&', '&amp;')
    .replaceAll('<', '&lt;')
    .replaceAll('>', '&gt;');
}
