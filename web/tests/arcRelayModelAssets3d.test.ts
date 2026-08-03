import assert from 'node:assert/strict';
import { createHash } from 'node:crypto';
import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import test from 'node:test';
import { fileURLToPath } from 'node:url';

const repository = join(dirname(fileURLToPath(import.meta.url)), '..', '..');
const looksRoot = join(repository, 'web', 'src', 'assets', 'class-looks');
const ledger = JSON.parse(
  readFileSync(
    join(repository, 'art', 'class-models', 'arc-relay', 'ledger.json'),
    'utf8',
  ),
) as FleetLedger;

const expectedGroups = [
  'underbody-locomotion',
  'chassis',
  'weapon-hardware',
  'team-accents',
  'emissives',
];

interface FleetLedger {
  budgetBytesPerLook: number;
  totals: { bytes: number; triangles: number; materials: number; textures: number };
  looks: {
    id: string;
    bytes: number;
    sha256: string;
    triangles: number;
    materials: number;
    textures: { role: string; width: number; height: number; bytes: number }[];
    bounds: { min: number[]; max: number[]; planformSpan: number; floorY: number };
  }[];
}

interface GlbDocument {
  asset?: {
    generator?: string;
    extras?: {
      provider?: string;
      source?: string;
      sourceProjectionDegrees?: number;
      archivedMasterProjectionDegrees?: number;
      requiredGroups?: string[];
    };
  };
  accessors?: { count?: number; min?: number[]; max?: number[] }[];
  animations?: unknown[];
  cameras?: unknown[];
  images?: { mimeType?: string; bufferView?: number }[];
  materials?: {
    extras?: { nilbotsRole?: string };
    normalTexture?: unknown;
    pbrMetallicRoughness?: { baseColorTexture?: unknown };
  }[];
  meshes?: { primitives?: { attributes?: Record<string, number>; indices?: number; material?: number; mode?: number }[] }[];
  nodes?: { name?: string; children?: number[]; mesh?: number; camera?: number; skin?: number }[];
  scenes?: { nodes?: number[] }[];
  skins?: unknown[];
  bufferViews?: { byteOffset?: number; byteLength?: number }[];
}

function readGlb(path: string): { bytes: Buffer; document: GlbDocument; binaryOffset: number } {
  const bytes = readFileSync(path);
  assert.equal(bytes.toString('ascii', 0, 4), 'glTF', path);
  assert.equal(bytes.readUInt32LE(4), 2, path);
  assert.equal(bytes.readUInt32LE(8), bytes.length, path);
  assert.equal(bytes.readUInt32LE(16), 0x4e4f534a, path);
  const jsonLength = bytes.readUInt32LE(12);
  const binaryHeader = 20 + jsonLength;
  assert.equal(bytes.readUInt32LE(binaryHeader + 4), 0x004e4942, path);
  return {
    bytes,
    document: JSON.parse(
      bytes.subarray(20, binaryHeader).toString('utf8').replace(/[\u0000\u0020]+$/u, ''),
    ) as GlbDocument,
    binaryOffset: binaryHeader + 8,
  };
}

function pngDimensions(bytes: Buffer): [number, number] {
  assert.deepEqual([...bytes.subarray(0, 8)], [137, 80, 78, 71, 13, 10, 26, 10]);
  return [bytes.readUInt32BE(16), bytes.readUInt32BE(20)];
}

function assertVectorNear(actual: number[], expected: number[], label: string): void {
  assert.equal(actual.length, expected.length, label);
  for (let axis = 0; axis < actual.length; axis += 1)
    assert.ok(Math.abs(actual[axis]! - expected[axis]!) < 1e-5, `${label} axis ${axis}`);
}

