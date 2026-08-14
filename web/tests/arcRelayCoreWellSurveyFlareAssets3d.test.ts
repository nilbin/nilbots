import assert from 'node:assert/strict';
import { createHash } from 'node:crypto';
import { spawnSync } from 'node:child_process';
import { readFileSync } from 'node:fs';
import { join, resolve } from 'node:path';
import test from 'node:test';

const repository = resolve(import.meta.dirname, '..', '..');
const reviewPath = join(
  repository,
  'art/class-models/runtime-tiers/arc-relay/longshot-objectives-review-v1/audit.json',
);
const review = JSON.parse(readFileSync(reviewPath, 'utf8'));
const generator = 'scripts/class-models/promote-core-well-survey-flare-v1.mjs';

const definitions = [
  {
    candidateId: 'core-well',
    runtimeId: 'arc-core-well',
    kind: 'objective',
    runtimeDirectory: 'web/src/assets/objective-models/arc-core-well',
    ledger: 'art/objective-models/arc-relay/core-well-ledger.json',
    textureSize: 512,
    textures: 4,
    targetPlanformSpan: 0.9,
    heightScale: 0.5,
    matteFloor: 0.82,
  },
  {
    candidateId: 'survey-flare',
    runtimeId: 'arc-survey-flare',
    kind: 'signature',
    signature: 'survey-flare',
    runtimeDirectory: 'web/src/assets/signature-models/arc-survey-flare',
    ledger: 'art/signature-models/arc-relay/survey-flare-ledger.json',
    textureSize: 256,
    textures: 3,
    targetPlanformSpan: 0.3,
    heightScale: 1,
    matteFloor: 0.72,
  },
] as const;

test('Core Well and Survey Flare are deterministic owner-approved promotions', () => {
  const result = spawnSync(
    process.execPath,
    [join(repository, generator), '--check'],
    { cwd: repository, encoding: 'utf8' },
  );

  assert.equal(result.status, 0, `${result.stdout}${result.stderr}`);
  assert.match(result.stdout, /Verified Core Well and Survey Flare \(1619284 \+ 361444 bytes\)/);
});

test('runtime Well and Flare exactly match their approved matte WebP artifacts', () => {
  for (const definition of definitions) {
    const candidate = review.candidates.find(
      (entry: { id: string }) => entry.id === definition.candidateId,
    );
    assert.ok(candidate, definition.candidateId);

    const runtimeDirectory = join(repository, definition.runtimeDirectory);
    const runtime = readFileSync(join(runtimeDirectory, 'model.glb'));
    const approved = readFileSync(join(repository, candidate.artifact.file));
    const manifest = JSON.parse(
      readFileSync(join(runtimeDirectory, 'model3d.json'), 'utf8'),
    );
    const ledger = JSON.parse(readFileSync(join(repository, definition.ledger), 'utf8'));
    const document = glbDocument(runtime);
    const scene = document.scenes?.[document.scene ?? 0];
    const root = document.nodes?.[scene?.nodes?.[0] ?? -1];
    const normalization = root?.extras?.nilbotsProviderNormalization;
    const matte = document.asset?.extras?.nilbotsMatteRoughness;

    assert.ok(runtime.equals(approved), definition.runtimeId);
    assert.equal(runtime.length, candidate.artifact.bytes, definition.runtimeId);
    assert.equal(sha256(runtime), candidate.artifact.sha256, definition.runtimeId);
    assert.equal(manifest.id, definition.runtimeId);
    assert.equal(manifest.kind, definition.kind);
    assert.equal(manifest.signature, definition.signature);
    assert.equal(manifest.part, 'whole');
    assert.equal(manifest.facing, '+x');
    assert.equal(manifest.up, '+y');
    assert.equal(manifest.source.generator, generator);
    assert.equal(manifest.source.recipe, 'art/class-models/runtime-tiers/arc-relay/longshot-objectives-review-v1/audit.json');
    assert.equal(manifest.source.artifact, candidate.artifact.file);
    assert.equal(manifest.source.approvedArtifact, candidate.artifact.file);
    assert.equal(manifest.source.sourceSha256, candidate.artifact.sha256);
    assert.equal(manifest.source.approvedSha256, candidate.artifact.sha256);
    assert.equal(manifest.source.taskId, candidate.taskId);
    assert.equal(manifest.source.endpoint, candidate.endpoint);
    assert.equal(manifest.source.orientation, 'identity');
    assert.equal(manifest.source.facingYawDegrees, 90);
    assert.equal(manifest.source.matteRoughnessFloor, definition.matteFloor);
    assert.equal(manifest.ledger.sha256, candidate.artifact.sha256);
    assert.equal(manifest.ledger.triangles, candidate.triangles);
    assert.equal(manifest.ledger.geometrySha256, candidate.geometrySha256);
    assert.equal(manifest.ledger.textureCount, definition.textures);

    assert.equal(ledger.status, 'owner-approved');
    assert.equal(ledger.generator, generator);
    assert.equal(ledger.provider, 'Meshy');
    assert.equal(ledger.providerModel, 'meshy-7');
    assert.equal(ledger.modelOwnedTeamGlow, false);
    assert.equal(ledger.model.id, definition.runtimeId);
    assert.equal(ledger.model.sha256, candidate.artifact.sha256);
    assert.match(ledger.rendererOwnedState, /spawn|scan/);

    assert.equal(document.cameras, undefined);
    assert.equal(document.skins, undefined);
    assert.equal(document.animations, undefined);
    assert.equal(document.meshes?.length, 1);
    assert.equal(document.materials?.length, 1);
    assert.equal(document.textures?.length, definition.textures);
    assert.equal(document.images?.length, definition.textures);
    assert.ok(document.images?.every((image) => image.mimeType === 'image/webp'));
    assert.ok(document.extensionsRequired?.includes('EXT_texture_webp'));
    assert.ok(document.extensionsRequired?.includes('KHR_mesh_quantization'));
    assert.ok(
      document.materials?.every(
        (material) => material.extras?.nilbotsRole !== 'team-accent',
      ),
    );
    assert.equal(root?.name, 'chassis');
    assert.equal(normalization?.facing, '+x');
    assert.equal(normalization?.up, '+y');
    assert.equal(normalization?.floorY, 0);
    assert.equal(normalization?.orientation, 'identity');
    assert.equal(normalization?.targetPlanformSpan, definition.targetPlanformSpan);
    assert.equal(normalization?.heightScale ?? 1, definition.heightScale);
    assert.equal(normalization?.facingYawDegrees, 90);
    assert.equal(matte?.floor, definition.matteFloor);
    assert.ok((matte?.minimumAfter ?? 0) >= definition.matteFloor);

    for (const texture of candidate.memory.textures) {
      assert.equal(texture.mimeType, 'image/webp');
      assert.equal(texture.width, definition.textureSize);
      assert.equal(texture.height, definition.textureSize);
    }
  }
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
    };
  };
  animations?: unknown;
  cameras?: unknown;
  extensionsRequired?: string[];
  images?: Array<{ mimeType?: string }>;
  materials?: Array<{ extras?: { nilbotsRole?: string } }>;
  meshes?: unknown[];
  nodes?: Array<{
    name?: string;
    extras?: {
      nilbotsProviderNormalization?: {
        facing?: string;
        up?: string;
        floorY?: number;
        orientation?: string;
        targetPlanformSpan?: number;
        heightScale?: number;
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
