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
const geometryTaskId = '019ffcc4-f21b-79f7-b6f4-2ecb9eed441a';
const retextureTaskId = '019ffccf-7594-7de3-a30f-603697cf63a2';
const geometrySource = source(
  'art/class-models/provider-runs/meshy/arc-relay-team-reactor-recess-v1/' +
    `team-reactor-recess-v1/${geometryTaskId}/raw-model.glb`,
  63_702_692,
  '877b917bd4799d37da2dce16e95b55b129f1cf9dcff779071493dde93c9bb6df',
);
const textureDonor = source(
  'art/class-models/provider-runs/meshy/arc-relay-team-reactor-recess-v1-retexture/' +
    `team-reactor-recess-v1/${retextureTaskId}/raw-model.glb`,
  64_218_900,
  '4f44faa5d43348218d1601191a5cd389d6b5e2d71be17498fb10251850d2a6dd',
);
const tierId = 'reactor-recess-review-v1';
const tierDirectory = path.join(
  repository,
  'art/class-models/runtime-tiers/arc-relay',
  tierId,
);
const auditPath = path.join(tierDirectory, 'audit.json');
const targetPlanformSpan = 1.5;
const facingYawDegrees = 90;
const matteRoughnessFloor = 0.82;
const profiles = [
  {
    id: 'lean',
    file: 'arc-team-reactor-recess-lean.glb',
    simplifyRatio: 0.04,
    simplifyError: 0.0025,
    textureSize: 512,
    budgets: { transferBytes: 2_000_000, triangles: 100_000 },
  },
  {
    id: 'rich',
    file: 'arc-team-reactor-recess-rich.glb',
    simplifyRatio: 0.08,
    simplifyError: 0.001,
    textureSize: 1024,
    budgets: { transferBytes: 8_000_000, triangles: 180_000 },
  },
];

verifySource(geometrySource);
verifySource(textureDonor);
mkdirSync(tierDirectory, { recursive: true });
if (!check) buildProfiles();

const geometryBytes = readFileSync(geometrySource.absolute);
const donorBytes = readFileSync(textureDonor.absolute);
const geometryDocument = readGlb(geometryBytes, 'Reactor geometry source').document;
const donorDocument = readGlb(donorBytes, 'Reactor retexture donor').document;
const outputs = profiles.map(inspectProfile);
const audit = {
  schemaVersion: 1,
  id: tierId,
  status: 'owner-review',
  generator: 'scripts/class-models/build-reactor-recess-review-tiers.mjs',
  sources: {
    geometry: portableSource(geometrySource, geometryTaskId, 'multi-image-to-3d'),
    textureDonor: portableSource(textureDonor, retextureTaskId, 'retexture'),
  },
  providerGeometryRewrite: providerGeometryRewrite(geometryDocument, donorDocument),
  providerSpend: {
    multiImageCredits: 30,
    retextureCredits: 10,
    totalCredits: 40,
    balanceAfter: 1060,
    autoRigging: false,
  },
  toolchain: {
    gltfTransform: toolVersion,
    simplifier: 'meshoptimizer through glTF-Transform',
    textureEncoding: 'WebP review tier; shipping compression remains a later gate',
  },
  normalization: {
    orientation: 'identity',
    sourceFacing: '+z',
    facing: '+x',
    up: '+y',
    floorY: 0,
    facingYawDegrees,
    targetPlanformSpan,
  },
  contract: {
    purpose: 'Reversible owner review only; no live Reactor replacement.',
    geometry:
      'Exact first-pass Meshy geometry and UVs are retained; only the corrected retexture PBR maps are transplanted before lossy review-tier simplification.',
    material:
      'Neutral aged ivory, graphite, and muted oxblood only; no emission or baked team color.',
    matte:
      `Metallic-roughness green is deterministically floored at ${matteRoughnessFloor}.`,
    stateOwnership:
      'Team color, charge, integrity, crest, banking pulse, damage, and destruction remain renderer-owned.',
  },
  profiles: outputs,
};

