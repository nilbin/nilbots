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
    'glass-manta',
    'helio-kite',
    'lancer',
    'mossback',
    'needle',
    'orbiter',
    'rift-runner',
    'scrap-jackal',
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

test('a bot look may recommend an existing projectile companion', () => {
  const manifest = JSON.parse(
    readFileSync(join(looksRoot, 'aureate-warden', 'look.json'), 'utf8'),
  ) as { defaultProjectile: string };
  const projectileRoot = join(
    looksRoot,
    '..',
    'projectile-looks',
    manifest.defaultProjectile,
  );
  assert.equal(manifest.defaultProjectile, 'regent-lance');
  assert.doesNotThrow(() =>
    readFileSync(join(projectileRoot, 'look.json')),
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
