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
const mutedBlueV3 = process.argv.includes('--muted-blue-v3');
const toolVersion = '4.4.2';
const generationTaskId = '019fff75-43b1-72d8-a2c9-f9a8b8fa32f2';
const retextureTaskId = mutedBlueV3
  ? '01a0003c-d477-7f08-88da-88e79bbbf41a'
  : '019fff7c-caa8-7434-a05b-4625d3d485c1';
const generationSource = source(
  'art/class-models/provider-runs/meshy/arc-relay-longshot-railback-v1/' +
    `longshot/${generationTaskId}/raw-model.glb`,
  64_759_580,
  'a91278f398e1fc9e2c903bb52c3f0cc60bb2a9e372a1c2f7d79e902dee851329',
);
const retextureDonor = source(
  mutedBlueV3
    ? 'art/class-models/provider-runs/meshy/arc-relay-longshot-railback-palette-retexture-v3/' +
        `longshot/${retextureTaskId}/raw-model.glb`
    : 'art/class-models/provider-runs/meshy/arc-relay-longshot-railback-v1-corrective-retexture/' +
        `longshot/${retextureTaskId}/raw-model.glb`,
  mutedBlueV3 ? 81_065_456 : 65_500_712,
  mutedBlueV3
    ? 'ab7ce8cc358ebc9e1d476bc2dfca2e3d65fd5516fe2b7ec6ca443d4e342ba14f'
    : 'c47a8d65bf6a58b93fd72c86bf6dcf9bdca2eb1fa0b165ee4b86c62b74b7aaad',
);
const tierId = mutedBlueV3
  ? 'meshy7-longshot-railback-muted-blue-review-v3'
  : 'meshy7-longshot-railback-review-v1';
const tierDirectory = path.join(repository, 'art/class-models/runtime-tiers/arc-relay', tierId);
const auditPath = path.join(tierDirectory, 'audit.json');
const targetPlanformSpan = 0.99;
const targetTransverseSpan = 0.7292042763168105;
const heightScale = 1.12;
const correctedHeight = 0.27376423804696887;
const facingYawDegrees = 180;
const matteRoughnessFloor = 0.72;
const runtimeLookScale = 1.1;
const profiles = mutedBlueV3
  ? [
      {
        id: 'lean',
        file: 'arc-longshot-railback-muted-blue-lean.glb',
        simplifyRatio: 0.04,
        simplifyError: 0.0025,
        textureSize: 512,
        budgets: { transferBytes: 2_200_000, triangles: 100_000 },
      },
    ]
  : [
      {
        id: 'lean',
        file: 'arc-longshot-railback-lean.glb',
        simplifyRatio: 0.04,
        simplifyError: 0.0025,
        textureSize: 512,
        budgets: { transferBytes: 2_200_000, triangles: 100_000 },
      },
      {
        id: 'rich',
        file: 'arc-longshot-railback-rich.glb',
        simplifyRatio: 0.08,
        simplifyError: 0.001,
        textureSize: 1024,
        budgets: { transferBytes: 5_000_000, triangles: 180_000 },
      },
    ];

for (const entry of [generationSource, retextureDonor]) verifySource(entry);
mkdirSync(tierDirectory, { recursive: true });
if (!check) buildProfiles();

