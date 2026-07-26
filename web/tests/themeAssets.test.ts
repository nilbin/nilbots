import assert from 'node:assert/strict';
import { readdirSync, readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import test from 'node:test';

const repositoryRoot = join(
  dirname(fileURLToPath(import.meta.url)),
  '..',
  '..',
);
const activeRoot = join(repositoryRoot, 'web', 'src', 'assets', 'themes');
const stagedRoot = join(repositoryRoot, 'art', 'themes');

type ThemeManifest = {
  id: string;
  textures: { floor: string };
  walls: {
    families: Record<
      string,
      {
        material: string;
        edgeAtlas: string;
        shadowAtlas: string;
      }
    >;
  };
};

function assertCompletePackage(root: string, expectedId: string) {
  const manifest = JSON.parse(
    readFileSync(join(root, 'theme.json'), 'utf8'),
  ) as ThemeManifest;
  assert.equal(manifest.id, expectedId);
  assert.doesNotThrow(() =>
    readFileSync(join(root, manifest.textures.floor)),
  );
  for (const family of Object.values(manifest.walls.families)) {
    for (const asset of [
      family.material,
      family.edgeAtlas,
      family.shadowAtlas,
    ])
      assert.doesNotThrow(() => readFileSync(join(root, asset)));
  }
}

test('only active map themes enter the web bundle', () => {
  const activeIds = readdirSync(activeRoot, { withFileTypes: true })
    .filter((entry) => entry.isDirectory())
    .map((entry) => entry.name)
    .sort();
  assert.deepEqual(activeIds, [
    'control-room',
    'ember-forge',
    'frost-relay',
    'overgrown-lab',
  ]);
  for (const id of activeIds) assertCompletePackage(join(activeRoot, id), id);
});

test('future map themes remain complete staged packages', () => {
  for (const id of ['desert-array', 'drowned-vault', 'void-sanctum'])
    assertCompletePackage(join(stagedRoot, id, 'runtime'), id);
});

test('theme registration does not eagerly decode high-resolution atlases', () => {
  const source = readFileSync(
    join(repositoryRoot, 'web', 'src', 'render', 'arenaThemes.ts'),
    'utf8',
  );
  const registration = source.slice(
    source.indexOf('function buildThemes'),
    source.indexOf('function buildLooks'),
  );
  assert.match(registration, /\blazyImage\(/);
  assert.doesNotMatch(
    registration,
    /\bloadImage\(/,
    'Decode theme images only when the replay asks for that theme.',
  );
});
