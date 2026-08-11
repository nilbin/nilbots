#!/usr/bin/env node
/**
 * Production-build Arc Relay product smoke against a real BotArena.App instance.
 *
 * Exercises the current product loop entirely through the UI: register -> author roster,
 * doctrine and map -> run -> watch -> return -> edit -> rerun -> compare tracks. Match
 * status is polled through the API only so the smoke does not wait for wall-clock broadcast.
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
  const expectedAnonymousSession = response.status() === 401
    && new URL(response.url()).pathname === '/api/accounts/me';
  if (response.status() >= 400 && !expectedAnonymousSession)
    browserErrors.push(`${response.status()} ${response.url()}`);
});

try {
  const suffix = Date.now().toString(36);
  const sheetName = `Coherence smoke ${suffix}`;
  await page.emulateMedia({ colorScheme: 'dark' });
  await page.goto(
    `${base}/login?mode=register&returnUrl=${encodeURIComponent('/entrants/sheets/new')}`,
    { waitUntil: 'networkidle' },
  );
  await page.getByLabel('Display name').fill(`Relay Smoke ${suffix}`);
  await page.getByLabel('Email').fill(`relay-smoke-${suffix}@example.test`);
  await page.getByLabel('Password').fill('correct-horse-battery-staple');
  await page.getByRole('button', { name: 'Create account', exact: true }).last().click();
  await page.waitForURL(`${base}/entrants/sheets/new`);
  await page.getByRole('heading', { name: 'Change. Run. Watch. Adjust.' }).waitFor();

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
  const runButton = page.locator('#trial-run').getByRole('button', { name: 'Practice & watch' });
  if (await runButton.isDisabled()) {
    const issues = await page.locator('[data-sheet-issue]').allTextContents();
    throw new Error(`Initial template is not runnable: ${issues.join(' | ')}`);
  }
  await page.screenshot({ path: join(output, 'sheet-workshop-desktop.png'), fullPage: true });

  // Author a real variant rather than merely running the starter unchanged. Body eight
  // becomes a Lantern, its assignment follows it, and the rename must rewrite every
  // doctrine/condition reference rather than exposing internal ids to the player.
  await page.getByRole('button', { name: '1 Roster', exact: true }).click();
  await page.locator('[aria-label="Fielded bodies"] button').nth(7).click();
  await page.locator('button').filter({ hasText: /^0\/2Lantern/ }).click();
  await page.locator('[data-assignment-select="5"]').click();
  const assignmentPanel = page.locator('[data-sheet-path="roles.5"]');
  await assignmentPanel.locator('button').filter({ hasText: /^Lantern1 fielded/ }).click();
  await assignmentPanel.locator('button').filter({ hasText: /^Palisade0 fielded/ }).click();
  const assignmentName = page.getByLabel('Assignment name');
  await assignmentName.fill('Scout');
  await assignmentName.press('Enter');
  await page.getByRole('heading', { name: 'Scout', exact: true }).waitFor();
  if (await assignmentPanel.locator('button').filter({ hasText: /^Lantern1 fielded · choice 1/ })
    .getAttribute('aria-pressed') !== 'true') {
    throw new Error('Roster chassis did not follow the renamed assignment.');
  }
  await page.evaluate(() => window.scrollTo(0, 0));
  await page.screenshot({ path: join(output, 'assignments-desktop.png'), fullPage: true });

  await page.getByRole('button', { name: '2 Battle plan', exact: true }).click();
  await page.locator('[data-doctrine-select="scout"]').click();
  const scoutDoctrine = page.locator('[data-sheet-path="doctrines.roles.scout"]');
  const scoutFallback = scoutDoctrine
    .locator('article[data-sheet-path^="doctrines.roles.scout.modes."]').last();
  await scoutFallback.locator(':scope > details > summary').click();
  await scoutFallback.getByRole('group', { name: 'Position in the push' })
    .getByRole('button', { name: 'Rear', exact: true }).click();

  await page.setViewportSize({ width: 390, height: 844 });
  const phoneOverflow = await page.evaluate(() =>
    document.documentElement.scrollWidth - document.documentElement.clientWidth);
  if (phoneOverflow > 1) throw new Error(`Sheet editor overflows phone viewport by ${phoneOverflow}px.`);
  await page.screenshot({ path: join(output, 'sheet-workshop-phone.png'), fullPage: true });
  await page.getByRole('button', { name: '1 Roster', exact: true }).click();
  await page.getByText('Selected assignment', { exact: true }).evaluate((element) =>
    element.scrollIntoView({ block: 'start' }));
  await page.evaluate(() => window.scrollBy(0, -64));
  await page.waitForTimeout(150);
  await page.screenshot({ path: join(output, 'assignments-phone.png') });
  await page.getByRole('button', { name: '2 Battle plan', exact: true }).click();
  await page.getByRole('heading', { name: 'Give every assignment its own playbook' }).evaluate((element) =>
    element.scrollIntoView({ block: 'start' }));
  await page.evaluate(() => window.scrollBy(0, -64));
  await page.waitForTimeout(150);
  await page.screenshot({ path: join(output, 'command-board-phone.png') });
  await page.getByRole('button', { name: /^Triggers/ }).click();
  await page.getByRole('heading', { name: 'Doctrine triggers' }).waitFor();
  await page.screenshot({ path: join(output, 'doctrine-conditions-phone.png') });
  await page.getByRole('button', { name: 'Done' }).click();
  await page.getByRole('button', { name: /^Core play/ }).click();
  await page.getByRole('heading', { name: 'Core custody' }).waitFor();
  await page.getByRole('group', { name: 'Ordinary pickup priority' })
    .getByRole('button', { name: 'Nearest Authorized', exact: true }).click();
  await page.screenshot({ path: join(output, 'doctrine-custody-phone.png') });
  await page.getByRole('button', { name: 'Done' }).click();

  await page.getByRole('button', { name: '3 Tactics map', exact: true }).click();
  await page.getByLabel('Map landmark legend').waitFor();
  await page.getByRole('button', { name: 'east side', exact: true }).click();
  await page.getByRole('button', { name: 'west side', exact: true }).click();
  const reflowWidth = page.getByLabel('local reflow width');
  await reflowWidth.fill('2');
  await page.screenshot({ path: join(output, 'tactics-map-phone.png'), fullPage: true });

  await runButton.scrollIntoViewIfNeeded();
  await page.waitForTimeout(150);
  const visiblePracticeActions = await visibleCount(
    page.getByRole('button', { name: /Practice/ }),
  );
  if (visiblePracticeActions !== 1)
    throw new Error(`Expected one visible Practice action, found ${visiblePracticeActions}.`);
  await page.screenshot({ path: join(output, 'practice-phone.png') });
  await page.setViewportSize({ width: 1440, height: 1050 });

  const first = await launchPractice(page, context, base, runButton);

  await page.reload({ waitUntil: 'networkidle' });
  await watchMatch(page);
  await page.screenshot({ path: join(output, 'trial-replay-desktop.png'), fullPage: true });

  await page.getByRole('link', { name: `← ${sheetName}`, exact: true }).click();
  await page.getByRole('heading', { name: 'Change. Run. Watch. Adjust.' }).waitFor();
  if (await page.getByLabel('Sheet name').inputValue() !== sheetName) {
    throw new Error('Returning from the replay lost the active draft.');
  }
  await page.getByRole('button', { name: 'Plot tracks' }).first().click();
  await page.locator('[data-unit-track]').first().waitFor({ timeout: 15_000 });
  const firstTrackCount = await page.locator('[data-unit-track]').count();
  await page.screenshot({ path: join(output, 'trial-tracks-desktop.png'), fullPage: true });

  await page.setViewportSize({ width: 390, height: 844 });
  const returnOverflow = await page.evaluate(() =>
    document.documentElement.scrollWidth - document.documentElement.clientWidth);
  if (returnOverflow > 1) throw new Error(`Returned editor overflows phone viewport by ${returnOverflow}px.`);
  await page.screenshot({ path: join(output, 'trial-tracks-phone.png'), fullPage: true });

  // Close the loop: change the authored map, rerun with one action, watch again and
  // return to two comparable run cards. This catches draft loss and revision conflicts.
  await page.getByLabel('local reflow width').fill('1');
  await page.setViewportSize({ width: 1440, height: 1050 });
  const rerunButton = page.locator('#trial-run')
    .getByRole('button', { name: 'Practice again & watch' });
  const second = await launchPractice(page, context, base, rerunButton);
  await page.reload({ waitUntil: 'networkidle' });
  await watchMatch(page);
  await page.screenshot({ path: join(output, 'trial-rerun-desktop.png'), fullPage: true });
  await page.getByRole('link', { name: `← ${sheetName}`, exact: true }).click();
  await page.getByRole('heading', { name: 'Change. Run. Watch. Adjust.' }).waitFor();
  if (await page.getByRole('button', { name: 'Plot tracks' }).count() !== 2)
    throw new Error('The returned editor did not retain both practice runs.');
  await page.getByRole('button', { name: 'Plot tracks' }).first().click();
  await page.locator('[data-unit-track]').first().waitFor({ timeout: 15_000 });
  const secondTrackCount = await page.locator('[data-unit-track]').count();
  await page.getByRole('button', { name: 'Save r3' }).waitFor();
  await page.screenshot({ path: join(output, 'trial-rerun-tracks-desktop.png'), fullPage: true });

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
    sheetName,
    matchIds: [first.created.id, second.created.id],
    revisions: [1, 2],
    statuses: [first.status, second.status],
    recordedTrackCounts: [firstTrackCount, secondTrackCount],
    replayFormatVersion: 'database-verified-4; withheld by the live public projection',
    broadcastVersions: [first.replay.broadcastVersion, second.replay.broadcastVersion],
    visibleTicks: [first.replay.worlds.length, second.replay.worlds.length],
    output,
  }, null, 2));
} finally {
  await browser.close();
}

async function launchPractice(page, context, base, button) {
  const matchResponse = page.waitForResponse((response) =>
    /\/api\/sheets\/[0-9a-f-]+\/trial$/i.test(response.url())
      && response.request().method() === 'POST');
  await button.click();
  const createdResponse = await matchResponse;
  if (!createdResponse.ok())
    throw new Error(`Match admission failed: ${createdResponse.status()} ${await createdResponse.text()}`);
  const created = await createdResponse.json();
  if (typeof created.id !== 'string') throw new Error('Match response has no id.');
  await page.waitForURL(`${base}/matches/${created.id}`);

  let status = '';
  for (let attempt = 0; attempt < 120; attempt += 1) {
    const detailResponse = await context.request.get(`${base}/api/matches/${created.id}`);
    const detail = await detailResponse.json();
    status = detail.status;
    if (status === 'Completed') break;
    if (status === 'Failed') throw new Error(`Practice match ${created.id} failed.`);
    await page.waitForTimeout(500);
  }
  if (status !== 'Completed')
    throw new Error(`Match did not complete; last status ${status}.`);

  let replay;
  for (let attempt = 0; attempt < 24; attempt += 1) {
    const replayResponse = await context.request.get(`${base}/api/matches/${created.id}/replay`);
    if (!replayResponse.ok())
      throw new Error(`Replay fetch failed: ${replayResponse.status()}`);
    replay = await replayResponse.json();
    if (replay.worlds?.length > 0) break;
    await page.waitForTimeout(250);
  }
  if (replay?.header?.replayVersion !== 3
      || replay.broadcastVersion !== 2
      || replay.partial !== true
      || replay.worlds?.length < 1) {
    throw new Error('Hosted replay did not arrive as a causal compact broadcast-v2 prefix.');
  }
  return { created, status, replay };
}

async function watchMatch(page) {
  await page.locator('[aria-label="Arena"]').waitFor({ timeout: 15_000 });
  await page.getByText(/Loading arena/).waitFor({ state: 'hidden', timeout: 60_000 });
  const overlay = page.locator('[data-play-overlay]');
  if (await overlay.count() > 0) {
    await page.getByRole('button', { name: 'Play match' }).click();
    await overlay.waitFor({ state: 'detached' });
  }
  // A causal live prefix has no gate and is already following the broadcast. In either
  // mode, leave enough time to observe actual movement rather than only a loaded frame.
  await page.waitForTimeout(900);
}

async function visibleCount(locator) {
  const candidates = await locator.all();
  let count = 0;
  for (const candidate of candidates) {
    if (await candidate.isVisible()) count += 1;
  }
  return count;
}
