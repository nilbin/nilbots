#!/usr/bin/env node

import { createHash } from 'node:crypto';
import { mkdir, readFile, writeFile } from 'node:fs/promises';
import path from 'node:path';

const JSON_CHUNK = 0x4e4f534a;
const BIN_CHUNK = 0x004e4942;
const EXPECTED_BONES = [
  'root',
  'body',
  'cannon',
  'hip_fl',
  'knee_fl',
  'ankle_fl',
  'hip_fr',
  'knee_fr',
  'ankle_fr',
  'hip_rl',
  'knee_rl',
  'ankle_rl',
  'hip_rr',
  'knee_rr',
  'ankle_rr',
];
const EXPECTED_TEAM_NODES = [
  'TeamInlay.RearLeft',
  'TeamInlay.RearRight',
  'TeamInlay.WestCentre',
];

const args = process.argv.slice(2);
const glbFlag = args.indexOf('--glb');
const outputFlag = args.indexOf('--output');
if (glbFlag < 0 || outputFlag < 0 || !args[glbFlag + 1] || !args[outputFlag + 1])
  throw new Error('usage: validate_aegis_glb.mjs --glb <path> --output <path>');

const glbPath = path.resolve(args[glbFlag + 1]);
const outputPath = path.resolve(args[outputFlag + 1]);
const bytes = await readFile(glbPath);
const errors = [];
const warnings = [];

function requireCondition(condition, message) {
  if (!condition) errors.push(message);
}

requireCondition(bytes.length >= 20, 'GLB is shorter than its minimum header');
requireCondition(bytes.toString('ascii', 0, 4) === 'glTF', 'GLB magic is not glTF');
requireCondition(bytes.readUInt32LE(4) === 2, 'GLB version is not 2');
requireCondition(
  bytes.readUInt32LE(8) === bytes.length,
  'GLB declared length does not match file length',
);

const chunks = [];
for (let offset = 12; offset + 8 <= bytes.length; ) {
  const byteLength = bytes.readUInt32LE(offset);
  const type = bytes.readUInt32LE(offset + 4);
  const start = offset + 8;
  const end = start + byteLength;
  requireCondition(end <= bytes.length, `GLB chunk at ${offset} exceeds file length`);
  if (end > bytes.length) break;
  chunks.push({ type, byteLength, start, end });
  offset = end;
}
requireCondition(chunks.length === 2, `expected JSON and BIN chunks, found ${chunks.length}`);
requireCondition(chunks[0]?.type === JSON_CHUNK, 'first GLB chunk is not JSON');
requireCondition(chunks[1]?.type === BIN_CHUNK, 'second GLB chunk is not BIN');

let gltf = {};
try {
  const jsonChunk = chunks.find((chunk) => chunk.type === JSON_CHUNK);
  requireCondition(Boolean(jsonChunk), 'GLB has no JSON chunk');
  if (jsonChunk)
    gltf = JSON.parse(
      bytes
        .subarray(jsonChunk.start, jsonChunk.end)
        .toString('utf8')
        .replace(/[\u0000\u0020]+$/u, ''),
    );
} catch (error) {
  errors.push(`GLB JSON cannot be parsed: ${error.message}`);
}

const nodes = gltf.nodes ?? [];
const meshes = gltf.meshes ?? [];
const materials = gltf.materials ?? [];
const animations = gltf.animations ?? [];
const skins = gltf.skins ?? [];
const accessors = gltf.accessors ?? [];
const nodeIndexesByName = new Map();
for (const [index, node] of nodes.entries()) {
  const matches = nodeIndexesByName.get(node.name) ?? [];
  matches.push(index);
  nodeIndexesByName.set(node.name, matches);
}

requireCondition((gltf.cameras ?? []).length === 0, 'runtime GLB contains cameras');
requireCondition(
  !(gltf.extensionsUsed ?? []).includes('KHR_lights_punctual') &&
    !gltf.extensions?.KHR_lights_punctual,
  'runtime GLB contains punctual lights',
);

