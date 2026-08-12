#!/usr/bin/env node
/**
 * Exercise the player-facing Orders v2 authoring loop against the controlled
 * site-review server. This deliberately drives the rendered UI instead of
 * importing editor helpers, so nested disclosure, autosave and phone controls
 * are covered by the same path a player uses.
 *
 * Start `cd web && npm run site-review` first, then run:
 *   node scripts/sheet-editor-smoke.mjs
 *
 * Optional environment:
 *   BASE=http://127.0.0.1:4181
 *   SHOTS=/tmp/nilbots-sheet-editor-smoke
 */

import { mkdir, rm } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { dirname, join, resolve } from 'node:path';
import { createRequire } from 'node:module';
import { fileURLToPath } from 'node:url';

const repository = join(dirname(fileURLToPath(import.meta.url)), '..');
const requireFromWeb = createRequire(join(repository, 'web', 'package.json'));
const { chromium } = requireFromWeb('playwright');
const base = (process.env.BASE ?? 'http://127.0.0.1:4181').replace(/\/$/, '');
const shots = resolve(process.env.SHOTS ?? join(tmpdir(), 'nilbots-sheet-editor-smoke'));

const health = await fetch(`${base}/__site-review/health`);
if (!health.ok || health.headers.get('x-nilbots-site-review') !== '1') {
  throw new Error(`${base} is not the controlled site-review server.`);
}

await mkdir(shots, { recursive: true });
const browser = await chromium.launch();
const runs = [
  { name: 'desktop', width: 1180, height: 900 },
  { name: 'phone', width: 390, height: 844 },
];

try {
  for (const run of runs) {
    const context = await browser.newContext({
      viewport: { width: run.width, height: run.height },
      deviceScaleFactor: 2,
      locale: 'en-GB',
      timezoneId: 'Europe/Stockholm',
    });
    const page = await context.newPage();
    const failures = [];
    page.on('pageerror', (error) => failures.push(`page error: ${error.message}`));
    page.on('console', (message) => {
      if (message.type() === 'error') failures.push(`console error: ${message.text()}`);
    });

    await page.goto(`${base}/entrants/sheets/new`, { waitUntil: 'networkidle' });
    await page.getByRole('heading', { name: 'Change. Run. Watch. Adjust.' }).waitFor();
    await page.getByLabel('Entrant name').fill(`Tapline field test ${run.name}`);

    const workshop = page.getByRole('navigation', { name: 'Sheet workshop' });
    await workshop.getByRole('button', { name: /Squad/i }).click();
    await page.getByText('8/8 assigned', { exact: true }).waitFor();
    await screenshot(page, join(shots, `orders-v2-squad-${run.name}.png`));

    await workshop.getByRole('button', { name: /Orders/i }).click();
    const homeFallback = page.locator('[data-sheet-path="orders.roles.0.orders.3"]');
    await homeFallback.getByRole('button', { name: /Hold home well/i }).click();
    await homeFallback.getByRole('button', { name: /^Line/i }).click();

    await page.getByRole('button', { name: '＋ Add priority order' }).click();
    await page.getByRole('button', { name: /Nested play/i }).click();
    const nested = page.locator('[data-sheet-path="orders.roles.0.orders.4"]');
    const child = page.locator('[data-sheet-path="orders.roles.0.orders.4.orders.0"]');
    await child.getByRole('button', { name: /Opening move/i }).click();
    await child.getByText('Movement intent', { exact: true }).waitFor();
    await child.getByRole('button', { name: /Delete/ }).click();
    await nested.getByRole('button', { name: /Delete/ }).click();

    await page.getByRole('button', { name: /Conditions/i }).click();
    await page.getByRole('dialog', { name: 'Named conditions' }).waitFor();
    await page.getByRole('button', { name: /Enemy visible/i }).click();
    const compare = page.getByLabel('Compare with');
    await compare.fill('2');
    await compare.fill('1');
    await page.getByRole('button', { name: 'Close' }).click();

    await workshop.getByRole('button', { name: /Map plan/i }).click();
    await page.getByText('Routes, zones and anchors live on Tapline itself. Social tiles and both spawn sides remain visible while you draw.').waitFor();
    await screenshot(page, join(shots, `orders-v2-map-${run.name}.png`));

    await page.reload({ waitUntil: 'networkidle' });
    const restoredName = await page.getByLabel('Entrant name').inputValue();
    if (restoredName !== `Tapline field test ${run.name}`) {
      failures.push(`IndexedDB autosave did not restore the draft: ${restoredName}`);
    }
    await page.getByText('Draft is ready for the shared compiler.').waitFor();

    const viewportRead = await page.evaluate(() => ({
      overflow: document.documentElement.scrollWidth - document.documentElement.clientWidth,
      visiblePracticeButtons: [...document.querySelectorAll('button')].filter((button) => {
        const style = getComputedStyle(button);
        const rect = button.getBoundingClientRect();
        return /Practice (?:& watch|again)/.test(button.textContent ?? '')
          && style.display !== 'none' && style.visibility !== 'hidden'
          && rect.width > 0 && rect.height > 0;
      }).length,
    }));
    if (viewportRead.overflow > 0) failures.push(`horizontal overflow: ${viewportRead.overflow}px`);
    if (viewportRead.visiblePracticeButtons !== 1) {
      failures.push(`expected one visible practice action, found ${viewportRead.visiblePracticeButtons}`);
    }

    await screenshot(page, join(shots, `orders-v2-restored-${run.name}.png`));
    await page.getByRole('button', { name: 'Practice & watch' }).click();
    await page.waitForURL(/\/matches\//);
    await page.getByLabel('Arena').waitFor();
    await screenshot(page, join(shots, `orders-v2-practice-${run.name}.png`));
    await page.getByRole('link', {
      name: new RegExp(`Tapline field test ${run.name}`),
    }).first().click();
    await page.getByLabel('Entrant name').waitFor();
    const returnedName = await page.getByLabel('Entrant name').inputValue();
    if (returnedName !== `Tapline field test ${run.name}`) {
      failures.push(`viewer return lost the draft: ${returnedName}`);
    }
    await page.getByRole('button', { name: 'Practice again' }).waitFor();
    await screenshot(page, join(shots, `orders-v2-returned-${run.name}.png`));
    await context.close();
    if (failures.length > 0) throw new Error(`${run.name}:\n${failures.join('\n')}`);
    console.log(`${run.name}: author → nest → conditions → map → reload → run → watch → return passed`);
  }
  console.log(`Screenshots: ${shots}`);
} finally {
  await browser.close();
}

async function screenshot(page, path) {
  await rm(path, { force: true });
  await page.screenshot({ path, fullPage: true });
}