const outputs = profiles.map((profile) => inspectProfile(profile));
const audit = {
  schemaVersion: 1,
  id: tierId,
  status: 'owner-review',
  generator: 'scripts/class-models/build-railback-review-tiers.mjs',
  source: {
    geometry: portableSource(generationSource, generationTaskId, 'multi-image-to-3d'),
    textureDonor: portableSource(retextureDonor, retextureTaskId, 'retexture'),
    provider: 'Meshy',
    providerModel: 'meshy-7',
    credits: mutedBlueV3
      ? {
          generation: 30,
          earlierCorrectiveRetexture: 10,
          paletteRetexture: 10,
          artifactPipelineTotal: 40,
          totalProjectSpend: 50,
          balanceAfter: 1010,
        }
      : { generation: 30, correctiveRetexture: 10, total: 40, balanceAfter: 1020 },
    autoRigging: false,
    ...(mutedBlueV3
      ? {
          palettePlan:
            'art/class-models/provider-runs/meshy/arc-relay-longshot-railback-palette-retexture-v3/plan.json',
          paletteCandidateId: 'railback-muted-blue-v3',
        }
      : {}),
  },
  toolchain: {
    gltfTransform: toolVersion,
    simplifier: 'meshoptimizer through glTF-Transform',
    textureEncoding: 'WebP review tiers',
    textureMaterialization: 'original-UV retexture maps transplanted onto exact generation geometry',
  },
  normalization: {
    orientation: 'identity',
    sourceFacing: '-x',
    facing: '+x',
    facingYawDegrees,
    up: '+y',
    floorY: 0,
    targetPlanformSpan,
    targetTransverseSpan,
    heightScale,
    correctedHeight,
    runtimeLookScale,
    liveMaximumSpan: targetPlanformSpan * runtimeLookScale,
    liveTransverseSpan: targetTransverseSpan * runtimeLookScale,
    liveHeight: correctedHeight * runtimeLookScale,
  },
  fidelityGates: {
    canonicalTop:
      'art/class-look-concepts/arc-relay/longshot-redesign-v2/railback-meshy7-v1/canonical-top-east.png',
    fidelityBrief:
      'art/class-look-concepts/arc-relay/longshot-redesign-v2/FIDELITY.md',
    planformAspectRatio: 1.3576442598505607,
    maximumAspectRatio: 1.45,
    minimumTransverseSpan: 0.68,
    minimumHeight: 0.26,
    exactCapacitorRibs: 3,
    connectedLeglessBody: true,
    fullWidthFrontShutter: true,
    splitCoolingRear: true,
  },
  contract: {
    purpose: mutedBlueV3
      ? 'Reversible muted-blue in-renderer comparison only; does not replace the approved runtime Longshot.'
      : 'Reversible in-renderer comparison only; does not replace the approved runtime Longshot.',
    geometry:
      'Exact first-pass provider geometry and UVs; deterministic normalization, 180-degree facing correction, 1.12 vertical scale, and lossy review simplification.',
    materials: mutedBlueV3
      ? 'Meshy 7 text-prompt muted dusty-blue PBR maps on one fused authored material; team ownership remains renderer-owned.'
      : 'Corrective multiview retexture maps on one fused authored material; team ownership remains renderer-owned.',
    matteFinish:
      `Metallic-roughness green channel is deterministically floored at ${matteRoughnessFloor}; base color, metallic, and normal are unchanged.`,
    tangentSpace:
      'Normal-mapped without authored tangents; Three.js derivative tangent-space review required.',
  },
  profiles: outputs,
};

if (check) {
  const recorded = JSON.parse(readFileSync(auditPath, 'utf8'));
  if (JSON.stringify(recorded, null, 2) !== JSON.stringify(audit, null, 2))
    throw new Error('Railback review-tier audit drifted from current artifacts.');
  console.log(`Verified ${outputs.length} ${tierId} artifacts.`);
} else {
  writeFileSync(auditPath, `${JSON.stringify(audit, null, 2)}\n`);
  console.log(`Built ${outputs.length} ${tierId} artifacts.`);
}

