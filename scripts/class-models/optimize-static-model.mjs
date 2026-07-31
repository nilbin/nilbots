#!/usr/bin/env node

/**
 * Build deterministic mobile-oriented runtime tiers from one normalized static GLB.
 *
 * The input is already the approved, +X-facing, +Y-up, floor-at-zero normalized
 * master. This script never repairs silhouette or presentation identity. It uses the
 * pinned native gltfpack release to simplify, quantize, generate tangents, resize and
 * encode PBR textures, and preserve named nodes/materials plus Nilbots semantic extras.
 *
 * Example:
 *
 *   node scripts/class-models/optimize-static-model.mjs \
 *     --source sandbox/normalized/model.glb \
 *     --look-id trident-wasp \
 *     --output-dir sandbox/static-model-tiers/trident-wasp \
 *     --gltfpack sandbox/model-tools/native/gltfpack
 */

import { execFileSync, spawnSync } from 'node:child_process';
import { createHash } from 'node:crypto';
import {
  chmod,
  mkdir,
  readFile,
  rename,
  rm,
  stat,
  writeFile,
} from 'node:fs/promises';
import { delimiter, dirname, isAbsolute, join, relative, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const scriptDirectory = dirname(fileURLToPath(import.meta.url));
const repository = resolve(scriptDirectory, '..', '..');
const options = parseArguments(process.argv.slice(2));
const MAXIMUM_RUNTIME_BOUND_DRIFT = 0.003;

const DEFAULT_TIERS = [
  {
    id: 'quality-reference',
    triangles: 120_000,
    textureLimit: 2_048,
    simplifyError: 0.015,
    textureQuality: { color: 9, normal: 9, attrib: 8 },
  },
  {
    id: 'mobile',
    triangles: 60_000,
    textureLimit: 1_024,
    simplifyError: 0.02,
    textureQuality: { color: 8, normal: 9, attrib: 8 },
  },
  {
    id: 'mobile-lean',
    triangles: 30_000,
    textureLimit: 1_024,
    simplifyError: 0.035,
    textureQuality: { color: 8, normal: 9, attrib: 8 },
  },
  {
    id: 'mobile-lite',
    triangles: 15_000,
    textureLimit: 512,
    simplifyError: 0.05,
    textureQuality: { color: 8, normal: 9, attrib: 8 },
  },
];

const sourcePath = resolveRequiredPath(options.source, '--source');
const outputDirectory = resolveRequiredPath(options.outputDir, '--output-dir');
const lookId = requiredText(options.lookId, '--look-id');
const tiers = options.tiers.length > 0 ? options.tiers : DEFAULT_TIERS;
const gltfpackPath = await locateGltfpack(options.gltfpack);

await mkdir(outputDirectory, { recursive: true });
const [sourceBytes, toolBytes] = await Promise.all([
  readFile(sourcePath),
  readFile(gltfpackPath),
]);
const sourceAudit = auditGlb(sourceBytes);
assertStaticSource(sourceAudit);
const sourceTriangles = sourceAudit.geometry.sceneTriangles;
if (sourceTriangles <= 0) throw new Error('Source GLB contains no rendered triangles.');

const toolVersion = execFileSync(gltfpackPath, ['-v'], {
  encoding: 'utf8',
}).trim();
if (toolVersion !== 'gltfpack 1.2') {
  throw new Error(
    `Expected pinned native gltfpack 1.2, received ${JSON.stringify(toolVersion)}.`,
  );
}

const report = {
  schemaVersion: 1,
  lookId,
  purpose: 'Static on-demand WebGL model runtime tier comparison',
  source: {
    file: displayPath(sourcePath),
    bytes: sourceBytes.length,
    sha256: sha256(sourceBytes),
    audit: sourceAudit,
  },
  tool: {
    name: 'gltfpack',
    version: toolVersion,
    executable: displayPath(gltfpackPath),
    sha256: sha256(toolBytes),
    nativeTextureEncodingRequired: true,
  },
  runtimeContract: {
    sourceAlreadyNormalized: true,
    coordinateSystem: '+X facing, +Y up, floor at Y=0',
    animations: 'forbidden',
    skins: 'forbidden',
    cameras: 'forbidden',
    lights: 'forbidden',
    geometryCompressionExtension: 'disabled until the loader owns a decoder',
    textureEncoding: 'embedded WebP; browser-decoded RGBA with mipmaps',
    semanticExtras: 'preserved with -ke; named materials preserved with -km',
    maximumWorldBoundDrift: MAXIMUM_RUNTIME_BOUND_DRIFT,
  },
  tiers: [],
};

for (const tier of tiers) {
  const outputPath = join(outputDirectory, `${lookId}-${tier.id}.glb`);
  const toolReportPath = join(outputDirectory, `${lookId}-${tier.id}.gltfpack.json`);
  const temporaryPath = join(outputDirectory, `.${lookId}-${tier.id}.first.glb`);
  const temporaryReportPath = join(
    outputDirectory,
    `.${lookId}-${tier.id}.first.gltfpack.json`,
  );
  const ratio = Math.min(1, tier.triangles / sourceTriangles);
  const args = gltfpackArguments({
    sourcePath,
    outputPath: temporaryPath,
    toolReportPath: temporaryReportPath,
    ratio,
    tier,
  });

  process.stderr.write(
    `[${lookId}] ${tier.id}: ${tier.triangles.toLocaleString()} tris / ${tier.textureLimit}px\n`,
  );
  const firstLog = runGltfpack(gltfpackPath, args);
  const firstBytes = await readFile(temporaryPath);
  const firstHash = sha256(firstBytes);

  let deterministicHash = firstHash;
  let deterministic = null;
  if (!options.skipDeterminism) {
    const secondArgs = gltfpackArguments({
      sourcePath,
      outputPath,
      toolReportPath,
      ratio,
      tier,
    });
    const secondLog = runGltfpack(gltfpackPath, secondArgs);
    const secondBytes = await readFile(outputPath);
    deterministicHash = sha256(secondBytes);
    deterministic = {
      passed: firstHash === deterministicHash,
      firstSha256: firstHash,
      secondSha256: deterministicHash,
    };
    if (!deterministic.passed) {
      throw new Error(
        `${tier.id} is not deterministic: ${firstHash} != ${deterministicHash}.`,
      );
    }
    await rm(temporaryPath, { force: true });
    await rm(temporaryReportPath, { force: true });
    if (firstLog !== secondLog) {
      deterministic.logMatch = false;
    } else {
      deterministic.logMatch = true;
    }
  } else {
    await rename(temporaryPath, outputPath);
    await rename(temporaryReportPath, toolReportPath);
  }

  const [outputBytes, toolReportBytes] = await Promise.all([
    readFile(outputPath),
    readFile(toolReportPath),
  ]);
  const outputAudit = auditGlb(outputBytes);
  const toolReport = JSON.parse(toolReportBytes);
  assertRuntimeDerivative(sourceAudit, outputAudit, tier);
  const boundDrift = compareBounds(sourceAudit.worldBounds, outputAudit.worldBounds);
  if (boundDrift.maximumAbsoluteDelta > MAXIMUM_RUNTIME_BOUND_DRIFT) {
    throw new Error(
      `${tier.id} changed normalized world bounds by ${boundDrift.maximumAbsoluteDelta}.`,
    );
  }

  report.tiers.push({
    ...tier,
    targetRatio: ratio,
    file: relative(outputDirectory, outputPath),
    bytes: outputBytes.length,
    sha256: deterministicHash,
    transferReductionFraction: 1 - outputBytes.length / sourceBytes.length,
    triangleReductionFraction:
      1 - outputAudit.geometry.sceneTriangles / sourceTriangles,
    boundDrift,
    audit: outputAudit,
    toolReport,
    deterministic,
    command: [
      displayPath(gltfpackPath),
      ...gltfpackArguments({
        sourcePath,
        outputPath,
        toolReportPath,
        ratio,
        tier,
      }).map(displayArgument),
    ],
  });
}

const reportPath = join(outputDirectory, 'build-report.json');
await writeFile(reportPath, `${JSON.stringify(report, null, 2)}\n`);
process.stdout.write(
  `${JSON.stringify(
    {
      report: displayPath(reportPath),
      tiers: report.tiers.map((tier) => ({
        id: tier.id,
        file: tier.file,
        bytes: tier.bytes,
        sha256: tier.sha256,
        triangles: tier.audit.geometry.sceneTriangles,
        vertices: tier.audit.geometry.vertices,
        textureMax: tier.audit.textures.maximumDimension,
        textureTransferBytes: tier.audit.textures.transferBytes,
        decodedRgbaMipBytes: tier.audit.textures.decodedRgbaMipBytes,
        deterministic: tier.deterministic?.passed ?? null,
      })),
    },
    null,
    2,
  )}\n`,
);

function parseArguments(args) {
  const parsed = {
    source: null,
    lookId: null,
    outputDir: null,
    gltfpack: null,
    tiers: [],
    skipDeterminism: false,
  };
  for (let index = 0; index < args.length; index += 1) {
    const value = args[index];
    if (value === '--source') parsed.source = args[++index];
    else if (value === '--look-id') parsed.lookId = args[++index];
    else if (value === '--output-dir') parsed.outputDir = args[++index];
    else if (value === '--gltfpack') parsed.gltfpack = args[++index];
    else if (value === '--skip-determinism') parsed.skipDeterminism = true;
    else if (value === '--tier') parsed.tiers.push(parseTier(args[++index]));
    else throw new Error(`Unknown argument: ${value}`);
  }
  return parsed;
}

function parseTier(value) {
  const [id, triangleText, textureText, errorText = '0.03'] = String(value).split(':');
  const triangles = Number(triangleText);
  const textureLimit = Number(textureText);
  const simplifyError = Number(errorText);
  if (!/^[a-z0-9][a-z0-9-]*$/.test(id ?? ''))
    throw new Error(`Invalid tier id: ${id}`);
  if (!Number.isInteger(triangles) || triangles < 1_000)
    throw new Error(`Invalid triangle target in --tier ${value}.`);
  if (!Number.isInteger(textureLimit) || textureLimit < 128)
    throw new Error(`Invalid texture limit in --tier ${value}.`);
  if (!(simplifyError > 0 && simplifyError <= 1))
    throw new Error(`Invalid simplify error in --tier ${value}.`);
  return {
    id,
    triangles,
    textureLimit,
    simplifyError,
    textureQuality: { color: 8, normal: 9, attrib: 8 },
  };
}

function gltfpackArguments({ sourcePath, outputPath, toolReportPath, ratio, tier }) {
  return [
    '-i',
    sourcePath,
    '-o',
    outputPath,
    '-si',
    ratio.toFixed(9),
    '-se',
    String(tier.simplifyError),
    '-sp',
    '-tw',
    '-tl',
    String(tier.textureLimit),
    '-tq',
    'color',
    String(tier.textureQuality.color),
    '-tq',
    'normal',
    String(tier.textureQuality.normal),
    '-tq',
    'attrib',
    String(tier.textureQuality.attrib),
    '-tj',
    '1',
    '-gt',
    '-kn',
    '-km',
    '-ke',
    '-r',
    toolReportPath,
    '-v',
  ];
}

function runGltfpack(executable, args) {
  const result = spawnSync(executable, args, {
    encoding: 'utf8',
    maxBuffer: 20 * 1024 * 1024,
  });
  const log = `${result.stdout ?? ''}${result.stderr ?? ''}`;
  if (result.status !== 0) {
    throw new Error(`gltfpack failed (${result.status}):\n${log}`);
  }
  return log.trim();
}

async function locateGltfpack(explicit) {
  const candidates = [];
  if (explicit) candidates.push(resolve(explicit));
  if (process.env.NILBOTS_GLTFPACK)
    candidates.push(resolve(process.env.NILBOTS_GLTFPACK));
  candidates.push(resolve(repository, 'sandbox/model-tools/native/gltfpack'));
  for (const folder of (process.env.PATH ?? '').split(delimiter)) {
    if (folder) candidates.push(join(folder, 'gltfpack'));
  }
  for (const candidate of candidates) {
    try {
      const metadata = await stat(candidate);
      if (!metadata.isFile()) continue;
      await chmod(candidate, metadata.mode | 0o100);
      return candidate;
    } catch {
      // Continue through deterministic lookup candidates.
    }
  }
  throw new Error(
    'Native gltfpack 1.2 was not found. Pass --gltfpack or set NILBOTS_GLTFPACK. ' +
      'The npm/WASM build cannot encode the required WebP textures.',
  );
}

function assertStaticSource(audit) {
  if (audit.scene.animations !== 0) throw new Error('Static source contains animations.');
  if (audit.scene.skins !== 0) throw new Error('Static source contains skins.');
  if (audit.scene.cameras !== 0) throw new Error('Static source contains cameras.');
  if (audit.scene.lights !== 0) throw new Error('Static source contains lights.');
  if (Math.abs(audit.worldBounds.minimum[1]) > 0.001)
    throw new Error(`Source floor is not Y=0: ${audit.worldBounds.minimum[1]}.`);
}

function assertRuntimeDerivative(source, output, tier) {
  assertStaticSource(output);
  if (output.scene.materials !== source.scene.materials) {
    throw new Error(
      `${tier.id} changed material count (${source.scene.materials} -> ${output.scene.materials}).`,
    );
  }
  if (JSON.stringify(output.semanticRoles) !== JSON.stringify(source.semanticRoles)) {
    throw new Error(`${tier.id} did not preserve semantic Nilbots roles.`);
  }
  if (output.geometry.sceneTriangles > tier.triangles + 8) {
    throw new Error(
      `${tier.id} exceeded its triangle target: ${output.geometry.sceneTriangles} > ${tier.triangles}.`,
    );
  }
  if (output.textures.maximumDimension > tier.textureLimit) {
    throw new Error(
      `${tier.id} exceeded its texture limit: ${output.textures.maximumDimension} > ${tier.textureLimit}.`,
    );
  }
  if (output.extensions.required.includes('EXT_meshopt_compression')) {
    throw new Error(`${tier.id} unexpectedly requires a meshopt runtime decoder.`);
  }
  const nonWebp = output.textures.images.filter((image) => image.mimeType !== 'image/webp');
  if (nonWebp.length > 0) throw new Error(`${tier.id} contains non-WebP runtime textures.`);
}

function auditGlb(bytes) {
  const glb = parseGlb(bytes);
  const document = glb.json;
  const imageViews = new Set(
    (document.images ?? [])
      .map((image) => image.bufferView)
      .filter((index) => Number.isInteger(index)),
  );
  const images = (document.images ?? []).map((image, index) => {
    const payload = imagePayload(glb, image);
    const dimensions = imageDimensions(payload, image.mimeType);
    return {
      index,
      name: image.name ?? null,
      mimeType: image.mimeType ?? inferImageMime(payload),
      bytes: payload.length,
      width: dimensions.width,
      height: dimensions.height,
      decodedRgbaBytes: dimensions.width * dimensions.height * 4,
      decodedRgbaMipBytes: Math.ceil(
        dimensions.width * dimensions.height * 4 * (4 / 3),
      ),
    };
  });
  const decodedRgbaBytes = images.reduce((sum, image) => sum + image.decodedRgbaBytes, 0);
  const decodedRgbaMipBytes = images.reduce(
    (sum, image) => sum + image.decodedRgbaMipBytes,
    0,
  );
  const bufferViews = document.bufferViews ?? [];
  const geometryBufferBytes = bufferViews.reduce(
    (sum, view, index) => sum + (imageViews.has(index) ? 0 : view.byteLength ?? 0),
    0,
  );
  const semanticRoles = {
    materials: (document.materials ?? [])
      .map((material, index) => ({
        index,
        name: material.name ?? null,
        role: material.extras?.nilbotsRole ?? null,
      }))
      .filter(({ role }) => role !== null),
    primitives: (document.meshes ?? []).flatMap((mesh, meshIndex) =>
      (mesh.primitives ?? []).flatMap((primitive, primitiveIndex) =>
        primitive.extras?.nilbotsRole
          ? [{ meshIndex, primitiveIndex, role: primitive.extras.nilbotsRole }]
          : [],
      ),
    ),
  };

  return {
    scene: {
      nodes: document.nodes?.length ?? 0,
      meshes: document.meshes?.length ?? 0,
      primitives: (document.meshes ?? []).reduce(
        (sum, mesh) => sum + (mesh.primitives?.length ?? 0),
        0,
      ),
      materials: document.materials?.length ?? 0,
      textures: document.textures?.length ?? 0,
      images: images.length,
      animations: document.animations?.length ?? 0,
      skins: document.skins?.length ?? 0,
      cameras: document.cameras?.length ?? 0,
      lights: document.extensions?.KHR_lights_punctual?.lights?.length ?? 0,
    },
    geometry: geometryMetrics(document),
    geometryBufferBytes,
    worldBounds: worldBounds(document),
    textures: {
      transferBytes: images.reduce((sum, image) => sum + image.bytes, 0),
      decodedRgbaBytes,
      decodedRgbaMipBytes,
      maximumDimension: Math.max(0, ...images.map((image) => Math.max(image.width, image.height))),
      images,
    },
    semanticRoles,
    extensions: {
      used: [...(document.extensionsUsed ?? [])].sort(),
      required: [...(document.extensionsRequired ?? [])].sort(),
    },
  };
}

function geometryMetrics(document) {
  let uniqueTriangles = 0;
  let vertices = 0;
  const meshTriangles = [];
  for (const mesh of document.meshes ?? []) {
    let triangles = 0;
    for (const primitive of mesh.primitives ?? []) {
      const mode = primitive.mode ?? 4;
      const count = Number.isInteger(primitive.indices)
        ? document.accessors[primitive.indices].count
        : document.accessors[primitive.attributes.POSITION].count;
      if (mode === 4) triangles += Math.floor(count / 3);
      else if (mode === 5 || mode === 6) triangles += Math.max(0, count - 2);
      else throw new Error(`Unsupported primitive mode ${mode}.`);
      vertices += document.accessors[primitive.attributes.POSITION].count;
    }
    uniqueTriangles += triangles;
    meshTriangles.push(triangles);
  }
  let sceneTriangles = 0;
  traverseScene(document, (_node, node) => {
    if (Number.isInteger(node.mesh)) sceneTriangles += meshTriangles[node.mesh] ?? 0;
  });
  return { uniqueTriangles, sceneTriangles, vertices };
}

function worldBounds(document) {
  const minimum = [Infinity, Infinity, Infinity];
  const maximum = [-Infinity, -Infinity, -Infinity];
  traverseScene(document, (matrix, node) => {
    if (!Number.isInteger(node.mesh)) return;
    const mesh = document.meshes[node.mesh];
    for (const primitive of mesh.primitives ?? []) {
      const accessor = document.accessors[primitive.attributes.POSITION];
      if (!accessor?.min || !accessor?.max)
        throw new Error('POSITION accessor is missing min/max bounds.');
      for (const x of [accessor.min[0], accessor.max[0]]) {
        for (const y of [accessor.min[1], accessor.max[1]]) {
          for (const z of [accessor.min[2], accessor.max[2]]) {
            const point = transformPoint(matrix, [x, y, z]);
            for (let axis = 0; axis < 3; axis += 1) {
              minimum[axis] = Math.min(minimum[axis], point[axis]);
              maximum[axis] = Math.max(maximum[axis], point[axis]);
            }
          }
        }
      }
    }
  });
  if (!minimum.every(Number.isFinite)) throw new Error('GLB has no bounded mesh instances.');
  return {
    minimum,
    maximum,
    size: maximum.map((value, axis) => value - minimum[axis]),
    planformSpan: Math.max(maximum[0] - minimum[0], maximum[2] - minimum[2]),
  };
}

function traverseScene(document, visit) {
  const nodes = document.nodes ?? [];
  const scene = document.scenes?.[document.scene ?? 0];
  if (!scene) throw new Error('GLB has no default scene.');
  const walk = (nodeIndex, parentMatrix) => {
    const node = nodes[nodeIndex];
    if (!node) throw new Error(`Missing node ${nodeIndex}.`);
    const matrix = multiplyMatrices(parentMatrix, nodeMatrix(node));
    visit(matrix, node);
    for (const child of node.children ?? []) walk(child, matrix);
  };
  for (const node of scene.nodes ?? []) walk(node, identityMatrix());
}

function nodeMatrix(node) {
  if (node.matrix) return [...node.matrix];
  const [x, y, z, w] = node.rotation ?? [0, 0, 0, 1];
  const [sx, sy, sz] = node.scale ?? [1, 1, 1];
  const [tx, ty, tz] = node.translation ?? [0, 0, 0];
  const xx = x * x;
  const xy = x * y;
  const xz = x * z;
  const xw = x * w;
  const yy = y * y;
  const yz = y * z;
  const yw = y * w;
  const zz = z * z;
  const zw = z * w;
  return [
    (1 - 2 * (yy + zz)) * sx,
    (2 * (xy + zw)) * sx,
    (2 * (xz - yw)) * sx,
    0,
    (2 * (xy - zw)) * sy,
    (1 - 2 * (xx + zz)) * sy,
    (2 * (yz + xw)) * sy,
    0,
    (2 * (xz + yw)) * sz,
    (2 * (yz - xw)) * sz,
    (1 - 2 * (xx + yy)) * sz,
    0,
    tx,
    ty,
    tz,
    1,
  ];
}

function identityMatrix() {
  return [1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1];
}

function multiplyMatrices(left, right) {
  const result = new Array(16).fill(0);
  for (let column = 0; column < 4; column += 1) {
    for (let row = 0; row < 4; row += 1) {
      for (let inner = 0; inner < 4; inner += 1) {
        result[column * 4 + row] +=
          left[inner * 4 + row] * right[column * 4 + inner];
      }
    }
  }
  return result;
}

function transformPoint(matrix, [x, y, z]) {
  const w = matrix[3] * x + matrix[7] * y + matrix[11] * z + matrix[15];
  return [
    (matrix[0] * x + matrix[4] * y + matrix[8] * z + matrix[12]) / w,
    (matrix[1] * x + matrix[5] * y + matrix[9] * z + matrix[13]) / w,
    (matrix[2] * x + matrix[6] * y + matrix[10] * z + matrix[14]) / w,
  ];
}

function compareBounds(source, output) {
  const deltas = {
    minimum: output.minimum.map((value, axis) => value - source.minimum[axis]),
    maximum: output.maximum.map((value, axis) => value - source.maximum[axis]),
    size: output.size.map((value, axis) => value - source.size[axis]),
    planformSpan: output.planformSpan - source.planformSpan,
  };
  const maximumAbsoluteDelta = Math.max(
    ...deltas.minimum.map(Math.abs),
    ...deltas.maximum.map(Math.abs),
    ...deltas.size.map(Math.abs),
    Math.abs(deltas.planformSpan),
  );
  return { maximumAbsoluteDelta, ...deltas };
}

function parseGlb(bytes) {
  if (bytes.subarray(0, 4).toString('ascii') !== 'glTF')
    throw new Error('Expected a binary glTF file.');
  if (bytes.readUInt32LE(4) !== 2) throw new Error('Expected glTF 2.0.');
  if (bytes.readUInt32LE(8) !== bytes.length) throw new Error('GLB length mismatch.');
  let offset = 12;
  let json = null;
  let binary = null;
  while (offset < bytes.length) {
    const length = bytes.readUInt32LE(offset);
    const type = bytes.readUInt32LE(offset + 4);
    const payload = bytes.subarray(offset + 8, offset + 8 + length);
    if (type === 0x4e4f534a)
      json = JSON.parse(payload.toString('utf8').replace(/[\u0000 ]+$/u, ''));
    else if (type === 0x004e4942) binary = payload;
    offset += 8 + length;
  }
  if (!json || !binary) throw new Error('GLB must contain JSON and BIN chunks.');
  return { json, binary };
}

function imagePayload(glb, image) {
  if (!Number.isInteger(image.bufferView))
    throw new Error('Runtime optimizer requires embedded image bufferViews.');
  const view = glb.json.bufferViews[image.bufferView];
  return glb.binary.subarray(
    view.byteOffset ?? 0,
    (view.byteOffset ?? 0) + view.byteLength,
  );
}

function imageDimensions(bytes, mimeType) {
  const mime = mimeType ?? inferImageMime(bytes);
  if (mime === 'image/png') {
    return { width: bytes.readUInt32BE(16), height: bytes.readUInt32BE(20) };
  }
  if (mime === 'image/jpeg') return jpegDimensions(bytes);
  if (mime === 'image/webp') return webpDimensions(bytes);
  throw new Error(`Unsupported embedded image MIME type: ${mime}.`);
}

function inferImageMime(bytes) {
  if (bytes.subarray(0, 8).equals(Buffer.from('89504e470d0a1a0a', 'hex')))
    return 'image/png';
  if (bytes[0] === 0xff && bytes[1] === 0xd8) return 'image/jpeg';
  if (bytes.subarray(0, 4).toString('ascii') === 'RIFF' && bytes.subarray(8, 12).toString('ascii') === 'WEBP')
    return 'image/webp';
  return 'application/octet-stream';
}

function jpegDimensions(bytes) {
  let offset = 2;
  while (offset + 9 < bytes.length) {
    if (bytes[offset] !== 0xff) {
      offset += 1;
      continue;
    }
    const marker = bytes[offset + 1];
    offset += 2;
    if (marker === 0xd8 || marker === 0xd9) continue;
    const length = bytes.readUInt16BE(offset);
    if (marker >= 0xc0 && marker <= 0xc3) {
      return { width: bytes.readUInt16BE(offset + 5), height: bytes.readUInt16BE(offset + 3) };
    }
    offset += length;
  }
  throw new Error('Could not read JPEG dimensions.');
}

function webpDimensions(bytes) {
  const kind = bytes.subarray(12, 16).toString('ascii');
  if (kind === 'VP8X') {
    return {
      width: 1 + bytes.readUIntLE(24, 3),
      height: 1 + bytes.readUIntLE(27, 3),
    };
  }
  if (kind === 'VP8 ') {
    return {
      width: bytes.readUInt16LE(26) & 0x3fff,
      height: bytes.readUInt16LE(28) & 0x3fff,
    };
  }
  if (kind === 'VP8L') {
    const bits = bytes.readUInt32LE(21);
    return { width: 1 + (bits & 0x3fff), height: 1 + ((bits >> 14) & 0x3fff) };
  }
  throw new Error(`Unsupported WebP chunk ${JSON.stringify(kind)}.`);
}

function sha256(bytes) {
  return createHash('sha256').update(bytes).digest('hex');
}

function resolveRequiredPath(value, flag) {
  if (!value) throw new Error(`${flag} is required.`);
  return resolve(value);
}

function requiredText(value, flag) {
  if (!value) throw new Error(`${flag} is required.`);
  if (!/^[a-z0-9][a-z0-9-]*$/.test(value)) throw new Error(`${flag} is invalid.`);
  return value;
}

function displayPath(path) {
  if (!isAbsolute(path)) return path;
  const local = relative(repository, path);
  return local.startsWith('..') ? path : local;
}

function displayArgument(value) {
  return isAbsolute(value) ? displayPath(value) : value;
}
