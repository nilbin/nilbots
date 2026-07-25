// Playwright walk-through of the site, run against a local server, checking that the
// real use cases are reachable and reporting where they are not. Promoted from a
// throwaway script because it found four defects on its first run (DECISIONS #98).
//
//   node scripts/ui-audit.mjs            # against http://127.0.0.1:8080
//   BASE=http://host:port node scripts/ui-audit.mjs
//
// Needs playwright on NODE_PATH or linked into ./node_modules; chromium comes from
// PLAYWRIGHT_BROWSERS_PATH. Screenshots land in the directory given by SHOTS.
import { chromium } from 'playwright';
import fs from 'node:fs';

const BASE = process.env.BASE ?? 'http://127.0.0.1:8080';
const SHOTS = process.env.SHOTS ?? '/tmp/nilbots-ui-audit';
fs.mkdirSync(SHOTS, { recursive: true });

const findings = [];
const note = (page, level, text) => findings.push({ page, level, text });

const browser = await chromium.launch({ args: ['--no-sandbox'] });
const ctx = await browser.newContext({ viewport: { width: 1280, height: 900 } });
const page = await ctx.newPage();

const consoleErrors = [];
page.on('console', (m) => { if (m.type() === 'error') consoleErrors.push(m.text()); });
page.on('pageerror', (e) => consoleErrors.push('pageerror: ' + e.message));
const failedRequests = [];
page.on('requestfailed', (r) => failedRequests.push(`${r.method()} ${r.url()} — ${r.failure()?.errorText}`));
page.on('response', (r) => {
  if (r.status() >= 400 && r.url().startsWith(BASE)) failedRequests.push(`${r.status()} ${r.url()}`);
});

const go = async (path, name) => {
  await page.goto(BASE + path, { waitUntil: 'networkidle' });
  await page.waitForTimeout(400);
  await page.screenshot({ path: `${SHOTS}/${name}.png`, fullPage: true });
  return page;
};

// ---------- 1. Landing / arena ----------
await go('/', '01-home');
const navLinks = await page.locator('header a, nav a').allInnerTexts();
note('/', 'info', `nav: ${navLinks.join(' | ')}`);
const matchRows = await page.locator('a[href^="/matches/"]').count();
note('/', matchRows > 0 ? 'ok' : 'gap', `${matchRows} match rows on the landing feed`);
// Is there any way to filter/search this feed?
const homeInputs = await page.locator('input, select').count();
note('/', homeInputs === 0 ? 'gap' : 'info', `${homeInputs} filter controls on the match feed`);

// ---------- 2. Leaderboard ----------
await go('/leaderboard', '02-leaderboard');
const ladderButtons = await page.locator('[aria-label="Ruleset ladder"] button').allInnerTexts();
note('/leaderboard', ladderButtons.length > 1 ? 'ok' : 'gap', `ladder switcher: ${ladderButtons.join(', ') || 'none'}`);
const rows = await page.locator('ol li').count();
note('/leaderboard', 'info', `${rows} ranked entries listed`);
const lbInputs = await page.locator('input[type="search"], input[type="text"]').count();
note('/leaderboard', lbInputs === 0 ? 'gap' : 'ok', `${lbInputs} search boxes`);

// Switch to a closed ladder and check it says so.
const closed = ladderButtons.find((t) => !t.includes('0.5'));
if (closed) {
  await page.getByRole('button', { name: closed }).click();
  await page.waitForTimeout(600);
  await page.screenshot({ path: `${SHOTS}/03-leaderboard-closed.png`, fullPage: true });
  const body = await page.locator('body').innerText();
  note('/leaderboard', body.toLowerCase().includes('closed') ? 'ok' : 'gap',
    `switching to ${closed}: closed-ladder notice ${body.toLowerCase().includes('closed') ? 'shown' : 'MISSING'}`);
}

// Does a leaderboard row link to the bot, by slug?
const firstBotHref = await page.locator('a[href^="/bots/"]').first().getAttribute('href');
note('/leaderboard', firstBotHref && !/[0-9a-f]{8}-[0-9a-f]{4}/.test(firstBotHref) ? 'ok' : 'gap',
  `first bot link: ${firstBotHref}`);

