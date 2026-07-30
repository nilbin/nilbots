#!/usr/bin/env node

import { createHash } from 'node:crypto';
import { createReadStream } from 'node:fs';
import { mkdir, readFile, stat, writeFile } from 'node:fs/promises';
import { createServer } from 'node:http';
import { createRequire } from 'node:module';
import {
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
const sourceReplayPath = resolve(
  process.env.REPLAY_PATH ??
    join(
      repository,
      'sandbox',
      'frontline-v4-runtime-review',
      'native-cli-c111',
      'replay.json',
    ),
);
const baselineDist = resolve(
  process.env.BASELINE_DIST ??
    join(
      repository,
      'sandbox',
      'frontline-v4-runtime-review',
      'baseline-dist-c111',
      'site',
    ),
);
const candidateDist = resolve(
  process.env.CANDIDATE_DIST ?? join(repository, 'web', 'dist-review'),
);
const outputDirectory = resolve(
  process.env.OUTPUT_DIRECTORY ??
    join(repository, 'art', 'frontline-map-models', 'review', 'runtime'),
);
const conceptPath = resolve(
  process.env.CONCEPT_PATH ??
    join(
      repository,
      'art',
      'frontline-map-models',
      'concepts',
      'frontline-ember-forge-matte-living-bastion-v4.png',
    ),
);
const requireFromWeb = createRequire(join(repository, 'web', 'package.json'));
const { chromium } = requireFromWeb('playwright');
const { createCanvas, loadImage } = requireFromWeb('@napi-rs/canvas');

const expected = {
  sourceSha256: '534ffe61961c86d6d1e07c02b56b3f721ecb392f13fcec0e360e4a3bfe88da68',
  replayHash: '8d2153a991bae77bf6f7d56242c5e20eb217ab02d71acb44c6c51ee6cb5291cf',
  mapId: 'frontline-labs-01-classes',
  width: 23,
  height: 15,
  tickCount: 500,
  reviewTime: 6,
};

const sourceReplayBytes = await readFile(sourceReplayPath);
const sourceSha256 = createHash('sha256')
  .update(sourceReplayBytes)
  .digest('hex');
const sourceReplay = JSON.parse(sourceReplayBytes);
verifySourceReplay(sourceReplay);
const replay = sourceReplay;
const replayBytes = sourceReplayBytes;

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

function verifySourceReplay(document) {
  const map = document?.header?.contract?.map;
  const tick = document?.ticks?.[6];
  const objectives = map?.regions?.filter(
    (region) =>
      region.kind === 'objective' &&
      region.regionId.startsWith('frontline-position-'),
  );
  const homePads = map?.regions?.filter((region) =>
    region.regionId.endsWith('-home-pad'),
  );
  const observed = {
    sourceSha256,
    replayHash: document?.replayHash,
    replayVersion: document?.header?.replayVersion,
    partial: document?.partial,
    mapId: map?.mapId,
    width: map?.width,
    height: map?.height,
    tickCount: document?.ticks?.length,
    activeLives: tick?.postState?.activeLives?.length,
    projectiles: tick?.postState?.projectiles?.length,
    spawnAnchors: map?.spawnAnchors?.length,
    objectiveRegions: objectives?.length,
    homePads: homePads?.length,
    presentation: document?.header?.presentation,
  };
  const wanted = {
    sourceSha256: expected.sourceSha256,
    replayHash: expected.replayHash,
    replayVersion: 3,
    partial: false,
    mapId: expected.mapId,
    width: expected.width,
    height: expected.height,
    tickCount: expected.tickCount,
    activeLives: 2,
    projectiles: 2,
    spawnAnchors: 6,
    objectiveRegions: 5,
    homePads: 2,
    presentation: {
      themeId: 'ember-forge',
      map: {
        boundaryWall: 'perimeter',
        interiorWall: 'cover',
        wallGroups: [],
      },
      forms: [
        {
          formId: 'fabricator-child',
          lookId: 'lattice-loom',
          projectileLookId: 'lattice-rivet',
        },
        {
          formId: 'fabricator-prime',
          lookId: 'lattice-loom',
          projectileLookId: 'lattice-rivet',
        },
      ],
    },
  };
  if (JSON.stringify(observed) !== JSON.stringify(wanted))
    throw new Error(
      `Frontline review source is not the approved replay:\n${JSON.stringify({
        wanted,
        observed,
      }, null, 2)}`,
    );
}

function serve(dist) {
  const server = createServer(async (request, response) => {
    const pathname = decodeURIComponent(
      new URL(request.url ?? '/', 'http://127.0.0.1').pathname,
    );
    if (pathname === '/replay.json') {
      response.writeHead(200, {
        'cache-control': 'no-store',
        'content-type': 'application/json; charset=utf-8',
      });
      response.end(replayBytes);
      return;
    }

    const relative = pathname === '/' ? 'index.html' : pathname.slice(1);
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
        'content-type': mime[extname(path)] ?? 'application/octet-stream',
      });
      createReadStream(path).pipe(response);
    } catch {
      response.writeHead(404);
      response.end('not found');
    }
  });
  return new Promise((accept, reject) => {
    server.once('error', reject);
    server.listen(0, '127.0.0.1', () => accept(server));
  });
}

