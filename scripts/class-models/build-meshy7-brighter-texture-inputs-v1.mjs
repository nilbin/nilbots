#!/usr/bin/env node

import { createHash } from 'node:crypto';
import { createRequire } from 'node:module';
import { mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const repository = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..', '..');
const requireFromWeb = createRequire(path.join(repository, 'web/package.json'));
const sharp = requireFromWeb('sharp');
const conceptDirectory = path.join(
  repository,
  'art/class-look-concepts/arc-relay/meshy7-hover-roster-v1',
);
const candidateDirectory = path.join(conceptDirectory, 'meshy7-input-candidates');
const outputDirectory = path.join(conceptDirectory, 'meshy7-brighter-texture-inputs-v1');
const conceptManifestPath = path.join(conceptDirectory, 'manifest.json');
const materialPromptPath = path.join(conceptDirectory, 'CONCEPT-SHEET-V7-PROMPT.md');
const classOrder = [
  'kestrel',
  'palisade',
  'towline',
  'patchbay',
  'lantern',
  'mortar',
  'minesmith',
  'hush',
  'relay',
  'switchback',
  'longshot',
  'mason',
  'sunder',
  'repulsor',
  'veil',
  'nest',
];
const retained = new Set(['kestrel', 'palisade']);
const transform = {
  operation: 'per-channel linear tonal lift',
  multiplier: 0.88,
  offset: 25,
  rationale:
    'Lift shadows and midtones above approved Palisade while preserving near-white highlights and reducing contrast rather than increasing saturation.',
};
const conceptManifest = JSON.parse(readFileSync(conceptManifestPath, 'utf8'));
const materialPrompt = readFileSync(materialPromptPath, 'utf8');
const materialDirection = conceptManifest.identityRevision?.materialDirection;
if (
  typeof materialDirection !== 'string' ||
  !materialDirection.includes('powder-coated metal') ||
  !materialDirection.includes('satin roughness') ||
  !materialDirection.includes('no toy gloss') ||
  !materialPrompt.includes('matte/satin powder-coated metal') ||
  !materialPrompt.includes('glossy plastic') ||
  !materialPrompt.includes('wet highlights')
)
  throw new Error('The pinned V7 source no longer proves the matte/satin material direction.');

mkdirSync(outputDirectory, { recursive: true });
const entries = [];
for (const classId of classOrder) {
  const manifestPath = path.join(candidateDirectory, `${classId}-v1/manifest.json`);
  const sourceManifest = JSON.parse(readFileSync(manifestPath, 'utf8'));
  if (
    sourceManifest.classId !== classId ||
    sourceManifest.status !== 'owner-review' ||
    sourceManifest.meshyInputs?.length !== 4
  )
    throw new Error(`${classId}: invalid owning multiview manifest.`);
  const inputs = [];
  for (const source of [...sourceManifest.meshyInputs].sort((left, right) => left.order - right.order)) {
    const sourcePath = path.join(path.dirname(manifestPath), source.file);
    const sourceBytes = readFileSync(sourcePath);
    if (sourceBytes.length === 0 || sha256(sourceBytes) !== source.sha256)
      throw new Error(`${classId}/${source.view}: source PNG drifted.`);
    const sourceMetadata = await sharp(sourceBytes).metadata();
    if (sourceMetadata.width !== source.width || sourceMetadata.height !== source.height)
      throw new Error(`${classId}/${source.view}: source dimensions drifted.`);
    if (retained.has(classId)) {
      inputs.push({
        ...source,
        source: path.relative(repository, sourcePath),
        output: path.relative(repository, sourcePath),
        outputSha256: source.sha256,
        transformApplied: false,
      });
      continue;
    }
    const classDirectory = path.join(outputDirectory, classId);
    mkdirSync(classDirectory, { recursive: true });
    const outputPath = path.join(classDirectory, source.file);
    await sharp(sourceBytes)
      .linear(transform.multiplier, transform.offset)
      .png()
      .toFile(outputPath);
    const outputBytes = readFileSync(outputPath);
    const outputMetadata = await sharp(outputBytes).metadata();
    if (outputMetadata.width !== source.width || outputMetadata.height !== source.height)
      throw new Error(`${classId}/${source.view}: tonal derivative dimensions drifted.`);
    inputs.push({
      ...source,
      source: path.relative(repository, sourcePath),
      output: path.relative(repository, outputPath),
      outputSha256: sha256(outputBytes),
      transformApplied: true,
    });
  }
  entries.push({ classId, retainedUnchanged: retained.has(classId), inputs });
}

const palisade = entries.find((entry) => entry.classId === 'palisade');
const palisadeOblique = palisade.inputs.find((input) => input.view === 'front-right-three-quarter');
const palisadeValue = await objectValueAudit(path.join(repository, palisadeOblique.output));
for (const entry of entries.filter((candidate) => !candidate.retainedUnchanged)) {
  const oblique = entry.inputs.find((input) => input.view === 'front-right-three-quarter');
  entry.valueAudit = await objectValueAudit(path.join(repository, oblique.output));
  if (entry.valueAudit.medianLuminance <= palisadeValue.medianLuminance)
    throw new Error(`${entry.classId}: tonal derivative is not brighter than Palisade.`);
}

const sheet = await buildSheet(entries);
const manifest = {
  schemaVersion: 1,
  status: 'paid-retexture-inputs-authorized',
  purpose:
    'Ordered multiview texture authority for the fourteen non-Kestrel/non-Palisade Arc Relay retextures, with a restrained owner-directed value lift',
  materialPolicy: {
    finish: 'matte/satin high-roughness industrial surfaces',
    sourceDirection: materialDirection,
    promptLog: path.relative(repository, materialPromptPath),
    promptLogSha256: sha256(readFileSync(materialPromptPath)),
    prohibitedCues: [
      'glossy plastic',
      'clearcoat',
      'lacquer',
      'wet highlights',
      'toy gloss',
    ],
    transformEffect:
      'The deterministic value lift changes RGB tone only; it adds no saturation, synthetic highlight, reflection, or gloss cue.',
  },
  transform,
  retainedUnchanged: [...retained],
  brightnessReference: {
    classId: 'palisade',
    input: palisadeOblique.output,
    ...palisadeValue,
  },
  valueAuditMethod:
    '384x256 RGB sample; retain pixels with luminance below 205 or HSV saturation above 0.14; compare median Rec.709 luminance',
  entries,
  approvalSheet: {
    file: path.relative(repository, sheet),
    sha256: sha256(readFileSync(sheet)),
  },
  runtimeAssetsPromoted: false,
};
writeFileSync(
  path.join(outputDirectory, 'manifest.json'),
  `${JSON.stringify(manifest, null, 2)}\n`,
);
console.log(
  `Built ${entries.length - retained.size} brighter multiview style sets; ` +
    `all oblique medians exceed Palisade ${palisadeValue.medianLuminance.toFixed(1)}.`,
);

async function buildSheet(manifestEntries) {
  const tileWidth = 384;
  const tileHeight = 256;
  const labelHeight = 34;
  const titleHeight = 68;
  const gap = 4;
  const width = tileWidth * 4 + gap * 3;
  const height = titleHeight + (tileHeight + labelHeight + gap) * 4 - gap;
  const composites = [
    {
      input: Buffer.from(
        `<svg width="${width}" height="${titleHeight}" xmlns="http://www.w3.org/2000/svg">` +
          '<rect width="100%" height="100%" fill="#080d12"/>' +
          '<text x="16" y="28" fill="#e8eef2" font-size="19" font-family="monospace" font-weight="700">' +
          'ARC RELAY · MULTIVIEW TEXTURE AUTHORITY · BRIGHTER V1</text>' +
          '<text x="16" y="52" fill="#9fb0bb" font-size="14" font-family="monospace">' +
          'Kestrel + Palisade retained · remaining 14 receive one restrained tonal lift</text></svg>',
      ),
      left: 0,
      top: 0,
    },
  ];
  for (const [index, entry] of manifestEntries.entries()) {
    const column = index % 4;
    const row = Math.floor(index / 4);
    const left = column * (tileWidth + gap);
    const top = titleHeight + row * (tileHeight + labelHeight + gap);
    const oblique = entry.inputs.find((input) => input.view === 'front-right-three-quarter');
    composites.push({
      input: Buffer.from(
        `<svg width="${tileWidth}" height="${labelHeight}" xmlns="http://www.w3.org/2000/svg">` +
          '<rect width="100%" height="100%" fill="#10151a"/>' +
          `<text x="10" y="23" fill="#dbe7ef" font-size="16" font-family="monospace" font-weight="700">${entry.classId.toUpperCase()}</text>` +
          `<text x="374" y="23" text-anchor="end" fill="#81939f" font-size="12" font-family="monospace">${entry.retainedUnchanged ? 'RETAINED' : 'LIFTED'}</text></svg>`,
      ),
      left,
      top,
    });
    composites.push({
      input: await sharp(path.join(repository, oblique.output))
        .resize(tileWidth, tileHeight, { fit: 'cover' })
        .png()
        .toBuffer(),
      left,
      top: top + labelHeight,
    });
  }
  const sheetPath = path.join(outputDirectory, 'texture-authority-sheet.png');
  await sharp({ create: { width, height, channels: 4, background: '#080d12' } })
    .composite(composites)
    .png()
    .toFile(sheetPath);
  return sheetPath;
}

async function objectValueAudit(file) {
  const { data } = await sharp(file)
    .resize(384, 256)
    .removeAlpha()
    .raw()
    .toBuffer({ resolveWithObject: true });
  const values = [];
  for (let index = 0; index < data.length; index += 3) {
    const red = data[index];
    const green = data[index + 1];
    const blue = data[index + 2];
    const maximum = Math.max(red, green, blue);
    const minimum = Math.min(red, green, blue);
    const saturation = maximum === 0 ? 0 : (maximum - minimum) / maximum;
    const luminance = 0.2126 * red + 0.7152 * green + 0.0722 * blue;
    if (luminance < 205 || saturation > 0.14) values.push(luminance);
  }
  values.sort((left, right) => left - right);
  return {
    sampledObjectPixels: values.length,
    medianLuminance: values[Math.floor((values.length - 1) * 0.5)],
    upperQuartileLuminance: values[Math.floor((values.length - 1) * 0.75)],
    meanLuminance: values.reduce((sum, value) => sum + value, 0) / values.length,
  };
}

function sha256(value) {
  return createHash('sha256').update(value).digest('hex');
}
