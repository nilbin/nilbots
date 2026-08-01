#!/usr/bin/env node

import { createHash } from 'node:crypto';
import { mkdir, writeFile } from 'node:fs/promises';
import { createRequire } from 'node:module';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const requireFromWeb = createRequire(path.join(root, 'web/package.json'));
const { chromium } = requireFromWeb('playwright');
const output = path.join(root, 'docs/reports/assets/presentation-art');
await mkdir(output, { recursive: true });

const oldUrl = process.env.NILBOTS_FLAT_REVIEW_URL ??
  'http://127.0.0.1:8941/sample-01.html';
const newUrl = process.env.NILBOTS_ART_REVIEW_URL ??
  'http://127.0.0.1:8940/sample-01.html';
const browser = await chromium.launch({ headless: true });
const context = await browser.newContext({ viewport: { width: 1440, height: 960 } });
const errors = [];

try {
  const beforePage = await context.newPage();
  beforePage.on('pageerror', (error) => errors.push(`before: ${error.message}`));
  await prepare(beforePage, oldUrl);
  await seek(beforePage, 64.8);
  const flat = await beforePage.locator('[aria-label="Arena"]').screenshot({
    path: path.join(output, 'flat-before.png'),
  });
  await beforePage.close();

  const page = await context.newPage();
  page.on('pageerror', (error) => errors.push(`after: ${error.message}`));
  await page.addInitScript(() => {
    window.__nilbotsAudioStarts = [];
    const prototype = globalThis.BaseAudioContext?.prototype;
    if (!prototype || prototype.__nilbotsWrapped) return;
    const original = prototype.createBufferSource;
    prototype.createBufferSource = function (...args) {
      const source = original.apply(this, args);
      const start = source.start;
      source.start = function (...startArgs) {
        window.__nilbotsAudioStarts.push(performance.now());
        return start.apply(this, startArgs);
      };
      return source;
    };
    prototype.__nilbotsWrapped = true;
  });
  await prepare(page, newUrl);
  const visibleText = await page.locator('body').innerText();
  if (/what matters now|core (born|stolen|banked)|pulse \d/i.test(visibleText))
    throw new Error('Arc Relay prose banner or story cue is still visible');

  await seek(page, 64.8);
  await page.getByRole('button', { name: 'Play' }).click();
  // Cross the tick-104 bank even on a cold art decode. The assertion below
  // still binds the sound to the explicit replay event; this is only wall-clock slack.
  await page.waitForTimeout(1_900);
  await page.getByRole('button', { name: 'Pause' }).click();
  const after = await page.locator('[aria-label="Arena"]').screenshot({
    path: path.join(output, 'tilted-after.png'),
  });
  const director = await page.locator('button', { hasText: 'director' }).getAttribute('aria-pressed');
  if (director !== 'true') throw new Error('auto-director did not stay engaged');
  const sfxStarts = await page.evaluate(() => window.__nilbotsAudioStarts.length);
  if (sfxStarts === 0) {
    const status = await page.locator('[data-sound-effects-control]').innerText();
    const playhead = await page.getByLabel('Playhead').getAttribute('aria-valuenow');
    const audioShape = await page.evaluate(() => ({
      base: typeof globalThis.BaseAudioContext?.prototype.createBufferSource,
      direct: typeof globalThis.AudioContext?.prototype.createBufferSource,
      wrapped: Boolean(globalThis.BaseAudioContext?.prototype.__nilbotsWrapped),
    }));
    throw new Error(
      `no WebAudio source fired during production smoke (${status}; tick ${playhead}; ${JSON.stringify(audioShape)})`,
    );
  }

  await page.getByLabel('Playhead').focus();
  await page.getByLabel('Playhead').press('End');
  await page.waitForTimeout(120);
  await page.getByText(/WINS|DRAW/).waitFor();
  await page.getByText(/REACTOR DESTROYED|HORIZON RANKING|FAULT ELIGIBILITY/).waitFor();
  await page.getByText(/integrity \d\/3/i).first().waitFor();
  await page.getByText(/charge \d\/3/i).first().waitFor();
  const victory = await page.locator('[aria-label="Arena"]').screenshot({
    path: path.join(output, 'victory-screen.png'),
  });
  await page.close();

  if (errors.length > 0) throw new Error(errors.join('\n'));
  const evidence = {
    schema: 'nilbots-presentation-art-smoke-v1',
    oldUrl,
    newUrl,
    productionBuild: 'web/dist-gate3',
    renderer: 'Canvas2D',
    replayTick: 66,
    diegeticOnly: true,
    autoDirectorEngaged: true,
    audioBufferStarts: sfxStarts,
    screenshots: {
      flatBefore: digest(flat),
      tiltedAfter: digest(after),
      victoryScreen: digest(victory),
    },
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

async function prepare(page, url) {
  await page.goto(url, { waitUntil: 'networkidle' });
  await page.locator('[data-play-overlay="ready"]').waitFor();
  await page.locator('[data-play-button]').click();
  await page.getByRole('button', { name: 'Pause' }).waitFor();
  await page
    .locator('[data-sound-effects-control]')
    .getByText('OBSIDIAN FOUNDRY')
    .first()
    .waitFor();
  await page.getByRole('button', { name: 'Pause' }).click();
}

async function seek(page, tick) {
  const timeline = page.getByLabel('Match timeline — drag to seek');
  const box = await timeline.boundingBox();
  const maximum = Number(await page.getByLabel('Playhead').getAttribute('aria-valuemax'));
  if (!box || !Number.isFinite(maximum) || maximum <= 0)
    throw new Error('timeline geometry is unavailable');
  await page.mouse.click(
    box.x + Math.max(0, Math.min(1, tick / maximum)) * box.width,
    box.y + box.height / 2,
  );
  await page.waitForTimeout(180);
}

function digest(buffer) {
  return createHash('sha256').update(buffer).digest('hex');
}
