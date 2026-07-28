#!/usr/bin/env node
/**
 * Capture every design-review page against the typed site-review server.
 *
 * Usage, from web/:
 *   npm run site-shots
 *   BASE=http://127.0.0.1:4273 SHOTS=./shots npm run site-shots
 *
 * Start `npm run site-review` separately. Requiring its health marker prevents this tool
 * from pointing at production and mistaking real API data for the controlled review state.
 */

import { mkdir, rm } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { dirname, join, resolve } from 'node:path';
import { createRequire } from 'node:module';
import { fileURLToPath } from 'node:url';

const scriptDirectory = dirname(fileURLToPath(import.meta.url));
const requireFromWeb = createRequire(
  join(scriptDirectory, '..', 'web', 'package.json'),
);
const { chromium } = requireFromWeb('playwright');

const base = (process.env.BASE ?? 'http://127.0.0.1:4181').replace(/\/$/, '');
const shots = resolve(
  process.env.SHOTS ?? join(tmpdir(), 'nilbots-site-shots'),
);
const healthUrl = `${base}/__site-review/health`;
const pages = [
  ['rankings', '/'],
  [
    'arena-global',
    '/',
    async (page) => {
      await page.getByRole('button', { name: 'Play', exact: true }).click();
      await page.getByLabel('Ranked set allowance').waitFor();
    },
  ],
  ['bots', '/bots'],
  ['watch', '/watch'],
  [
    'watch-filtered',
    '/watch?bot=pincer-gen-10&map=arena-01&ranked=true',
  ],
  ['bot', '/bots/pincer-gen-10'],
  [
    'arena-ranked',
    '/bots/pincer-gen-10',
    async (page) => {
      await page
        .getByRole('button', { name: 'Ranked set', exact: true })
        .click();
      await page.getByLabel('Ranked set allowance').waitFor();
    },
  ],
  [
    'arena-ranked-capped',
    '/bots/pincer-gen-10?review=ranked-capped',
    async (page) => {
      await page
        .getByRole('button', { name: 'Ranked set', exact: true })
        .click();
      await page
        .getByText('Daily allowance used.', { exact: false })
        .waitFor();
    },
  ],
  [
    'arena-challenge',
    '/bots/warden-gen-1',
    async (page) => {
      await page
        .getByRole('button', { name: 'Challenge', exact: true })
        .click();
      await page.getByLabel('Challenge allowance').waitFor();
    },
  ],
  ['match-completed', '/matches/30000000-0000-4000-8000-000000000001'],
  ['match-live', '/matches/30000000-0000-4000-8000-000000000002'],
  ['match-failed', '/matches/30000000-0000-4000-8000-000000000003'],
  ['ranked-set', '/sets/40000000-0000-4000-8000-000000000001'],
  ['firstrun', '/garage?review=first-run'],
  ['bot-appearance', '/bots/pincer-gen-10/appearance'],
  ['shop', '/store'],
  ['docs', '/docs'],
  ['not-found', '/not-a-real-route'],
  ['login', '/login'],
  [
    'register',
    '/login',
    (page) => page.getByRole('button', { name: 'Create account' }).click(),
  ],
];
const widths = [
  ['wide', 1180],
  ['narrow', 390],
];

await assertSiteReviewServer();
await mkdir(shots, { recursive: true });

let browser;
try {
  browser = await chromium.launch();
} catch (error) {
  throw new Error(
    'Chromium is unavailable. Install the pinned review browser with "npm run site-shots:install".',
    { cause: error },
  );
}
const failedShots = [];
let linkedRouteFailures = [];

