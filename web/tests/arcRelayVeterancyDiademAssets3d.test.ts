import assert from 'node:assert/strict';
import { createHash } from 'node:crypto';
import { spawnSync } from 'node:child_process';
import { readFileSync } from 'node:fs';
import { join, resolve } from 'node:path';
import test from 'node:test';

const repository = resolve(import.meta.dirname, '..', '..');
const tierDirectory =
  'art/class-models/runtime-tiers/arc-relay/meshy7-veterancy-split-diadem-whole-v1';
const audit = JSON.parse(
  readFileSync(join(repository, tierDirectory, 'audit.json'), 'utf8'),
);
const generator = 'scripts/class-models/promote-veterancy-whole-diadem-v1.mjs';
const taskIds = [
  '01a00294-5948-7ee8-bc63-2f81cb0be753',
  '01a00295-fde7-7cbf-a014-4c4b04b6f3b4',
  '01a00297-c1fe-7f4a-8236-b646e390bd2b',
  '01a00299-e6dc-7d02-a713-86a9ccf8c7e9',
  '01a00290-9941-7b7c-86eb-568a05e213a2',
];

test('the five complete Meshy diadem ranks are deterministic promotions', () => {
  const result = spawnSync(process.execPath, [join(repository, generator), '--check'], {
    cwd: repository,
    encoding: 'utf8',
  });

  assert.equal(result.status, 0, `${result.stdout}${result.stderr}`);
  assert.match(result.stdout, /Verified five promoted whole-diadem runtime assets/);
});

