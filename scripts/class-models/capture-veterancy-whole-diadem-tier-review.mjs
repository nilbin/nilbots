#!/usr/bin/env node

/** Render rich/lean rank progression plus a six-view lean rank-five audit. */

import { createHash } from 'node:crypto';
import { createRequire } from 'node:module';
import { mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const repository = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..', '..');
const requireFromWeb = createRequire(path.join(repository, 'web', 'package.json'));
const { chromium } = requireFromWeb('playwright');
const sharp = requireFromWeb('sharp');
const baseUrl = process.argv[2] ?? 'http://127.0.0.1:9201';
const tierRoot =
  'art/class-models/runtime-tiers/arc-relay/meshy7-veterancy-split-diadem-whole-v1';
const output = path.join(
  repository,
  'art/reviews/arc-relay-veterancy-modules-meshy7-v1/gate-c/veterancy-split-diadem-whole-v1',
);
const captures = path.join(output, 'captures');
const viewer = new URL('/scripts/class-models/model-turntable-review.html', baseUrl);
const tile = 360;

mkdirSync(captures, { recursive: true });
const browser = await chromium.launch({ headless: true });
const evidence = [];
try {
  for (const profile of ['rich-512', 'lean-256'])
    for (let rank = 1; rank <= 5; rank += 1)
      evidence.push(await captureModel(rank, profile, 'front-three-quarter'));
  for (const view of ['positive-x', 'negative-x', 'positive-z', 'negative-z', 'top-down'])
    evidence.push(await captureModel(5, 'lean-256', view));
} finally {
  await browser.close();
}

await progressionSheet();
await rankFiveSheet();
const record = {
  schemaVersion: 1,
  source: tierRoot,
  purpose: 'Rich/lean fidelity comparison and fixed-orientation rank-five gate',
  captures: evidence,
  sheets: [
    'rank-1-5-rich-vs-lean.png',
    'rank-5-lean-six-view.png',
  ].map((file) => {
    const bytes = readFileSync(path.join(output, file));
    return { file, bytes: bytes.length, sha256: sha256(bytes) };
  }),
};
writeFileSync(path.join(output, 'capture-record.json'), `${JSON.stringify(record, null, 2)}\n`);
console.log(`Captured ${evidence.length} whole-diadem tier views.`);

async function captureModel(rank, profile, view) {
  const model = `${tierRoot}/rank-${rank}-${profile}.glb`;
  const url = new URL(viewer);
  url.searchParams.set('model', `/${model}`);
  url.searchParams.set('view', view);
  const context = await browser.newContext({
    viewport: { width: tile, height: tile },
    deviceScaleFactor: 1,
  });
  const page = await context.newPage();
  const pageErrors = [];
  const failedRequests = [];
  const modelResponses = [];
  page.on('pageerror', (error) => pageErrors.push(error.message));
  page.on('requestfailed', (request) => failedRequests.push(request.url()));
  page.on('response', (response) => {
    if (new URL(response.url()).pathname.endsWith('.glb'))
      modelResponses.push({ url: response.url(), status: response.status() });
  });
  await page.goto(url.toString(), { waitUntil: 'domcontentloaded' });
  await page.waitForFunction(() => window.__modelTurntableReady === true, null, {
    timeout: 120_000,
  });
  const renderer = await page.locator('canvas').evaluate((canvas) =>
    canvas.getContext('2d') ? '2d' : 'webgl',
  );
  if (
    renderer !== 'webgl' ||
    modelResponses.length !== 1 ||
    modelResponses[0].status !== 200 ||
    pageErrors.length ||
    failedRequests.length
  )
    throw new Error(
      `Rank ${rank} ${profile} ${view} failed: ` +
        JSON.stringify({ renderer, modelResponses, pageErrors, failedRequests }),
    );
  const id = `rank-${rank}-${profile}-${view}`;
  const file = path.join(captures, `${id}.png`);
  await page.locator('canvas').screenshot({ path: file, animations: 'disabled' });
  await context.close();
  const bytes = readFileSync(file);
  return {
    id,
    rank,
    profile,
    view,
    model,
    file: path.relative(repository, file),
    bytes: bytes.length,
    sha256: sha256(bytes),
    renderer,
    pageErrors,
    failedRequests,
  };
}

async function progressionSheet() {
  const labelHeight = 44;
  const headerHeight = 82;
  const width = tile * 5;
  const height = headerHeight + (tile + labelHeight) * 2;
  const composites = [{
    input: titleSvg(width, headerHeight, 'WHOLE SPLIT DIADEM · RANK 1–5 · RICH / LEAN'),
    left: 0,
    top: 0,
  }];
  for (const [row, profile] of ['rich-512', 'lean-256'].entries())
    for (let rank = 1; rank <= 5; rank += 1) {
      const capture = evidence.find(
        (entry) => entry.rank === rank && entry.profile === profile && entry.view === 'front-three-quarter',
      );
      const top = headerHeight + row * (tile + labelHeight);
      composites.push(
        {
          input: labelSvg(tile, labelHeight, `RANK ${rank} · ${profile.toUpperCase()}`),
          left: (rank - 1) * tile,
          top,
        },
        {
          input: readFileSync(path.join(repository, capture.file)),
          left: (rank - 1) * tile,
          top: top + labelHeight,
        },
      );
    }
  await sharp({ create: { width, height, channels: 4, background: '#080d12' } })
    .composite(composites)
    .png({ compressionLevel: 9 })
    .toFile(path.join(output, 'rank-1-5-rich-vs-lean.png'));
}

async function rankFiveSheet() {
  const views = [
    ['front-three-quarter', 'GAMEPLAY 3/4'],
    ['positive-x', '+X CAMERA'],
    ['negative-x', '-X CAMERA'],
    ['positive-z', '+Z CAMERA'],
    ['negative-z', '-Z CAMERA'],
    ['top-down', 'STRICT TOP'],
  ];
  const labelHeight = 44;
  const headerHeight = 82;
  const width = tile * 3;
  const height = headerHeight + (tile + labelHeight) * 2;
  const composites = [{
    input: titleSvg(width, headerHeight, 'RANK 5 LEAN · FIXED ORIENTATION / CLEARANCE'),
    left: 0,
    top: 0,
  }];
  for (const [index, [view, label]] of views.entries()) {
    const capture = evidence.find(
      (entry) => entry.rank === 5 && entry.profile === 'lean-256' && entry.view === view,
    );
    const left = (index % 3) * tile;
    const top = headerHeight + Math.floor(index / 3) * (tile + labelHeight);
    composites.push(
      { input: labelSvg(tile, labelHeight, label), left, top },
      { input: readFileSync(path.join(repository, capture.file)), left, top: top + labelHeight },
    );
  }
  await sharp({ create: { width, height, channels: 4, background: '#080d12' } })
    .composite(composites)
    .png({ compressionLevel: 9 })
    .toFile(path.join(output, 'rank-5-lean-six-view.png'));
}

function titleSvg(width, height, title) {
  return Buffer.from(`<svg xmlns="http://www.w3.org/2000/svg" width="${width}" height="${height}">
    <rect width="100%" height="100%" fill="#080d12"/>
    <text x="16" y="32" fill="#e8eef2" font-family="Menlo,monospace" font-size="21" font-weight="700">${title}</text>
    <text x="16" y="59" fill="#9fb0bb" font-family="Menlo,monospace" font-size="13">Meshy 7 whole-rank models · 34% open center · 0.78 roughness floor · +Y up · +X facing</text>
  </svg>`);
}

function labelSvg(width, height, label) {
  return Buffer.from(`<svg xmlns="http://www.w3.org/2000/svg" width="${width}" height="${height}">
    <rect width="100%" height="100%" fill="#111820"/>
    <text x="12" y="28" fill="#f0d99a" font-family="Menlo,monospace" font-size="15" font-weight="700">${label}</text>
  </svg>`);
}

function sha256(value) {
  return createHash('sha256').update(value).digest('hex');
}