try {
  for (const [name, path, prepare] of pages) {
    for (const [label, width] of widths) {
      const shot = `${name}-${label}`;
      const context = await browser.newContext({
        viewport: { width, height: 900 },
        deviceScaleFactor: 2,
        locale: 'en-GB',
        timezoneId: 'Europe/Stockholm',
      });
      const page = await context.newPage();
      await page.clock.setFixedTime(new Date('2026-07-28T10:00:00Z'));
      const failures = [];
      const screenshotPath = join(shots, `${shot}.png`);

      // A failed recapture must not leave yesterday's image looking current.
      await rm(screenshotPath, { force: true });

      page.on('pageerror', (error) => {
        failures.push(`page error: ${firstLine(error)}`);
      });
      page.on('console', (message) => {
        const expectedAnonymousResponse =
          path.startsWith('/login') &&
          /status (code )?of 401|status of 401|status.*401/i.test(
            message.text(),
          );
        if (message.type() === 'error' && !expectedAnonymousResponse) {
          failures.push(`console error: ${firstLine(message.text())}`);
        }
      });
      page.on('requestfailed', (request) => {
        if (isReviewServiceUrl(request.url())) {
          failures.push(
            `review request failed: ${request.method()} ${request.url()} (${request.failure()?.errorText ?? 'unknown error'})`,
          );
        }
      });
      page.on('response', (response) => {
        if (!isReviewServiceUrl(response.url())) return;
        if (
          isApiUrl(response.url()) &&
          response.headers()['x-nilbots-site-review'] !== '1'
        ) {
          failures.push(`API escaped the review server: ${response.url()}`);
        }
        if (!response.ok()) {
          const expected =
            response.headers()['x-nilbots-site-review-expected-status'] ===
            String(response.status());
          if (!expected) {
            failures.push(
              `review response ${response.status()}: ${response.request().method()} ${response.url()}`,
            );
          }
        }
      });

      try {
        await page.goto(`${base}${path}`, {
          waitUntil: 'networkidle',
          timeout: 20_000,
        });
        if (prepare) await prepare(page);
        await page.waitForTimeout(500);

        const result = await page.evaluate(() => {
          const main = document.querySelector('main');
          const text = (main?.innerText ?? '').trim();
          return {
            textLength: text.length,
            hasVisibleMain:
              main instanceof HTMLElement &&
              main.getBoundingClientRect().height > 0 &&
              getComputedStyle(main).visibility !== 'hidden',
            overflow:
              document.documentElement.scrollWidth -
              document.documentElement.clientWidth,
          };
        });

        if (!result.hasVisibleMain || result.textLength < 40) {
          failures.push(
            `blank main content (${result.textLength} visible text characters)`,
          );
        }
        if (result.overflow > 0) {
          failures.push(`horizontal overflow: ${result.overflow}px`);
        }

        await page.screenshot({
          path: screenshotPath,
          fullPage: true,
        });

        const state = failures.length === 0 ? 'ok' : 'FAILED';
        console.log(
          `${shot.padEnd(18)} ${state} · ${result.textLength} chars · overflow ${result.overflow}px`,
        );
      } catch (error) {
        failures.push(`capture failed: ${firstLine(error)}`);
        console.log(`${shot.padEnd(18)} FAILED`);
      } finally {
        await context.close();
      }

      if (failures.length > 0) {
        failedShots.push({ shot, failures: [...new Set(failures)] });
      }
    }
  }
  linkedRouteFailures = await auditLinkedReviewRoutes(browser);
} finally {
  await browser.close();
}

if (failedShots.length > 0 || linkedRouteFailures.length > 0) {
  console.error('\nSite review failed:');
  for (const { shot, failures } of failedShots) {
    console.error(`  ${shot}`);
    for (const failure of failures) console.error(`    - ${failure}`);
  }
  if (linkedRouteFailures.length > 0) {
    console.error('  linked route audit');
    for (const failure of linkedRouteFailures) {
      console.error(`    - ${failure}`);
    }
  }
  process.exitCode = 1;
} else {
  console.log(
    `\nScreenshots written to ${shots}; rendered internal links are closed.`,
  );
}

