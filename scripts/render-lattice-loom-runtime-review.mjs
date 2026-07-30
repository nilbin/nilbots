#!/usr/bin/env node

import { createHash } from 'node:crypto';
import { createReadStream, existsSync } from 'node:fs';
import {
  mkdir,
  readFile,
  stat,
  writeFile,
} from 'node:fs/promises';
import { createServer } from 'node:http';
import { createRequire } from 'node:module';
import {
  dirname,
  extname,
  join,
  resolve,
  sep,
} from 'node:path';
import { fileURLToPath } from 'node:url';

const scriptDirectory = dirname(fileURLToPath(import.meta.url));
const repository = resolve(scriptDirectory, '..');
const dist = resolve(
  process.env.REVIEW_DIST ?? join(repository, 'web', 'dist-review'),
);
const outputDirectory = resolve(
  process.env.OUTPUT_DIRECTORY ??
    join(repository, 'sandbox', 'class-model-builds', 'lattice-loom', 'runtime-review'),
);
const blueReplayPath = requiredPath('BLUE_REPLAY');
const orangeReplayPath = requiredPath('ORANGE_REPLAY');
const tick = Number(process.env.CAPTURE_TICK ?? 215);
if (!Number.isInteger(tick) || tick < 0)
  throw new Error('CAPTURE_TICK must be a non-negative integer.');

const requireFromWeb = createRequire(join(repository, 'web', 'package.json'));
const { chromium } = requireFromWeb('playwright');
const { createCanvas, loadImage } = requireFromWeb('@napi-rs/canvas');
const [blueBytes, orangeBytes, modelBytes] = await Promise.all([
  readFile(blueReplayPath),
  readFile(orangeReplayPath),
  readFile(
    join(
      repository,
      'web',
      'src',
      'assets',
      'class-looks',
      'lattice-loom',
      'model.glb',
    ),
  ),
]);
const blueReplay = JSON.parse(blueBytes);
const orangeReplay = JSON.parse(orangeBytes);
assertReviewReplay(blueReplay, 'blue');
assertReviewReplay(orangeReplay, 'orange');
await stat(join(dist, 'index.html'));
await mkdir(outputDirectory, { recursive: true });

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

function serve(replayBytes) {
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

async function capture(
  browser,
  replayBytes,
  label,
  { fallback = false } = {},
) {
  const server = await serve(replayBytes);
  const page = await browser.newPage({
    viewport: { width: 1600, height: 1000 },
    deviceScaleFactor: 1,
  });
  const errors = [];
  const glbResponses = [];
  page.on('pageerror', (error) => errors.push(error.message));
  page.on('response', async (response) => {
    if (!new URL(response.url()).pathname.endsWith('.glb')) return;
    glbResponses.push({
      url: new URL(response.url()).pathname,
      status: response.status(),
      contentLength: Number(
        (await response.allHeaders())['content-length'] ?? 0,
      ),
    });
  });
  await page.addInitScript(() => {
    localStorage.setItem('nilbots.soundtrack.enabled.v1', 'false');
  });
  if (fallback)
    await page.route('**/*.glb', async (route) => {
      await route.abort('failed');
    });

  const normalPath = join(
    outputDirectory,
    `lattice-loom-${label}-${fallback ? 'fallback-' : ''}normal-v1.png`,
  );
  const closePath = fallback
    ? null
    : join(
        outputDirectory,
        `lattice-loom-${label}-close-v1.png`,
      );
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

    const pause = page.getByRole('button', { name: 'Pause' });
    if (await pause.isVisible()) await pause.click();
    await page.getByRole('button', { name: 'Restart' }).click();
    await pause.waitFor({ state: 'visible', timeout: 10_000 });
    await pause.click();
    const forward = page.getByRole('button', {
      name: 'Step forward one tick',
    });
    let currentTick = await readPresentedTick(arena);
    while (currentTick < tick) {
      const count = Math.min(24, tick - currentTick);
      await forward.evaluate((button, steps) => {
        for (let index = 0; index < steps; index += 1) button.click();
      }, count);
      await page.waitForTimeout(25);
      const nextTick = await readPresentedTick(arena);
      if (nextTick <= currentTick)
        throw new Error(
          `Transport stopped at tick ${currentTick} while seeking ${tick}.`,
        );
      currentTick = nextTick;
    }
    await page.waitForFunction(
      (wanted) => {
        const arena = document.querySelector(
          'section[aria-label="Arena"]',
        );
        if (!arena) return false;
        return [...arena.querySelectorAll('p')].some((paragraph) =>
          paragraph.textContent
            ?.trim()
            .startsWith(String(wanted).padStart(3, '0')),
        );
      },
      tick,
      { timeout: 30_000 },
    );

    // This is the product camera: auto-fit follows the live cluster at the fixed 58°
    // pitch. Let the spring settle after the explicit seek before recording it.
    await page.waitForTimeout(3_000);
    if (errors.length > 0) throw new Error(errors.join('\n'));
    await canvas.screenshot({ path: normalPath });

    if (closePath) {
      const bounds = await canvas.boundingBox();
      if (!bounds) throw new Error('Arena canvas has no review bounds.');
      await page.mouse.move(
        bounds.x + bounds.width / 2,
        bounds.y + bounds.height / 2,
      );
      // The first gesture hands the camera to the reviewer. The toggle responds by
      // showing the whole board, so make that state transition explicit before the
      // second ordinary wheel gesture takes it to the supported eight-tile minimum.
      await page.mouse.wheel(0, -1);
      await page.waitForTimeout(300);
      await page.mouse.wheel(0, -850);
      await page.waitForTimeout(3_000);
      await canvas.screenshot({ path: closePath });
    }

    const canvasSize = await canvas.evaluate((element) => ({
      width: element.width,
      height: element.height,
    }));
    return {
      label,
      fallback,
      normalPath,
      closePath,
      glbResponses,
      canvas: canvasSize,
    };
  } finally {
    await page.close();
    await new Promise((accept) => server.close(accept));
  }
}

