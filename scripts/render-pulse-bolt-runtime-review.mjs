#!/usr/bin/env node

import { createHash } from 'node:crypto';
import { createReadStream } from 'node:fs';
import {
  mkdir,
  readFile,
  readdir,
  stat,
  writeFile,
} from 'node:fs/promises';
import { createServer } from 'node:http';
import { createRequire } from 'node:module';
import {
  basename,
  dirname,
  extname,
  join,
  relative,
  resolve,
  sep,
} from 'node:path';
import { fileURLToPath } from 'node:url';

const scriptDirectory = dirname(fileURLToPath(import.meta.url));
const repository = resolve(scriptDirectory, '..');
if (!process.env.REPLAY_PATH)
  throw new Error(
    'REPLAY_PATH is required and must name a real replay-v3 Frontline artifact.',
  );
const replayPath = resolve(process.env.REPLAY_PATH);
const reviewThemeId = process.env.REVIEW_THEME_ID ?? 'ember-forge';
const allowMapPresentationOverlay =
  process.env.ALLOW_MAP_PRESENTATION_OVERLAY === '1';
const dist = resolve(
  process.env.DIST_DIRECTORY ??
    join(repository, 'web', 'dist-review'),
);
const outputDirectory = resolve(
  process.env.OUTPUT_DIRECTORY ??
    join(
      repository,
      'art',
      'class-models',
      'controls',
      'pulse-bolt-3d',
      'review',
      'runtime',
    ),
);
const reviewTime = Number(process.env.REVIEW_TIME ?? 6.5);
if (!Number.isFinite(reviewTime) || reviewTime < 0)
  throw new Error('REVIEW_TIME must be a non-negative finite replay time.');

const modelPath = join(
  repository,
  'web',
  'src',
  'assets',
  'projectile-looks',
  'pulse-bolt',
  'model.glb',
);
const requireFromWeb = createRequire(
  join(repository, 'web', 'package.json'),
);
const { chromium } = requireFromWeb('playwright');
const { createCanvas, loadImage } = requireFromWeb('@napi-rs/canvas');
const viewport = { width: 1600, height: 1000, deviceScaleFactor: 1 };
const mime = {
  '.css': 'text/css; charset=utf-8',
  '.glb': 'model/gltf-binary',
  '.html': 'text/html; charset=utf-8',
  '.js': 'text/javascript; charset=utf-8',
  '.json': 'application/json; charset=utf-8',
  '.m4a': 'audio/mp4',
  '.png': 'image/png',
  '.svg': 'image/svg+xml',
  '.webp': 'image/webp',
  '.woff2': 'font/woff2',
};

const [sourceReplayBytes, modelBytes] = await Promise.all([
  readFile(replayPath),
  readFile(modelPath),
]);
const sourceReplay = JSON.parse(sourceReplayBytes);
const review = reviewReplay(sourceReplay);
const replay = review.document;
const replayBytes = Buffer.from(`${JSON.stringify(replay)}\n`);
assertReplay(sourceReplay, replay);
await stat(join(dist, 'index.html'));
await mkdir(outputDirectory, { recursive: true });

const modelHash = sha256(modelBytes);
const builtModel = await findBuiltAsset(
  join(dist, 'assets'),
  modelHash,
);
const builtModelRelative = `assets/${basename(builtModel)}`;
const candidateServer = await serve({ rejectModel: false });
const fallbackServer = await serve({ rejectModel: true });
const browser = await chromium.launch({
  headless: true,
  args: ['--use-angle=swiftshader'],
});

const paths = {
  auto: join(outputDirectory, 'pulse-bolt-real-replay-auto-fit-v1.png'),
  candidate: join(
    outputDirectory,
    'pulse-bolt-real-replay-authored-v1.png',
  ),
  candidateRepeat: join(
    outputDirectory,
    'pulse-bolt-real-replay-authored-repeat-v1.png',
  ),
  fallback: join(
    outputDirectory,
    'pulse-bolt-real-replay-svg-fallback-v1.png',
  ),
  comparison: join(
    outputDirectory,
    'pulse-bolt-real-replay-comparison-v1.png',
  ),
  detail: join(
    outputDirectory,
    'pulse-bolt-real-replay-difference-detail-v1.png',
  ),
  evidence: join(
    outputDirectory,
    'pulse-bolt-runtime-review-v1.json',
  ),
};

