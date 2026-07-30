import { createHash } from 'node:crypto';
import { mkdir, readFile, writeFile } from 'node:fs/promises';
import { createRequire } from 'node:module';
import { basename, resolve, sep } from 'node:path';

const require = createRequire(import.meta.url);
const { createCanvas, loadImage } = require('../../web/node_modules/@napi-rs/canvas');

const repositoryRoot = resolve(import.meta.dirname, '../..');
const [recipeArgument, outputArgument] = process.argv.slice(2);
if (!recipeArgument || !outputArgument) {
  throw new Error(
    'Usage: node scripts/class-models/extract-team-accent.mjs <recipe.json> <output-directory>',
  );
}

const recipePath = resolve(recipeArgument);
const recipeBytes = await readFile(recipePath);
const recipe = JSON.parse(recipeBytes);
validateRecipe(recipe);

const inputPath = repositoryFile(recipe.inputs.model.file, 'normalized model');
const baseColorPath = repositoryFile(
  recipe.inputs.baseColor.file,
  'lossless base-color map',
);
const emissionPath = repositoryFile(
  recipe.inputs.emission.file,
  'lossless emission map',
);
const outputDirectory = resolve(outputArgument);
const outputModelPath = outputFile(outputDirectory, recipe.outputs.model);
const outputMaskPath = outputFile(
  outputDirectory,
  recipe.outputs.losslessMask,
);
const outputBasePath = outputFile(
  outputDirectory,
  recipe.outputs.neutralHullBase,
);
const outputEmissionPath = resolve(
  outputDirectory,
  outputFilename(
    recipe.outputs.neutralHullEmission,
    'neutral hull emission',
  ),
);
const outputReportPath = resolve(
  outputDirectory,
  recipe.reportFilename ?? 'team-accent-report.json',
);

const thresholds = recipe.thresholds;

await mkdir(outputDirectory, { recursive: true });

const sourceBytes = await readFile(inputPath);
assertPinnedArtifact('normalized model', sourceBytes, recipe.inputs.model);
const source = parseGlb(sourceBytes, inputPath);
const sourceDocument = source.document;
const sourcePrimitive = expectSinglePrimitive(sourceDocument, inputPath);
const sourceMaterial =
  sourceDocument.materials?.[sourcePrimitive.material ?? 0] ?? {};

const [baseColorBytes, emissionBytes] = await Promise.all([
  readFile(baseColorPath),
  readFile(emissionPath),
]);
assertPinnedArtifact(
  'lossless base-color map',
  baseColorBytes,
  recipe.inputs.baseColor,
);
assertPinnedArtifact(
  'lossless emission map',
  emissionBytes,
  recipe.inputs.emission,
);
const [baseSource, emissionSource] = await Promise.all([
  loadPixels(baseColorBytes),
  loadPixels(emissionBytes),
]);

if (
  baseSource.width % emissionSource.width !== 0 ||
  baseSource.height % emissionSource.height !== 0
) {
  throw new Error(
    `Expected integer base/emission resolution ratio, got ${baseSource.width}x${baseSource.height} and ${emissionSource.width}x${emissionSource.height}.`,
  );
}

console.log(
  `Deriving ${baseSource.width}x${baseSource.height} lossless team mask from base color + emission...`,
);
const maskResult = deriveTeamMask(baseSource, emissionSource, thresholds);
const maskPng = encodeBinaryMask(
  maskResult.mask,
  baseSource.width,
  baseSource.height,
);
assertPinnedArtifact(
  'generated team-accent mask',
  maskPng,
  recipe.outputs.losslessMask,
);
await writeFile(outputMaskPath, maskPng);

console.log('Classifying lean mesh faces by seven UV samples...');
const sourceIndices = readAccessor(source, sourcePrimitive.indices);
const sourceUvs = readAccessor(
  source,
  sourcePrimitive.attributes?.TEXCOORD_0,
);
const sourcePositions = readAccessor(
  source,
  sourcePrimitive.attributes?.POSITION,
);
const split = splitTriangles({
  indices: sourceIndices.values,
  uvs: sourceUvs.values,
  positions: sourcePositions.values,
  mask: maskResult.mask,
  maskWidth: baseSource.width,
  maskHeight: baseSource.height,
});
if (split.hullIndices.length === 0 || split.accentIndices.length === 0) {
  throw new Error(
    `Team split must leave both primitives non-empty; got ${split.hullIndices.length / 3} hull and ${split.accentIndices.length / 3} accent triangles.`,
  );
}

const outputDocument = structuredClone(sourceDocument);
const viewData = outputDocument.bufferViews.map((view) =>
  Buffer.from(
    source.binary.subarray(
      view.byteOffset ?? 0,
      (view.byteOffset ?? 0) + view.byteLength,
    ),
  ),
);

const outputPrimitive = outputDocument.meshes[0].primitives[0];
const indexAccessorIndex = outputPrimitive.indices;
const indexAccessor = outputDocument.accessors[indexAccessorIndex];
const indexViewIndex = indexAccessor.bufferView;
const indexView = outputDocument.bufferViews[indexViewIndex];
const componentByteSize = componentSize(indexAccessor.componentType);
if (
  (indexAccessor.byteOffset ?? 0) !== 0 ||
  indexView.byteLength < indexAccessor.count * componentByteSize ||
  indexView.byteLength - indexAccessor.count * componentByteSize > 3
) {
  throw new Error(
    'The proof expects the lean model index accessor to own its complete buffer view.',
  );
}

const hullIndexBytes = encodeScalarAccessor(
  split.hullIndices,
  indexAccessor.componentType,
);
const accentIndexBytes = encodeScalarAccessor(
  split.accentIndices,
  indexAccessor.componentType,
);
viewData[indexViewIndex] = hullIndexBytes;
indexView.byteLength = hullIndexBytes.length;
indexAccessor.count = split.hullIndices.length;

const accentIndexViewIndex = outputDocument.bufferViews.length;
outputDocument.bufferViews.push({
  buffer: 0,
  byteLength: accentIndexBytes.length,
  target: 34963,
});
viewData.push(accentIndexBytes);
const accentIndexAccessorIndex = outputDocument.accessors.length;
outputDocument.accessors.push({
  bufferView: accentIndexViewIndex,
  byteOffset: 0,
  componentType: indexAccessor.componentType,
  count: split.accentIndices.length,
  type: 'SCALAR',
});

