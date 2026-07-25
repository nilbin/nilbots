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
  'projectile-looks',
);

test('projectile looks are genuine East-facing SVG packages', () => {
  const ids: string[] = [];
  for (const directory of readdirSync(looksRoot, { withFileTypes: true })) {
    if (!directory.isDirectory()) continue;
    const root = join(looksRoot, directory.name);
    const manifest = JSON.parse(
      readFileSync(join(root, 'look.json'), 'utf8'),
    ) as { id: string; sprite: string; scale: number };
    ids.push(manifest.id);
    assert.equal(manifest.id, directory.name);
    assert.equal(manifest.sprite, 'sprite.svg');
    assert.ok(manifest.scale >= 0.3 && manifest.scale <= 0.7);
    const source = readFileSync(join(root, manifest.sprite), 'utf8');
    assert.match(source, /viewBox="0 0 256 256"/);
    assert.doesNotMatch(source, /<image\b/i);
    assert.doesNotMatch(source, /data:image\//i);
  }
  assert.deepEqual(
    ids.sort(),
    ['arc-spark', 'ion-orb', 'pulse-bolt', 'razor-shard', 'regent-lance'],
  );
});