let candidate;
let fallback;
try {
  candidate = await captureCandidate(browser, candidateServer);
  fallback = await captureFallback(browser, fallbackServer);
  await comparisonBoard(
    paths.candidate,
    paths.fallback,
    paths.comparison,
  );
  const changedBounds = await differenceDetail(
    paths.candidate,
    paths.fallback,
    paths.detail,
  );

  const screenshotHashes = {};
  for (const [key, path] of Object.entries(paths)) {
    if (key === 'evidence') continue;
    screenshotHashes[key] = sha256(await readFile(path));
  }
  const deterministicRepeat =
    screenshotHashes.candidate === screenshotHashes.candidateRepeat;
  const fallbackIsDistinct =
    screenshotHashes.candidate !== screenshotHashes.fallback;
  if (!deterministicRepeat)
    throw new Error(
      'Returning to the same paused replay time did not reproduce the same canvas pixels.',
    );
  if (!fallbackIsDistinct)
    throw new Error('The authored GLB and forced SVG fallback are identical.');
  if (
    candidate.errors.length > 0 ||
    fallback.errors.length > 0
  )
    throw new Error(
      `Browser review emitted page errors: ${JSON.stringify({
        candidate: candidate.errors,
        fallback: fallback.errors,
      })}`,
    );

  const evidence = {
    version: 1,
    generatedBy: 'scripts/render-pulse-bolt-runtime-review.mjs',
    replay: {
      file: relative(repository, replayPath),
      sourceByteLength: sourceReplayBytes.length,
      sourceSha256: sha256(sourceReplayBytes),
      sourceReplayHash: sourceReplay.replayHash,
      sourcePresentation: sourceReplay.header.presentation,
      reviewByteLength: replayBytes.length,
      reviewSha256: sha256(replayBytes),
      reviewIsUnhashedPartial: replay.partial && replay.replayHash === null,
      reviewPresentation: replay.header.presentation,
      mapPresentationSource: review.mapPresentationSource,
      tickCount: replay.ticks.length,
      participants: replay.header.provenance.participants.map((participant) => ({
        participantId: participant.participantId,
        teamId: participant.teamId,
        accent: participant.accent,
        projectileLookId: participant.projectileLookId,
      })),
      reviewedProjectiles: projectilesAt(replay, Math.floor(reviewTime)),
    },
    frame: {
      time: reviewTime,
      cameraPitchDegrees: 58,
      autoFitCapture: true,
      closeCapture: {
        minSpanTiles: 8,
        method:
          'disable auto-fit, use the supported wheel gesture, and seek the paused product timeline',
      },
      viewport,
      canvas: candidate.canvas,
    },
    model: {
      source: 'web/src/assets/projectile-looks/pulse-bolt/model.glb',
      byteLength: modelBytes.length,
      sha256: modelHash,
      builtAsset: builtModelRelative,
      candidateRequest: candidate.modelRequest,
      forcedFallbackRequest: fallback.modelRequest,
    },
    assertions: {
      candidateModelServed: candidate.modelRequest.status === 200,
      forcedModelRejected: fallback.modelRequest.status === 404,
      glbAndSvgFallbackAreVisuallyDistinct: fallbackIsDistinct,
      repeatedReplayTimeIsPixelIdentical: deterministicRepeat,
      pageErrors: {
        candidate: candidate.errors,
        fallback: fallback.errors,
      },
    },
    changedPixelBounds: changedBounds,
    screenshots: Object.fromEntries(
      Object.entries(paths)
        .filter(([key]) => key !== 'evidence')
        .map(([key, path]) => [
          key,
          {
            file: basename(path),
            sha256: screenshotHashes[key],
          },
        ]),
    ),
    limitations: [
      review.mapPresentationSource === 'native-replay'
        ? 'Gameplay frames and Ember Forge map presentation come from the named real engine replay. This review changes only per-form projectile presentation, so it is served as an explicitly unhashed partial copy and is not represented as the original canonical replay.'
        : 'PROVISIONAL ONLY: gameplay frames come from the named real engine replay, but its map presentation was null. This review serves an explicitly unhashed partial copy with both Ember Forge map and Pulse Bolt form overlays; it cannot approve the map/theme handoff.',
      'This compares the canonical Pulse Bolt GLB with its forced SVG-derived WebGL fallback; class-specific mechanical projectile models remain art-side until their corrections pass.',
      'Renderer-owned trail, glow, hover, turn easing, impact, and team paint are unchanged between the two captures.',
      'The review uses headless SwiftShader for reproducible evidence; a hardware-GPU visual pass remains part of integration QA.',
    ],
  };
  if (!evidence.assertions.candidateModelServed)
    throw new Error('Candidate review never received the Pulse Bolt GLB.');
  if (!evidence.assertions.forcedModelRejected)
    throw new Error('Fallback review did not reject the Pulse Bolt GLB.');

  await writeFile(
    paths.evidence,
    `${JSON.stringify(evidence, null, 2)}\n`,
  );
  process.stdout.write(`${JSON.stringify(evidence, null, 2)}\n`);
} finally {
  await browser.close();
  await Promise.all([
    close(candidateServer.server),
    close(fallbackServer.server),
  ]);
}

