#!/usr/bin/env node

/** Capture one pinned replay through two production builds for wall-cap A/B review. */

import { createHash } from 'node:crypto';
import { createReadStream } from 'node:fs';
import { mkdir, readFile, stat, writeFile } from 'node:fs/promises';
import { createServer } from 'node:http';
import { createRequire } from 'node:module';
import { dirname, extname, join, relative, resolve, sep } from 'node:path';
import { fileURLToPath } from 'node:url';

const scriptDirectory = dirname(fileURLToPath(import.meta.url));
const repository = resolve(scriptDirectory, '..');
const args = parseArguments(process.argv.slice(2));
const replayPath = resolve(args.replay);
const output = resolve(args.output);
const builds = [
  { id: 'before', label: 'BEFORE · SQUARE CAP LIPS', dist: resolve(args.baselineDist) },
  { id: 'after', label: 'AFTER · EXPOSED CORNERS SHAPED', dist: resolve(args.candidateDist) },
];
const replayBytes = await readFile(replayPath);
const replay = JSON.parse(replayBytes);
const requireFromWeb = createRequire(join(repository, 'web', 'package.json'));
const { chromium } = requireFromWeb('playwright');
const { createCanvas, loadImage } = requireFromWeb('@napi-rs/canvas');

await mkdir(output, { recursive: true });
await Promise.all(builds.map(({ dist }) => stat(join(dist, 'index.html'))));
const browser = await chromium.launch({
  headless: true,
  args: ['--use-angle=swiftshader'],
});
const captures = [];
try {
  for (const build of builds) captures.push(await captureBuild(browser, build));
} finally {
  await browser.close();
}

const sheet = createCanvas(1628, 552);
const context = sheet.getContext('2d');
context.fillStyle = '#080d13';
context.fillRect(0, 0, sheet.width, sheet.height);
context.font = '600 22px ui-monospace, monospace';
context.textBaseline = 'middle';
context.fillStyle = '#edf5fa';
for (const [index, capture] of captures.entries()) {
  const left = 14 + index * 807;
  const image = await loadImage(resolve(repository, capture.path));
  context.drawImage(image, left, 14, 793, 496);
  context.strokeStyle = index === 0 ? '#6b7885' : '#7ac7a5';
  context.lineWidth = 3;
  context.strokeRect(left + 1.5, 15.5, 790, 493);
  context.fillText(capture.label, left + 8, 532);
}
const sheetPath = join(output, 'wall-cap-before-after.png');
const sheetBytes = sheet.toBuffer('image/png');
await writeFile(sheetPath, sheetBytes);
const report = {
  schemaVersion: 1,
  purpose: 'Same replay/tick/camera comparison of topology cap corner geometry',
  replay: {
    file: displayPath(replayPath),
    bytes: replayBytes.length,
    sha256: sha256(replayBytes),
    canonicalHash: replay.replayHash,
    themeId: replay.header?.presentation?.themeId,
  },
  tick: args.tick,
  viewport: { width: 1600, height: 1000, deviceScaleFactor: 1 },
  camera: 'production 58-degree whole-arena, Fit off',
  replayMutation: false,
  captures,
  contactSheet: {
    path: displayPath(sheetPath),
    bytes: sheetBytes.length,
    sha256: sha256(sheetBytes),
  },
};
const reportPath = join(output, 'wall-cap-evidence.json');
await writeFile(reportPath, `${JSON.stringify(report, null, 2)}\n`);
process.stdout.write(
  `${JSON.stringify(
    { report: displayPath(reportPath), contactSheet: report.contactSheet },
    null,
    2,
  )}\n`,
);

