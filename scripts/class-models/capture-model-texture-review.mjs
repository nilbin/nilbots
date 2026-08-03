#!/usr/bin/env node

import { createHash } from 'node:crypto';
import { createRequire } from 'node:module';
import {
  copyFileSync,
  mkdirSync,
  readFileSync,
  writeFileSync,
} from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const repository = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..', '..');
const requireFromWeb = createRequire(path.join(repository, 'web', 'package.json'));
const { chromium, webkit } = requireFromWeb('playwright');
const sharp = requireFromWeb('sharp');

const options = parseOptions(process.argv.slice(2));
const builtModelPath = absolute(required('built-model'));
const replayPath = absolute(required('replay'));
const lookId = required('look');
const outputDirectory = absolute(required('output-directory'));
const reviewUrl = options.url?.[0] ?? 'http://127.0.0.1:4173/?standalone&audio=off';
const browserName = options.browser?.[0] ?? 'chromium';
const browserType = { chromium, webkit }[browserName];
if (!browserType) throw new Error('--browser must be chromium or webkit.');
const inspectionZoom = Number(options['inspection-zoom']?.[0] ?? 0);
const variants = (options.variant ?? []).map(parseVariant);
if (variants.length < 2) throw new Error('Provide at least two --variant label=path values.');
if (inspectionZoom !== 0 && (!Number.isFinite(inspectionZoom) || inspectionZoom <= 1))
  throw new Error('--inspection-zoom must be greater than one.');

const replay = JSON.parse(readFileSync(replayPath, 'utf8'));
const look = replay.header?.presentation?.forms?.find((form) => form.lookId === lookId);
const life = replay.initialFrame?.state?.activeLives?.find(
  (candidate) => candidate.formId === look?.formId,
);
if (
  replay.header?.replayVersion !== 3 ||
  replay.header?.contract?.rules?.gameMode?.kind !== 'arc-relay' ||
  !look ||
  !life
)
  throw new Error(`Replay must be a complete Arc Relay v3 replay containing ${lookId}.`);

const viewports = [
  { id: 'desktop', width: 1440, height: 900, crop: 320 },
  { id: 'phone', width: 844, height: 390, crop: 150 },
];
const inspectionViewports = viewports.filter((viewport) => viewport.id === 'desktop');
const originalBuiltModel = readFileSync(builtModelPath);
const records = [];
mkdirSync(outputDirectory, { recursive: true });

try {
  for (const variant of variants) {
    copyFileSync(variant.file, builtModelPath);
    const browser = await browserType.launch({ headless: true });
    try {
      for (const viewport of viewports)
        records.push(await capture(browser, variant, viewport));
    } finally {
      await browser.close();
    }
  }
} finally {
  writeFileSync(builtModelPath, originalBuiltModel);
}

const variantDifferences = {};
for (const viewport of viewports)
  variantDifferences[viewport.id] = await cropVariantFootprint(viewport);
for (const viewport of viewports) await contactSheet(viewport);

const inspectionRecords = [];
const inspectionDifferences = {};
if (inspectionZoom > 1) {
  try {
    for (const variant of variants) {
      copyFileSync(variant.file, builtModelPath);
      const browser = await browserType.launch({ headless: true });
      try {
        for (const viewport of inspectionViewports)
          inspectionRecords.push(
            await capture(browser, variant, viewport, {
              mode: 'inspection',
              focusPixel: variantDifferences[viewport.id].focusPixel,
              zoom: inspectionZoom,
            }),
          );
      } finally {
        await browser.close();
      }
    }
  } finally {
    writeFileSync(builtModelPath, originalBuiltModel);
  }
  for (const viewport of inspectionViewports)
    inspectionDifferences[viewport.id] = await cropVariantFootprint(
      viewport,
      'inspection',
      inspectionRecords,
    );
  for (const viewport of inspectionViewports) {
    await contactSheet(viewport, 'inspection');
    await arenaContactSheet(viewport, 'inspection');
  }
}
writeFileSync(
  path.join(outputDirectory, 'evidence.json'),
  `${JSON.stringify({
    schemaVersion: 1,
    replay: path.relative(repository, replayPath),
    replayHash: replay.replayHash,
    browser: browserName,
    lookId,
    formId: look.formId,
    authoritativePosition: life.position,
    builtModel: path.relative(repository, builtModelPath),
    variantDifferences,
    ...(inspectionZoom > 1 ? { inspectionZoom, inspectionDifferences } : {}),
    variants: variants.map((variant) => ({
      label: variant.label,
      file: path.relative(repository, variant.file),
      bytes: readFileSync(variant.file).length,
      sha256: sha256(readFileSync(variant.file)),
    })),
    captures: records,
    ...(inspectionZoom > 1 ? { inspectionCaptures: inspectionRecords } : {}),
  }, null, 2)}\n`,
);
console.log(
  `Captured ${records.length} real-scale and ${inspectionRecords.length} inspection frames.`,
);

