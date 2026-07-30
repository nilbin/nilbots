import assert from 'node:assert/strict';
import { createHash } from 'node:crypto';
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

interface ModelSpec {
  version: number;
  id: string;
  file: string;
  kind: string;
  part: string;
  facing: string;
  up: string;
}

interface GlbAccessor {
  bufferView?: number;
  byteOffset?: number;
  componentType?: number;
  count?: number;
  max?: number[];
  min?: number[];
  type?: string;
}

interface GlbBufferView {
  buffer?: number;
  byteLength?: number;
  byteOffset?: number;
  byteStride?: number;
}

interface GlbMaterial {
  name?: string;
  extras?: {
    nilbotsNeutral?: boolean;
    nilbotsRole?: string;
    nilbotsSourceTextureFree?: boolean;
    nilbotsTeamColourAtRuntime?: boolean;
  };
  emissiveFactor?: number[];
  emissiveTexture?: { index?: number };
  normalTexture?: { index?: number };
  pbrMetallicRoughness?: {
    baseColorFactor?: number[];
    baseColorTexture?: { index?: number };
    metallicRoughnessTexture?: { index?: number };
  };
}

interface GlbPrimitive {
  attributes?: Record<string, number>;
  extras?: { nilbotsRole?: string };
  indices?: number;
  material?: number;
  mode?: number;
}

interface GlbMesh {
  extras?: {
    nilbotsPanelKind?: string;
    nilbotsRole?: string;
    nilbotsSurfaceFitted?: boolean;
    nilbotsTextureFree?: boolean;
  };
  primitives?: GlbPrimitive[];
}

interface GlbNode {
  children?: number[];
  matrix?: number[];
  mesh?: number;
  rotation?: number[];
  scale?: number[];
  translation?: number[];
}

interface GlbDocument {
  accessors?: GlbAccessor[];
  animations?: unknown[];
  bufferViews?: GlbBufferView[];
  cameras?: unknown[];
  extensions?: {
    KHR_lights_punctual?: { lights?: unknown[] };
  };
  images?: {
    bufferView?: number;
    mimeType?: string;
    name?: string;
  }[];
  materials?: GlbMaterial[];
  meshes?: GlbMesh[];
  nodes?: GlbNode[];
  scene?: number;
  scenes?: { nodes?: number[] }[];
  textures?: { source?: number }[];
}

interface ParsedGlb {
  binaryOffset: number;
  bytes: Buffer;
  document: GlbDocument;
}

function modelPath(id: string): string {
  return join(classLooksPath, id, 'model.glb');
}

