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
const toolVersion = '4.4.2';
const profile = {
  simplifyRatio: 0.04,
  simplifyError: 0.0025,
  textureSize: 512,
  budgets: { transferBytes: 2_000_000, triangles: 100_000 },
};
const outputDirectory = path.join(
  repository,
  'art/class-models/runtime-tiers/arc-relay/meshy7-signature-props-review-v3',
);
const auditPath = path.join(outputDirectory, 'audit.json');
const tripGeometry = source(
  'art/class-models/provider-runs/meshy/arc-relay-meshy7-signature-props-v3/' +
    'arc-trip-node-v3/019ff5c4-af6d-7bee-bfac-591b78b34c52/raw-model.glb',
  65_112_900,
  '3c086868cdc4025f5db44d2622b986eba6b7c4897a184395223f2e5b79f6921a',
);
const tripTextureDonor = source(
  'art/class-models/provider-runs/meshy/arc-relay-meshy7-signature-props-retexture-v3/' +
    'arc-trip-node-v3/019ff5eb-39a6-7432-bdb5-8efad5ef17d6/raw-model.glb',
  80_931_284,
  'a2d71be750eca6ebcc024dcb0e341b799396887f814d0ec0b18ad515a1223345',
);
const sentinelProvider = source(
  'art/class-models/provider-runs/meshy/arc-relay-meshy7-signature-props-v3/' +
    'arc-sentinel-seed-v3/019ff5c8-f17f-7083-a432-71f16c9f1c05/raw-model.glb',
  63_731_588,
  'cc024f1659f2462ee796c09bde8072556a897739459b3f8df9d6f115efe1e1b1',
);
const artifacts = {
  trip: path.join(outputDirectory, 'arc-trip-node-lean.glb'),
  sentinel: path.join(outputDirectory, 'arc-sentinel-seed-lean.glb'),
};

for (const entry of [tripGeometry, tripTextureDonor, sentinelProvider]) verifySource(entry);
mkdirSync(outputDirectory, { recursive: true });
if (!check) build();

const classes = [inspectTrip(), inspectSentinel()];
const audit = {
  schemaVersion: 1,
  id: 'meshy7-signature-props-review-v3',
  status: 'owner-review',
  generator: 'scripts/class-models/build-meshy7-signature-prop-review-tier.mjs',
  toolchain: {
    gltfTransform: toolVersion,
    simplifier: 'meshoptimizer through glTF-Transform',
    textureEncoding: 'WebP lean tier; explicit owner approval is required before shipping',
  },
  normalization: {
    orientation: 'identity',
    facing: '+x',
    up: '+y',
    floorY: 0,
    targetPlanformSpan: 0.99,
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
    budgets: profile.budgets,
  },
  contract: {
    purpose:
      'Owner review in the real renderer; promotion must copy the explicitly approved hashes byte-for-byte.',
    tripRetexture:
      'Meshy 7 original-UV PBR maps transplanted onto the pinned Trip geometry, then roughness deterministically floored at 0.72.',
    sentinelSplit:
      'Every pinned Sentinel triangle assigned exactly once to stationary-base or yaw-head by indexed connected-component centroid; the head rotates about +Y at the normalized model origin.',
    teamOwnership: 'No teal or orange team paint is baked into either model.',
    tangentSpace: 'Normal-mapped without authored tangents; Three.js derivative tangent-space review required.',
  },
  totals: {
    transferBytes: classes.reduce((sum, entry) => sum + entry.artifact.bytes, 0),
    triangles: classes.reduce((sum, entry) => sum + entry.triangles, 0),
    modelGpuBytesRgba8Fallback: classes.reduce(
      (sum, entry) => sum + entry.memory.modelGpuBytesRgba8Fallback,
      0,
    ),
  },
  classes,
};

if (check) {
  const recorded = JSON.parse(readFileSync(auditPath, 'utf8'));
  if (JSON.stringify(recorded, null, 2) !== JSON.stringify(audit, null, 2))
    throw new Error('Signature-prop review-tier audit drifted from the current artifacts.');
  console.log('Verified Trip and split Sentinel review artifacts.');
} else {
  writeFileSync(auditPath, `${JSON.stringify(audit, null, 2)}\n`);
  console.log('Built Trip and split Sentinel review artifacts.');
}

