import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import test from 'node:test';

const read = (path: string) => readFileSync(new URL(path, import.meta.url), 'utf8');
const shell = read('../src/site/Shell.tsx');
const site = read('../src/site/Site.tsx');
const season = read('../src/site/pages/SeasonPage.tsx');
const entrants = read('../src/site/pages/ArcRelayPage.tsx');
const workshop = read('../src/site/sheets/TacticalSheetEditor.tsx');
const orders = read('../src/site/sheets/OrdersWorkspace.tsx');
const map = read('../src/site/sheets/MapPlanEditor.tsx');
const docs = read('../src/site/pages/DocsPage.tsx');
const styles = read('../src/index.css');
const siteReview = read('../vite.site-review.config.ts');
const siteShots = read('../../scripts/site-shots.mjs');

test('primary navigation has one season-centred hierarchy on desktop and phone', () => {
  const desktop = [...shell.matchAll(/<TopLink to="([^"]+)">([^<]+)<\/TopLink>/g)].map((match) => [match[1], match[2]]);
  const phone = [...shell.matchAll(/<MobileLink to="([^"]+)">([^<]+)<\/MobileLink>/g)].map((match) => [match[1], match[2]]);
  const expected = [['/', 'Season'], ['/entrants', 'Entrants'], ['/watch', 'Watch'], ['/docs', 'Docs']];
  assert.deepEqual(desktop, expected);
  assert.deepEqual(phone, expected);
  assert.doesNotMatch(shell, />Relay<|>Sheets</);
});

test('old Relay URLs redirect without becoming a second product hierarchy', () => {
  assert.match(site, /path="\/relay" element={<Navigate to="\/" replace \/>}/);
  assert.match(site, /path="\/relay\/sheets" element={<Navigate to="\/entrants\/sheets\/new" replace \/>}/);
  assert.match(site, /path="\/entrants\/sheets\/:sheetId\/edit"/);
});

test('the home screen reads the authoritative season projection', () => {
  assert.match(season, /useArcRelaySeason\(\)/);
  assert.match(season, /Season standings/);
  assert.match(season, /Only settled, fully revealed results appear here/);
  assert.match(season, /baselineMatchesPerEntrantPerDay/);
});

test('ranked enrollment and immediate practice use different visible verbs', () => {
  assert.match(entrants, /Ranked runs automatically/);
  assert.match(entrants, /There is no ranked queue button/);
  assert.match(entrants, /Field for ranked/);
  assert.match(workshop, /Practice & watch/);
  assert.match(workshop, /Practice is immediate and unranked/);
});

test('responsive practice docks never render two visible actions at one breakpoint', () => {
  assert.match(workshop, /btn btn-secondary hidden[^\n]+sm:inline-flex/);
  assert.match(workshop, /fixed inset-x-0 bottom-0[^\n]+sm:hidden/);
  assert.match(workshop, /Scenario/);
  assert.match(workshop, /Practice again/);
  assert.doesNotMatch(workshop, /Import JSON pair|Export pinned pair|layout sha256/i);
});

test('the workshop speaks the consolidated Orders v2 language only', () => {
  assert.match(workshop, /id: 'squad'.*id: 'orders'.*id: 'map'/s);
  assert.match(orders, /What this role tries, in order/);
  assert.match(orders, /first executable leaf wins/i);
  assert.match(orders, /Named conditions/);
  assert.match(orders, /Shared capacity rules/);
  assert.match(orders, /Shared exact orders/);
  assert.match(orders, /Move/);
  assert.match(orders, /Fight/);
  assert.match(orders, /Use skill/);
  assert.match(orders, /next\.operator = 'equals'/);
  assert.match(orders, /Handoff/);
  assert.match(orders, /Drop Core/);
  assert.doesNotMatch(`${workshop}\n${orders}`, /custodyPolicies|doctrines|muster|escort|assignment order/i);
});

test('conditions, lifecycle and capacities are explicit game controls rather than opaque strings', () => {
  assert.match(orders, /Enter \/ remain while/);
  assert.match(orders, /Latch until/);
  assert.match(orders, /AND · all true/);
  assert.match(orders, /OR · any true/);
  assert.match(orders, /Bodies per resolved target/);
  assert.match(orders, /Target priorities are read left to right/);
  assert.doesNotMatch(orders, /advanced settings/i);
});

test('the shared-order library can author every physical action and a nested play', () => {
  assert.match(orders, /ACTIONS\.map\(\(action\)/);
  assert.match(orders, /add\(\{ orders: \[freshOrder\('move', 'opening-move'\)\] \}\)/);
  for (const label of ['Move', 'Fight', 'Use skill', 'Handoff', 'Drop Core', 'Nested play'])
    assert.match(orders, new RegExp(label.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')));
});

test('roles directly cast exact class bodies and surface signature identity', () => {
  assert.match(orders, /The eight bodies/);
  assert.match(orders, /Bodies sharing a role run the exact same ordered plan/);
  assert.match(orders, /ClassIcon/);
  assert.match(orders, /signatureName/);
  assert.match(orders, /Upgrade rhythm/);
  assert.doesNotMatch(orders, /candidateClasses|overflowRoleId|\.minimum|\.preferred|\.maximum\b/);
});

test('map planning renders all authoritative social landmarks and opens inside movement cards', () => {
  assert.match(orders, /MapTargetPreview/);
  assert.match(map, /Healing/);
  assert.match(map, /Delivery/);
  assert.match(map, /Protected spawn/);
  assert.match(map, /Skill blocked/);
  assert.match(map, /relationForSpawn/);
  assert.match(map, /nearestSpawnTeam/);
  assert.match(map, /Draw the ground your orders name/);
  assert.match(orders, /Plot a route before this patrol can run/);
  assert.match(orders, /Open map and add route/);
  assert.doesNotMatch(orders, /value=\{value\.to!\}/);
});

test('sheet tools contain player-facing entrant controls only', () => {
  assert.match(workshop, /Sheet tools/);
  assert.match(workshop, /Field this entrant when saved/);
  assert.match(workshop, /Save as a new entrant/);
  assert.doesNotMatch(workshop, /import source|export source|portable source|type="file"|sha256|content hash/i);
});

test('custom-mind authoring stays dormant on the launch web surface', () => {
  assert.doesNotMatch(entrants, /custom mind|custom-mind|preflight|C# source/i);
  assert.doesNotMatch(docs, /custom mind|custom-mind|preflight|C# source/i);
});

test('primary and practice actions use the established forge controls', () => {
  assert.match(styles, /\.btn-primary\s*\{[^}]*background:\s*var\(--color-arena-material\)/s);
  assert.match(styles, /\.btn-secondary\s*\{[^}]*background:/s);
  assert.match(styles, /\.btn-secondary-symbol\s*\{[^}]*color:\s*var\(--color-arena-material\)/s);
  assert.doesNotMatch(shell, /<footer|deterministic Arc Relay/);
});

test('the controlled browser review opens the current Orders v2 Tapline workshop', () => {
  assert.match(siteReview, /starter-tapline-v2\.json/);
  assert.match(siteReview, /map-plans\/tapline-6\/starter-tapline-v2\.json/);
  assert.match(siteReview, /\/api\/sheets\/catalog/);
  assert.match(siteShots, /\['sheet-workshop', '\/entrants\/sheets\/new'\]/);
});
