#!/usr/bin/env node
'use strict';

/**
 * Build the hard Aegis family gate from renders produced by build_aegis_forms.py.
 *
 * Sharp is deliberately injected as argv[2] instead of installed in this worktree.
 * The repository's tracked output remains independent of a developer-local module path:
 *
 *   node audit_family_board.cjs /absolute/path/to/node_modules/sharp
 */

const crypto = require('node:crypto');
const fs = require('node:fs');
const path = require('node:path');

const sharpPath = process.argv[2];
if (!sharpPath || !path.isAbsolute(sharpPath))
  throw new Error('Pass the absolute path to the installed sharp package.');
const sharp = require(sharpPath);

const root = __dirname;
const review = path.join(root, 'review');
const output = path.join(review, 'aegis-family-hard-gate.png');
const reportPath = path.join(root, 'reports', 'aegis-family-hard-gate.json');

const assets = [
  {
    id: 'aegis-tortoise',
    label: 'MOBILE AUTHORITY',
    blue: path.join(
      review,
      'family-reference',
      'aegis-tortoise-mobile-gameplay-blue.png',
    ),
    red: path.join(
      review,
      'family-reference',
      'aegis-tortoise-mobile-gameplay-red.png',
    ),
  },
  {
    id: 'aegis-tortoise-shell',
    label: 'SHELL',
    blue: path.join(review, 'aegis-tortoise-shell', 'gameplay-blue.png'),
    red: path.join(review, 'aegis-tortoise-shell', 'gameplay-red.png'),
  },
  {
    id: 'aegis-tortoise-turret',
    label: 'TURRET',
    blue: path.join(review, 'aegis-tortoise-turret', 'gameplay-blue.png'),
    red: path.join(review, 'aegis-tortoise-turret', 'gameplay-red.png'),
  },
];

const WIDTH = 1620;
const COLUMN = 540;
const HEADER = 70;
const REVIEW_FRAME = 512;
const REVIEW_ROW = 540;
const GAME_FRAME = 192;
const GAME_ROW = 285;
const HEIGHT = HEADER + REVIEW_ROW * 2 + GAME_ROW;
const BACKGROUND = '#0b1020';

function escapeXml(value) {
  return value.replace(/[<>&'"]/g, (character) => ({
    '<': '&lt;',
    '>': '&gt;',
    '&': '&amp;',
    "'": '&apos;',
    '"': '&quot;',
  })[character]);
}

function svgOverlay() {
  const labels = assets
    .map(({ label }, index) => {
      const centre = index * COLUMN + COLUMN / 2;
      return `<text x="${centre}" y="42" text-anchor="middle">${escapeXml(label)}</text>`;
    })
    .join('');
  const dividers = [COLUMN, COLUMN * 2]
    .map((x) => `<path d="M ${x} 0 V ${HEIGHT}" class="divider"/>`)
    .join('');
  return Buffer.from(`
    <svg xmlns="http://www.w3.org/2000/svg" width="${WIDTH}" height="${HEIGHT}">
      <style>
        text { fill:#dce8f7; font:700 22px -apple-system,BlinkMacSystemFont,"Segoe UI",sans-serif; letter-spacing:2px }
        .tag { font-size:16px; letter-spacing:1px; fill:#dce8f7 }
        .note { font-size:15px; font-weight:600; letter-spacing:.5px; fill:#92a7bf }
        .divider { stroke:#405068; stroke-width:2 }
        .rule { stroke:#405068; stroke-width:2 }
      </style>
      <rect width="${WIDTH}" height="${HEIGHT}" fill="${BACKGROUND}"/>
      <rect y="${HEADER}" width="${WIDTH}" height="${REVIEW_ROW}" fill="#151d29"/>
      <rect y="${HEADER + REVIEW_ROW}" width="${WIDTH}" height="${REVIEW_ROW}" fill="#111923"/>
      <rect y="${HEADER + REVIEW_ROW * 2}" width="${WIDTH}" height="${GAME_ROW}" fill="#0d141e"/>
      ${labels}
      ${dividers}
      <path d="M 0 ${HEADER} H ${WIDTH}" class="rule"/>
      <path d="M 0 ${HEADER + REVIEW_ROW} H ${WIDTH}" class="rule"/>
      <path d="M 0 ${HEADER + REVIEW_ROW * 2} H ${WIDTH}" class="rule"/>
      <text x="22" y="${HEADER + 30}" class="tag">BLUE TEAM</text>
      <text x="22" y="${HEADER + REVIEW_ROW + 30}" class="tag">RED TEAM</text>
      <text x="22" y="${HEADER + REVIEW_ROW * 2 + 30}" class="tag">GAME-SIZE GATE</text>
      <text x="22" y="${HEIGHT - 18}" class="note">192 px complete render frames at native resolution · same camera, lights, floor, exposure, and one-tile fit</text>
    </svg>
  `);
}

function sha256(file) {
  return crypto.createHash('sha256').update(fs.readFileSync(file)).digest('hex');
}

