#!/usr/bin/env node
/** Production-asset browser smoke for the hosted Arc Relay entrant lane. */
import { createRequire } from 'node:module';
import { mkdir } from 'node:fs/promises';
import { join, resolve } from 'node:path';
import { spawnSync } from 'node:child_process';

const requireFromWeb = createRequire(new URL('../web/package.json', import.meta.url));
const { chromium } = requireFromWeb('playwright');
const base = (process.env.BASE ?? 'http://127.0.0.1:8093').replace(/\/$/, '');
const output = resolve(process.env.OUT ?? 'docs/reports/assets/entrant-ladder-pass');
await mkdir(output, { recursive: true });

const browser = await chromium.launch({ headless: true });
const alpha = await browser.newContext({ viewport: { width: 1440, height: 1050 } });
const beta = await browser.newContext({ viewport: { width: 1440, height: 1050 } });
const alphaPage = await alpha.newPage();
const betaPage = await beta.newPage();
const errors = [];
for (const page of [alphaPage, betaPage]) {
  page.on('pageerror', (error) => errors.push(error.message));
  page.on('console', (message) => {
    if (message.type() === 'error') errors.push(message.text());
  });
}

try {
  const suffix = Date.now().toString(36);
  await register(alpha, `Northbank ${suffix}`, `north-${suffix}@example.test`);
  await register(beta, `Southwire ${suffix}`, `south-${suffix}@example.test`);
  const catalog = await okJson(await alpha.request.get(`${base}/api/arc-relay/catalog`));
  const classIds = [...catalog.newSheetTemplate.slots]
    .sort((a, b) => a.unitId - b.unitId).map((slot) => slot.classId);

  const sheetName = `Northbank Draw ${suffix}`;
  const sheet = await okJson(await alpha.request.post(`${base}/api/arc-relay/sheets`, {
    data: { name: sheetName, expectedRevision: null, document: catalog.newSheetTemplate },
  }));
  const crestOptions = await okJson(await alpha.request.get(
    `${base}/api/arc-relay/entrants/${sheet.id}/crest-options`));
  await okJson(await alpha.request.put(`${base}/api/arc-relay/entrants/${sheet.id}/crest`, {
    data: { variant: crestOptions.options[3].variant },
  }));

  const mindName = `Southwire Mind ${suffix}`;
  const mind = await okJson(await beta.request.post(`${base}/api/arc-relay/minds`, {
    data: {
      name: mindName,
      entryType: 'SmokeRelayMind',
      files: [{ name: 'SmokeRelayMind.cs', content: mindSource() }],
      composition: { classIds, adaptivePolicyId: null, adaptiveClassIds: [] },
      crestVariant: 37,
    },
  }));
  await poll(async () => {
    const entrants = await okJson(await beta.request.get(`${base}/api/arc-relay/entrants`));
    const entrant = entrants.find((value) => value.id === mind.entrant.id);
    if (entrant?.status === 'failed') throw new Error('Custom mind build failed.');
    return entrant?.status === 'required' ? entrant : null;
  }, 90_000, 'custom mind build');

  const preflight = await okJson(await beta.request.post(
    `${base}/api/arc-relay/entrants/${mind.entrant.id}/preflight`, { data: {} }));
  await poll(async () => {
    const entrants = await okJson(await beta.request.get(`${base}/api/arc-relay/entrants`));
    const entrant = entrants.find((value) => value.id === mind.entrant.id);
    if (entrant?.status === 'failed') throw new Error('Custom mind preflight failed.');
    return entrant?.status === 'passed' ? entrant : null;
  }, 90_000, 'custom mind preflight');

  await okJson(await alpha.request.put(`${base}/api/arc-relay/entrants/${sheet.id}/ladder`, {
    data: { optedIn: true },
  }));
  await okJson(await beta.request.put(`${base}/api/arc-relay/entrants/${mind.entrant.id}/ladder`, {
    data: { optedIn: true },
  }));

  const ranked = await poll(async () => {
    const matches = await okJson(await alpha.request.get(`${base}/api/matches/?take=100`));
    return matches.find((match) => match.id !== preflight.matchId
      && match.participants.some((participant) => participant.nameSnapshot === sheetName)
      && match.participants.some((participant) => participant.nameSnapshot === mindName)) ?? null;
  }, 70_000, 'passive ladder pairing');
  await poll(async () => {
    const detail = await okJson(await alpha.request.get(`${base}/api/matches/${ranked.id}`));
    if (detail.status === 'Failed') throw new Error('Ranked entrant match failed.');
    return detail.status === 'Completed' ? detail : null;
  }, 90_000, 'ranked match execution');

  // Dedicated smoke database only: move the presentation clock beyond the causal
  // horizon and release its already-scheduled settlement job. Canonical replay bytes
  // and result facts are untouched.
  advanceDedicatedSmokeClock(ranked.id);
  const settled = await poll(async () => {
    const ladder = await okJson(await alpha.request.get(`${base}/api/arc-relay/ladder`));
    const pair = ladder.entrants.filter((entrant) =>
      entrant.id === sheet.id || entrant.id === mind.entrant.id);
    return pair.length === 2 && pair.every((entrant) => entrant.rankedMatches === 1)
      ? { ...ladder, entrants: pair } : null;
  }, 30_000, 'rating settlement');

  await alphaPage.goto(`${base}/relay`, { waitUntil: 'networkidle' });
  const primary = await alphaPage.locator('nav[aria-label="Primary navigation"]').first().innerText();
  if (/\bbots?\b|garage/i.test(primary)) throw new Error(`Legacy navigation leaked: ${primary}`);
  const sheetCard = alphaPage.locator('article').filter({ hasText: sheetName }).first();
  await sheetCard.waitFor();
  await sheetCard.screenshot({ path: join(output, 'sheet-card.png') });
  const ladderPanel = alphaPage.locator('section').filter({ hasText: 'Ranked Arc Relay' }).last();
  await ladderPanel.screenshot({ path: join(output, 'ladder.png') });

  await betaPage.goto(`${base}/relay`, { waitUntil: 'networkidle' });
  const mindCard = betaPage.locator('article').filter({ hasText: mindName }).first();
  await mindCard.waitFor();
  await mindCard.screenshot({ path: join(output, 'mind-card.png') });

  await alphaPage.goto(`${base}/matches/${ranked.id}`, { waitUntil: 'networkidle' });
  const score = alphaPage.locator('[aria-label="Arc Relay score"]');
  await score.waitFor({ timeout: 20_000 });
  await alphaPage.getByRole('button', { name: 'Play match' }).click({ timeout: 20_000 });
  await alphaPage.locator('[data-play-overlay]').waitFor({ state: 'detached' });
  await alphaPage.waitForTimeout(1_000);
  const scoreText = await score.innerText();
  if (!scoreText.includes(sheetName) || !scoreText.includes(mindName))
    throw new Error(`Score bug lacks entrant identities: ${scoreText}`);
  await score.screenshot({ path: join(output, 'score-bug.png') });
  await alphaPage.screenshot({ path: join(output, 'hosted-match.png'), fullPage: true });

  const detail = await okJson(await alpha.request.get(`${base}/api/matches/${ranked.id}`));
  if (errors.length > 0) throw new Error([...new Set(errors)].join('\n'));
  console.log(JSON.stringify({
    sheetEntrantId: sheet.id,
    mindEntrantId: mind.entrant.id,
    preflightMatchId: preflight.matchId,
    rankedMatchId: ranked.id,
    replayHash: detail.replayHash,
    ratings: settled.entrants.map((entrant) => ({
      id: entrant.id, kind: entrant.kind, rating: entrant.rating,
      rankedMatches: entrant.rankedMatches, status: entrant.status,
    })),
    screenshots: ['sheet-card.png', 'mind-card.png', 'ladder.png', 'score-bug.png', 'hosted-match.png']
      .map((name) => join(output, name)),
  }, null, 2));
} finally {
  await browser.close();
}

