import assert from 'node:assert/strict';
import { createHash } from 'node:crypto';
import { spawnSync } from 'node:child_process';
import { readFileSync } from 'node:fs';
import { join, resolve } from 'node:path';
import test from 'node:test';

const repository = resolve(import.meta.dirname, '..', '..');
const review = JSON.parse(
  readFileSync(
    join(
      repository,
      'art/class-models/runtime-tiers/arc-relay/reactor-recess-review-v1/audit.json',
    ),
    'utf8',
  ),
);
const ledger = JSON.parse(
  readFileSync(join(repository, 'art/objective-models/arc-relay/ledger.json'), 'utf8'),
);
const runtimeDirectory = join(
  repository,
  'web/src/assets/objective-models/arc-team-reactor',
);

test('the owner-approved Reactor recess is a deterministic runtime promotion', () => {
  const result = spawnSync(
    process.execPath,
    [join(repository, 'scripts/class-models/promote-reactor-recess-v1.mjs'), '--check'],
    { cwd: repository, encoding: 'utf8' },
  );

  assert.equal(result.status, 0, `${result.stdout}${result.stderr}`);
  assert.match(result.stdout, /Verified Reactor recess \(1635852 bytes\)/);
  assert.equal(ledger.status, 'owner-approved');
  assert.equal(ledger.provider, 'Meshy');
  assert.equal(ledger.providerModel, 'meshy-7');
  assert.equal(ledger.modelOwnedTeamGlow, false);
  assert.match(ledger.rendererOwnedTeamGlow, /point light/);
});

test('the runtime Reactor is the exact neutral matte lean artifact', () => {
  const profile = review.profiles.find((entry: { id: string }) => entry.id === 'lean');
  assert.ok(profile);
  const runtime = readFileSync(join(runtimeDirectory, 'model.glb'));
  const approved = readFileSync(join(repository, profile.artifact.file));
  const manifest = JSON.parse(
    readFileSync(join(runtimeDirectory, 'model3d.json'), 'utf8'),
  );
  const document = glbDocument(runtime);
  const normalization = document.nodes?.find(
    (node) => node.extras?.nilbotsProviderNormalization,
  )?.extras?.nilbotsProviderNormalization;

  assert.ok(runtime.equals(approved));
  assert.equal(runtime.length, 1_635_852);
  assert.equal(sha256(runtime), profile.artifact.sha256);
  assert.equal(manifest.id, 'arc-team-reactor');
  assert.equal(manifest.kind, 'objective');
  assert.equal(manifest.part, 'whole');
  assert.equal(manifest.facing, '+x');
  assert.equal(manifest.up, '+y');
  assert.equal(manifest.source.generator, 'scripts/class-models/promote-reactor-recess-v1.mjs');
  assert.equal(manifest.source.provider, 'Meshy');
  assert.equal(manifest.source.model, 'meshy-7');
  assert.equal(manifest.source.endpoint, 'retexture');
  assert.equal(manifest.source.sourceSha256, profile.artifact.sha256);
  assert.equal(manifest.ledger.triangles, 79_318);
  assert.equal(manifest.ledger.geometrySha256, profile.geometrySha256);
  assert.equal(document.cameras, undefined);
  assert.equal(document.skins, undefined);
  assert.equal(document.animations, undefined);
  assert.equal(document.meshes?.length, 1);
  assert.equal(document.materials?.length, 1);
  assert.equal(document.textures?.length, 3);
  assert.equal(document.images?.length, 3);
  assert.ok(document.images?.every((image) => image.mimeType === 'image/webp'));
  assert.ok(document.extensionsRequired?.includes('EXT_texture_webp'));
  assert.ok(document.extensionsRequired?.includes('KHR_mesh_quantization'));
  assert.ok(
    document.materials?.every((material) => material.extras?.nilbotsRole !== 'team-accent'),
  );
  assert.equal(document.asset?.extras?.nilbotsMatteRoughness?.floor, 0.82);
  assert.ok(
    (document.asset?.extras?.nilbotsMatteRoughness?.minimumAfter ?? 0) >= 0.82,
  );
  assert.equal(normalization?.facing, '+x');
  assert.equal(normalization?.up, '+y');
  assert.equal(normalization?.floorY, 0);
  assert.equal(normalization?.orientation, 'identity');
  assert.equal(normalization?.targetPlanformSpan, 1.5);
  assert.equal(normalization?.facingYawDegrees, 90);
});

function sha256(bytes: Buffer): string {
  return createHash('sha256').update(bytes).digest('hex');
}

interface GlbDocument {
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
    extras?: {
      nilbotsProviderNormalization?: {
        facing?: string;
        up?: string;
        floorY?: number;
        orientation?: string;
        targetPlanformSpan?: number;
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
