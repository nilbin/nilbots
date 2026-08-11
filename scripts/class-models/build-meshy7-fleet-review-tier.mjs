#!/usr/bin/env node

import { spawnSync } from 'node:child_process';
import { createHash } from 'node:crypto';
import {
  existsSync,
  mkdirSync,
  mkdtempSync,
  readFileSync,
  rmSync,
  writeFileSync,
} from 'node:fs';
import { tmpdir } from 'node:os';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import {
  geometryFingerprint,
  inspectModelMemory,
  readGlb,
  triangleCount,
} from './model-memory.mjs';

const repository = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..', '..');
const check = process.argv.includes('--check');
const classArgumentIndex = process.argv.indexOf('--class');
const classFilter = classArgumentIndex === -1 ? null : process.argv[classArgumentIndex + 1];
const sourceAuditPath = path.resolve(
  repository,
  option(
    '--source-audit',
    'art/class-models/provider-runs/meshy/arc-relay-meshy7-fleet/fleet-audit.json',
  ),
);
const sourceAudit = JSON.parse(readFileSync(sourceAuditPath, 'utf8'));
const tierId = option('--tier-id', 'meshy7-fleet-review-v1');
if (!/^meshy7-fleet-review-v[1-9][0-9]*$/.test(tierId))
  throw new Error(`Unsafe --tier-id ${tierId}.`);
const tierDirectory = path.join(
  repository,
  'art/class-models/runtime-tiers/arc-relay',
  tierId,
);
const auditPath = path.join(tierDirectory, 'audit.json');
const kestrelTierPath = path.join(
  repository,
  'art/class-models/runtime-tiers/arc-relay/meshy7-kestrel-review-v1/arc-kestrel-lean.glb',
);
const targetPlanformSpan = 0.99;
const matteRoughnessFloor = 0.65;
const facingCorrections = new Map([
  ['kestrel', {
    sourceFacing: '-x',
    yawDegrees: 180,
    evidence: '+X camera matches the tapered beam face; -X shows the paired rear exhaust blocks.',
  }],
  ['palisade', {
    sourceFacing: '-x',
    yawDegrees: 180,
    evidence: '+X camera matches the broad blue shield/front; -X shows the gold-and-black rear block.',
  }],
  ['towline', {
    sourceFacing: '-x',
    yawDegrees: 180,
    evidence: '+X camera exposes the cable spool and forward beam face; -X is the closed rear plate.',
  }],
  ['patchbay', {
    sourceFacing: '-x',
    yawDegrees: 180,
    evidence: '+X camera matches the orange patch face and lateral repair arm; -X is the rear block.',
  }],
  ['lantern', {
    sourceFacing: '+z',
    yawDegrees: 90,
    evidence: 'Before correction, the -Z camera matches the circular black beam aperture and +Z shows the rectangular back pack.',
  }],
  ['mortar', {
    sourceFacing: '+z',
    yawDegrees: 90,
    evidence: 'Before correction, the -Z camera reveals the forward emitter below the launch deck and +Z is the boxed rear.',
  }],
  ['minesmith', {
    sourceFacing: '-x',
    yawDegrees: 180,
    evidence: '+X camera shows the twin mine canisters and open front bay; -X is the closed rear shell.',
  }],
  ['hush', {
    sourceFacing: 'fourfold-radial',
    yawDegrees: 180,
    evidence: 'The provider output is fourfold rotationally symmetric with no unique front; the ordered-source lobe registration is retained.',
  }],
  ['relay', {
    sourceFacing: '+x',
    yawDegrees: 0,
    evidence: 'Before correction, the +X axis looks into the open U throat from the source front; -X is the closed crossbar/back.',
  }],
  ['switchback', {
    sourceFacing: '-x',
    yawDegrees: 180,
    evidence: 'The provider output made the two axial ends visually equivalent; the ordered-source registration is retained.',
  }],
  ['longshot', {
    sourceFacing: '-x',
    yawDegrees: 180,
    evidence: '+X camera matches the low beam slit; -X shows the orange-outlined rear cells.',
  }],
  ['mason', {
    sourceFacing: '-x',
    yawDegrees: 180,
    evidence: '+X camera matches the twin prongs and open forward work bay; -X is the closed rear.',
  }],
  ['sunder', {
    sourceFacing: '+z',
    yawDegrees: 90,
    evidence: 'Before correction, the -Z camera matches the circular beam face and +Z shows the closed rear panel.',
  }],
  ['repulsor', {
    sourceFacing: 'twelvefold-radial',
    yawDegrees: 180,
    evidence: 'The provider output is twelvefold rotationally symmetric with no unique front; the ordered-source segment registration is retained.',
  }],
  ['veil', {
    sourceFacing: '+z',
    yawDegrees: 90,
    evidence: 'Before correction, the -Z camera matches the concave open front and +Z shows the closed rear.',
  }],
  ['nest', {
    sourceFacing: '-x',
    yawDegrees: 180,
    evidence: '+X camera matches the open center lane and forward pod caps; -X shows the rear plates.',
  }],
]);
const toolVersion = '4.4.2';
const profile = {
  simplifyRatio: 0.04,
  simplifyError: 0.0025,
  textureSize: 512,
  budgets: { transferBytes: 4_000_000, triangles: 100_000 },
};
const profileOverrides = new Map([
  [
    'sunder',
    {
      simplifyError: 0.01,
      budgets: { transferBytes: 9_000_000, triangles: 220_000 },
      reason:
        'The disconnected layered shell reaches its topology limit before the shared lean target; retained for owner review rather than applying a second destructive pass.',
    },
  ],
]);
const outputTextureCountOverrides = new Map([
  ['mortar', 3],
]);
const entries = sourceAudit.entries.map((entry) => ({
  ...entry,
  facingCorrection: facingCorrections.get(entry.classId),
  sourcePath: path.join(repository, entry.rawModel.file),
  geometrySourcePath: path.join(
    repository,
    entry.retextured ? entry.source.rawModel.file : entry.rawModel.file,
  ),
  outputPath: entry.classId === 'kestrel'
    ? kestrelTierPath
    : path.join(tierDirectory, `arc-${entry.classId}-lean.glb`),
  reused: entry.classId === 'kestrel',
  matteRoughnessFloor: entry.newInCurrentBatch ? matteRoughnessFloor : null,
}));

