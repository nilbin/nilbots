#!/usr/bin/env node

import { createHash } from 'node:crypto';
import { createRequire } from 'node:module';
import { mkdir, readFile, writeFile } from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { readGlb, triangleCount } from './model-memory.mjs';

const repository = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..', '..');
const requireFromWeb = createRequire(path.join(repository, 'web', 'package.json'));
const sharp = requireFromWeb('sharp');
const outputDirectory = path.join(
  repository,
  'art/class-models/provider-runs/meshy/arc-relay-meshy7-fleet',
);
const inputRoot = path.join(
  repository,
  'art/class-look-concepts/arc-relay/meshy7-hover-roster-v1/meshy7-input-candidates',
);
const classes = [
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

await mkdir(outputDirectory, { recursive: true });
const entries = [];
for (const classId of classes) entries.push(await inspectClass(classId));

const audit = {
  schemaVersion: 1,
  status: 'owner-review',
  generatedAt: new Date().toISOString(),
  purpose: 'Raw Meshy 7 fleet provider audit; no runtime assets promoted',
  fleet: {
    classes: entries.length,
    successfulTasks: entries.filter((entry) => entry.status === 'SUCCEEDED').length,
    remainingFleetTasks: 15,
    remainingFleetCredits: entries
      .filter((entry) => entry.classId !== 'kestrel')
      .reduce((sum, entry) => sum + entry.consumedCredits, 0),
    balanceBefore: 2180,
    balanceAfter: 1730,
    autoRigging: false,
  },
  entries,
};
await writeFile(
  path.join(outputDirectory, 'fleet-audit.json'),
  `${JSON.stringify(audit, null, 2)}\n`,
);
await buildContactSheet(entries);
await writeFile(
  path.join(outputDirectory, 'REVIEW.md'),
  `# Meshy 7 Arc Relay fleet provider review\n\n` +
    `- Status: owner review only\n` +
    `- Classes represented: ${entries.length}\n` +
    `- Remaining-fleet tasks succeeded: ${audit.fleet.successfulTasks - 1}/15\n` +
    `- Remaining-fleet credits consumed: ${audit.fleet.remainingFleetCredits}\n` +
    `- Balance: ${audit.fleet.balanceBefore} before, ${audit.fleet.balanceAfter} after\n` +
    `- Auto-rigging: disabled\n` +
    `- Runtime assets promoted: no\n\n` +
    `\`provider-contact-sheet.png\` uses the selected retextured Kestrel preview and the 15 ` +
    `new raw Meshy 7 provider previews in the approved 4x4 roster order. Raw provider GLBs ` +
    `remain review masters and are not mobile shipping assets.\n`,
);

console.log(
  `Audited ${entries.length} classes; remaining fleet consumed ` +
    `${audit.fleet.remainingFleetCredits} credits.`,
);

async function inspectClass(classId) {
  let manifest;
  let manifestPath;
  if (classId === 'kestrel') {
    manifestPath = path.join(inputRoot, 'kestrel-v1', 'manifest.json');
    manifest = JSON.parse(await readFile(manifestPath, 'utf8'));
  } else {
    manifestPath = path.join(inputRoot, `${classId}-v1`, 'manifest.json');
    manifest = JSON.parse(await readFile(manifestPath, 'utf8'));
  }
  const run = classId === 'kestrel' ? manifest.retextureRun : manifest.providerRun;
  if (!run || run.status !== 'SUCCEEDED') throw new Error(`${classId}: provider run is incomplete.`);
  const runDirectory = path.resolve(path.dirname(manifestPath), run.directory);
  const timing = JSON.parse(await readFile(path.join(runDirectory, 'timing.json'), 'utf8'));
  const artifacts = JSON.parse(await readFile(path.join(runDirectory, 'artifacts.json'), 'utf8'));
  const requestRecordText = await readFile(path.join(runDirectory, 'request-record.json'), 'utf8');
  const taskText = await readFile(path.join(runDirectory, 'task.sanitized.json'), 'utf8');
  if (/data:image|Bearer\s|meshy_sk_|[?&](?:X-Amz-|token=|signature=)/i.test(requestRecordText + taskText))
    throw new Error(`${classId}: retained provider record appears to contain sensitive input data.`);
  const modelPath = path.join(runDirectory, 'raw-model.glb');
  const model = await readFile(modelPath);
  const artifact = artifacts.find((entry) => entry.file === 'raw-model.glb');
  const modelSha256 = sha256(model);
  if (!artifact || artifact.sha256 !== modelSha256 || run.rawModelSha256 !== modelSha256)
    throw new Error(`${classId}: raw GLB provenance hash mismatch.`);
  const { document } = readGlb(model, `${classId} raw Meshy 7 model`);
  return {
    classId,
    taskId: run.taskId,
    endpoint: classId === 'kestrel' ? 'retexture' : 'multi-image-to-3d',
    status: timing.status,
    consumedCredits: timing.consumedCredits,
    balanceBefore: timing.balanceBefore,
    balanceAfter: timing.balanceAfter,
    totalSeconds: timing.totalSeconds,
    runDirectory: path.relative(repository, runDirectory),
    preview: path.relative(repository, path.join(runDirectory, 'provider-preview.png')),
    rawModel: {
      file: path.relative(repository, modelPath),
      bytes: model.length,
      sha256: modelSha256,
      triangles: triangleCount(document),
      meshes: document.meshes?.length ?? 0,
      materials: document.materials?.length ?? 0,
      textures: document.textures?.length ?? 0,
      imageMimeTypes: [...new Set((document.images ?? []).map((image) => image.mimeType ?? 'unknown'))],
    },
  };
}

async function buildContactSheet(values) {
  const columns = 4;
  const rows = 4;
  const cardWidth = 360;
  const cardHeight = 360;
  const labelHeight = 42;
  const canvas = sharp({
    create: {
      width: columns * cardWidth,
      height: rows * cardHeight,
      channels: 4,
      background: '#0a1016',
    },
  });
  const composites = [];
  for (const [index, entry] of values.entries()) {
    const left = (index % columns) * cardWidth;
    const top = Math.floor(index / columns) * cardHeight;
    const preview = await sharp(path.join(repository, entry.preview))
      .resize({
        width: cardWidth - 16,
        height: cardHeight - labelHeight - 16,
        fit: 'contain',
        background: '#101820',
      })
      .png()
      .toBuffer();
    composites.push(
      { input: preview, left: left + 8, top: top + 8 },
      {
        input: labelSvg(cardWidth - 16, labelHeight - 8, title(entry.classId)),
        left: left + 8,
        top: top + cardHeight - labelHeight,
      },
    );
  }
  await canvas
    .composite(composites)
    .png({ compressionLevel: 9 })
    .toFile(path.join(outputDirectory, 'provider-contact-sheet.png'));
}

function labelSvg(width, height, label) {
  const safe = label.replaceAll('&', '&amp;').replaceAll('<', '&lt;');
  return Buffer.from(`<svg xmlns="http://www.w3.org/2000/svg" width="${width}" height="${height}">
    <rect width="100%" height="100%" rx="7" fill="#15222d"/>
    <text x="${width / 2}" y="${Math.round(height * 0.68)}" fill="#e8eef2" text-anchor="middle" font-family="Menlo,monospace" font-size="18" font-weight="700">${safe}</text>
  </svg>`);
}

function title(value) {
  return value.replace(/(^|-)([a-z])/g, (_match, separator, letter) =>
    `${separator ? ' ' : ''}${letter.toUpperCase()}`,
  );
}

function sha256(value) {
  return createHash('sha256').update(value).digest('hex');
}
