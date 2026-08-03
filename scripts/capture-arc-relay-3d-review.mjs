#!/usr/bin/env node

import { createHash } from 'node:crypto';
import { createRequire } from 'node:module';
import { copyFileSync, mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { gunzipSync } from 'node:zlib';
import { withCanonicalTeamVision } from './arc-relay-team-vision.mjs';

const repository = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const requireFromWeb = createRequire(path.join(repository, 'web', 'package.json'));
const { chromium } = requireFromWeb('playwright');
const sharp = requireFromWeb('sharp');
const THREE = requireFromWeb('three');

const options = parseOptions(process.argv.slice(2));
const required = [
  'primary-replay',
  'primary-run',
  'primary-broadcast',
  'swapped-replay',
  'swapped-run',
  'swapped-broadcast',
];
for (const name of required)
  if (!options[name]) throw new Error(`Missing --${name}.`);

const viewport = { width: 1440, height: 900 };
const reviewDirectory = options['output-directory']
  ? absolute(options['output-directory'])
  : path.join(repository, 'art', 'reviews', 'arc-relay-3d');
const stillDirectory = path.join(reviewDirectory, 'stills');
const reviewDist = path.join(repository, 'web', 'dist-review');
const reviewUrl = options.url ?? 'http://127.0.0.1:8931/web/dist-review/?standalone&audio=off';
const scenarios = {
  primary: loadScenario('primary'),
  swapped: loadScenario('swapped'),
};

mkdirSync(stillDirectory, { recursive: true });
const browser = await chromium.launch({ headless: true });
const smoke = {
  schemaVersion: 1,
  viewport,
  url: reviewUrl,
  captures: [],
};

try {
  for (const name of ['primary', 'swapped']) {
    installTransport(scenarios[name]);
    for (const renderer of ['2d', '3d'])
      smoke.captures.push(await capture(name, renderer));
  }

  for (const renderer of ['2d', '3d'])
    copyFileSync(
      path.join(stillDirectory, `arena-primary-${renderer}.png`),
      path.join(stillDirectory, `arena-tick000-${renderer}.png`),
    );

  const primaryPositions = openingPositions(scenarios.primary.replay);
  const swappedPositions = openingPositions(scenarios.swapped.replay);
  const classes = [...primaryPositions.keys()].sort();
  if (
    classes.length !== 16 ||
    classes.some((classId) => !swappedPositions.has(classId))
  )
    throw new Error('Both captures must contain all 16 Arc Relay classes.');

  for (const renderer of ['2d', '3d']) {
    const primaryArena = path.join(stillDirectory, `arena-primary-${renderer}.png`);
    const swappedArena = path.join(stillDirectory, `arena-swapped-${renderer}.png`);
    for (const classId of classes) {
      const primaryPoint = project(
        renderer,
        primaryPositions.get(classId),
        scenarios.primary.replay,
      );
      const swappedPoint = project(
        renderer,
        swappedPositions.get(classId),
        scenarios.swapped.replay,
      );
      const primaryCrop = await crop(primaryArena, primaryPoint);
      const swappedCrop = await crop(swappedArena, swappedPoint);
      await sharp({
        create: {
          width: 440,
          height: 220,
          channels: 4,
          background: '#080d12',
        },
      })
        .composite([
          { input: primaryCrop, left: 0, top: 0 },
          { input: swappedCrop, left: 220, top: 0 },
        ])
        .png()
        .toFile(path.join(stillDirectory, `${classId}-${renderer}.png`));
    }
  }

  writeFileSync(
    path.join(reviewDirectory, 'smoke.json'),
    `${JSON.stringify(smoke, null, 2)}\n`,
  );
} finally {
  installTransport(scenarios.primary);
  await browser.close();
}

console.log(`Captured ${smoke.captures.length} fixed Arc Relay arena frames.`);

async function capture(scenarioName, renderer) {
  const scenario = scenarios[scenarioName];
  const context = await browser.newContext({ viewport, deviceScaleFactor: 1 });
  if (renderer === '2d') {
    await context.addInitScript(() => {
      const nativeGetContext = HTMLCanvasElement.prototype.getContext;
      HTMLCanvasElement.prototype.getContext = function getContext(type, ...args) {
        if (type === 'webgl' || type === 'webgl2' || type === 'experimental-webgl')
          return null;
        return nativeGetContext.call(this, type, ...args);
      };
    });
  }
  await context.addInitScript(() => {
    try {
      Object.defineProperty(Element.prototype, 'requestFullscreen', {
        configurable: true,
        value: undefined,
      });
    } catch {
      // CSS immersive mode is sufficient if a browser makes this property immutable.
    }
  });

  const page = await context.newPage();
  const pageErrors = [];
  const consoleErrors = [];
  const failedRequests = [];
  page.on('pageerror', (error) => pageErrors.push(error.message));
  page.on('requestfailed', (request) =>
    failedRequests.push(`${request.method()} ${request.url()} ${request.failure()?.errorText ?? ''}`),
  );
  page.on('console', (message) => {
    if (message.type() !== 'error') return;
    const value = message.text();
    if (
      renderer === '2d' &&
      (value.includes('Error creating WebGL context') ||
        value.includes('A WebGL context could not be created'))
    )
      return;
    consoleErrors.push(value);
  });

  const started = Date.now();
  await page.goto(`${reviewUrl}&capture=${scenarioName}-${renderer}`, {
    waitUntil: 'domcontentloaded',
  });
  await page.locator('[data-play-overlay="ready"]').waitFor({ timeout: 30_000 });
  const readyMilliseconds = Date.now() - started;
  const cameraToggle = page.getByRole('button', { name: /director|overview/i });
  const cameraLabel = await cameraToggle.textContent();
  if (cameraLabel?.includes('director')) {
    await cameraToggle.click();
  } else {
    // Canvas2D mounts after a forced WebGL failure. Exercise a real state transition so
    // its newly-created camera receives the overview instruction as well as the prop.
    await cameraToggle.click();
    await page.getByRole('button', { name: /director/i }).click();
  }
  await page.getByRole('button', { name: /overview/i }).waitFor();
  const tickBefore = await readTick(page);
  await page.getByRole('button', { name: 'Play match' }).click();
  await page.waitForTimeout(1_600);
  const tickAfter = await readTick(page);
  await page.getByRole('button', { name: 'Pause' }).click();
  // Playwright's actionability round-trip can consume a full replay tick on a busy cold
  // scene. Restart and the immediately-following pause are evidence setup, not user-input
  // coverage, so dispatch them directly and catch the tick-000 render on the next frame.
  await page
    .getByRole('button', { name: 'Restart' })
    .evaluate((button) => button.click());
  await page.waitForFunction(() => {
    const arena = document.querySelector('section[aria-label="Arena"]');
    return [...(arena?.querySelectorAll('p') ?? [])].some((node) =>
      /^\s*000\s*\/\s*\d{3}\s*$/.test(node.textContent ?? ''),
    );
  });
  await page
    .getByRole('button', { name: 'Pause' })
    .evaluate((button) => button.click());
  // A cold sixteen-model scene can spend long enough between React's restart and pause
  // commits to cross tick 001. The transport is paused now, so step back to the requested
  // authoritative opening frame instead of accepting a shifted comparison or racing the
  // next animation frame again.
  for (let attempts = 0; attempts < 3 && (await readTick(page)) > 0; attempts += 1)
    await page
      .getByRole('button', { name: 'Step back one tick' })
      .evaluate((button) => button.click());
  if ((await readTick(page)) !== 0)
    throw new Error(`${scenarioName} ${renderer} restart escaped tick 000 before pause.`);
  await page.getByRole('button', { name: 'full screen' }).click();
  const arena = page.getByRole('region', { name: 'Arena' });
  await page.waitForTimeout(2_500);
  const bounds = await arena.boundingBox();
  if (!bounds || Math.round(bounds.width) !== viewport.width || Math.round(bounds.height) !== viewport.height)
    throw new Error(`${scenarioName} ${renderer} arena did not fill the fixed viewport.`);

  const canvasKind = await arena.locator('canvas').evaluate((canvas) =>
    canvas.getContext('2d') ? '2d' : 'webgl',
  );
  const expectedCanvasKind = renderer === '3d' ? 'webgl' : '2d';
  if (canvasKind !== expectedCanvasKind)
    throw new Error(`${scenarioName} requested ${renderer} but rendered ${canvasKind}.`);
  if (tickAfter <= tickBefore)
    throw new Error(`${scenarioName} ${renderer} playback did not advance.`);
  if (pageErrors.length || consoleErrors.length || failedRequests.length)
    throw new Error(
      `${scenarioName} ${renderer} smoke errors:\n${[
        ...pageErrors,
        ...consoleErrors,
        ...failedRequests,
      ].join('\n')}`,
    );

  const output = path.join(stillDirectory, `arena-${scenarioName}-${renderer}.png`);
  await arena.screenshot({ path: output, animations: 'disabled' });
  const record = {
    scenario: scenarioName,
    renderer,
    canonicalReplayHash: scenario.run.Replay.Hash,
    broadcastGzipSha256: sha256(scenario.broadcastGzip),
    ticks: scenario.replay.ticks.length,
    readyMilliseconds,
    tickBefore,
    tickAfter,
    canvasKind,
    arenaPixels: { width: Math.round(bounds.width), height: Math.round(bounds.height) },
    screenshotSha256: sha256(readFileSync(output)),
    pageErrors,
    consoleErrors,
    failedRequests,
  };
  await context.close();
  return record;
}

async function readTick(page) {
  const value = await page
    .getByRole('region', { name: 'Arena' })
    .locator('p')
    .filter({ hasText: /^\s*\d{3}\s*\/\s*\d{3}\s*$/ })
    .first()
    .textContent();
  const match = value?.match(/\d+/);
  if (!match) throw new Error('Could not read the arena tick badge.');
  return Number(match[0]);
}

function loadScenario(name) {
  const canonicalPath = absolute(options[`${name}-replay`]);
  const runPath = absolute(options[`${name}-run`]);
  const broadcastPath = absolute(options[`${name}-broadcast`]);
  const canonicalGzip = readFileSync(canonicalPath);
  const broadcastGzip = readFileSync(broadcastPath);
  const replay = JSON.parse(gunzipSync(canonicalGzip));
  const broadcastJson = gunzipSync(broadcastGzip).toString('utf8');
  const broadcast = JSON.parse(broadcastJson);
  const run = JSON.parse(readFileSync(runPath, 'utf8'));
  if (
    replay.header?.replayVersion !== 3 ||
    replay.header?.contract?.rules?.gameMode?.kind !== 'arc-relay' ||
    broadcast.broadcastVersion !== 1 ||
    broadcast.canonicalReplayHash !== run.Replay.Hash ||
    broadcast.worlds?.length !== replay.ticks.length
  )
    throw new Error(`${name} inputs do not preserve one canonical Arc Relay replay.`);
  const runtimeBroadcast = withCanonicalTeamVision(broadcast, replay);
  const runtimeBroadcastJson = runtimeBroadcast === broadcast
    ? broadcastJson
    : JSON.stringify(runtimeBroadcast);
  return {
    canonicalGzip,
    broadcastGzip,
    broadcastJson: runtimeBroadcastJson,
    broadcast,
    replay,
    run,
  };
}

function installTransport(scenario) {
  mkdirSync(reviewDist, { recursive: true });
  writeFileSync(path.join(reviewDist, 'replay.json'), scenario.broadcastJson);
  writeFileSync(path.join(reviewDist, 'replay.json.gz'), scenario.broadcastGzip);
  writeFileSync(
    path.join(reviewDist, 'replays.json'),
    `${JSON.stringify([
      {
        id: 'arc-relay-3d-capture',
        url: 'replay.json',
        map: scenario.run.MapId,
        bots: scenario.run.Participants.map((participant) => participant.Name),
        ticks: scenario.replay.ticks.length,
        reason: null,
      },
    ], null, 2)}\n`,
  );
}

function openingPositions(replay) {
  const lives = replay.initialFrame?.state?.activeLives ?? [];
  return new Map(
    lives.map((life) => [
      String(life.formId).replace(/^arc-body-/, ''),
      { x: life.position.x, y: life.position.y },
    ]),
  );
}

function project(renderer, position, replay) {
  if (!position) throw new Error('Missing opening position.');
  const map = replay.header.contract.map;
  if (renderer === '2d') {
    const logicalHeight = viewport.height / 0.9;
    const aspect = viewport.width / logicalHeight;
    const frameWidth = Math.max(map.width + 0.4, (map.height + 0.4) * aspect);
    const frameHeight = frameWidth / aspect;
    const tile = Math.min(viewport.width / frameWidth, logicalHeight / frameHeight);
    const originX = viewport.width / 2 - (map.width / 2) * tile;
    const originY = logicalHeight / 2 - (map.height / 2) * tile;
    return {
      x: originX + (position.x + 0.5) * tile,
      y: (originY + (position.y + 0.5) * tile) * 0.9,
    };
  }

  const aspect = viewport.width / viewport.height;
  const frameWidth = Math.max(map.width + 0.4, (map.height + 0.4) * aspect);
  const frameHeight = frameWidth / aspect;
  const span = Math.max(frameWidth / aspect, frameHeight);
  const camera = new THREE.PerspectiveCamera(42, aspect, 0.1, 200);
  const distance = (span / 2) / Math.tan((camera.fov * Math.PI) / 360);
  const pitch = (58 * Math.PI) / 180;
  camera.position.set(
    map.width / 2,
    Math.sin(pitch) * distance * 1.02,
    map.height / 2 + Math.cos(pitch) * distance * 1.02,
  );
  camera.lookAt(map.width / 2, 0, map.height / 2);
  camera.updateMatrixWorld();
  const point = new THREE.Vector3(position.x + 0.5, 0.25, position.y + 0.5).project(camera);
  return {
    x: (point.x * 0.5 + 0.5) * viewport.width,
    y: (-point.y * 0.5 + 0.5) * viewport.height,
  };
}

async function crop(input, point) {
  // Keep the arena pixels at 1:1 scale. The wider window preserves neighboring tiles
  // and avoids making a gameplay-scale body look like a showroom close-up.
  const width = 220;
  const height = 220;
  const left = Math.max(0, Math.min(viewport.width - width, Math.round(point.x - width / 2)));
  const top = Math.max(0, Math.min(viewport.height - height, Math.round(point.y - height / 2)));
  return sharp(input)
    .extract({ left, top, width, height })
    .png()
    .toBuffer();
}

function parseOptions(args) {
  const parsed = {};
  for (let index = 0; index < args.length; index += 2) {
    const name = args[index];
    if (!name?.startsWith('--') || !args[index + 1])
      throw new Error(`Expected --name value, received ${name ?? 'nothing'}.`);
    parsed[name.slice(2)] = args[index + 1];
  }
  return parsed;
}

function absolute(value) {
  return path.resolve(repository, value);
}

function sha256(value) {
  return createHash('sha256').update(value).digest('hex');
}
