#!/usr/bin/env node

import { createHash } from 'node:crypto';
import { createRequire } from 'node:module';
import { existsSync, mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import {
  geometryFingerprint,
  inspectModelMemory,
  readGlb,
  triangleCount,
} from './model-memory.mjs';

const JSON_CHUNK = 0x4e4f534a;
const BIN_CHUNK = 0x004e4942;
const repository = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..', '..');
const requireFromWeb = createRequire(path.join(repository, 'web/package.json'));
const sharp = requireFromWeb('sharp');
const check = process.argv.includes('--check');
const sourceAuditPath = path.join(
  repository,
  'art/class-models/runtime-tiers/arc-relay/meshy7-longshot-railback-review-v1/audit.json',
);
const outputDirectory = path.join(
  repository,
  'art/class-models/runtime-tiers/arc-relay/meshy7-longshot-railback-color-review-v2',
);
const outputPath = path.join(outputDirectory, 'arc-longshot-railback-color-lift.glb');
const auditPath = path.join(outputDirectory, 'audit.json');
const transform = {
  operation: 'per-channel linear tonal lift on baseColorTexture RGB only',
  multiplier: 0.88,
  offset: 25,
  encoding: 'WebP quality 90',
  rationale:
    'Separate the dusty slate-blue authored hull from the dark blue-gray arena floor without increasing saturation, gloss, or team color.',
};
const sourceAudit = JSON.parse(readFileSync(sourceAuditPath, 'utf8'));
const sourceProfile = sourceAudit.profiles?.find((profile) => profile.id === 'lean');
if (!sourceProfile || sourceAudit.status !== 'owner-review')
  throw new Error('Expected the pinned Railback lean owner-review source.');
const sourcePath = path.join(repository, sourceProfile.artifact.file);
const sourceBytes = readFileSync(sourcePath);
if (sourceBytes.length !== sourceProfile.artifact.bytes || sha256(sourceBytes) !== sourceProfile.artifact.sha256)
  throw new Error('Railback lean source drifted from its audit.');

mkdirSync(outputDirectory, { recursive: true });
if (!check) await buildVariant();
if (!existsSync(outputPath)) throw new Error('Railback color-lift output is missing.');

const outputBytes = readFileSync(outputPath);
const sourceGlb = readGlb(sourceBytes, 'Railback lean source');
const outputGlb = readGlb(outputBytes, 'Railback color lift');
const sourceImages = embeddedImages(sourceGlb);
const outputImages = embeddedImages(outputGlb);
const sourceSlots = textureSlots(sourceGlb.document);
const outputSlots = textureSlots(outputGlb.document);
const sourceBaseIndex = imageIndexForSlot(sourceSlots, 'baseColorTexture');
const outputBaseIndex = imageIndexForSlot(outputSlots, 'baseColorTexture');
const sourceBase = sourceImages[sourceBaseIndex];
const outputBase = outputImages[outputBaseIndex];
const sourceRoughness = sourceImages[imageIndexForSlot(sourceSlots, 'metallicRoughnessTexture')];
const outputRoughness = outputImages[imageIndexForSlot(outputSlots, 'metallicRoughnessTexture')];
const sourceNormal = sourceImages[imageIndexForSlot(sourceSlots, 'normalTexture')];
const outputNormal = outputImages[imageIndexForSlot(outputSlots, 'normalTexture')];
const geometrySha256 = geometryFingerprint(outputBytes, 'Railback color lift');
const sourceGeometrySha256 = geometryFingerprint(sourceBytes, 'Railback lean source');
const colorMetrics = {
  before: await valueMetrics(sourceBase.bytes),
  after: await valueMetrics(outputBase.bytes),
};
const memory = inspectModelMemory(outputBytes, 'Railback color lift');
const triangles = triangleCount(outputGlb.document);
const extras = outputGlb.document.asset?.extras?.nilbotsBaseColorValueLift;
if (
  geometrySha256 !== sourceGeometrySha256 ||
  triangles !== sourceProfile.triangles ||
  outputGlb.document.meshes?.length !== sourceProfile.meshes ||
  outputGlb.document.materials?.length !== sourceProfile.materials ||
  outputGlb.document.textures?.length !== sourceProfile.textures ||
  sha256(sourceBase.bytes) === sha256(outputBase.bytes) ||
  sha256(sourceRoughness.bytes) !== sha256(outputRoughness.bytes) ||
  sha256(sourceNormal.bytes) !== sha256(outputNormal.bytes) ||
  extras?.multiplier !== transform.multiplier ||
  extras?.offset !== transform.offset ||
  extras?.scope !== 'baseColorTexture.rgb' ||
  outputBytes.length > 2_200_000
)
  throw new Error('Railback color-only review contract failed.');
if (colorMetrics.after.medianLuminance <= colorMetrics.before.medianLuminance + 8)
  throw new Error('Railback color lift did not create enough value separation.');

const outputProfile = {
  ...sourceProfile,
  id: 'lean',
  settings: {
    ...sourceProfile.settings,
    baseColorTransform: transform,
  },
  artifact: {
    file: path.relative(repository, outputPath),
    bytes: outputBytes.length,
    sha256: sha256(outputBytes),
  },
  geometrySha256,
  memory,
};
const audit = {
  ...sourceAudit,
  id: 'meshy7-longshot-railback-color-review-v2',
  status: 'owner-review',
  generator: 'scripts/class-models/build-railback-color-review-v2.mjs',
  source: {
    ...sourceAudit.source,
    baseReviewArtifact: {
      file: sourceProfile.artifact.file,
      bytes: sourceProfile.artifact.bytes,
      sha256: sourceProfile.artifact.sha256,
    },
  },
  toolchain: {
    ...sourceAudit.toolchain,
    colorAdjustment: 'Sharp RGB linear transform and WebP encoding',
  },
  contract: {
    ...sourceAudit.contract,
    purpose:
      'Reversible in-renderer color comparison only; geometry, scale, matte roughness, team accents, and the approved runtime Longshot remain unchanged.',
    materials:
      'Only the embedded baseColorTexture RGB was lifted. Metallic-roughness and normal texture bytes are identical to the Railback lean source.',
    matteFinish:
      'Metallic-roughness remains deterministically matte; its texture and the normal map are unchanged while base-color RGB alone receives the documented tonal lift.',
  },
  colorAdjustment: {
    transform,
    sourceBaseColorSha256: sha256(sourceBase.bytes),
    outputBaseColorSha256: sha256(outputBase.bytes),
    roughnessPreservedSha256: sha256(outputRoughness.bytes),
    normalPreservedSha256: sha256(outputNormal.bytes),
    geometryPreservedSha256: geometrySha256,
    valueMetrics: colorMetrics,
    saturationIncrease: false,
    roughnessChanged: false,
    normalChanged: false,
    geometryChanged: false,
    runtimeTeamColorsChanged: false,
  },
  profiles: [outputProfile],
};
const auditBytes = Buffer.from(`${JSON.stringify(audit, null, 2)}\n`);

if (check) {
  if (!existsSync(auditPath) || !readFileSync(auditPath).equals(auditBytes))
    throw new Error('Railback color-review audit drifted from current artifacts.');
  console.log('Railback color-only review artifact and audit are current.');
} else {
  writeFileSync(auditPath, auditBytes);
  console.log(
    `Built Railback color-only review: median luminance ` +
      `${colorMetrics.before.medianLuminance} → ${colorMetrics.after.medianLuminance}; ` +
      `${outputBytes.length} bytes.`,
  );
}

async function buildVariant() {
  const { document, binary } = parseGlb(sourceBytes);
  const slots = textureSlots(document);
  const imageIndex = imageIndexForSlot(slots, 'baseColorTexture');
  const image = document.images?.[imageIndex];
  const view = document.bufferViews?.[image?.bufferView];
  if (!image || !view || image.mimeType !== 'image/webp')
    throw new Error('Railback color lift requires one embedded WebP baseColorTexture.');
  const encoded = binary.subarray(
    view.byteOffset ?? 0,
    (view.byteOffset ?? 0) + view.byteLength,
  );
  const { data, info } = await sharp(encoded).raw().toBuffer({ resolveWithObject: true });
  if (info.channels < 3) throw new Error('Railback baseColorTexture must contain RGB.');
  let changedPixels = 0;
  let channelDelta = 0;
  for (let offset = 0; offset < data.length; offset += info.channels) {
    let changed = false;
    for (let channel = 0; channel < 3; channel += 1) {
      const before = data[offset + channel];
      const after = clamp8(Math.round(before * transform.multiplier + transform.offset));
      data[offset + channel] = after;
      channelDelta += after - before;
      if (after !== before) changed = true;
    }
    if (changed) changedPixels += 1;
  }
  const replacement = await sharp(data, { raw: info })
    .webp({ quality: 90, alphaQuality: 100, effort: 6, smartSubsample: true })
    .toBuffer();
  let outputBinary = Buffer.from(binary.subarray(0, document.buffers[0].byteLength));
  const padding = (4 - (outputBinary.length % 4)) % 4;
  if (padding) outputBinary = Buffer.concat([outputBinary, Buffer.alloc(padding)]);
  const replacementView = document.bufferViews.length;
  document.bufferViews.push({
    buffer: 0,
    byteOffset: outputBinary.length,
    byteLength: replacement.length,
  });
  outputBinary = Buffer.concat([outputBinary, replacement]);
  image.bufferView = replacementView;
  image.mimeType = 'image/webp';
  document.buffers[0].byteLength = outputBinary.length;
  document.asset.generator =
    `${document.asset?.generator ?? 'unknown'} + Nilbots base-color value lift 1`;
  document.asset.extras = {
    ...(document.asset.extras ?? {}),
    nilbotsBaseColorValueLift: {
      version: 1,
      scope: 'baseColorTexture.rgb',
      multiplier: transform.multiplier,
      offset: transform.offset,
      pixels: info.width * info.height,
      changedPixels,
      averageChannelDelta: channelDelta / (info.width * info.height * 3),
      saturationIncrease: false,
      roughnessChanged: false,
      normalChanged: false,
      geometryChanged: false,
      teamColorsChanged: false,
    },
  };
  writeFileSync(outputPath, buildGlb(document, outputBinary));
}

function parseGlb(bytes) {
  if (
    bytes.toString('ascii', 0, 4) !== 'glTF' ||
    bytes.readUInt32LE(4) !== 2 ||
    bytes.readUInt32LE(8) !== bytes.length
  )
    throw new Error('Expected a valid glTF 2.0 binary.');
  let document = null;
  let binary = null;
  let offset = 12;
  while (offset < bytes.length) {
    const length = bytes.readUInt32LE(offset);
    const type = bytes.readUInt32LE(offset + 4);
    const data = bytes.subarray(offset + 8, offset + 8 + length);
    if (type === JSON_CHUNK) document = JSON.parse(data.toString('utf8').trim());
    if (type === BIN_CHUNK) binary = data;
    offset += 8 + length;
  }
  if (!document || !binary) throw new Error('GLB requires JSON and BIN chunks.');
  return { document, binary };
}

function buildGlb(gltf, binary) {
  const json = padded(Buffer.from(JSON.stringify(gltf)), 0x20);
  const bin = padded(binary, 0);
  const totalLength = 12 + 8 + json.length + 8 + bin.length;
  const output = Buffer.alloc(totalLength);
  output.write('glTF', 0, 4, 'ascii');
  output.writeUInt32LE(2, 4);
  output.writeUInt32LE(totalLength, 8);
  output.writeUInt32LE(json.length, 12);
  output.writeUInt32LE(JSON_CHUNK, 16);
  json.copy(output, 20);
  const binHeader = 20 + json.length;
  output.writeUInt32LE(bin.length, binHeader);
  output.writeUInt32LE(BIN_CHUNK, binHeader + 4);
  bin.copy(output, binHeader + 8);
  return output;
}

function embeddedImages(glb) {
  return (glb.document.images ?? []).map((image, imageIndex) => {
    const view = glb.document.bufferViews?.[image.bufferView];
    if (!view) throw new Error(`Image ${imageIndex} is not embedded.`);
    return {
      imageIndex,
      mimeType: image.mimeType,
      bytes: glb.binary.subarray(
        view.byteOffset ?? 0,
        (view.byteOffset ?? 0) + view.byteLength,
      ),
    };
  });
}

function textureSlots(document) {
  const result = new Map();
  const add = (textureInfo, slot) => {
    const texture = document.textures?.[textureInfo?.index];
    const imageIndex = texture?.extensions?.EXT_texture_webp?.source ?? texture?.source;
    if (imageIndex === undefined) throw new Error(`Missing image for ${slot}.`);
    result.set(slot, imageIndex);
  };
  const material = document.materials?.[0];
  add(material?.pbrMetallicRoughness?.baseColorTexture, 'baseColorTexture');
  add(material?.pbrMetallicRoughness?.metallicRoughnessTexture, 'metallicRoughnessTexture');
  add(material?.normalTexture, 'normalTexture');
  return result;
}

function imageIndexForSlot(slots, slot) {
  const index = slots.get(slot);
  if (index === undefined) throw new Error(`Railback is missing ${slot}.`);
  return index;
}

async function valueMetrics(encoded) {
  const { data, info } = await sharp(encoded).removeAlpha().raw().toBuffer({ resolveWithObject: true });
  const values = [];
  for (let offset = 0; offset < data.length; offset += info.channels)
    values.push(0.2126 * data[offset] + 0.7152 * data[offset + 1] + 0.0722 * data[offset + 2]);
  values.sort((left, right) => left - right);
  return {
    pixels: values.length,
    lowerQuartileLuminance: round(values[Math.floor((values.length - 1) * 0.25)]),
    medianLuminance: round(values[Math.floor((values.length - 1) * 0.5)]),
    upperQuartileLuminance: round(values[Math.floor((values.length - 1) * 0.75)]),
    meanLuminance: round(values.reduce((sum, value) => sum + value, 0) / values.length),
  };
}

function padded(value, byte) {
  const padding = (4 - (value.length % 4)) % 4;
  return padding ? Buffer.concat([value, Buffer.alloc(padding, byte)]) : value;
}

function clamp8(value) {
  return Math.max(0, Math.min(255, value));
}

function round(value) {
  return Math.round(value * 100) / 100;
}

function sha256(value) {
  return createHash('sha256').update(value).digest('hex');
}