for (const bone of EXPECTED_BONES)
  requireCondition(
    nodeIndexesByName.get(bone)?.length === 1,
    `expected exactly one rig node named ${bone}`,
  );
requireCondition(skins.length === 1, `expected one skin, found ${skins.length}`);
if (skins.length === 1) {
  requireCondition(
    skins[0].name === 'Aegis.Tortoise.Rig',
    `unexpected skin name: ${skins[0].name}`,
  );
  const jointNames = (skins[0].joints ?? []).map((index) => nodes[index]?.name);
  requireCondition(
    JSON.stringify([...jointNames].sort()) ===
      JSON.stringify([...EXPECTED_BONES].sort()),
    `skin joints differ from the 15-bone Aegis contract: ${jointNames.join(', ')}`,
  );
}

const animationNames = animations.map((animation) => animation.name);
for (const action of ['Aegis_Idle', 'Aegis_Walk'])
  requireCondition(
    animationNames.filter((name) => name === action).length === 1,
    `expected exactly one ${action} animation`,
  );
requireCondition(
  animations.length === 2,
  `expected exactly two authored animations, found ${animations.length}`,
);
for (const animation of animations) {
  requireCondition(
    (animation.channels ?? []).length === 45,
    `${animation.name} should animate 15 bones through 45 channels`,
  );
  for (const channel of animation.channels ?? []) {
    const target = channel.target ?? {};
    requireCondition(
      EXPECTED_BONES.includes(nodes[target.node]?.name),
      `${animation.name} targets a node outside the rig contract`,
    );
    requireCondition(
      ['translation', 'rotation', 'scale'].includes(target.path),
      `${animation.name} contains unsupported target path ${target.path}`,
    );
  }
}

const semanticMaterialIndexes = materials.flatMap((material, index) =>
  material.extras?.nilbotsRole === 'team-accent' ? [index] : [],
);
requireCondition(
  semanticMaterialIndexes.length === 1,
  `expected exactly one semantic team-accent material, found ${semanticMaterialIndexes.length}`,
);
const semanticMaterialIndex = semanticMaterialIndexes[0];

const teamNodes = nodes.filter((node) => node.name?.startsWith('TeamInlay.'));
requireCondition(
  teamNodes.length === EXPECTED_TEAM_NODES.length,
  `expected three TeamInlay nodes, found ${teamNodes.length}`,
);
for (const name of EXPECTED_TEAM_NODES) {
  const indexes = nodeIndexesByName.get(name) ?? [];
  requireCondition(indexes.length === 1, `expected exactly one ${name} node`);
  const node = nodes[indexes[0]];
  requireCondition(
    node?.extras?.nilbotsRole === 'team-accent' &&
      node?.extras?.nilbotsPart === 'semantic-team-inlay',
    `${name} lacks semantic team extras`,
  );
  const primitives = meshes[node?.mesh]?.primitives ?? [];
  requireCondition(primitives.length > 0, `${name} has no mesh primitives`);
  requireCondition(
    primitives.every((primitive) => primitive.material === semanticMaterialIndex),
    `${name} does not exclusively use the semantic team material`,
  );
}

const rigIndexes = nodeIndexesByName.get('Aegis.Tortoise.Rig') ?? [];
requireCondition(rigIndexes.length === 1, 'expected exactly one Aegis.Tortoise.Rig node');
const rigExtras = nodes[rigIndexes[0]]?.extras ?? {};
requireCondition(rigExtras.nilbotsFacing === '+X', 'rig facing contract is not +X');
requireCondition(
  rigExtras.nilbotsUp === '+Y after glTF export',
  'rig up-axis contract is not +Y after glTF export',
);
requireCondition(
  rigExtras.nilbotsAnimationContract ===
    'looped in-place actions; replay renderer seeks normalized clip time',
  'rig animation contract is missing or changed',
);

