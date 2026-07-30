import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import test from 'node:test';
import { fileURLToPath } from 'node:url';

const strikerModelPath = join(
  dirname(fileURLToPath(import.meta.url)),
  '..',
  'src',
  'assets',
  'class-looks',
  'trident-wasp',
  'model.glb',
);
const aegisModelPath = join(
  dirname(fileURLToPath(import.meta.url)),
  '..',
  'src',
  'assets',
  'class-looks',
  'aegis-tortoise',
  'model.glb',
);

interface GlbDocument {
  accessors?: { count?: number }[];
  animations?: {
    name?: string;
    channels?: unknown[];
    samplers?: unknown[];
  }[];
  materials?: {
    name?: string;
    extras?: { nilbotsRole?: string };
    emissiveTexture?: unknown;
    normalTexture?: unknown;
    pbrMetallicRoughness?: {
      baseColorFactor?: number[];
      baseColorTexture?: unknown;
      metallicRoughnessTexture?: unknown;
    };
  }[];
  meshes?: {
    primitives?: {
      attributes?: Record<string, number>;
      extras?: { nilbotsRole?: string };
      indices?: number;
      material?: number;
      mode?: number;
    }[];
  }[];
  skins?: { joints?: number[] }[];
}

function readGlbDocument(path: string): GlbDocument {
  const bytes = readFileSync(path);
  assert.equal(bytes.toString('ascii', 0, 4), 'glTF');
  assert.equal(bytes.readUInt32LE(4), 2);
  assert.equal(bytes.readUInt32LE(8), bytes.length);
  assert.equal(bytes.readUInt32LE(16), 0x4e4f534a);

  const jsonLength = bytes.readUInt32LE(12);
  return JSON.parse(
    bytes
      .subarray(20, 20 + jsonLength)
      .toString('utf8')
      .replace(/[\u0000\u0020]+$/u, ''),
  ) as GlbDocument;
}

test('the approved Striker GLB keeps hull detail and one renderer-owned team surface', () => {
  const document = readGlbDocument(strikerModelPath);
  const primitives = document.meshes?.flatMap(
    (mesh) => mesh.primitives ?? [],
  ) ?? [];
  assert.equal(primitives.length, 2);

  const hull = primitives.find(
    (primitive) => primitive.extras?.nilbotsRole === 'hull',
  );
  const accent = primitives.find(
    (primitive) => primitive.extras?.nilbotsRole === 'team-accent',
  );
  assert.ok(hull);
  assert.ok(accent);
  assert.deepEqual(accent.attributes, hull.attributes);
  assert.equal(hull.mode, 4);
  assert.equal(accent.mode, 4);

  const hullMaterial = document.materials?.[hull.material ?? -1];
  const accentMaterial = document.materials?.[accent.material ?? -1];
  assert.equal(hullMaterial?.name, 'Nilbots Hull');
  assert.ok(hullMaterial?.pbrMetallicRoughness?.baseColorTexture);
  assert.ok(hullMaterial?.emissiveTexture);
  assert.ok(hullMaterial?.normalTexture);
  assert.ok(
    hullMaterial?.pbrMetallicRoughness?.metallicRoughnessTexture,
  );

  assert.equal(accentMaterial?.name, 'Nilbots Team Accent');
  assert.equal(accentMaterial?.extras?.nilbotsRole, 'team-accent');
  assert.deepEqual(
    accentMaterial?.pbrMetallicRoughness?.baseColorFactor,
    [1, 1, 1, 1],
  );
  assert.equal(
    accentMaterial?.pbrMetallicRoughness?.baseColorTexture,
    undefined,
  );
  assert.equal(accentMaterial?.emissiveTexture, undefined);
  assert.ok(accentMaterial?.normalTexture);
  assert.ok(
    accentMaterial?.pbrMetallicRoughness?.metallicRoughnessTexture,
  );

  const hullIndexCount =
    document.accessors?.[hull.indices ?? -1]?.count ?? 0;
  const accentIndexCount =
    document.accessors?.[accent.indices ?? -1]?.count ?? 0;
  assert.equal((hullIndexCount + accentIndexCount) % 3, 0);
  assert.ok(hullIndexCount > accentIndexCount);
  assert.ok(accentIndexCount > 0);
});

test('the approved Aegis GLB carries its exact rig clips and renderer-owned team surface', () => {
  const document = readGlbDocument(aegisModelPath);
  assert.deepEqual(
    document.animations?.map((animation) => animation.name),
    ['Aegis_Idle', 'Aegis_Walk'],
  );
  for (const animation of document.animations ?? []) {
    assert.equal(animation.channels?.length, 45);
    assert.equal(animation.samplers?.length, 45);
  }
  assert.equal(document.skins?.length, 1);
  assert.equal(document.skins?.[0].joints?.length, 15);
  assert.equal(
    document.materials?.filter(
      (material) =>
        material.extras?.nilbotsRole === 'team-accent',
    ).length,
    1,
  );
});