function address(server) {
  const value = server.address();
  if (!value || typeof value === 'string')
    throw new Error('Review server did not expose a TCP address.');
  return `http://127.0.0.1:${value.port}/?standalone&audio=off`;
}

async function seek(page, target) {
  const pause = page.getByRole('button', { name: 'Pause', exact: true });
  if (await pause.isVisible()) {
    await pause.click();
    await page.waitForTimeout(50);
  }
  const root = page.locator(
    '[aria-label="Match timeline — drag to seek"]',
  );
  const thumb = page.getByRole('slider', { name: 'Playhead' });
  await root.waitFor({ state: 'visible' });
  const bounds = await root.boundingBox();
  if (!bounds) throw new Error('Timeline did not expose clickable bounds.');

  // Land on canonical tick zero first. The full replay has 500 ticks, so a pointer
  // cannot address tick 6 precisely enough on this physical track. Production step
  // controls are exact integers and preserve the native replay untouched.
  await page.mouse.click(bounds.x, bounds.y + bounds.height / 2);
  await page.waitForTimeout(50);
  const atStart = Number(await thumb.getAttribute('aria-valuenow'));
  if (Math.abs(atStart) > 0.011)
    throw new Error(`Timeline left edge reports ${atStart}, expected zero.`);
  const step = page.getByRole('button', {
    name: 'Step forward one tick',
  });
  for (let tick = 0; tick < target; tick += 1) await step.click();
  const value = Number(await thumb.getAttribute('aria-valuenow'));
  if (Math.abs(value - target) > 0.011)
    throw new Error(`Exact step seek reports ${value}, expected ${target}.`);
  return value;
}