console.log('Neutralizing fixed cyan from the hull textures...');
const baseImageIndex = textureImageIndex(
  outputDocument,
  sourceMaterial.pbrMetallicRoughness?.baseColorTexture?.index,
  'base color',
);
const emissionImageIndex = textureImageIndex(
  outputDocument,
  sourceMaterial.emissiveTexture?.index,
  'emission',
);
const baseImageView = outputDocument.images[baseImageIndex].bufferView;
const emissionImageView = outputDocument.images[emissionImageIndex].bufferView;
const [runtimeBase, runtimeEmission] = await Promise.all([
  loadPixels(viewData[baseImageView]),
  loadPixels(viewData[emissionImageView]),
]);
const runtimeMaskBase = downsampleMaskAny(
  maskResult.mask,
  baseSource.width,
  baseSource.height,
  runtimeBase.width,
  runtimeBase.height,
);
const runtimeMaskEmission = downsampleMaskAny(
  maskResult.mask,
  baseSource.width,
  baseSource.height,
  runtimeEmission.width,
  runtimeEmission.height,
);
const neutralBase = neutralizeBaseColor(runtimeBase, runtimeMaskBase);
const neutralEmission = neutralizeEmission(
  runtimeEmission,
  runtimeMaskEmission,
);
assertPinnedArtifact(
  'generated neutral hull base',
  neutralBase.png,
  recipe.outputs.neutralHullBase,
);
assertPinnedArtifact(
  'generated neutral hull emission',
  neutralEmission.png,
  recipe.outputs.neutralHullEmission,
);
await Promise.all([
  writeFile(outputBasePath, neutralBase.png),
  writeFile(outputEmissionPath, neutralEmission.png),
]);
viewData[baseImageView] = neutralBase.png;
viewData[emissionImageView] = neutralEmission.png;
outputDocument.images[baseImageIndex].mimeType = 'image/png';
outputDocument.images[emissionImageIndex].mimeType = 'image/png';

const hullMaterialIndex = outputPrimitive.material ?? 0;
const hullMaterial = outputDocument.materials[hullMaterialIndex];
hullMaterial.name = 'Nilbots Hull';
const accentMaterial = makeTeamAccentMaterial(hullMaterial);
const accentMaterialIndex = outputDocument.materials.length;
outputDocument.materials.push(accentMaterial);

const sharedAttributes = structuredClone(outputPrimitive.attributes);
const sharedMode = outputPrimitive.mode ?? 4;
const hullPrimitive = {
  ...outputPrimitive,
  attributes: sharedAttributes,
  indices: indexAccessorIndex,
  material: hullMaterialIndex,
  mode: sharedMode,
  extras: {
    ...(outputPrimitive.extras ?? {}),
    nilbotsRole: 'hull',
  },
};
const accentPrimitive = {
  attributes: structuredClone(sharedAttributes),
  indices: accentIndexAccessorIndex,
  material: accentMaterialIndex,
  mode: sharedMode,
  extras: {
    nilbotsRole: 'team-accent',
  },
};
outputDocument.meshes[0].primitives = [hullPrimitive, accentPrimitive];
outputDocument.asset.generator = `${sourceDocument.asset?.generator ?? 'unknown'} + Nilbots team-accent postprocess 1`;

const outputBytes = buildGlb(outputDocument, viewData);
assertPinnedArtifact(
  'generated team-accent model',
  outputBytes,
  recipe.outputs.model,
);
await writeFile(outputModelPath, outputBytes);

const sourceTriangleHash = triangleSetHash(sourceIndices.values);
const outputTriangleHash = triangleSetHash([
  ...split.hullIndices,
  ...split.accentIndices,
]);
const sourceAttributeHash = sha256(
  source.binary.subarray(
    sourceDocument.bufferViews[sourcePositions.accessor.bufferView].byteOffset ??
      0,
    (sourceDocument.bufferViews[sourcePositions.accessor.bufferView].byteOffset ??
      0) +
      sourceDocument.bufferViews[sourcePositions.accessor.bufferView].byteLength,
  ),
);
const outputAttributeHash = sha256(
  viewData[sourcePositions.accessor.bufferView],
);
const sourceNodeHash = sha256(
  Buffer.from(
    JSON.stringify({
      scene: sourceDocument.scene,
      scenes: sourceDocument.scenes,
      nodes: sourceDocument.nodes,
    }),
  ),
);
const outputNodeHash = sha256(
  Buffer.from(
    JSON.stringify({
      scene: outputDocument.scene,
      scenes: outputDocument.scenes,
      nodes: outputDocument.nodes,
    }),
  ),
);

const validationChecks = {
  trianglePartitionExact: sourceTriangleHash === outputTriangleHash,
  triangleCountPreserved:
    sourceIndices.values.length ===
    split.hullIndices.length + split.accentIndices.length,
  sharedGeometryAccessors:
    JSON.stringify(hullPrimitive.attributes) ===
    JSON.stringify(accentPrimitive.attributes),
  geometryBufferUnchanged: sourceAttributeHash === outputAttributeHash,
  nodesScenesFacingUnchanged: sourceNodeHash === outputNodeHash,
  teamMaterialTagged:
    accentMaterial.extras?.nilbotsRole === 'team-accent',
  teamPrimitiveTagged:
    accentPrimitive.extras?.nilbotsRole === 'team-accent',
  teamMaterialHasNoFixedColorMaps:
    !accentMaterial.pbrMetallicRoughness?.baseColorTexture &&
    !accentMaterial.emissiveTexture,
  teamMaterialKeepsNormalAndMetallicRoughness:
    Boolean(accentMaterial.normalTexture) &&
    Boolean(accentMaterial.pbrMetallicRoughness?.metallicRoughnessTexture),
  hullKeepsFullPbr:
    Boolean(hullMaterial.pbrMetallicRoughness?.baseColorTexture) &&
    Boolean(hullMaterial.pbrMetallicRoughness?.metallicRoughnessTexture) &&
    Boolean(hullMaterial.normalTexture) &&
    Boolean(hullMaterial.emissiveTexture),
  cyanRemovedFromHullBase: neutralBase.cyanPixelsAfter === 0,
  cyanRemovedFromHullEmission: neutralEmission.cyanPixelsAfter === 0,
};
if (Object.values(validationChecks).some((passed) => !passed)) {
  throw new Error(
    `Internal structural validation failed: ${JSON.stringify(validationChecks)}`,
  );
}

