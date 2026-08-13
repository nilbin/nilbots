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
const outputDirectory = path.join(
  repository,
  'art/class-models/runtime-tiers/arc-relay/longshot-objectives-review-v1',
);
const auditPath = path.join(outputDirectory, 'audit.json');

const sources = {
  longshotGeometry: source(
    'art/class-models/provider-runs/meshy/arc-relay-longshot-objectives-v1/' +
      'longshot/019ffbaf-4f01-7375-b78c-761f30f55aae/raw-model.glb',
    66_098_296,
    'd14c8e5750fd15598ecea3ed838594aaffc0febedd3996f5942800723d6166f2',
  ),
  longshotTextureDonor: source(
    'art/class-models/provider-runs/meshy/arc-relay-longshot-objectives-v1-corrective-retextures/' +
      'longshot/019ffbca-f9c4-757e-9588-c718abfe1538/raw-model.glb',
    65_841_424,
    '8c31b10fc469c9f3547312f0192357c56785d83e931c82d4e5a8806cce6de174',
  ),
  flareGeometry: source(
    'art/class-models/provider-runs/meshy/arc-relay-longshot-objectives-v1/' +
      'survey-flare/019ffbb2-f103-72db-9a57-b4bf849a5f4f/raw-model.glb',
    62_086_004,
    'd58d5a7b2b7e9c628b2b6f74a070e8b52a8e7c6919f4a1abe11cacbfbcaf19b4',
  ),
  flareTextureDonor: source(
    'art/class-models/provider-runs/meshy/arc-relay-longshot-objectives-v1-corrective-retextures/' +
      'survey-flare/019ffbcd-1911-75e8-a822-90a439c88415/raw-model.glb',
    62_555_540,
    '92a4e968a0212e5361aac12527aff2bb5ec4bfeb35acd8840f360c05aaccfda8',
  ),
  reactor: source(
    'art/class-models/provider-runs/meshy/arc-relay-longshot-objectives-v1-corrective-geometry/' +
      'team-reactor-v2/019ffbd5-9d7f-72d9-a6d0-6d79c058801c/raw-model.glb',
    64_400_716,
    '06375e52e8756397582bbfd0979f3fa836d987b063fe32660ba767f577cbdb4b',
  ),
  well: source(
    'art/class-models/provider-runs/meshy/arc-relay-longshot-objectives-v1-corrective-geometry/' +
      'core-well-v2/019ffbd2-0fcf-7716-bba6-8f920b78fcc9/raw-model.glb',
    63_281_464,
    '9b52811a5e55a79bd2790a565b40ea40e463f6dcb81b3ebbef532e5009696766',
  ),
};

const profiles = {
  longshot: profile(0.04, 0.0025, 512, 2_000_000, 100_000, 0.72),
  'survey-flare': profile(0.008, 0.004, 256, 500_000, 20_000, 0.72),
  'team-reactor': profile(0.04, 0.0025, 512, 2_000_000, 100_000, 0.72),
  'core-well': profile(0.04, 0.0025, 512, 2_000_000, 100_000, 0.82),
};
const outputs = {
  longshot: path.join(outputDirectory, 'arc-longshot-lean.glb'),
  'survey-flare': path.join(outputDirectory, 'arc-survey-flare-lean.glb'),
  'team-reactor': path.join(outputDirectory, 'arc-team-reactor-lean.glb'),
  'core-well': path.join(outputDirectory, 'arc-core-well-lean.glb'),
};

for (const entry of Object.values(sources)) verifySource(entry);
mkdirSync(outputDirectory, { recursive: true });
if (!check) build();

const candidates = [
  inspectCandidate({
    id: 'longshot',
    kind: 'bot',
    taskId: '019ffbca-f9c4-757e-9588-c718abfe1538',
    sourceFacing: '-x',
    yawDegrees: 180,
    targetPlanformSpan: 0.99,
    expectedTextures: 3,
    source: {
      geometry: portableSource(sources.longshotGeometry),
      textureDonor: portableSource(sources.longshotTextureDonor),
    },
  }),
  inspectCandidate({
    id: 'survey-flare',
    kind: 'signature',
    taskId: '019ffbcd-1911-75e8-a822-90a439c88415',
    sourceFacing: '+z',
    yawDegrees: 90,
    targetPlanformSpan: 0.3,
    expectedTextures: 3,
    source: {
      geometry: portableSource(sources.flareGeometry),
      textureDonor: portableSource(sources.flareTextureDonor),
    },
  }),
  inspectCandidate({
    id: 'team-reactor',
    kind: 'objective',
    taskId: '019ffbd5-9d7f-72d9-a6d0-6d79c058801c',
    sourceFacing: '+z',
    yawDegrees: 90,
    targetPlanformSpan: 1.5,
    expectedTextures: 4,
    source: portableSource(sources.reactor),
  }),
  inspectCandidate({
    id: 'core-well',
    kind: 'objective',
    taskId: '019ffbd2-0fcf-7716-bba6-8f920b78fcc9',
    sourceFacing: '+z',
    yawDegrees: 90,
    targetPlanformSpan: 1,
    heightScale: 0.8,
    expectedTextures: 4,
    source: portableSource(sources.well),
  }),
];

