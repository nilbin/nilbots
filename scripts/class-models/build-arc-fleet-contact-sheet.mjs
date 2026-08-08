#!/usr/bin/env node

import { createRequire } from 'node:module';
import { existsSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const repository = path.resolve(
  path.dirname(fileURLToPath(import.meta.url)),
  '..',
  '..',
);
const requireFromWeb = createRequire(path.join(repository, 'web', 'package.json'));
const sharp = requireFromWeb('sharp');
const reviewDirectory = path.resolve(
  repository,
  process.argv[2] ??
    'art/class-models/provider-runs/meshy/arc-fleet-review',
);
const stillDirectory = path.join(reviewDirectory, 'stills');
const teamZero = [
  'kestrel',
  'palisade',
  'towline',
  'patchbay',
  'lantern',
  'mortar',
  'minesmith',
  'hush',
];
const teamOne = [
  'relay',
  'switchback',
  'longshot',
  'mason',
  'sunder',
  'repulsor',
  'veil',
  'nest',
];

await buildCandidateSheet([...teamZero, ...teamOne]);
await buildComparisonSheet('team0-2d-vs-3d.png', teamZero);
await buildComparisonSheet('team1-2d-vs-3d.png', teamOne);
console.log(`Built Arc fleet contact sheets in ${reviewDirectory}.`);

async function buildCandidateSheet(classes) {
  const columns = 2;
  const cardWidth = 440;
  const labelHeight = 38;
  const imageHeight = 220;
  const headerHeight = 58;
  const rows = Math.ceil(classes.length / columns);
  const composites = [
    {
      input: labelSvg(
        columns * cardWidth,
        headerHeight,
        'MESHY T2 CANDIDATES · REAL REPLAY SCALE · AMBER / CYAN',
        22,
      ),
      left: 0,
      top: 0,
    },
  ];
  for (const [index, classId] of classes.entries()) {
    const left = (index % columns) * cardWidth;
    const top = headerHeight + Math.floor(index / columns) * (labelHeight + imageHeight);
    composites.push(
      {
        input: labelSvg(cardWidth, labelHeight, title(classId), 19),
        left,
        top,
      },
      {
        input: requiredStill(classId, '3d'),
        left,
        top: top + labelHeight,
      },
    );
  }
  await blank(columns * cardWidth, headerHeight + rows * (labelHeight + imageHeight))
    .composite(composites)
    .png()
    .toFile(path.join(reviewDirectory, 'fleet-candidates-3d.png'));
}

async function buildComparisonSheet(filename, classes) {
  const imageWidth = 440;
  const labelHeight = 38;
  const imageHeight = 220;
  const headerHeight = 58;
  const composites = [
    {
      input: labelSvg(
        imageWidth * 2,
        headerHeight,
        'CANONICAL 2D (LEFT) · MESHY T2 3D (RIGHT) · SAME REPLAY SCALE',
        21,
      ),
      left: 0,
      top: 0,
    },
  ];
  for (const [index, classId] of classes.entries()) {
    const top = headerHeight + index * (labelHeight + imageHeight);
    composites.push(
      {
        input: labelSvg(imageWidth * 2, labelHeight, title(classId), 19),
        left: 0,
        top,
      },
      {
        input: requiredStill(classId, '2d'),
        left: 0,
        top: top + labelHeight,
      },
      {
        input: requiredStill(classId, '3d'),
        left: imageWidth,
        top: top + labelHeight,
      },
    );
  }
  await blank(imageWidth * 2, headerHeight + classes.length * (labelHeight + imageHeight))
    .composite(composites)
    .png()
    .toFile(path.join(reviewDirectory, filename));
}

function blank(width, height) {
  return sharp({
    create: {
      width,
      height,
      channels: 4,
      background: '#080d12',
    },
  });
}

function requiredStill(classId, renderer) {
  const filename = path.join(stillDirectory, `${classId}-${renderer}.png`);
  if (!existsSync(filename)) throw new Error(`Missing capture ${filename}.`);
  return filename;
}

function labelSvg(width, height, text, fontSize) {
  const safe = text.replaceAll('&', '&amp;').replaceAll('<', '&lt;');
  return Buffer.from(`<svg xmlns="http://www.w3.org/2000/svg" width="${width}" height="${height}">
    <rect width="100%" height="100%" fill="#0d151d"/>
    <line x1="0" y1="${height - 1}" x2="${width}" y2="${height - 1}" stroke="#20303c"/>
    <text x="14" y="${Math.round(height * 0.68)}" fill="#dbe7ef" font-family="Menlo,monospace" font-size="${fontSize}" font-weight="700">${safe}</text>
  </svg>`);
}

function title(value) {
  return value.replace(/(^|-)([a-z])/g, (_match, separator, letter) =>
    `${separator ? ' ' : ''}${letter.toUpperCase()}`,
  );
}
