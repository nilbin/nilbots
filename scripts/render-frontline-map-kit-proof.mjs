#!/usr/bin/env node
/**
 * Render the presentation-only modular Frontline map-kit proof.
 *
 * The proof reads the real experimental map JSON and instances reusable wall
 * parts from its tiles. It deliberately does not write a whole-map mesh or
 * touch the runtime renderer.
 */

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
const view = process.env.FRONTLINE_KIT_VIEW ?? 'map';
if (!['map', 'lineup'].includes(view))
  throw new Error(`Unknown FRONTLINE_KIT_VIEW: ${view}`);

const output = resolve(
  process.env.OUTPUT ??
    join(
      repository,
      'art',
      'frontline-map-models',
      'review',
      'procedural',
      view === 'lineup'
        ? 'frontline-procedural-kit-lineup-v1.png'
        : 'frontline-procedural-topology-proof.png',
    ),
);

const routes = new Map([
  [
    '/',
    join(
      repository,
      'art',
      'frontline-map-models',
      'procedural',
      view === 'lineup' ? 'lineup.html' : 'proof.html',
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
    '/map.json',
    join(repository, 'maps', 'experimental', 'frontline-01.json'),
  ],
  [
    '/floor.webp',
    join(
      repository,
      'web',
      'src',
      'assets',
      'themes',
      'ember-forge',
      'floor-forge.webp',
    ),
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
    '/perimeter-normal.png',
    join(
      repository,
      'art',
      'themes',
      'ember-forge',
      'walls',
      'perimeter',
      'normal.png',
    ),
  ],
  [
    '/perimeter-roughness.png',
    join(
      repository,
      'art',
      'themes',
      'ember-forge',
      'walls',
      'perimeter',
      'roughness.png',
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
  [
    '/cover-normal.png',
    join(
      repository,
      'art',
      'themes',
      'ember-forge',
      'walls',
      'cover',
      'normal.png',
    ),
  ],
  [
    '/cover-roughness.png',
    join(
      repository,
      'art',
      'themes',
      'ember-forge',
      'walls',
      'cover',
      'roughness.png',
    ),
  ],
]);

const contentTypes = {
  '.html': 'text/html; charset=utf-8',
  '.js': 'text/javascript; charset=utf-8',
  '.json': 'application/json; charset=utf-8',
  '.png': 'image/png',
  '.webp': 'image/webp',
};

const server = createServer((request, response) => {
  const path = routes.get(new URL(request.url ?? '/', 'http://127.0.0.1').pathname);
  if (!path || !existsSync(path)) {
    response.writeHead(404);
    response.end('not found');
    return;
  }
  response.setHeader('content-type', contentTypes[extname(path)] ?? 'application/octet-stream');
  createReadStream(path).pipe(response);
});

await new Promise((accept, reject) => {
  server.once('error', reject);
  server.listen(0, '127.0.0.1', accept);
});

try {
  const address = server.address();
  if (!address || typeof address === 'string') throw new Error('No local review address.');

  const systemChrome = '/Applications/Google Chrome.app/Contents/MacOS/Google Chrome';
  const browser = await chromium.launch({
    headless: true,
    ...(existsSync(systemChrome) ? { executablePath: systemChrome } : {}),
    args: ['--use-angle=swiftshader'],
  });
  try {
    const page = await browser.newPage({
      viewport: { width: 1600, height: 1000 },
      deviceScaleFactor: 1,
    });
    const errors = [];
    page.on('console', (message) => {
      if (message.type() === 'error') errors.push(message.text());
    });
    page.on('pageerror', (error) => errors.push(error.message));

    await page.goto(`http://127.0.0.1:${address.port}/`, {
      waitUntil: 'networkidle',
    });
    try {
      await page.waitForSelector('body[data-ready="1"]');
    } catch (error) {
      if (errors.length > 0)
        throw new Error(`Procedural proof did not become ready:\n${errors.join('\n')}`);
      throw error;
    }
    if (errors.length > 0) throw new Error(errors.join('\n'));

    await mkdir(dirname(output), { recursive: true });
    await page.screenshot({ path: output });
    process.stdout.write(
      `${JSON.stringify({
        output,
        view,
        map: 'maps/experimental/frontline-01.json',
        cameraPitchDegrees: 58,
        wholeMapMesh: false,
        rendererModified: false,
      })}\n`,
    );
  } finally {
    await browser.close();
  }
} finally {
  await new Promise((accept) => server.close(accept));
}