if (entries.length !== 16 || new Set(entries.map((entry) => entry.classId)).size !== 16)
  throw new Error('Meshy 7 fleet audit must contain exactly 16 unique Arc Relay classes.');
if (entries.some((entry) => !entry.facingCorrection) || facingCorrections.size !== entries.length)
  throw new Error('Every Arc Relay class must have one source-facing correction.');
if (classArgumentIndex !== -1 && !entries.some((entry) => entry.classId === classFilter))
  throw new Error(`Unknown --class ${classFilter ?? 'value'}.`);
for (const entry of entries) validateSource(entry);

mkdirSync(tierDirectory, { recursive: true });
if (!check) buildProfiles();

const classes = entries.map(inspectEntry);
const totals = classes.reduce(
  (sum, entry) => ({
    transferBytes: sum.transferBytes + entry.artifact.bytes,
    triangles: sum.triangles + entry.triangles,
    modelGpuBytesRgba8Fallback:
      sum.modelGpuBytesRgba8Fallback + entry.memory.modelGpuBytesRgba8Fallback,
  }),
  { transferBytes: 0, triangles: 0, modelGpuBytesRgba8Fallback: 0 },
);
const audit = {
  schemaVersion: 1,
  id: tierId,
  status: 'owner-review',
  generator: 'scripts/class-models/build-meshy7-fleet-review-tier.mjs',
  sourceAudit: path.relative(repository, sourceAuditPath),
  toolchain: {
    gltfTransform: toolVersion,
    simplifier: 'meshoptimizer through glTF-Transform',
    textureEncoding: 'WebP review tier; KTX2 is required before shipping',
  },
  normalization: {
    orientation: 'identity',
    sourceFacing: 'per-class audited from pinned strict-front and strict-back inputs',
    facing: '+x',
    up: '+y',
    floorY: 0,
    targetPlanformSpan,
    facingCorrections: Object.fromEntries(facingCorrections),
  },
  profile: {
    id: 'lean',
    settings: {
      simplifyRatio: profile.simplifyRatio,
      simplifyError: profile.simplifyError,
      lockBorder: false,
      textureSize: profile.textureSize,
      geometryCompression: 'quantize',
      textureEncoding: 'WebP',
    },
    classOverrides: Object.fromEntries(profileOverrides),
    budgets: profile.budgets,
  },
  contract: {
    purpose:
      'Reversible in-renderer fleet review only; no approved runtime model is replaced or promoted.',
    geometry: 'lossy review simplification from pinned Meshy 7 provider masters',
    materials: 'one fused authored material; team ownership remains renderer-owned',
    ...(entries.some((entry) => entry.retextured)
      ? {
          retexture:
            'Meshy original-UV PBR maps transplanted onto pinned source geometry; provider rewritten vertex/index streams are not used',
        }
      : {}),
    ...(entries.some((entry) => entry.matteRoughnessFloor !== null)
      ? {
          matteFinish:
            'Current-batch metallic-roughness green channel is deterministically floored at 0.65; raw provider artifacts, base color, metallic, normals, and retained approvals are unchanged',
        }
      : {}),
    tangentSpace:
      'normal-mapped without authored tangents; Three.js derivative tangent-space review required',
  },
  totals,
  classes,
};