const report = {
  version: 1,
  method: 'deterministic-offline-team-accent-face-partition',
  recipe: {
    file: relativeToRepository(recipePath),
    sha256: sha256(recipeBytes),
  },
  source: {
    taskId: recipe.taskId,
    imageEnhancement: recipe.imageEnhancement,
    model: relativeToRepository(inputPath),
    modelBytes: sourceBytes.length,
    modelSha256: sha256(sourceBytes),
    baseColor: {
      file: relativeToRepository(baseColorPath),
      width: baseSource.width,
      height: baseSource.height,
      sha256: sha256(baseColorBytes),
    },
    emission: {
      file: relativeToRepository(emissionPath),
      width: emissionSource.width,
      height: emissionSource.height,
      sha256: sha256(emissionBytes),
    },
  },
  outputs: {
    model: artifact(outputModelPath, outputBytes),
    losslessMask: artifact(outputMaskPath, maskPng, {
      width: baseSource.width,
      height: baseSource.height,
      format: 'PNG',
    }),
    neutralHullBase: artifact(outputBasePath, neutralBase.png, {
      width: runtimeBase.width,
      height: runtimeBase.height,
      format: 'PNG',
    }),
    neutralHullEmission: artifact(
      outputEmissionPath,
      neutralEmission.png,
      {
        width: runtimeEmission.width,
        height: runtimeEmission.height,
        format: 'PNG',
      },
    ),
  },
  thresholds,
  mask: {
    workingWidth: maskResult.workingWidth,
    workingHeight: maskResult.workingHeight,
    baseCandidatePixelsAtWorkingResolution:
      maskResult.baseCandidatePixels,
    emissionCandidatePixelsAtWorkingResolution:
      maskResult.emissionCandidatePixels,
    initialTwoChannelPixelsAtWorkingResolution:
      maskResult.initialPixels,
    strongSeedPixelsAtWorkingResolution:
      maskResult.strongSeedPixels,
    finalPixelsAtWorkingResolution:
      maskResult.keptPixelsAtWorkingResolution,
    keptPixels: maskResult.keptPixels,
    keptPercent:
      (maskResult.keptPixels * 100) /
      (baseSource.width * baseSource.height),
    components: maskResult.components,
  },
  triangleSplit: {
    sourceTriangles: sourceIndices.values.length / 3,
    hullTriangles: split.hullIndices.length / 3,
    teamAccentTriangles: split.accentIndices.length / 3,
    hullTriangleHash: triangleSetHash(split.hullIndices),
    teamAccentTriangleHash: triangleSetHash(split.accentIndices),
    teamAccentPercent:
      (split.accentIndices.length * 100) / sourceIndices.values.length,
    sourceSurfaceArea: split.sourceSurfaceArea,
    teamAccentSurfaceArea: split.accentSurfaceArea,
    teamAccentSurfaceAreaPercent:
      (split.accentSurfaceArea * 100) / split.sourceSurfaceArea,
    votesHistogram: Object.fromEntries(
      split.votesHistogram.map((count, votes) => [String(votes), count]),
    ),
    connectedComponents: split.connectedComponents,
  },
  hullNeutralization: {
    baseColor: {
      maskedPixels: neutralBase.maskedPixels,
      cyanPixelsBefore: neutralBase.cyanPixelsBefore,
      cyanPixelsAfter: neutralBase.cyanPixelsAfter,
    },
    emission: {
      maskedPixels: neutralEmission.maskedPixels,
      cyanPixelsBefore: neutralEmission.cyanPixelsBefore,
      cyanPixelsAfter: neutralEmission.cyanPixelsAfter,
    },
  },
  preservation: {
    sourceTriangleHash,
    outputTriangleHash,
    sourceAttributeBufferSha256: sourceAttributeHash,
    outputAttributeBufferSha256: outputAttributeHash,
    sourceNodesScenesSha256: sourceNodeHash,
    outputNodesScenesSha256: outputNodeHash,
  },
  structure: {
    primitives: [
      {
        role: 'hull',
        material: hullMaterialIndex,
        indexAccessor: indexAccessorIndex,
        attributes: hullPrimitive.attributes,
      },
      {
        role: 'team-accent',
        material: accentMaterialIndex,
        indexAccessor: accentIndexAccessorIndex,
        attributes: accentPrimitive.attributes,
      },
    ],
    teamMaterial: accentMaterial,
  },
  validation: {
    pass: true,
    checks: validationChecks,
  },
};
await writeFile(outputReportPath, `${JSON.stringify(report, null, 2)}\n`);

console.log(
  `Wrote ${relativeToRepository(outputModelPath)} (${formatBytes(outputBytes.length)}).`,
);
console.log(
  `Team accent: ${report.triangleSplit.teamAccentTriangles.toLocaleString()} / ${report.triangleSplit.sourceTriangles.toLocaleString()} triangles (${report.triangleSplit.teamAccentPercent.toFixed(2)}%), ${report.triangleSplit.teamAccentSurfaceAreaPercent.toFixed(2)}% surface area.`,
);
console.log(`Structural checks: ${Object.keys(validationChecks).length}/${Object.keys(validationChecks).length} passed.`);
console.log(`Report: ${relativeToRepository(outputReportPath)}`);

