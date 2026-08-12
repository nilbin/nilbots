#!/usr/bin/env node

import { createHash } from 'node:crypto';
import { createRequire } from 'node:module';
import { readFileSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const repository = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..', '..');
const review = path.resolve(
  repository,
  process.env.REVIEW_ROOT ?? 'art/reviews/arc-relay-model-lighting-v1',
);
const requireFromWeb = createRequire(path.join(repository, 'web', 'package.json'));
const { chromium } = requireFromWeb('playwright');
const frames = [
  ['desktop-overview-tick-60', 'Desktop · overview · tick 60'],
  ['desktop-overview-tick-90', 'Desktop · overview · tick 90'],
  ['phone-landscape-overview-tick-60', 'Phone landscape · overview · tick 60'],
];

const rows = frames.map(([id, label]) => ({
  id,
  label,
  baseline: imageData(path.join(review, 'baseline', `${id}.png`)),
  candidate: imageData(path.join(review, 'candidate', `${id}.png`)),
}));
const browser = await chromium.launch({ headless: true });
try {
  const page = await browser.newPage({ viewport: { width: 1440, height: 900 } });
  await page.setContent(reviewMarkup(rows), { waitUntil: 'load' });
  await page.screenshot({ path: path.join(review, 'gameplay-scale-ab.png'), fullPage: true });
} finally {
  await browser.close();
}

const sheet = readFileSync(path.join(review, 'gameplay-scale-ab.png'));
writeFileSync(
  path.join(review, 'sheet-evidence.json'),
  `${JSON.stringify({
    schemaVersion: 1,
    pinnedCamera: 'overview',
    frames: rows.map(({ id, label, baseline, candidate }) => ({
      id,
      label,
      baselineSha256: baseline.sha256,
      candidateSha256: candidate.sha256,
    })),
    sheet: 'gameplay-scale-ab.png',
    sheetSha256: createHash('sha256').update(sheet).digest('hex'),
  }, null, 2)}\n`,
);
console.log(`Built ${path.relative(repository, path.join(review, 'gameplay-scale-ab.png'))}.`);

function imageData(imagePath) {
  const bytes = readFileSync(imagePath);
  return {
    src: `data:image/png;base64,${bytes.toString('base64')}`,
    sha256: createHash('sha256').update(bytes).digest('hex'),
  };
}

function reviewMarkup(reviewRows) {
  const rowsMarkup = reviewRows.map((row) => `
    <section>
      <h2>${row.label}</h2>
      <div class="pair">
        <figure><figcaption>BASELINE</figcaption><img src="${row.baseline.src}"></figure>
        <figure><figcaption>ACTOR FILL + RIM</figcaption><img src="${row.candidate.src}"></figure>
      </div>
    </section>`).join('');
  return `<!doctype html>
  <meta charset="utf-8">
  <style>
    * { box-sizing: border-box; }
    html, body { margin: 0; background: #09090b; color: #ddd6cb; }
    body { padding: 20px; font-family: ui-monospace, SFMono-Regular, Menlo, monospace; }
    h1 { margin: 0 0 6px; font: 700 20px system-ui, sans-serif; }
    p { margin: 0 0 20px; color: #9b958c; font-size: 12px; }
    section { margin-top: 20px; }
    h2 { margin: 0 0 8px; color: #b8afa2; font-size: 12px; letter-spacing: .04em; }
    .pair { display: grid; grid-template-columns: 1fr 1fr; gap: 10px; }
    figure { position: relative; margin: 0; border: 1px solid #39342e; background: #020203; }
    figcaption { position: absolute; z-index: 1; top: 8px; left: 8px; padding: 5px 7px;
      border: 1px solid #564d42; border-radius: 3px; background: rgba(9, 9, 11, .9);
      color: #f0e9df; font-size: 10px; letter-spacing: .08em; }
    img { display: block; width: 100%; height: auto; }
  </style>
  <h1>Arc Relay model-lighting review</h1>
  <p>Same replay, tick, overview camera, viewport, and WebGL renderer. Only genuine bot and signature-prop GLBs receive the candidate light.</p>
  ${rowsMarkup}`;
}
