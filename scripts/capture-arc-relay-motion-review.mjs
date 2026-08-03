#!/usr/bin/env node

import { createHash } from 'node:crypto';
import { createRequire } from 'node:module';
import {
  mkdirSync,
  readFileSync,
  writeFileSync,
} from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const repository = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const requireFromWeb = createRequire(path.join(repository, 'web', 'package.json'));
const { chromium } = requireFromWeb('playwright');
const sharp = requireFromWeb('sharp');

const url = process.env.REVIEW_URL ??
  'http://127.0.0.1:4175/?standalone&audio=off';
const seconds = Number(process.env.MOTION_SECONDS ?? 8);
const output = path.resolve(
  repository,
  process.env.MOTION_OUTPUT ?? 'art/reviews/arc-relay-3d/motion',
);
const reviews = [
  {
    id: 'stock-a-v-flow-b-desktop',
    choice: 'Stock · roster A · amber',
    viewport: { width: 1440, height: 900 },
  },
  {
    id: 'stock-b-v-flow-a-desktop',
    choice: 'Stock · roster B · amber',
    viewport: { width: 1440, height: 900 },
  },
  {
    id: 'stock-a-carrier-desktop',
    choice: 'Stock · roster A · amber',
    viewport: { width: 1440, height: 900 },
    seek: 26,
  },
  {
    id: 'stock-a-carrier-canvas-desktop',
    choice: 'Stock · roster A · amber',
    viewport: { width: 1440, height: 900 },
    seek: 26,
    renderer: '2d',
  },
  {
    id: 'stock-a-v-flow-b-phone',
    choice: 'Stock · roster A · amber',
    viewport: { width: 844, height: 390 },
    mobile: true,
  },
];

mkdirSync(output, { recursive: true });
const browser = await chromium.launch({ headless: true });
const evidence = [];
try {
  for (const review of reviews) evidence.push(await capture(review));
} finally {
  await browser.close();
}
writeFileSync(
  path.join(output, 'evidence.json'),
  `${JSON.stringify({ schemaVersion: 1, url, seconds, captures: evidence }, null, 2)}\n`,
);
console.log(`Captured ${evidence.length} real-replay motion reviews in ${path.relative(repository, output)}.`);

async function capture(review) {
  const context = await browser.newContext({
    viewport: review.viewport,
    deviceScaleFactor: 1,
    isMobile: review.mobile ?? false,
    hasTouch: review.mobile ?? false,
    acceptDownloads: true,
  });
  if (review.renderer === '2d') {
    await context.addInitScript(() => {
      const nativeGetContext = HTMLCanvasElement.prototype.getContext;
      HTMLCanvasElement.prototype.getContext = function getContext(type, ...args) {
        if (type === 'webgl' || type === 'webgl2' || type === 'experimental-webgl')
          return null;
        return nativeGetContext.call(this, type, ...args);
      };
    });
  }
  const page = await context.newPage();
  const pageErrors = [];
  const consoleErrors = [];
  page.on('pageerror', (error) => pageErrors.push(error.message));
  page.on('console', (message) => {
    if (message.type() !== 'error') return;
    const value = message.text();
    if (
      review.renderer === '2d' &&
      (value.includes('Error creating WebGL context') ||
        value.includes('A WebGL context could not be created'))
    )
      return;
    consoleErrors.push(value);
  });

  await page.goto(`${url}&motion=${review.id}`, { waitUntil: 'domcontentloaded' });
  const choice = page.getByRole('button', { name: new RegExp(escape(review.choice), 'i') });
  await choice.waitFor({ timeout: 30_000 });
  if ((await choice.getAttribute('aria-current')) !== 'true') await choice.click();
  await page.locator('[data-play-overlay="ready"]').waitFor({ timeout: 45_000 });

  const camera = page.getByRole('button', { name: /director|overview/i });
  if ((await camera.textContent())?.toLowerCase().includes('overview')) await camera.click();
  await page.getByRole('button', { name: /director/i }).waitFor();
  const halfSpeed = page.getByRole('button', { name: '0.5×', exact: true });
  await halfSpeed.click();
  if ((await halfSpeed.getAttribute('aria-pressed')) !== 'true')
    throw new Error(`${review.id} did not enter the default half-speed review cadence.`);
  if (review.seek !== undefined) {
    const timeline = page.locator('[aria-label^="Match timeline"]');
    const thumb = timeline.locator('[role="slider"]');
    const maximum = Number(await thumb.getAttribute('aria-valuemax'));
    const bounds = await timeline.boundingBox();
    if (!bounds || !Number.isFinite(maximum))
      throw new Error(`${review.id} could not measure its replay timeline.`);
    await page.mouse.click(
      bounds.x + bounds.width * (review.seek / maximum),
      bounds.y + bounds.height / 2,
    );
    await page.waitForFunction(
      (expected) =>
        [...document.querySelectorAll('section[aria-label="Arena"] p')].some((node) =>
          new RegExp(`^\\s*0*${expected}\\s*\\/`).test(node.textContent ?? ''),
        ),
      review.seek,
    );
  }
  const fullscreen = page.getByRole('button', { name: 'full screen' });
  if (await fullscreen.isVisible()) await fullscreen.click();
  const arena = page.getByRole('region', { name: 'Arena' });
  const canvas = arena.locator('canvas');
  await canvas.waitFor();
  const kind = await canvas.evaluate((node) =>
    node.getContext('2d') ? '2d' : 'webgl',
  );
  const expectedKind = review.renderer ?? 'webgl';
  if (kind !== expectedKind)
    throw new Error(`${review.id} mounted ${kind}, expected ${expectedKind}.`);
  if (review.seek !== undefined) await page.waitForTimeout(1_200);
  const canvasPixels = await canvas.evaluate((node) => ({
    width: node.width,
    height: node.height,
    clientWidth: node.clientWidth,
    clientHeight: node.clientHeight,
  }));
  const stillPath = path.join(output, `${review.id}-opening.webp`);
  await arena.screenshot({ path: stillPath, type: 'webp', quality: 88 });

  const tickBefore = await readTick(page);
  const downloadPromise = page.waitForEvent('download', { timeout: (seconds + 20) * 1_000 });
  await canvas.evaluate((node, duration) => {
    const stream = node.captureStream(30);
    const recorder = new MediaRecorder(stream, {
      mimeType: 'video/webm;codecs=vp8',
      videoBitsPerSecond: node.width > 800 ? 4_000_000 : 2_000_000,
    });
    const chunks = [];
    recorder.addEventListener('dataavailable', (event) => {
      if (event.data.size > 0) chunks.push(event.data);
    });
    recorder.addEventListener('stop', () => {
      for (const track of stream.getTracks()) track.stop();
      const href = URL.createObjectURL(new Blob(chunks, { type: recorder.mimeType }));
      const link = document.createElement('a');
      link.href = href;
      link.download = 'arc-relay-motion.webm';
      link.click();
      setTimeout(() => URL.revokeObjectURL(href), 1_000);
    });
    recorder.start(250);
    setTimeout(() => recorder.stop(), duration * 1_000);
  }, seconds);
  await page.getByRole('button', { name: 'Play match' }).click();
  const download = await downloadPromise;
  const videoPath = path.join(output, `${review.id}.webm`);
  await download.saveAs(videoPath);
  const tickAfter = await readTick(page);
  const contactSheetPath = path.join(output, `${review.id}-contact.webp`);
  const video = readFileSync(videoPath);
  await writeContactSheet(page, video, review.viewport, contactSheetPath);

  const record = {
    id: review.id,
    replay: review.choice,
    viewport: review.viewport,
    mobile: review.mobile ?? false,
    renderer: kind,
    playbackSpeed: 0.5,
    requestedStartTick: review.seek ?? 0,
    canvasPixels,
    tickBefore,
    tickAfter,
    ticksAdvanced: tickAfter - tickBefore,
    video: path.relative(repository, videoPath),
    videoBytes: video.length,
    videoSha256: sha256(video),
    contactSheet: path.relative(repository, contactSheetPath),
    contactSheetSha256: sha256(readFileSync(contactSheetPath)),
    openingStill: path.relative(repository, stillPath),
    openingStillSha256: sha256(readFileSync(stillPath)),
    pageErrors,
    consoleErrors,
  };
  await context.close();
  if (pageErrors.length || consoleErrors.length)
    throw new Error(`${review.id} produced browser errors: ${[...pageErrors, ...consoleErrors].join('\n')}`);
  return record;
}

