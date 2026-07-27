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
  '/api/leaderboard': {
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
  '/api/me': null,
};

const b = await chromium.launch();
const page = await b.newPage({ viewport: { width: 1180, height: 900 }, deviceScaleFactor: 2 });
await page.route('**/api/**', async (route) => {
  const url = new URL(route.request().url());
  const key = Object.keys(FIXTURES).find((k) => url.pathname.startsWith(k));
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
for (const [name, path] of [['leaderboard', '/leaderboard']]) {
  for (const [label, width] of [['wide', 1180], ['narrow', 390]]) {
    await page.setViewportSize({ width, height: 900 });
    await page.goto(BASE + path, { waitUntil: 'networkidle' });
    await page.waitForTimeout(700);
    const overflow = await page.evaluate(() =>
      document.documentElement.scrollWidth - document.documentElement.clientWidth);
    await page.screenshot({ path: `${SHOTS}/${name}-${label}.png`, fullPage: true });
    console.log(`  ${name}-${label}  overflow ${overflow}px  ${SHOTS}/${name}-${label}.png`);
  }
}
await b.close();
