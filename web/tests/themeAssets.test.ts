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
    fog?: {
      unseenTerrainDarkness?: number;
    };
    camera?: {
      yawDegrees?: number;
      pitchDegrees?: number;
      fovDegrees?: number;
    };
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
      colorMultiplier?: string;
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
          bastionCover?: {
            shoulderEvery: number;
            towerEvery: number;
            minRise: number;
            maxRise: number;
            boundaryDepth: number;
            bodyColor: string;
            trimColor: string;
            heatColor: string;
          };
          treeCover?: {
            trunkHeight: number;
            trunkRadius: number;
            canopyRadius: number;
            canopyHeight: number;
            canopyLobes: number;
            density: number;
            rootSpread: number;
            minHeightScale: number;
            maxHeightScale: number;
            minWidthScale: number;
            maxWidthScale: number;
            boundaryDepth: number;
            trunkColor: string;
            canopyColor: string;
            canopyAccentColor: string;
          };
        };
        material3d?: {
          normalMap?: string;
          roughnessMap?: string;
          normalScale?: number;
          roughness?: number;
          metalness?: number;
          colorMultiplier?: string;
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
        family.geometry3d.height >=
          (family.geometry3d.treeCover ? 0 : 0.25) &&
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
      if (family.geometry3d.bastionCover) {
        const bastion = family.geometry3d.bastionCover;
        assert.ok(Number.isInteger(bastion.shoulderEvery));
        assert.ok(Number.isInteger(bastion.towerEvery));
        assert.ok(bastion.minRise < bastion.maxRise);
        assert.ok(bastion.boundaryDepth >= 0 && bastion.boundaryDepth <= 2);
        for (const color of [
          bastion.bodyColor,
          bastion.trimColor,
          bastion.heatColor,
        ])
          assert.match(color, /^#[0-9a-f]{6}$/i);
      }
      if (family.geometry3d.treeCover) {
        const tree = family.geometry3d.treeCover;
        assert.ok(tree.trunkHeight >= 0.18 && tree.trunkHeight <= 0.65);
        assert.ok(tree.trunkRadius >= 0.04 && tree.trunkRadius <= 0.14);
        assert.ok(tree.canopyRadius >= 0.12 && tree.canopyRadius <= 0.3);
        assert.ok(tree.canopyHeight >= 0.08 && tree.canopyHeight <= 0.22);
        assert.ok(Number.isInteger(tree.canopyLobes));
        assert.ok(tree.density >= 0.3 && tree.density <= 2);
        assert.ok(tree.rootSpread >= 0.05 && tree.rootSpread <= 0.45);
        assert.ok(tree.minHeightScale < tree.maxHeightScale);
        assert.ok(tree.minWidthScale < tree.maxWidthScale);
        assert.ok(tree.boundaryDepth >= 0 && tree.boundaryDepth <= 2.5);
        for (const color of [
          tree.trunkColor,
          tree.canopyColor,
          tree.canopyAccentColor,
        ])
          assert.match(color, /^#[0-9a-f]{6}$/i);
      }
    }
    if (family.material3d) {
      if (family.material3d.colorMultiplier)
        assert.match(
          family.material3d.colorMultiplier,
          /^#[0-9a-f]{6}$/i,
        );
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
    'woodland-grove',
  ]);
  for (const id of activeIds) assertCompletePackage(join(activeRoot, id), id);
});

test('future map themes remain complete staged packages', () => {
  for (const id of ['desert-array', 'drowned-vault', 'void-sanctum'])
    assertCompletePackage(join(stagedRoot, id, 'runtime'), id);
});