function deriveTeamMask(base, emission, config) {
  if (
    base.width !== emission.width * 2 ||
    base.height !== emission.height * 2
  ) {
    throw new Error(
      `Conservative mask requires a 2:1 base/emission ratio; got ${base.width}x${base.height} and ${emission.width}x${emission.height}.`,
    );
  }
  const workingWidth = emission.width;
  const workingHeight = emission.height;
  const workingPixels = workingWidth * workingHeight;
  const baseCandidate = new Uint8Array(workingPixels);
  const emissionCandidate = new Uint8Array(workingPixels);
  const strongSeed = new Uint8Array(workingPixels);
  const initial = new Uint8Array(workingPixels);
  let baseCandidatePixels = 0;
  let emissionCandidatePixels = 0;
  let initialPixels = 0;
  let strongSeedPixels = 0;

  for (let y = 0; y < workingHeight; y += 1) {
    for (let x = 0; x < workingWidth; x += 1) {
      const pixel = y * workingWidth + x;
      const topLeft = (y * 2 * base.width + x * 2) * 4;
      const topRight = topLeft + 4;
      const bottomLeft = topLeft + base.width * 4;
      const bottomRight = bottomLeft + 4;
      const baseR =
        (base.data[topLeft] +
          base.data[topRight] +
          base.data[bottomLeft] +
          base.data[bottomRight]) /
        4;
      const baseG =
        (base.data[topLeft + 1] +
          base.data[topRight + 1] +
          base.data[bottomLeft + 1] +
          base.data[bottomRight + 1]) /
        4;
      const baseB =
        (base.data[topLeft + 2] +
          base.data[topRight + 2] +
          base.data[bottomLeft + 2] +
          base.data[bottomRight + 2]) /
        4;
      const emissionOffset = pixel * 4;
      const emissionR = emission.data[emissionOffset];
      const emissionG = emission.data[emissionOffset + 1];
      const emissionB = emission.data[emissionOffset + 2];
      const baseLoose = isChannelCyan(
        baseR,
        baseG,
        baseB,
        config.baseLoose,
      );
      const baseStrong = isChannelCyan(
        baseR,
        baseG,
        baseB,
        config.baseStrong,
      );
      const emissionLoose = isChannelCyan(
        emissionR,
        emissionG,
        emissionB,
        config.emissionLoose,
      );
      const emissionStrong = isChannelCyan(
        emissionR,
        emissionG,
        emissionB,
        config.emissionStrong,
      );
      if (baseLoose) {
        baseCandidate[pixel] = 1;
        baseCandidatePixels += 1;
      }
      if (emissionLoose) {
        emissionCandidate[pixel] = 1;
        emissionCandidatePixels += 1;
      }
      if (baseLoose && emissionLoose) {
        initial[pixel] = 1;
        initialPixels += 1;
      }
      if (
        (baseStrong && emissionLoose) ||
        (emissionStrong && baseLoose)
      ) {
        strongSeed[pixel] = 1;
        strongSeedPixels += 1;
      }
    }
  }

  const seededInitial = filterSeededComponents(
    initial,
    strongSeed,
    workingWidth,
    workingHeight,
    0,
  );
  let workingMask = seededInitial.mask;
  for (
    let iteration = 0;
    iteration < config.guidedGrowthIterations;
    iteration += 1
  ) {
    workingMask = guidedGrow(
      workingMask,
      baseCandidate,
      workingWidth,
      workingHeight,
    );
  }
  workingMask = erodeSquare3x3(
    dilateSquare3x3(workingMask, workingWidth, workingHeight),
    workingWidth,
    workingHeight,
  );
  const final = filterSeededComponents(
    workingMask,
    strongSeed,
    workingWidth,
    workingHeight,
    config.minimumFinalComponentPixelsAtWorkingResolution,
  );
  const mask = upsampleMask2x(
    final.mask,
    workingWidth,
    workingHeight,
  );
  const keptPixelsAtWorkingResolution = countSet(final.mask);
  return {
    mask,
    workingWidth,
    workingHeight,
    baseCandidatePixels,
    emissionCandidatePixels,
    initialPixels,
    strongSeedPixels,
    keptPixelsAtWorkingResolution,
    keptPixels: keptPixelsAtWorkingResolution * 4,
    components: {
      initial: seededInitial.stats,
      final: final.stats,
    },
  };
}

function filterSeededComponents(
  source,
  seed,
  width,
  height,
  minimumArea,
) {
  const sourceCount = countSet(source);
  const visited = new Uint8Array(source.length);
  const output = new Uint8Array(source.length);
  const queue = new Uint32Array(sourceCount);
  const components = [];
  let keptComponents = 0;
  let removedComponents = 0;
  let keptPixels = 0;
  let removedPixels = 0;
  for (let start = 0; start < source.length; start += 1) {
    if (!source[start] || visited[start]) continue;
    let head = 0;
    let tail = 1;
    let containsSeed = Boolean(seed[start]);
    queue[0] = start;
    visited[start] = 1;
    while (head < tail) {
      const pixel = queue[head];
      head += 1;
      if (seed[pixel]) containsSeed = true;
      const x = pixel % width;
      const y = (pixel - x) / width;
      for (let offsetY = -1; offsetY <= 1; offsetY += 1) {
        const nextY = y + offsetY;
        if (nextY < 0 || nextY >= height) continue;
        for (let offsetX = -1; offsetX <= 1; offsetX += 1) {
          if (offsetX === 0 && offsetY === 0) continue;
          const nextX = x + offsetX;
          if (nextX < 0 || nextX >= width) continue;
          const next = nextY * width + nextX;
          if (!source[next] || visited[next]) continue;
          visited[next] = 1;
          queue[tail] = next;
          tail += 1;
        }
      }
    }
    const keep = containsSeed && tail >= minimumArea;
    components.push({ pixels: tail, containsStrongSeed: containsSeed, kept: keep });
    if (keep) {
      keptComponents += 1;
      keptPixels += tail;
      for (let index = 0; index < tail; index += 1) {
        output[queue[index]] = 1;
      }
    } else {
      removedComponents += 1;
      removedPixels += tail;
    }
  }
  components.sort((left, right) => right.pixels - left.pixels);
  return {
    mask: output,
    stats: {
      total: components.length,
      kept: keptComponents,
      removed: removedComponents,
      keptPixels,
      removedPixels,
      largest: components.slice(0, 24),
    },
  };
}

function guidedGrow(source, candidate, width, height) {
  const output = source.slice();
  for (let pixel = 0; pixel < source.length; pixel += 1) {
    if (source[pixel] || !candidate[pixel]) continue;
    const x = pixel % width;
    const y = (pixel - x) / width;
    let neighbor = false;
    for (let offsetY = -1; offsetY <= 1 && !neighbor; offsetY += 1) {
      const nextY = y + offsetY;
      if (nextY < 0 || nextY >= height) continue;
      for (let offsetX = -1; offsetX <= 1; offsetX += 1) {
        if (offsetX === 0 && offsetY === 0) continue;
        const nextX = x + offsetX;
        if (nextX < 0 || nextX >= width) continue;
        if (source[nextY * width + nextX]) {
          neighbor = true;
          break;
        }
      }
    }
    if (neighbor) output[pixel] = 1;
  }
  return output;
}