async function capture(browser, variant, viewport, captureOptions = {}) {
  const context = await browser.newContext({
    viewport: { width: viewport.width, height: viewport.height },
    deviceScaleFactor: 1,
  });
  const page = await context.newPage();
  const pageErrors = [];
  const consoleErrors = [];
  const failedRequests = [];
  const decoderResponses = [];
  const modelResponses = [];
  const glbResponses = [];
  page.on('pageerror', (error) => pageErrors.push(error.message));
  page.on('console', (message) => {
    if (message.type() === 'error') consoleErrors.push(message.text());
  });
  page.on('requestfailed', (request) =>
    failedRequests.push(`${request.method()} ${request.url()} ${request.failure()?.errorText ?? ''}`),
  );
  page.on('response', (response) => {
    const url = response.url();
    if (url.includes('basis_transcoder')) decoderResponses.push({ url, status: response.status() });
    if (url.endsWith('.glb')) glbResponses.push({ url, status: response.status() });
    if (url.endsWith(path.basename(builtModelPath)))
      modelResponses.push({ url, status: response.status() });
  });

  const started = Date.now();
  const separator = reviewUrl.includes('?') ? '&' : '?';
  await page.goto(
    `${reviewUrl}${separator}texture-tier=${encodeURIComponent(variant.label)}-${viewport.id}`,
    { waitUntil: 'domcontentloaded' },
  );
  await page.locator('[data-play-overlay="ready"]').waitFor({ timeout: 60_000 });
  const readyMilliseconds = Date.now() - started;

  // Remove the cold-start scrim, but keep every texture comparison on the same
  // authoritative opening frame. Merely waiting for the asset gate is not enough:
  // PlayOverlay intentionally dims and blurs the arena until its one required gesture.
  await page.getByRole('button', { name: 'Play match' }).click();
  await page.locator('[data-play-overlay]').waitFor({ state: 'detached' });
  await page.getByRole('button', { name: 'Pause' }).evaluate((button) => button.click());
  await page.getByRole('button', { name: 'Restart' }).evaluate((button) => button.click());
  await page.waitForFunction(() =>
    [...document.querySelectorAll('section[aria-label="Arena"] p')].some((node) =>
      /^\s*000\s*\/\s*\d{3}\s*$/.test(node.textContent ?? ''),
    ),
  );
  await page.getByRole('button', { name: 'Pause' }).evaluate((button) => button.click());
  for (let attempts = 0; attempts < 3 && (await readTick(page)) > 0; attempts += 1)
    await page
      .getByRole('button', { name: 'Step back one tick' })
      .evaluate((button) => button.click());
  if ((await readTick(page)) !== 0)
    throw new Error(`${variant.label}/${viewport.id} escaped tick 000 during evidence setup.`);

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
  if ((await cameraToggle.textContent())?.toLowerCase().includes('overview'))
    await cameraToggle.click();
  await page.getByRole('button', { name: /director/i }).waitFor();
  await page.waitForTimeout(3_000);

  if (captureOptions.mode === 'inspection') {
    const arenaBounds = await arena.boundingBox();
    if (!arenaBounds) throw new Error(`${variant.label}/${viewport.id} has no arena bounds.`);
    const from = {
      x: arenaBounds.x + captureOptions.focusPixel.x,
      y: arenaBounds.y + captureOptions.focusPixel.y,
    };
    const centre = {
      // Perspective and the critically damped camera spring absorb part of a drag during
      // the evidence wait. Carry slightly through the optical centre so the settled bot,
      // rather than the mouse endpoint, lands there.
      x: from.x + (arenaBounds.x + arenaBounds.width / 2 - from.x) * 1.2,
      y: from.y + (arenaBounds.y + arenaBounds.height / 2 - from.y) * 1.55,
    };
    // Put the rendered bot under the optical centre before zooming. WebGL camera zoom is
    // centre-anchored, so reversing these gestures magnifies the empty floor beside a
    // spawn-side body and makes a nominal close-up less useful than the real-scale frame.
    await page.mouse.move(from.x, from.y);
    await page.mouse.down();
    await page.mouse.move(centre.x, centre.y, { steps: 12 });
    await page.mouse.up();
    await page.mouse.wheel(0, -Math.log(captureOptions.zoom) / 0.0016);
    await page.waitForTimeout(3_000);
  }

  const browserScale = await page.evaluate(() => ({
    devicePixelRatio: window.devicePixelRatio,
    visualViewportScale: window.visualViewport?.scale ?? 1,
  }));
  if (browserScale.devicePixelRatio !== 1 || browserScale.visualViewportScale !== 1)
    throw new Error(
      `${variant.label}/${viewport.id} changed browser scale during camera evidence.`,
    );

  const canvasKind = await arena.locator('canvas').evaluate((canvas) =>
    canvas.getContext('2d') ? '2d' : 'webgl',
  );
  if (canvasKind !== 'webgl') throw new Error(`${variant.label}/${viewport.id} fell back to Canvas2D.`);
  const gpu = await arena.locator('canvas').evaluate((canvas) => {
    const gl = canvas.getContext('webgl2') ?? canvas.getContext('webgl');
    if (!gl) return null;
    const debug = gl.getExtension('WEBGL_debug_renderer_info');
    return {
      renderer: debug ? gl.getParameter(debug.UNMASKED_RENDERER_WEBGL) : gl.getParameter(gl.RENDERER),
      webgl2: typeof WebGL2RenderingContext !== 'undefined' && gl instanceof WebGL2RenderingContext,
      astc: Boolean(gl.getExtension('WEBGL_compressed_texture_astc')),
      etc2: Boolean(gl.getExtension('WEBGL_compressed_texture_etc')),
      s3tc: Boolean(gl.getExtension('WEBGL_compressed_texture_s3tc')),
      pvrtc: Boolean(
        gl.getExtension('WEBGL_compressed_texture_pvrtc') ||
        gl.getExtension('WEBKIT_WEBGL_compressed_texture_pvrtc')
      ),
    };
  });
  const uniqueGlbs = new Set(glbResponses.map((response) => response.url));
  if (uniqueGlbs.size !== 16 || glbResponses.some((response) => response.status !== 200))
    throw new Error(
      `${variant.label}/${viewport.id} loaded ${uniqueGlbs.size}/16 fleet GLBs successfully.`,
    );
  if (pageErrors.length || consoleErrors.length || failedRequests.length)
    throw new Error(
      `${variant.label}/${viewport.id} emitted errors:\n${[
        ...pageErrors,
        ...consoleErrors,
        ...failedRequests,
      ].join('\n')}`,
    );

  const prefix = captureOptions.mode === 'inspection'
    ? `${viewport.id}-inspection-${variant.label}`
    : `${viewport.id}-${variant.label}`;
  const framePath = path.join(outputDirectory, `${prefix}-arena.png`);
  await arena.screenshot({ path: framePath, animations: 'disabled' });
  const bounds = await arena.boundingBox();
  const record = {
    variant: variant.label,
    viewport: viewport.id,
    mode: captureOptions.mode ?? 'real-scale',
    viewportPixels: { width: viewport.width, height: viewport.height },
    arenaPixels: bounds && { width: Math.round(bounds.width), height: Math.round(bounds.height) },
    browserScale,
    ...(captureOptions.mode === 'inspection'
      ? {
          cameraInput: {
            kind: 'arena-pan-and-wheel',
            requestedZoomFactor: captureOptions.zoom,
          },
        }
      : {}),
    readyMilliseconds,
    canvasKind,
    gpu,
    modelResponses,
    glbResponses,
    decoderResponses,
    arenaScreenshot: path.relative(repository, framePath),
    arenaSha256: sha256(readFileSync(framePath)),
    pageErrors,
    consoleErrors,
    failedRequests,
  };
  await context.close();
  return record;
}

