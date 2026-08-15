#!/usr/bin/env node

/** Normalize, separate, matte, simplify, and audit all five complete diadem ranks. */

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
const noBuild = process.argv.includes('--no-build');
const toolVersion = '4.4.2';
const inputRoot = path.join(
  repository,
  'art/class-models/provider-runs/meshy/arc-veterancy-modules-v1/inputs/veterancy-split-diadem-whole-v1',
);
const output = path.join(
  repository,
  'art/class-models/runtime-tiers/arc-relay/meshy7-veterancy-split-diadem-whole-v1',
);
const auditPath = path.join(output, 'audit.json');
const targetGapRatio = 0.34;
const facingYawDegrees = 90;
const matteRoughnessFloor = 0.78;
const rankTuning = [
  { rank: 1, targetPlanformSpan: 0.78, heightScale: 1 },
  { rank: 2, targetPlanformSpan: 0.82, heightScale: 1.25 },
  { rank: 3, targetPlanformSpan: 0.86, heightScale: 1.1 },
  { rank: 4, targetPlanformSpan: 0.9, heightScale: 1.05 },
  { rank: 5, targetPlanformSpan: 0.94, heightScale: 1.05 },
];
const profiles = [
  {
    id: 'rich-512',
    textureSize: 512,
    simplifyRatio: 0.025,
    simplifyError: 0.001,
    budgets: { transferBytes: 1_200_000, triangles: 60_000 },
  },
  {
    id: 'lean-256',
    textureSize: 256,
    simplifyRatio: 0.01,
    simplifyError: 0.0025,
    budgets: { transferBytes: 500_000, triangles: 25_000 },
  },
];

const ranks = rankTuning.map(loadRank);
mkdirSync(output, { recursive: true });
if (!check && !noBuild) buildAll();
const outputs = ranks.map((rank) => ({
  rank: rank.rank,
  source: rank.source,
  normalization: inspectNormalization(rank),
  profiles: profiles.map((profile) => inspectProfile(rank, profile)),
}));
const audit = {
  schemaVersion: 1,
  id: 'meshy7-veterancy-split-diadem-whole-v1',
  status: 'runtime-review',
  generator: 'scripts/class-models/build-veterancy-whole-diadem-runtime-tiers.mjs',
  ownerDirection:
    'Five complete Meshy rank models must match the approved motion-safe conceptual art; renderer reconstruction is superseded.',
  source: {
    provider: 'Meshy',
    providerModel: 'meshy-7',
    endpoint: 'multi-image-to-3d',
    credits: { perRank: 30, ranks: 5, total: 150, balanceAfter: 650 },
    autoRigging: false,
    concept:
      'art/reviews/arc-relay-veterancy-modules-meshy7-v1/correction-v5/motion-safe-split-diadem-rank-1-5-clean.png',
    clearanceConcept:
      'art/reviews/arc-relay-veterancy-modules-meshy7-v1/correction-v5/motion-safe-split-diadem-rank-1-5-clearance.png',
  },
  toolchain: {
    gltfTransform: toolVersion,
    simplifier: 'meshoptimizer through glTF-Transform',
    geometryCompression: 'KHR_mesh_quantization',
    textureEncoding: 'WebP',
  },
  contract: {
    orientation: 'provider +x width rotated 90 degrees about world +y into local lateral +z',
    facing: '+x',
    up: '+y',
    floorY: 0,
    targetGapRatio,
    centreClearance:
      'Disconnected provider halves are translated outward without scaling until the empty lateral gap is 34% of final span; Core animation is not modified.',
    matteFinish:
      `Metallic-roughness green channel is deterministically floored at ${matteRoughnessFloor}; base colour, metallic, normal, and emission remain provider-authored.`,
    runtimeSelection:
      'Exactly one complete rank mesh is visible per bot; no repeated feather instances or renderer-built crown geometry.',
  },
  ranks: outputs,
};

if (check) {
  const recorded = JSON.parse(readFileSync(auditPath, 'utf8'));
  if (JSON.stringify(recorded, null, 2) !== JSON.stringify(audit, null, 2))
    throw new Error('Whole-diadem runtime-tier audit drifted from current artifacts.');
  console.log('Verified ten whole-diadem review-tier artifacts.');
} else {
  writeFileSync(auditPath, `${JSON.stringify(audit, null, 2)}\n`);
  console.log('Built ten whole-diadem review-tier artifacts.');
}

function loadRank(tuning) {
  const manifestFile = path.join(inputRoot, `rank-${tuning.rank}`, 'manifest.json');
  const manifest = JSON.parse(readFileSync(manifestFile, 'utf8'));
  const taskId = manifest.providerRun?.taskId;
  if (
    manifest.canonicalIdentity?.rank !== tuning.rank ||
    manifest.providerRun?.status !== 'SUCCEEDED' ||
    manifest.providerRun?.consumedCredits !== 30 ||
    typeof taskId !== 'string'
  )
    throw new Error(`Rank ${tuning.rank} manifest does not contain one successful 30-credit task.`);
  const file =
    `art/class-models/provider-runs/meshy/arc-veterancy-modules-v1/` +
    `veterancy-split-diadem-rank-${tuning.rank}-v1/${taskId}/raw-model.glb`;
  const absolute = path.join(repository, file);
  const bytes = readFileSync(absolute);
  if (sha256(bytes) !== manifest.providerRun.rawModelSha256)
    throw new Error(`Rank ${tuning.rank} provider source SHA-256 drifted.`);
  return {
    ...tuning,
    taskId,
    source: {
      file,
      bytes: bytes.length,
      sha256: sha256(bytes),
      taskId,
      manifest: path.relative(repository, manifestFile),
    },
    absolute,
  };
}