function dilateSquare3x3(source, width, height) {
  const output = new Uint8Array(source.length);
  for (let pixel = 0; pixel < source.length; pixel += 1) {
    if (!source[pixel]) continue;
    const x = pixel % width;
    const y = (pixel - x) / width;
    for (let offsetY = -1; offsetY <= 1; offsetY += 1) {
      const nextY = y + offsetY;
      if (nextY < 0 || nextY >= height) continue;
      for (let offsetX = -1; offsetX <= 1; offsetX += 1) {
        const nextX = x + offsetX;
        if (nextX < 0 || nextX >= width) continue;
        output[nextY * width + nextX] = 1;
      }
    }
  }
  return output;
}

function erodeSquare3x3(source, width, height) {
  const output = new Uint8Array(source.length);
  for (let y = 1; y + 1 < height; y += 1) {
    for (let x = 1; x + 1 < width; x += 1) {
      let keep = true;
      for (let offsetY = -1; offsetY <= 1 && keep; offsetY += 1) {
        for (let offsetX = -1; offsetX <= 1; offsetX += 1) {
          if (!source[(y + offsetY) * width + x + offsetX]) {
            keep = false;
            break;
          }
        }
      }
      if (keep) output[y * width + x] = 1;
    }
  }
  return output;
}

function upsampleMask2x(source, width, height) {
  const outputWidth = width * 2;
  const output = new Uint8Array(source.length * 4);
  for (let y = 0; y < height; y += 1) {
    for (let x = 0; x < width; x += 1) {
      if (!source[y * width + x]) continue;
      const outputX = x * 2;
      const outputY = y * 2;
      const topLeft = outputY * outputWidth + outputX;
      output[topLeft] = 1;
      output[topLeft + 1] = 1;
      output[topLeft + outputWidth] = 1;
      output[topLeft + outputWidth + 1] = 1;
    }
  }
  return output;
}

function countSet(input) {
  let count = 0;
  for (const value of input) count += value;
  return count;
}

function isChannelCyan(r, g, b, config) {
  return (
    g >= config.greenMin &&
    b >= config.blueMin &&
    g - r >= config.greenMinusRedMin &&
    b - r >= config.blueMinusRedMin &&
    2 * g >= b &&
    2 * b >= g
  );
}

function splitTriangles({
  indices,
  uvs,
  positions,
  mask,
  maskWidth,
  maskHeight,
}) {
  if (indices.length % 3 !== 0) {
    throw new Error(`Index count ${indices.length} is not divisible by three.`);
  }
  const samples = [
    [1 / 3, 1 / 3, 1 / 3],
    [0.0597158717, 0.4701420641, 0.4701420641],
    [0.4701420641, 0.0597158717, 0.4701420641],
    [0.4701420641, 0.4701420641, 0.0597158717],
    [0.7974269853, 0.1012865073, 0.1012865073],
    [0.1012865073, 0.7974269853, 0.1012865073],
    [0.1012865073, 0.1012865073, 0.7974269853],
  ];
  const hullIndices = [];
  const accentIndices = [];
  const triangleCount = indices.length / 3;
  const votesHistogram = Array(samples.length + 1).fill(0);
  const selectedTriangles = new Uint8Array(triangleCount);
  let sourceSurfaceArea = 0;
  let accentSurfaceArea = 0;

  for (let triangle = 0; triangle < triangleCount; triangle += 1) {
    const indexOffset = triangle * 3;
    const a = indices[indexOffset];
    const b = indices[indexOffset + 1];
    const c = indices[indexOffset + 2];
    let votes = 0;
    for (const [wa, wb, wc] of samples) {
      const u =
        uvs[a * 2] * wa + uvs[b * 2] * wb + uvs[c * 2] * wc;
      const v =
        uvs[a * 2 + 1] * wa +
        uvs[b * 2 + 1] * wb +
        uvs[c * 2 + 1] * wc;
      if (sampleRepeatMask(mask, maskWidth, maskHeight, u, v)) votes += 1;
    }
    votesHistogram[votes] += 1;
    const area = triangleArea(positions, a, b, c);
    sourceSurfaceArea += area;
    if (votes >= thresholds.triangleVoteThreshold) {
      selectedTriangles[triangle] = 1;
      accentSurfaceArea += area;
      accentIndices.push(a, b, c);
    } else {
      hullIndices.push(a, b, c);
    }
  }

  return {
    hullIndices,
    accentIndices,
    votesHistogram,
    sourceSurfaceArea,
    accentSurfaceArea,
    connectedComponents: selectedTriangleComponents(
      indices,
      selectedTriangles,
      positions.length / 3,
    ),
  };
}

function selectedTriangleComponents(indices, selected, vertexCount) {
  const parent = new Int32Array(selected.length);
  const size = new Uint32Array(selected.length);
  const lastTriangleForVertex = new Int32Array(vertexCount);
  lastTriangleForVertex.fill(-1);
  for (let triangle = 0; triangle < selected.length; triangle += 1) {
    parent[triangle] = triangle;
    if (!selected[triangle]) continue;
    size[triangle] = 1;
    for (let corner = 0; corner < 3; corner += 1) {
      const vertex = indices[triangle * 3 + corner];
      const previous = lastTriangleForVertex[vertex];
      if (previous >= 0) union(triangle, previous);
      lastTriangleForVertex[vertex] = triangle;
    }
  }
  const componentSizes = new Map();
  for (let triangle = 0; triangle < selected.length; triangle += 1) {
    if (!selected[triangle]) continue;
    const root = find(triangle);
    componentSizes.set(root, (componentSizes.get(root) ?? 0) + 1);
  }
  const sorted = [...componentSizes.values()].sort((a, b) => b - a);
  return {
    total: sorted.length,
    singleTriangle: sorted.filter((value) => value === 1).length,
    underTenTriangles: sorted.filter((value) => value < 10).length,
    trianglesInComponentsUnderTen: sorted
      .filter((value) => value < 10)
      .reduce((sum, value) => sum + value, 0),
    largestTriangleCounts: sorted.slice(0, 24),
  };

  function find(input) {
    let value = input;
    while (parent[value] !== value) {
      parent[value] = parent[parent[value]];
      value = parent[value];
    }
    return value;
  }

  function union(left, right) {
    let a = find(left);
    let b = find(right);
    if (a === b) return;
    if (size[a] < size[b]) [a, b] = [b, a];
    parent[b] = a;
    size[a] += size[b];
  }
}

