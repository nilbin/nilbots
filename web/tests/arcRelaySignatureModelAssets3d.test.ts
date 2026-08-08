import assert from 'node:assert/strict';
import { createHash } from 'node:crypto';
import { readFileSync } from 'node:fs';
import { join, resolve } from 'node:path';
import { spawnSync } from 'node:child_process';
import test from 'node:test';

const repository = resolve(import.meta.dirname, '..', '..');
const provider = JSON.parse(
  readFileSync(join(repository, 'art/signature-models/arc-relay/provider-audit.json'), 'utf8'),
);
const tier = JSON.parse(
  readFileSync(
    join(
      repository,
      'art/signature-models/arc-relay/runtime-tiers/ktx2-selective-v1/audit.json',
    ),
    'utf8',
  ),
);

test('approved signature props use the reproducible selective KTX2 pipeline', () => {
  const result = spawnSync(
    process.execPath,
    [
      join(repository, 'scripts/class-models/build-arc-runtime-texture-tier.mjs'),
      '--profile',
      'signatures',
      '--check',
    ],
    { cwd: repository, encoding: 'utf8' },
  );
  assert.equal(result.status, 0, `${result.stdout}${result.stderr}`);
  assert.match(result.stdout, /Verified 2 selective KTX2 looks/);
  assert.equal(tier.looks.length, 2);
  assert.ok(tier.totals.modelGpuBytesCompressedTarget <= 2 * 1024 * 1024);
  assert.ok(tier.totals.modelGpuBytesRgba8Fallback <= 6 * 1024 * 1024);
});

test('runtime signature GLBs exactly match their approved tier and orientation', () => {
  for (const source of provider.models) {
    const runtime = tier.looks.find((look: { id: string }) => look.id === source.lookId);
    assert.ok(runtime, source.lookId);
    const assetDirectory = join(repository, 'web/src/assets/signature-models', source.lookId);
    const promoted = readFileSync(join(assetDirectory, 'model.glb'));
    const derivative = readFileSync(join(repository, runtime.runtime.file));
    const manifest = JSON.parse(readFileSync(join(assetDirectory, 'model3d.json'), 'utf8'));
    const document = glbDocument(promoted);
    const root = document.nodes?.[document.scenes?.[0]?.nodes?.[0]];

    assert.ok(promoted.equals(derivative), source.lookId);
    assert.equal(sha256(promoted), runtime.runtime.sha256, source.lookId);
    assert.equal(manifest.kind, 'signature', source.lookId);
    assert.equal(manifest.signature, source.signatureId, source.lookId);
    assert.equal(manifest.source.sourceSha256, runtime.runtime.sha256, source.lookId);
    assert.equal(manifest.source.approvedSha256, source.candidate.sha256, source.lookId);
    assert.equal(manifest.source.orientation, source.orientation, source.lookId);
    assert.equal(manifest.ledger.modelGpuBytesCompressedTarget, runtime.memory.modelGpuBytesCompressedTarget);
    assert.equal(manifest.ledger.modelGpuBytesRgba8Fallback, runtime.memory.modelGpuBytesRgba8Fallback);
    assert.equal(document.cameras, undefined, source.lookId);
    assert.equal(document.skins, undefined, source.lookId);
    assert.equal(document.animations, undefined, source.lookId);
    assert.equal(document.images?.length, 4, source.lookId);
    assert.ok(document.images?.every((image: { mimeType?: string }) => image.mimeType === 'image/ktx2'));
    assert.ok(document.extensionsRequired?.includes('KHR_texture_basisu'));
    assert.deepEqual(root?.extras?.nilbotsProviderNormalization, {
      facing: '+x',
      up: '+y',
      floorY: 0,
      orientation: source.orientation,
      ...(source.facingYawDegrees === 0 ? {} : { facingYawDegrees: source.facingYawDegrees }),
      targetPlanformSpan: 0.99,
      sourceBounds: source.candidate.sourceBounds,
    });
  }
});

function sha256(bytes: Buffer): string {
  return createHash('sha256').update(bytes).digest('hex');
}

function glbDocument(bytes: Buffer): {
  cameras?: unknown;
  skins?: unknown;
  animations?: unknown;
  extensionsRequired?: string[];
  images?: { mimeType?: string }[];
  nodes?: { extras?: { nilbotsProviderNormalization?: unknown } }[];
  scenes?: { nodes?: number[] }[];
} {
  assert.equal(bytes.toString('ascii', 0, 4), 'glTF');
  assert.equal(bytes.readUInt32LE(4), 2);
  assert.equal(bytes.readUInt32LE(8), bytes.length);
  const jsonLength = bytes.readUInt32LE(12);
  assert.equal(bytes.readUInt32LE(16), 0x4e4f534a);
  return JSON.parse(bytes.subarray(20, 20 + jsonLength).toString('utf8').trim());
}
