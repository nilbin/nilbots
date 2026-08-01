import assert from 'node:assert/strict';
import { readdirSync, readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import test from 'node:test';
import {
  applyTeamAccentToSvg,
  presentationBotLook,
} from './.harness/harness.entry.js';

const assetsRoot = join(
  dirname(fileURLToPath(import.meta.url)),
  '..',
  'src',
  'assets',
);
const classLooksRoot = join(assetsRoot, 'class-looks');
const projectilesRoot = join(assetsRoot, 'class-projectile-looks');
const conceptRoot = join(
  dirname(fileURLToPath(import.meta.url)),
  '..',
  '..',
  'art',
  'class-look-concepts',
);

const expectedLooks = new Map([
  ['aegis-tortoise', ['bulwark', 'rebound-diamond']],
  ['aegis-tortoise-shell', ['bulwark', 'rebound-diamond']],
  ['aegis-tortoise-turret', ['bulwark', 'rebound-diamond']],
  ['lattice-loom', ['fabricator', 'lattice-rivet']],
  ['trident-wasp', ['striker', 'trident-spark']],
  ['trident-wasp-volley', ['striker', 'trident-spark']],
  ...[
    'kestrel',
    'palisade',
    'towline',
    'patchbay',
    'lantern',
    'mortar',
    'minesmith',
    'hush',
    'relay',
    'switchback',
    'longshot',
    'mason',
    'sunder',
    'repulsor',
    'veil',
    'nest',
  ].map((classId) => [`arc-${classId}`, [classId, 'arc-pulse']]),
]);

test('internal class looks are genuine tagged SVG packages with paired shots', () => {
  const found = new Set<string>();
  for (const directory of readdirSync(classLooksRoot, {
    withFileTypes: true,
  })) {
    if (!directory.isDirectory()) continue;
    const root = join(classLooksRoot, directory.name);
    const manifest = JSON.parse(
      readFileSync(join(root, 'look.json'), 'utf8'),
    ) as {
      id: string;
      sprite: string;
      classId: string;
      defaultProjectile: string;
      scale: number;
    };
    found.add(manifest.id);
    assert.equal(manifest.id, directory.name);
    assert.equal(manifest.sprite, 'sprite.svg');
    assert.ok(manifest.scale >= 0.8 && manifest.scale <= 1.4);
    assert.deepEqual(
      [manifest.classId, manifest.defaultProjectile],
      expectedLooks.get(manifest.id),
    );

    const source = readFileSync(join(root, manifest.sprite), 'utf8');
    assert.match(source, /viewBox="0 0 512 512"/);
    assert.doesNotMatch(source, /<image\b|data:image\//i);
    const tagged = source.match(
      /<(?:path|circle|ellipse|rect|polygon)\b[^>]*data-team-accent="true"[^>]*>/gi,
    ) ?? [];
    assert.ok(
      tagged.length >= 2 && tagged.length <= 8,
      `${manifest.id} has a restrained semantic accent set`,
    );
    assert.doesNotMatch(
      source,
      /<g\b[^>]*data-team-accent="true"/i,
      `${manifest.id} tags direct surfaces, not inherited groups`,
    );
    for (const element of tagged) {
      assert.match(element, /\bfill="#[0-9a-f]{6}"/i);
      assert.doesNotMatch(element, /\bfill="none"/i);
    }
  }
  assert.deepEqual([...found].sort(), [...expectedLooks.keys()].sort());
});

test('internal class projectiles are compact white-alpha SVG masks', () => {
  const expected = [
    'arc-pulse',
    'lattice-rivet',
    'rebound-diamond',
    'trident-spark',
  ];
  const found: string[] = [];
  for (const directory of readdirSync(projectilesRoot, {
    withFileTypes: true,
  })) {
    if (!directory.isDirectory()) continue;
    const root = join(projectilesRoot, directory.name);
    const manifest = JSON.parse(
      readFileSync(join(root, 'look.json'), 'utf8'),
    ) as { id: string; sprite: string; scale: number };
    found.push(manifest.id);
    assert.equal(manifest.id, directory.name);
    assert.equal(manifest.sprite, 'sprite.svg');
    assert.ok(manifest.scale >= 0.3 && manifest.scale <= 0.7);
    const source = readFileSync(join(root, manifest.sprite), 'utf8');
    assert.match(source, /viewBox="0 0 256 256"/);
    assert.doesNotMatch(source, /<image\b|data:image\//i);
    const colors = [
      ...source.matchAll(/(?:fill|stroke)="(#[0-9a-f]{3,8})"/gi),
    ].map((match) => match[1].toLowerCase());
    assert.ok(colors.length > 0);
    assert.ok(colors.every((color) => color === '#fff'));
  }
  assert.deepEqual(found.sort(), expected);
});

test('semantic team accents replace tagged paint and preserve authored armor', () => {
  const source =
    '<svg><path fill="#334155"/>' +
    '<path data-team-accent="true" fill="#38bdf8" stroke="#e0f2fe"/>' +
    '</svg>';
  assert.equal(
    applyTeamAccentToSvg(source, '#fb923c'),
    '<svg><path fill="#334155"/>' +
      '<path data-team-accent="true" fill="#fb923c" stroke="#fb923c"/>' +
      '</svg>',
  );
  assert.equal(
    applyTeamAccentToSvg(source, 'url(javascript:bad)'),
    source,
    'only a strict replay-safe colour is inserted into SVG markup',
  );
});

test('internal class looks expose typed class metadata without entering cosmetics options', () => {
  assert.equal(presentationBotLook('trident-wasp').classId, 'striker');
  assert.equal(presentationBotLook('aegis-tortoise').classId, 'bulwark');
  assert.equal(presentationBotLook('lattice-loom').classId, 'fabricator');
  assert.equal(presentationBotLook('arc-kestrel').classId, 'kestrel');
  assert.equal(presentationBotLook('arc-nest').classId, 'nest');
});

test('the concept registry preserves three exact pairs per class', () => {
  const document = JSON.parse(
    readFileSync(join(conceptRoot, 'store-bundles.json'), 'utf8'),
  ) as {
    version: number;
    bundles: {
      packId: string;
      classId: string;
      classDefault: boolean;
      availability: string;
      chassis: { id: string; source: string };
      projectile: { id: string; source: string };
    }[];
  };
  assert.equal(document.version, 1);
  assert.equal(document.bundles.length, 9);
  for (const classId of ['striker', 'bulwark', 'fabricator']) {
    const entries = document.bundles.filter(
      (bundle) => bundle.classId === classId,
    );
    assert.equal(entries.length, 3);
    assert.equal(
      entries.filter((bundle) => bundle.classDefault).length,
      1,
    );
  }
  assert.equal(
    document.bundles.filter(
      (bundle) => bundle.availability === 'live-store',
    ).length,
    6,
  );
  for (const bundle of document.bundles) {
    assert.match(bundle.packId, /^(striker|bulwark|fabricator)-/);
    assert.doesNotThrow(() =>
      readFileSync(join(conceptRoot, bundle.chassis.source)),
    );
    assert.doesNotThrow(() =>
      readFileSync(join(conceptRoot, bundle.projectile.source)),
    );
  }
});