async function diffMetrics(bluePath, redPath, size) {
  const blue = await sharp(bluePath)
    .resize(size, size, { fit: 'fill', kernel: sharp.kernel.lanczos3 })
    .removeAlpha()
    .raw()
    .toBuffer();
  const red = await sharp(redPath)
    .resize(size, size, { fit: 'fill', kernel: sharp.kernel.lanczos3 })
    .removeAlpha()
    .raw()
    .toBuffer();
  if (blue.length !== red.length) throw new Error('Team render sizes differ.');

  let changed4 = 0;
  let changed16 = 0;
  let sum = 0;
  let maximum = 0;
  let minX = size;
  let minY = size;
  let maxX = -1;
  let maxY = -1;
  for (let pixel = 0; pixel < size * size; pixel += 1) {
    const offset = pixel * 3;
    const delta = Math.max(
      Math.abs(blue[offset] - red[offset]),
      Math.abs(blue[offset + 1] - red[offset + 1]),
      Math.abs(blue[offset + 2] - red[offset + 2]),
    );
    sum += delta;
    maximum = Math.max(maximum, delta);
    if (delta > 4) {
      changed4 += 1;
      const x = pixel % size;
      const y = Math.floor(pixel / size);
      minX = Math.min(minX, x);
      minY = Math.min(minY, y);
      maxX = Math.max(maxX, x);
      maxY = Math.max(maxY, y);
    }
    if (delta > 16) changed16 += 1;
  }
  const pixels = size * size;
  return {
    frameSize: [size, size],
    pixels,
    changedOver4: changed4,
    changedOver4Percent: Number(((changed4 / pixels) * 100).toFixed(6)),
    changedOver16: changed16,
    changedOver16Percent: Number(((changed16 / pixels) * 100).toFixed(6)),
    meanMaxChannelDelta255: Number((sum / pixels).toFixed(6)),
    maximumChannelDelta255: maximum,
    changedBoundsOver4:
      maxX < 0 ? null : { minimum: [minX, minY], maximum: [maxX, maxY] },
  };
}

async function frame(file, size) {
  return sharp(file)
    .resize(size, size, { fit: 'fill', kernel: sharp.kernel.lanczos3 })
    .png({ compressionLevel: 9, adaptiveFiltering: false })
    .toBuffer();
}

async function main() {
  for (const asset of assets)
    for (const file of [asset.blue, asset.red])
      if (!fs.existsSync(file)) throw new Error(`Missing family-gate input: ${file}`);

  const composites = [{ input: svgOverlay(), left: 0, top: 0 }];
  for (const [index, asset] of assets.entries()) {
    const x = index * COLUMN + Math.floor((COLUMN - REVIEW_FRAME) / 2);
    composites.push({
      input: await frame(asset.blue, REVIEW_FRAME),
      left: x,
      top: HEADER + 18,
    });
    composites.push({
      input: await frame(asset.red, REVIEW_FRAME),
      left: x,
      top: HEADER + REVIEW_ROW + 18,
    });

    const gameTop = HEADER + REVIEW_ROW * 2 + 48;
    const gamePairWidth = GAME_FRAME * 2 + 18;
    const gameLeft = index * COLUMN + Math.floor((COLUMN - gamePairWidth) / 2);
    composites.push({
      input: await frame(asset.blue, GAME_FRAME),
      left: gameLeft,
      top: gameTop,
    });
    composites.push({
      input: await frame(asset.red, GAME_FRAME),
      left: gameLeft + GAME_FRAME + 18,
      top: gameTop,
    });
  }

  fs.mkdirSync(path.dirname(output), { recursive: true });
  await sharp({
    create: {
      width: WIDTH,
      height: HEIGHT,
      channels: 4,
      background: BACKGROUND,
    },
  })
    .composite(composites)
    .png({ compressionLevel: 9, adaptiveFiltering: false })
    .toFile(output);

  const report = {
    schemaVersion: 1,
    verdict: 'hard-family-gate-evidence',
    authority: 'approved authored mobile Aegis rendered by the same Blender review rig',
    board: {
      file: path.relative(root, output),
      dimensions: [WIDTH, HEIGHT],
      bytes: fs.statSync(output).size,
      sha256: sha256(output),
      fullReviewFramePixels: REVIEW_FRAME,
      gameSizeFramePixels: GAME_FRAME,
      resamplingKernel: 'Lanczos3',
    },
    rigInvariant:
      'All six source renders use the exact same camera, lights, floor, exposure, team colours, and one-tile fit from build_aegis_forms.py.',
    assets: [],
  };
  for (const asset of assets) {
    report.assets.push({
      id: asset.id,
      blue: {
        file: path.relative(root, asset.blue),
        sha256: sha256(asset.blue),
      },
      red: {
        file: path.relative(root, asset.red),
        sha256: sha256(asset.red),
      },
      teamDiffFull: await diffMetrics(asset.blue, asset.red, 768),
      teamDiffGameSize: await diffMetrics(asset.blue, asset.red, GAME_FRAME),
    });
  }
  fs.mkdirSync(path.dirname(reportPath), { recursive: true });
  fs.writeFileSync(reportPath, `${JSON.stringify(report, null, 2)}\n`);
  process.stdout.write(`${JSON.stringify(report.board, null, 2)}\n`);
}

main().catch((error) => {
  console.error(error);
  process.exitCode = 1;
});