const armorNodes = nodes.filter((node) => node.name?.startsWith('Armor.ProviderMaster.'));
requireCondition(armorNodes.length === 1, 'expected one provider upper-armor master');
requireCondition(
  armorNodes[0]?.extras?.nilbotsPart === 'provider-upper-armor-master',
  'provider armor semantic part is missing',
);
for (const name of ['Cannon.Barrel', 'Cannon.Housing', 'Cannon.Muzzle'])
  requireCondition(
    nodeIndexesByName.get(name)?.length === 1,
    `missing authored cannon part ${name}`,
  );
for (const corner of ['FL', 'FR', 'RL', 'RR'])
  for (const part of ['UpperPiston', 'Lower', 'Foot', 'ToeArmor'])
    requireCondition(
      nodeIndexesByName.get(`Leg.${corner}.${part}`)?.length === 1,
      `missing authored leg part Leg.${corner}.${part}`,
    );

let primitiveCount = 0;
let indexedTriangleCount = 0;
const missingTangentPrimitives = [];
for (const [meshIndex, mesh] of meshes.entries()) {
  for (const [primitiveIndex, primitive] of (mesh.primitives ?? []).entries()) {
    primitiveCount += 1;
    requireCondition(
      primitive.mode === undefined || primitive.mode === 4,
      `mesh ${meshIndex} primitive ${primitiveIndex} is not TRIANGLES`,
    );
    if (primitive.indices !== undefined)
      indexedTriangleCount += (accessors[primitive.indices]?.count ?? 0) / 3;
    const material = materials[primitive.material];
    if (material?.normalTexture && primitive.attributes?.TANGENT === undefined)
      missingTangentPrimitives.push({
        mesh: mesh.name ?? meshIndex,
        primitive: primitiveIndex,
        material: material.name ?? primitive.material,
      });
  }
}
if (missingTangentPrimitives.length > 0)
  warnings.push(
    `${missingTangentPrimitives.length} normal-mapped primitive(s) rely on renderer-generated tangents`,
  );

const skinConsumerNodes = nodes.filter((node) => node.skin !== undefined).length;
const report = {
  schemaVersion: 1,
  asset: 'aegis-tortoise-mobile',
  file: {
    name: path.basename(glbPath),
    bytes: bytes.length,
    sha256: createHash('sha256').update(bytes).digest('hex'),
  },
  verdict: errors.length === 0 ? 'pass-with-recorded-warnings' : 'fail',
  errors,
  warnings,
  structure: {
    nodes: nodes.length,
    meshes: meshes.length,
    primitives: primitiveCount,
    indexedTriangles: indexedTriangleCount,
    materials: materials.length,
    textures: (gltf.textures ?? []).length,
    images: (gltf.images ?? []).length,
    accessors: accessors.length,
    animations: animations.map((animation) => ({
      name: animation.name,
      channels: animation.channels?.length ?? 0,
      samplers: animation.samplers?.length ?? 0,
    })),
    skins: skins.length,
    skinJoints: skins[0]?.joints?.length ?? 0,
    skinConsumerNodes,
    semanticTeamMaterials: semanticMaterialIndexes.length,
    semanticTeamNodes: teamNodes.length,
    cameras: (gltf.cameras ?? []).length,
    lights: gltf.extensions?.KHR_lights_punctual?.lights?.length ?? 0,
    extensionsUsed: gltf.extensionsUsed ?? [],
    extensionsRequired: gltf.extensionsRequired ?? [],
  },
  portability: {
    normalMappedPrimitivesWithoutTangents: missingTangentPrimitives,
    note:
      'Nilbots targets Three.js; broader glTF portability still requires authored tangents or a recorded generated-tangent basis.',
  },
};

await mkdir(path.dirname(outputPath), { recursive: true });
await writeFile(outputPath, `${JSON.stringify(report, null, 2)}\n`);
process.stdout.write(`${JSON.stringify(report, null, 2)}\n`);
if (errors.length > 0) process.exitCode = 1;
