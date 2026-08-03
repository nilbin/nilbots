import assert from 'node:assert/strict';
import { createHash } from 'node:crypto';
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
const audit = JSON.parse(readFileSync(auditPath, 'utf8')) as FleetAudit;
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

interface FleetLedger {
  sourceAudit: string;
  provider: string;
  endpoint: string;
  model: string;
  modelType: string;
  meshContract: string;
  modelOwnedTeamGlow: boolean;
  budgetBytesPerLook: number;
  totals: { bytes: number; triangles: number; materials: number; textures: number };
  looks: {
    id: string;
    taskId: string;
    artifact: string;
    orientation: string;
    bytes: number;
    sha256: string;
    triangles: number;
    materials: number;
    textures: number;
    targetPlanformSpan: number;
    floorY: number;
  }[];
}

interface GlbDocument {
  accessors?: { count?: number }[];
  animations?: unknown[];
  cameras?: unknown[];
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

test('all runtime Arc Relay GLBs are the exact approved Meshy candidates', () => {
  assert.equal(approved.length, 16);
  assert.equal(new Set(approved.map((entry) => entry.lookId)).size, 16);
  assert.equal(ledger.looks.length, 16);
  assert.equal(ledger.sourceAudit, 'art/class-models/provider-runs/meshy/arc-fleet-review/fleet-audit.json');
  assert.equal(ledger.provider, audit.provider);
  assert.equal(ledger.endpoint, audit.endpoint);
  assert.equal(ledger.model, audit.model);
  assert.equal(ledger.modelType, audit.modelType);
  assert.equal(ledger.meshContract, audit.reviewContract.meshContract);
  assert.equal(ledger.modelOwnedTeamGlow, false);

  const totals = { bytes: 0, triangles: 0, materials: 0, textures: 0 };
  for (const entry of approved) {
    const directory = join(looksRoot, entry.lookId);
    const runtime = readGlb(join(directory, 'model.glb'));
    const candidate = readFileSync(join(repository, entry.candidate.file));
    const manifest = JSON.parse(readFileSync(join(directory, 'model3d.json'), 'utf8')) as {
      id: string;
      facing: string;
      up: string;
      nodes?: unknown;
      motion?: unknown;
      signature?: string;
      source: {
        artifact: string;
        sourceSha256: string;
        provider: string;
        model: string;
        endpoint: string;
        modelType: string;
        taskId: string;
        orientation: string;
      };
      ledger: {
        bytes: number;
        sha256: string;
        triangles: number;
        materials: number;
        textureCount: number;
      };
    };
    const fleetEntry = ledger.looks.find((look) => look.id === entry.lookId);
    assert.ok(fleetEntry, entry.lookId);

    assert.ok(runtime.bytes.equals(candidate), `${entry.lookId} differs from approved candidate`);
    assert.equal(runtime.bytes.length, entry.candidate.bytes, entry.lookId);
    assert.ok(runtime.bytes.length <= ledger.budgetBytesPerLook, entry.lookId);
    assert.equal(sha256(runtime.bytes), entry.candidate.sha256, entry.lookId);
    assert.equal(fleetEntry.sha256, entry.candidate.sha256, entry.lookId);
    assert.equal(manifest.ledger.sha256, entry.candidate.sha256, entry.lookId);
    assert.equal(manifest.source.sourceSha256, entry.candidate.sha256, entry.lookId);

    assert.equal(manifest.id, entry.lookId);
    assert.equal(manifest.facing, '+x');
    assert.equal(manifest.up, '+y');
    assert.equal(manifest.nodes, undefined, `${entry.lookId} is intentionally monolithic`);
    assert.ok(manifest.motion, `${entry.lookId} keeps root-level motion tuning`);
    assert.ok(manifest.signature, `${entry.lookId} keeps signature identity`);
    assert.equal(manifest.source.artifact, entry.candidate.file);
    assert.equal(manifest.source.provider, audit.provider);
    assert.equal(manifest.source.model, audit.model);
    assert.equal(manifest.source.endpoint, audit.endpoint);
    assert.equal(manifest.source.modelType, audit.modelType);
    assert.equal(manifest.source.taskId, entry.taskId);
    assert.equal(manifest.source.orientation, entry.orientation);

    const document = runtime.document;
    assert.equal(document.cameras, undefined, entry.lookId);
    assert.equal(document.skins, undefined, entry.lookId);
    assert.equal(document.animations, undefined, entry.lookId);
    assert.ok(document.nodes?.every((node) => node.camera === undefined && node.skin === undefined));
    assert.equal(document.materials?.length, 1, entry.lookId);
    assert.equal(document.textures?.length, 4, entry.lookId);
    assert.equal(document.images?.length, 4, entry.lookId);
    assert.ok(document.images?.every((image) => image.mimeType === 'image/webp'), entry.lookId);
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
      targetPlanformSpan: audit.reviewContract.targetPlanformSpan,
      sourceBounds: root?.extras?.nilbotsProviderNormalization?.sourceBounds,
    });

    assert.equal(manifest.ledger.bytes, entry.candidate.bytes);
    assert.equal(manifest.ledger.triangles, entry.candidate.triangles);
    assert.equal(manifest.ledger.materials, entry.candidate.materials);
    assert.equal(manifest.ledger.textureCount, entry.candidate.textures);
    assert.deepEqual(fleetEntry, {
      id: entry.lookId,
      taskId: entry.taskId,
      artifact: entry.candidate.file,
      orientation: entry.orientation,
      bytes: entry.candidate.bytes,
      sha256: entry.candidate.sha256,
      triangles: entry.candidate.triangles,
      materials: entry.candidate.materials,
      textures: entry.candidate.textures,
      targetPlanformSpan: audit.reviewContract.targetPlanformSpan,
      floorY: 0,
    });

    totals.bytes += entry.candidate.bytes;
    totals.triangles += entry.candidate.triangles;
    totals.materials += entry.candidate.materials;
    totals.textures += entry.candidate.textures;
  }
  assert.deepEqual(totals, ledger.totals);
});
