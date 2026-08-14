#!/usr/bin/env node

import { createRequire } from 'node:module';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const repository = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..', '..');
const requireFromWeb = createRequire(path.join(repository, 'web', 'package.json'));
const sharp = requireFromWeb('sharp');

const conceptDirectory = path.join(
  repository,
  'art/class-look-concepts/arc-relay/longshot-redesign-v2',
);

const candidates = [
  {
    id: 'railback',
    label: '1 · RAILBACK',
    note: 'wide shoulders · exposed three-stage rail spine',
  },
  {
    id: 'hammerhead',
    label: '2 · HAMMERHEAD',
    note: 'broad charging brow · compact stepped crown',
  },
  {
    id: 'rail-citadel',
    label: '3 · RAIL CITADEL',
    note: 'near-square mass · raised accelerator bridge',
  },
];

const cardWidth = 600;
const headerHeight = 104;
const labelHeight = 76;
const obliqueHeight = 430;
const topHeight = 360;
const proofHeight = 150;
const boardWidth = cardWidth * candidates.length;
const boardHeight = headerHeight + labelHeight + obliqueHeight + topHeight + proofHeight;
const panelBackground = '#e2ded8';

const composites = [
  {
    input: headerSvg(boardWidth, headerHeight),
    left: 0,
    top: 0,
  },
];

for (const [index, candidate] of candidates.entries()) {
  const left = index * cardWidth;
  composites.push(
    {
      input: labelSvg(cardWidth, labelHeight, candidate.label, candidate.note),
      left,
      top: headerHeight,
    },
    {
      input: await prepareOblique(candidate.id),
      left: left + 10,
      top: headerHeight + labelHeight + 10,
    },
    {
      input: await prepareTop(candidate.id),
      left: left + 10,
      top: headerHeight + labelHeight + obliqueHeight + 10,
    },
    {
      input: proofBackground(cardWidth, proofHeight),
      left,
      top: headerHeight + labelHeight + obliqueHeight + topHeight,
    },
    {
      input: await prepareProof(candidate.id),
      left: left + 38,
      top: headerHeight + labelHeight + obliqueHeight + topHeight + 28,
    },
  );
}

await sharp({
  create: {
    width: boardWidth,
    height: boardHeight,
    channels: 4,
    background: '#080d12',
  },
})
  .composite(composites)
  .png()
  .toFile(path.join(conceptDirectory, 'concept-comparison.png'));

console.log(
  `Built ${path.relative(repository, path.join(conceptDirectory, 'concept-comparison.png'))}.`,
);

async function prepareOblique(id) {
  return sharp(path.join(conceptDirectory, `${id}-oblique.png`))
    .rotate(270, { background: panelBackground })
    .resize(cardWidth - 20, obliqueHeight - 20, {
      fit: 'contain',
      background: panelBackground,
    })
    .png()
    .toBuffer();
}

async function prepareTop(id) {
  return sharp(path.join(conceptDirectory, `${id}-top.png`))
    .resize(cardWidth - 20, topHeight - 20, {
      fit: 'contain',
      background: panelBackground,
    })
    .png()
    .toBuffer();
}

async function prepareProof(id) {
  return sharp(path.join(conceptDirectory, `${id}-top.png`))
    .resize(64, 64, {
      fit: 'contain',
      background: panelBackground,
    })
    .extend({
      top: 10,
      bottom: 10,
      left: 10,
      right: 10,
      background: '#17191c',
    })
    .png()
    .toBuffer();
}

function headerSvg(width, height) {
  return Buffer.from(`<svg xmlns="http://www.w3.org/2000/svg" width="${width}" height="${height}">
    <rect width="100%" height="100%" fill="#080d12"/>
    <text x="22" y="42" fill="#edf3f6" font-family="Menlo,monospace" font-size="29" font-weight="700">LONGSHOT REDESIGN V2 · CHUNKY SILHOUETTE GATE</text>
    <text x="22" y="76" fill="#9eb0ba" font-family="Menlo,monospace" font-size="17">same matte palette · oblique + strict top · front faces East/right → · no guns or team-colour paint</text>
    <line x1="0" y1="${height - 1}" x2="${width}" y2="${height - 1}" stroke="#263742"/>
  </svg>`);
}

function labelSvg(width, height, label, note) {
  return Buffer.from(`<svg xmlns="http://www.w3.org/2000/svg" width="${width}" height="${height}">
    <rect width="100%" height="100%" fill="#0d151d"/>
    <text x="16" y="31" fill="#edf3f6" font-family="Menlo,monospace" font-size="22" font-weight="700">${label}</text>
    <text x="16" y="57" fill="#9eb0ba" font-family="Menlo,monospace" font-size="14">${note}</text>
    <line x1="0" y1="${height - 1}" x2="${width}" y2="${height - 1}" stroke="#263742"/>
  </svg>`);
}

function proofBackground(width, height) {
  return Buffer.from(`<svg xmlns="http://www.w3.org/2000/svg" width="${width}" height="${height}">
    <defs>
      <pattern id="grid" width="32" height="32" patternUnits="userSpaceOnUse">
        <rect width="32" height="32" fill="#17191c"/>
        <path d="M 32 0 L 0 0 0 32" fill="none" stroke="#342923" stroke-width="1"/>
      </pattern>
    </defs>
    <rect width="100%" height="100%" fill="#0b1115"/>
    <rect x="22" y="14" width="116" height="116" rx="8" fill="url(#grid)" stroke="#31424d"/>
    <text x="158" y="55" fill="#d9e2e7" font-family="Menlo,monospace" font-size="18" font-weight="700">≈48 PX READ</text>
    <text x="158" y="82" fill="#9eb0ba" font-family="Menlo,monospace" font-size="14">broad body + three stages</text>
    <text x="158" y="105" fill="#9eb0ba" font-family="Menlo,monospace" font-size="14">must survive without detail</text>
    <line x1="${width - 1}" y1="0" x2="${width - 1}" y2="${height}" stroke="#263742"/>
  </svg>`);
}
