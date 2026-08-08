import assert from 'node:assert/strict';
import { createHash } from 'node:crypto';
import { spawnSync } from 'node:child_process';
import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import test from 'node:test';
import { fileURLToPath } from 'node:url';

const repository = join(dirname(fileURLToPath(import.meta.url)), '..', '..');
const looksRoot = join(repository, 'web', 'src', 'assets', 'class-looks');
const auditPath = join(
  repository,
  'art/class-models/provider-runs/meshy/arc-fleet-review/fleet-audit.json',
);
const tierAuditPath = join(
  repository,
  'art/class-models/runtime-tiers/arc-relay/ktx2-selective-v1/audit.json',
);
const audit = JSON.parse(readFileSync(auditPath, 'utf8')) as FleetAudit;
const tier = JSON.parse(readFileSync(tierAuditPath, 'utf8')) as RuntimeTierAudit;
const ledger = JSON.parse(
  readFileSync(join(repository, 'art/class-models/arc-relay/ledger.json'), 'utf8'),
) as FleetLedger;
const approved = [audit.pilot, ...audit.generated].sort((left, right) =>
  left.lookId.localeCompare(right.lookId),
);

interface AuditEntry {
  lookId: string;
  taskId: string;
  orientation: 'identity' | 'lay-flat-x';
  facingYawDegrees?: number;
  candidate: {
    file: string;
    bytes: number;
    sha256: string;
    triangles: number;
    materials: number;
    textures: number;
  };
}

interface FleetAudit {
  provider: string;
  endpoint: string;
  model: string;
  modelType: string;
  pilot: AuditEntry;
  generated: AuditEntry[];
  reviewContract: {
    targetPlanformSpan: number;
    modelOwnedTeamGlow: boolean;
    meshContract: string;
  };
}

interface TextureMemory {
  imageIndex: number;
  name: string | null;
  slots: string[];
  mimeType: string;
  encoding: 'ETC1S' | 'UASTC';
  width: number;
  height: number;
  mipLevels: number;
  transferBytes: number;
  gpuBytesCompressedTarget: number;
  gpuBytesRgba8Fallback: number;
}

interface LookMemory {
  transferBytes: number;
  geometryGpuBytes: number;
  textureGpuBytesCompressedTarget: number;
  textureGpuBytesRgba8Fallback: number;
  modelGpuBytesCompressedTarget: number;
  modelGpuBytesRgba8Fallback: number;
  textures: TextureMemory[];
}

interface RuntimeTierLook {
  id: string;
  taskId: string;
  approvedCandidate: { file: string; bytes: number; sha256: string };
  runtime: { file: string; bytes: number; sha256: string };
  orientation: string;
  facingYawDegrees: number;
  triangles: number;
  materials: number;
  textures: number;
  geometrySha256: string;
  targetPlanformSpan: number;
  floorY: number;
  memory: LookMemory;
}

interface RuntimeTierAudit {
  id: string;
  generator: string;
  sourceAudit: string;
  budgets: {
    perLookTransferBytes: number;
    fleetTransferBytes: number;
    compressedTextureGpuBytes: number;
    rgba8FallbackTextureGpuBytes: number;
    compressedModelGpuBytes: number;
    rgba8FallbackModelGpuBytes: number;
  };
  totals: {
    runtimeTransferBytes: number;
    geometryGpuBytes: number;
    textureGpuBytesCompressedTarget: number;
    textureGpuBytesRgba8Fallback: number;
    modelGpuBytesCompressedTarget: number;
    modelGpuBytesRgba8Fallback: number;
    triangles: number;
  };
  looks: RuntimeTierLook[];
}

interface FleetLedger {
  sourceAudit: string;
  runtimeTier: string;
  textureTier: string;
  provider: string;
  endpoint: string;
  model: string;
  modelType: string;
  meshContract: string;
  modelOwnedTeamGlow: boolean;
  budgetBytesPerLook: number;
  budgets: RuntimeTierAudit['budgets'];
  totals: {
    bytes: number;
    triangles: number;
    materials: number;
    textures: number;
    geometryGpuBytes: number;
    textureGpuBytesCompressedTarget: number;
    textureGpuBytesRgba8Fallback: number;
    modelGpuBytesCompressedTarget: number;
    modelGpuBytesRgba8Fallback: number;
  };
  looks: {
    id: string;
    taskId: string;
    artifact: string;
    approvedArtifact: string;
    orientation: string;
    facingYawDegrees: number;
    bytes: number;
    sha256: string;
    triangles: number;
    materials: number;
    textures: number;
    geometrySha256: string;
    memory: LookMemory;
    targetPlanformSpan: number;
    floorY: number;
  }[];
}