async function captureCandidate(browserInstance, reviewServer) {
  const page = await reviewPage(browserInstance);
  try {
    const modelResponse = waitForModel(page);
    await openReplay(page, reviewServer.server);
    const response = await modelResponse;
    await seek(page, reviewTime);
    await page.waitForTimeout(4_000);
    await screenshot(page, paths.auto);

    await minimumZoom(page);
    await seek(page, reviewTime);
    await settleFrames(page);
    await screenshot(page, paths.candidate);
    await seek(page, reviewTime + 0.25);
    await settleFrames(page);
    await seek(page, reviewTime);
    await settleFrames(page);
    await screenshot(page, paths.candidateRepeat);
    return {
      canvas: await canvasSize(page),
      errors: page.reviewErrors,
      modelRequest: responseSummary(response),
    };
  } finally {
    await page.close();
  }
}

async function captureFallback(browserInstance, reviewServer) {
  const page = await reviewPage(browserInstance);
  try {
    const modelResponse = waitForModel(page);
    await openReplay(page, reviewServer.server);
    const response = await modelResponse;
    await seek(page, reviewTime);
    await page.waitForTimeout(4_000);
    await minimumZoom(page);
    await seek(page, reviewTime);
    await settleFrames(page);
    await screenshot(page, paths.fallback);
    return {
      errors: page.reviewErrors,
      modelRequest: responseSummary(response),
    };
  } finally {
    await page.close();
  }
}

async function reviewPage(browserInstance) {
  const page = await browserInstance.newPage({
    viewport: { width: viewport.width, height: viewport.height },
    deviceScaleFactor: viewport.deviceScaleFactor,
  });
  page.reviewErrors = [];
  page.on('pageerror', (error) =>
    page.reviewErrors.push(error.message),
  );
  await page.addInitScript(() => {
    localStorage.setItem('nilbots.soundtrack.enabled.v1', 'false');
  });
  return page;
}

function waitForModel(page) {
  return page.waitForResponse(
    (response) =>
      new URL(response.url()).pathname === `/${builtModelRelative}`,
    { timeout: 120_000 },
  );
}

async function openReplay(page, server) {
  await page.goto(address(server), {
    waitUntil: 'domcontentloaded',
    timeout: 120_000,
  });
  const arena = page.locator('section[aria-label="Arena"]');
  await arena.locator('canvas').waitFor({
    state: 'visible',
    timeout: 120_000,
  });
  await page
    .getByText(/Loading arena/)
    .waitFor({ state: 'hidden', timeout: 120_000 });
  await page.keyboard.press('ArrowLeft');
}

async function seek(page, time) {
  const root = page.locator(
    '[aria-label="Match timeline — drag to seek"]',
  );
  const thumb = page.locator(
    '[role="slider"][aria-label="Playhead"]',
  );
  await root.waitFor({ state: 'visible', timeout: 120_000 });
  await thumb.waitFor({ state: 'attached', timeout: 120_000 });
  await root.scrollIntoViewIfNeeded();
  const maximum = Number(await thumb.getAttribute('aria-valuemax'));
  if (!Number.isFinite(maximum) || time > maximum)
    throw new Error(
      `Cannot seek to ${time}; timeline maximum is ${maximum}.`,
    );
  const box = await root.boundingBox();
  if (!box) throw new Error('Timeline has no measurable bounds.');

  let ratio = time / maximum;
  for (let attempt = 0; attempt < 6; attempt += 1) {
    await page.mouse.click(
      box.x + box.width * Math.max(0, Math.min(ratio, 1)),
      box.y + box.height / 2,
    );
    await settleFrames(page);
    const current = Number(await thumb.getAttribute('aria-valuenow'));
    if (Math.abs(current - time) <= 0.011) return;
    ratio += (time - current) / maximum;
  }
  throw new Error(
    `Timeline settled at ${await thumb.getAttribute('aria-valuenow')} instead of ${time}.`,
  );
}

