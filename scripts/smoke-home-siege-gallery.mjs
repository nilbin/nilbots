#!/usr/bin/env node

import { createHash } from 'node:crypto';
import { createRequire } from 'node:module';
import { mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const requireFromWeb = createRequire(path.join(root, 'web', 'package.json'));
const { chromium } = requireFromWeb('playwright');
const baseUrl = process.argv[2] ?? 'http://127.0.0.1:8948/index.html';
const output = path.resolve(
  process.argv[3] ?? path.join(root, 'docs/reports/assets/home-siege-stage-1'),
);
const expectedCards = Number(process.argv[4] ?? 2);
if (!Number.isInteger(expectedCards) || expectedCards < 1) {
  throw new Error(`Expected-card count must be a positive integer, got ${process.argv[4]}.`);
}
mkdirSync(output, { recursive: true });

const browser = await chromium.launch({
  headless: true,
  args: process.env.REVIEW_HOST_RESOLVER
    ? [`--host-resolver-rules=${process.env.REVIEW_HOST_RESOLVER}`]
    : [],
});
const page = await browser.newPage({ viewport: { width: 1440, height: 960 } });
const errors = [];
page.on('pageerror', (error) => errors.push(`page: ${error.message}`));
page.on('requestfailed', (request) =>
  errors.push(`request: ${request.url()} ${request.failure()?.errorText ?? ''}`));
page.on('console', (message) => {
  if (message.type() === 'error') errors.push(`console: ${message.text()}`);
});

const evidence = { schemaVersion: 1, baseUrl, cards: [], samples: [] };
try {
  await page.goto(baseUrl, { waitUntil: 'networkidle' });
  const cards = await page.locator('ul a').evaluateAll((nodes) => nodes.map((node) => ({
    href: node.getAttribute('href'),
    title: node.querySelector('strong')?.textContent?.trim() ?? '',
    subtitle: node.querySelector('span')?.textContent?.trim() ?? '',
    score: [...node.querySelectorAll('dl > div')]
      .find((row) => row.querySelector('dt')?.textContent?.trim() === 'Final score')
      ?.querySelector('dd')?.textContent?.trim() ?? '',
  })));
  if (cards.length !== expectedCards) {
    throw new Error(`Expected ${expectedCards} cards, got ${cards.length}.`);
  }
  for (const card of cards) {
    if (!card.href || !card.title.includes('Home Siege')
        || !card.subtitle.includes('wins')
        || !card.score.includes('Core deliveries')) {
      throw new Error(`Incomplete outcome-visible card: ${JSON.stringify(card)}`);
    }
  }
  const indexPath = path.join(output, 'gallery-index.png');
  await page.screenshot({ path: indexPath, fullPage: true });
  evidence.cards = cards;
  evidence.indexScreenshotSha256 = sha256(indexPath);

  for (const [index, card] of cards.entries()) {
    const before = errors.length;
    await page.goto(new URL(card.href, baseUrl).href, {
      waitUntil: 'domcontentloaded',
    });
    await page.locator('[data-play-overlay="ready"]').waitFor({ timeout: 45_000 });
    const arena = page.getByRole('region', { name: 'Arena' });
    const canvas = arena.locator('canvas');
    await canvas.waitFor({ timeout: 45_000 });
    await page.locator('[aria-label="Arc Relay score"]').waitFor({ timeout: 15_000 });
    const renderer = await canvas.evaluate((element) =>
      element.getContext('2d') ? 'canvas2d' : 'webgl');
    const tickBefore = Number(await page.getByLabel('Playhead').getAttribute('aria-valuenow'));
    await page.getByRole('button', { name: 'Play match' }).click();
    await page.waitForTimeout(1_500);
    const tickAfter = Number(await page.getByLabel('Playhead').getAttribute('aria-valuenow'));
    if (!(tickAfter > tickBefore)) throw new Error(`${card.title}: playback did not advance.`);
    await page.getByRole('button', { name: 'Pause' }).click();
    const screenshot = path.join(output, `sample-${index + 1}.png`);
    await arena.screenshot({ path: screenshot, animations: 'disabled' });
    const added = errors.slice(before);
    if (added.length) throw new Error(`${card.title}: ${added.join('\n')}`);
    evidence.samples.push({
      href: card.href,
      renderer,
      scoreBug: true,
      tickBefore,
      tickAfter,
      screenshotSha256: sha256(screenshot),
    });
  }
} finally {
  await browser.close();
}

evidence.errors = errors;
writeFileSync(
  path.join(output, 'smoke.json'),
  `${JSON.stringify(evidence, null, 2)}\n`,
);
console.log(`Smoked ${evidence.samples.length} outcome-visible Home Siege replays.`);

function sha256(file) {
  return createHash('sha256').update(readFileSync(file)).digest('hex');
}