async function crop(input, output, point, size, viewport) {
  const left = Math.max(0, Math.min(viewport.width - size, Math.round(point.x - size / 2)));
  const top = Math.max(0, Math.min(viewport.height - size, Math.round(point.y - size / 2)));
  await sharp(input).extract({ left, top, width: size, height: size }).png().toFile(output);
}

async function cropVariantFootprint(viewport, mode = 'real-scale', selectedRecords = records) {
  const prefix = mode === 'inspection' ? `${viewport.id}-inspection` : viewport.id;
  const baselinePath = path.join(
    outputDirectory,
    `${prefix}-${variants[0].label}-arena.png`,
  );
  const baseline = await sharp(baselinePath)
    .removeAlpha()
    .raw()
    .toBuffer({ resolveWithObject: true });
  const bounds = {
    left: baseline.info.width,
    top: baseline.info.height,
    right: -1,
    bottom: -1,
  };
  const metrics = [];
  for (const variant of variants.slice(1)) {
    const candidate = await sharp(
      path.join(outputDirectory, `${prefix}-${variant.label}-arena.png`),
    )
      .removeAlpha()
      .raw()
      .toBuffer({ resolveWithObject: true });
    if (
      candidate.info.width !== baseline.info.width ||
      candidate.info.height !== baseline.info.height ||
      candidate.data.length !== baseline.data.length
    )
      throw new Error(`${viewport.id}/${variant.label} changed the evidence viewport.`);
    let changedPixels = 0;
    let absoluteDifference = 0;
    let squaredDifference = 0;
    for (let offset = 0; offset < baseline.data.length; offset += 3) {
      const difference = Math.max(
        Math.abs(baseline.data[offset] - candidate.data[offset]),
        Math.abs(baseline.data[offset + 1] - candidate.data[offset + 1]),
        Math.abs(baseline.data[offset + 2] - candidate.data[offset + 2]),
      );
      absoluteDifference += difference;
      squaredDifference += difference * difference;
      // A two-level delta can be antialiasing or driver noise. Three levels remains
      // sensitive to a texture change at phone scale without letting an unrelated edge
      // make the crop span the entire arena.
      if (difference < 3) continue;
      const pixel = offset / 3;
      const x = pixel % baseline.info.width;
      const y = Math.floor(pixel / baseline.info.width);
      changedPixels += 1;
      bounds.left = Math.min(bounds.left, x);
      bounds.top = Math.min(bounds.top, y);
      bounds.right = Math.max(bounds.right, x);
      bounds.bottom = Math.max(bounds.bottom, y);
    }
    const pixelCount = baseline.info.width * baseline.info.height;
    metrics.push({
      variant: variant.label,
      changedPixels,
      meanAbsoluteDifference: absoluteDifference / pixelCount,
      rootMeanSquareDifference: Math.sqrt(squaredDifference / pixelCount),
    });
  }
  if (bounds.right < bounds.left || bounds.bottom < bounds.top)
    throw new Error(`${viewport.id} variants produced no measurable rendered difference.`);

  const point = {
    x: (bounds.left + bounds.right) / 2,
    y: (bounds.top + bounds.bottom) / 2,
  };
  for (const variant of variants) {
    const cropPath = path.join(
      outputDirectory,
      `${prefix}-${variant.label}-${lookId}.png`,
    );
    await crop(
      path.join(outputDirectory, `${prefix}-${variant.label}-arena.png`),
      cropPath,
      point,
      viewport.crop,
      viewport,
    );
    const record = selectedRecords.find(
      (candidate) => candidate.variant === variant.label && candidate.viewport === viewport.id,
    );
    record.cropScreenshot = path.relative(repository, cropPath);
    record.cropSha256 = sha256(readFileSync(cropPath));
  }
  return {
    threshold: 3,
    focusPixel: point,
    renderedBounds: {
      x: bounds.left,
      y: bounds.top,
      width: bounds.right - bounds.left + 1,
      height: bounds.bottom - bounds.top + 1,
    },
    metrics,
  };
}

