#!/usr/bin/env node

import { createHash } from 'node:crypto';
import { createRequire } from 'node:module';
import { mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const repository = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const requireFromWeb = createRequire(path.join(repository, 'web', 'package.json'));
const { chromium } = requireFromWeb('playwright');
const options = parseOptions(process.argv.slice(2));
const baseUrl = options.url;
if (!baseUrl) throw new Error('Missing --url.');
const outputDirectory = path.resolve(
  options.output ?? path.join(repository, 'art', 'reviews', 'arc-relay-operation-counterplay'),
);
const expectedRenderer = options.renderer ?? 'webgl';
if (!['webgl', 'canvas2d'].includes(expectedRenderer))
  throw new Error(`Unsupported --renderer '${expectedRenderer}'.`);
const sampleLimit = Number(options.limit ?? 10);
if (!Number.isInteger(sampleLimit) || sampleLimit < 1 || sampleLimit > 10)
  throw new Error('--limit must be an integer from 1 to 10.');
mkdirSync(outputDirectory, { recursive: true });

const browser = await chromium.launch({ headless: true });
const context = await browser.newContext({
  viewport: { width: 1440, height: 900 },
  deviceScaleFactor: 1,
});
if (expectedRenderer === 'canvas2d') {
  await context.addInitScript(() => {
    const original = HTMLCanvasElement.prototype.getContext;
    HTMLCanvasElement.prototype.getContext = function (kind, ...args) {
      if (['webgl', 'webgl2', 'experimental-webgl'].includes(kind)) return null;
      return original.call(this, kind, ...args);
    };
  });
}
const page = await context.newPage();
const failures = [];
page.on('pageerror', (error) => failures.push(`page: ${error.message}`));
page.on('requestfailed', (request) =>
  failures.push(`request: ${request.method()} ${request.url()} ${request.failure()?.errorText ?? ''}`),
);
page.on('console', (message) => {
  if (message.type() !== 'error') return;
  if (
    expectedRenderer === 'canvas2d' &&
    message.text().includes('Error creating WebGL context')
  ) return;
  failures.push(`console: ${message.text()}`);
});

const smoke = {
  schemaVersion: 1,
  url: baseUrl,
  outcomeVisible: true,
  expectedRenderer,
  index: null,
  samples: [],
};

try {
  await page.goto(baseUrl, { waitUntil: 'networkidle' });
  const links = await page.locator('ul a').evaluateAll((nodes) =>
    nodes.map((node) => ({
      href: node.getAttribute('href'),
      title: node.querySelector('strong')?.textContent?.trim() ?? '',
      subtitle: node.querySelector('span')?.textContent?.trim() ?? '',
      explanations: Object.fromEntries(
        [...node.querySelectorAll('dl > div')].map((row) => [
          row.querySelector('dt')?.textContent?.trim() ?? '',
          row.querySelector('dd')?.textContent?.trim() ?? '',
        ]),
      ),
    })),
  );
  if (links.length !== 10) throw new Error(`Expected 10 gallery cards, got ${links.length}.`);
  for (const link of links) {
    if (!link.href || !link.title || !link.subtitle)
      throw new Error('Every gallery card must have a link, title, and subtitle.');
    for (const phrase of ['wins', 'prepare t', 'commit t', 'success t', 'release t', 'baseline resumed'])
      if (!link.subtitle.includes(phrase))
        throw new Error(`${link.title}: subtitle is missing '${phrase}'.`);
    if (!link.explanations['Actual opponent']?.includes('stock mind'))
      throw new Error(`${link.title}: actual stock-mind opponent is not explained.`);
    if (!link.explanations['Final score']?.includes('Core deliveries'))
      throw new Error(`${link.title}: authoritative final score is not explained.`);
  }
  const indexScreenshot = path.join(outputDirectory, 'index.png');
  await page.screenshot({ path: indexScreenshot, fullPage: true });
  smoke.index = {
    cards: links,
    screenshot: relative(indexScreenshot),
    screenshotSha256: sha256(readFileSync(indexScreenshot)),
  };

  for (const [index, link] of links.slice(0, sampleLimit).entries()) {
    const beforeFailures = failures.length;
    const started = Date.now();
    await page.goto(new URL(link.href, baseUrl).href, { waitUntil: 'domcontentloaded' });
    await page.locator('[data-play-overlay="ready"]').waitFor({ timeout: 45_000 });
    const arena = page.getByRole('region', { name: 'Arena' });
    await arena.locator('canvas').waitFor({ timeout: 45_000 });
    const canvasKind = await arena.locator('canvas').evaluate((canvas) =>
      canvas.getContext('2d') ? '2d' : 'webgl',
    );
    const expectedCanvasKind = expectedRenderer === 'webgl' ? 'webgl' : '2d';
    if (canvasKind !== expectedCanvasKind)
      throw new Error(`${link.title}: expected ${expectedCanvasKind}, got ${canvasKind}.`);
    await page.locator('[aria-label="Arc Relay score"]').waitFor({ timeout: 15_000 });
    const tickBefore = await readTick(page);
    await page.getByRole('button', { name: 'Play match' }).click();
    await page.waitForTimeout(1_600);
    const tickAfter = await readTick(page);
    if (tickAfter <= tickBefore)
      throw new Error(`${link.title}: playback did not advance.`);
    await page.getByRole('button', { name: 'Pause' }).click();
    const operationTick = Number(link.subtitle.match(/success t(\d+)/)?.[1]);
    const releaseTick = Number(link.subtitle.match(/success t\d+→release t(\d+)/)?.[1]);
    if (!Number.isFinite(operationTick) || !Number.isFinite(releaseTick))
      throw new Error(`${link.title}: could not parse its operation proof window.`);
    await seekTick(page, operationTick);
    await page.getByRole('button', { name: /tactics/i }).click();
    let tactics = page.locator('section[aria-label="Tactics lens"]');
    await tactics.waitFor({ timeout: 5_000 });
    let activePlay = tactics.locator('button[aria-pressed]').first();
    await activePlay.waitFor({ timeout: 5_000 });
    await activePlay.click();
    await tactics.getByText('entrant execution trace', { exact: false }).waitFor({ timeout: 5_000 });
    if (operationTick < releaseTick && (await tactics.textContent())?.includes(`T${releaseTick}`))
      throw new Error(`${link.title}: tactics card exposed its future release tick.`);
    await tactics.getByRole('button', { name: 'Close tactics lens' }).click();
    if (index === 0) {
      const pickupTick = await page.evaluate(() => {
        const replayDocument = window.__BOTARENA_REPLAY__;
        const events = Array.isArray(replayDocument?.events) ? replayDocument.events : [];
        for (let tick = 0; tick < events.length; tick += 1) {
          for (const event of events[tick] ?? []) {
            const fact = event?.payload?.fact ?? event?.arcRelayFact;
            if (fact?.kind === 'core-picked-up') return tick;
          }
        }
        return null;
      });
      if (!Number.isInteger(pickupTick))
        throw new Error(`${link.title}: could not find a Core pickup beat.`);
      await seekTick(page, pickupTick);
      await page.getByRole('button', {
        name: new RegExp(`Seek to Core pickup at tick ${pickupTick}$`),
      }).waitFor({ timeout: 5_000 });
      if (await page.getByText('CORE PICKED UP', { exact: true }).count())
        throw new Error(`${link.title}: obsolete text event banner is still mounted.`);
      const possessionScreenshot = path.join(
        outputDirectory,
        `core-pickup-${expectedRenderer}.png`,
      );
      await arena.screenshot({ path: possessionScreenshot, animations: 'disabled' });

      const cameraToggle = page.getByRole('button', { name: /director/i });
      await cameraToggle.click();
      await page.waitForFunction(() =>
        [...document.querySelectorAll('button')].some((button) =>
          /overview/i.test(button.textContent ?? '') && button.getAttribute('aria-pressed') === 'false',
        ),
      );
      await page.waitForTimeout(1_200);
      const overviewScreenshot = path.join(
        outputDirectory,
        `three-theater-overview-${expectedRenderer}.png`,
      );
      await arena.screenshot({ path: overviewScreenshot, animations: 'disabled' });
      await page.getByRole('button', { name: /overview/i }).click();
      await page.waitForFunction(() =>
        [...document.querySelectorAll('button')].some((button) =>
          /director/i.test(button.textContent ?? '') && button.getAttribute('aria-pressed') === 'true',
        ),
      );

      const evidenceTick = Number(link.subtitle.match(/commit t(\d+)/)?.[1]);
      if (!Number.isFinite(evidenceTick))
        throw new Error(`${link.title}: could not parse its commitment tick.`);
      await seekTick(page, evidenceTick);
      await page.waitForTimeout(1_200);
      await page.getByRole('button', { name: /tactics/i }).click();
      tactics = page.locator('section[aria-label="Tactics lens"]');
      await tactics.waitFor({ timeout: 5_000 });
      activePlay = tactics.locator('button[aria-pressed]').first();
      await activePlay.waitFor({ timeout: 5_000 });
      await activePlay.click();
      await tactics.getByText('entrant execution trace', { exact: false }).waitFor({ timeout: 5_000 });
      smoke.possessionCue = {
        tick: pickupTick,
        presentation: 'diegetic Core pickup effect plus timeline anchor; no text banner',
        screenshot: relative(possessionScreenshot),
        screenshotSha256: sha256(readFileSync(possessionScreenshot)),
      };
      smoke.tacticsLens = {
        tick: evidenceTick,
        activeTrace: true,
        selectedTraceCard: true,
      };
      smoke.strategicOverview = {
        scope: 'all three Wells; deep home aprons may be cropped',
        screenshot: relative(overviewScreenshot),
        screenshotSha256: sha256(readFileSync(overviewScreenshot)),
      };
    }
    const screenshot = index === 0
      ? path.join(outputDirectory, `first-operation-${expectedRenderer}.png`)
      : null;
    if (screenshot) await arena.screenshot({ path: screenshot, animations: 'disabled' });
    const newFailures = failures.slice(beforeFailures);
    if (newFailures.length)
      throw new Error(`${link.title}: ${newFailures.join('\n')}`);
    smoke.samples.push({
      sample: link.href,
      title: link.title,
      subtitle: link.subtitle,
      readyMilliseconds: Date.now() - started,
      canvasKind,
      tickBefore,
      tickAfter,
      scoreBug: true,
      tacticsTrace: true,
      tacticsProofTick: operationTick,
      causalCard: true,
      pageErrors: [],
      consoleErrors: [],
      failedRequests: [],
      ...(screenshot
        ? {
            screenshot: relative(screenshot),
            screenshotSha256: sha256(readFileSync(screenshot)),
          }
        : {}),
    });
  }
} finally {
  await context.close();
  await browser.close();
}

const output = path.join(
  outputDirectory,
  expectedRenderer === 'webgl' ? 'smoke.json' : `smoke-${expectedRenderer}.json`,
);
writeFileSync(output, `${JSON.stringify(smoke, null, 2)}\n`);
console.log(
  `Smoked ${smoke.samples.length} labelled ${expectedRenderer} operation replays.`,
);

function parseOptions(args) {
  const parsed = {};
  for (let index = 0; index < args.length; index += 1) {
    const token = args[index];
    if (!token.startsWith('--')) throw new Error(`Unexpected argument '${token}'.`);
    const name = token.slice(2);
    const value = args[index + 1];
    if (!value || value.startsWith('--')) throw new Error(`Missing value for --${name}.`);
    parsed[name] = value;
    index += 1;
  }
  return parsed;
}

async function readTick(currentPage) {
  const value = await currentPage
    .getByRole('region', { name: 'Arena' })
    .locator('p')
    .filter({ hasText: /^\s*\d{3}\s*\/\s*\d{3}\s*$/ })
    .first()
    .textContent();
  const match = value?.match(/\d+/);
  if (!match) throw new Error(`Could not read arena tick from '${value}'.`);
  return Number(match[0]);
}

async function seekTick(currentPage, tick) {
  const timeline = currentPage.locator('[aria-label^="Match timeline"]');
  const thumb = timeline.locator('[role="slider"]');
  const maximum = Number(await thumb.getAttribute('aria-valuemax'));
  const bounds = await timeline.boundingBox();
  if (!bounds || !Number.isFinite(maximum))
    throw new Error(`Could not measure the timeline for tick ${tick}.`);
  await currentPage.mouse.click(
    bounds.x + bounds.width * (tick / maximum),
    bounds.y + bounds.height / 2,
  );
  await currentPage.waitForFunction(
    (expected) =>
      [...document.querySelectorAll('section[aria-label="Arena"] p')].some((node) =>
        new RegExp(`^\\s*0*${expected}\\s*\\/`).test(node.textContent ?? ''),
      ),
    tick,
  );
}

function sha256(value) {
  return createHash('sha256').update(value).digest('hex');
}

function relative(value) {
  return path.relative(repository, value);
}