function buildProfiles() {
  const temporary = mkdtempSync(path.join(tmpdir(), 'nilbots-railback-review-tiers-'));
  try {
    const materialized = path.join(temporary, 'materialized.glb');
    const normalized = path.join(temporary, 'normalized.glb');
    const facing = path.join(temporary, 'facing.glb');
    const matte = path.join(temporary, 'matte.glb');
    run(process.execPath, [
      script('transplant-meshy-retexture.mjs'),
      generationSource.absolute,
      retextureDonor.absolute,
      materialized,
    ]);
    const sourceGeometryHash = geometryFingerprint(
      readFileSync(generationSource.absolute),
      'Railback generation source',
    );
    const materializedGeometryHash = geometryFingerprint(
      readFileSync(materialized),
      'Railback materialized source',
    );
    if (sourceGeometryHash !== materializedGeometryHash)
      throw new Error('Railback original-UV texture transplant changed source geometry.');
    run(process.execPath, [
      script('normalize-provider-glb.mjs'),
      materialized,
      normalized,
      String(targetPlanformSpan),
      'identity',
      '--strip-authored-tangents',
      '--height-scale',
      String(heightScale),
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
      console.log(`Building Railback ${profile.id} review tier...`);
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
  const { document } = readGlb(bytes, `Railback ${profile.id}`);
  const scene = document.scenes?.[document.scene ?? 0];
  const root = document.nodes?.[scene?.nodes?.[0]];
  const normalization = root?.extras?.nilbotsProviderNormalization;
  const matte = document.asset?.extras?.nilbotsMatteRoughness;
  const triangles = triangleCount(document);
  const primitives = (document.meshes ?? []).flatMap((mesh) => mesh.primitives ?? []);
  const positionVertices = primitives.reduce(
    (sum, primitive) =>
      sum + (document.accessors?.[primitive.attributes?.POSITION]?.count ?? 0),
    0,
  );
  const memory = inspectModelMemory(bytes, `Railback ${profile.id}`);
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
    throw new Error(`${profile.id}: review artifact changed the monolithic Railback contract.`);
  if (
    normalization?.facing !== '+x' ||
    normalization?.up !== '+y' ||
    normalization?.floorY !== 0 ||
    normalization?.orientation !== 'identity' ||
    normalization?.targetPlanformSpan !== targetPlanformSpan ||
    normalization?.facingYawDegrees !== facingYawDegrees ||
    normalization?.heightScale !== heightScale
  )
    throw new Error(`${profile.id}: review artifact normalization drifted.`);
  if (
    matte?.floor !== matteRoughnessFloor ||
    matte?.minimumAfter < matteRoughnessFloor ||
    matte?.baseColorChanged !== false ||
    matte?.metallicChanged !== false ||
    matte?.normalChanged !== false
  )
    throw new Error(`${profile.id}: deterministic matte gate drifted.`);
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
    throw new Error(
      `${profile.id}: ${bytes.length} bytes exceeds ${profile.budgets.transferBytes}-byte budget.`,
    );
  if (triangles > profile.budgets.triangles)
    throw new Error(
      `${profile.id}: ${triangles} triangles exceeds ${profile.budgets.triangles}-triangle budget.`,
    );
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
    positionVertices,
    meshes: document.meshes.length,
    primitives: primitives.length,
    materials: document.materials.length,
    textures: document.textures.length,
    geometrySha256: geometryFingerprint(bytes, `Railback ${profile.id}`),
    memory,
  };
}

function source(file, bytes, sha256Value) {
  return { file, absolute: path.join(repository, file), bytes, sha256: sha256Value };
}

function verifySource(entry) {
  if (!existsSync(entry.absolute)) throw new Error(`Missing provider source ${entry.file}.`);
  const bytes = readFileSync(entry.absolute);
  if (bytes.length !== entry.bytes || sha256(bytes) !== entry.sha256)
    throw new Error(`${entry.file}: source bytes or SHA-256 drifted.`);
}

function portableSource(entry, taskId, endpoint) {
  return { file: entry.file, bytes: entry.bytes, sha256: entry.sha256, taskId, endpoint };
}

function script(name) {
  return path.join(repository, 'scripts/class-models', name);
}

function run(command, arguments_) {
  const result = spawnSync(command, arguments_, { cwd: repository, stdio: 'inherit' });
  if (result.error) throw result.error;
  if (result.status !== 0)
    throw new Error(`${command} failed with exit code ${result.status}.`);
}

function sha256(value) {
  return createHash('sha256').update(value).digest('hex');
}