async function captureBuild(browser, build) {
  const server = await serve(build.dist, replayBytes);
  const context = await browser.newContext({
    viewport: { width: 1600, height: 1000 },
    deviceScaleFactor: 1,
  });
  const page = await context.newPage();
  const pageErrors = [];
  const consoleErrors = [];
  const failedRequests = [];
  page.on('pageerror', (error) => pageErrors.push(error.message));
  page.on('console', (message) => {
    if (message.type() === 'error') consoleErrors.push(message.text());
  });
  page.on('requestfailed', (request) =>
    failedRequests.push(`${request.url()}: ${request.failure()?.errorText ?? 'failed'}`),
  );
  await page.addInitScript(() => {
    localStorage.setItem('nilbots.soundtrack.enabled.v1', 'false');
  });
  try {
    await page.goto(address(server), { waitUntil: 'domcontentloaded', timeout: 60_000 });
    const arena = page.locator('section[aria-label="Arena"]');
    const canvas = arena.locator('canvas');
    try {
      await canvas.waitFor({ state: 'visible', timeout: 30_000 });
    } catch (error) {
      const body = (await page.locator('body').innerText()).slice(0, 2_000);
      throw new Error(
        `${build.id} arena did not mount.\nPage errors: ${pageErrors.join(' | ') || 'none'}\n` +
          `Console errors: ${consoleErrors.join(' | ') || 'none'}\n` +
          `Failed requests: ${failedRequests.join(' | ') || 'none'}\nBody: ${body}`,
        { cause: error },
      );
    }
    const play = page.getByRole('button', { name: 'Play match', exact: true });
    await play.waitFor({ state: 'visible', timeout: 180_000 });
    await play.click();
    await page.locator('[data-play-overlay]').waitFor({ state: 'detached', timeout: 30_000 });
    await seek(page, args.tick);
    const fit = page.getByRole('button', { name: /fit/i });
    if ((await fit.getAttribute('aria-pressed')) === 'true') await fit.click();
    await page.waitForTimeout(1_500);
    if (pageErrors.length > 0) throw new Error(pageErrors.join('\n'));

    await canvas.evaluate((element) =>
      element.scrollIntoView({ block: 'center', inline: 'center', behavior: 'instant' }),
    );
    await page.waitForTimeout(120);
    const box = await canvas.boundingBox();
    if (!box) throw new Error('Arena canvas has no visible bounds.');
    const cdp = await context.newCDPSession(page);
    const screenshot = await cdp.send('Page.captureScreenshot', {
      format: 'png',
      fromSurface: true,
      captureBeyondViewport: false,
    });
    await cdp.detach();
    const viewport = await loadImage(Buffer.from(screenshot.data, 'base64'));
    const left = Math.max(0, box.x);
    const top = Math.max(0, box.y);
    const right = Math.min(1600, box.x + box.width);
    const bottom = Math.min(1000, box.y + box.height);
    const crop = createCanvas(Math.round(right - left), Math.round(bottom - top));
    crop.getContext('2d').drawImage(
      viewport,
      left,
      top,
      right - left,
      bottom - top,
      0,
      0,
      crop.width,
      crop.height,
    );
    const bytes = crop.toBuffer('image/png');
    if (bytes.length < 50_000) throw new Error(`${build.id} capture appears blank.`);
    const path = join(output, `${build.id}.png`);
    await writeFile(path, bytes);
    return {
      id: build.id,
      label: build.label,
      dist: displayPath(build.dist),
      path: displayPath(path),
      bytes: bytes.length,
      sha256: sha256(bytes),
    };
  } finally {
    await context.close();
    await new Promise((accept) => server.close(accept));
  }
}

async function seek(page, target) {
  const pause = page.getByRole('button', { name: 'Pause', exact: true });
  if (await pause.isVisible()) await pause.click();
  const timeline = page.locator('[aria-label="Match timeline — drag to seek"]');
  const thumb = page.getByRole('slider', { name: 'Playhead' });
  const bounds = await timeline.boundingBox();
  if (!bounds) throw new Error('Timeline has no bounds.');
  const maximum = Number(await thumb.getAttribute('aria-valuemax'));
  await page.mouse.click(
    bounds.x + (bounds.width * target) / maximum,
    bounds.y + bounds.height / 2,
  );
  await thumb.focus();
  let current = Number(await thumb.getAttribute('aria-valuenow'));
  const steps = Math.round((target - current) / 0.01);
  if (Math.abs(steps) > 240) throw new Error(`Pointer seek reached ${current}.`);
  const key = steps < 0 ? 'ArrowLeft' : 'ArrowRight';
  for (let index = 0; index < Math.abs(steps); index += 1) await page.keyboard.press(key);
}

function serve(root, bytes) {
  const server = createServer(async (request, response) => {
    const pathname = decodeURIComponent(
      new URL(request.url ?? '/', 'http://127.0.0.1').pathname,
    );
    if (pathname === '/replay.json') {
      response.writeHead(200, {
        'cache-control': 'no-store',
        'content-length': bytes.length,
        'content-type': 'application/json; charset=utf-8',
      });
      response.end(bytes);
      return;
    }
    const local = pathname === '/' ? 'index.html' : pathname.slice(1);
    const path = resolve(root, local);
    if (path !== root && !path.startsWith(`${root}${sep}`)) {
      response.writeHead(403);
      response.end('forbidden');
      return;
    }
    try {
      const metadata = await stat(path);
      response.writeHead(200, {
        'cache-control': 'no-store',
        'content-length': metadata.size,
        'content-type': mimeType(path),
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
  if (!value || typeof value === 'string') throw new Error('Server has no address.');
  return `http://127.0.0.1:${value.port}/?standalone&audio=off`;
}

function parseArguments(values) {
  const result = { tick: 0 };
  const flags = new Map([
    ['--baseline-dist', 'baselineDist'],
    ['--candidate-dist', 'candidateDist'],
    ['--replay', 'replay'],
    ['--output', 'output'],
    ['--tick', 'tick'],
  ]);
  for (let index = 0; index < values.length; index += 2) {
    const key = flags.get(values[index]);
    if (!key || !values[index + 1]) throw new Error(`Invalid argument ${values[index]}.`);
    result[key] = key === 'tick' ? Number(values[index + 1]) : values[index + 1];
  }
  for (const required of ['baselineDist', 'candidateDist', 'replay', 'output']) {
    if (!result[required]) throw new Error(`Missing --${required.replace(/[A-Z]/g, (c) => `-${c.toLowerCase()}`)}.`);
  }
  return result;
}

function displayPath(path) {
  const local = relative(repository, resolve(path));
  return local.startsWith('..') ? resolve(path) : local;
}

function sha256(bytes) {
  return createHash('sha256').update(bytes).digest('hex');
}

function mimeType(path) {
  return (
    {
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
    }[extname(path)] ?? 'application/octet-stream'
  );
}
