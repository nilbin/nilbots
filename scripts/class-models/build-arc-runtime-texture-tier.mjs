#!/usr/bin/env node

import { createHash } from 'node:crypto';
import { spawnSync } from 'node:child_process';
import {
  copyFileSync,
  existsSync,
  mkdtempSync,
  mkdirSync,
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
const auditPath = path.join(
  repository,
  'art/class-models/provider-runs/meshy/arc-fleet-review/fleet-audit.json',
);
const tierDirectory = path.join(
  repository,
  'art/class-models/runtime-tiers/arc-relay/ktx2-selective-v1',
);
const tierAuditPath = path.join(tierDirectory, 'audit.json');
const check = process.argv.includes('--check');
const options = parseOptions(process.argv.slice(2).filter((value) => value !== '--check'));
const providerAudit = JSON.parse(readFileSync(auditPath, 'utf8'));
const entries = [providerAudit.pilot, ...providerAudit.generated].sort((left, right) =>
  left.lookId.localeCompare(right.lookId),
);
const TOOL_VERSION = '4.4.2';
const textureContract = {
  baseColorTexture: { width: 512, height: 512, encoding: 'ETC1S', quality: 255 },
  normalTexture: {
    width: 256,
    height: 256,
    encoding: 'UASTC',
    level: 2,
    rdoLambda: 0.5,
  },
  metallicRoughnessTexture: {
    width: 256,
    height: 256,
    encoding: 'UASTC',
    level: 2,
    rdoLambda: 4,
  },
  emissiveTexture: { width: 128, height: 128, encoding: 'ETC1S', quality: 255 },
};
const budgets = {
  decoderTransferBytes: 600_000,
  perLookTransferBytes: 1_048_576,
  fleetTransferBytes: 12 * 1_048_576,
  compressedTextureGpuBytes: 6 * 1_048_576,
  rgba8FallbackTextureGpuBytes: 36 * 1_048_576,
  compressedModelGpuBytes: 12 * 1_048_576,
  rgba8FallbackModelGpuBytes: 48 * 1_048_576,
};

if (entries.length !== 16 || new Set(entries.map((entry) => entry.lookId)).size !== 16)
  throw new Error('The provider audit must contain exactly sixteen unique Arc Relay looks.');

let tool = null;
let toolEnvironment = process.env;
if (!check) {
  const toktx = absolute(required('toktx'));
  if (!existsSync(toktx)) throw new Error(`Missing toktx executable: ${toktx}`);
  toolEnvironment = {
    ...process.env,
    PATH: `${path.dirname(toktx)}:${process.env.PATH ?? ''}`,
  };
  tool = options['gltf-transform']
    ? { command: absolute(options['gltf-transform']), prefix: [] }
    : { command: 'npx', prefix: ['--yes', `@gltf-transform/cli@${TOOL_VERSION}`] };
  const gltfVersion = run(tool, ['--version'], { capture: true }).trim();
  const ktxVersion = run({ command: toktx, prefix: [] }, ['--version'], {
    capture: true,
  }).trim();
  if (gltfVersion !== TOOL_VERSION)
    throw new Error(`Expected glTF-Transform ${TOOL_VERSION}, received ${gltfVersion}.`);
  if (!ktxVersion.includes(`v${TOOL_VERSION}`))
    throw new Error(`Expected KTX-Software ${TOOL_VERSION}, received ${ktxVersion}.`);
  mkdirSync(tierDirectory, { recursive: true });
}

const looks = [];
for (const entry of entries) {
  const approvedPath = path.join(repository, entry.candidate.file);
  const approved = readFileSync(approvedPath);
  if (approved.length !== entry.candidate.bytes || sha256(approved) !== entry.candidate.sha256)
    throw new Error(`${entry.lookId}: approved candidate bytes or SHA-256 changed.`);
  const sourcePath = normalizedRawSource(entry);
  const source = readFileSync(sourcePath);
  const outputPath = path.join(tierDirectory, `${entry.lookId}.glb`);

  if (!check) {
    console.log(`Building ${entry.lookId}...`);
    buildLook(entry, approvedPath, sourcePath, outputPath);
  }
  if (!existsSync(outputPath)) throw new Error(`${entry.lookId}: runtime tier model is missing.`);
  const runtime = readFileSync(outputPath);
  const { document } = readGlb(runtime, entry.lookId);
  const root = document.nodes?.[document.scenes?.[0]?.nodes?.[0]];
  const normalization = root?.extras?.nilbotsProviderNormalization;
  const triangles = triangleCount(document);
  const memory = inspectModelMemory(runtime, entry.lookId);
  const approvedGeometrySha256 = geometryFingerprint(approved, `${entry.lookId} approved`);
  const runtimeGeometrySha256 = geometryFingerprint(runtime, `${entry.lookId} runtime`);

  if (
    document.cameras ||
    document.skins ||
    document.animations ||
    document.materials?.length !== 1 ||
    document.textures?.length !== 4 ||
    document.images?.length !== 4
  )
    throw new Error(`${entry.lookId}: runtime tier changed the monolithic mesh contract.`);
  if (
    !(document.extensionsRequired ?? []).includes('KHR_texture_basisu') ||
    !(document.extensionsRequired ?? []).includes('KHR_mesh_quantization') ||
    (document.images ?? []).some((image) => image.mimeType !== 'image/ktx2')
  )
    throw new Error(`${entry.lookId}: runtime tier must require quantized geometry and KTX2.`);
  if (
    normalization?.facing !== '+x' ||
    normalization?.up !== '+y' ||
    normalization?.floorY !== 0 ||
    normalization?.orientation !== entry.orientation ||
    (normalization?.facingYawDegrees ?? 0) !== (entry.facingYawDegrees ?? 0) ||
    normalization?.targetPlanformSpan !== providerAudit.reviewContract.targetPlanformSpan
  )
    throw new Error(`${entry.lookId}: runtime normalization drifted from visual approval.`);
  if (triangles !== entry.candidate.triangles)
    throw new Error(`${entry.lookId}: runtime geometry drifted from the approved candidate.`);
  if (runtimeGeometrySha256 !== approvedGeometrySha256)
    throw new Error(`${entry.lookId}: runtime accessor data drifted from visual approval.`);
  assertTextureContract(entry.lookId, memory.textures);
  if (runtime.length > budgets.perLookTransferBytes)
    throw new Error(`${entry.lookId}: runtime model exceeds the per-look transfer budget.`);

  looks.push({
    id: entry.lookId,
    taskId: entry.taskId,
    approvedCandidate: {
      file: entry.candidate.file,
      bytes: approved.length,
      sha256: sha256(approved),
    },
    normalizedSource: {
      file: path.relative(repository, sourcePath),
      bytes: source.length,
      sha256: sha256(source),
    },
    runtime: {
      file: path.relative(repository, outputPath),
      bytes: runtime.length,
      sha256: sha256(runtime),
    },
    orientation: entry.orientation,
    facingYawDegrees: entry.facingYawDegrees ?? 0,
    triangles,
    geometrySha256: runtimeGeometrySha256,
    materials: document.materials.length,
    textures: document.textures.length,
    targetPlanformSpan: normalization.targetPlanformSpan,
    floorY: normalization.floorY,
    memory,
  });
}

const totals = looks.reduce(
  (sum, look) => ({
    approvedTransferBytes: sum.approvedTransferBytes + look.approvedCandidate.bytes,
    runtimeTransferBytes: sum.runtimeTransferBytes + look.runtime.bytes,
    geometryGpuBytes: sum.geometryGpuBytes + look.memory.geometryGpuBytes,
    textureGpuBytesCompressedTarget:
      sum.textureGpuBytesCompressedTarget + look.memory.textureGpuBytesCompressedTarget,
    textureGpuBytesRgba8Fallback:
      sum.textureGpuBytesRgba8Fallback + look.memory.textureGpuBytesRgba8Fallback,
    modelGpuBytesCompressedTarget:
      sum.modelGpuBytesCompressedTarget + look.memory.modelGpuBytesCompressedTarget,
    modelGpuBytesRgba8Fallback:
      sum.modelGpuBytesRgba8Fallback + look.memory.modelGpuBytesRgba8Fallback,
    triangles: sum.triangles + look.triangles,
  }),
  {
    approvedTransferBytes: 0,
    runtimeTransferBytes: 0,
    geometryGpuBytes: 0,
    textureGpuBytesCompressedTarget: 0,
    textureGpuBytesRgba8Fallback: 0,
    modelGpuBytesCompressedTarget: 0,
    modelGpuBytesRgba8Fallback: 0,
    triangles: 0,
  },
);
assertFleetBudgets(totals);

const tierAudit = {
  schemaVersion: 1,
  id: 'arc-relay-ktx2-selective-v1',
  generator: 'scripts/class-models/build-arc-runtime-texture-tier.mjs',
  sourceAudit: path.relative(repository, auditPath),
  toolchain: {
    gltfTransform: TOOL_VERSION,
    ktxSoftware: TOOL_VERSION,
  },
  contract: {
    geometry: 'byte-semantic accessor match to the approved quantized monolithic candidate',
    texturePolicy: textureContract,
    mipmaps: true,
    compressedTarget:
      'opaque ETC1S to ETC1/ETC2 RGB at 4 bpp; UASTC to ASTC/BC7/ETC2 RGBA at 8 bpp',
    fallback: 'RGBA8 when the device exposes no supported compressed texture target',
    decoder: 'Three KTX2Loader, initialized only by the WebGL renderer',
  },
  budgets,
  totals,
  looks,
};

if (check) {
  if (readFileSync(tierAuditPath, 'utf8') !== json(tierAudit))
    throw new Error(`${path.relative(repository, tierAuditPath)} is not the audited tier.`);
} else {
  writeFileSync(tierAuditPath, json(tierAudit));
}
console.log(
  `${check ? 'Verified' : 'Built'} ${looks.length} selective KTX2 looks ` +
    `(${totals.runtimeTransferBytes} transfer bytes, ` +
    `${totals.modelGpuBytesCompressedTarget} compressed-target GPU bytes).`,
);

function buildLook(entry, approvedPath, sourcePath, outputPath) {
  const temporary = mkdtempSync(path.join(tmpdir(), `nilbots-${entry.lookId}-ktx2-`));
  try {
    const hybrid = path.join(temporary, '00-approved-geometry-raw-textures.glb');
    writeFileSync(
      hybrid,
      approvedGeometryWithRawTextures(
        readFileSync(approvedPath),
        readFileSync(sourcePath),
        entry.lookId,
      ),
    );
    const names = textureNames(hybrid, entry.lookId);
    let input = hybrid;
    for (const [index, slot] of [
      'baseColorTexture',
      'normalTexture',
      'metallicRoughnessTexture',
      'emissiveTexture',
    ].entries()) {
      const output = path.join(temporary, `1${index}-${slot}.glb`);
      const spec = textureContract[slot];
      run(tool, [
        'resize',
        input,
        output,
        '--pattern',
        names[slot],
        '--width',
        String(spec.width),
        '--height',
        String(spec.height),
      ]);
      input = output;
    }
    const normal = path.join(temporary, '20-normal-uastc.glb');
    run(tool, [
      'uastc',
      input,
      normal,
      '--slots',
      'normalTexture',
      '--level',
      '2',
      '--rdo',
      '--rdo-lambda',
      '0.5',
      '--rdo-multithreading',
      'false',
      '--jobs',
      '1',
    ]);
    const material = path.join(temporary, '21-material-uastc.glb');
    run(tool, [
      'uastc',
      normal,
      material,
      '--slots',
      'metallicRoughnessTexture',
      '--level',
      '2',
      '--rdo',
      '--rdo-lambda',
      '4',
      '--rdo-multithreading',
      'false',
      '--jobs',
      '1',
    ]);
    const encoded = path.join(temporary, '22-color-etc1s.glb');
    run(tool, [
      'etc1s',
      material,
      encoded,
      '--slots',
      '{baseColorTexture,emissiveTexture}',
      '--quality',
      '255',
      '--jobs',
      '1',
    ]);
    // The approved candidate already owns its audited facing correction. Only its texture
    // payloads are replaced, so applying the yaw again here would double-rotate Kestrel and
    // Mortar while still producing superficially valid normalization metadata.
    copyFileSync(encoded, outputPath);
  } finally {
    rmSync(temporary, { recursive: true, force: true });
  }
}

function approvedGeometryWithRawTextures(approvedBytes, rawBytes, lookId) {
  const approved = readGlb(approvedBytes, `${lookId} approved`);
  const raw = readGlb(rawBytes, `${lookId} raw texture source`);
  const document = structuredClone(approved.document);
  const approvedSlots = textureSources(document, lookId);
  const rawSlots = textureSources(raw.document, lookId);
  const parts = [Buffer.from(approved.binary)];
  let byteOffset = approved.binary.length;

  for (const slot of [
    'baseColorTexture',
    'normalTexture',
    'metallicRoughnessTexture',
    'emissiveTexture',
  ]) {
    const target = approvedSlots[slot];
    const source = rawSlots[slot];
    const sourceView = raw.document.bufferViews?.[source.image.bufferView];
    if (!sourceView || !['image/jpeg', 'image/png'].includes(source.image.mimeType))
      throw new Error(`${lookId}: ${slot} raw texture is not an embedded JPEG or PNG.`);
    const imageBytes = Buffer.from(
      raw.binary.subarray(
        sourceView.byteOffset ?? 0,
        (sourceView.byteOffset ?? 0) + sourceView.byteLength,
      ),
    );
    const padding = (4 - (byteOffset % 4)) % 4;
    if (padding) {
      parts.push(Buffer.alloc(padding));
      byteOffset += padding;
    }
    const viewIndex = document.bufferViews.length;
    document.bufferViews.push({ buffer: 0, byteOffset, byteLength: imageBytes.length });
    parts.push(imageBytes);
    byteOffset += imageBytes.length;

    document.images[target.imageIndex] = {
      ...document.images[target.imageIndex],
      bufferView: viewIndex,
      mimeType: source.image.mimeType,
    };
    const texture = document.textures[target.textureIndex];
    texture.source = target.imageIndex;
    if (texture.extensions?.EXT_texture_webp) delete texture.extensions.EXT_texture_webp;
    if (texture.extensions && Object.keys(texture.extensions).length === 0)
      delete texture.extensions;
  }
  document.extensionsUsed = (document.extensionsUsed ?? []).filter(
    (extension) => extension !== 'EXT_texture_webp',
  );
  document.extensionsRequired = (document.extensionsRequired ?? []).filter(
    (extension) => extension !== 'EXT_texture_webp',
  );
  if (document.extensionsUsed.length === 0) delete document.extensionsUsed;
  if (document.extensionsRequired.length === 0) delete document.extensionsRequired;
  const binary = Buffer.concat(parts);
  document.buffers[0].byteLength = binary.length;
  return buildGlb(document, binary);
}

function textureSources(document, lookId) {
  const material = document.materials?.[0];
  const infos = {
    baseColorTexture: material?.pbrMetallicRoughness?.baseColorTexture,
    normalTexture: material?.normalTexture,
    metallicRoughnessTexture: material?.pbrMetallicRoughness?.metallicRoughnessTexture,
    emissiveTexture: material?.emissiveTexture,
  };
  const result = {};
  for (const [slot, info] of Object.entries(infos)) {
    const textureIndex = info?.index;
    const texture = document.textures?.[textureIndex];
    const imageIndex =
      texture?.extensions?.KHR_texture_basisu?.source ??
      texture?.extensions?.EXT_texture_webp?.source ??
      texture?.source;
    const image = document.images?.[imageIndex];
    if (textureIndex === undefined || imageIndex === undefined || !image)
      throw new Error(`${lookId}: ${slot} has no resolvable texture source.`);
    result[slot] = { textureIndex, imageIndex, image };
  }
  return result;
}

function buildGlb(document, binary) {
  const jsonBytes = Buffer.from(JSON.stringify(document));
  const jsonPadding = (4 - (jsonBytes.length % 4)) % 4;
  const binaryPadding = (4 - (binary.length % 4)) % 4;
  const json = Buffer.concat([jsonBytes, Buffer.alloc(jsonPadding, 0x20)]);
  const bin = Buffer.concat([binary, Buffer.alloc(binaryPadding)]);
  const total = 12 + 8 + json.length + 8 + bin.length;
  const header = Buffer.alloc(12);
  header.write('glTF', 0, 'ascii');
  header.writeUInt32LE(2, 4);
  header.writeUInt32LE(total, 8);
  const jsonHeader = Buffer.alloc(8);
  jsonHeader.writeUInt32LE(json.length, 0);
  jsonHeader.writeUInt32LE(0x4e4f534a, 4);
  const binHeader = Buffer.alloc(8);
  binHeader.writeUInt32LE(bin.length, 0);
  binHeader.writeUInt32LE(0x004e4942, 4);
  return Buffer.concat([header, jsonHeader, json, binHeader, bin]);
}

function textureNames(file, lookId) {
  const { document } = readGlb(readFileSync(file), lookId);
  const result = {};
  const material = document.materials?.[0];
  const slots = {
    baseColorTexture: material?.pbrMetallicRoughness?.baseColorTexture,
    normalTexture: material?.normalTexture,
    metallicRoughnessTexture: material?.pbrMetallicRoughness?.metallicRoughnessTexture,
    emissiveTexture: material?.emissiveTexture,
  };
  for (const [slot, info] of Object.entries(slots)) {
    const texture = document.textures?.[info?.index];
    const image = document.images?.[texture?.source];
    if (!image?.name) throw new Error(`${lookId}: ${slot} has no named source image.`);
    result[slot] = image.name;
  }
  if (new Set(Object.values(result)).size !== 4)
    throw new Error(`${lookId}: runtime texture slots must use four distinct images.`);
  return result;
}

function normalizedRawSource(entry) {
  const directory = path.dirname(path.join(repository, entry.candidate.file));
  const filename = path.basename(entry.candidate.file);
  const candidates = [
    filename.replace('-identity-facing-review.glb', '-identity-raw.glb'),
    filename.replace('-facing-review.glb', '-raw.glb'),
    filename.replace('-review.glb', '-raw.glb'),
  ];
  for (const candidate of [...new Set(candidates)]) {
    const file = path.join(directory, candidate);
    if (candidate !== filename && existsSync(file)) return file;
  }
  throw new Error(`${entry.lookId}: could not resolve the normalized raw visual source.`);
}

function assertTextureContract(lookId, textures) {
  if (textures.length !== 4) throw new Error(`${lookId}: expected four runtime textures.`);
  for (const [slot, spec] of Object.entries(textureContract)) {
    const texture = textures.find((candidate) => candidate.slots.includes(slot));
    if (
      !texture ||
      texture.slots.length !== 1 ||
      texture.width !== spec.width ||
      texture.height !== spec.height ||
      texture.encoding !== spec.encoding
    )
      throw new Error(`${lookId}: ${slot} does not match the selective texture contract.`);
  }
}

function assertFleetBudgets(totals) {
  for (const [field, budget] of [
    ['runtimeTransferBytes', budgets.fleetTransferBytes],
    ['textureGpuBytesCompressedTarget', budgets.compressedTextureGpuBytes],
    ['textureGpuBytesRgba8Fallback', budgets.rgba8FallbackTextureGpuBytes],
    ['modelGpuBytesCompressedTarget', budgets.compressedModelGpuBytes],
    ['modelGpuBytesRgba8Fallback', budgets.rgba8FallbackModelGpuBytes],
  ])
    if (totals[field] > budget)
      throw new Error(`Fleet ${field} ${totals[field]} exceeds budget ${budget}.`);
}

function run(selected, args, { capture = false } = {}) {
  const result = spawnSync(selected.command, [...selected.prefix, ...args], {
    cwd: repository,
    env: toolEnvironment,
    encoding: 'utf8',
    stdio: capture ? 'pipe' : ['ignore', 'pipe', 'pipe'],
  });
  if (result.error) throw result.error;
  if (result.status !== 0)
    throw new Error(
      `${selected.command} ${args[0] ?? ''} failed with exit code ${result.status}:\n` +
        `${result.stdout ?? ''}${result.stderr ?? ''}`,
    );
  return `${result.stdout ?? ''}${result.stderr ?? ''}`;
}

function parseOptions(args) {
  const parsed = {};
  for (let index = 0; index < args.length; index += 2) {
    const name = args[index];
    const value = args[index + 1];
    if (!name?.startsWith('--') || !value)
      throw new Error(`Expected --name value, received ${name ?? 'nothing'}.`);
    parsed[name.slice(2)] = value;
  }
  return parsed;
}

function required(name) {
  const value = options[name];
  if (!value)
    throw new Error(
      'Usage: build-arc-runtime-texture-tier.mjs --toktx <path> ' +
        '[--gltf-transform <path>] [--check]',
    );
  return value;
}

function absolute(value) {
  return path.resolve(repository, value);
}

function sha256(bytes) {
  return createHash('sha256').update(bytes).digest('hex');
}

function json(value) {
  return `${JSON.stringify(value, null, 2)}\n`;
}