test('Ember Forge carries the approved living-bastion profiles', () => {
  const manifest = JSON.parse(
    readFileSync(join(activeRoot, 'ember-forge', 'theme.json'), 'utf8'),
  ) as ThemeManifest;
  assert.equal(
    manifest.walls.families.perimeter.geometry3d?.height,
    0.84,
  );
  assert.equal(
    manifest.walls.families.perimeter.geometry3d?.cornerRadius,
    0.29,
  );
  assert.deepEqual(
    manifest.walls.families.perimeter.geometry3d?.upperProfile,
    { height: 0.25, inset: 0.035, chamfer: 0.045 },
  );
  assert.equal(
    manifest.walls.families.cover.geometry3d?.height,
    0.58,
  );
  assert.equal(
    manifest.walls.families.cover.geometry3d?.cornerRadius,
    0.35,
  );
  assert.deepEqual(
    manifest.walls.families.cover.geometry3d?.upperProfile,
    { height: 0.18, inset: 0.05, chamfer: 0.035 },
  );
  assert.equal(
    manifest.environment3d?.floor?.roughness,
    0.98,
  );
  assert.equal(
    manifest.environment3d?.floor?.bumpScale,
    0.028,
  );
  assert.equal(
    manifest.environment3d?.floor?.colorMultiplier,
    '#c7c7c7',
  );
  assert.equal(manifest.environment3d?.camera?.yawDegrees, 12);
  assert.equal(manifest.environment3d?.camera?.pitchDegrees, 52);
  assert.equal(manifest.environment3d?.camera?.fovDegrees, 28);
  assert.equal(
    manifest.environment3d?.fog?.unseenTerrainDarkness,
    0.2,
  );
  assert.equal(
    manifest.walls.families.perimeter.geometry3d?.bastionCover
      ?.boundaryDepth,
    1.8,
  );
  assert.equal(
    manifest.walls.families.perimeter.geometry3d?.bastionCover
      ?.chimneyEvery,
    6,
  );
  assert.equal(
    manifest.walls.families.perimeter.geometry3d?.bastionCover
      ?.furnaceEvery,
    5,
  );
  assert.equal(
    manifest.walls.families.perimeter.geometry3d?.bastionCover
      ?.accentColor,
    '#9b3043',
  );
  assert.equal(
    manifest.walls.families.cover.geometry3d?.bastionCover
      ?.boundaryDepth,
    0,
  );
  assert.equal(
    manifest.walls.families.cover.geometry3d?.bastionCover
      ?.chimneyEvery,
    null,
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

test('Woodland Grove carries a MOBA camera and organic tree families', () => {
  const manifest = JSON.parse(
    readFileSync(join(activeRoot, 'woodland-grove', 'theme.json'), 'utf8'),
  ) as ThemeManifest;
  const perimeter = manifest.walls.families.perimeter.geometry3d!;
  const cover = manifest.walls.families.cover.geometry3d!;
  assert.equal(manifest.environment3d?.camera?.yawDegrees, 12);
  assert.equal(manifest.environment3d?.camera?.pitchDegrees, 52);
  assert.equal(manifest.environment3d?.camera?.fovDegrees, 26);
  assert.equal(perimeter.height, 0);
  assert.equal(cover.height, 0);
  assert.ok(perimeter.treeCover);
  assert.ok(cover.treeCover);
  assert.ok(
    perimeter.treeCover.trunkHeight > cover.treeCover.trunkHeight,
  );
  assert.ok(
    perimeter.treeCover.canopyRadius > cover.treeCover.canopyRadius,
  );
  assert.ok(
    perimeter.treeCover.maxHeightScale >
      perimeter.treeCover.minHeightScale,
  );
  assert.ok(
    perimeter.treeCover.maxWidthScale >
      perimeter.treeCover.minWidthScale,
  );
  assert.ok(perimeter.treeCover.boundaryDepth > 1);
  assert.equal(cover.treeCover.boundaryDepth, 0);
  assert.ok(cover.treeCover.maxHeightScale >= 1.5);
  assert.ok(cover.treeCover.maxWidthScale >= 1.5);
  assert.ok(cover.treeCover.trunkHeight >= 0.5);
  assert.ok(
    perimeter.height +
      perimeter.treeCover.trunkHeight *
        perimeter.treeCover.maxHeightScale +
      perimeter.treeCover.canopyHeight * 2.4 <=
      1.8,
  );
  assert.ok(
    cover.height +
      cover.treeCover.trunkHeight * cover.treeCover.maxHeightScale +
      cover.treeCover.canopyHeight * 2.4 <=
      1.8,
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
