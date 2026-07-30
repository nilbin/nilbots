import assert from 'node:assert/strict';
import { createHash } from 'node:crypto';
import { readFileSync, statSync } from 'node:fs';
import { dirname, join } from 'node:path';
import test from 'node:test';
import { fileURLToPath } from 'node:url';

const assetRoot = join(
  dirname(fileURLToPath(import.meta.url)),
  '..',
  'src',
  'assets',
  'class-projectile-looks',
);

const candidates = [
  {
    id: 'trident-spark',
    modelBytes: 301_476,
    modelSha256:
      '7775011b33450bcd782f5ffcc5e67923641be19b0b4c46d59b2d08712cd1a82c',
    manifestSha256:
      'a00d8b58f6a563a7f169f99b8a2bb2de0ee154d2c0a8f906d5d88895aa46504c',
    triangles: 1_468,
    rootNode: 'TridentSpark.Root.Hardpoint',
  },
  {
    id: 'rebound-diamond',
    modelBytes: 301_504,
    modelSha256:
      '6dbb7f123d77ab3a69d470e2480df5e5e717a43f130878f66aab8b83727049a9',
    manifestSha256:
      'c442fe0f39e123fcf14af28d5883d417febc0f3b4d7bcdd276be5b835b4c33fe',
    triangles: 1_436,
    rootNode: 'ReboundDiamond.Root.OpenRing',
  },
  {
    id: 'lattice-rivet',
    modelBytes: 468_276,
    modelSha256:
      '73606f2a94c9d848f2dab8c9a4b81214643eef06c2d15481e277e8cc14c92354',
    manifestSha256:
      '9686b17b0f61aa35ca7142956415a5a575cf6772cfa5d783567fdbcbf22db1fb',
    triangles: 2_824,
    rootNode: 'LatticeRivet.Root.SingleBody',
  },
] as const;

interface ModelManifest {
  version: number;
  id: string;
  file: string;
  kind: string;
  part: string;
  facing: string;
  up: string;
}

interface GlbAccessor {
  count?: number;
  max?: number[];
  min?: number[];
}

interface GlbBufferView {
  byteLength?: number;
  byteOffset?: number;
}

interface GlbDocument {
  accessors?: GlbAccessor[];
  animations?: unknown[];
  asset?: { generator?: string; version?: string };
  bufferViews?: GlbBufferView[];
  cameras?: unknown[];
  extensions?: Record<string, unknown>;
  images?: {
    bufferView?: number;
    mimeType?: string;
    name?: string;
  }[];
  materials?: {
    extras?: {
      nilbotsPalette?: string;
      nilbotsRole?: string;
    };
    emissiveTexture?: { index?: number };
    normalTexture?: unknown;
    pbrMetallicRoughness?: {
      baseColorTexture?: { index?: number };
      metallicRoughnessTexture?: unknown;
    };
  }[];
  meshes?: {
    primitives?: {
      attributes?: Record<string, number>;
      indices?: number;
      material?: number;
      mode?: number;
    }[];
  }[];
  nodes?: {
    mesh?: number;
    name?: string;
  }[];
  skins?: unknown[];
  textures?: {
    source?: number;
  }[];
}

interface ParsedGlb {
  bytes: Buffer;
  binaryOffset: number;
  document: GlbDocument;
}

function sha256(bytes: Buffer): string {
  return createHash('sha256').update(bytes).digest('hex');
}

function readGlb(path: string): ParsedGlb {
  const bytes = readFileSync(path);
  assert.equal(bytes.toString('ascii', 0, 4), 'glTF');
  assert.equal(bytes.readUInt32LE(4), 2);
  assert.equal(bytes.readUInt32LE(8), bytes.length);
  assert.equal(bytes.readUInt32LE(16), 0x4e4f534a);

  const jsonLength = bytes.readUInt32LE(12);
  const binaryHeader = 20 + jsonLength;
  assert.equal(bytes.readUInt32LE(binaryHeader + 4), 0x004e4942);

  return {
    bytes,
    binaryOffset: binaryHeader + 8,
    document: JSON.parse(
      bytes
        .subarray(20, 20 + jsonLength)
        .toString('utf8')
        .replace(/[\u0000\u0020]+$/u, ''),
    ) as GlbDocument,
  };
}

function embeddedPngSize(
  glb: ParsedGlb,
  image: NonNullable<GlbDocument['images']>[number],
): readonly [number, number] {
  assert.equal(image.mimeType, 'image/png');
  const view = glb.document.bufferViews?.[image.bufferView ?? -1];
  assert.ok(view);
  const start = glb.binaryOffset + (view.byteOffset ?? 0);
  assert.deepEqual(
    [...glb.bytes.subarray(start, start + 8)],
    [137, 80, 78, 71, 13, 10, 26, 10],
  );
  return [
    glb.bytes.readUInt32BE(start + 16),
    glb.bytes.readUInt32BE(start + 20),
  ];
}