function buildAll() {
  const temporary = mkdtempSync(path.join(tmpdir(), 'nilbots-whole-diadem-tiers-'));
  try {
    for (const rank of ranks) {
      const split = path.join(temporary, `rank-${rank.rank}-split.glb`);
      const normalized = path.join(temporary, `rank-${rank.rank}-normalized.glb`);
      const facing = path.join(temporary, `rank-${rank.rank}-facing.glb`);
      const matte = path.join(temporary, `rank-${rank.rank}-matte.glb`);
      run(process.execPath, [
        script('widen-provider-split-gap.mjs'),
        rank.absolute,
        split,
        String(targetGapRatio),
      ]);
      run(process.execPath, [
        script('normalize-provider-glb.mjs'),
        split,
        normalized,
        String(rank.targetPlanformSpan),
        'identity',
        '--strip-authored-tangents',
        '--height-scale',
        String(rank.heightScale),
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
        console.log(`Building whole diadem rank ${rank.rank} ${profile.id}...`);
        run('npx', [
          '--yes',
          `@gltf-transform/cli@${toolVersion}`,
          'optimize',
          matte,
          artifactPath(rank, profile),
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
    }
  } finally {
    rmSync(temporary, { recursive: true, force: true });
  }
}

function inspectNormalization(rank) {
  const lean = readFileSync(artifactPath(rank, profiles[1]));
  const { document } = readGlb(lean, `rank ${rank.rank} lean`);
  const scene = document.scenes?.[document.scene ?? 0];
  const root = document.nodes?.[scene?.nodes?.[0]];
  const normalization = root?.extras?.nilbotsProviderNormalization;
  const split = document.asset?.extras?.nilbotsSplitClearance;
  if (
    root?.name !== 'chassis' ||
    normalization?.orientation !== 'identity' ||
    normalization?.targetPlanformSpan !== rank.targetPlanformSpan ||
    (normalization?.heightScale ?? 1) !== rank.heightScale ||
    normalization?.facingYawDegrees !== facingYawDegrees ||
    split?.targetGapRatio !== targetGapRatio ||
    split.achievedGapRatio < targetGapRatio ||
    split.achievedGapRatio > targetGapRatio + 0.005
  )
    throw new Error(`Rank ${rank.rank} normalization or split-clearance contract drifted.`);
  const sourceHeight = normalization.sourceBounds.max[1] - normalization.sourceBounds.min[1];
  const scale = root.scale?.[0];
  return {
    targetPlanformSpan: rank.targetPlanformSpan,
    targetCentreGap: rank.targetPlanformSpan * split.achievedGapRatio,
    heightScale: rank.heightScale,
    normalizedHeight: sourceHeight * scale * rank.heightScale,
    facingYawDegrees,
    splitClearance: split,
  };
}

function inspectProfile(rank, profile) {
  const file = artifactPath(rank, profile);
  if (!existsSync(file)) throw new Error(`Missing rank ${rank.rank} ${profile.id} artifact.`);
  const bytes = readFileSync(file);
  const { document } = readGlb(bytes, `rank ${rank.rank} ${profile.id}`);
  const primitives = (document.meshes ?? []).flatMap((mesh) => mesh.primitives ?? []);
  const triangles = triangleCount(document);
  const memory = inspectModelMemory(bytes, `rank ${rank.rank} ${profile.id}`);
  const matte = document.asset?.extras?.nilbotsMatteRoughness;
  if (
    document.cameras ||
    document.skins ||
    document.animations ||
    document.meshes?.length !== 1 ||
    primitives.length !== 1 ||
    document.materials?.length !== 1 ||
    matte?.floor !== matteRoughnessFloor ||
    matte?.minimumAfter < matteRoughnessFloor ||
    matte?.baseColorChanged !== false ||
    matte?.metallicChanged !== false ||
    matte?.normalChanged !== false
  )
    throw new Error(`Rank ${rank.rank} ${profile.id} changed the static one-material contract.`);
  if (
    !(document.extensionsRequired ?? []).includes('KHR_mesh_quantization') ||
    !(document.extensionsRequired ?? []).includes('EXT_texture_webp') ||
    (document.images ?? []).some((image) => image.mimeType !== 'image/webp') ||
    memory.textures.some(
      (texture) => texture.width !== profile.textureSize || texture.height !== profile.textureSize,
    )
  )
    throw new Error(`Rank ${rank.rank} ${profile.id} texture/quantization contract drifted.`);
  if (bytes.length > profile.budgets.transferBytes || triangles > profile.budgets.triangles)
    throw new Error(
      `Rank ${rank.rank} ${profile.id} exceeds budget: ${bytes.length} bytes, ${triangles} triangles.`,
    );
  return {
    id: profile.id,
    settings: {
      textureSize: profile.textureSize,
      simplifyRatio: profile.simplifyRatio,
      simplifyError: profile.simplifyError,
      simplifyLockBorder: false,
      matteRoughnessFloor,
    },
    budgets: profile.budgets,
    artifact: {
      file: path.relative(repository, file),
      bytes: bytes.length,
      sha256: sha256(bytes),
    },
    triangles,
    geometrySha256: geometryFingerprint(bytes, `rank ${rank.rank} ${profile.id}`),
    meshes: document.meshes.length,
    primitives: primitives.length,
    materials: document.materials.length,
    textures: document.textures?.length ?? 0,
    memory,
  };
}

function artifactPath(rank, profile) {
  return path.join(output, `rank-${rank.rank}-${profile.id}.glb`);
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
