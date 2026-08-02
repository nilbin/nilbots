#!/usr/bin/env node
/**
 * Production-build Arc Relay smoke against a real BotArena.App instance.
 *
 * BASE=http://127.0.0.1:8093 \
 * OUT=/tmp/nilbots-arc-relay-smoke \
 * node scripts/smoke-arc-relay-product.mjs
 */
import { createRequire } from 'node:module';
import { mkdir } from 'node:fs/promises';
import { join } from 'node:path';

const requireFromWeb = createRequire(new URL('../web/package.json', import.meta.url));
const { chromium } = requireFromWeb('playwright');
const base = process.env.BASE ?? 'http://127.0.0.1:8093';
const output = process.env.OUT ?? '/tmp/nilbots-arc-relay-product-smoke';
await mkdir(output, { recursive: true });

const browser = await chromium.launch({ headless: true });
const context = await browser.newContext({ viewport: { width: 1440, height: 1050 } });
const page = await context.newPage();
const browserErrors = [];
page.on('pageerror', (error) => browserErrors.push(error.message));
page.on('console', (message) => {
  if (message.type() === 'error') browserErrors.push(message.text());
});

try {
  const suffix = Date.now().toString(36);
  const registration = await context.request.post(`${base}/api/accounts/register`, {
    data: {
      displayName: `Relay Smoke ${suffix}`,
      email: `relay-smoke-${suffix}@example.test`,
      password: 'correct-horse-battery-staple',
    },
  });
  if (!registration.ok()) throw new Error(`Registration failed: ${registration.status()}`);

  await page.goto(`${base}/relay`, { waitUntil: 'networkidle' });
  await page.getByRole('heading', { name: 'Eight bodies. One drawn plan.' }).waitFor();
  const saveResponse = page.waitForResponse((response) =>
    response.url() === `${base}/api/arc-relay/sheets`
      && response.request().method() === 'POST');
  await page.getByRole('button', { name: 'Save sheet', exact: true }).click();
  if (!(await saveResponse).ok()) throw new Error('First sheet save failed.');

  const copyResponse = page.waitForResponse((response) =>
    response.url() === `${base}/api/arc-relay/sheets`
      && response.request().method() === 'POST');
  await page.getByRole('button', { name: 'Save a copy', exact: true }).click();
  if (!(await copyResponse).ok()) throw new Error('Second sheet save failed.');
  await page.screenshot({ path: join(output, 'sheet-workshop.png'), fullPage: true });

  const matchResponse = page.waitForResponse((response) =>
    response.url() === `${base}/api/arc-relay/matches`
      && response.request().method() === 'POST');
  await page.getByRole('button', { name: 'Run match', exact: true }).click();
  const createdResponse = await matchResponse;
  if (!createdResponse.ok()) throw new Error(`Match admission failed: ${createdResponse.status()}`);
  const created = await createdResponse.json();
  if (typeof created.id !== 'string') throw new Error('Match response has no id.');
  await page.waitForURL(`${base}/matches/${created.id}`);

  let status = '';
  let matchDetail;
  for (let attempt = 0; attempt < 40; attempt += 1) {
    const detailResponse = await context.request.get(`${base}/api/matches/${created.id}`);
    matchDetail = await detailResponse.json();
    status = matchDetail.status;
    if (status === 'Completed') break;
    await page.waitForTimeout(250);
  }
  if (status !== 'Completed') throw new Error(`Match did not complete; last status ${status}.`);

  let replay;
  for (let attempt = 0; attempt < 24; attempt += 1) {
    const replayResponse = await context.request.get(`${base}/api/matches/${created.id}/replay`);
    if (!replayResponse.ok()) throw new Error(`Replay fetch failed: ${replayResponse.status()}`);
    replay = await replayResponse.json();
    if (replay.worlds?.length > 0) break;
    await page.waitForTimeout(250);
  }
  if (replay.header?.replayVersion !== 3
      || replay.broadcastVersion !== 2
      || replay.partial !== true
      || replay.worlds?.length < 1) {
    throw new Error('Hosted replay did not arrive as a causal compact broadcast-v2 prefix.');
  }

  await page.reload({ waitUntil: 'networkidle' });
  await page.locator('[aria-label="Arena"]').waitFor({ timeout: 15_000 });
  await page.screenshot({ path: join(output, 'hosted-match.png'), fullPage: true });
  if (browserErrors.length > 0) {
    throw new Error(`Browser errors:\n${[...new Set(browserErrors)].join('\n')}`);
  }

  console.log(JSON.stringify({
    matchId: created.id,
    status,
    replayFormatVersion: 'database-verified-4; withheld by the live public projection',
    broadcastVersion: replay.broadcastVersion,
    visibleTicks: replay.worlds.length,
    screenshots: {
      workshop: join(output, 'sheet-workshop.png'),
      match: join(output, 'hosted-match.png'),
    },
  }, null, 2));
} finally {
  await browser.close();
}
