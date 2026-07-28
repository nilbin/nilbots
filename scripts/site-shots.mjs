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

const bot = (rank, name, owner, accent, lookId, rating, sets, movement, wins, losses, history) => ({
  id: `bot-${rank}`, slug: name.toLowerCase().replace(/[^a-z0-9]+/g, '-'),
  rank, name, owner, accent, lookId, rating, rankedSets: sets,
  // Season-shaped fields. Absent from the real payload today; present here so the
  // harness can show the design as designed.
  movementSinceSeasonOpen: movement, wins, losses, seasonHistory: history,
});

const FIXTURES = {
  '^/api/leaderboard': {
    rulesVersion: '0.5', activeRulesVersion: '0.5', ladders: ['0.4', '0.5'],
    season: { number: 3, endsAt: '2026-08-08T00:00:00Z', qualifyingRank: 8 },
    entries: [
      bot(1, 'Warden gen-1', 'ada', '#7dd3fc', 'aureate-warden', 1341, 14, 3, 62, 29, [1290,1301,1298,1315,1322,1334,1341]),
      bot(2, 'Bastille gen-5', 'kell', '#ef4444', 'bulwark', 1309, 12, -2, 54, 31, [1330,1326,1331,1318,1312,1307,1309]),
      bot(3, 'Pincer gen-10', 'you', '#22d3ee', 'vanguard', 1284, 11, 14, 48, 22, [1231,1240,1252,1249,1263,1275,1284]),
      bot(4, 'Rampart gen-2', 'juno', '#bef264', 'orbiter', 1250, 9, -6, 41, 38, [1288,1281,1272,1266,1259,1254,1250]),
      bot(5, 'Halyard gen-3', 'mox', '#fb7185', 'needle', 1238, 9, 0, 39, 35, [1236,1240,1237,1241,1235,1239,1238]),
      bot(6, 'Murder Roomba', 'you', '#f5a623', 'mantis', 1147, 6, -5, 28, 26, [1176,1170,1163,1158,1152,1149,1147]),
    ],
  },
  '^/api/season$': {
    number: 3, name: 'Season 3',
    opensAt: '2026-06-27T00:00:00Z', endsAt: '2026-08-08T00:00:00Z',
    qualifyingRank: 8,
  },
  '^/api/bots/[^/]+/ratings$': {
    generations: [
      { versionNumber: 8, isActive: false, points: [1150, 1163, 1171, 1168, 1182, 1190, 1198] },
      { versionNumber: 9, isActive: false, points: [1198, 1188, 1205, 1214, 1209, 1226, 1231] },
      { versionNumber: 10, isActive: true, points: [1231, 1226, 1244, 1252, 1249, 1268, 1284] },
    ],
  },
  '^/api/accounts/me': { id: 'u1', displayName: 'you', email: 'you@example.com' },
  '^/api/bots/[^/]+$': {
    id: 'bot-3', slug: 'pincer-gen-10', name: 'Pincer gen-10', owner: 'you',
    accent: '#22d3ee', lookId: 'vanguard', projectileLookId: null, isOwner: true,
    currentStanding: { rank: 3, rating: 1284, rulesVersion: '0.5', rankedSets: 11 },
    ratingHistory: [
      { generation: 8, live: false, ratings: [1150, 1163, 1171, 1168, 1182, 1190, 1198] },
      { generation: 9, live: false, ratings: [1198, 1188, 1205, 1214, 1209, 1226, 1231] },
      { generation: 10, live: true, ratings: [1231, 1226, 1244, 1252, 1249, 1268, 1284] },
    ],
    versions: [
      { id: 'v10', versionNumber: 10, status: 'Built', isActive: true,
        artifactHash: '9f31c0a4b7de51', createdAt: '2026-07-17T10:00:00Z' },
      { id: 'v9', versionNumber: 9, status: 'Built', isActive: false,
        artifactHash: '3ab77c1904ee62', createdAt: '2026-07-02T10:00:00Z' },
      { id: 'v8', versionNumber: 8, status: 'Failed', isActive: false,
        artifactHash: null, createdAt: '2026-06-24T10:00:00Z' },
    ],
  },
  '^/api/bots/mine$': [],
  '^/api/bots/[^/]+/stats$': {
    overall: { played: 214, wins: 118, losses: 96, draws: 0 },
    ranked: { played: 66, wins: 38, losses: 28, draws: 0 },
    unranked: { played: 148, wins: 80, losses: 68, draws: 0 },
    combat: { games: 214, damageDealt: 512, faults: 0 },
  },
  '^/api/bots/[^/]+/matches': { wins: 118, losses: 96, draws: 0, matches: [] },
  '^/api/bots$': [
    { id: 'bot-1', slug: 'warden-gen-1', name: 'Warden gen-1', owner: 'ada',
      accent: '#7dd3fc', lookId: 'aureate-warden', projectileLookId: 'pulse-bolt',
      createdAt: '2026-06-01T00:00:00Z', versionCount: 4, ratings: [],
      activeVersion: { id: 'v4', versionNumber: 4, status: 'Built' },
      currentStanding: { rank: 1, rating: 1341, rulesVersion: '0.5', rankedSets: 14 } },
    { id: 'bot-3', slug: 'pincer-gen-10', name: 'Pincer gen-10', owner: 'you',
      accent: '#22d3ee', lookId: 'vanguard', projectileLookId: 'pulse-bolt',
      createdAt: '2026-05-11T00:00:00Z', versionCount: 10, ratings: [],
      activeVersion: { id: 'v10', versionNumber: 10, status: 'Built' },
      currentStanding: { rank: 3, rating: 1284, rulesVersion: '0.5', rankedSets: 11 } },
  ],
  '^/api/matches/[^/]+/live$': { presentationTick: 24, totalTicks: null },
  '^/api/matches': [
    { id: 'm1', mapId: 'bastion-01', status: 'Completed', broadcasting: false,
      matchSetId: null, setGame: null, winnerSlot: 0, endReason: 'elimination',
      endTick: 39, createdAt: '2026-07-27T21:00:00Z', completedAt: '2026-07-27T21:02:00Z',
      participants: [
        { slot: 0, nameSnapshot: 'Pincer gen-10', ownerDisplayNameSnapshot: 'you', accentSnapshot: '#22d3ee', lookIdSnapshot: 'vanguard', projectileLookIdSnapshot: 'pulse-bolt', outcome: 'Win', finalHealth: 2 },
        { slot: 1, nameSnapshot: 'Bastille gen-5', ownerDisplayNameSnapshot: 'kell', accentSnapshot: '#ef4444', lookIdSnapshot: 'bulwark', projectileLookIdSnapshot: 'pulse-bolt', outcome: 'Loss', finalHealth: 2 },
      ] },
    { id: 'm2', mapId: 'arena-01', status: 'Running', broadcasting: true,
      matchSetId: null, setGame: null, winnerSlot: null, endReason: null,
      endTick: null, createdAt: '2026-07-27T21:20:00Z', completedAt: null,
      participants: [
        { slot: 0, nameSnapshot: 'Warden gen-1', ownerDisplayNameSnapshot: 'ada', accentSnapshot: '#7dd3fc', lookIdSnapshot: 'aureate-warden', projectileLookIdSnapshot: 'pulse-bolt', outcome: null, finalHealth: 2 },
        { slot: 1, nameSnapshot: 'Rampart gen-2', ownerDisplayNameSnapshot: 'juno', accentSnapshot: '#bef264', lookIdSnapshot: 'orbiter', projectileLookIdSnapshot: 'pulse-bolt', outcome: null, finalHealth: 2 },
      ] },
  ],
};