function build() {
  const temporary = mkdtempSync(path.join(tmpdir(), 'nilbots-signature-props-tier-'));
  try {
    const tripMaterialized = path.join(temporary, 'trip-materialized.glb');
    const tripNormalized = path.join(temporary, 'trip-normalized.glb');
    const tripFacing = path.join(temporary, 'trip-facing.glb');
    const tripMatte = path.join(temporary, 'trip-matte.glb');
    run(process.execPath, [
      script('transplant-meshy-retexture.mjs'),
      tripGeometry.absolute,
      tripTextureDonor.absolute,
      tripMaterialized,
    ]);
    run(process.execPath, [
      script('normalize-provider-glb.mjs'),
      tripMaterialized,
      tripNormalized,
      '0.99',
      'identity',
      '--strip-authored-tangents',
    ]);
    run(process.execPath, [
      script('correct-provider-facing.mjs'),
      tripNormalized,
      tripFacing,
      '-90',
    ]);
    run(process.execPath, [
      script('enforce-matte-roughness.mjs'),
      tripFacing,
      tripMatte,
      '0.72',
    ]);
    optimize(tripMatte, artifacts.trip);

    // Keep the source basename stable because the split records it as provenance.
    const sentinelNormalized = path.join(temporary, 'model-normalized-identity-raw.glb');
    const sentinelSplit = path.join(temporary, 'sentinel-split.glb');
    run(process.execPath, [
      script('normalize-provider-glb.mjs'),
      sentinelProvider.absolute,
      sentinelNormalized,
      '0.99',
      'identity',
      '--strip-authored-tangents',
    ]);
    run(process.execPath, [
      script('split-sentinel-yaw-head.mjs'),
      sentinelNormalized,
      sentinelSplit,
      '-0.02',
    ]);
    optimize(sentinelSplit, artifacts.sentinel);
  } finally {
    rmSync(temporary, { recursive: true, force: true });
  }
}

function inspectTrip() {
  const entry = inspectArtifact(artifacts.trip, 'trip-node', 1, 3);
  const transplant = entry.document.asset?.extras?.nilbotsRetextureTransplant;
  const matte = entry.document.asset?.extras?.nilbotsMatteRoughness;
  const normalization = normalizationOf(entry.document);
  if (
    transplant?.preservedOriginalUvs !== true ||
    transplant?.donorImages !== 3 ||
    matte?.floor !== 0.72 ||
    matte?.minimumAfter < 0.72 ||
    matte?.baseColorChanged !== false ||
    matte?.metallicChanged !== false ||
    matte?.normalChanged !== false ||
    normalization?.facingYawDegrees !== -90
  )
    throw new Error('Trip retexture, matte, or facing provenance is invalid.');
  return {
    signatureId: 'trip-node',
    providerTaskId: '019ff5eb-39a6-7432-bdb5-8efad5ef17d6',
    endpoint: 'retexture',
    source: {
      geometry: portableSource(tripGeometry),
      textureDonor: { ...portableSource(tripTextureDonor), preservedOriginalUvs: true },
    },
    facing: { sourceFacing: '+z', targetFacing: '+x', yawDegrees: -90 },
    matteRoughness: matte,
    ...portableInspection(entry),
  };
}

function inspectSentinel() {
  const entry = inspectArtifact(artifacts.sentinel, 'sentinel-seed', 2, 4);
  const split = entry.document.asset?.extras?.nilbotsSentinelYawSplit;
  const yawHead = entry.document.nodes?.find((node) => node.name === 'yaw-head');
  const stationary = entry.document.nodes?.find((node) => node.name === 'stationary-base');
  if (
    split?.partitionUnionVerified !== true ||
    split.sourceTriangles !== split.stationaryTriangles + split.yawHeadTriangles ||
    split.sourceSha256 !==
      'd2e3283a91c29d05af9f27f2b714246caf734d6f3e1c89b318f010d327f5f284' ||
    yawHead?.extras?.yawAxis !== '+y' ||
    JSON.stringify(yawHead?.extras?.pivot) !== '[0,0,0]' ||
    stationary?.mesh === undefined
  )
    throw new Error('Sentinel yaw-head partition contract is invalid.');
  return {
    signatureId: 'sentinel-seed',
    providerTaskId: '019ff5c8-f17f-7083-a432-71f16c9f1c05',
    endpoint: 'multi-image-to-3d',
    source: portableSource(sentinelProvider),
    split,
    nodes: {
      stationary: 'stationary-base',
      yawPivot: 'yaw-head',
      yawMesh: 'yaw-head-mesh',
    },
    facing: { sourceFacing: 'radial/neutral', targetFacing: '+x', yawDegrees: 0 },
    ...portableInspection(entry),
  };
}