function neutralizeBaseColor(source, downsampledMask) {
  const output = new Uint8ClampedArray(source.data);
  let maskedPixels = 0;
  let cyanPixelsBefore = 0;
  for (let pixel = 0, offset = 0; pixel < downsampledMask.length; pixel += 1, offset += 4) {
    const r = output[offset];
    const g = output[offset + 1];
    const b = output[offset + 2];
    const cyan = isBroadCyan(r, g, b);
    if (cyan) cyanPixelsBefore += 1;
    if (!downsampledMask[pixel] && !cyan) continue;
    maskedPixels += 1;
    const luma = Math.round(0.2126 * r + 0.7152 * g + 0.0722 * b);
    output[offset] = luma;
    output[offset + 1] = luma;
    output[offset + 2] = luma;
  }
  return {
    png: encodePixels(output, source.width, source.height),
    maskedPixels,
    cyanPixelsBefore,
    cyanPixelsAfter: countCyan(output, isBroadCyan),
  };
}

function neutralizeEmission(source, downsampledMask) {
  const output = new Uint8ClampedArray(source.data);
  let maskedPixels = 0;
  let cyanPixelsBefore = 0;
  for (let pixel = 0, offset = 0; pixel < downsampledMask.length; pixel += 1, offset += 4) {
    const cyan = isBroadCyan(
      output[offset],
      output[offset + 1],
      output[offset + 2],
    );
    if (cyan) cyanPixelsBefore += 1;
    if (!downsampledMask[pixel] && !cyan) continue;
    maskedPixels += 1;
    output[offset] = 0;
    output[offset + 1] = 0;
    output[offset + 2] = 0;
  }
  return {
    png: encodePixels(output, source.width, source.height),
    maskedPixels,
    cyanPixelsBefore,
    cyanPixelsAfter: countCyan(output, isBroadCyan),
  };
}

function makeTeamAccentMaterial(hullMaterial) {
  const hullPbr = hullMaterial.pbrMetallicRoughness ?? {};
  const pbrMetallicRoughness = {
    baseColorFactor: [1, 1, 1, 1],
    metallicFactor: hullPbr.metallicFactor ?? 1,
    roughnessFactor: hullPbr.roughnessFactor ?? 1,
  };
  if (hullPbr.metallicRoughnessTexture) {
    pbrMetallicRoughness.metallicRoughnessTexture = structuredClone(
      hullPbr.metallicRoughnessTexture,
    );
  }
  const material = {
    name: 'Nilbots Team Accent',
    doubleSided: hullMaterial.doubleSided ?? false,
    pbrMetallicRoughness,
    emissiveFactor: [1, 1, 1],
    extras: {
      nilbotsRole: 'team-accent',
    },
  };
  if (hullMaterial.normalTexture) {
    material.normalTexture = structuredClone(hullMaterial.normalTexture);
  }
  if (hullMaterial.occlusionTexture) {
    material.occlusionTexture = structuredClone(
      hullMaterial.occlusionTexture,
    );
  }
  return material;
}

function textureImageIndex(document, textureIndex, label) {
  if (!Number.isInteger(textureIndex)) {
    throw new Error(`The ${label} texture is missing.`);
  }
  const imageIndex = document.textures?.[textureIndex]?.source;
  if (!Number.isInteger(imageIndex)) {
    throw new Error(`The ${label} texture has no image source.`);
  }
  if (!Number.isInteger(document.images?.[imageIndex]?.bufferView)) {
    throw new Error(`The ${label} image is not embedded in the GLB.`);
  }
  return imageIndex;
}

async function loadPixels(pathOrBytes) {
  const image = await loadImage(pathOrBytes);
  const canvas = createCanvas(image.width, image.height);
  const context = canvas.getContext('2d');
  context.drawImage(image, 0, 0);
  const imageData = context.getImageData(0, 0, image.width, image.height);
  return {
    width: image.width,
    height: image.height,
    data: imageData.data,
  };
}

function encodeBinaryMask(mask, width, height) {
  const pixels = new Uint8ClampedArray(mask.length * 4);
  for (let pixel = 0, offset = 0; pixel < mask.length; pixel += 1, offset += 4) {
    const value = mask[pixel] ? 255 : 0;
    pixels[offset] = value;
    pixels[offset + 1] = value;
    pixels[offset + 2] = value;
    pixels[offset + 3] = 255;
  }
  return encodePixels(pixels, width, height);
}

function encodePixels(pixels, width, height) {
  const canvas = createCanvas(width, height);
  const context = canvas.getContext('2d');
  const imageData = context.createImageData(width, height);
  imageData.data.set(pixels);
  context.putImageData(imageData, 0, 0);
  return canvas.toBuffer('image/png');
}

function downsampleMaskAny(source, sourceWidth, sourceHeight, width, height) {
  const output = new Uint8Array(width * height);
  for (let sourceY = 0; sourceY < sourceHeight; sourceY += 1) {
    const targetY = Math.min(
      height - 1,
      Math.floor((sourceY * height) / sourceHeight),
    );
    const sourceRow = sourceY * sourceWidth;
    const targetRow = targetY * width;
    for (let sourceX = 0; sourceX < sourceWidth; sourceX += 1) {
      if (!source[sourceRow + sourceX]) continue;
      const targetX = Math.min(
        width - 1,
        Math.floor((sourceX * width) / sourceWidth),
      );
      output[targetRow + targetX] = 1;
    }
  }
  return output;
}

function isCyan(r, g, b, config) {
  const max = Math.max(r, g, b);
  const min = Math.min(r, g, b);
  const delta = max - min;
  if (
    max < config.valueMin * 255 ||
    delta < config.channelDeltaMin ||
    delta / max < config.saturationMin
  ) {
    return false;
  }
  const hue = rgbHue(r, g, b, max, delta);
  return hue >= config.hueMin && hue <= config.hueMax;
}

