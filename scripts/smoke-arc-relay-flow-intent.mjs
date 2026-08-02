#!/usr/bin/env node

import { createHash } from 'node:crypto';
import { mkdir, writeFile } from 'node:fs/promises';
import { createRequire } from 'node:module';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const requireFromWeb = createRequire(path.join(root, 'web/package.json'));
const { chromium } = requireFromWeb('playwright');
const output = path.join(root, 'docs/reports/assets/arc-relay-flow-intent');
const url = process.env.NILBOTS_FLOW_REVIEW_URL ??
  'http://127.0.0.1:8942/sample-01.html';
await mkdir(output, { recursive: true });

const browser = await chromium.launch({ headless: true });
const context = await browser.newContext({ viewport: { width: 1440, height: 960 } });
const page = await context.newPage();
const errors = [];
page.on('pageerror', (error) => errors.push(error.message));

try {
  await page.goto(url, { waitUntil: 'networkidle' });
  await page.locator('[data-play-overlay="ready"]').waitFor();
  await page.locator('[data-play-button]').click();
  await page.getByRole('button', { name: 'Pause' }).waitFor();
  await page.getByRole('button', { name: 'Pause' }).click();

  const reverse = await page.evaluate(() => {
    const replay = window.__BOTARENA_REPLAY__;
    const worlds = replay?.worlds;
    if (!Array.isArray(worlds)) return null;
    const direction = {
      north: [0, -1],
      east: [1, 0],
      south: [0, 1],
      west: [-1, 0],
    };
    const actors = (world) => new Map(
      (world?.[4] ?? []).map((actor) => [
        `${actor[0]}:${actor[1]}:${actor[2]}`,
        actor,
      ]),
    );
    for (let tick = 1; tick < worlds.length; tick += 1) {
      const before = actors(worlds[tick - 1]);
      const after = actors(worlds[tick]);
      for (const [actorKey, end] of after) {
        const start = before.get(actorKey);
        if (!start) continue;
        const dx = end[6] - start[6];
        const dy = end[7] - start[7];
        if (dx === 0 && dy === 0) continue;
        const forward = direction[start[8]];
        if (!forward || dx * forward[0] + dy * forward[1] >= 0) continue;
        return { actorKey, tick, facing: start[8], dx, dy };
      }
    }
    return null;
  });
  if (reverse === null)
    throw new Error('sample contains no authoritative reverse movement');

  await seek(page, reverse.tick - 0.5);
  const arena = page.locator('[aria-label="Arena"]');
  const motion = await arena.screenshot({
    path: path.join(output, 'reverse-motion.png'),
  });
  const beforePlayhead = Number(
    await page.getByLabel('Playhead').getAttribute('aria-valuenow'),
  );
  await page.getByRole('button', { name: 'Play' }).click();
  await page.waitForTimeout(500);
  await page.getByRole('button', { name: 'Pause' }).click();
  const afterPlayhead = Number(
    await page.getByLabel('Playhead').getAttribute('aria-valuenow'),
  );
  if (!(afterPlayhead > beforePlayhead))
    throw new Error('production viewer playhead did not advance');
  if (errors.length > 0) throw new Error(errors.join('\n'));

  const evidence = {
    schema: 'nilbots-arc-relay-flow-intent-smoke-v1',
    url,
    productionBuild: 'web/dist-gate3',
    renderer: 'Canvas2D',
    authoritativeReverse: reverse,
    playheadAdvancedFrom: beforePlayhead,
    playheadAdvancedTo: afterPlayhead,
    screenshotSha256: createHash('sha256').update(motion).digest('hex'),
    pageErrors: [],
  };
  await writeFile(
    path.join(output, 'smoke.json'),
    `${JSON.stringify(evidence, null, 2)}\n`,
  );
  console.log(JSON.stringify(evidence, null, 2));
} finally {
  await context.close();
  await browser.close();
}

async function seek(page, tick) {
  const timeline = page.getByLabel('Match timeline — drag to seek');
  const box = await timeline.boundingBox();
  const maximum = Number(
    await page.getByLabel('Playhead').getAttribute('aria-valuemax'),
  );
  if (!box || !Number.isFinite(maximum) || maximum <= 0)
    throw new Error('timeline geometry is unavailable');
  await page.mouse.click(
    box.x + Math.max(0, Math.min(1, tick / maximum)) * box.width,
    box.y + box.height / 2,
  );
  await page.waitForTimeout(180);
}