const browser = await chromium.launch({
  headless: true,
  args: ['--use-angle=swiftshader'],
});
let blue;
let orange;
let fallback;
try {
  blue = await capture(browser, blueBytes, 'cyan-source-mirror');
  orange = await capture(browser, orangeBytes, 'orange-source-mirror');
  fallback = await capture(browser, blueBytes, 'cyan-source-mirror', {
    fallback: true,
  });
} finally {
  await browser.close();
}

const boardPath = join(
  outputDirectory,
  'lattice-loom-runtime-team-review-board-v1.png',
);
await makeBoard(
  [
    ['DUPLICATE CYAN SOURCE · NORMAL FOLLOW', blue.normalPath],
    ['DUPLICATE CYAN SOURCE · SUPPORTED MIN-ZOOM', blue.closePath],
    ['DUPLICATE ORANGE SOURCE · NORMAL FOLLOW', orange.normalPath],
    ['DUPLICATE ORANGE SOURCE · SUPPORTED MIN-ZOOM', orange.closePath],
  ],
  boardPath,
);
const fallbackBoardPath = join(
  outputDirectory,
  'lattice-loom-runtime-fallback-board-v1.png',
);
await makeBoard(
  [
    ['GENUINE GLB · NORMAL FOLLOW', blue.normalPath],
    ['SVG-DERIVED FALLBACK · SAME TICK', fallback.normalPath],
  ],
  fallbackBoardPath,
);

const report = {
  schemaVersion: 1,
  runtimeModel: {
    file: 'web/src/assets/class-looks/lattice-loom/model.glb',
    bytes: modelBytes.length,
    sha256: sha256(modelBytes),
  },
  replayPair: {
    blue: replayRecord(blueReplayPath, blueBytes, blueReplay),
    orange: replayRecord(orangeReplayPath, orangeBytes, orangeReplay),
    comparison:
      'real Fable Fabricator mirror replays with duplicate submitted accents; presentation resolves the safe blue/orange team pair, and the selected tick has the same six active form/position/facing/health records',
  },
  camera: {
    normal: 'product auto-fit action follow',
    close:
      'supported manual wheel zoom after camera handoff; deltaY -850, clamped to the product eight-tile minimum',
    pitchDegreesAboveHorizontal: 58,
    perspectiveFieldOfViewDegrees: 42,
  },
  tick,
  viewport: {
    width: 1600,
    height: 1000,
    deviceScaleFactor: 1,
  },
  captures: { blue, orange, fallback },
  boards: { team: boardPath, fallback: fallbackBoardPath },
};
const reportPath = join(
  outputDirectory,
  'lattice-loom-runtime-review-report-v1.json',
);
await writeFile(reportPath, `${JSON.stringify(report, null, 2)}\n`, 'utf8');
process.stdout.write(`${JSON.stringify({ report: reportPath, ...report.boards })}\n`);