if (check) {
  const recorded = JSON.parse(readFileSync(auditPath, 'utf8'));
  if (JSON.stringify(recorded, null, 2) !== JSON.stringify(audit, null, 2))
    throw new Error('Meshy 7 fleet review-tier audit drifted from the current artifacts.');
  console.log(`Verified ${classes.length} ${tierId} artifacts.`);
} else {
  writeFileSync(auditPath, `${JSON.stringify(audit, null, 2)}\n`);
  console.log(`Built ${classes.length} ${tierId} artifacts.`);
}

function validateSource(entry) {
  if (!existsSync(entry.sourcePath))
    throw new Error(`${entry.classId}: missing ignored source ${entry.rawModel.file}.`);
  const source = readFileSync(entry.sourcePath);
  const sourceSha256 = sha256(source);
  if (source.length !== entry.rawModel.bytes || sourceSha256 !== entry.rawModel.sha256)
    throw new Error(`${entry.classId}: source bytes or SHA-256 drifted from the fleet audit.`);
  if (entry.geometrySourcePath !== entry.sourcePath) {
    if (!existsSync(entry.geometrySourcePath))
      throw new Error(`${entry.classId}: missing original-UV geometry source.`);
    const geometrySource = readFileSync(entry.geometrySourcePath);
    if (
      geometrySource.length !== entry.source.rawModel.bytes ||
      sha256(geometrySource) !== entry.source.rawModel.sha256
    )
      throw new Error(`${entry.classId}: original-UV geometry source drifted.`);
  }
}

function buildProfiles() {
  const temporary = mkdtempSync(path.join(tmpdir(), 'nilbots-meshy7-fleet-tier-'));
  try {
    for (const entry of entries) {
      if (classFilter && entry.classId !== classFilter) continue;
      if (entry.reused) {
        if (!existsSync(entry.outputPath))
          throw new Error('The reviewed Kestrel lean artifact is missing.');
        console.log('Reusing reviewed Kestrel lean tier.');
        continue;
      }
      console.log(`Building ${entry.classId} lean review tier...`);
      const settings = { ...profile, ...profileOverrides.get(entry.classId) };
      const materialized = path.join(temporary, `${entry.classId}-materialized.glb`);
      const matteMaterialized = path.join(temporary, `${entry.classId}-matte-materialized.glb`);
      const normalized = path.join(temporary, `${entry.classId}-normalized.glb`);
      const optimized = path.join(temporary, `${entry.classId}-optimized.glb`);
      if (entry.geometrySourcePath !== entry.sourcePath)
        run(process.execPath, [
          path.join(repository, 'scripts/class-models/transplant-meshy-retexture.mjs'),
          entry.geometrySourcePath,
          entry.sourcePath,
          materialized,
        ]);
      if (entry.matteRoughnessFloor !== null)
        run(process.execPath, [
          path.join(repository, 'scripts/class-models/enforce-matte-roughness.mjs'),
          materialized,
          matteMaterialized,
          String(entry.matteRoughnessFloor),
        ]);
      const normalizationInput = entry.matteRoughnessFloor !== null
        ? matteMaterialized
        : entry.geometrySourcePath === entry.sourcePath
          ? entry.sourcePath
          : materialized;
      run(process.execPath, [
        path.join(repository, 'scripts/class-models/normalize-provider-glb.mjs'),
        normalizationInput,
        normalized,
        String(targetPlanformSpan),
        'identity',
        '--strip-authored-tangents',
      ]);
      run('npx', [
        '--yes',
        `@gltf-transform/cli@${toolVersion}`,
        'optimize',
        normalized,
        optimized,
        '--compress',
        'quantize',
        '--texture-compress',
        'webp',
        '--texture-size',
        String(profile.textureSize),
        '--flatten',
        'false',
        '--join',
        'false',
        '--instance',
        'false',
        '--palette',
        'false',
        '--simplify',
        'true',
        '--simplify-ratio',
        String(settings.simplifyRatio),
        '--simplify-error',
        String(settings.simplifyError),
        '--simplify-lock-border',
        'false',
        '--weld',
        'true',
      ]);
      run(process.execPath, [
        path.join(repository, 'scripts/class-models/correct-provider-facing.mjs'),
        optimized,
        entry.outputPath,
        String(entry.facingCorrection.yawDegrees),
      ]);
    }
  } finally {
    rmSync(temporary, { recursive: true, force: true });
  }
}

