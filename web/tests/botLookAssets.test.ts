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

test('the vector proof look contains no embedded raster image', () => {
  const source = readFileSync(join(looksRoot, 'lancer', 'sprite.svg'), 'utf8');
  assert.match(source, /viewBox="0 0 512 512"/);
  assert.doesNotMatch(source, /<image\b/i);
  assert.doesNotMatch(source, /data:image\//i);
});