const audit = {
  schemaVersion: 1,
  id: 'longshot-objectives-review-v1',
  status: 'owner-review',
  generator: 'scripts/class-models/build-longshot-objectives-review-tier.mjs',
  toolchain: {
    gltfTransform: toolVersion,
    simplifier: 'meshoptimizer through glTF-Transform',
    textureEncoding: 'WebP review tier',
  },
  contract: {
    purpose: 'Reversible in-renderer review only; no candidate is a runtime promotion.',
    up: '+y',
    facing: '+x where directional',
    floorY: 0,
    materials: 'Neutral authored paint only; team teal/cyan and orange remain renderer-owned.',
    retexture:
      'Longshot and Survey Flare use their first-pass geometry with corrected Meshy original-UV PBR maps transplanted from the approved multiview retextures.',
    matteFinish:
      'Metallic-roughness green channel is deterministically floored at 0.72 for bot/reactor assets and 0.82 for the Core Well environment housing, without changing base color, metallic, or normal maps.',
    wellProfile:
      'Owner-directed deterministic 0.80 vertical scale preserves the one-tile planform and floor while reducing the Well housing from about 0.237 to 0.190 tile high.',
    stateOwnership:
      'Survey radius/timing, Well spawn/rearm/source/Core indications, and Reactor ownership/charge/integrity remain renderer-owned overlays.',
  },
  providerSpend: {
    initialBatchCredits: 120,
    correctiveBatchCredits: 80,
    balanceAfter: 1100,
    autoRigging: false,
  },
  acceptedDeviations: [
    {
      id: 'core-well',
      taskId: '019ffbd2-0fcf-7716-bba6-8f920b78fcc9',
      ownerDecision:
        'Four-anchor silhouette accepted as overall fine on 2026-08-13; owner requested a flatter, less shiny local derivative before gameplay review.',
      evidence:
        'art/reviews/arc-relay-longshot-objectives-meshy7-v1/corrective-provider-review/core-well-six-view.png',
      providerRetryUsed: false,
      additionalCredits: 0,
    },
  ],
  totals: {
    transferBytes: candidates.reduce((sum, entry) => sum + entry.artifact.bytes, 0),
    triangles: candidates.reduce((sum, entry) => sum + entry.triangles, 0),
    modelGpuBytesRgba8Fallback: candidates.reduce(
      (sum, entry) => sum + entry.memory.modelGpuBytesRgba8Fallback,
      0,
    ),
  },
  candidates,
};

if (check) {
  const recorded = JSON.parse(readFileSync(auditPath, 'utf8'));
  if (JSON.stringify(recorded, null, 2) !== JSON.stringify(audit, null, 2))
    throw new Error('Longshot/objectives review audit drifted from current artifacts.');
  console.log('Verified Longshot, Survey Flare, Team Reactor, and Core Well review artifacts.');
} else {
  writeFileSync(auditPath, `${JSON.stringify(audit, null, 2)}\n`);
  console.log('Built Longshot, Survey Flare, Team Reactor, and Core Well review artifacts.');
}

function build() {
  const temporary = mkdtempSync(path.join(tmpdir(), 'nilbots-longshot-objectives-tier-'));
  try {
    buildRetextured(
      sources.longshotGeometry.absolute,
      sources.longshotTextureDonor.absolute,
      outputs.longshot,
      0.99,
      180,
      profiles.longshot,
    );
    buildRetextured(
      sources.flareGeometry.absolute,
      sources.flareTextureDonor.absolute,
      outputs['survey-flare'],
      0.3,
      90,
      profiles['survey-flare'],
    );

    const reactorNormalized = path.join(temporary, 'reactor-normalized.glb');
    const reactorFacing = path.join(temporary, 'reactor-facing.glb');
    const reactorMatte = path.join(temporary, 'reactor-matte.glb');
    normalize(sources.reactor.absolute, reactorNormalized, 1.5);
    correctFacing(reactorNormalized, reactorFacing, 90);
    enforceMatte(reactorFacing, reactorMatte);
    optimize(reactorMatte, outputs['team-reactor'], profiles['team-reactor']);

    const wellNormalized = path.join(temporary, 'well-normalized.glb');
    const wellFacing = path.join(temporary, 'well-facing.glb');
    const wellMatte = path.join(temporary, 'well-matte.glb');
    normalize(sources.well.absolute, wellNormalized, 1, 0.8);
    correctFacing(wellNormalized, wellFacing, 90);
    enforceMatte(wellFacing, wellMatte, profiles['core-well'].matteRoughnessFloor);
    optimize(wellMatte, outputs['core-well'], profiles['core-well']);

    function buildRetextured(geometry, donor, output, span, yaw, selectedProfile) {
      const tag = path.basename(output, '.glb');
      const materialized = path.join(temporary, `${tag}-materialized.glb`);
      const normalized = path.join(temporary, `${tag}-normalized.glb`);
      const facing = path.join(temporary, `${tag}-facing.glb`);
      const matte = path.join(temporary, `${tag}-matte.glb`);
      run(process.execPath, [script('transplant-meshy-retexture.mjs'), geometry, donor, materialized]);
      normalize(materialized, normalized, span);
      correctFacing(normalized, facing, yaw);
      enforceMatte(facing, matte, selectedProfile.matteRoughnessFloor);
      optimize(matte, output, selectedProfile);
    }
  } finally {
    rmSync(temporary, { recursive: true, force: true });
  }
}

