import assert from 'node:assert/strict';
import { createHash } from 'node:crypto';
import { spawnSync } from 'node:child_process';
import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import test from 'node:test';
import { fileURLToPath } from 'node:url';

const repository = join(dirname(fileURLToPath(import.meta.url)), '..', '..');
const looksRoot = join(repository, 'web/src/assets/class-looks');
const reviewAuditPath = join(
  repository,
  'art/class-models/runtime-tiers/arc-relay/meshy7-fleet-review-v3/audit.json',
);
const iconEvidencePath = join(
  repository,
  'art/reviews/arc-relay-meshy7-fleet-retexture-v3/top-down-east-icons.json',
);
const review = JSON.parse(readFileSync(reviewAuditPath, 'utf8')) as ReviewAudit;
const ledger = JSON.parse(
  readFileSync(join(repository, 'art/class-models/arc-relay/ledger.json'), 'utf8'),
) as FleetLedger;

interface TextureMemory {
  imageIndex: number;
  name: string | null;
  slots: string[];
  mimeType: string;
  encoding: string;
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

interface ReviewEntry {
  classId: string;
  taskId: string;
  endpoint: string;
  artifact: { file: string; bytes: number; sha256: string };
  triangles: number;
  geometrySha256: string;
  matteRoughness?: { floor: number };
  facing: {
    sourceFacing: string;
    targetFacing: string;
    yawDegrees: number;
    evidence: string;
  };
  budgets: { transferBytes: number; triangles: number };
  memory: LookMemory;
}

interface ReviewAudit {
  id: string;
  status: string;
  sourceAudit: string;
  normalization: {
    facing: string;
    up: string;
    floorY: number;
    targetPlanformSpan: number;
  };
  profile: {
    budgets: { transferBytes: number; triangles: number };
    classOverrides: Record<string, unknown>;
  };
  totals: {
    transferBytes: number;
    triangles: number;
    modelGpuBytesRgba8Fallback: number;
  };
  classes: ReviewEntry[];
}

interface FleetLedger {
  version: number;
  status: string;
  sourceAudit: string;
  runtimeTier: string;
  textureTier: string;
  provider: string;
  endpoint: string;
  model: string;
  modelType: string;
  originalUV: boolean;
  modelOwnedTeamGlow: boolean;
  budgets: {
    default: ReviewAudit['profile']['budgets'];
    classOverrides: Record<string, unknown>;
  };
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
    classId: string;
    taskId: string;
    artifact: string;
    facing: ReviewEntry['facing'];
    bytes: number;
    sha256: string;
    triangles: number;
    materials: number;
    textures: number;
    geometrySha256: string;
    matteRoughnessFloor: number | null;
    memory: LookMemory;
    budgets: ReviewEntry['budgets'];
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
      };
    };
  }[];
  scenes?: { nodes?: number[] }[];
  skins?: unknown[];
  textures?: { extensions?: { EXT_texture_webp?: { source?: number } } }[];
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

test('the owner-approved Meshy 7 V3 promotion is deterministic', () => {
  const result = spawnSync(
    process.execPath,
    [join(repository, 'scripts/class-models/promote-meshy7-fleet-v3.mjs'), '--check'],
    { cwd: repository, encoding: 'utf8' },
  );
  assert.equal(result.status, 0, `${result.stdout}${result.stderr}`);
  assert.match(result.stdout, /Verified 16 Meshy 7 V3 Arc Relay models/);
});