async function capture(
  browser,
  server,
  output,
  { cameraFrame, canvasFallback = false },
) {
  const page = await browser.newPage({
    viewport: { width: 1600, height: 1000 },
    deviceScaleFactor: 1,
  });
  const errors = [];
  page.on('pageerror', (error) => errors.push(error.message));
  await page.addInitScript(({ fallback }) => {
    localStorage.setItem('nilbots.soundtrack.enabled.v1', 'false');
    if (!fallback) return;
    const getContext = HTMLCanvasElement.prototype.getContext;
    HTMLCanvasElement.prototype.getContext = function patched(type, ...args) {
      if (
        type === 'webgl' ||
        type === 'webgl2' ||
        type === 'experimental-webgl'
      )
        return null;
      return getContext.call(this, type, ...args);
    };
  }, { fallback: canvasFallback });

  try {
    await page.goto(address(server), {
      waitUntil: 'networkidle',
      timeout: 120_000,
    });
    const arena = page.locator('section[aria-label="Arena"]');
    const canvas = arena.locator('canvas');
    await canvas.waitFor({ state: 'visible', timeout: 120_000 });
    await page
      .getByText(/Loading arena/)
      .waitFor({ state: 'hidden', timeout: 120_000 });
    const actualTime = await seek(page, expected.reviewTime);

    const fit = page.getByRole('button', { name: /fit/i });
    const isFitting = (await fit.getAttribute('aria-pressed')) === 'true';
    if (cameraFrame === 'whole-arena' && isFitting) await fit.click();
    if (cameraFrame === 'auto-fit' && !isFitting) await fit.click();
    if (cameraFrame === '8-tile-minimum') {
      if (!isFitting) await fit.click();
      // This replay has one active unit per team at tick 6.5. Inspecting one unit asks
      // the production auto-camera to fit that team, which reaches the supported
      // eight-tile minimum without a review-only hook or a gesture race.
      await page.getByRole('button', { name: 'Inspect' }).first().click();
    }

    // Identical real-time settling for the camera spring and async material uploads.
    await page.waitForTimeout(3_000);
    if (errors.length > 0) throw new Error(errors.join('\n'));
    await canvas.screenshot({ path: output });
    const metrics = await canvas.evaluate((element) => ({
      width: element.width,
      height: element.height,
      cssWidth: element.clientWidth,
      cssHeight: element.clientHeight,
    }));
    const bytes = (await stat(output)).size;
    if (bytes < 50_000)
      throw new Error(`Review capture looks blank (${bytes} bytes): ${output}`);
    return {
      ...metrics,
      bytes,
      actualTime,
      cameraFrame,
      renderer: canvasFallback ? 'Canvas2D fallback' : 'WebGL',
    };
  } finally {
    await page.close();
  }
}

function drawContained(context, image, x, y, width, height) {
  const scale = Math.min(width / image.width, height / image.height);
  const drawnWidth = image.width * scale;
  const drawnHeight = image.height * scale;
  context.drawImage(
    image,
    x + (width - drawnWidth) / 2,
    y + (height - drawnHeight) / 2,
    drawnWidth,
    drawnHeight,
  );
}

async function contactSheet(path, panels) {
  const images = await Promise.all(panels.map((panel) => loadImage(panel.path)));
  const imageWidth = panels[0].width;
  const imageHeight = panels[0].height;
  const headerHeight = 98;
  const gap = 2;
  const board = createCanvas(
    imageWidth * panels.length + gap * (panels.length - 1),
    imageHeight + headerHeight,
  );
  const context = board.getContext('2d');
  context.fillStyle = '#090706';
  context.fillRect(0, 0, board.width, board.height);

  for (let index = 0; index < panels.length; index += 1) {
    const panel = panels[index];
    const x = index * (imageWidth + gap);
    context.font = '700 18px sans-serif';
    context.fillStyle = panel.color;
    context.fillText(panel.label, x + 20, 30);
    context.font = '12px monospace';
    context.fillStyle = '#ad9c8c';
    context.fillText(panel.subtitle, x + 20, 56);
    context.fillStyle = '#807367';
    context.fillText(panel.detail, x + 20, 77);
    context.fillStyle = '#080706';
    context.fillRect(x, headerHeight, imageWidth, imageHeight);
    drawContained(
      context,
      images[index],
      x,
      headerHeight,
      imageWidth,
      imageHeight,
    );
  }
  await writeFile(path, board.toBuffer('image/png'));
}

await Promise.all([
  stat(join(baselineDist, 'index.html')),
  stat(join(candidateDist, 'index.html')),
  stat(conceptPath),
  mkdir(outputDirectory, { recursive: true }),
]);