test('runtime diadems exactly match the audited lean matte artifacts', () => {
  assert.equal(audit.status, 'runtime-review');
  assert.equal(audit.source.provider, 'Meshy');
  assert.equal(audit.source.providerModel, 'meshy-7');
  assert.equal(audit.source.endpoint, 'multi-image-to-3d');
  assert.equal(audit.source.credits.total, 150);
  assert.equal(audit.source.credits.balanceAfter, 650);
  assert.equal(audit.source.autoRigging, false);
  assert.equal(audit.contract.targetGapRatio, 0.34);
  assert.equal(audit.ranks.length, 5);

  let totalBytes = 0;
  let previousSpan = 0;
  let previousHeight = 0;
  for (const rank of audit.ranks) {
    const lean = rank.profiles.find((profile: { id: string }) => profile.id === 'lean-256');
    assert.ok(lean, `rank ${rank.rank}`);
    const id = `arc-veterancy-diadem-rank-${rank.rank}`;
    const runtimeDirectory = join(repository, `web/src/assets/ornament-models/${id}`);
    const runtime = readFileSync(join(runtimeDirectory, 'model.glb'));
    const approved = readFileSync(join(repository, lean.artifact.file));
    const manifest = JSON.parse(
      readFileSync(join(runtimeDirectory, 'model3d.json'), 'utf8'),
    );
    const document = glbDocument(runtime);
    const scene = document.scenes?.[document.scene ?? 0];
    const root = document.nodes?.[scene?.nodes?.[0] ?? -1];
    const normalization = root?.extras?.nilbotsProviderNormalization;
    const split = document.asset?.extras?.nilbotsSplitClearance;
    const matte = document.asset?.extras?.nilbotsMatteRoughness;

    assert.ok(runtime.equals(approved), id);
    assert.equal(runtime.length, lean.artifact.bytes, id);
    assert.equal(sha256(runtime), lean.artifact.sha256, id);
    assert.equal(manifest.id, id);
    assert.equal(manifest.kind, 'ornament');
    assert.equal(manifest.part, 'whole');
    assert.equal(manifest.facing, '+x');
    assert.equal(manifest.up, '+y');
    assert.equal(manifest.source.generator, generator);
    assert.equal(manifest.source.recipe, `${tierDirectory}/audit.json`);
    assert.equal(manifest.source.artifact, lean.artifact.file);
    assert.equal(manifest.source.taskId, taskIds[rank.rank - 1]);
    assert.equal(manifest.source.rank, rank.rank);
    assert.equal(manifest.source.orientation, 'identity');
    assert.equal(manifest.source.facingYawDegrees, 90);
    assert.equal(manifest.source.splitGapRatio, 0.34);
    assert.equal(manifest.source.matteRoughnessFloor, 0.78);
    assert.equal(manifest.ledger.sha256, lean.artifact.sha256);
    assert.equal(manifest.ledger.geometrySha256, lean.geometrySha256);
    assert.equal(manifest.ledger.triangles, lean.triangles);
    assert.equal(manifest.ledger.textureCount, 4);

    assert.equal(document.cameras, undefined);
    assert.equal(document.skins, undefined);
    assert.equal(document.animations, undefined);
    assert.equal(document.meshes?.length, 1);
    assert.equal(document.materials?.length, 1);
    assert.equal(document.textures?.length, 4);
    assert.equal(document.images?.length, 4);
    assert.ok(document.images?.every((image) => image.mimeType === 'image/webp'));
    assert.ok(document.extensionsRequired?.includes('EXT_texture_webp'));
    assert.ok(document.extensionsRequired?.includes('KHR_mesh_quantization'));
    assert.equal(root?.name, 'chassis');
    assert.equal(normalization?.facing, '+x');
    assert.equal(normalization?.up, '+y');
    assert.equal(normalization?.floorY, 0);
    assert.equal(normalization?.facingYawDegrees, 90);
    assert.equal(split?.targetGapRatio, 0.34);
    assert.ok((split?.achievedGapRatio ?? 0) >= 0.34);
    assert.equal(matte?.floor, 0.78);
    assert.ok((matte?.minimumAfter ?? 0) >= 0.78);
    assert.ok(
      lean.memory.textures.every(
        (texture: { width: number; height: number; mimeType: string }) =>
          texture.width === 256 &&
          texture.height === 256 &&
          texture.mimeType === 'image/webp',
      ),
    );
    assert.ok(runtime.length <= 500_000, id);
    assert.ok(lean.triangles <= 25_000, id);
    assert.ok(rank.normalization.targetPlanformSpan > previousSpan);
    assert.ok(rank.normalization.normalizedHeight >= previousHeight);

    totalBytes += runtime.length;
    previousSpan = rank.normalization.targetPlanformSpan;
    previousHeight = rank.normalization.normalizedHeight;
  }

  assert.equal(totalBytes, 1_504_868);
  assert.ok(totalBytes <= 1_550_000);
});

function sha256(bytes: Buffer): string {
  return createHash('sha256').update(bytes).digest('hex');
}

interface GlbDocument {
  scene?: number;
  scenes?: Array<{ nodes?: number[] }>;
  asset?: {
    extras?: {
      nilbotsMatteRoughness?: { floor?: number; minimumAfter?: number };
      nilbotsSplitClearance?: { targetGapRatio?: number; achievedGapRatio?: number };
    };
  };
  animations?: unknown;
  cameras?: unknown;
  extensionsRequired?: string[];
  images?: Array<{ mimeType?: string }>;
  materials?: unknown[];
  meshes?: unknown[];
  nodes?: Array<{
    name?: string;
    extras?: {
      nilbotsProviderNormalization?: {
        facing?: string;
        up?: string;
        floorY?: number;
        facingYawDegrees?: number;
      };
    };
  }>;
  skins?: unknown;
  textures?: unknown[];
}

function glbDocument(bytes: Buffer): GlbDocument {
  assert.equal(bytes.toString('ascii', 0, 4), 'glTF');
  assert.equal(bytes.readUInt32LE(4), 2);
  assert.equal(bytes.readUInt32LE(8), bytes.length);
  assert.equal(bytes.readUInt32LE(16), 0x4e4f534a);
  const jsonLength = bytes.readUInt32LE(12);
  return JSON.parse(bytes.subarray(20, 20 + jsonLength).toString('utf8').trim());
}