test('WebGL configures KTX2 support before actors request models from mixed asset tiers', () => {
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

test('all runtime Arc Relay GLBs are the exact approved matte WebP V3 artifacts', () => {
  assert.equal(review.id, 'meshy7-fleet-review-v3');
  assert.equal(review.status, 'owner-review');
  assert.equal(review.classes.length, 16);
  assert.equal(new Set(review.classes.map((entry) => entry.classId)).size, 16);
  assert.equal(ledger.version, 4);
  assert.equal(ledger.status, 'owner-approved');
  assert.equal(ledger.runtimeTier, 'art/class-models/runtime-tiers/arc-relay/meshy7-fleet-review-v3/audit.json');
  assert.equal(ledger.sourceAudit, review.sourceAudit);
  assert.equal(ledger.textureTier, 'meshy7-fleet-review-v3-webp');
  assert.equal(ledger.provider, 'Meshy');
  assert.equal(ledger.model, 'meshy-7');
  assert.equal(ledger.endpoint, 'retexture');
  assert.equal(ledger.modelType, 'original-uv-pbr-retexture');
  assert.equal(ledger.originalUV, true);
  assert.equal(ledger.modelOwnedTeamGlow, false);
  assert.deepEqual(ledger.budgets, {
    default: review.profile.budgets,
    classOverrides: review.profile.classOverrides,
  });

  for (const entry of review.classes) {
    const id = `arc-${entry.classId}`;
    const fleetEntry = ledger.looks.find((look) => look.id === id);
    assert.ok(fleetEntry, id);
    const directory = join(looksRoot, id);
    const runtime = readGlb(join(directory, 'model.glb'));
    const approved = readFileSync(join(repository, entry.artifact.file));
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
        textureTier: string;
        sourceSha256: string;
        approvedSha256: string;
        provider: string;
        model: string;
        endpoint: string;
        modelType: string;
        taskId: string;
        orientation: string;
        facingYawDegrees?: number;
        originalUV: boolean;
        matteRoughnessFloor?: number;
      };
      ledger: {
        bytes: number;
        sha256: string;
        triangles: number;
        materials: number;
        textureCount: number;
        geometrySha256: string;
        textureTier: string;
      };
    };

    assert.ok(runtime.bytes.equals(approved), `${id} differs from its approved V3 artifact`);
    assert.equal(runtime.bytes.length, entry.artifact.bytes, id);
    assert.ok(runtime.bytes.length <= entry.budgets.transferBytes, id);
    assert.equal(sha256(runtime.bytes), entry.artifact.sha256, id);
    assert.equal(manifest.id, id);
    assert.equal(manifest.facing, '+x');
    assert.equal(manifest.up, '+y');
    assert.equal(manifest.nodes, undefined, `${id} remains monolithic`);
    assert.ok(manifest.motion, `${id} keeps its existing movement tuning`);
    assert.ok(manifest.signature, `${id} keeps its signature identity`);
    assert.equal(manifest.source.artifact, entry.artifact.file);
    assert.equal(manifest.source.approvedArtifact, entry.artifact.file);
    assert.equal(manifest.source.sourceSha256, entry.artifact.sha256);
    assert.equal(manifest.source.approvedSha256, entry.artifact.sha256);
    assert.equal(manifest.source.textureTier, ledger.textureTier);
    assert.equal(manifest.source.provider, 'Meshy');
    assert.equal(manifest.source.model, 'meshy-7');
    assert.equal(manifest.source.endpoint, 'retexture');
    assert.equal(manifest.source.modelType, 'original-uv-pbr-retexture');
    assert.equal(manifest.source.taskId, entry.taskId);
    assert.equal(manifest.source.orientation, 'identity');
    assert.equal(manifest.source.facingYawDegrees ?? 0, entry.facing.yawDegrees);
    assert.equal(manifest.source.originalUV, true);
    assert.equal(manifest.source.matteRoughnessFloor, entry.matteRoughness?.floor);

    const document = runtime.document;
    assert.equal(document.cameras, undefined, id);
    assert.equal(document.skins, undefined, id);
    assert.equal(document.animations, undefined, id);
    assert.ok(document.nodes?.every((node) => node.camera === undefined && node.skin === undefined));
    assert.equal(document.meshes?.length, 1, id);
    assert.equal(document.meshes?.[0]?.primitives?.length, 1, id);
    assert.equal(document.materials?.length, 1, id);
    assert.equal(document.textures?.length, 3, id);
    assert.equal(document.images?.length, 3, id);
    assert.ok(document.images?.every((image) => image.mimeType === 'image/webp'), id);
    assert.ok(document.textures?.every(
      (texture) => texture.extensions?.EXT_texture_webp?.source !== undefined,
    ), id);
    assert.ok(document.extensionsRequired?.includes('EXT_texture_webp'), id);
    assert.ok(document.extensionsRequired?.includes('KHR_mesh_quantization'), id);
    assert.ok(document.materials?.every(
      (material) => material.extras?.nilbotsRole !== 'team-accent',
    ), `${id} must keep team color renderer-owned`);
    assert.equal(triangles(document), entry.triangles, id);
    assert.ok(entry.triangles <= entry.budgets.triangles, id);

    const root = document.nodes?.[document.scenes?.[0]?.nodes?.[0] ?? -1];
    const normalization = root?.extras?.nilbotsProviderNormalization;
    assert.equal(root?.name, 'chassis', id);
    assert.equal(normalization?.facing, '+x', id);
    assert.equal(normalization?.up, '+y', id);
    assert.equal(normalization?.floorY, 0, id);
    assert.equal(normalization?.orientation, 'identity', id);
    assert.equal(normalization?.targetPlanformSpan, review.normalization.targetPlanformSpan, id);
    assert.equal(normalization?.facingYawDegrees ?? 0, entry.facing.yawDegrees, id);
    if (entry.facing.yawDegrees !== 0)
      assert.equal(normalization?.facingCorrectionVersion, 1, id);

    assert.equal(manifest.ledger.bytes, entry.artifact.bytes);
    assert.equal(manifest.ledger.sha256, entry.artifact.sha256);
    assert.equal(manifest.ledger.triangles, entry.triangles);
    assert.equal(manifest.ledger.materials, 1);
    assert.equal(manifest.ledger.textureCount, 3);
    assert.equal(manifest.ledger.geometrySha256, entry.geometrySha256);
    assert.equal(manifest.ledger.textureTier, ledger.textureTier);
    assert.equal(fleetEntry.artifact, entry.artifact.file);
    assert.equal(fleetEntry.sha256, entry.artifact.sha256);
    assert.equal(fleetEntry.geometrySha256, entry.geometrySha256);
    assert.equal(fleetEntry.matteRoughnessFloor, entry.matteRoughness?.floor ?? null);
    assert.deepEqual(fleetEntry.memory, entry.memory);
    assert.deepEqual(fleetEntry.budgets, entry.budgets);
  }

  assert.deepEqual(ledger.totals, {
    bytes: review.totals.transferBytes,
    triangles: review.totals.triangles,
    materials: 16,
    textures: 48,
    geometryGpuBytes: review.classes.reduce((sum, entry) => sum + entry.memory.geometryGpuBytes, 0),
    textureGpuBytesCompressedTarget: review.classes.reduce(
      (sum, entry) => sum + entry.memory.textureGpuBytesCompressedTarget,
      0,
    ),
    textureGpuBytesRgba8Fallback: review.classes.reduce(
      (sum, entry) => sum + entry.memory.textureGpuBytesRgba8Fallback,
      0,
    ),
    modelGpuBytesCompressedTarget: review.classes.reduce(
      (sum, entry) => sum + entry.memory.modelGpuBytesCompressedTarget,
      0,
    ),
    modelGpuBytesRgba8Fallback: review.totals.modelGpuBytesRgba8Fallback,
  });
});

