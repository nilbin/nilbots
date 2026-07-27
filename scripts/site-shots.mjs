// Screenshot site pages against a mocked API.
//
// The site needs a server and a database to render, which makes visual review of a
// redesign either impossible or a half-hour of setup. Every page reads through
// `site/queries.ts`, so intercepting `/api/**` and answering with fixtures renders the
// real components with real layout and no backend at all.
//
//   node scripts/site-shots.mjs            # writes to /tmp/site-shots
//   SHOTS=dir BASE=http://... node scripts/site-shots.mjs
import pw from '/Users/sebastian.lind/source/e2e-tests/node_modules/playwright/index.js';
import { mkdirSync } from 'node:fs';
const { chromium } = pw;

const BASE = process.env.BASE ?? 'http://127.0.0.1:4173';
const SHOTS = process.env.SHOTS ?? '/tmp/site-shots';
mkdirSync(SHOTS, { recursive: true });

const bot = (rank, name, owner, accent, lookId, rating, sets) => ({
  id: `bot-${rank}`, slug: name.toLowerCase().replace(/[^a-z0-9]+/g, '-'),
  rank, name, owner, accent, lookId, rating, rankedSets: sets,
});

const FIXTURES = {
  '^/api/leaderboard': {
    rulesVersion: '0.5', activeRulesVersion: '0.5', ladders: ['0.4', '0.5'],
    entries: [
      bot(1, 'Warden gen-1', 'ada', '#7dd3fc', 'aureate-warden', 1341, 14),
      bot(2, 'Bastille gen-5', 'kell', '#ef4444', 'bulwark', 1309, 12),
      bot(3, 'Pincer gen-10', 'you', '#22d3ee', 'vanguard', 1284, 11),
      bot(4, 'Rampart gen-2', 'juno', '#bef264', 'orbiter', 1250, 9),
      bot(5, 'Halyard gen-3', 'mox', '#fb7185', 'needle', 1238, 9),
      bot(6, 'Murder Roomba', 'you', '#f5a623', 'mantis', 1147, 6),
    ],
  },
  '^/api/me': { id: 'u1', displayName: 'you', email: 'you@example.com' },
  '^/api/bots/[^/]+$': {
    id: 'bot-3', slug: 'pincer-gen-10', name: 'Pincer gen-10', owner: 'you',
    accent: '#22d3ee', lookId: 'vanguard', projectileLookId: null, isOwner: true,
    currentStanding: { rank: 3, rating: 1284, rulesVersion: '0.5', rankedSets: 11 },
    versions: [
      { id: 'v10', versionNumber: 10, status: 'Built', isActive: true,
        artifactHash: '9f31c0a4b7de51', createdAt: '2026-07-17T10:00:00Z' },
      { id: 'v9', versionNumber: 9, status: 'Built', isActive: false,
        artifactHash: '3ab77c1904ee62', createdAt: '2026-07-02T10:00:00Z' },
      { id: 'v8', versionNumber: 8, status: 'Failed', isActive: false,
        artifactHash: null, createdAt: '2026-06-24T10:00:00Z' },
    ],
  },
  '^/api/bots/[^/]+/statistics$': {
    combat: { games: 214, wins: 118, losses: 96, damageDealt: 512, damageTaken: 489 },
    ranked: { sets: 11, setsWon: 7 },
  },
  '^/api/bots/[^/]+/matches': { wins: 118, losses: 96, draws: 0, matches: [] },
  '^/api/bots$': [],
  '^/api/matches': [],
};

const b = await chromium.launch();
const page = await b.newPage({ viewport: { width: 1180, height: 900 }, deviceScaleFactor: 2 });
// A blank screenshot is worse than no screenshot: it looks like a design decision.
const failures = [];
page.on('pageerror', (e) => failures.push(String(e).split('\n')[0]));
page.on('console', (m) => { if (m.type() === 'error') failures.push(m.text().slice(0, 200)); });
await page.route('**/api/**', async (route) => {
  const url = new URL(route.request().url());
  // Patterns, not prefixes: components call /api/bots/{id}/statistics with the bot's
  // id while the page was reached by slug, so a prefix match answers the wrong route
  // and the page dies on a field that was never there.
  const key = Object.keys(FIXTURES).find((k) => new RegExp(k).test(url.pathname));
  if (key === undefined) return route.fulfill({ status: 404, body: '{}' });
  const body = FIXTURES[key];
  return route.fulfill({
    status: body === null ? 401 : 200,
    contentType: 'application/json',
    body: JSON.stringify(body ?? {}),
  });
});

// Every page is shot wide and narrow: a redesign that only works on a laptop is half a
// redesign, and the narrow shot is the one that catches a table nobody can read.
for (const [name, path] of [['leaderboard', '/leaderboard'], ['bot', '/bots/pincer-gen-10']]) {
  for (const [label, width] of [['wide', 1180], ['narrow', 390]]) {
    await page.setViewportSize({ width, height: 900 });
    await page.goto(BASE + path, { waitUntil: 'networkidle' });
    await page.waitForTimeout(700);
    const overflow = await page.evaluate(() =>
      document.documentElement.scrollWidth - document.documentElement.clientWidth);
    await page.screenshot({ path: `${SHOTS}/${name}-${label}.png`, fullPage: true });
    // A page that rendered nothing is the failure this harness exists to catch, so it
    // is reported as loudly as a crash rather than saved as a black rectangle.
    const text = (await page.locator('body').innerText()).trim();
    const bad = failures.splice(0).filter((f) => !/SignalR|negotiation|Failed to start|404/.test(f));
    console.log(
      `  ${name}-${label}  overflow ${overflow}px  ${text.length} chars` +
        (text.length < 40 ? '  ← BLANK' : '') +
        (bad.length ? `\n      ${bad.slice(0, 2).join('\n      ')}` : ''),
    );
  }
}
await b.close();