async function readPresentedTick(arena) {
  return arena.evaluate((element) => {
    for (const paragraph of element.querySelectorAll('p')) {
      const match = paragraph.textContent?.trim().match(/^(\d+)\s*\/\s*\d+/);
      if (match) return Number(match[1]);
    }
    return -1;
  });
}

function requiredPath(name) {
  const value = process.env[name];
  if (!value)
    throw new Error(
      `${name} is required and must name an immutable real replay.json.`,
    );
  const path = resolve(value);
  if (!existsSync(path)) throw new Error(`${name} does not exist: ${path}`);
  return path;
}

function assertReviewReplay(replay, label) {
  if (!Array.isArray(replay.ticks) || tick >= replay.ticks.length)
    throw new Error(`${label} replay does not contain tick ${tick}.`);
  const classes = replay.header?.contract?.topology?.teams?.map(
    (team) => team.classId,
  );
  if (
    !Array.isArray(classes) ||
    classes.length !== 2 ||
    classes.some((classId) => classId !== 'fabricator')
  )
    throw new Error(`${label} replay is not a Fabricator mirror.`);
  const active = replay.ticks[tick]?.tickStart?.state?.activeLives;
  if (!Array.isArray(active) || active.length !== 6)
    throw new Error(`${label} replay tick ${tick} does not have six bodies.`);
}

function replayRecord(path, bytes, replay) {
  return {
    file: path,
    bytes: bytes.length,
    sha256: sha256(bytes),
    ticks: replay.ticks.length,
    accents: replay.header.provenance.participants.map(
      (participant) => participant.accent,
    ),
    activeAtCapture: replay.ticks[tick].tickStart.state.activeLives.map(
      ({ actorId, formId, position, facing, health }) => ({
        teamId: actorId.teamId,
        formId,
        position,
        facing,
        health,
      }),
    ),
  };
}

async function makeBoard(entries, path) {
  const images = await Promise.all(
    entries.map(async ([label, imagePath]) => ({
      label,
      image: await loadImage(imagePath),
    })),
  );
  const columns = entries.length === 2 ? 2 : 2;
  const rows = Math.ceil(images.length / columns);
  const cellWidth = Math.max(...images.map(({ image }) => image.width));
  const imageHeight = Math.max(...images.map(({ image }) => image.height));
  const titleHeight = 50;
  const header = 78;
  const gap = 3;
  const board = createCanvas(
    columns * cellWidth + (columns - 1) * gap,
    header + rows * (titleHeight + imageHeight) + (rows - 1) * gap,
  );
  const context = board.getContext('2d');
  context.fillStyle = '#090706';
  context.fillRect(0, 0, board.width, board.height);
  context.font = '700 20px sans-serif';
  context.fillStyle = '#f2d9c2';
  context.fillText('LATTICE LOOM · REAL FABLE RUNTIME', 22, 31);
  context.font = '13px monospace';
  context.fillStyle = '#a9947d';
  context.fillText(
    `tick ${tick} · exact 58° / 42° FOV · six separate Fabricator instances · authored GLB unless labelled fallback`,
    22,
    57,
  );
  for (const [index, { label, image }] of images.entries()) {
    const column = index % columns;
    const row = Math.floor(index / columns);
    const x = column * (cellWidth + gap);
    const y = header + row * (titleHeight + imageHeight + gap);
    context.fillStyle = '#12100f';
    context.fillRect(x, y, cellWidth, titleHeight);
    context.font = '700 16px sans-serif';
    context.fillStyle = label.includes('CYAN') ? '#67e8f9' : '#fdba74';
    if (label.includes('FALLBACK')) context.fillStyle = '#d6d3d1';
    context.fillText(label, x + 20, y + 31);
    context.drawImage(image, x, y + titleHeight);
  }
  await writeFile(path, board.toBuffer('image/png'));
}

function sha256(bytes) {
  return createHash('sha256').update(bytes).digest('hex');
}