interface GlbDocument {
  accessors?: { count?: number }[];
  animations?: unknown[];
  cameras?: unknown[];
  extensionsRequired?: string[];
  images?: { mimeType?: string }[];
  materials?: { extras?: { nilbotsRole?: string } }[];
  meshes?: { primitives?: { indices?: number; mode?: number }[] }[];
  nodes?: {
    name?: string;
    rotation?: number[];
    camera?: number;
    skin?: number;
    extras?: {
      nilbotsProviderNormalization?: {
        facing?: string;
        up?: string;
        floorY?: number;
        orientation?: string;
        facingYawDegrees?: number;
        facingCorrectionVersion?: number;
        targetPlanformSpan?: number;
        sourceBounds?: unknown;
      };
    };
  }[];
  scenes?: { nodes?: number[] }[];
  skins?: unknown[];
  textures?: unknown[];
}

function sha256(bytes: Buffer): string {
  return createHash('sha256').update(bytes).digest('hex');
}

function readGlb(path: string): { bytes: Buffer; document: GlbDocument } {
  const bytes = readFileSync(path);
  assert.equal(bytes.toString('ascii', 0, 4), 'glTF', path);
  assert.equal(bytes.readUInt32LE(4), 2, path);
  assert.equal(bytes.readUInt32LE(8), bytes.length, path);
  assert.equal(bytes.readUInt32LE(16), 0x4e4f534a, path);
  const jsonLength = bytes.readUInt32LE(12);
  return {
    bytes,
    document: JSON.parse(bytes.subarray(20, 20 + jsonLength).toString('utf8').trim()) as GlbDocument,
  };
}

function triangles(document: GlbDocument): number {
  return (document.meshes ?? []).flatMap((mesh) => mesh.primitives ?? []).reduce(
    (sum, primitive) => {
      assert.equal(primitive.mode ?? 4, 4);
      assert.notEqual(primitive.indices, undefined);
      const count = document.accessors?.[primitive.indices!]?.count ?? 0;
      assert.equal(count % 3, 0);
      return sum + count / 3;
    },
    0,
  );
}

function assertVectorNear(actual: number[] | undefined, expected: number[], label: string): void {
  assert.equal(actual?.length, expected.length, label);
  for (let index = 0; index < expected.length; index += 1)
    assert.ok(Math.abs(actual![index]! - expected[index]!) < 1e-9, `${label} component ${index}`);
}

test('the selective KTX2 tier is internally audited without regeneration tools', () => {
  const result = spawnSync(
    process.execPath,
    [join(repository, 'scripts/class-models/build-arc-runtime-texture-tier.mjs'), '--check'],
    { cwd: repository, encoding: 'utf8' },
  );
  assert.equal(result.status, 0, `${result.stdout}${result.stderr}`);
  assert.match(result.stdout, /Verified 16 selective KTX2 looks/);
});

test('WebGL configures KTX2 support before any actors request fleet models', () => {
  const canvasSource = readFileSync(
    join(repository, 'web/src/render3d/ArenaCanvas3D.tsx'),
    'utf8',
  );
  const loaderSource = readFileSync(join(repository, 'web/src/render3d/lookModel.ts'), 'utf8');
  const configure = canvasSource.indexOf('configureModelTextureSupport(renderer);');
  const actors = canvasSource.indexOf('buildActors(replay)');

  assert.ok(configure >= 0, 'ArenaCanvas3D must configure the KTX2 loader');
  assert.ok(actors >= 0, 'ArenaCanvas3D must build replay actors');
  assert.ok(configure < actors, 'KTX2 target detection must precede the first GLB request');
  assert.match(loaderSource, /new KTX2Loader\(loadingManager\)/);
  assert.match(loaderSource, /loader\.setKTX2Loader\(ktx2Loader\)/);
  assert.match(loaderSource, /ktx2Loader\.detectSupport\(renderer\)/);
});