function isBroadCyan(r, g, b) {
  const max = Math.max(r, g, b);
  const min = Math.min(r, g, b);
  const delta = max - min;
  if (max < 4 || delta < 2 || delta / max < 0.06) return false;
  const hue = rgbHue(r, g, b, max, delta);
  return hue >= 150 && hue <= 220;
}

function rgbHue(r, g, b, max, delta) {
  let hue;
  if (max === r) hue = ((g - b) / delta) % 6;
  else if (max === g) hue = (b - r) / delta + 2;
  else hue = (r - g) / delta + 4;
  hue *= 60;
  return hue < 0 ? hue + 360 : hue;
}

function countCyan(pixels, predicate) {
  let count = 0;
  for (let offset = 0; offset < pixels.length; offset += 4) {
    if (predicate(pixels[offset], pixels[offset + 1], pixels[offset + 2])) {
      count += 1;
    }
  }
  return count;
}

function sampleRepeatMask(mask, width, height, uInput, vInput) {
  const u = ((uInput % 1) + 1) % 1;
  const v = ((vInput % 1) + 1) % 1;
  const x = Math.min(width - 1, Math.floor(u * width));
  const y = Math.min(height - 1, Math.floor(v * height));
  return mask[y * width + x] === 1;
}

function triangleArea(positions, a, b, c) {
  const abx = positions[b * 3] - positions[a * 3];
  const aby = positions[b * 3 + 1] - positions[a * 3 + 1];
  const abz = positions[b * 3 + 2] - positions[a * 3 + 2];
  const acx = positions[c * 3] - positions[a * 3];
  const acy = positions[c * 3 + 1] - positions[a * 3 + 1];
  const acz = positions[c * 3 + 2] - positions[a * 3 + 2];
  const crossX = aby * acz - abz * acy;
  const crossY = abz * acx - abx * acz;
  const crossZ = abx * acy - aby * acx;
  return 0.5 * Math.hypot(crossX, crossY, crossZ);
}

function parseGlb(bytes, label) {
  if (bytes.toString('ascii', 0, 4) !== 'glTF') {
    throw new Error(`${label} is not a GLB file.`);
  }
  if (bytes.readUInt32LE(4) !== 2) {
    throw new Error(`${label} is not glTF 2.0.`);
  }
  const jsonLength = bytes.readUInt32LE(12);
  if (bytes.readUInt32LE(16) !== 0x4e4f534a) {
    throw new Error(`${label} does not begin with a JSON chunk.`);
  }
  const jsonStart = 20;
  const jsonEnd = jsonStart + jsonLength;
  const document = JSON.parse(
    bytes
      .subarray(jsonStart, jsonEnd)
      .toString('utf8')
      .replace(/[\u0000\u0020]+$/u, ''),
  );
  const binaryHeader = jsonEnd;
  const binaryLength = bytes.readUInt32LE(binaryHeader);
  if (bytes.readUInt32LE(binaryHeader + 4) !== 0x004e4942) {
    throw new Error(`${label} has no binary chunk after JSON.`);
  }
  const binary = bytes.subarray(
    binaryHeader + 8,
    binaryHeader + 8 + binaryLength,
  );
  return { bytes, document, binary };
}

function expectSinglePrimitive(document, label) {
  if (
    document.buffers?.length !== 1 ||
    document.meshes?.length !== 1 ||
    document.meshes[0].primitives?.length !== 1
  ) {
    throw new Error(`${label} is not the single-buffer, single-primitive lean proof.`);
  }
  return document.meshes[0].primitives[0];
}

function readAccessor(glb, accessorIndex) {
  const accessor = glb.document.accessors?.[accessorIndex];
  if (!accessor || accessor.sparse) {
    throw new Error(`Accessor ${accessorIndex} is missing or sparse.`);
  }
  const view = glb.document.bufferViews?.[accessor.bufferView];
  if (!view) throw new Error(`Accessor ${accessorIndex} has no buffer view.`);
  const itemSize = accessorItemSize(accessor.type);
  const byteSize = componentSize(accessor.componentType);
  const stride = view.byteStride ?? itemSize * byteSize;
  const start = (view.byteOffset ?? 0) + (accessor.byteOffset ?? 0);
  const values = new Array(accessor.count * itemSize);
  for (let item = 0; item < accessor.count; item += 1) {
    const itemOffset = start + item * stride;
    for (let component = 0; component < itemSize; component += 1) {
      values[item * itemSize + component] = readComponent(
        glb.binary,
        itemOffset + component * byteSize,
        accessor.componentType,
      );
    }
  }
  return { accessor, view, values };
}

function accessorItemSize(type) {
  const sizes = { SCALAR: 1, VEC2: 2, VEC3: 3, VEC4: 4 };
  const size = sizes[type];
  if (!size) throw new Error(`Unsupported accessor type ${type}.`);
  return size;
}

function componentSize(componentType) {
  const sizes = { 5121: 1, 5123: 2, 5125: 4, 5126: 4 };
  const size = sizes[componentType];
  if (!size) throw new Error(`Unsupported component type ${componentType}.`);
  return size;
}

function readComponent(buffer, offset, componentType) {
  if (componentType === 5121) return buffer.readUInt8(offset);
  if (componentType === 5123) return buffer.readUInt16LE(offset);
  if (componentType === 5125) return buffer.readUInt32LE(offset);
  if (componentType === 5126) return buffer.readFloatLE(offset);
  throw new Error(`Unsupported component type ${componentType}.`);
}

function encodeScalarAccessor(values, componentType) {
  const output = Buffer.alloc(values.length * componentSize(componentType));
  for (let index = 0; index < values.length; index += 1) {
    const offset = index * componentSize(componentType);
    if (componentType === 5121) output.writeUInt8(values[index], offset);
    else if (componentType === 5123)
      output.writeUInt16LE(values[index], offset);
    else if (componentType === 5125)
      output.writeUInt32LE(values[index], offset);
    else throw new Error(`Unsupported index component type ${componentType}.`);
  }
  return output;
}