if (check) {
  const recorded = JSON.parse(readFileSync(auditPath, 'utf8'));
  if (JSON.stringify(recorded, null, 2) !== JSON.stringify(audit, null, 2))
    throw new Error('Reactor-recess review-tier audit drifted from the current artifacts.');
  console.log(`Verified ${outputs.length} ${tierId} artifacts.`);
} else {
  writeFileSync(auditPath, `${JSON.stringify(audit, null, 2)}\n`);
  console.log(`Built ${outputs.length} ${tierId} artifacts.`);
}

function buildProfiles() {
  const temporary = mkdtempSync(path.join(tmpdir(), 'nilbots-reactor-recess-tiers-'));
  try {
    const materialized = path.join(temporary, 'materialized.glb');
    const normalized = path.join(temporary, 'normalized.glb');
    const facing = path.join(temporary, 'facing.glb');
    const matte = path.join(temporary, 'matte.glb');
    run(process.execPath, [
      script('transplant-meshy-retexture.mjs'),
      geometrySource.absolute,
      textureDonor.absolute,
      materialized,
    ]);
    if (
      geometryFingerprint(readFileSync(materialized), 'materialized Reactor') !==
      geometryFingerprint(readFileSync(geometrySource.absolute), 'source Reactor')
    )
      throw new Error('Retexture transplant changed the approved Reactor geometry.');
    run(process.execPath, [
      script('normalize-provider-glb.mjs'),
      materialized,
      normalized,
      String(targetPlanformSpan),
      'identity',
      '--strip-authored-tangents',
    ]);
    run(process.execPath, [
      script('correct-provider-facing.mjs'),
      normalized,
      facing,
      String(facingYawDegrees),
    ]);
    run(process.execPath, [
      script('enforce-matte-roughness.mjs'),
      facing,
      matte,
      String(matteRoughnessFloor),
    ]);
    for (const profile of profiles) {
      console.log(`Building Reactor recess ${profile.id} review tier...`);
      run('npx', [
        '--yes',
        `@gltf-transform/cli@${toolVersion}`,
        'optimize',
        matte,
        path.join(tierDirectory, profile.file),
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
  } finally {
    rmSync(temporary, { recursive: true, force: true });
  }
}

function inspectProfile(profile) {
  const outputPath = path.join(tierDirectory, profile.file);
  if (!existsSync(outputPath)) throw new Error(`${profile.id}: output GLB is missing.`);
  const bytes = readFileSync(outputPath);
  const { document } = readGlb(bytes, `Reactor recess ${profile.id}`);
  const scene = document.scenes?.[document.scene ?? 0];
  const root = document.nodes?.[scene?.nodes?.[0]];
  const normalization = root?.extras?.nilbotsProviderNormalization;
  const matte = document.asset?.extras?.nilbotsMatteRoughness;
  const primitives = (document.meshes ?? []).flatMap((mesh) => mesh.primitives ?? []);
  const triangles = triangleCount(document);
  const memory = inspectModelMemory(bytes, `Reactor recess ${profile.id}`);
  if (
    document.cameras ||
    document.skins ||
    document.animations ||
    document.meshes?.length !== 1 ||
    primitives.length !== 1 ||
    document.materials?.length !== 1 ||
    document.textures?.length !== 3 ||
    document.images?.length !== 3
  )
    throw new Error(`${profile.id}: review artifact changed the monolithic Reactor contract.`);
  if (
    normalization?.facing !== '+x' ||
    normalization?.up !== '+y' ||
    normalization?.floorY !== 0 ||
    normalization?.orientation !== 'identity' ||
    normalization?.targetPlanformSpan !== targetPlanformSpan ||
    normalization?.facingYawDegrees !== facingYawDegrees
  )
    throw new Error(`${profile.id}: review artifact normalization drifted.`);
  if (
    matte?.floor !== matteRoughnessFloor ||
    matte?.minimumAfter < matteRoughnessFloor ||
    matte?.baseColorChanged !== false ||
    matte?.metallicChanged !== false ||
    matte?.normalChanged !== false
  )
    throw new Error(`${profile.id}: matte provenance drifted.`);
  if (
    !(document.extensionsRequired ?? []).includes('KHR_mesh_quantization') ||
    !(document.extensionsRequired ?? []).includes('EXT_texture_webp') ||
    (document.images ?? []).some((image) => image.mimeType !== 'image/webp')
  )
    throw new Error(`${profile.id}: review artifact must use quantized geometry and WebP maps.`);
  if (
    memory.textures.some(
      (texture) => texture.width !== profile.textureSize || texture.height !== profile.textureSize,
    )
  )
    throw new Error(`${profile.id}: texture dimensions drifted from ${profile.textureSize}.`);
  if (bytes.length > profile.budgets.transferBytes)
    throw new Error(`${profile.id}: ${bytes.length} bytes exceeds its transfer budget.`);
  if (triangles > profile.budgets.triangles)
    throw new Error(`${profile.id}: ${triangles} triangles exceeds its triangle budget.`);
  return {
    id: profile.id,
    settings: {
      simplifyRatio: profile.simplifyRatio,
      simplifyError: profile.simplifyError,
      lockBorder: false,
      textureSize: profile.textureSize,
      geometryCompression: 'quantize',
      textureEncoding: 'WebP',
      matteRoughnessFloor,
    },
    budgets: profile.budgets,
    artifact: {
      file: path.relative(repository, outputPath),
      bytes: bytes.length,
      sha256: sha256(bytes),
    },
    triangles,
    positionVertices: primitives.reduce(
      (sum, primitive) =>
        sum + (document.accessors?.[primitive.attributes?.POSITION]?.count ?? 0),
      0,
    ),
    meshes: document.meshes.length,
    primitives: primitives.length,
    materials: document.materials.length,
    textures: document.textures.length,
    geometrySha256: geometryFingerprint(bytes, `Reactor recess ${profile.id}`),
    memory,
  };
}

function providerGeometryRewrite(before, after) {
  const beforePrimitive = before.meshes?.[0]?.primitives?.[0];
  const afterPrimitive = after.meshes?.[0]?.primitives?.[0];
  const beforePosition = before.accessors?.[beforePrimitive?.attributes?.POSITION];
  const afterPosition = after.accessors?.[afterPrimitive?.attributes?.POSITION];
  if (!beforePosition?.min || !beforePosition?.max || !afterPosition?.min || !afterPosition?.max)
    throw new Error('Provider rewrite audit requires position accessor bounds.');
  const beforeDimensions = beforePosition.max.map((value, axis) => value - beforePosition.min[axis]);
  const afterDimensions = afterPosition.max.map((value, axis) => value - afterPosition.min[axis]);
  const dimensionScales = afterDimensions.map((value, axis) => value / beforeDimensions[axis]);
  return {
    sourceTriangles: triangleCount(before),
    outputTriangles: triangleCount(after),
    triangleDelta: triangleCount(after) - triangleCount(before),
    sourcePositionVertices: beforePosition.count,
    outputPositionVertices: afterPosition.count,
    dimensionScales,
    dimensionScaleSpread: Math.max(...dimensionScales) - Math.min(...dimensionScales),
    acceptedAsGeometrySource: false,
    resolution: 'Retexture maps transplanted onto the exact first-pass geometry and UVs.',
  };
}

function source(file, bytes, sha256Value) {
  return { file, absolute: path.join(repository, file), bytes, sha256: sha256Value };
}

function verifySource(entry) {
  if (!existsSync(entry.absolute)) throw new Error(`Missing ignored provider source ${entry.file}.`);
  const bytes = readFileSync(entry.absolute);
  if (bytes.length !== entry.bytes || sha256(bytes) !== entry.sha256)
    throw new Error(`${entry.file}: source bytes or SHA-256 drifted.`);
}

function portableSource(entry, taskId, endpoint) {
  return {
    taskId,
    endpoint,
    provider: 'Meshy',
    providerModel: 'meshy-7',
    file: entry.file,
    bytes: entry.bytes,
    sha256: entry.sha256,
  };
}

function script(name) {
  return path.join(repository, 'scripts/class-models', name);
}

function run(command, args) {
  const result = spawnSync(command, args, { cwd: repository, stdio: 'inherit' });
  if (result.error) throw result.error;
  if (result.status !== 0)
    throw new Error(`${command} failed with exit code ${result.status}.`);
}

function sha256(value) {
  return createHash('sha256').update(value).digest('hex');
}