async function contactSheet(viewport, mode = 'real-scale') {
  const prefix = mode === 'inspection' ? `${viewport.id}-inspection` : viewport.id;
  const cellWidth = viewport.crop + 16;
  const cellHeight = viewport.crop + 42;
  const composites = [];
  for (let index = 0; index < variants.length; index += 1) {
    const variant = variants[index];
    const image = readFileSync(
      path.join(outputDirectory, `${prefix}-${variant.label}-${lookId}.png`),
    );
    const label = Buffer.from(
      `<svg width="${cellWidth}" height="34" xmlns="http://www.w3.org/2000/svg">` +
      `<rect width="100%" height="100%" fill="#080d12"/>` +
      `<text x="8" y="23" fill="#dbe7ef" font-size="14" font-family="monospace">` +
      `${escapeXml(variant.label)}</text></svg>`,
    );
    composites.push({ input: label, left: index * cellWidth, top: 0 });
    composites.push({ input: image, left: index * cellWidth + 8, top: 34 });
  }
  await sharp({
    create: {
      width: cellWidth * variants.length,
      height: cellHeight,
      channels: 4,
      background: '#080d12',
    },
  })
    .composite(composites)
    .png()
    .toFile(
      path.join(
        outputDirectory,
        `${viewport.id}${mode === 'inspection' ? '-inspection' : ''}-comparison.png`,
      ),
    );
}