function inspectArtifact(absolute, label, expectedMeshes, expectedTextures) {
  if (!existsSync(absolute)) throw new Error(`${label}: review artifact is missing.`);
  const bytes = readFileSync(absolute);
  const { document } = readGlb(bytes, label);
  const triangles = triangleCount(document);
  const primitives = (document.meshes ?? []).flatMap((mesh) => mesh.primitives ?? []);
  const normalization = normalizationOf(document);
  if (
    document.cameras ||
    document.skins ||
    document.animations ||
    document.materials?.length !== 1 ||
    document.meshes?.length !== expectedMeshes ||
    primitives.length !== expectedMeshes ||
    document.textures?.length !== expectedTextures ||
    document.images?.length !== expectedTextures ||
    normalization?.facing !== '+x' ||
    normalization?.up !== '+y' ||
    normalization?.floorY !== 0 ||
    normalization?.orientation !== 'identity' ||
    normalization?.targetPlanformSpan !== 0.99 ||
    !(document.extensionsRequired ?? []).includes('KHR_mesh_quantization') ||
    (document.images ?? []).some((image) => image.mimeType !== 'image/webp') ||
    bytes.length > profile.budgets.transferBytes ||
    triangles > profile.budgets.triangles
  )
    throw new Error(`${label}: review artifact contract or budget drifted.`);
  return {
    absolute,
    bytes,
    document,
    triangles,
    primitives,
    memory: inspectModelMemory(bytes, label),
  };
}

function portableInspection(entry) {
  return {
    artifact: {
      file: path.relative(repository, entry.absolute),
      bytes: entry.bytes.length,
      sha256: sha256(entry.bytes),
    },
    budgets: profile.budgets,
    triangles: entry.triangles,
    positionVertices: entry.primitives.reduce(
      (sum, primitive) =>
        sum + (entry.document.accessors?.[primitive.attributes?.POSITION]?.count ?? 0),
      0,
    ),
    meshes: entry.document.meshes.length,
    primitives: entry.primitives.length,
    materials: entry.document.materials.length,
    textures: entry.document.textures.length,
    geometrySha256: geometryFingerprint(entry.bytes),
    memory: entry.memory,
  };
}

function normalizationOf(document) {
  return document.nodes?.find((node) => node.extras?.nilbotsProviderNormalization)
    ?.extras?.nilbotsProviderNormalization;
}

function optimize(input, output) {
  run('npx', [
    '--yes',
    `@gltf-transform/cli@${toolVersion}`,
    'optimize',
    input,
    output,
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
    String(profile.simplifyRatio),
    '--simplify-error',
    String(profile.simplifyError),
    '--simplify-lock-border',
    'false',
    '--weld',
    'true',
  ]);
}

function source(file, bytes, sha256Value) {
  return { file, absolute: path.join(repository, file), bytes, sha256: sha256Value };
}

function verifySource(entry) {
  if (!existsSync(entry.absolute)) throw new Error(`Missing ignored source ${entry.file}.`);
  const bytes = readFileSync(entry.absolute);
  if (bytes.length !== entry.bytes || sha256(bytes) !== entry.sha256)
    throw new Error(`${entry.file}: source bytes or SHA-256 drifted.`);
}

function portableSource(entry) {
  return { file: entry.file, bytes: entry.bytes, sha256: entry.sha256 };
}

function script(name) {
  return path.join(repository, 'scripts/class-models', name);
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
