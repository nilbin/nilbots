import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import test from 'node:test';

const shell = readFileSync(
  new URL('../src/site/Shell.tsx', import.meta.url),
  'utf8',
);
const site = readFileSync(
  new URL('../src/site/Site.tsx', import.meta.url),
  'utf8',
);
const season = readFileSync(
  new URL('../src/site/pages/SeasonPage.tsx', import.meta.url),
  'utf8',
);
const entrants = readFileSync(
  new URL('../src/site/pages/ArcRelayPage.tsx', import.meta.url),
  'utf8',
);
const workshop = readFileSync(
  new URL('../src/site/sheets/TacticalSheetEditor.tsx', import.meta.url),
  'utf8',
);
const docs = readFileSync(
  new URL('../src/site/pages/DocsPage.tsx', import.meta.url),
  'utf8',
);
const styles = readFileSync(
  new URL('../src/index.css', import.meta.url),
  'utf8',
);

test('primary navigation has one season-centred hierarchy on desktop and phone', () => {
  const desktop = [...shell.matchAll(/<TopLink to="([^"]+)">([^<]+)<\/TopLink>/g)]
    .map((match) => [match[1], match[2]]);
  const phone = [...shell.matchAll(/<MobileLink to="([^"]+)">([^<]+)<\/MobileLink>/g)]
    .map((match) => [match[1], match[2]]);
  const expected = [
    ['/', 'Season'],
    ['/entrants', 'Entrants'],
    ['/watch', 'Watch'],
    ['/docs', 'Docs'],
  ];

  assert.deepEqual(desktop, expected);
  assert.deepEqual(phone, expected);
  assert.doesNotMatch(shell, />Relay<|>Sheets</);
});

test('old Relay URLs redirect without becoming a second product hierarchy', () => {
  assert.match(site, /path="\/relay" element={<Navigate to="\/" replace \/>}/);
  assert.match(
    site,
    /path="\/relay\/sheets" element={<Navigate to="\/entrants\/sheets\/new" replace \/>}/,
  );
  assert.match(site, /path="\/entrants\/sheets\/:sheetId\/edit"/);
});

test('the home screen reads the authoritative season projection', () => {
  assert.match(season, /useArcRelaySeason\(\)/);
  assert.match(season, /Season standings/);
  assert.match(season, /Only settled, fully revealed results appear here/);
  assert.match(season, /Equal ratings share a rank/);
  assert.match(season, /baselineMatchesPerEntrantPerDay/);
});

test('ranked enrollment and immediate practice have different visible verbs', () => {
  assert.match(entrants, /Ranked runs automatically/);
  assert.match(entrants, /There is no ranked queue button/);
  assert.match(entrants, /Field for ranked/);
  assert.match(workshop, /Practice (?:&amp;|&) watch/);
  assert.match(workshop, /Practice is immediate and unranked/);
});

test('the phone shortcut navigates to one configured practice action', () => {
  assert.equal(workshop.match(/'Practice & watch'/g)?.length, 1);
  assert.match(workshop, /Practice setup/);
  assert.doesNotMatch(workshop, /Import JSON pair|Export pinned pair|layout sha256/);
});

test('conditions and custody are focused doctrine tools rather than top-level steps', () => {
  assert.doesNotMatch(workshop, /id: 'conditions'|id: 'custody'/);
  assert.match(workshop, /id: 'doctrines'.*path\.startsWith\('authoring'\).*path\.startsWith\('custodyPolicies'\)/s);
  assert.match(workshop, /title="Doctrine triggers"/);
  assert.match(workshop, /title="Core custody"/);
  assert.match(workshop, /label: '3 Map'/);
});

test('doctrine authoring presents a tactical command board over the compiler schema', () => {
  assert.match(workshop, /Tactical command board/);
  assert.match(workshop, /data-doctrine-select/);
  assert.match(workshop, /classInfo\.signatureName/);
  assert.match(workshop, /Choose action/);
  assert.match(workshop, /Trigger library/);
  assert.match(workshop, /WHEN \{predicateSentence\(predicate\)\}/);
  assert.match(workshop, /Core relay plans/);
  assert.match(workshop, /Advanced command-plan settings/);
});

test('composition turns compiler roles into editable player assignments', () => {
  assert.match(workshop, /Squad assignments/);
  assert.match(workshop, /Turn chassis into jobs/);
  assert.match(workshop, /AssignmentNameEditor/);
  assert.match(workshop, /Eligible chassis · priority order/);
  assert.match(workshop, /Normal staffing target/);
  assert.match(workshop, /Advanced allocation rules/);
});

test('custom-mind authoring is dormant on the launch web surface', () => {
  assert.doesNotMatch(entrants, /custom mind|custom-mind|preflight|C# source/i);
  assert.doesNotMatch(docs, /custom mind|custom-mind|preflight|C# source/i);
});

test('primary actions use the forge material and the dead site footer is gone', () => {
  assert.match(styles, /\.btn-primary\s*\{[^}]*background:\s*var\(--color-arena-material\)/s);
  assert.match(styles, /\.btn\s*\{[^}]*display:\s*inline-flex[^}]*line-height:\s*1\.15/s);
  assert.doesNotMatch(shell, /<footer|deterministic Arc Relay/);
});
