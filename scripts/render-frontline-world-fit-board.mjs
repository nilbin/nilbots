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

const root = join(repository, 'art', 'frontline-map-models');
const output = join(root, 'review', 'world-fit', 'frontline-striker-world-fit-v1.png');
const routes = new Map([
  ['/', join(root, 'world-fit', 'board.html')],
  [
    '/environment.png',
    join(root, 'concepts', 'frontline-ember-forge-matte-living-bastion-v4.png'),
  ],
  [
    '/striker-oblique.png',
    join(root, 'world-fit', 'references', 'striker-oblique-target-v1-review.png'),
  ],
  [
    '/striker-multiview.jpg',
    join(root, 'world-fit', 'references', 'striker-v2-approval-sheet-review.jpg'),
  ],
]);
const contentTypes = {
  '.html': 'text/html; charset=utf-8',
  '.jpg': 'image/jpeg',
  '.png': 'image/png',
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
  if (!address || typeof address === 'string') throw new Error('No local board address.');
  const systemChrome = '/Applications/Google Chrome.app/Contents/MacOS/Google Chrome';
  const browser = await chromium.launch({
    headless: true,
    ...(existsSync(systemChrome) ? { executablePath: systemChrome } : {}),
  });
  try {
    const page = await browser.newPage({
      viewport: { width: 1800, height: 1200 },
      deviceScaleFactor: 1,
    });
    await page.goto(`http://127.0.0.1:${address.port}/`, {
      waitUntil: 'networkidle',
    });
    await mkdir(dirname(output), { recursive: true });
    await page.screenshot({ path: output });
    process.stdout.write(`${JSON.stringify({ output, width: 1800, height: 1200 })}\n`);
  } finally {
    await browser.close();
  }
} finally {
  await new Promise((accept) => server.close(accept));
}