async function auditLinkedReviewRoutes(browser) {
  const origin = new URL(base).origin;
  const queued = ['/', '/login', '/garage?review=first-run'];
  const seen = new Set();
  const failures = [];
  const context = await browser.newContext({
    viewport: { width: 390, height: 900 },
    locale: 'en-GB',
    timezoneId: 'Europe/Stockholm',
  });
  const page = await context.newPage();
  await page.clock.setFixedTime(new Date('2026-07-28T10:00:00Z'));
  let currentPath = queued[0];

  page.on('pageerror', (error) => {
    failures.push(`${currentPath}: page error: ${firstLine(error)}`);
  });
  page.on('console', (message) => {
    const expectedAnonymousResponse =
      /status (code )?of 401|status of 401|status.*401/i.test(message.text());
    if (message.type() === 'error' && !expectedAnonymousResponse) {
      failures.push(
        `${currentPath}: console error: ${firstLine(message.text())}`,
      );
    }
  });
  page.on('requestfailed', (request) => {
    if (isReviewServiceUrl(request.url())) {
      failures.push(
        `${currentPath}: request failed: ${request.method()} ${request.url()} (${request.failure()?.errorText ?? 'unknown error'})`,
      );
    }
  });
  page.on('response', (response) => {
    if (!isReviewServiceUrl(response.url())) return;
    if (
      isApiUrl(response.url()) &&
      response.headers()['x-nilbots-site-review'] !== '1'
    ) {
      failures.push(
        `${currentPath}: API escaped the review server: ${response.url()}`,
      );
    }
    if (response.status() < 400) return;
    const expected =
      response.headers()['x-nilbots-site-review-expected-status'] ===
      String(response.status());
    if (!expected) {
      failures.push(
        `${currentPath}: response ${response.status()}: ${response.request().method()} ${response.url()}`,
      );
    }
  });

  try {
    while (queued.length > 0) {
      if (seen.size >= 100) {
        failures.push('link crawl exceeded its 100-route safety limit');
        break;
      }
      currentPath = queued.shift();
      if (seen.has(currentPath)) continue;
      seen.add(currentPath);

      try {
        await page.goto(`${base}${currentPath}`, {
          waitUntil: 'networkidle',
          timeout: 20_000,
        });
        const hrefs = await page.locator('a[href]').evaluateAll((anchors) =>
          anchors.map((anchor) => anchor.href),
        );
        for (const href of hrefs) {
          const target = new URL(href);
          if (
            target.origin !== origin ||
            (target.protocol !== 'http:' && target.protocol !== 'https:')
          ) {
            continue;
          }
          target.hash = '';
          const path = `${target.pathname}${target.search}`;
          if (!seen.has(path) && !queued.includes(path)) queued.push(path);
        }
      } catch (error) {
        failures.push(`${currentPath}: navigation failed: ${firstLine(error)}`);
      }
    }
  } finally {
    await context.close();
  }

  const uniqueFailures = [...new Set(failures)];
  const state = uniqueFailures.length === 0 ? 'ok' : 'FAILED';
  console.log(
    `linked-route-audit ${state} · ${seen.size} rendered internal routes`,
  );
  return uniqueFailures;
}

async function assertSiteReviewServer() {
  let response;
  try {
    response = await fetch(healthUrl, { signal: AbortSignal.timeout(5_000) });
  } catch (error) {
    throw new Error(
      `Site-review server is not reachable at ${base}. Start it with "npm run site-review".`,
      { cause: error },
    );
  }

  const body = await response.json().catch(() => null);
  if (
    !response.ok ||
    response.headers.get('x-nilbots-site-review') !== '1' ||
    body?.ready !== true
  ) {
    throw new Error(
      `${base} is not the typed site-review server. Refusing to capture it.`,
    );
  }
}

function isApiUrl(value) {
  return new URL(value).pathname.startsWith('/api/');
}

function isReviewServiceUrl(value) {
  const path = new URL(value).pathname;
  return path.startsWith('/api/') || path.startsWith('/hubs/');
}

function firstLine(value) {
  return String(value).split('\n')[0];
}
