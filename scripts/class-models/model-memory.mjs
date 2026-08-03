#!/usr/bin/env node

import { createHash } from 'node:crypto';
import { readFileSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const JSON_CHUNK = 0x4e4f534a;
const BIN_CHUNK = 0x004e4942;
const KTX2_IDENTIFIER = Buffer.from([
  0xab, 0x4b, 0x54, 0x58, 0x20, 0x32, 0x30, 0xbb,
  0x0d, 0x0a, 0x1a, 0x0a,
]);
const KTX2_COLOR_MODEL_ETC1S = 163;
const KTX2_COLOR_MODEL_UASTC = 166;

export function readGlb(bytes, label = 'GLB') {
  if (
    bytes.toString('ascii', 0, 4) !== 'glTF' ||
    bytes.readUInt32LE(4) !== 2 ||
    bytes.readUInt32LE(8) !== bytes.length
  )
    throw new Error(`${label}: invalid glTF 2.0 binary header.`);
  let offset = 12;
  let document = null;
  let binary = null;
  while (offset < bytes.length) {
    const length = bytes.readUInt32LE(offset);
    const type = bytes.readUInt32LE(offset + 4);
    const data = bytes.subarray(offset + 8, offset + 8 + length);
    if (type === JSON_CHUNK) document = JSON.parse(data.toString('utf8').trim());
    if (type === BIN_CHUNK) binary = data;
    offset += 8 + length;
  }
  if (!document || !binary) throw new Error(`${label}: GLB requires JSON and BIN chunks.`);
  return { document, binary };
}

export function inspectModelMemory(bytes, label = 'GLB') {
  const { document, binary } = readGlb(bytes, label);
  const imageSlots = textureImageSlots(document);
  const textures = (document.images ?? []).map((image, imageIndex) => {
    const view = document.bufferViews?.[image.bufferView];
    if (!view) throw new Error(`${label}: image ${imageIndex} is not embedded in the GLB.`);
    const encoded = binary.subarray(
      view.byteOffset ?? 0,
      (view.byteOffset ?? 0) + view.byteLength,
    );
    const slots = [...(imageSlots.get(imageIndex) ?? [])].sort();
    if (image.mimeType === 'image/ktx2') {
      const ktx = inspectKtx2(encoded, `${label}: image ${imageIndex}`);
      return {
        imageIndex,
        name: image.name ?? null,
        slots,
        mimeType: image.mimeType,
        encoding: ktx.encoding,
        width: ktx.width,
        height: ktx.height,
        mipLevels: ktx.mipLevels,
        transferBytes: encoded.length,
        gpuBytesCompressedTarget: ktx.gpuBytesCompressedTarget,
        gpuBytesRgba8Fallback: ktx.gpuBytesRgba8Fallback,
      };
    }
    if (image.mimeType !== 'image/webp')
      throw new Error(`${label}: unsupported runtime texture ${image.mimeType ?? 'unknown'}.`);
    const dimensions = webpDimensions(encoded, `${label}: image ${imageIndex}`);
    const mipLevels = completeMipCount(dimensions.width, dimensions.height);
    const gpuBytes = rgba8MipBytes(dimensions.width, dimensions.height, mipLevels);
    return {
      imageIndex,
      name: image.name ?? null,
      slots,
      mimeType: image.mimeType,
      encoding: 'RGBA8',
      width: dimensions.width,
      height: dimensions.height,
      mipLevels,
      transferBytes: encoded.length,
      gpuBytesCompressedTarget: gpuBytes,
      gpuBytesRgba8Fallback: gpuBytes,
    };
  });
  const geometryGpuBytes = geometryBufferBytes(document);
  return {
    transferBytes: bytes.length,
    geometryGpuBytes,
    textureGpuBytesCompressedTarget: textures.reduce(
      (sum, texture) => sum + texture.gpuBytesCompressedTarget,
      0,
    ),
    textureGpuBytesRgba8Fallback: textures.reduce(
      (sum, texture) => sum + texture.gpuBytesRgba8Fallback,
      0,
    ),
    modelGpuBytesCompressedTarget:
      geometryGpuBytes +
      textures.reduce((sum, texture) => sum + texture.gpuBytesCompressedTarget, 0),
    modelGpuBytesRgba8Fallback:
      geometryGpuBytes +
      textures.reduce((sum, texture) => sum + texture.gpuBytesRgba8Fallback, 0),
    textures,
  };
}

export function triangleCount(document) {
  return (document.meshes ?? []).flatMap((mesh) => mesh.primitives ?? []).reduce(
    (sum, primitive) => {
      if ((primitive.mode ?? 4) !== 4 || primitive.indices === undefined)
        throw new Error('Model memory audit requires indexed triangle primitives.');
      return sum + (document.accessors?.[primitive.indices]?.count ?? 0) / 3;
    },
    0,
  );
}

export function geometryFingerprint(bytes, label = 'GLB') {
  const { document, binary } = readGlb(bytes, label);
  const hash = createHash('sha256');
  for (const [meshIndex, mesh] of (document.meshes ?? []).entries())
    for (const [primitiveIndex, primitive] of (mesh.primitives ?? []).entries()) {
      hash.update(`mesh:${meshIndex}/primitive:${primitiveIndex}/mode:${primitive.mode ?? 4}\n`);
      const semantics = [
        ...(primitive.indices === undefined ? [] : [['INDICES', primitive.indices]]),
        ...Object.entries(primitive.attributes ?? {}).sort(([left], [right]) =>
          left.localeCompare(right),
        ),
      ];
      for (const [semantic, accessorIndex] of semantics) {
        const accessor = document.accessors?.[accessorIndex];
        const view = document.bufferViews?.[accessor?.bufferView];
        if (!accessor || !view || accessor.sparse)
          throw new Error(`${label}: geometry fingerprint requires dense buffer-view accessors.`);
        const componentBytes = {
          5120: 1,
          5121: 1,
          5122: 2,
          5123: 2,
          5125: 4,
          5126: 4,
        }[accessor.componentType];
        const components = {
          SCALAR: 1,
          VEC2: 2,
          VEC3: 3,
          VEC4: 4,
          MAT2: 4,
          MAT3: 9,
          MAT4: 16,
        }[accessor.type];
        if (!componentBytes || !components)
          throw new Error(`${label}: unsupported accessor encoding in geometry fingerprint.`);
        const elementBytes = componentBytes * components;
        const stride = view.byteStride ?? elementBytes;
        const start = (view.byteOffset ?? 0) + (accessor.byteOffset ?? 0);
        hash.update(
          `${semantic}:${accessor.count}:${accessor.componentType}:${accessor.type}:` +
            `${Boolean(accessor.normalized)}\n`,
        );
        for (let index = 0; index < accessor.count; index += 1)
          hash.update(binary.subarray(start + index * stride, start + index * stride + elementBytes));
      }
    }
  return hash.digest('hex');
}

function textureImageSlots(document) {
  const imageSlots = new Map();
  const add = (textureInfo, slot) => {
    if (textureInfo?.index === undefined) return;
    const texture = document.textures?.[textureInfo.index];
    const imageIndex =
      texture?.extensions?.KHR_texture_basisu?.source ??
      texture?.extensions?.EXT_texture_webp?.source ??
      texture?.source;
    if (imageIndex === undefined) throw new Error(`Texture slot ${slot} has no image source.`);
    if (!imageSlots.has(imageIndex)) imageSlots.set(imageIndex, new Set());
    imageSlots.get(imageIndex).add(slot);
  };
  for (const material of document.materials ?? []) {
    add(material.pbrMetallicRoughness?.baseColorTexture, 'baseColorTexture');
    add(material.pbrMetallicRoughness?.metallicRoughnessTexture, 'metallicRoughnessTexture');
    add(material.normalTexture, 'normalTexture');
    add(material.occlusionTexture, 'occlusionTexture');
    add(material.emissiveTexture, 'emissiveTexture');
  }
  return imageSlots;
}

function geometryBufferBytes(document) {
  const accessorIndices = new Set();
  for (const mesh of document.meshes ?? [])
    for (const primitive of mesh.primitives ?? []) {
      if (primitive.indices !== undefined) accessorIndices.add(primitive.indices);
      for (const index of Object.values(primitive.attributes ?? {})) accessorIndices.add(index);
      for (const target of primitive.targets ?? [])
        for (const index of Object.values(target)) accessorIndices.add(index);
    }
  const views = new Set();
  for (const index of accessorIndices) {
    const accessor = document.accessors?.[index];
    if (!accessor) throw new Error(`Mesh references missing accessor ${index}.`);
    if (accessor.bufferView !== undefined) views.add(accessor.bufferView);
    if (accessor.sparse?.indices?.bufferView !== undefined)
      views.add(accessor.sparse.indices.bufferView);
    if (accessor.sparse?.values?.bufferView !== undefined)
      views.add(accessor.sparse.values.bufferView);
  }
  return [...views].reduce((sum, index) => {
    const view = document.bufferViews?.[index];
    if (!view) throw new Error(`Accessor references missing buffer view ${index}.`);
    return sum + view.byteLength;
  }, 0);
}

function inspectKtx2(bytes, label) {
  if (bytes.length < 80 || !bytes.subarray(0, 12).equals(KTX2_IDENTIFIER))
    throw new Error(`${label}: invalid KTX2 header.`);
  if (bytes.readUInt32LE(12) !== 0)
    throw new Error(`${label}: expected a Basis Universal KTX2 payload.`);
  const width = bytes.readUInt32LE(20);
  const height = bytes.readUInt32LE(24);
  const mipLevels = Math.max(1, bytes.readUInt32LE(40));
  const dfdOffset = bytes.readUInt32LE(48);
  const colorModel = bytes.readUInt8(dfdOffset + 12);
  const encoding = colorModel === KTX2_COLOR_MODEL_ETC1S
    ? 'ETC1S'
    : colorModel === KTX2_COLOR_MODEL_UASTC
      ? 'UASTC'
      : null;
  if (!encoding) throw new Error(`${label}: unsupported KTX2 color model ${colorModel}.`);
  return {
    encoding,
    width,
    height,
    mipLevels,
    // Three's target preference is ETC1/ETC2 RGB (4 bpp) for this fleet's opaque ETC1S
    // maps, and ASTC/BC7/ETC2 RGBA (8 bpp) for UASTC. Both are 4x4 block formats.
    gpuBytesCompressedTarget: blockMipBytes(
      width,
      height,
      mipLevels,
      encoding === 'ETC1S' ? 8 : 16,
    ),
    gpuBytesRgba8Fallback: rgba8MipBytes(width, height, mipLevels),
  };
}

function webpDimensions(bytes, label) {
  if (bytes.toString('ascii', 0, 4) !== 'RIFF' || bytes.toString('ascii', 8, 12) !== 'WEBP')
    throw new Error(`${label}: invalid WebP header.`);
  let offset = 12;
  while (offset + 8 <= bytes.length) {
    const type = bytes.toString('ascii', offset, offset + 4);
    const length = bytes.readUInt32LE(offset + 4);
    const payload = offset + 8;
    if (type === 'VP8X' && length >= 10)
      return {
        width: 1 + bytes.readUIntLE(payload + 4, 3),
        height: 1 + bytes.readUIntLE(payload + 7, 3),
      };
    if (type === 'VP8L' && length >= 5 && bytes[payload] === 0x2f) {
      const bits = bytes.readUInt32LE(payload + 1);
      return {
        width: 1 + (bits & 0x3fff),
        height: 1 + ((bits >>> 14) & 0x3fff),
      };
    }
    if (type === 'VP8 ' && length >= 10) {
      for (let index = payload; index + 7 < payload + length; index += 1)
        if (bytes[index] === 0x9d && bytes[index + 1] === 0x01 && bytes[index + 2] === 0x2a)
          return {
            width: bytes.readUInt16LE(index + 3) & 0x3fff,
            height: bytes.readUInt16LE(index + 5) & 0x3fff,
          };
    }
    offset = payload + length + (length % 2);
  }
  throw new Error(`${label}: could not read WebP dimensions.`);
}

function completeMipCount(width, height) {
  return Math.floor(Math.log2(Math.max(width, height))) + 1;
}

function rgba8MipBytes(width, height, levels) {
  let total = 0;
  for (let level = 0; level < levels; level += 1)
    total += Math.max(1, width >> level) * Math.max(1, height >> level) * 4;
  return total;
}

function blockMipBytes(width, height, levels, blockBytes) {
  let total = 0;
  for (let level = 0; level < levels; level += 1)
    total +=
      Math.ceil(Math.max(1, width >> level) / 4) *
      Math.ceil(Math.max(1, height >> level) / 4) *
      blockBytes;
  return total;
}

if (path.resolve(process.argv[1] ?? '') === fileURLToPath(import.meta.url)) {
  const files = process.argv.slice(2);
  if (files.length === 0)
    throw new Error('Usage: node scripts/class-models/model-memory.mjs <model.glb> [...]');
  for (const file of files) {
    const absolute = path.resolve(file);
    console.log(
      JSON.stringify(
        { file, ...inspectModelMemory(readFileSync(absolute), file) },
        null,
        2,
      ),
    );
  }
}
