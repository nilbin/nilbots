import assert from 'node:assert/strict';
import { createHash } from 'node:crypto';
import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import test from 'node:test';
import { fileURLToPath } from 'node:url';

const modelPath = join(
  dirname(fileURLToPath(import.meta.url)),
  '..',
  'src',
  'assets',
  'projectile-looks',
  'pulse-bolt',
  'model.glb',
);

interface GlbDocument {
  accessors?: { count?: number }[];
  animations?: unknown[];
  cameras?: unknown[];
  extensions?: {
    KHR_lights_punctual?: { lights?: unknown[] };
  };
  materials?: {
    extras?: { nilbotsRole?: string };
  }[];
  meshes?: {
    primitives?: {
      attributes?: Record<string, number>;
      indices?: number;
      material?: number;
      mode?: number;
    }[];
  }[];
  skins?: unknown[];
}

function readGlb(path: string): {
  bytes: Buffer;
  document: GlbDocument;
} {
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
  return { bytes, document };
}

test('Pulse Bolt installs the exact audited compact projectile control', () => {
  const { bytes, document } = readGlb(modelPath);
  assert.equal(bytes.length, 16_032);
  assert.equal(
    createHash('sha256').update(bytes).digest('hex'),
    '63f2b97cc2b6f427d6e4be989894f902e5547e03a866c6a0130030188dabfe6e',
  );

  const primitives =
    document.meshes?.flatMap((mesh) => mesh.primitives ?? []) ?? [];
  assert.equal(primitives.length, 5);
  assert.ok(
    primitives.every(
      (primitive) =>
        primitive.mode === undefined &&
        primitive.attributes?.POSITION !== undefined &&
        primitive.attributes.NORMAL !== undefined,
    ),
  );
  const triangleCount = primitives.reduce(
    (sum, primitive) =>
      sum + (document.accessors?.[primitive.indices ?? -1]?.count ?? 0) / 3,
    0,
  );
  assert.equal(triangleCount, 212);

  assert.equal(document.materials?.length, 2);
  assert.ok(
    document.materials?.every(
      (material) =>
        material.extras?.nilbotsRole === 'projectile-mask',
    ),
  );
  assert.equal(document.animations, undefined);
  assert.equal(document.cameras, undefined);
  assert.equal(document.skins, undefined);
  assert.equal(document.extensions?.KHR_lights_punctual?.lights, undefined);
});