const b = await chromium.launch();
const page = await b.newPage({ viewport: { width: 1180, height: 900 }, deviceScaleFactor: 2 });
// A blank screenshot is worse than no screenshot: it looks like a design decision.
const failures = [];
page.on('pageerror', (e) => failures.push(String(e).split('\n')[0]));
page.on('console', (m) => { if (m.type() === 'error') failures.push(m.text().slice(0, 200)); });
await page.route('**/api/**', async (route) => {
  const url = new URL(route.request().url());
  // Patterns, not prefixes: components call /api/bots/{id}/statistics with the bot's id
  // while the page was reached by slug, so a prefix match answers the wrong route and the
  // page dies on a field that was never there.
  //
  // Most literal wins, not first declared. /api/bots/mine matches both `bots/mine` and
  // `bots/{id}`, and depending on which was written first the garage got a single bot
  // object where it wanted a list — a blank page whose cause is three files away.
  const literals = (k) => k.replace(/\[\^\/\]\+|[\^$]/g, '').length;
  const key = Object.keys(FIXTURES)
    .filter((k) => new RegExp(k).test(url.pathname))
    .sort((a, z) => literals(z) - literals(a))[0];
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
for (const [name, path] of [
  ['season', '/'],
  ['bots', '/bots'],
  ['watch', '/watch'],
  ['bot', '/bots/pincer-gen-10'],
  ['firstrun', '/garage'],
]) {
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
    const bad = failures.splice(0).filter((f) => !/SignalR|negotiation|Failed to start|status of 404/.test(f));
    console.log(
      `  ${name}-${label}  overflow ${overflow}px  ${text.length} chars` +
        (text.length < 40 ? '  ← BLANK' : '') +
        (bad.length ? `\n      ${bad.slice(0, 2).join('\n      ')}` : ''),
    );
  }
}
await b.close();
