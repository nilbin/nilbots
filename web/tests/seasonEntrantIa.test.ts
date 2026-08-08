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
});
