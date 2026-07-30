#!/usr/bin/env node

import { createReadStream } from 'node:fs';
import { readFile, stat, writeFile } from 'node:fs/promises';
import { createServer } from 'node:http';
import { createRequire } from 'node:module';
import { basename, dirname, extname, join, resolve, sep } from 'node:path';
import { fileURLToPath } from 'node:url';

const scriptDirectory = dirname(fileURLToPath(import.meta.url));
const repository = resolve(scriptDirectory, '..');
const baselineDist = resolve(
  process.env.BASELINE_DIST ??
    join(repository, '..', 'frontline-wall-baseline', 'web', 'dist-review'),
);
const candidateDist = resolve(
  process.env.CANDIDATE_DIST ?? join(repository, 'web', 'dist-review'),
);
const outputDirectory = resolve(
  process.env.OUTPUT_DIRECTORY ??
    join(repository, 'art', 'frontline-map-models', 'review', 'runtime'),
);
const requireFromWeb = createRequire(join(repository, 'web', 'package.json'));
const { chromium } = requireFromWeb('playwright');
const { createCanvas, loadImage } = requireFromWeb('@napi-rs/canvas');

const replay = JSON.parse(
  await readFile(
    join(repository, 'web', 'tests', 'fixtures', 'frontline-replay-v2.json'),
    'utf8',
  ),
);
replay.header.presentation = {
  themeId: 'ember-forge',
  map: {
    boundaryWall: 'perimeter',
    interiorWall: 'cover',
    wallGroups: [],
  },
};
const replayBytes = Buffer.from(JSON.stringify(replay));

const mime = {
  '.css': 'text/css; charset=utf-8',
  '.html': 'text/html; charset=utf-8',
  '.js': 'text/javascript; charset=utf-8',
  '.json': 'application/json; charset=utf-8',
  '.m4a': 'audio/mp4',
  '.png': 'image/png',
  '.svg': 'image/svg+xml',
  '.webp': 'image/webp',
  '.woff2': 'font/woff2',
};

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

async function capture(browser, server, output) {
  const page = await browser.newPage({
    viewport: { width: 1600, height: 1000 },
    deviceScaleFactor: 1,
  });
  const errors = [];
  page.on('pageerror', (error) => errors.push(error.message));
  await page.addInitScript(() => {
    localStorage.setItem('nilbots.soundtrack.enabled.v1', 'false');
  });
  try {
    await page.goto(address(server), {
      waitUntil: 'networkidle',
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

    // Every ArrowLeft is an explicit paused seek. More presses than the fixture has ticks
    // make the chosen frame independent of network/decode time.
    for (let index = 0; index < replay.ticks.length + 2; index += 1)
      await page.keyboard.press('ArrowLeft');

    const fit = page.getByRole('button', { name: /fit/i });
    if ((await fit.getAttribute('aria-pressed')) === 'true') await fit.click();

    // `showEverything` uses the normal camera spring. Let both revisions settle for the
    // same real duration while playback is paused at tick zero.
    await page.waitForTimeout(2_500);
    if (errors.length > 0) throw new Error(errors.join('\n'));
    await arena.locator('canvas').screenshot({ path: output });
    return await arena.locator('canvas').evaluate((canvas) => ({
      width: canvas.width,
      height: canvas.height,
    }));
  } finally {
    await page.close();
  }
}

await stat(join(baselineDist, 'index.html'));
await stat(join(candidateDist, 'index.html'));
await import('node:fs/promises').then(({ mkdir }) =>
  mkdir(outputDirectory, { recursive: true }),
);

const baselineServer = await serve(baselineDist);
const candidateServer = await serve(candidateDist);
const browser = await chromium.launch({
  headless: true,
  args: ['--use-angle=swiftshader'],
});

const baselinePath = join(
  outputDirectory,
  'frontline-runtime-walls-before-v1.png',
);
const candidatePath = join(
  outputDirectory,
  'frontline-runtime-walls-topology-kit-v1.png',
);
const boardPath = join(
  outputDirectory,
  'frontline-runtime-walls-before-after-v1.png',
);

try {
  const baseline = await capture(browser, baselineServer, baselinePath);
  const candidate = await capture(browser, candidateServer, candidatePath);
  if (
    baseline.width !== candidate.width ||
    baseline.height !== candidate.height
  )
    throw new Error(
      `Canvas mismatch: ${JSON.stringify({ baseline, candidate })}`,
    );

  const [beforeImage, afterImage] = await Promise.all([
    loadImage(baselinePath),
    loadImage(candidatePath),
  ]);
  const header = 76;
  const gap = 2;
  const board = createCanvas(
    beforeImage.width + afterImage.width + gap,
    beforeImage.height + header,
  );
  const context = board.getContext('2d');
  context.fillStyle = '#090706';
  context.fillRect(0, 0, board.width, board.height);
  context.font = '700 20px sans-serif';
  context.fillStyle = '#f2a36d';
  context.fillText('BEFORE · FABLE + CLASS MODEL BASELINE', 22, 32);
  context.fillStyle = '#8bd5a6';
  context.fillText(
    'AFTER · FRONTLINE MAP PRESENTATION PILOT',
    beforeImage.width + gap + 22,
    32,
  );
  context.font = '13px monospace';
  context.fillStyle = '#a9947d';
  const subtitle =
    'same replay · tick 0 · 1600×1000 viewport · whole-arena frame · exact 58° camera';
  context.fillText(subtitle, 22, 58);
  context.fillText(subtitle, beforeImage.width + gap + 22, 58);
  context.drawImage(beforeImage, 0, header);
  context.drawImage(afterImage, beforeImage.width + gap, header);
  await writeFile(boardPath, board.toBuffer('image/png'));

  process.stdout.write(
    `${JSON.stringify({
      board: boardPath,
      baseline: baselinePath,
      candidate: candidatePath,
      baselineDist,
      candidateDist,
      replayFixture: 'web/tests/fixtures/frontline-replay-v2.json',
      presentation: replay.header.presentation,
      tick: 0,
      viewport: { width: 1600, height: 1000, deviceScaleFactor: 1 },
      canvas: baseline,
      cameraPitchDegrees: 58,
      cameraFrame: 'whole-arena',
      comparisonScope:
        'full Frontline presentation pilot: spawn/capture overlays and topology walls',
      providerCalls: 0,
      labels: [basename(baselinePath), basename(candidatePath)],
    })}\n`,
  );
} finally {
  await browser.close();
  await Promise.all([
    new Promise((accept) => baselineServer.close(accept)),
    new Promise((accept) => candidateServer.close(accept)),
  ]);
}
