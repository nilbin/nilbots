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
  environment3d?: {
    lighting?: {
      keyColor?: string;
      keyIntensity?: number;
      ambientColor?: string;
      ambientIntensity?: number;
      fillColor?: string;
      fillIntensity?: number;
    };
    floor?: {
      bumpScale?: number;
      roughness?: number;
      metalness?: number;
    };
  };
  textures: { floor: string };
  walls: {
    families: Record<
      string,
      {
        material: string;
        edgeAtlas: string;
        shadowAtlas: string;
        geometry3d?: {
          height: number;
          cornerRadius: number;
          upperProfile?: {
            height: number;
            inset: number;
            chamfer: number;
          };
          details?: {
            panelEvery: number;
            ventEvery: number;
            clampEvery: number;
            panelColor: string;
            clampColor: string;
            ventColor: string;
          };
        };
        material3d?: {
          normalMap?: string;
          roughnessMap?: string;
          normalScale?: number;
          roughness?: number;
          metalness?: number;
        };
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
    if (family.geometry3d) {
      assert.ok(
        family.geometry3d.height >= 0.25 &&
          family.geometry3d.height <= 0.9,
        `wall height ${family.geometry3d.height} is outside the runtime contract`,
      );
      assert.ok(
        family.geometry3d.cornerRadius >= 0 &&
          family.geometry3d.cornerRadius <= 0.4,
        `wall radius ${family.geometry3d.cornerRadius} is outside the runtime contract`,
      );
      if (family.geometry3d.upperProfile) {
        assert.ok(family.geometry3d.upperProfile.height > 0);
        assert.ok(family.geometry3d.upperProfile.inset >= 0);
        assert.ok(family.geometry3d.upperProfile.chamfer > 0);
      }
      if (family.geometry3d.details) {
        for (const frequency of [
          family.geometry3d.details.panelEvery,
          family.geometry3d.details.ventEvery,
          family.geometry3d.details.clampEvery,
        ])
          assert.ok(Number.isInteger(frequency) && frequency > 0);
      }
    }
    if (family.material3d) {
      for (const asset of [
        family.material3d.normalMap,
        family.material3d.roughnessMap,
      ])
        if (asset)
          assert.doesNotThrow(() => readFileSync(join(root, asset)));
    }
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

test('Ember Forge carries the approved Frontline 3D wall profiles', () => {
  const manifest = JSON.parse(
    readFileSync(join(activeRoot, 'ember-forge', 'theme.json'), 'utf8'),
  ) as ThemeManifest;
  assert.equal(
    manifest.walls.families.perimeter.geometry3d?.height,
    0.72,
  );
  assert.equal(
    manifest.walls.families.perimeter.geometry3d?.cornerRadius,
    0.23,
  );
  assert.deepEqual(
    manifest.walls.families.perimeter.geometry3d?.upperProfile,
    { height: 0.19, inset: 0.025, chamfer: 0.035 },
  );
  assert.equal(
    manifest.walls.families.cover.geometry3d?.height,
    0.46,
  );
  assert.equal(
    manifest.walls.families.cover.geometry3d?.cornerRadius,
    0.31,
  );
  assert.deepEqual(
    manifest.walls.families.cover.geometry3d?.upperProfile,
    { height: 0.14, inset: 0.04, chamfer: 0.03 },
  );
  assert.equal(
    manifest.environment3d?.floor?.roughness,
    0.95,
  );
  assert.equal(
    manifest.environment3d?.floor?.bumpScale,
    0.045,
  );
  assert.equal(
    manifest.walls.families.perimeter.material3d?.normalMap,
    'pbr/wall-perimeter-normal.webp',
  );
  assert.equal(
    manifest.walls.families.perimeter.material3d?.roughnessMap,
    'pbr/wall-perimeter-roughness.webp',
  );
  assert.equal(
    manifest.walls.families.cover.material3d?.normalMap,
    'pbr/wall-cover-normal.webp',
  );
  assert.equal(
    manifest.walls.families.cover.material3d?.roughnessMap,
    'pbr/wall-cover-roughness.webp',
  );
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
