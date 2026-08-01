#!/usr/bin/env node

import { createRequire } from 'node:module';
import { readFile, writeFile } from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const repositoryRoot = path.resolve(
  path.dirname(fileURLToPath(import.meta.url)),
  '..',
);
const requireFromWeb = createRequire(
  path.join(repositoryRoot, 'web', 'package.json'),
);
const sharp = requireFromWeb('sharp');
const classes = [
  'kestrel', 'palisade', 'towline', 'patchbay',
  'lantern', 'mortar', 'minesmith', 'hush',
  'relay', 'switchback', 'longshot', 'mason',
  'sunder', 'repulsor', 'veil', 'nest',
];
const width = 2048;
const height = 2048;
const cell = width / 4;
const background = `<svg xmlns="http://www.w3.org/2000/svg" width="${width}" height="${height}">
  <defs>
    <radialGradient id="field" cx="50%" cy="34%" r="76%">
      <stop offset="0" stop-color="#182431"/><stop offset="1" stop-color="#070b10"/>
    </radialGradient>
    <pattern id="grid" width="32" height="28.8" patternUnits="userSpaceOnUse">
      <path d="M32 0H0V28.8" fill="none" stroke="#9dc2d7" stroke-opacity=".055" stroke-width="1"/>
    </pattern>
  </defs>
  <rect width="2048" height="2048" fill="url(#field)"/>
  <rect width="2048" height="2048" fill="url(#grid)"/>
  ${classes.map((name, index) => {
    const x = (index % 4) * cell;
    const y = Math.floor(index / 4) * cell;
    return `<g>
      <rect x="${x + 12}" y="${y + 12}" width="${cell - 24}" height="${cell - 24}" rx="28" fill="#0b1118" fill-opacity=".48" stroke="#b7d2df" stroke-opacity=".14"/>
      <text x="${x + 34}" y="${y + 52}" fill="#edf5f8" font-family="ui-monospace,Menlo,monospace" font-size="24" font-weight="700" letter-spacing="3">${name.toUpperCase()}</text>
      <text x="${x + 34}" y="${y + 82}" fill="#8ea7b5" font-family="ui-monospace,Menlo,monospace" font-size="15" letter-spacing="2">EAST · 20° OBLIQUE</text>
      <ellipse cx="${x + 146}" cy="${y + 390}" rx="104" ry="22" fill="#000" fill-opacity=".5"/>
      <ellipse cx="${x + 366}" cy="${y + 390}" rx="104" ry="22" fill="#000" fill-opacity=".5"/>
    </g>`;
  }).join('')}
</svg>`;

const composites = [];
for (const [index, classId] of classes.entries()) {
  const lookRoot = path.join(
    repositoryRoot,
    'web/src/assets/class-looks',
    `arc-${classId}`,
  );
  const manifest = JSON.parse(
    await readFile(path.join(lookRoot, 'look.json'), 'utf8'),
  );
  const source = manifest.sprite.endsWith('.png')
    ? await rasterTemplate(lookRoot, manifest.sprite)
    : await readFile(path.join(lookRoot, manifest.sprite), 'utf8');
  const left = (index % 4) * cell;
  const top = Math.floor(index / 4) * cell;
  for (const [team, accent, offset] of [
    ['cyan', '#38bdf8', 36],
    ['red', '#fb4f5d', 256],
  ]) {
    const tinted = applyAccent(source, accent);
    const input = await sharp(Buffer.from(tinted))
      .resize(220, 198, { fit: 'fill' })
      .png()
      .toBuffer();
    composites.push({ input, left: left + offset, top: top + 174 });
    composites.push({
      input: Buffer.from(
        `<svg width="220" height="24"><text x="110" y="18" text-anchor="middle" fill="${accent}" font-family="ui-monospace,Menlo,monospace" font-size="15" font-weight="700" letter-spacing="2">${team.toUpperCase()}</text></svg>`,
      ),
      left: left + offset,
      top: top + 420,
    });
  }
}

const output = await sharp(Buffer.from(background))
  .composite(composites)
  .png({ compressionLevel: 9 })
  .toBuffer();
await writeFile(
  path.join(repositoryRoot, 'arc-relay-class-contact-sheet.png'),
  output,
);

function applyAccent(source, accent) {
  return source.replace(
    /<[^>]+\bdata-team-accent="true"[^>]*>/gi,
    (element) =>
      element
        .replace(/\bfill="(?!none\b)[^"]*"/gi, `fill="${accent}"`)
        .replace(/\bstroke="(?!none\b)[^"]*"/gi, `stroke="${accent}"`),
  );
}

async function rasterTemplate(lookRoot, sprite) {
  const [template, base, mask] = await Promise.all([
    readFile(path.join(lookRoot, 'team.svg'), 'utf8'),
    readFile(path.join(lookRoot, sprite)),
    readFile(path.join(lookRoot, 'team-mask.png')),
  ]);
  return template
    .replace(
      '__BASE_IMAGE_URL__',
      `data:image/png;base64,${base.toString('base64')}`,
    )
    .replace(
      '__TEAM_MASK_URL__',
      `data:image/png;base64,${mask.toString('base64')}`,
    );
}
