#!/usr/bin/env node

import { createReadStream, existsSync } from 'node:fs';
import { mkdir } from 'node:fs/promises';
import { createServer } from 'node:http';
import { createRequire } from 'node:module';
import { dirname, extname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const scriptDirectory = dirname(fileURLToPath(import.meta.url));
const repository = resolve(scriptDirectory, '..');
const requireFromWeb = createRequire(join(repository, 'web', 'package.json'));
const { chromium } = requireFromWeb('playwright');
const output = resolve(
  process.env.OUTPUT ??
    join(
      repository,
      'art',
      'frontline-map-models',
      'review',
      'clearance',
      'frontline-striker-112-clearance-v1.png',
    ),
);
const worktrees = resolve(repository, '..');
const striker = resolve(
  process.env.STRIKER_REVIEW_GLB ??
    join(
      worktrees,
      'codex-class-models',
      'sandbox',
      'meshy-runtime',
      'v2-off-lean-runtime-correct-112.glb',
    ),
);
if (!existsSync(striker))
  throw new Error(`Approved 1.12 Striker review GLB is unavailable: ${striker}`);

const routes = new Map([
  [
    '/',
    join(
      repository,
      'art',
      'frontline-map-models',
      'clearance',
      'clearance.html',
    ),
  ],
  [
    '/three.module.js',
    join(repository, 'web', 'node_modules', 'three', 'build', 'three.module.min.js'),
  ],
  [
    '/three.core.min.js',
    join(repository, 'web', 'node_modules', 'three', 'build', 'three.core.min.js'),
  ],
  [
    '/GLTFLoader.js',
    join(
      repository,
      'web',
      'node_modules',
      'three',
      'examples',
      'jsm',
      'loaders',
      'GLTFLoader.js',
    ),
  ],
  [
    '/utils/BufferGeometryUtils.js',
    join(
      repository,
      'web',
      'node_modules',
      'three',
      'examples',
      'jsm',
      'utils',
      'BufferGeometryUtils.js',
    ),
  ],
  [
    '/utils/SkeletonUtils.js',
    join(
      repository,
      'web',
      'node_modules',
      'three',
      'examples',
      'jsm',
      'utils',
      'SkeletonUtils.js',
    ),
  ],
  ['/map.json', join(repository, 'maps', 'experimental', 'frontline-01.json')],
  [
    '/floor.webp',
    join(repository, 'web', 'src', 'assets', 'themes', 'ember-forge', 'floor-forge.webp'),
  ],
  [
    '/perimeter-albedo.png',
    join(
      repository,
      'art',
      'themes',
      'ember-forge',
      'walls',
      'perimeter',
      'albedo.png',
    ),
  ],
  [
    '/cover-albedo.png',
    join(
      repository,
      'art',
      'themes',
      'ember-forge',
      'walls',
      'cover',
      'albedo.png',
    ),
  ],
  ['/striker-112.glb', striker],
]);
const types = {
  '.html': 'text/html; charset=utf-8',
  '.js': 'text/javascript; charset=utf-8',
  '.json': 'application/json; charset=utf-8',
  '.png': 'image/png',
  '.webp': 'image/webp',
  '.glb': 'model/gltf-binary',
};
const server = createServer((request, response) => {
  const path = routes.get(new URL(request.url ?? '/', 'http://127.0.0.1').pathname);
  if (!path || !existsSync(path)) {
    process.stderr.write(`404 ${request.url ?? '/'}${path ? ` -> ${path}` : ''}\n`);
    response.writeHead(404);
    response.end('not found');
    return;
  }
  response.setHeader('content-type', types[extname(path)] ?? 'application/octet-stream');
  createReadStream(path).pipe(response);
});
await new Promise((accept, reject) => {
  server.once('error', reject);
  server.listen(0, '127.0.0.1', accept);
});

try {
  const address = server.address();
  if (!address || typeof address === 'string') throw new Error('No local review address.');
  const browser = await chromium.launch({
    headless: true,
    args: ['--use-angle=swiftshader'],
  });
  try {
    const page = await browser.newPage({
      viewport: { width: 1920, height: 1080 },
      deviceScaleFactor: 1,
    });
    const errors = [];
    page.on('console', (message) => {
      if (message.type() === 'error') errors.push(message.text());
    });
    page.on('pageerror', (error) => errors.push(error.message));
    await page.goto(`http://127.0.0.1:${address.port}/`, {
      waitUntil: 'networkidle',
      timeout: 120_000,
    });
    try {
      await page.waitForSelector('html[data-ready="true"]', {
        state: 'attached',
        timeout: 120_000,
      });
    } catch (error) {
      throw new Error(
        `${error.message}\nBrowser errors:\n${errors.join('\n') || '(none)'}`,
      );
    }
    if (errors.length > 0) throw new Error(errors.join('\n'));
    await mkdir(dirname(output), { recursive: true });
    await page.screenshot({ path: output });
    process.stdout.write(
      `${JSON.stringify({
        output,
        map: 'maps/experimental/frontline-01.json',
        strikerReviewSource: striker,
        strikerCommitted: false,
        botVisualSpan: 1.12,
        cameraPitchDegrees: 58,
        focusSpanTiles: 8,
        currentBodyOutset: 0.055,
        proposedOpenEdgeInset: 0.08,
        rendererModified: false,
      })}\n`,
    );
  } finally {
    await browser.close();
  }
} finally {
  await new Promise((accept) => server.close(accept));
}
