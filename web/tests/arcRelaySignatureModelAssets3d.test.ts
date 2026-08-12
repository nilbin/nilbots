import assert from 'node:assert/strict';
import { createHash } from 'node:crypto';
import { readFileSync } from 'node:fs';
import { join, resolve } from 'node:path';
import { spawnSync } from 'node:child_process';
import test from 'node:test';

const repository = resolve(import.meta.dirname, '..', '..');
const review = JSON.parse(
  readFileSync(
    join(
      repository,
      'art/class-models/runtime-tiers/arc-relay/meshy7-signature-props-review-v3/audit.json',
    ),
    'utf8',
  ),
);
const ledger = JSON.parse(
  readFileSync(join(repository, 'art/signature-models/arc-relay/ledger.json'), 'utf8'),
);

test('approved Meshy 7 signature props are deterministic runtime promotions', () => {
  const result = spawnSync(
    process.execPath,
    [join(repository, 'scripts/class-models/promote-meshy7-signature-props-v3.mjs'), '--check'],
    { cwd: repository, encoding: 'utf8' },
  );
  assert.equal(result.status, 0, `${result.stdout}${result.stderr}`);
  assert.match(result.stdout, /Verified 2 Meshy 7 signature props \(3264816 bytes\)/);
  assert.equal(ledger.status, 'owner-approved');
  assert.equal(ledger.models.length, 2);
  assert.equal(ledger.totals.bytes, 3_264_816);
  assert.equal(ledger.totals.triangles, 156_769);
  assert.equal(ledger.totals.modelGpuBytesRgba8Fallback, 12_958_296);
});

test('runtime signature GLBs exactly match the approved Meshy 7 review artifacts', () => {
  for (const source of review.classes) {
    const id = `arc-${source.signatureId}`;
    const recorded = ledger.models.find((model: { id: string }) => model.id === id);
    assert.ok(recorded, id);
    const assetDirectory = join(repository, 'web/src/assets/signature-models', id);
    const promoted = readFileSync(join(assetDirectory, 'model.glb'));
    const approved = readFileSync(join(repository, source.artifact.file));
    const manifest = JSON.parse(readFileSync(join(assetDirectory, 'model3d.json'), 'utf8'));
    const document = glbDocument(promoted);
    const normalization = document.nodes?.find(
      (node) => node.extras?.nilbotsProviderNormalization,
    )?.extras?.nilbotsProviderNormalization;
    const expectedParts = source.signatureId === 'sentinel-seed' ? 2 : 1;
    const expectedTextures = source.signatureId === 'sentinel-seed' ? 4 : 3;

    assert.ok(promoted.equals(approved), id);
    assert.equal(promoted.length, source.artifact.bytes, id);
    assert.equal(sha256(promoted), source.artifact.sha256, id);
    assert.equal(manifest.kind, 'signature', id);
    assert.equal(manifest.signature, source.signatureId, id);
    assert.equal(manifest.source.generator, 'scripts/class-models/promote-meshy7-signature-props-v3.mjs');
    assert.equal(manifest.source.sourceSha256, source.artifact.sha256, id);
    assert.equal(manifest.source.approvedSha256, source.artifact.sha256, id);
    assert.equal(manifest.source.provider, 'Meshy', id);
    assert.equal(manifest.source.model, 'meshy-7', id);
    assert.equal(manifest.source.endpoint, source.endpoint, id);
    assert.equal(manifest.source.taskId, source.providerTaskId, id);
    assert.equal(manifest.source.orientation, 'identity', id);
    assert.equal(manifest.ledger.bytes, source.artifact.bytes, id);
    assert.equal(manifest.ledger.triangles, source.triangles, id);
    assert.equal(
      manifest.ledger.modelGpuBytesRgba8Fallback,
      source.memory.modelGpuBytesRgba8Fallback,
      id,
    );
    assert.equal(document.cameras, undefined, id);
    assert.equal(document.skins, undefined, id);
    assert.equal(document.animations, undefined, id);
    assert.equal(document.meshes?.length, expectedParts, id);
    assert.equal(document.images?.length, expectedTextures, id);
    assert.ok(
      document.images?.every((image) => image.mimeType === 'image/webp'),
      id,
    );
    assert.ok(document.extensionsRequired?.includes('EXT_texture_webp'), id);
    assert.ok(document.extensionsRequired?.includes('KHR_mesh_quantization'), id);
    assert.deepEqual(normalization, {
      facing: '+x',
      up: '+y',
      floorY: 0,
      orientation: 'identity',
      targetPlanformSpan: 0.99,
      sourceBounds: normalization?.sourceBounds,
      ...(source.facing.yawDegrees === 0
        ? {}
        : { facingYawDegrees: source.facing.yawDegrees, facingCorrectionVersion: 1 }),
    });
  }
});

test('Sentinel preserves the exact stationary-base and yaw-head partition', () => {
  const source = review.classes.find(
    (entry: { signatureId: string }) => entry.signatureId === 'sentinel-seed',
  );
  assert.ok(source);
  const document = glbDocument(
    readFileSync(
      join(repository, 'web/src/assets/signature-models/arc-sentinel-seed/model.glb'),
    ),
  );
  const stationary = document.nodes?.find((node) => node.name === source.nodes.stationary);
  const yawHead = document.nodes?.find((node) => node.name === source.nodes.yawPivot);
  const yawMesh = document.nodes?.find((node) => node.name === source.nodes.yawMesh);

  assert.deepEqual(document.asset?.extras?.nilbotsSentinelYawSplit, source.split);
  assert.equal(source.split.partitionUnionVerified, true);
  assert.equal(
    source.split.sourceTriangles,
    source.split.stationaryTriangles + source.split.yawHeadTriangles,
  );
  assert.notEqual(stationary?.mesh, undefined);
  assert.equal(yawHead?.extras?.yawAxis, '+y');
  assert.deepEqual(yawHead?.extras?.pivot, [0, 0, 0]);
  assert.ok(yawHead?.children?.some((index) => document.nodes?.[index] === yawMesh));
  assert.notEqual(yawMesh?.mesh, undefined);
});

function sha256(bytes: Buffer): string {
  return createHash('sha256').update(bytes).digest('hex');
}

interface GlbDocument {
  asset?: { extras?: { nilbotsSentinelYawSplit?: unknown } };
  cameras?: unknown;
  skins?: unknown;
  animations?: unknown;
  extensionsRequired?: string[];
  images?: { mimeType?: string }[];
  meshes?: unknown[];
  nodes?: Array<{
    name?: string;
    mesh?: number;
    children?: number[];
    extras?: {
      nilbotsProviderNormalization?: {
        facing?: string;
        up?: string;
        floorY?: number;
        orientation?: string;
        targetPlanformSpan?: number;
        sourceBounds?: unknown;
        facingYawDegrees?: number;
        facingCorrectionVersion?: number;
      };
      yawAxis?: string;
      pivot?: number[];
    };
  }>;
}

function glbDocument(bytes: Buffer): GlbDocument {
  assert.equal(bytes.toString('ascii', 0, 4), 'glTF');
  assert.equal(bytes.readUInt32LE(4), 2);
  assert.equal(bytes.readUInt32LE(8), bytes.length);
  const jsonLength = bytes.readUInt32LE(12);
  assert.equal(bytes.readUInt32LE(16), 0x4e4f534a);
  return JSON.parse(bytes.subarray(20, 20 + jsonLength).toString('utf8').trim());
}