test('all runtime Arc Relay GLBs are the exact audited KTX2 derivatives', () => {
  assert.equal(approved.length, 16);
  assert.equal(tier.looks.length, 16);
  assert.equal(ledger.looks.length, 16);
  assert.equal(tier.sourceAudit, 'art/class-models/provider-runs/meshy/arc-fleet-review/fleet-audit.json');
  assert.equal(ledger.runtimeTier, 'art/class-models/runtime-tiers/arc-relay/ktx2-selective-v1/audit.json');
  assert.equal(ledger.textureTier, tier.id);
  assert.equal(ledger.provider, audit.provider);
  assert.equal(ledger.endpoint, audit.endpoint);
  assert.equal(ledger.model, audit.model);
  assert.equal(ledger.modelType, audit.modelType);
  assert.equal(ledger.meshContract, audit.reviewContract.meshContract);
  assert.equal(ledger.modelOwnedTeamGlow, false);
  assert.deepEqual(ledger.budgets, tier.budgets);

  for (const entry of approved) {
    const tierLook = tier.looks.find((look) => look.id === entry.lookId);
    const fleetEntry = ledger.looks.find((look) => look.id === entry.lookId);
    assert.ok(tierLook, entry.lookId);
    assert.ok(fleetEntry, entry.lookId);
    const directory = join(looksRoot, entry.lookId);
    const runtime = readGlb(join(directory, 'model.glb'));
    const derivative = readFileSync(join(repository, tierLook.runtime.file));
    const manifest = JSON.parse(readFileSync(join(directory, 'model3d.json'), 'utf8')) as {
      id: string;
      facing: string;
      up: string;
      nodes?: unknown;
      motion?: unknown;
      signature?: string;
      source: {
        artifact: string;
        approvedArtifact: string;
        approvedSha256: string;
        textureTier: string;
        sourceSha256: string;
        provider: string;
        model: string;
        endpoint: string;
        modelType: string;
        taskId: string;
        orientation: string;
        facingYawDegrees?: number;
      };
      ledger: {
        bytes: number;
        sha256: string;
        triangles: number;
        materials: number;
        textureCount: number;
        geometrySha256: string;
        geometryGpuBytes: number;
        textureGpuBytesCompressedTarget: number;
        textureGpuBytesRgba8Fallback: number;
        modelGpuBytesCompressedTarget: number;
        modelGpuBytesRgba8Fallback: number;
        textureTier: string;
      };
    };

    assert.ok(runtime.bytes.equals(derivative), `${entry.lookId} differs from tier derivative`);
    assert.equal(runtime.bytes.length, tierLook.runtime.bytes, entry.lookId);
    assert.ok(runtime.bytes.length <= ledger.budgetBytesPerLook, entry.lookId);
    assert.equal(sha256(runtime.bytes), tierLook.runtime.sha256, entry.lookId);
    assert.equal(manifest.ledger.sha256, tierLook.runtime.sha256, entry.lookId);
    assert.equal(manifest.source.sourceSha256, tierLook.runtime.sha256, entry.lookId);
    assert.equal(tierLook.approvedCandidate.sha256, entry.candidate.sha256, entry.lookId);

    assert.equal(manifest.id, entry.lookId);
    assert.equal(manifest.facing, '+x');
    assert.equal(manifest.up, '+y');
    assert.equal(manifest.nodes, undefined, `${entry.lookId} remains monolithic`);
    assert.ok(manifest.motion, `${entry.lookId} keeps root-level motion tuning`);
    assert.ok(manifest.signature, `${entry.lookId} keeps signature identity`);
    assert.equal(manifest.source.artifact, tierLook.runtime.file);
    assert.equal(manifest.source.approvedArtifact, entry.candidate.file);
    assert.equal(manifest.source.approvedSha256, entry.candidate.sha256);
    assert.equal(manifest.source.textureTier, tier.id);
    assert.equal(manifest.source.provider, audit.provider);
    assert.equal(manifest.source.model, audit.model);
    assert.equal(manifest.source.endpoint, audit.endpoint);
    assert.equal(manifest.source.modelType, audit.modelType);
    assert.equal(manifest.source.taskId, entry.taskId);
    assert.equal(manifest.source.orientation, entry.orientation);
    assert.equal(manifest.source.facingYawDegrees ?? 0, entry.facingYawDegrees ?? 0);

    const document = runtime.document;
    assert.equal(document.cameras, undefined, entry.lookId);
    assert.equal(document.skins, undefined, entry.lookId);
    assert.equal(document.animations, undefined, entry.lookId);
    assert.ok(document.nodes?.every((node) => node.camera === undefined && node.skin === undefined));
    assert.equal(document.materials?.length, 1, entry.lookId);
    assert.equal(document.textures?.length, 4, entry.lookId);
    assert.equal(document.images?.length, 4, entry.lookId);
    assert.ok(document.images?.every((image) => image.mimeType === 'image/ktx2'), entry.lookId);
    assert.ok(document.extensionsRequired?.includes('KHR_texture_basisu'), entry.lookId);
    assert.ok(
      document.materials?.every((material) => material.extras?.nilbotsRole !== 'team-accent'),
      `${entry.lookId} must not manufacture model-owned team paint`,
    );
    assert.equal(triangles(document), entry.candidate.triangles, entry.lookId);
    const root = document.nodes?.[document.scenes?.[0]?.nodes?.[0] ?? -1];
    assert.equal(root?.name, 'chassis', entry.lookId);
    assert.deepEqual(root?.extras?.nilbotsProviderNormalization, {
      facing: '+x',
      up: '+y',
      floorY: 0,
      orientation: entry.orientation,
      ...(entry.facingYawDegrees ? { facingYawDegrees: entry.facingYawDegrees } : {}),
      ...(entry.facingYawDegrees ? { facingCorrectionVersion: 1 } : {}),
      targetPlanformSpan: audit.reviewContract.targetPlanformSpan,
      sourceBounds: root?.extras?.nilbotsProviderNormalization?.sourceBounds,
    });
    if (entry.lookId === 'arc-kestrel')
      assertVectorNear(root?.rotation, [0, Math.SQRT1_2, Math.SQRT1_2, 0], entry.lookId);
    if (entry.lookId === 'arc-mortar')
      assertVectorNear(root?.rotation, [0, 1, 0, 0], entry.lookId);

    assert.equal(manifest.ledger.bytes, tierLook.runtime.bytes);
    assert.equal(manifest.ledger.triangles, entry.candidate.triangles);
    assert.equal(manifest.ledger.materials, entry.candidate.materials);
    assert.equal(manifest.ledger.textureCount, entry.candidate.textures);
    assert.equal(manifest.ledger.geometrySha256, tierLook.geometrySha256);
    assert.equal(manifest.ledger.geometryGpuBytes, tierLook.memory.geometryGpuBytes);
    assert.equal(
      manifest.ledger.textureGpuBytesCompressedTarget,
      tierLook.memory.textureGpuBytesCompressedTarget,
    );
    assert.equal(
      manifest.ledger.textureGpuBytesRgba8Fallback,
      tierLook.memory.textureGpuBytesRgba8Fallback,
    );
    assert.equal(manifest.ledger.textureTier, tier.id);
    assert.equal(fleetEntry.artifact, tierLook.runtime.file);
    assert.equal(fleetEntry.approvedArtifact, entry.candidate.file);
    assert.equal(fleetEntry.sha256, tierLook.runtime.sha256);
    assert.equal(fleetEntry.geometrySha256, tierLook.geometrySha256);
    assert.deepEqual(fleetEntry.memory, tierLook.memory);
  }

  assert.deepEqual(ledger.totals, {
    bytes: tier.totals.runtimeTransferBytes,
    triangles: tier.totals.triangles,
    materials: 16,
    textures: 64,
    geometryGpuBytes: tier.totals.geometryGpuBytes,
    textureGpuBytesCompressedTarget: tier.totals.textureGpuBytesCompressedTarget,
    textureGpuBytesRgba8Fallback: tier.totals.textureGpuBytesRgba8Fallback,
    modelGpuBytesCompressedTarget: tier.totals.modelGpuBytesCompressedTarget,
    modelGpuBytesRgba8Fallback: tier.totals.modelGpuBytesRgba8Fallback,
  });
  assert.ok(tier.totals.runtimeTransferBytes <= tier.budgets.fleetTransferBytes);
  assert.ok(tier.totals.textureGpuBytesCompressedTarget <= tier.budgets.compressedTextureGpuBytes);
  assert.ok(tier.totals.textureGpuBytesRgba8Fallback <= tier.budgets.rgba8FallbackTextureGpuBytes);
  assert.ok(tier.totals.modelGpuBytesCompressedTarget <= tier.budgets.compressedModelGpuBytes);
  assert.ok(tier.totals.modelGpuBytesRgba8Fallback <= tier.budgets.rgba8FallbackModelGpuBytes);
});