async function minimumZoom(page) {
  const canvas = page.locator('section[aria-label="Arena"] canvas');
  const box = await canvas.boundingBox();
  if (!box) throw new Error('Arena canvas has no measurable bounds.');
  const fit = page
    .getByRole('button')
    .filter({ hasText: 'fit' })
    .first();
  if ((await fit.getAttribute('aria-pressed')) === 'true')
    await fit.click();
  await page.mouse.move(
    box.x + box.width / 2,
    box.y + box.height / 2,
  );
  await page.mouse.wheel(0, -5_000);
  await page.waitForTimeout(8_000);
}

async function settleFrames(page) {
  await page.evaluate(
    () =>
      new Promise((accept) =>
        requestAnimationFrame(() =>
          requestAnimationFrame(() =>
            requestAnimationFrame(accept),
          ),
        ),
      ),
  );
}

async function screenshot(page, path) {
  await page
    .locator('section[aria-label="Arena"] canvas')
    .screenshot({ path });
}

async function canvasSize(page) {
  return page
    .locator('section[aria-label="Arena"] canvas')
    .evaluate((canvas) => ({
      width: canvas.width,
      height: canvas.height,
      cssWidth: canvas.clientWidth,
      cssHeight: canvas.clientHeight,
    }));
}

async function comparisonBoard(candidatePath, fallbackPath, output) {
  const [candidateImage, fallbackImage] = await Promise.all([
    loadImage(candidatePath),
    loadImage(fallbackPath),
  ]);
  const header = 76;
  const gap = 2;
  const board = createCanvas(
    candidateImage.width + fallbackImage.width + gap,
    candidateImage.height + header,
  );
  const context = board.getContext('2d');
  context.fillStyle = '#090706';
  context.fillRect(0, 0, board.width, board.height);
  context.font = '700 20px sans-serif';
  context.fillStyle = '#8bd5a6';
  context.fillText('AUTHORED PULSE BOLT GLB', 22, 32);
  context.fillStyle = '#f2a36d';
  context.fillText(
    'FORCED SVG-DERIVED FALLBACK',
    candidateImage.width + gap + 22,
    32,
  );
  context.font = '13px monospace';
  context.fillStyle = '#a9947d';
  const subtitle =
    `real Frontline frames · ${
      review.mapPresentationSource === 'native-replay'
        ? `native ${reviewThemeId} map`
        : `PROVISIONAL ${reviewThemeId} overlay`
    } · t=${reviewTime} · 58° camera · 8-tile minimum`;
  context.fillText(subtitle, 22, 58);
  context.fillText(
    subtitle,
    candidateImage.width + gap + 22,
    58,
  );
  context.drawImage(candidateImage, 0, header);
  context.drawImage(
    fallbackImage,
    candidateImage.width + gap,
    header,
  );
  await writeFile(output, board.toBuffer('image/png'));
}

