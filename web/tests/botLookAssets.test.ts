import assert from 'node:assert/strict';
import { readdirSync, readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import test from 'node:test';

const looksRoot = join(
  dirname(fileURLToPath(import.meta.url)),
  '..',
  'src',
  'assets',
  'bot-looks',
);

test('every bot-look manifest references a supported local sprite', () => {
  for (const directory of readdirSync(looksRoot, { withFileTypes: true })) {
    if (!directory.isDirectory()) continue;
    const root = join(looksRoot, directory.name);
    const manifest = JSON.parse(
      readFileSync(join(root, 'look.json'), 'utf8'),
    ) as { id: string; sprite: string };
    assert.equal(manifest.id, directory.name);
    assert.match(manifest.sprite, /\.(png|svg)$/);
    assert.doesNotThrow(() => readFileSync(join(root, manifest.sprite)));
  }
});

test('shipped vector looks contain no embedded raster image', () => {
  for (const id of [
    'aureate-warden',
    'bulwark',
    'lancer',
    'mantis',
    'needle',
    'orbiter',
    'vanguard',
  ]) {
    const manifest = JSON.parse(
      readFileSync(join(looksRoot, id, 'look.json'), 'utf8'),
    ) as { sprite: string };
    assert.equal(manifest.sprite, 'sprite.svg');
    const source = readFileSync(join(looksRoot, id, manifest.sprite), 'utf8');
    assert.match(source, /viewBox="0 0 512 512"/);
    assert.doesNotMatch(source, /<image\b/i);
    assert.doesNotMatch(source, /data:image\//i);
  }
});

test('every recommended projectile companion resolves to a real package', () => {
  const companions = new Map<string, string>();
  for (const directory of readdirSync(looksRoot, { withFileTypes: true })) {
    if (!directory.isDirectory()) continue;
    const manifest = JSON.parse(
      readFileSync(join(looksRoot, directory.name, 'look.json'), 'utf8'),
    ) as { defaultProjectile?: string };
    if (manifest.defaultProjectile === undefined) continue;
    companions.set(directory.name, manifest.defaultProjectile);
    assert.doesNotThrow(() =>
      readFileSync(
        join(looksRoot, '..', 'projectile-looks', manifest.defaultProjectile!, 'look.json'),
      ),
    );
  }
  // The pairs are a deliberate art-direction choice, so pin them rather than
  // only proving that whatever is declared happens to exist.
  assert.deepEqual(
    [...companions].sort(),
    [
      ['aureate-warden', 'regent-lance'],
      ['mantis', 'talon'],
    ],
  );
});

test('replaced raster looks retain references outside the runtime bundle', () => {
  const referencesRoot = join(
    looksRoot,
    '..',
    '..',
    '..',
    '..',
    'art',
    'bot-looks',
  );
  for (const id of [
    'aureate-warden',
    'bulwark',
    'needle',
    'orbiter',
    'vanguard',
  ])
    assert.doesNotThrow(() =>
      readFileSync(join(referencesRoot, id, 'raster-reference.png')),
    );
});
