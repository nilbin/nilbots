import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import test from 'node:test';
import { fileURLToPath } from 'node:url';

const classLooksPath = join(
  dirname(fileURLToPath(import.meta.url)),
  '..',
  'src',
  'assets',
  'class-looks',
);
const strikerModelPath = join(
  classLooksPath,
  'trident-wasp',
  'model.glb',
);
const fabricatorModelPath = join(
  classLooksPath,
  'lattice-loom',
const aegisModelPath = join(
  classLooksPath,
  'aegis-tortoise',
  'model.glb',
);

interface GlbDocument {
  accessors?: {
    bufferView?: number;
    byteOffset?: number;
    componentType?: number;
    count?: number;
    max?: number[];
    min?: number[];
    type?: string;
  }[];
  animations?: {
    name?: string;
    channels?: unknown[];
    samplers?: unknown[];
  }[];
  bufferViews?: {
    buffer?: number;
    byteLength?: number;
    byteOffset?: number;
    byteStride?: number;
  }[];
  cameras?: unknown[];
  extensions?: {
    KHR_lights_punctual?: { lights?: unknown[] };
  };
  materials?: {
    name?: string;
    extras?: { nilbotsRole?: string };
    emissiveFactor?: number[];
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

interface ParsedGlb {
  bytes: Buffer;
  document: GlbDocument;
  binaryOffset: number;
}

function readGlb(path: string): ParsedGlb {
  const bytes = readFileSync(path);
  assert.equal(bytes.toString('ascii', 0, 4), 'glTF');
  assert.equal(bytes.readUInt32LE(4), 2);
  assert.equal(bytes.readUInt32LE(8), bytes.length);
  assert.equal(bytes.readUInt32LE(16), 0x4e4f534a);

  const jsonLength = bytes.readUInt32LE(12);
  const document = JSON.parse(
    bytes
      .subarray(20, 20 + jsonLength)
      .toString('utf8')
      .replace(/[\u0000\u0020]+$/u, ''),
  ) as GlbDocument;
  const binaryHeader = 20 + jsonLength;
  assert.equal(bytes.readUInt32LE(binaryHeader + 4), 0x004e4942);
  return { bytes, document, binaryOffset: binaryHeader + 8 };
}

function readGlbDocument(path: string): GlbDocument {
  return readGlb(path).document;
}

function accessorComponents(type: string | undefined): number {
  switch (type) {
    case 'SCALAR':
      return 1;
    case 'VEC2':
      return 2;
    case 'VEC3':
      return 3;
    case 'VEC4':
      return 4;
    default:
      throw new Error(`Unsupported accessor type ${type}`);
  }
}

function readAccessor(glb: ParsedGlb, index: number): number[][] {
  const accessor = glb.document.accessors?.[index];
  assert.ok(accessor, `missing accessor ${index}`);
  const view = glb.document.bufferViews?.[accessor.bufferView ?? -1];
  assert.ok(view, `missing buffer view for accessor ${index}`);
  assert.equal(view.buffer ?? 0, 0);
  const components = accessorComponents(accessor.type);
  const componentBytes =
    accessor.componentType === 5125 || accessor.componentType === 5126
      ? 4
      : 0;
  assert.equal(
    componentBytes,
    4,
    `unsupported component type ${accessor.componentType}`,
  );
  const stride = view.byteStride ?? components * componentBytes;
  const start =
    glb.binaryOffset +
    (view.byteOffset ?? 0) +
    (accessor.byteOffset ?? 0);

  return Array.from({ length: accessor.count ?? 0 }, (_, element) =>
    Array.from({ length: components }, (_, component) => {
      const offset = start + element * stride + component * componentBytes;
      return accessor.componentType === 5125
        ? glb.bytes.readUInt32LE(offset)
        : glb.bytes.readFloatLE(offset);
    }),
  );
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

test('the Fabricator GLB stays lean and exposes exactly four team-panel quadrants', () => {
  const glb = readGlb(fabricatorModelPath);
  const { document } = glb;
  assert.equal(glb.bytes.length, 6_604_048);
  assert.ok(glb.bytes.length <= 7_500_000);

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
  assert.equal(hull.attributes?.TANGENT, 3);
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
  assert.deepEqual(accentMaterial?.emissiveFactor, [1, 1, 1]);
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
  assert.equal(hullIndexCount, 224_607);
  assert.equal(accentIndexCount, 393);
  assert.equal((hullIndexCount + accentIndexCount) / 3, 75_000);

  const positionAccessor =
    document.accessors?.[hull.attributes?.POSITION ?? -1];
  assert.deepEqual(positionAccessor?.min, [
    -0.5400000214576721,
    0,
    -0.46545326709747314,
  ]);
  assert.deepEqual(positionAccessor?.max, [
    0.5400000214576721,
    0.21867996454238892,
    0.46545326709747314,
  ]);

  const positions = readAccessor(glb, hull.attributes?.POSITION ?? -1);
  const accentIndices = readAccessor(glb, accent.indices ?? -1).flat();
  const quadrantCounts = new Map<string, number>();
  for (let cursor = 0; cursor < accentIndices.length; cursor += 3) {
    const triangle = accentIndices
      .slice(cursor, cursor + 3)
      .map((index) => positions[index]);
    const x =
      (triangle[0][0] + triangle[1][0] + triangle[2][0]) / 3;
    const z =
      (triangle[0][2] + triangle[1][2] + triangle[2][2]) / 3;
    assert.ok(Math.abs(x) > 0.1, `accent leaked toward the core at x=${x}`);
    assert.ok(Math.abs(z) > 0.18, `accent leaked toward the core at z=${z}`);
    const quadrant = `${x < 0 ? 'west' : 'east'}-${z < 0 ? 'south' : 'north'}`;
    quadrantCounts.set(
      quadrant,
      (quadrantCounts.get(quadrant) ?? 0) + 1,
    );
  }
  assert.deepEqual(
    [...quadrantCounts.entries()].sort(([left], [right]) =>
      left.localeCompare(right),
    ),
    [
      ['east-north', 33],
      ['east-south', 40],
      ['west-north', 29],
      ['west-south', 29],
    ],
  );

  assert.deepEqual(document.animations ?? [], []);
  assert.deepEqual(document.cameras ?? [], []);
  assert.deepEqual(
    document.extensions?.KHR_lights_punctual?.lights ?? [],
    [],
  );
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
