#!/usr/bin/env node

import { createHash } from 'node:crypto';
import { mkdir, readFile, writeFile } from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const JSON_CHUNK = 0x4e4f534a;
const BIN_CHUNK = 0x004e4942;
const EPSILON = 1e-6;
const scriptDirectory = path.dirname(fileURLToPath(import.meta.url));

const ASSETS = [
  {
    id: 'aegis-tortoise-shell',
    armature: 'Aegis.Shell.Rig',
    bones: [
      'root',
      'hub',
      'guard',
      'support_north',
      'support_west',
      'support_south',
    ],
    teamNodes: [
      'Shell.TeamInlay.GuardNorth',
      'Shell.TeamInlay.GuardSouth',
      'Shell.TeamInlay.WestCentre',
    ],
    clips: {
      AegisShell_Deploy: 1,
      AegisShell_Idle: 2,
    },
    requiredNodes: [
      'Shell.Hub.CoreBronzeBezel',
      'Shell.Hub.CoreInnerSeal',
      'Shell.Hub.ClassEnergy',
      'Shell.Guard.BronzeFrame',
      'Shell.Guard.NavyFace',
      'Shell.Hub.RearBand.North',
      'Shell.Hub.RearBand.South',
    ],
    triangleBudget: 18_000,
    byteBudget: 3_500_000,
  },
  {
    id: 'aegis-tortoise-turret',
    armature: 'Aegis.Turret.Rig',
    bones: [
      'root',
      'hub',
      'arm_east',
      'support_east',
      'emitter_east',
      'arm_north',
      'support_north',
      'emitter_north',
      'arm_west',
      'support_west',
      'emitter_west',
      'arm_south',
      'support_south',
      'emitter_south',
    ],
    teamNodes: [
      'Turret.Arm.East.TeamInlay',
      'Turret.Arm.North.TeamInlay',
      'Turret.Arm.West.TeamInlay',
      'Turret.Arm.South.TeamInlay',
    ],
    clips: {
      AegisTurret_Deploy: 1,
      AegisTurret_Idle: 2,
    },
    requiredNodes: [
      'Turret.Hub.CoreBronzeBezel',
      'Turret.Hub.CoreInnerSeal',
      'Turret.Hub.ClassEnergy',
      'Turret.Arm.East.RootBandNorth',
      'Turret.Arm.East.RootBandSouth',
    ],
    triangleBudget: 15_000,
    byteBudget: 3_500_000,
  },
];

const args = process.argv.slice(2);
const rootFlag = args.indexOf('--root');
const outputFlag = args.indexOf('--output');
const root = path.resolve(
  rootFlag >= 0 ? args[rootFlag + 1] : scriptDirectory,
);
const output = path.resolve(
  outputFlag >= 0
    ? args[outputFlag + 1]
    : path.join(root, 'reports/aegis-forms-structural-validation.json'),
);
const buildReport = JSON.parse(
  await readFile(path.join(root, 'build-report.json'), 'utf8'),
);

const results = [];
for (const specification of ASSETS)
  results.push(await validateAsset(specification, buildReport));

const report = {
  schemaVersion: 1,
  validator: path.basename(fileURLToPath(import.meta.url)),
  verdict: results.every((result) => result.errors.length === 0)
    ? 'pass'
    : 'fail',
  assets: results,
};
await mkdir(path.dirname(output), { recursive: true });
await writeFile(output, `${JSON.stringify(report, null, 2)}\n`);
process.stdout.write(`${JSON.stringify(report, null, 2)}\n`);
if (report.verdict !== 'pass') process.exitCode = 1;