async function arenaContactSheet(viewport, mode = 'real-scale') {
  const prefix = mode === 'inspection' ? `${viewport.id}-inspection` : viewport.id;
  const cellWidth = Math.min(720, viewport.width);
  const cellHeight = Math.round(cellWidth * viewport.height / viewport.width);
  const labelHeight = 34;
  const composites = [];
  for (let index = 0; index < variants.length; index += 1) {
    const variant = variants[index];
    const frame = await sharp(
      path.join(outputDirectory, `${prefix}-${variant.label}-arena.png`),
    )
      .resize(cellWidth, cellHeight)
      .png()
      .toBuffer();
    const label = Buffer.from(
      `<svg width="${cellWidth}" height="${labelHeight}" xmlns="http://www.w3.org/2000/svg">` +
      `<rect width="100%" height="100%" fill="#080d12"/>` +
      `<text x="8" y="23" fill="#dbe7ef" font-size="14" font-family="monospace">` +
      `${escapeXml(variant.label)} — in-game camera</text></svg>`,
    );
    composites.push({ input: label, left: index * cellWidth, top: 0 });
    composites.push({ input: frame, left: index * cellWidth, top: labelHeight });
  }
  await sharp({
    create: {
      width: cellWidth * variants.length,
      height: cellHeight + labelHeight,
      channels: 4,
      background: '#080d12',
    },
  })
    .composite(composites)
    .png()
    .toFile(path.join(outputDirectory, `${prefix}-game-camera-comparison.png`));
}

async function readTick(page) {
  const label = await page
    .getByRole('region', { name: 'Arena' })
    .locator('p')
    .filter({ hasText: /^\s*\d{3}\s*\/\s*\d{3}\s*$/ })
    .first()
    .textContent();
  const match = label?.match(/^\s*(\d+)\s*\//);
  if (!match) throw new Error(`Could not read authoritative replay tick from ${label ?? 'nothing'}.`);
  return Number(match[1]);
}

function parseVariant(value) {
  const split = value.indexOf('=');
  if (split <= 0) throw new Error(`Variant must be label=path, received ${value}.`);
  const label = value.slice(0, split);
  if (!/^[a-z0-9]+(?:-[a-z0-9]+)*$/.test(label))
    throw new Error(`Variant label must be kebab-case, received ${label}.`);
  const file = absolute(value.slice(split + 1));
  return { label, file };
}

function parseOptions(args) {
  const parsed = {};
  for (let index = 0; index < args.length; index += 2) {
    const name = args[index];
    const value = args[index + 1];
    if (!name?.startsWith('--') || !value)
      throw new Error(`Expected --name value, received ${name ?? 'nothing'}.`);
    const key = name.slice(2);
    (parsed[key] ??= []).push(value);
  }
  return parsed;
}

function required(name) {
  const value = options[name]?.[0];
  if (!value)
    throw new Error(
      'Usage: capture-model-texture-review.mjs --built-model <dist.glb> --replay <replay.json> ' +
      '--look <look-id> --output-directory <dir> --variant label=file [--variant ...] [--url ...]',
    );
  return value;
}

function absolute(value) {
  return path.resolve(repository, value);
}

function sha256(value) {
  return createHash('sha256').update(value).digest('hex');
}

function escapeXml(value) {
  return value.replaceAll('&', '&amp;').replaceAll('<', '&lt;').replaceAll('>', '&gt;');
}
