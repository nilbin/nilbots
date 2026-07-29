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
  for (const directory of readdirSync(looksRoot, { withFileTypes: true })) {
    if (!directory.isDirectory()) continue;
    const id = directory.name;
    const manifest = JSON.parse(
      readFileSync(join(looksRoot, id, 'look.json'), 'utf8'),
    ) as { sprite: string };
    if (manifest.sprite !== 'sprite.svg') continue;
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
  // The Warden's earned pair and the six approved class store packs are the
  // complete recommendation set. Pinning it stops an unrelated chassis from
  // silently changing a player's independently selected projectile.
  assert.deepEqual([...companions].sort(), [
    ['aureate-warden', 'regent-lance'],
    ['bulwark-gatehouse', 'bulwark-gate-slug'],
    ['bulwark-mirror-bastion', 'bulwark-mirror-wedge'],
    ['fabricator-copyforge', 'fabricator-copy-bit'],
    ['fabricator-rivet-mantis', 'fabricator-rivet-punch'],
    ['striker-arc-viper', 'striker-arc-cutter'],
    ['striker-vector-kestrel', 'striker-vector-fork'],
  ]);
});

test('alternate class skins declare their class and direct team surfaces', () => {
  for (const [id, classId] of [
    ['bulwark-gatehouse', 'bulwark'],
    ['bulwark-mirror-bastion', 'bulwark'],
    ['fabricator-copyforge', 'fabricator'],
    ['fabricator-rivet-mantis', 'fabricator'],
    ['striker-arc-viper', 'striker'],
    ['striker-vector-kestrel', 'striker'],
  ]) {
    const manifest = JSON.parse(
      readFileSync(join(looksRoot, id, 'look.json'), 'utf8'),
    ) as { classId?: string; sprite: string };
    assert.equal(manifest.classId, classId);

    const source = readFileSync(join(looksRoot, id, manifest.sprite), 'utf8');
    const tagged = [
      ...source.matchAll(
        /<([a-z]+)\b[^>]*data-team-accent="true"[^>]*>/gi,
      ),
    ];
    assert.ok(
      tagged.length >= 3,
      `${id} needs several team-readable chassis surfaces.`,
    );
    for (const [element, elementName] of tagged) {
      assert.notEqual(
        elementName.toLowerCase(),
        'g',
        `${id} must tag direct shapes, not an inherited group.`,
      );
      assert.match(element, /\b(?:fill|stroke)="(?!none\b)[^"]+"/i);
    }
  }
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