async function differenceDetail(candidatePath, fallbackPath, output) {
  const [candidateImage, fallbackImage] = await Promise.all([
    loadImage(candidatePath),
    loadImage(fallbackPath),
  ]);
  if (
    candidateImage.width !== fallbackImage.width ||
    candidateImage.height !== fallbackImage.height
  )
    throw new Error('Candidate and fallback canvas sizes differ.');
  const width = candidateImage.width;
  const height = candidateImage.height;
  const probe = createCanvas(width, height);
  const context = probe.getContext('2d');
  context.drawImage(candidateImage, 0, 0);
  const left = context.getImageData(0, 0, width, height).data;
  context.clearRect(0, 0, width, height);
  context.drawImage(fallbackImage, 0, 0);
  const right = context.getImageData(0, 0, width, height).data;

  let minimumX = width;
  let minimumY = height;
  let maximumX = -1;
  let maximumY = -1;
  let changedPixels = 0;
  for (let index = 0; index < left.length; index += 4) {
    const delta = Math.max(
      Math.abs(left[index] - right[index]),
      Math.abs(left[index + 1] - right[index + 1]),
      Math.abs(left[index + 2] - right[index + 2]),
      Math.abs(left[index + 3] - right[index + 3]),
    );
    if (delta < 10) continue;
    const pixel = index / 4;
    const x = pixel % width;
    const y = Math.floor(pixel / width);
    minimumX = Math.min(minimumX, x);
    minimumY = Math.min(minimumY, y);
    maximumX = Math.max(maximumX, x);
    maximumY = Math.max(maximumY, y);
    changedPixels += 1;
  }
  if (changedPixels === 0)
    throw new Error('No changed pixels found between model and fallback.');

  const padding = 48;
  const x = Math.max(0, minimumX - padding);
  const y = Math.max(0, minimumY - padding);
  const cropWidth =
    Math.min(width - 1, maximumX + padding) - x + 1;
  const cropHeight =
    Math.min(height - 1, maximumY + padding) - y + 1;
  const scale = Math.min(2, 900 / Math.max(cropWidth, cropHeight));
  const renderedWidth = Math.round(cropWidth * scale);
  const renderedHeight = Math.round(cropHeight * scale);
  const header = 76;
  const gap = 2;
  const board = createCanvas(
    renderedWidth * 2 + gap,
    renderedHeight + header,
  );
  const boardContext = board.getContext('2d');
  boardContext.fillStyle = '#090706';
  boardContext.fillRect(0, 0, board.width, board.height);
  boardContext.font = '700 20px sans-serif';
  boardContext.fillStyle = '#8bd5a6';
  boardContext.fillText('AUTHORED GLB · DETAIL', 22, 32);
  boardContext.fillStyle = '#f2a36d';
  boardContext.fillText(
    'SVG FALLBACK · SAME PIXELS',
    renderedWidth + gap + 22,
    32,
  );
  boardContext.font = '13px monospace';
  boardContext.fillStyle = '#a9947d';
  boardContext.fillText(
    `${changedPixels} changed pixels above threshold · crop ${cropWidth}×${cropHeight}`,
    22,
    58,
  );
  boardContext.drawImage(
    candidateImage,
    x,
    y,
    cropWidth,
    cropHeight,
    0,
    header,
    renderedWidth,
    renderedHeight,
  );
  boardContext.drawImage(
    fallbackImage,
    x,
    y,
    cropWidth,
    cropHeight,
    renderedWidth + gap,
    header,
    renderedWidth,
    renderedHeight,
  );
  await writeFile(output, board.toBuffer('image/png'));
  return {
    x,
    y,
    width: cropWidth,
    height: cropHeight,
    changedPixels,
    threshold: 10,
  };
}

async function findBuiltAsset(directory, wantedHash) {
  for (const entry of await readdir(directory)) {
    if (extname(entry) !== '.glb') continue;
    const path = join(directory, entry);
    if (sha256(await readFile(path)) === wantedHash) return path;
  }
  throw new Error(
    `No built GLB in ${directory} matches Pulse Bolt ${wantedHash}. Run npm run build:review first.`,
  );
}

async function serve({ rejectModel }) {
  const server = createServer(async (request, response) => {
    const pathname = decodeURIComponent(
      new URL(request.url ?? '/', 'http://127.0.0.1').pathname,
    );
    if (pathname === '/replay.json') {
      response.writeHead(200, {
        'cache-control': 'no-store',
        'content-length': replayBytes.length,
        'content-type': 'application/json; charset=utf-8',
      });
      response.end(replayBytes);
      return;
    }

    const relative = pathname === '/' ? 'index.html' : pathname.slice(1);
    if (rejectModel && relative === builtModelRelative) {
      response.writeHead(404, {
        'cache-control': 'no-store',
        'content-length': 0,
      });
      response.end();
      return;
    }
    const path = resolve(dist, relative);
    if (path !== dist && !path.startsWith(`${dist}${sep}`)) {
      response.writeHead(403);
      response.end('forbidden');
      return;
    }
    try {
      const entry = await stat(path);
      if (!entry.isFile()) throw new Error('not a file');
      response.writeHead(200, {
        'cache-control': 'no-store',
        'content-length': entry.size,
        'content-type':
          mime[extname(path)] ?? 'application/octet-stream',
      });
      createReadStream(path).pipe(response);
    } catch {
      response.writeHead(404);
      response.end('not found');
    }
  });
  await new Promise((accept, reject) => {
    server.once('error', reject);
    server.listen(0, '127.0.0.1', accept);
  });
  return { server };
}