function buildGlb(document, viewData) {
  if (document.bufferViews.length !== viewData.length) {
    throw new Error('Every output buffer view must have one binary payload.');
  }
  const binaryParts = [];
  let binaryLength = 0;
  for (let index = 0; index < document.bufferViews.length; index += 1) {
    const padding = (4 - (binaryLength % 4)) % 4;
    if (padding) {
      binaryParts.push(Buffer.alloc(padding));
      binaryLength += padding;
    }
    const bytes = viewData[index];
    const view = document.bufferViews[index];
    view.buffer = 0;
    view.byteOffset = binaryLength;
    view.byteLength = bytes.length;
    binaryParts.push(bytes);
    binaryLength += bytes.length;
  }
  const binaryPadding = (4 - (binaryLength % 4)) % 4;
  if (binaryPadding) {
    binaryParts.push(Buffer.alloc(binaryPadding));
    binaryLength += binaryPadding;
  }
  const binary = Buffer.concat(binaryParts, binaryLength);
  document.buffers = [{ byteLength: binary.length }];

  let json = Buffer.from(JSON.stringify(document));
  const jsonPadding = (4 - (json.length % 4)) % 4;
  if (jsonPadding) json = Buffer.concat([json, Buffer.alloc(jsonPadding, 0x20)]);

  const output = Buffer.alloc(12 + 8 + json.length + 8 + binary.length);
  output.write('glTF', 0, 'ascii');
  output.writeUInt32LE(2, 4);
  output.writeUInt32LE(output.length, 8);
  output.writeUInt32LE(json.length, 12);
  output.writeUInt32LE(0x4e4f534a, 16);
  json.copy(output, 20);
  const binaryHeader = 20 + json.length;
  output.writeUInt32LE(binary.length, binaryHeader);
  output.writeUInt32LE(0x004e4942, binaryHeader + 4);
  binary.copy(output, binaryHeader + 8);
  return output;
}

function triangleSetHash(indices) {
  const triangles = [];
  for (let offset = 0; offset < indices.length; offset += 3) {
    triangles.push(
      `${indices[offset]},${indices[offset + 1]},${indices[offset + 2]}`,
    );
  }
  triangles.sort();
  return sha256(Buffer.from(triangles.join('\n')));
}

function artifact(path, bytes, extra = {}) {
  return {
    file: relativeToRepository(path),
    bytes: bytes.length,
    sha256: sha256(bytes),
    ...extra,
  };
}

function sha256(bytes) {
  return createHash('sha256').update(bytes).digest('hex');
}

function relativeToRepository(path) {
  const prefix = `${repositoryRoot}/`;
  return path.startsWith(prefix) ? path.slice(prefix.length) : path;
}

function formatBytes(bytes) {
  return `${(bytes / (1024 * 1024)).toFixed(2)} MiB`;
}

function validateRecipe(input) {
  if (input?.version !== 1) {
    throw new Error(`Unsupported team-accent recipe version ${input?.version}.`);
  }
  if (
    input.method !==
    'deterministic-offline-team-accent-face-partition'
  ) {
    throw new Error(`Unsupported team-accent method ${input.method}.`);
  }
  if (typeof input.taskId !== 'string' || input.taskId.length === 0) {
    throw new Error('The recipe must name its provider task ID.');
  }
  if (typeof input.imageEnhancement !== 'boolean') {
    throw new Error('The recipe must pin imageEnhancement as a boolean.');
  }
  for (const [label, pin] of Object.entries(input.inputs ?? {})) {
    validateArtifactPin(`input ${label}`, pin, true);
  }
  for (const [label, pin] of Object.entries(input.outputs ?? {})) {
    validateArtifactPin(`output ${label}`, pin, false);
  }
  for (const required of ['model', 'baseColor', 'emission']) {
    if (!input.inputs?.[required]) {
      throw new Error(`The recipe is missing input ${required}.`);
    }
  }
  for (const required of [
    'model',
    'losslessMask',
    'neutralHullBase',
    'neutralHullEmission',
  ]) {
    if (!input.outputs?.[required]) {
      throw new Error(`The recipe is missing output ${required}.`);
    }
  }
  if (
    typeof input.thresholds !== 'object' ||
    input.thresholds === null ||
    input.thresholds.triangleSamples !== 7 ||
    input.thresholds.triangleVoteThreshold !== 5
  ) {
    throw new Error(
      'The recipe must pin the seven-sample, five-vote Striker classifier.',
    );
  }
  if (
    input.reportFilename !== undefined &&
    basename(input.reportFilename) !== input.reportFilename
  ) {
    throw new Error('reportFilename must be a plain filename.');
  }
}

function validateArtifactPin(label, pin, input) {
  if (typeof pin !== 'object' || pin === null) {
    throw new Error(`The ${label} pin is missing.`);
  }
  const pathKey = input ? 'file' : 'fileName';
  if (typeof pin[pathKey] !== 'string' || pin[pathKey].length === 0) {
    throw new Error(`The ${label} pin must include ${pathKey}.`);
  }
  if (
    !Number.isSafeInteger(pin.bytes) ||
    pin.bytes <= 0 ||
    !/^[0-9a-f]{64}$/u.test(pin.sha256)
  ) {
    throw new Error(`The ${label} pin must include exact bytes and SHA-256.`);
  }
}

function repositoryFile(file, label) {
  const path = resolve(repositoryRoot, file);
  if (!path.startsWith(`${repositoryRoot}${sep}`)) {
    throw new Error(`The ${label} must resolve inside the repository.`);
  }
  return path;
}

function outputFile(directory, pin) {
  return resolve(directory, outputFilename(pin, 'generated artifact'));
}

function outputFilename(pin, label) {
  if (
    typeof pin?.fileName !== 'string' ||
    basename(pin.fileName) !== pin.fileName
  ) {
    throw new Error(`The ${label} must use a plain output filename.`);
  }
  return pin.fileName;
}

function assertPinnedArtifact(label, bytes, pin) {
  if (bytes.length !== pin.bytes) {
    throw new Error(
      `${label} byte-size drift: expected ${pin.bytes}, got ${bytes.length}.`,
    );
  }
  const hash = sha256(bytes);
  if (hash !== pin.sha256) {
    throw new Error(
      `${label} SHA-256 drift: expected ${pin.sha256}, got ${hash}.`,
    );
  }
}