function modelSpec(id: string): ModelSpec {
  return JSON.parse(
    readFileSync(join(classLooksPath, id, 'model3d.json'), 'utf8'),
  ) as ModelSpec;
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

function sha256(bytes: Buffer): string {
  return createHash('sha256').update(bytes).digest('hex');
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

function readBufferView(glb: ParsedGlb, index: number): Buffer {
  const view = glb.document.bufferViews?.[index];
  assert.ok(view, `missing buffer view ${index}`);
  assert.equal(view.buffer ?? 0, 0);
  const start = glb.binaryOffset + (view.byteOffset ?? 0);
  return glb.bytes.subarray(start, start + (view.byteLength ?? 0));
}

function jpegSize(bytes: Buffer): { height: number; width: number } {
  assert.equal(bytes.readUInt16BE(0), 0xffd8);
  const startOfFrameMarkers = new Set([
    0xc0, 0xc1, 0xc2, 0xc3, 0xc5, 0xc6, 0xc7,
    0xc9, 0xca, 0xcb, 0xcd, 0xce, 0xcf,
  ]);
  let cursor = 2;
  while (cursor + 8 < bytes.length) {
    while (bytes[cursor] === 0xff) cursor += 1;
    const marker = bytes[cursor];
    cursor += 1;
    if (marker === 0xd9 || marker === 0xda) break;
    if (marker === 0xd8 || marker === 0x01) continue;
    const length = bytes.readUInt16BE(cursor);
    if (startOfFrameMarkers.has(marker))
      return {
        height: bytes.readUInt16BE(cursor + 3),
        width: bytes.readUInt16BE(cursor + 5),
      };
    cursor += length;
  }
  throw new Error('JPEG has no start-of-frame marker');
}

function triangleCount(
  document: GlbDocument,
  primitive: GlbPrimitive,
): number {
  assert.equal(primitive.mode ?? 4, 4);
  const indexCount =
    document.accessors?.[primitive.indices ?? -1]?.count ?? 0;
  assert.equal(indexCount % 3, 0);
  return indexCount / 3;
}

function assertNoScenePayload(document: GlbDocument): void {
  assert.deepEqual(document.animations ?? [], []);
  assert.deepEqual(document.cameras ?? [], []);
  assert.deepEqual(
    document.extensions?.KHR_lights_punctual?.lights ?? [],
    [],
  );
}

function worldBoundsY(document: GlbDocument): {
  max: number;
  min: number;
} {
  const nodes = document.nodes ?? [];
  const meshes = document.meshes ?? [];
  const scene = document.scenes?.[document.scene ?? 0];
  assert.ok(scene);
  let min = Number.POSITIVE_INFINITY;
  let max = Number.NEGATIVE_INFINITY;

  function visit(nodeIndex: number, parentY: number): void {
    const node = nodes[nodeIndex];
    assert.ok(node, `missing node ${nodeIndex}`);
    assert.equal(node.matrix, undefined);
    assert.equal(node.rotation, undefined);
    assert.equal(node.scale, undefined);
    const worldY = parentY + (node.translation?.[1] ?? 0);
    if (node.mesh !== undefined) {
      const mesh = meshes[node.mesh];
      assert.ok(mesh, `missing mesh ${node.mesh}`);
      for (const primitive of mesh.primitives ?? []) {
        const position =
          document.accessors?.[primitive.attributes?.POSITION ?? -1];
        assert.ok(position?.min);
        assert.ok(position.max);
        min = Math.min(min, worldY + position.min[1]);
        max = Math.max(max, worldY + position.max[1]);
      }
    }
    for (const child of node.children ?? []) visit(child, worldY);
  }

  for (const node of scene.nodes ?? []) visit(node, 0);
  assert.ok(Number.isFinite(min));
  assert.ok(Number.isFinite(max));
  return { min, max };
}

test('the Bulwark runtime GLB pins the approved 512-tier body and neutral team emitters', () => {
  assert.deepEqual(modelSpec('aegis-tortoise'), {
    version: 1,
    id: 'aegis-tortoise',
    file: 'model.glb',
    kind: 'bot',
    part: 'whole',
    facing: '+x',
    up: '+y',
  });

  const glb = readGlb(modelPath('aegis-tortoise'));
  const { document } = glb;
  assert.equal(glb.bytes.length, 685_744);
  assert.equal(
    sha256(glb.bytes),
    '0d083a34c6d5a947882273dce0b7d18d10364bdf9e36f437c0c94e7f2b4d5c8c',
  );

  const entries = (document.meshes ?? []).flatMap((mesh) =>
    (mesh.primitives ?? []).map((primitive) => ({ mesh, primitive })),
  );
  assert.equal(entries.length, 13);
  const accentEntries = entries.filter(
    ({ mesh, primitive }) =>
      mesh.extras?.nilbotsRole === 'team-accent' &&
      primitive.extras?.nilbotsRole === 'team-accent',
  );
  assert.equal(accentEntries.length, 4);
  assert.deepEqual(
    accentEntries
      .map(({ mesh }) => mesh.extras?.nilbotsPanelKind)
      .sort(),
    [
      'forward-shield-long',
      'forward-shield-long',
      'rear-outer-pod-compact',
      'rear-outer-pod-compact',
    ],
  );
  assert.ok(
    accentEntries.every(
      ({ mesh }) =>
        mesh.extras?.nilbotsSurfaceFitted === true &&
        mesh.extras?.nilbotsTextureFree === true,
    ),
  );

  const accentMaterialIndices = new Set(
    accentEntries.map(({ primitive }) => primitive.material),
  );
  assert.deepEqual([...accentMaterialIndices], [1]);
  const accentMaterial = document.materials?.[1];
  assert.equal(accentMaterial?.name, 'Nilbots Team Accent');
  assert.deepEqual(accentMaterial?.extras, {
    nilbotsNeutral: true,
    nilbotsRole: 'team-accent',
    nilbotsSourceTextureFree: true,
    nilbotsTeamColourAtRuntime: true,
  });
  assert.equal(
    accentMaterial?.pbrMetallicRoughness?.baseColorTexture,
    undefined,
  );
  assert.equal(
    accentMaterial?.pbrMetallicRoughness?.metallicRoughnessTexture,
    undefined,
  );
  assert.equal(accentMaterial?.emissiveTexture, undefined);
  assert.equal(accentMaterial?.normalTexture, undefined);

  const bodyEntries = entries.filter(
    ({ primitive }) => primitive.extras?.nilbotsRole !== 'team-accent',
  );
  assert.equal(bodyEntries.length, 9);
  assert.ok(
    bodyEntries.every(({ primitive }) => primitive.material === 0),
  );
  assert.equal(
    bodyEntries.reduce(
      (sum, { primitive }) => sum + triangleCount(document, primitive),
      0,
    ),
    12_979,
  );
  assert.equal(
    accentEntries.reduce(
      (sum, { primitive }) => sum + triangleCount(document, primitive),
      0,
    ),
    112,
  );
  assert.equal(
    entries.reduce(
      (sum, { primitive }) => sum + triangleCount(document, primitive),
      0,
    ),
    13_091,
  );

  const bodyMaterial = document.materials?.[0];
  const textureIndices = [
    bodyMaterial?.emissiveTexture?.index,
    bodyMaterial?.normalTexture?.index,
    bodyMaterial?.pbrMetallicRoughness?.baseColorTexture?.index,
    bodyMaterial?.pbrMetallicRoughness?.metallicRoughnessTexture?.index,
  ];
  assert.deepEqual(textureIndices, [0, 1, 2, 3]);
  const images = textureIndices.map((textureIndex) => {
    const imageIndex = document.textures?.[textureIndex ?? -1]?.source;
    return document.images?.[imageIndex ?? -1];
  });
  assert.deepEqual(
    images.map((image) => image?.name),
    ['emissive', 'normal', 'base_color', 'metallic_roughness'],
  );
  for (const image of images) {
    assert.equal(image?.mimeType, 'image/jpeg');
    assert.ok(image?.bufferView !== undefined);
    assert.deepEqual(
      jpegSize(readBufferView(glb, image.bufferView)),
      { height: 512, width: 512 },
    );
  }

  const bounds = worldBoundsY(document);
  assert.ok(Math.abs(bounds.min) < 1e-7, `floor is y=${bounds.min}`);
  assertNoScenePayload(document);
});

test('the Fabricator GLB stays lean and exposes exactly four team-panel quadrants', () => {
  assert.deepEqual(modelSpec('lattice-loom'), {
    version: 1,
    id: 'lattice-loom',
    file: 'model.glb',
    kind: 'bot',
    part: 'whole',
    facing: '+x',
    up: '+y',
  });

  const glb = readGlb(modelPath('lattice-loom'));
  const { document } = glb;
  assert.equal(glb.bytes.length, 6_604_048);
  assert.ok(glb.bytes.length <= 7_500_000);
  assert.equal(
    sha256(glb.bytes),
    'e5ada31e067aea1e44d62d561e0b33e1b892a1e883ac7aade7d88993a946bcb1',
  );

  const primitives =
    document.meshes?.flatMap((mesh) => mesh.primitives ?? []) ?? [];
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
    const quadrant =
      `${x < 0 ? 'west' : 'east'}-${z < 0 ? 'south' : 'north'}`;
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

  assertNoScenePayload(document);
});