async function register(context, displayName, email) {
  await okJson(await context.request.post(`${base}/api/accounts/register`, {
    data: { displayName, email, password: 'correct-horse-battery-staple' },
  }));
}

async function okJson(response) {
  if (!response.ok()) throw new Error(`${response.request().method()} ${response.url()} -> ${response.status()} ${await response.text()}`);
  return response.json();
}

async function poll(read, timeoutMs, label) {
  const until = Date.now() + timeoutMs;
  while (Date.now() < until) {
    const value = await read();
    if (value) return value;
    await new Promise((resolve) => setTimeout(resolve, 500));
  }
  throw new Error(`Timed out waiting for ${label}.`);
}

function advanceDedicatedSmokeClock(matchId) {
  if (process.env.SMOKE_ALLOW_CLOCK_ADVANCE !== '1')
    throw new Error('Set SMOKE_ALLOW_CLOCK_ADVANCE=1 for the dedicated smoke database.');
  const sql = `
    UPDATE "Matches" SET "BroadcastStartedAt" = NOW() - INTERVAL '2 hours' WHERE "Id" = '${matchId}';
    UPDATE "BackgroundJobs" SET "AvailableAt" = NOW()
      WHERE "Type" = 'SettleArcRelayRating'
        AND "PayloadJson"::jsonb->>'matchId' = '${matchId}';`;
  const container = process.env.SMOKE_PG_CONTAINER;
  const command = container ? 'docker' : 'psql';
  const args = container
    ? ['exec', container, 'psql', '-U', process.env.PGUSER ?? 'botarena',
      '-d', process.env.PGDATABASE ?? 'botarena', '--set', 'ON_ERROR_STOP=1', '--command', sql]
    : ['--set', 'ON_ERROR_STOP=1', '--command', sql];
  const result = spawnSync(command, args, { encoding: 'utf8', env: process.env });
  if (result.status !== 0)
    throw new Error(`Smoke clock advance failed: ${result.error?.message ?? result.stderr ?? 'unknown error'}`);
}

function mindSource() { return `using BotArena.Sdk;

public sealed class SmokeRelayMind : IGenericMindBot
{
    public void StartMatch(MindStart start) { }
    public void Think(MindContext mind)
    {
        foreach (MindBody body in mind.Bodies)
            body.Hold("entrant product smoke");
    }
    public void EndMatch(MindEnd end) { }
}`; }