for (const candidate of candidates) {
  test(`${candidate.id} pins its projectile model manifest`, () => {
    const manifestBytes = readFileSync(
      join(assetRoot, candidate.id, 'model3d.json'),
    );
    assert.equal(sha256(manifestBytes), candidate.manifestSha256);
    assert.deepEqual(JSON.parse(manifestBytes.toString('utf8')), {
      version: 1,
      id: candidate.id,
      file: 'model.glb',
      kind: 'projectile',
      part: 'whole',
      facing: '+x',
      up: '+y',
    } satisfies ModelManifest);
  });

  test(`${candidate.id} is a deterministic, runtime-budget projectile GLB`, () => {
    const path = join(assetRoot, candidate.id, 'model.glb');
    const glb = readGlb(path);
    const { document } = glb;
    const primitives =
      document.meshes?.flatMap((mesh) => mesh.primitives ?? []) ?? [];

    assert.equal(statSync(path).size, candidate.modelBytes);
    assert.equal(sha256(glb.bytes), candidate.modelSha256);
    assert.ok(candidate.modelBytes < 500_000);
    assert.equal(document.asset?.version, '2.0');
    assert.match(document.asset?.generator ?? '', /Blender/i);
    assert.equal(document.animations, undefined);
    assert.equal(document.cameras, undefined);
    assert.equal(document.skins, undefined);
    assert.equal(document.extensions?.KHR_lights_punctual, undefined);

    assert.ok(primitives.length > 0);
    let indexCount = 0;
    for (const primitive of primitives) {
      assert.ok(primitive.mode === undefined || primitive.mode === 4);
      assert.ok(primitive.indices !== undefined);
      assert.ok(primitive.material !== undefined);
      assert.ok(primitive.attributes?.POSITION !== undefined);
      assert.ok(primitive.attributes?.NORMAL !== undefined);
      assert.ok(primitive.attributes?.TANGENT !== undefined);
      assert.ok(primitive.attributes?.TEXCOORD_0 !== undefined);
      indexCount += document.accessors?.[primitive.indices]?.count ?? 0;
    }
    assert.equal(indexCount / 3, candidate.triangles);

    assert.ok((document.materials?.length ?? 0) >= 3);
    for (const material of document.materials ?? []) {
      assert.equal(material.extras?.nilbotsRole, 'projectile-mask');
      assert.equal(
        material.extras?.nilbotsPalette,
        'team-neutral-grayscale',
      );
      assert.ok(material.pbrMetallicRoughness?.baseColorTexture);
      assert.ok(material.pbrMetallicRoughness?.metallicRoughnessTexture);
      assert.ok(material.normalTexture);
      assert.ok(material.emissiveTexture);
      const albedoSource =
        document.textures?.[
          material.pbrMetallicRoughness?.baseColorTexture?.index ?? -1
        ]?.source;
      const emissionSource =
        document.textures?.[material.emissiveTexture.index ?? -1]?.source;
      assert.ok(albedoSource !== undefined);
      assert.equal(emissionSource, albedoSource);
      assert.match(
        document.images?.[albedoSource]?.name ?? '',
        /^surface-albedo-(?:core|dark|mid)$/u,
      );
    }

    assert.equal(document.images?.length, 5);
    for (const image of document.images ?? [])
      assert.deepEqual(embeddedPngSize(glb, image), [128, 128]);

    const rootNode = document.nodes?.find(
      (node) => node.name === candidate.rootNode,
    );
    assert.ok(rootNode);
    const rootPosition = document.meshes?.[rootNode.mesh ?? -1]
      ?.primitives?.[0]?.attributes?.POSITION;
    assert.ok(rootPosition !== undefined);
    const rootBounds = document.accessors?.[rootPosition];
    assert.ok(rootBounds?.min);
    assert.ok(rootBounds.max);
    assert.ok((rootBounds.min[1] ?? Number.NaN) >= -1e-6);
    assert.ok((rootBounds.max[1] ?? 0) >= 0.07);
    assert.ok((rootBounds.max[1] ?? Number.POSITIVE_INFINITY) <= 0.09);
    assert.ok(
      (rootBounds.max[0] ?? 0) - (rootBounds.min[0] ?? 0) >= 0.98,
    );
    assert.ok(
      (rootBounds.max[0] ?? 0) - (rootBounds.min[0] ?? 0) <= 1.01,
    );
  });
}