function normalize(input, output, span, heightScale = 1) {
  const arguments_ = [
    script('normalize-provider-glb.mjs'),
    input,
    output,
    String(span),
    'identity',
    '--strip-authored-tangents',
  ];
  if (heightScale !== 1) arguments_.push('--height-scale', String(heightScale));
  run(process.execPath, arguments_);
}

function correctFacing(input, output, yaw) {
  run(process.execPath, [script('correct-provider-facing.mjs'), input, output, String(yaw)]);
}

function enforceMatte(input, output, floor = 0.72) {
  run(process.execPath, [script('enforce-matte-roughness.mjs'), input, output, String(floor)]);
}

function optimize(input, output, selectedProfile) {
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
    String(selectedProfile.textureSize),
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
    String(selectedProfile.simplifyRatio),
    '--simplify-error',
    String(selectedProfile.simplifyError),
    '--simplify-lock-border',
    'false',
    '--weld',
    'true',
  ]);
}

function inspectCandidate({
  id,
  kind,
  taskId,
  sourceFacing,
  yawDegrees,
  targetPlanformSpan,
  heightScale = 1,
  expectedTextures,
  source: providerSource,
}) {
  const absolute = outputs[id];
  if (!existsSync(absolute)) throw new Error(`${id}: review artifact is missing.`);
  const bytes = readFileSync(absolute);
  const { document } = readGlb(bytes, id);
  const selectedProfile = profiles[id];
  const primitives = (document.meshes ?? []).flatMap((mesh) => mesh.primitives ?? []);
  const normalization = document.nodes?.find(
    (node) => node.extras?.nilbotsProviderNormalization,
  )?.extras?.nilbotsProviderNormalization;
  const matte = document.asset?.extras?.nilbotsMatteRoughness;
  const triangles = triangleCount(document);
  const memory = inspectModelMemory(bytes, id);
  if (
    document.cameras ||
    document.skins ||
    document.animations ||
    document.meshes?.length !== 1 ||
    primitives.length !== 1 ||
    document.materials?.length !== 1 ||
    document.textures?.length !== expectedTextures ||
    document.images?.length !== expectedTextures ||
    normalization?.facing !== '+x' ||
    normalization?.up !== '+y' ||
    normalization?.floorY !== 0 ||
    normalization?.orientation !== 'identity' ||
    normalization?.targetPlanformSpan !== targetPlanformSpan ||
    normalization?.facingYawDegrees !== yawDegrees ||
    (normalization?.heightScale ?? 1) !== heightScale ||
    matte?.floor !== selectedProfile.matteRoughnessFloor ||
    matte?.minimumAfter < selectedProfile.matteRoughnessFloor ||
    matte?.baseColorChanged !== false ||
    matte?.metallicChanged !== false ||
    matte?.normalChanged !== false ||
    !(document.extensionsRequired ?? []).includes('KHR_mesh_quantization') ||
    !(document.extensionsRequired ?? []).includes('EXT_texture_webp') ||
    (document.images ?? []).some((image) => image.mimeType !== 'image/webp') ||
    memory.textures.some(
      (texture) =>
        texture.width !== selectedProfile.textureSize ||
        texture.height !== selectedProfile.textureSize,
    ) ||
    bytes.length > selectedProfile.budgets.transferBytes ||
    triangles > selectedProfile.budgets.triangles
  )
    throw new Error(`${id}: review artifact contract or budget drifted.`);
  return {
    id,
    kind,
    taskId,
    endpoint: kind === 'objective' ? 'multi-image-to-3d' : 'retexture',
    source: providerSource,
    facing: {
      sourceFacing,
      targetFacing: '+x',
      yawDegrees,
    },
    normalization: { targetPlanformSpan, heightScale, up: '+y', floorY: 0 },
    profile: selectedProfile,
    matteRoughness: matte,
    artifact: {
      file: path.relative(repository, absolute),
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
    geometrySha256: geometryFingerprint(bytes, id),
    memory,
  };
}

function profile(
  simplifyRatio,
  simplifyError,
  textureSize,
  transferBytes,
  triangles,
  matteRoughnessFloor,
) {
  return {
    simplifyRatio,
    simplifyError,
    textureSize,
    geometryCompression: 'quantize',
    textureEncoding: 'WebP',
    matteRoughnessFloor,
    budgets: { transferBytes, triangles },
  };
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
  const result = spawnSync(command, arguments_, { cwd: repository, stdio: 'inherit' });
  if (result.error) throw result.error;
  if (result.status !== 0)
    throw new Error(`${command} failed with exit code ${result.status}.`);
}

function sha256(value) {
  return createHash('sha256').update(value).digest('hex');
}