function inspectEntry(entry) {
  if (!existsSync(entry.outputPath)) throw new Error(`${entry.classId}: output GLB is missing.`);
  const bytes = readFileSync(entry.outputPath);
  const { document } = readGlb(bytes, entry.classId);
  const scene = document.scenes?.[document.scene ?? 0];
  const root = document.nodes?.[scene?.nodes?.[0]];
  const normalization = root?.extras?.nilbotsProviderNormalization;
  const triangles = triangleCount(document);
  const primitives = (document.meshes ?? []).flatMap((mesh) => mesh.primitives ?? []);
  const textureCount = document.textures?.length ?? 0;
  const expectedTextures = outputTextureCountOverrides.get(entry.classId) ?? entry.rawModel.textures;
  if (
    document.cameras ||
    document.skins ||
    document.animations ||
    document.materials?.length !== 1 ||
    textureCount !== expectedTextures ||
    document.images?.length !== expectedTextures ||
    document.meshes?.length !== 1 ||
    primitives.length !== 1
  )
    throw new Error(`${entry.classId}: review artifact changed the monolithic model contract.`);
  if (
    normalization?.facing !== '+x' ||
    normalization?.up !== '+y' ||
    normalization?.floorY !== 0 ||
    normalization?.orientation !== 'identity' ||
    normalization?.targetPlanformSpan !== targetPlanformSpan ||
    normalization?.facingYawDegrees !== entry.facingCorrection.yawDegrees ||
    normalization?.facingCorrectionVersion !== 1
  )
    throw new Error(`${entry.classId}: review artifact normalization drifted.`);
  if (
    !(document.extensionsRequired ?? []).includes('KHR_mesh_quantization') ||
    (document.images ?? []).some((image) => image.mimeType !== 'image/webp')
  )
    throw new Error(`${entry.classId}: expected quantized geometry and embedded WebP maps.`);
  const budgets = profileOverrides.get(entry.classId)?.budgets ?? profile.budgets;
  if (bytes.length > budgets.transferBytes || triangles > budgets.triangles)
    throw new Error(`${entry.classId}: lean review budget exceeded.`);
  const positionVertices = primitives.reduce(
    (sum, primitive) =>
      sum + (document.accessors?.[primitive.attributes?.POSITION]?.count ?? 0),
    0,
  );
  const matteMetadata = document.asset?.extras?.nilbotsMatteRoughness;
  if (
    entry.matteRoughnessFloor !== null &&
    (matteMetadata?.floor !== entry.matteRoughnessFloor ||
      matteMetadata?.minimumAfter < entry.matteRoughnessFloor ||
      matteMetadata?.baseColorChanged !== false ||
      matteMetadata?.metallicChanged !== false ||
      matteMetadata?.normalChanged !== false)
  )
    throw new Error(`${entry.classId}: matte roughness provenance is missing or invalid.`);
  return {
    classId: entry.classId,
    taskId: entry.taskId,
    endpoint: entry.endpoint,
    source: entry.retextured
      ? {
          geometry: {
            file: path.relative(repository, entry.geometrySourcePath),
            bytes: entry.source.rawModel.bytes,
            sha256: entry.source.rawModel.sha256,
          },
          textureDonor: {
            file: path.relative(repository, entry.sourcePath),
            bytes: entry.rawModel.bytes,
            sha256: entry.rawModel.sha256,
            preservedOriginalUvs: true,
          },
        }
      : {
          file: path.relative(repository, entry.sourcePath),
          bytes: entry.rawModel.bytes,
          sha256: entry.rawModel.sha256,
        },
    reusedReviewedKestrel: entry.reused,
    ...(entry.matteRoughnessFloor !== null
      ? { matteRoughness: matteMetadata }
      : {}),
    facing: {
      sourceFacing: entry.facingCorrection.sourceFacing,
      targetFacing: '+x',
      yawDegrees: entry.facingCorrection.yawDegrees,
      evidence: entry.facingCorrection.evidence,
    },
    profileOverrides: profileOverrides.get(entry.classId) ?? null,
    budgets,
    artifact: {
      file: path.relative(repository, entry.outputPath),
      bytes: bytes.length,
      sha256: sha256(bytes),
    },
    triangles,
    positionVertices,
    meshes: document.meshes.length,
    primitives: primitives.length,
    materials: document.materials.length,
    textures: textureCount,
    geometrySha256: geometryFingerprint(bytes, entry.classId),
    memory: inspectModelMemory(bytes, entry.classId),
  };
}

function run(command, arguments_) {
  const result = spawnSync(command, arguments_, {
    cwd: repository,
    encoding: 'utf8',
    stdio: 'inherit',
  });
  if (result.error) throw result.error;
  if (result.status !== 0)
    throw new Error(`${command} failed with exit code ${result.status}.`);
}

function sha256(value) {
  return createHash('sha256').update(value).digest('hex');
}

function option(name, fallback) {
  const index = process.argv.indexOf(name);
  if (index === -1) return fallback;
  const value = process.argv[index + 1];
  if (!value || value.startsWith('--')) throw new Error(`${name} requires a value.`);
  return value;
}