test('shared web icons are the exact transparent top-down east-facing V3 renders', () => {
  const result = spawnSync(
    process.execPath,
    [join(repository, 'scripts/class-models/build-meshy7-fleet-web-icons.mjs'), '--check'],
    { cwd: repository, encoding: 'utf8' },
  );
  assert.equal(result.status, 0, `${result.stdout}${result.stderr}`);
  assert.match(result.stdout, /Verified 16 top-down east-facing Arc Relay web icons/);

  const evidence = JSON.parse(readFileSync(iconEvidencePath, 'utf8')) as {
    status: string;
    sourceAudit: string;
    contract: {
      projection: string;
      facing: string;
      transparentBackground: boolean;
      runtimePixels: { width: number; height: number };
      frontBandPixels: number;
    };
    sheet: { file: string; sha256: string };
    icons: {
      classId: string;
      source: { file: string; sha256: string; geometrySha256: string };
      camera: string;
      sprite: { file: string; bytes: number; sha256: string; width: number; height: number };
      teamMask: { file: string; bytes: number; sha256: string; frontBandPixels: number; accentedPixels: number };
    }[];
  };
  assert.equal(evidence.status, 'owner-approved-runtime-icon-tier');
  assert.equal(evidence.sourceAudit, 'art/class-models/runtime-tiers/arc-relay/meshy7-fleet-review-v3/audit.json');
  assert.equal(evidence.contract.projection, 'strict orthographic top-down');
  assert.equal(evidence.contract.facing, '+X maps to screen-right/east');
  assert.equal(evidence.contract.transparentBackground, true);
  assert.deepEqual(evidence.contract.runtimePixels, { width: 192, height: 192 });
  assert.equal(evidence.icons.length, 16);
  assert.equal(new Set(evidence.icons.map((icon) => icon.classId)).size, 16);
  assert.equal(
    sha256(readFileSync(join(repository, evidence.sheet.file))),
    evidence.sheet.sha256,
  );

  for (const icon of evidence.icons) {
    const entry = review.classes.find((candidate) => candidate.classId === icon.classId);
    assert.ok(entry, icon.classId);
    assert.equal(icon.source.file, entry.artifact.file);
    assert.equal(icon.source.sha256, entry.artifact.sha256);
    assert.equal(icon.source.geometrySha256, entry.geometrySha256);
    assert.match(icon.camera, /top-down.*\+X maps screen-right\/east/);
    assert.equal(icon.sprite.file, `web/src/assets/class-looks/arc-${icon.classId}/sprite.png`);
    assert.equal(icon.teamMask.file, `web/src/assets/class-looks/arc-${icon.classId}/team-mask.png`);
    assert.equal(icon.sprite.width, 192);
    assert.equal(icon.sprite.height, 192);
    assert.equal(icon.teamMask.frontBandPixels, evidence.contract.frontBandPixels);
    assert.ok(icon.teamMask.accentedPixels > 0);

    for (const artifact of [icon.sprite, icon.teamMask]) {
      const bytes = readFileSync(join(repository, artifact.file));
      assert.equal(bytes.length, artifact.bytes, artifact.file);
      assert.equal(sha256(bytes), artifact.sha256, artifact.file);
      assert.equal(bytes.toString('hex', 0, 8), '89504e470d0a1a0a', artifact.file);
      assert.equal(bytes.readUInt32BE(16), 192, artifact.file);
      assert.equal(bytes.readUInt32BE(20), 192, artifact.file);
    }
    const teamTemplate = readFileSync(
      join(looksRoot, `arc-${icon.classId}`, 'team.svg'),
      'utf8',
    );
    assert.match(teamTemplate, /facing East in a strict top-down projection/);
  }
});
