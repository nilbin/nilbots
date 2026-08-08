#!/usr/bin/env node
/**
 * Production-build Arc Relay product smoke against a real BotArena.App instance.
 *
 * Exercises the current product loop: Season -> entrant overview -> edit -> run ->
 * watch -> return -> plot recorded tracks. Captures phone and desktop evidence.
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
  if (message.type() === 'error'
      && !message.text().startsWith('Failed to load resource:')) {
    browserErrors.push(message.text());
  }
});
page.on('response', (response) => {
  if (response.status() >= 400)
    browserErrors.push(`${response.status()} ${response.url()}`);
});

try {
  const suffix = Date.now().toString(36);
  const sheetName = `Coherence smoke ${suffix}`;
  const registration = await context.request.post(`${base}/api/accounts/register`, {
    data: {
      displayName: `Relay Smoke ${suffix}`,
      email: `relay-smoke-${suffix}@example.test`,
      password: 'correct-horse-battery-staple',
    },
  });
  if (!registration.ok()) throw new Error(`Registration failed: ${registration.status()}`);

  await page.emulateMedia({ colorScheme: 'dark' });
  await page.goto(`${base}/`, { waitUntil: 'networkidle' });
  await page.getByRole('heading', { name: 'Season 1' }).waitFor();
  await page.screenshot({ path: join(output, 'season-desktop.png'), fullPage: true });
  await page.setViewportSize({ width: 390, height: 844 });
  const seasonPhoneOverflow = await page.evaluate(() =>
    document.documentElement.scrollWidth - document.documentElement.clientWidth);
  if (seasonPhoneOverflow > 1) throw new Error(`Season overflows phone viewport by ${seasonPhoneOverflow}px.`);
  await page.screenshot({ path: join(output, 'season-phone.png'), fullPage: true });
  await page.setViewportSize({ width: 1440, height: 1050 });

  await page.goto(`${base}/entrants`, { waitUntil: 'networkidle' });
  await page.getByRole('heading', { name: 'Build and manage entrants.' }).waitFor();
  await page.screenshot({ path: join(output, 'entrants-desktop.png'), fullPage: true });
  await page.setViewportSize({ width: 390, height: 844 });
  const overviewPhoneOverflow = await page.evaluate(() =>
    document.documentElement.scrollWidth - document.documentElement.clientWidth);
  if (overviewPhoneOverflow > 1) throw new Error(`Entrants overflows phone viewport by ${overviewPhoneOverflow}px.`);
  await page.screenshot({ path: join(output, 'entrants-phone.png'), fullPage: true });
  await page.setViewportSize({ width: 1440, height: 1050 });

  await page.goto(`${base}/entrants/sheets/new`, { waitUntil: 'networkidle' });
  await page.getByRole('heading', { name: 'Change. Run. Watch. Adjust.' }).waitFor();
  const nameField = page.getByLabel('Sheet name');
  await nameField.fill(sheetName);
  const runButton = page.locator('#trial-run').getByRole('button', { name: 'Run & watch' });
  if (await runButton.isDisabled()) {
    const issues = await page.locator('[data-sheet-issue]').allTextContents();
    throw new Error(`Initial template is not runnable: ${issues.join(' | ')}`);
  }
  await page.screenshot({ path: join(output, 'sheet-workshop-desktop.png'), fullPage: true });

  await page.setViewportSize({ width: 390, height: 844 });
  const phoneOverflow = await page.evaluate(() =>
    document.documentElement.scrollWidth - document.documentElement.clientWidth);
  if (phoneOverflow > 1) throw new Error(`Sheet editor overflows phone viewport by ${phoneOverflow}px.`);
  await page.screenshot({ path: join(output, 'sheet-workshop-phone.png'), fullPage: true });
  await page.setViewportSize({ width: 1440, height: 1050 });

  const matchResponse = page.waitForResponse((response) =>
    /\/api\/sheets\/[0-9a-f-]+\/trial$/i.test(response.url())
      && response.request().method() === 'POST');
  await runButton.click();
  const createdResponse = await matchResponse;
  if (!createdResponse.ok()) throw new Error(`Match admission failed: ${createdResponse.status()}`);
  const created = await createdResponse.json();
  if (typeof created.id !== 'string') throw new Error('Match response has no id.');
  await page.waitForURL(`${base}/matches/${created.id}`);

  let status = '';
  for (let attempt = 0; attempt < 120; attempt += 1) {
    const detailResponse = await context.request.get(`${base}/api/matches/${created.id}`);
    const detail = await detailResponse.json();
    status = detail.status;
    if (status === 'Completed') break;
    await page.waitForTimeout(500);
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
  await page.screenshot({ path: join(output, 'trial-replay-desktop.png'), fullPage: true });

  await page.getByRole('link', { name: `← ${sheetName}`, exact: true }).click();
  await page.getByRole('heading', { name: 'Change. Run. Watch. Adjust.' }).waitFor();
  if (await page.getByLabel('Sheet name').inputValue() !== sheetName) {
    throw new Error('Returning from the replay lost the active draft.');
  }
  await page.getByRole('button', { name: 'Plot tracks' }).first().click();
  await page.locator('[data-unit-track]').first().waitFor({ timeout: 15_000 });
  const trackCount = await page.locator('[data-unit-track]').count();
  await page.screenshot({ path: join(output, 'trial-tracks-desktop.png'), fullPage: true });

  await page.setViewportSize({ width: 390, height: 844 });
  const returnOverflow = await page.evaluate(() =>
    document.documentElement.scrollWidth - document.documentElement.clientWidth);
  if (returnOverflow > 1) throw new Error(`Returned editor overflows phone viewport by ${returnOverflow}px.`);
  await page.screenshot({ path: join(output, 'trial-tracks-phone.png'), fullPage: true });

  await page.goto(`${base}/`, { waitUntil: 'networkidle' });
  await page.getByText(sheetName, { exact: true }).first().waitFor();
  await page.screenshot({ path: join(output, 'season-with-entrant-phone.png'), fullPage: true });
  await page.setViewportSize({ width: 1440, height: 1050 });
  await page.screenshot({ path: join(output, 'season-with-entrant-desktop.png'), fullPage: true });

  await page.emulateMedia({ colorScheme: 'light' });
  await page.goto(`${base}/docs`, { waitUntil: 'networkidle' });
  await page.getByRole('heading', { name: 'Arc Relay field guide' }).waitFor();
  const docsPhoneOverflow = await page.evaluate(() =>
    document.documentElement.scrollWidth - document.documentElement.clientWidth);
  if (docsPhoneOverflow > 1) throw new Error(`Docs overflow phone viewport by ${docsPhoneOverflow}px.`);
  await page.screenshot({ path: join(output, 'docs-phone-light.png'), fullPage: true });
  await page.setViewportSize({ width: 1440, height: 1050 });
  await page.screenshot({ path: join(output, 'docs-desktop-light.png'), fullPage: true });

  if (browserErrors.length > 0) {
    throw new Error(`Browser errors:\n${[...new Set(browserErrors)].join('\n')}`);
  }

  console.log(JSON.stringify({
    matchId: created.id,
    status,
    recordedTrackCount: trackCount,
    replayFormatVersion: 'database-verified-4; withheld by the live public projection',
    broadcastVersion: replay.broadcastVersion,
    visibleTicks: replay.worlds.length,
    output,
  }, null, 2));
} finally {
  await browser.close();
}