async function validateAsset(specification, aggregateBuildReport) {
  const file = path.join(root, 'candidate', `${specification.id}.glb`);
  const perAssetReport = JSON.parse(
    await readFile(
      path.join(root, 'reports', `${specification.id}-build.json`),
      'utf8',
    ),
  );
  const bytes = await readFile(file);
  const errors = [];
  const warnings = [];
  const check = (condition, message) => {
    if (!condition) errors.push(message);
  };

  check(bytes.length >= 20, 'GLB is shorter than its minimum header');
  check(bytes.toString('ascii', 0, 4) === 'glTF', 'GLB magic is not glTF');
  check(bytes.readUInt32LE(4) === 2, 'GLB version is not 2');
  check(
    bytes.readUInt32LE(8) === bytes.length,
    'GLB declared length does not match file length',
  );

  const chunks = [];
  for (let offset = 12; offset + 8 <= bytes.length; ) {
    const byteLength = bytes.readUInt32LE(offset);
    const type = bytes.readUInt32LE(offset + 4);
    const start = offset + 8;
    const end = start + byteLength;
    check(end <= bytes.length, `GLB chunk at ${offset} exceeds file length`);
    if (end > bytes.length) break;
    chunks.push({ type, byteLength, start, end });
    offset = end;
  }
  check(chunks.length === 2, `expected two GLB chunks, found ${chunks.length}`);
  check(chunks[0]?.type === JSON_CHUNK, 'first GLB chunk is not JSON');
  check(chunks[1]?.type === BIN_CHUNK, 'second GLB chunk is not BIN');

  let gltf = {};
  try {
    const json = chunks.find((chunk) => chunk.type === JSON_CHUNK);
    check(Boolean(json), 'GLB has no JSON chunk');
    if (json)
      gltf = JSON.parse(
        bytes
          .subarray(json.start, json.end)
          .toString('utf8')
          .replace(/[\u0000\u0020]+$/u, ''),
      );
  } catch (error) {
    errors.push(`GLB JSON cannot be parsed: ${error.message}`);
  }

  const nodes = gltf.nodes ?? [];
  const meshes = gltf.meshes ?? [];
  const materials = gltf.materials ?? [];
  const accessors = gltf.accessors ?? [];
  const animations = gltf.animations ?? [];
  const skins = gltf.skins ?? [];
  const byName = new Map();
  for (const [index, node] of nodes.entries()) {
    const indexes = byName.get(node.name) ?? [];
    indexes.push(index);
    byName.set(node.name, indexes);
  }

  check(gltf.asset?.version === '2.0', 'asset version is not glTF 2.0');
  check(
    gltf.asset?.generator ===
      'Nilbots deterministic Aegis forms / Blender 5.2.0',
    `unexpected generator ${String(gltf.asset?.generator)}`,
  );
  check((gltf.cameras ?? []).length === 0, 'runtime GLB contains cameras');
  check(
    !(gltf.extensionsUsed ?? []).includes('KHR_lights_punctual') &&
      !gltf.extensions?.KHR_lights_punctual,
    'runtime GLB contains punctual lights',
  );
  check((gltf.scenes ?? []).length === 1, 'expected exactly one scene');
  const sceneRoots = (gltf.scenes?.[gltf.scene ?? 0]?.nodes ?? []).map(
    (index) => nodes[index]?.name,
  );
  check(
    sceneRoots.length === 1 && sceneRoots[0] === specification.armature,
    `scene root differs from ${specification.armature}`,
  );

  for (const bone of specification.bones)
    check(
      byName.get(bone)?.length === 1,
      `expected exactly one rig node named ${bone}`,
    );
  check(skins.length === 1, `expected one skin, found ${skins.length}`);
  if (skins.length === 1) {
    check(
      skins[0].name === specification.armature,
      `unexpected skin name ${String(skins[0].name)}`,
    );
    const jointNames = (skins[0].joints ?? []).map(
      (index) => nodes[index]?.name,
    );
    check(
      sameSet(jointNames, specification.bones),
      `skin joints differ from contract: ${jointNames.join(', ')}`,
    );
  }
  const rigIndex = byName.get(specification.armature)?.[0];
  const rigExtras = nodes[rigIndex]?.extras ?? {};
  check(rigExtras.nilbotsFacing === '+X', 'rig facing contract is not +X');
  check(
    rigExtras.nilbotsUp === '+Y after glTF export',
    'rig up-axis contract is not +Y after glTF export',
  );
  check(
    rigExtras.nilbotsAnimationContract ===
      'in-place authored actions; a replay renderer must seek normalized clip time',
    'rig deterministic animation contract is missing or changed',
  );

  for (const name of specification.requiredNodes)
    check(byName.get(name)?.length === 1, `missing authored family part ${name}`);

  check(
    animations.length === Object.keys(specification.clips).length,
    `expected ${Object.keys(specification.clips).length} animations, found ${animations.length}`,
  );
  const animationSummary = [];
  for (const [clipName, expectedDuration] of Object.entries(
    specification.clips,
  )) {
    const matching = animations.filter((animation) => animation.name === clipName);
    check(matching.length === 1, `expected exactly one ${clipName} animation`);
    if (matching.length !== 1) continue;
    const animation = matching[0];
    const channelKeys = new Set();
    for (const channel of animation.channels ?? []) {
      const targetName = nodes[channel.target?.node]?.name;
      const targetPath = channel.target?.path;
      check(
        specification.bones.includes(targetName),
        `${clipName} targets a node outside the rig contract`,
      );
      check(
        ['translation', 'rotation', 'scale'].includes(targetPath),
        `${clipName} contains unsupported path ${String(targetPath)}`,
      );
      channelKeys.add(`${targetName}:${targetPath}`);
    }
    check(
      channelKeys.size === specification.bones.length * 3,
      `${clipName} does not animate each bone through translation/rotation/scale`,
    );
    const inputAccessors = new Set(
      (animation.samplers ?? []).map((sampler) => sampler.input),
    );
    const minima = [...inputAccessors].map(
      (index) => accessors[index]?.min?.[0],
    );
    const maxima = [...inputAccessors].map(
      (index) => accessors[index]?.max?.[0],
    );
    const start = Math.min(...minima);
    const end = Math.max(...maxima);
    const duration = end - start;
    check(
      near(duration, expectedDuration),
      `${clipName} duration ${duration} differs from ${expectedDuration}s`,
    );
    check(
      near(start, 1 / 24),
      `${clipName} should retain authored frame-1 timestamp`,
    );
    check(
      (animation.samplers ?? []).every(
        (sampler) =>
          sampler.interpolation === undefined ||
          sampler.interpolation === 'LINEAR',
      ),
      `${clipName} contains non-linear glTF interpolation`,
    );
    animationSummary.push({
      name: clipName,
      seconds: duration,
      channels: animation.channels?.length ?? 0,
      samplers: animation.samplers?.length ?? 0,
    });
  }

  const semanticMaterials = materials.flatMap((material, index) =>
    material.extras?.nilbotsRole === 'team-accent' ? [index] : [],
  );
  check(
    semanticMaterials.length === 1,
    `expected one team-accent material, found ${semanticMaterials.length}`,
  );
  const teamMaterialIndex = semanticMaterials[0];
  const teamMaterial = materials[teamMaterialIndex] ?? {};
  check(
    teamMaterial.name === 'NB_TEAM_ACCENT',
    'semantic team material has unexpected name',
  );
  check(
    teamMaterial.pbrMetallicRoughness?.baseColorTexture === undefined,
    'team material must not carry fixed base-colour pixels',
  );
  check(
    teamMaterial.emissiveTexture === undefined,
    'team material must not carry fixed emission pixels',
  );
  check(
    vectorNear(
      teamMaterial.pbrMetallicRoughness?.baseColorFactor ?? [1, 1, 1, 1],
      [1, 1, 1, 1],
    ),
    'team material neutral base factor is not white',
  );
  check(
    vectorNear(teamMaterial.emissiveFactor ?? [0, 0, 0], [0.72, 0.72, 0.72]),
    'team material neutral emission factor is not the bounded 0.72 value',
  );

  const energyMaterials = materials.filter(
    (material) => material.name === 'NB_CLASS_ENERGY_CYAN',
  );
  check(
    energyMaterials.length === 1,
    `expected one fixed class-energy material, found ${energyMaterials.length}`,
  );
  if (energyMaterials.length === 1) {
    const factor =
      energyMaterials[0].pbrMetallicRoughness?.baseColorFactor ?? [];
    check(
      factor.length === 4 && factor[2] > factor[1] && factor[1] > factor[0] * 3,
      'fixed class-energy base is no longer visibly cyan',
    );
    const emission = energyMaterials[0].emissiveFactor ?? [];
    check(
      emission.length === 3 &&
        emission[2] > emission[1] &&
        emission[1] > emission[0] * 3,
      'fixed class-energy emission is no longer visibly cyan',
    );
    check(
      energyMaterials[0].extras?.nilbotsRole !== 'team-accent',
      'fixed class energy is incorrectly team-tintable',
    );
  }

  const teamNodes = nodes.filter(
    (node) => node.extras?.nilbotsRole === 'team-accent',
  );
  check(
    teamNodes.length === specification.teamNodes.length,
    `expected ${specification.teamNodes.length} semantic team nodes, found ${teamNodes.length}`,
  );
  for (const name of specification.teamNodes) {
    const indexes = byName.get(name) ?? [];
    check(indexes.length === 1, `expected exactly one ${name}`);
    const node = nodes[indexes[0]] ?? {};
    check(
      node.extras?.nilbotsPart === 'semantic-team-inlay',
      `${name} lacks semantic team part metadata`,
    );
    const primitives = meshes[node.mesh]?.primitives ?? [];
    check(primitives.length > 0, `${name} has no mesh primitive`);
    check(
      primitives.every(
        (primitive) => primitive.material === teamMaterialIndex,
      ),
      `${name} does not exclusively use the team material`,
    );
  }

  const texturedNames = new Set([
    'NB_NAVY_ARMOR',
    'NB_GRAPHITE_MECHANICAL',
    'NB_WEATHERED_BRONZE',
  ]);
  for (const name of texturedNames) {
    const matching = materials.filter((material) => material.name === name);
    check(matching.length === 1, `expected one ${name} material`);
    if (matching.length !== 1) continue;
    const material = matching[0];
    check(
      Boolean(material.pbrMetallicRoughness?.baseColorTexture),
      `${name} has no compact albedo texture`,
    );
    check(
      Boolean(material.pbrMetallicRoughness?.metallicRoughnessTexture),
      `${name} has no compact metallic/roughness texture`,
    );
    check(Boolean(material.normalTexture), `${name} has no compact normal map`);
  }
  check(
    (gltf.images ?? []).length === 9 &&
      (gltf.images ?? []).every(
        (image) =>
          image.mimeType === 'image/png' &&
          image.bufferView !== undefined &&
          image.uri === undefined,
      ),
    'expected nine embedded compact PNG PBR maps',
  );

  let primitives = 0;
  let triangles = 0;
  const tangentFailures = [];
  for (const [meshIndex, mesh] of meshes.entries()) {
    for (const [primitiveIndex, primitive] of (mesh.primitives ?? []).entries()) {
      primitives += 1;
      check(
        primitive.mode === undefined || primitive.mode === 4,
        `mesh ${meshIndex} primitive ${primitiveIndex} is not TRIANGLES`,
      );
      check(
        primitive.indices !== undefined,
        `mesh ${meshIndex} primitive ${primitiveIndex} is not indexed`,
      );
      check(
        primitive.attributes?.POSITION !== undefined &&
          primitive.attributes?.NORMAL !== undefined,
        `mesh ${meshIndex} primitive ${primitiveIndex} lacks POSITION/NORMAL`,
      );
      if (primitive.indices !== undefined)
        triangles += (accessors[primitive.indices]?.count ?? 0) / 3;
      const material = materials[primitive.material];
      if (material?.normalTexture) {
        if (
          primitive.attributes?.TANGENT === undefined ||
          primitive.attributes?.TEXCOORD_0 === undefined
        )
          tangentFailures.push({
            mesh: mesh.name ?? meshIndex,
            primitive: primitiveIndex,
            material: material.name,
          });
      }
    }
  }
  check(
    tangentFailures.length === 0,
    `${tangentFailures.length} normal-mapped primitives lack tangent/UV attributes`,
  );
  check(
    Number.isInteger(triangles) && triangles <= specification.triangleBudget,
    `triangle count ${triangles} exceeds ${specification.triangleBudget}`,
  );
  check(
    bytes.length <= specification.byteBudget,
    `GLB bytes ${bytes.length} exceed ${specification.byteBudget}`,
  );

  if (perAssetReport.symmetry) {
    for (const [source, group] of Object.entries(
      perAssetReport.symmetry.canonicalMeshGroups,
    )) {
      check(group.sharedMesh === true, `${source} lost source shared-mesh gate`);
      const meshIndexes = group.nodes.map(
        (name) => meshesForNamedNode(name, byName, nodes)[0],
      );
      check(
        new Set(meshIndexes).size === 1,
        `${source} cardinal instances do not share one GLB mesh`,
      );
    }
  }

  const artifactHash = hash(bytes);
  const aggregateAsset = aggregateBuildReport.assets.find(
    (asset) => asset.assetId === specification.id,
  );
  check(Boolean(aggregateAsset), 'asset is missing from aggregate build report');
  check(
    aggregateAsset?.artifacts.glb.sha256 === artifactHash &&
      perAssetReport.artifacts.glb.sha256 === artifactHash,
    'build report GLB hash differs from candidate',
  );
  check(
    aggregateAsset?.artifacts.glb.bytes === bytes.length &&
      perAssetReport.artifacts.glb.bytes === bytes.length,
    'build report GLB byte count differs from candidate',
  );
  check(
    aggregateAsset?.authoredConstruction.uniqueTriangles === triangles &&
      perAssetReport.authoredConstruction.uniqueTriangles === triangles,
    'build report triangle count differs from GLB',
  );
  const bounds = perAssetReport.authoredConstruction.boundsRuntimeYUp;
  const planform = perAssetReport.authoredConstruction.planformSpan;
  check(planform >= 0.99 && planform <= 1.01, `planform ${planform} is not one tile`);
  check(
    bounds.minimum[1] >= 0 && bounds.minimum[1] <= 0.01,
    `runtime floor ${bounds.minimum[1]} is outside the floor tolerance`,
  );
  check(
    perAssetReport.authoredConstruction.height <= 0.35,
    `height ${perAssetReport.authoredConstruction.height} exceeds form ceiling`,
  );

  return {
    assetId: specification.id,
    verdict: errors.length === 0 ? 'pass' : 'fail',
    errors,
    warnings,
    file: {
      name: path.basename(file),
      bytes: bytes.length,
      sha256: artifactHash,
    },
    budgets: {
      triangles,
      triangleCeiling: specification.triangleBudget,
      bytes: bytes.length,
      byteCeiling: specification.byteBudget,
      planform,
      floorY: bounds.minimum[1],
      height: perAssetReport.authoredConstruction.height,
    },
    structure: {
      nodes: nodes.length,
      meshes: meshes.length,
      primitives,
      materials: materials.length,
      textures: (gltf.textures ?? []).length,
      images: (gltf.images ?? []).length,
      accessors: accessors.length,
      skins: skins.length,
      skinJoints: skins[0]?.joints?.length ?? 0,
      animations: animationSummary,
      semanticTeamMaterials: semanticMaterials.length,
      semanticTeamNodes: teamNodes.length,
      normalMappedPrimitivesWithoutTangents: tangentFailures,
      cameras: (gltf.cameras ?? []).length,
      lights: gltf.extensions?.KHR_lights_punctual?.lights?.length ?? 0,
      extensionsUsed: gltf.extensionsUsed ?? [],
      extensionsRequired: gltf.extensionsRequired ?? [],
    },
  };
}

function meshesForNamedNode(name, byName, nodes) {
  return (byName.get(name) ?? [])
    .map((index) => nodes[index]?.mesh)
    .filter((index) => index !== undefined);
}

function sameSet(left, right) {
  return (
    left.length === right.length &&
    JSON.stringify([...left].sort()) === JSON.stringify([...right].sort())
  );
}

function near(left, right) {
  return Math.abs(left - right) <= EPSILON;
}

function vectorNear(left, right) {
  return (
    left.length === right.length &&
    left.every((value, index) => near(value, right[index]))
  );
}

function hash(bytes) {
  return createHash('sha256').update(bytes).digest('hex');
}