// ---------- 3. Bots list ----------
await go('/bots', '04-bots');
const botCards = await page.locator('a[href^="/bots/"]').count();
note('/bots', 'info', `${botCards} bots listed`);
const botsControls = await page.locator('input, select, [role="group"] button').count();
note('/bots', botsControls === 0 ? 'gap' : 'ok', `${botsControls} filter/sort/search controls`);
const bodyText = await page.locator('body').innerText();
note('/bots', /@[\w.-]+\.\w+/.test(bodyText) ? 'FAIL' : 'ok', 'no email addresses rendered');

// ---------- 4. Bot detail (someone else's bot, signed out) ----------
if (firstBotHref) {
  await go(firstBotHref, '05-bot-detail-anon');
  const detail = await page.locator('body').innerText();
  note(firstBotHref, detail.includes('Ranked') ? 'info' : 'ok', 'ranked control visible while signed out?');
  const versions = await page.locator('text=/v\\d+/').count();
  note(firstBotHref, 'info', `${versions} version markers`);
  const hasMatchHistory = detail.toLowerCase().includes('match') || detail.toLowerCase().includes('recent');
  note(firstBotHref, hasMatchHistory ? 'ok' : 'gap', 'match history section present');
}

// ---------- 5. A match / replay viewer ----------
await go('/', '06-home-again');
const matchHref = await page.locator('a[href^="/matches/"]').first().getAttribute('href');
if (matchHref) {
  await go(matchHref, '07-match');
  await page.waitForTimeout(2500);
  await page.screenshot({ path: `${SHOTS}/08-match-loaded.png`, fullPage: true });
  const canvas = await page.locator('canvas, svg').count();
  note(matchHref, canvas > 0 ? 'ok' : 'gap', `${canvas} canvas/svg elements (viewer rendered)`);
  const controls = await page.locator('button').allInnerTexts();
  note(matchHref, 'info', `viewer controls: ${controls.slice(0, 12).join(', ')}`);
}

// ---------- 6. Docs ----------
await go('/docs', '09-docs');
const docsText = await page.locator('body').innerText();
note('/docs', docsText.length > 2000 ? 'ok' : 'gap', `${docsText.length} chars of docs rendered`);
note('/docs', docsText.includes('matchmade') || docsText.includes('live ladder') ? 'ok' : 'gap',
  'docs mention the current ranked behaviour');
const headings = await page.locator('h1, h2, h3').allInnerTexts();
note('/docs', 'info', `sections: ${headings.slice(0, 14).join(' | ')}`);

// ---------- 7. Auth + garage (signed out) ----------
await go('/garage', '10-garage-signed-out');
note('/garage', 'info', 'signed-out garage: ' + (await page.locator('body').innerText()).slice(0, 120).replace(/\n/g, ' '));

await go('/login', '11-login');
const authFields = await page.locator('input').count();
note('/login', authFields >= 2 ? 'ok' : 'gap', `${authFields} inputs on the auth form`);

// ---------- 8. Mobile viewport ----------
const mobile = await ctx.newPage();
await mobile.setViewportSize({ width: 390, height: 844 });
await mobile.goto(BASE + '/leaderboard', { waitUntil: 'networkidle' });
await mobile.waitForTimeout(500);
await mobile.screenshot({ path: `${SHOTS}/12-mobile-leaderboard.png`, fullPage: true });
const overflow = await mobile.evaluate(() =>
  document.documentElement.scrollWidth > document.documentElement.clientWidth + 2);
note('mobile', overflow ? 'gap' : 'ok', `390px viewport horizontal overflow: ${overflow}`);

// ---------- 9. Unknown routes ----------
await go('/bots/no-such-bot-at-all', '13-unknown-bot');
const notFound = (await page.locator('body').innerText()).trim();
note('/bots/<unknown>', notFound.length > 0 ? 'info' : 'gap', `unknown bot page shows: "${notFound.slice(0, 90)}"`);

fs.writeFileSync(`${SHOTS}/findings.json`,
  JSON.stringify({ findings, consoleErrors, failedRequests }, null, 2));

console.log('--- FINDINGS ---');
for (const f of findings) console.log(`[${f.level}] ${f.page}: ${f.text}`);
console.log('\n--- CONSOLE ERRORS ---');
console.log(consoleErrors.length ? [...new Set(consoleErrors)].join('\n') : '(none)');
console.log('\n--- FAILED/4xx REQUESTS ---');
console.log(failedRequests.length ? [...new Set(failedRequests)].join('\n') : '(none)');

await browser.close();