function reviewReplay(document) {
  const copy = structuredClone(document);
  const sourcePresentation = copy.header?.presentation;
  let map;
  let mapPresentationSource;
  if (sourcePresentation === null) {
    if (!allowMapPresentationOverlay)
      throw new Error(
        'Source replay has null presentation. Regenerate it through the Frontline presentation writer, or set ALLOW_MAP_PRESENTATION_OVERLAY=1 for provisional non-approval evidence.',
      );
    map = {
      boundaryWall: 'perimeter',
      interiorWall: 'cover',
      wallGroups: [],
    };
    mapPresentationSource = 'provisional-review-overlay';
  } else {
    if (
      sourcePresentation?.themeId !== reviewThemeId ||
      sourcePresentation?.map?.boundaryWall !== 'perimeter' ||
      sourcePresentation?.map?.interiorWall !== 'cover'
    )
      throw new Error(
        `Source replay must natively carry ${reviewThemeId} with perimeter/cover wall families.`,
      );
    map = structuredClone(sourcePresentation.map);
    mapPresentationSource = 'native-replay';
  }
  const forms = copy.header.contract.rules.forms.map((form) => ({
    formId: form.id,
    lookId: null,
    projectileLookId: 'pulse-bolt',
  }));
  copy.header.presentation = {
    themeId: reviewThemeId,
    map,
    forms,
  };
  copy.result = null;
  copy.replayHash = null;
  copy.partial = true;
  return { document: copy, mapPresentationSource };
}

function assertReplay(source, document) {
  if (
    source.header?.replayVersion !== 3 ||
    !source.header?.contract?.map?.mapId?.startsWith('frontline-') ||
    !Array.isArray(source.header?.provenance?.participants) ||
    source.header.provenance.participants.length < 2 ||
    source.header.provenance.participants.some(
      (participant) => participant.projectileLookId !== 'pulse-bolt',
    )
  )
    throw new Error(
      'Review source must be a real replay-v3 Frontline artifact using Pulse Bolt for at least two participants.',
    );
  if (
    document.header.presentation?.themeId !== reviewThemeId ||
    document.header.presentation?.map?.boundaryWall !== 'perimeter' ||
    document.header.presentation?.map?.interiorWall !== 'cover' ||
    document.partial !== true ||
    document.replayHash !== null
  )
    throw new Error(
      'Review replay must carry an explicit, unhashed presentation-only Frontline overlay.',
    );
  const tick = Math.floor(reviewTime);
  const projectiles = projectilesAt(document, tick);
  if (
    projectiles.length < 2 ||
    new Set(
      projectiles.map((projectile) => projectile.ownerParticipantId),
    ).size < 2
  )
    throw new Error(
      `Review tick ${tick} must show Pulse Bolts from two owners.`,
    );
}

function projectilesAt(document, tick) {
  const frame = document.ticks.find((entry) => entry.tick === tick);
  return (frame?.postState?.projectiles ?? []).map((projectile) => ({
    projectileId: projectile.projectileId,
    ownerParticipantId: projectile.ownerParticipantId,
    ownerTeamId: projectile.ownerTeamId,
    x: projectile.position.x,
    y: projectile.position.y,
    heading: projectile.heading ?? projectile.direction,
  }));
}

function responseSummary(response) {
  return {
    status: response.status(),
    urlPath: new URL(response.url()).pathname,
    contentLength: Number(
      response.headers()['content-length'] ?? 0,
    ),
  };
}

function address(server) {
  const value = server.address();
  if (!value || typeof value === 'string')
    throw new Error('Review server did not expose a TCP address.');
  return `http://127.0.0.1:${value.port}/?standalone&audio=off`;
}

function close(server) {
  return new Promise((accept) => server.close(accept));
}

function sha256(bytes) {
  return createHash('sha256').update(bytes).digest('hex');
}