test('all sixteen Arc Relay GLBs satisfy the authored model, budget, and semantic-paint contract', () => {
  assert.equal(ledger.looks.length, 16);
  let totalBytes = 0;
  let totalTriangles = 0;
  let totalMaterials = 0;
  let totalTextures = 0;

  for (const entry of ledger.looks) {
    const directory = join(looksRoot, entry.id);
    const manifest = JSON.parse(readFileSync(join(directory, 'model3d.json'), 'utf8')) as {
      id: string;
      facing: string;
      up: string;
      nodes: Record<string, string | string[]>;
      ledger: Omit<(typeof entry), 'id' | 'vertices' | 'textures' | 'bounds'> & { textureCount: number };
    };
    const { bytes, document, binaryOffset } = readGlb(join(directory, 'model.glb'));
    const primitives = document.meshes?.flatMap((mesh) => mesh.primitives ?? []) ?? [];

    assert.equal(manifest.id, entry.id);
    assert.equal(manifest.facing, '+x');
    assert.equal(manifest.up, '+y');
    assert.deepEqual(expectedGroups.map((key) => manifest.nodes[
      key === 'underbody-locomotion'
        ? 'locomotion'
        : key === 'team-accents'
          ? 'teamAccents'
          : key === 'weapon-hardware'
            ? 'hardware'
            : key
    ]), expectedGroups);
    assert.equal(bytes.length, entry.bytes);
    assert.ok(bytes.length <= ledger.budgetBytesPerLook, entry.id);
    assert.equal(createHash('sha256').update(bytes).digest('hex'), entry.sha256);
    assert.equal(manifest.ledger.sha256, entry.sha256);
    assert.equal(manifest.ledger.bytes, entry.bytes);

    assert.equal(document.cameras, undefined, entry.id);
    assert.equal(document.skins, undefined, entry.id);
    assert.equal(document.animations, undefined, entry.id);
    assert.equal(document.asset?.extras?.provider, 'none', entry.id);
    assert.equal(
      document.asset?.extras?.source,
      'named-group-orthographic-vector',
      entry.id,
    );
    assert.equal(document.asset?.extras?.sourceProjectionDegrees, 0, entry.id);
    assert.equal(document.asset?.extras?.archivedMasterProjectionDegrees, 20, entry.id);
    assert.deepEqual(document.asset?.extras?.requiredGroups, expectedGroups, entry.id);
    assert.ok(document.nodes?.every((node) => node.camera === undefined && node.skin === undefined));
    const rootIndex = document.scenes?.[0]?.nodes?.[0] ?? -1;
    const root = document.nodes?.[rootIndex];
    assert.ok(root);
    assert.deepEqual(root.children?.map((index) => document.nodes?.[index]?.name), expectedGroups);
    const groupTop = Object.fromEntries(
      (root.children ?? []).map((groupIndex) => {
        const group = document.nodes?.[groupIndex];
        const meshNodes = (group?.children ?? [])
          .map((nodeIndex) => document.nodes?.[nodeIndex])
          .filter((node) => node?.mesh !== undefined);
        const maximumY = Math.max(
          ...meshNodes.flatMap((node) =>
            (document.meshes?.[node!.mesh!]?.primitives ?? []).map((primitive) =>
              document.accessors?.[primitive.attributes?.POSITION ?? -1]?.max?.[1] ?? 0,
            ),
          ),
        );
        return [group?.name, maximumY];
      }),
    );
    assert.ok(groupTop['underbody-locomotion'] < groupTop.chassis, entry.id);
    assert.ok(groupTop.chassis < groupTop['weapon-hardware'], entry.id);

    const semanticMaterials = (document.materials ?? []).filter(
      (material) => material.extras?.nilbotsRole === 'team-accent',
    );
    assert.equal(semanticMaterials.length, 1, entry.id);
    assert.equal(
      semanticMaterials[0]?.pbrMetallicRoughness?.baseColorTexture,
      undefined,
      entry.id,
    );
    const semanticIndex = document.materials?.indexOf(semanticMaterials[0]!) ?? -1;
    assert.ok(primitives.some((primitive) => primitive.material === semanticIndex), entry.id);

    let triangles = 0;
    const mins = [Number.POSITIVE_INFINITY, Number.POSITIVE_INFINITY, Number.POSITIVE_INFINITY];
    const maxs = [Number.NEGATIVE_INFINITY, Number.NEGATIVE_INFINITY, Number.NEGATIVE_INFINITY];
    for (const primitive of primitives) {
      assert.equal(primitive.mode ?? 4, 4, entry.id);
      const material = document.materials?.[primitive.material ?? -1];
      if (material?.normalTexture !== undefined)
        assert.equal(typeof primitive.attributes?.TANGENT, 'number', entry.id);
      const position = document.accessors?.[primitive.attributes?.POSITION ?? -1];
      assert.ok(position?.min && position.max, entry.id);
      for (let axis = 0; axis < 3; axis += 1) {
        mins[axis] = Math.min(mins[axis], position.min![axis]!);
        maxs[axis] = Math.max(maxs[axis], position.max![axis]!);
      }
      const indices = document.accessors?.[primitive.indices ?? -1];
      assert.equal((indices?.count ?? 0) % 3, 0, entry.id);
      triangles += (indices?.count ?? 0) / 3;
    }
    assert.equal(triangles, entry.triangles, entry.id);
    assertVectorNear(mins, entry.bounds.min, `${entry.id} minimum bounds`);
    assertVectorNear(maxs, entry.bounds.max, `${entry.id} maximum bounds`);
    assert.ok(Math.abs(mins[1]) < 1e-5, entry.id);
    assert.ok(Math.max(maxs[0] - mins[0], maxs[2] - mins[2]) <= 1, entry.id);

    assert.equal(document.images?.length, 3, entry.id);
    for (const [index, image] of (document.images ?? []).entries()) {
      assert.equal(image.mimeType, 'image/png');
      const view = document.bufferViews?.[image.bufferView ?? -1];
      assert.ok(view);
      const start = binaryOffset + (view.byteOffset ?? 0);
      const png = bytes.subarray(start, start + (view.byteLength ?? 0));
      assert.deepEqual(pngDimensions(png), [256, 256], `${entry.id} texture ${index}`);
    }

    assert.equal(document.materials?.length, entry.materials, entry.id);
    totalBytes += entry.bytes;
    totalTriangles += entry.triangles;
    totalMaterials += entry.materials;
    totalTextures += entry.textures.length;
  }

  assert.deepEqual(
    { bytes: totalBytes, triangles: totalTriangles, materials: totalMaterials, textures: totalTextures },
    ledger.totals,
  );
});