const baselineServer = await serve(baselineDist);
const candidateServer = await serve(candidateDist);
const browser = await chromium.launch({
  headless: true,
  args: ['--use-angle=swiftshader'],
});

const paths = Object.fromEntries(
  Object.entries({
    baselineAuto: 'frontline-runtime-v4-before-autofit-v2.png',
    candidateAuto: 'frontline-runtime-v4-after-autofit-v2.png',
    baselineWhole: 'frontline-runtime-v4-before-whole-v2.png',
    candidateWhole: 'frontline-runtime-v4-after-whole-v2.png',
    candidateMinimum: 'frontline-runtime-v4-after-8-tile-v2.png',
    fallbackWhole: 'frontline-runtime-v4-canvas-fallback-v2.png',
    conceptBoard: 'frontline-runtime-v4-concept-autofit-v2.png',
    gameplayBoard: 'frontline-runtime-v4-gameplay-views-v2.png',
    report: 'frontline-runtime-v4-review-v2.json',
  }).map(([key, name]) => [key, join(outputDirectory, name)]),
);

try {
  const baselineAuto = await capture(
    browser,
    baselineServer,
    paths.baselineAuto,
    { cameraFrame: 'auto-fit' },
  );
  const candidateAuto = await capture(
    browser,
    candidateServer,
    paths.candidateAuto,
    { cameraFrame: 'auto-fit' },
  );
  const baselineWhole = await capture(
    browser,
    baselineServer,
    paths.baselineWhole,
    { cameraFrame: 'whole-arena' },
  );
  const candidateWhole = await capture(
    browser,
    candidateServer,
    paths.candidateWhole,
    { cameraFrame: 'whole-arena' },
  );
  const candidateMinimum = await capture(
    browser,
    candidateServer,
    paths.candidateMinimum,
    { cameraFrame: '8-tile-minimum' },
  );
  const fallbackWhole = await capture(
    browser,
    candidateServer,
    paths.fallbackWhole,
    { cameraFrame: 'whole-arena', canvasFallback: true },
  );

  const captures = [
    baselineAuto,
    candidateAuto,
    baselineWhole,
    candidateWhole,
    candidateMinimum,
    fallbackWhole,
  ];
  const canvas = baselineAuto;
  if (
    captures.some(
      (capture) =>
        capture.cssWidth !== canvas.cssWidth ||
        capture.cssHeight !== canvas.cssHeight ||
        Math.abs(capture.actualTime - expected.reviewTime) > 0.011,
    )
  )
    throw new Error(`Capture mismatch: ${JSON.stringify(captures, null, 2)}`);

  const common =
    'fresh canonical 23×15 v3 replay · tick 6 · 2 bots + 2 projectiles';
  const native =
    'native Ember/form presentation · full replay · hash verified';
  await contactSheet(paths.conceptBoard, [
    {
      path: conceptPath,
      width: canvas.cssWidth,
      height: canvas.cssHeight,
      color: '#e9a15f',
      label: 'CONCEPT REFERENCE · V4 DIRECTION',
      subtitle: 'approved material/form language · not tile authority',
      detail: 'not cropped or sampled into runtime · non-gameplay reference',
    },
    {
      path: paths.baselineAuto,
      width: canvas.cssWidth,
      height: canvas.cssHeight,
      color: '#d9916c',
      label: 'BEFORE · LEGACY EMBER RUNTIME',
      subtitle: common,
      detail: `auto-fit · exact 58° camera · ${native}`,
    },
    {
      path: paths.candidateAuto,
      width: canvas.cssWidth,
      height: canvas.cssHeight,
      color: '#8bd5a6',
      label: 'AFTER · DETERMINISTIC V4-INSPIRED RUNTIME',
      subtitle: common,
      detail: `same auto-fit/frame · exact 58° camera · ${native}`,
    },
  ]);
  await contactSheet(paths.gameplayBoard, [
    {
      path: paths.candidateWhole,
      width: canvas.cssWidth,
      height: canvas.cssHeight,
      color: '#8bd5a6',
      label: 'AFTER · WHOLE 23×15 ARENA',
      subtitle: common,
      detail: '6 spawn anchors · 5 capture zones · topology unchanged',
    },
    {
      path: paths.candidateMinimum,
      width: canvas.cssWidth,
      height: canvas.cssHeight,
      color: '#f0b56e',
      label: 'AFTER · 8-TILE GAMEPLAY OBLIQUE',
      subtitle: common,
      detail: 'supported minimum span · exact 58° · wall profile inspection',
    },
    {
      path: paths.fallbackWhole,
      width: canvas.cssWidth,
      height: canvas.cssHeight,
      color: '#8ebad9',
      label: 'CANVAS2D FALLBACK · CANONICAL THEME',
      subtitle: common,
      detail: 'forced no-WebGL · original Ember floor/site asset path',
    },
  ]);

  const report = {
    boards: {
      conceptAndAutoFit: relative(repository, paths.conceptBoard),
      gameplayViews: relative(repository, paths.gameplayBoard),
    },
    rawCaptures: {
      baselineAuto: relative(repository, paths.baselineAuto),
      candidateAuto: relative(repository, paths.candidateAuto),
      baselineWhole: relative(repository, paths.baselineWhole),
      candidateWhole: relative(repository, paths.candidateWhole),
      candidateMinimum: relative(repository, paths.candidateMinimum),
      fallbackWhole: relative(repository, paths.fallbackWhole),
    },
    sourceReplay: {
      durableId: 'frontline-v4-native-cli-fabricator-vs-fabricator-seed-104729',
      generatedPath: relative(repository, sourceReplayPath),
      bytes: sourceReplayBytes.length,
      sha256: sourceSha256,
      canonicalReplayHash: sourceReplay.replayHash,
      replayVersion: sourceReplay.header.replayVersion,
      mapId: sourceReplay.header.contract.map.mapId,
      dimensions: {
        width: sourceReplay.header.contract.map.width,
        height: sourceReplay.header.contract.map.height,
      },
      ticks: sourceReplay.ticks.length,
      command:
        'nilbots experiment frontline-labs --bot <AlphaProject> --opponent <BetaProject> --seed 104729 --runtime in-process --classes fabricator-vs-fabricator --out sandbox/frontline-v4-runtime-review/native-cli-c111',
      verification:
        'nilbots verify: OK — canonical replay v3 content, contract, and hash verify.',
    },
    reviewDocument: {
      partial: replay.partial,
      replayHash: replay.replayHash,
      result: {
        completionReason: replay.result.completionReason,
        endTick: replay.result.endTick,
      },
      ticks: replay.ticks.length,
      time: expected.reviewTime,
      activeLives: replay.ticks[6].postState.activeLives.length,
      projectiles: replay.ticks[6].postState.projectiles.length,
      presentation: replay.header.presentation,
      provenance:
        'Fresh native CLI replay; no review-time replay or presentation mutation.',
    },
    runtimeEvidence: {
      baselineDist: relative(repository, baselineDist),
      candidateDist: relative(repository, candidateDist),
      conceptPath: relative(repository, conceptPath),
      viewport: { width: 1600, height: 1000, deviceScaleFactor: 1 },
      cameraPitchDegrees: 58,
      captures,
      providerCalls: 0,
    },
  };
  await writeFile(paths.report, `${JSON.stringify(report, null, 2)}\n`);
  process.stdout.write(`${JSON.stringify({
    report: paths.report,
    boards: report.boards,
    sourceReplay: report.sourceReplay,
    reviewDocument: report.reviewDocument,
  })}\n`);
} finally {
  await browser.close();
  await Promise.all([
    new Promise((accept) => baselineServer.close(accept)),
    new Promise((accept) => candidateServer.close(accept)),
  ]);
}