async function writeContactSheet(page, video, viewport, destination) {
  const source = `data:video/webm;base64,${video.toString('base64')}`;
  await page.setContent('<video muted playsinline></video><canvas></canvas>');
  const dimensions = await page.evaluate(async (dataUrl) => {
    const videoNode = document.querySelector('video');
    videoNode.src = dataUrl;
    await new Promise((resolve, reject) => {
      videoNode.addEventListener('loadedmetadata', resolve, { once: true });
      videoNode.addEventListener('error', reject, { once: true });
    });
    return {
      width: videoNode.videoWidth,
      height: videoNode.videoHeight,
      duration: videoNode.duration,
    };
  }, source);
  const frameWidth = Math.min(360, Math.max(180, Math.round(viewport.width / 2)));
  const frameHeight = Math.round((frameWidth * dimensions.height) / dimensions.width);
  const frameTimes = Array.from({ length: 8 }, (_, index) =>
    Math.min(dimensions.duration - 0.05, 1.5 + index * 0.125),
  );
  const frames = [];
  for (const time of frameTimes) {
    const png = await page.evaluate(async (at) => {
      const videoNode = document.querySelector('video');
      const canvasNode = document.querySelector('canvas');
      videoNode.currentTime = Math.max(0, at);
      await new Promise((resolve) =>
        videoNode.addEventListener('seeked', resolve, { once: true }),
      );
      canvasNode.width = videoNode.videoWidth;
      canvasNode.height = videoNode.videoHeight;
      canvasNode.getContext('2d').drawImage(videoNode, 0, 0);
      return canvasNode.toDataURL('image/png');
    }, time);
    frames.push(Buffer.from(png.slice(png.indexOf(',') + 1), 'base64'));
  }
  const composites = await Promise.all(
    frames.map(async (input, index) => ({
      input: await sharp(input).resize(frameWidth, frameHeight).png().toBuffer(),
      left: (index % 4) * frameWidth,
      top: Math.floor(index / 4) * frameHeight,
    })),
  );
  await sharp({
    create: {
      width: frameWidth * 4,
      height: frameHeight * 2,
      channels: 4,
      background: '#070b10',
    },
  })
    .composite(composites)
    .webp({ quality: 88 })
    .toFile(destination);
}

async function readTick(page) {
  const text = await page
    .getByRole('region', { name: 'Arena' })
    .locator('p')
    .filter({ hasText: /^\s*\d{3}\s*\/\s*\d{3}\s*$/ })
    .first()
    .textContent();
  const tick = text?.match(/\d+/)?.[0];
  if (tick === undefined) throw new Error('Could not read replay tick.');
  return Number(tick);
}

function sha256(value) {
  return createHash('sha256').update(value).digest('hex');
}

function escape(value) {
  return value.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
}
