#!/usr/bin/env node

/**
 * Build the premium Arc Relay class-look packages from the approved chroma atlas.
 *
 * The generated illustration is kept under art/. This deterministic pass keys the
 * green screen, normalises every cell, separates cyan semantic light surfaces, and
 * emits a compact raster derivative, an alpha mask, and a layered source template.
 * The renderer composites the two images so team colour stays semantic without
 * duplicating base64 payloads inside the JavaScript bundle.
 *
 * Why a raster derivative: a reproducible genuine-vector fallback is archived beside
 * the atlas, but its flat/iconic finish did not clear gameplay art review. The visual
 * assets brief explicitly permits a PNG exception when gameplay-scale evidence is
 * recorded. The runtime remains one rotatable sprite per class; there are no frames.
 */

import { createRequire } from 'node:module';
import { mkdir, readFile, writeFile } from 'node:fs/promises';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const repositoryRoot = join(dirname(fileURLToPath(import.meta.url)), '..');
const requireFromWeb = createRequire(join(repositoryRoot, 'web', 'package.json'));
const sharp = requireFromWeb('sharp');
const sourceRoot = join(
  repositoryRoot,
  'art',
  'class-look-concepts',
  'arc-relay',
  'premium-roster-v1',
);
const atlasPath = join(sourceRoot, 'chroma-atlas.png');
const rasterMasterRoot = join(sourceRoot, 'raster-masters');
const classLookRoot = join(
  repositoryRoot,
  'web',
  'src',
  'assets',
  'class-looks',
);
const runtimeSize = 192;
const runtimeMaxWidth = 173;
const runtimeMaxHeight = 153;

const classes = [
  klass('kestrel', 'Kestrel', 'low-hover', 1.08),
  klass('palisade', 'Palisade', 'treads', 1.1),
  klass('towline', 'Towline', 'wheels', 1.07),
  klass('patchbay', 'Patchbay', 'skids', 1.04),
  klass('lantern', 'Lantern', 'low-hover', 1.03),
  klass('mortar', 'Mortar', 'treads', 1.09),
  klass('minesmith', 'Minesmith', 'wheels', 1.06),
  klass('hush', 'Hush', 'low-hover', 1.08),
  klass('relay', 'Relay', 'skids', 1.05),
  klass('switchback', 'Switchback', 'low-hover', 1.06),
  klass('longshot', 'Longshot', 'skids', 1.1),
  klass('mason', 'Mason', 'treads', 1.09),
  klass('sunder', 'Sunder', 'low-hover', 1.07),
  klass('repulsor', 'Repulsor', 'treads', 1.08),
  klass('veil', 'Veil', 'low-hover', 1.06),
  klass('nest', 'Nest', 'wheels', 1.08),
];

const atlas = sharp(atlasPath);
const metadata = await atlas.metadata();
if (!metadata.width || !metadata.height)
  throw new Error(`Could not read atlas dimensions: ${atlasPath}`);

await mkdir(rasterMasterRoot, { recursive: true });

for (const [index, entry] of classes.entries()) {
  const column = index % 4;
  const row = Math.floor(index / 4);
  const left = Math.round((column * metadata.width) / 4);
  const top = Math.round((row * metadata.height) / 4);
  const right = Math.round(((column + 1) * metadata.width) / 4);
  const bottom = Math.round(((row + 1) * metadata.height) / 4);
  const extracted = await sharp(atlasPath)
    .extract({ left, top, width: right - left, height: bottom - top })
    .ensureAlpha()
    .raw()
    .toBuffer({ resolveWithObject: true });
  const keyed = keyChroma(extracted.data, extracted.info);
  const normalised = await normaliseCell(keyed, extracted.info);
  const { base, mask } = separateTeamLights(
    normalised.data,
    normalised.info,
    entry.id,
  );
  const basePng = await sharp(base, {
    raw: { ...normalised.info, channels: 4 },
  })
    .png({ compressionLevel: 9, adaptiveFiltering: true })
    .toBuffer();
  const maskPng = await sharp(mask, {
    raw: { ...normalised.info, channels: 4 },
  })
    .png({ compressionLevel: 9, adaptiveFiltering: true, palette: true })
    .toBuffer();

  await writeFile(join(rasterMasterRoot, `${entry.id}-base.png`), basePng);
  await writeFile(join(rasterMasterRoot, `${entry.id}-team-mask.png`), maskPng);

  const packageId = `arc-${entry.id}`;
  const packageRoot = join(classLookRoot, packageId);
  await mkdir(packageRoot, { recursive: true });
  await writeFile(join(packageRoot, 'sprite.png'), basePng);
  await writeFile(join(packageRoot, 'team-mask.png'), maskPng);
  await writeFile(join(packageRoot, 'team.svg'), teamTemplate(entry), 'utf8');
  await writeFile(
    join(packageRoot, 'look.json'),
    `${JSON.stringify(
      {
        id: packageId,
        label: `Arc Relay ${entry.label}`,
        sprite: 'sprite.png',
        suggestedAccent: '#38bdf8',
        defaultProjectile: 'arc-pulse',
        classId: entry.id,
        locomotionCue: entry.locomotionCue,
        scale: entry.scale,
      },
      null,
      2,
    )}\n`,
    'utf8',
  );
}

function klass(id, label, locomotionCue, scale) {
  return { id, label, locomotionCue, scale };
}

function keyChroma(source, info) {
  const result = Buffer.alloc(info.width * info.height * 4);
  for (let offset = 0; offset < source.length; offset += 4) {
    const pixel = offset / 4;
    const x = pixel % info.width;
    const y = Math.floor(pixel / info.width);
    const outsideCellGutter =
      x < 6 || x >= info.width - 6 || y < 14 || y >= info.height - 24;
    const red = source[offset];
    const green = source[offset + 1];
    const blue = source[offset + 2];
    const sourceAlpha = source[offset + 3];
    const dominance = green - Math.max(red, blue);
    const greenConfidence =
      smoothstep(34, 126, dominance) * smoothstep(82, 184, green);
    const alpha = outsideCellGutter
      ? 0
      : Math.round(sourceAlpha * (1 - greenConfidence));
    const spill = greenConfidence * Math.max(0, dominance);
    const fringeSpill =
      smoothstep(7, 38, dominance) * smoothstep(48, 132, green);

    result[offset] = alpha < 3 ? 0 : red;
    result[offset + 1] =
      alpha < 3
        ? 0
        : Math.round(
            Math.max(
              0,
              green - spill * 0.92 - Math.max(0, dominance - 7) * fringeSpill,
            ),
          );
    result[offset + 2] = alpha < 3 ? 0 : blue;
    result[offset + 3] = alpha;
  }
  return result;
}

async function normaliseCell(source, info) {
  const trimmed = await sharp(source, {
    raw: { ...info, channels: 4 },
  })
    .trim({ background: '#00000000', threshold: 4 })
    .resize(runtimeMaxWidth, runtimeMaxHeight, { fit: 'inside' })
    .png({ compressionLevel: 9 })
    .toBuffer({ resolveWithObject: true });
  const left = Math.floor((runtimeSize - trimmed.info.width) / 2);
  const right = runtimeSize - trimmed.info.width - left;
  const top = Math.max(
    4,
    Math.floor((runtimeSize - trimmed.info.height) / 2) - 4,
  );
  const bottom = runtimeSize - trimmed.info.height - top;
  return sharp(trimmed.data)
    .extend({
      left,
      right,
      top,
      bottom,
      background: { r: 0, g: 0, b: 0, alpha: 0 },
    })
    .ensureAlpha()
    .raw()
    .toBuffer({ resolveWithObject: true });
}

function separateTeamLights(source, info, classId) {
  const base = Buffer.from(source);
  const mask = Buffer.alloc(info.width * info.height * 4);
  for (let offset = 0; offset < source.length; offset += 4) {
    let red = source[offset];
    let green = source[offset + 1];
    let blue = source[offset + 2];
    const alpha = source[offset + 3];
    if (classId === 'sunder') {
      const warmLead = red - Math.max(green, blue);
      const warmMaterial =
        smoothstep(18, 92, warmLead) *
        smoothstep(48, 170, red) *
        (1 - smoothstep(72, 148, green));
      const luminance = red * 0.2126 + green * 0.7152 + blue * 0.0722;
      red = mix(red, 42 + luminance * 0.42, warmMaterial);
      green = mix(green, 34 + luminance * 0.34, warmMaterial);
      blue = mix(blue, 54 + luminance * 0.54, warmMaterial);
      base[offset] = red;
      base[offset + 1] = green;
      base[offset + 2] = blue;
    }
    const coolLead = (green + blue) / 2 - red;
    const coolBrightness = Math.max(green, blue);
    const cyanBalance = 1 - Math.min(1, Math.abs(green - blue) / 118);
    const weight =
      smoothstep(18, 92, coolLead) *
      smoothstep(62, 176, coolBrightness) *
      (0.46 + cyanBalance * 0.54);
    const neutral = Math.round(
      22 + (red * 0.2126 + green * 0.7152 + blue * 0.0722) * 0.38,
    );

    base[offset] = mix(red, neutral, weight);
    base[offset + 1] = mix(green, neutral, weight);
    base[offset + 2] = mix(blue, neutral + 4, weight);

    mask[offset] = 255;
    mask[offset + 1] = 255;
    mask[offset + 2] = 255;
    mask[offset + 3] = Math.round(alpha * weight);
  }
  return { base, mask };
}

function teamTemplate(entry) {
  return `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 512 512" role="img" data-runtime-art="raster-exception" aria-label="${entry.label} facing East in a shallow oblique projection">
  <defs>
    <mask id="team-light-mask" maskUnits="userSpaceOnUse" x="0" y="0" width="512" height="512">
      <image href="__TEAM_MASK_URL__" x="0" y="0" width="512" height="512" preserveAspectRatio="none"/>
    </mask>
    <filter id="team-light-glow" x="-30%" y="-30%" width="160%" height="160%" color-interpolation-filters="sRGB">
      <feGaussianBlur in="SourceGraphic" stdDeviation="7" result="blur"/>
      <feComponentTransfer in="blur" result="soft"><feFuncA type="linear" slope=".62"/></feComponentTransfer>
      <feMerge><feMergeNode in="soft"/><feMergeNode in="SourceGraphic"/></feMerge>
    </filter>
  </defs>
  <g id="underbody-locomotion">
${indent(locomotion(entry.locomotionCue), 4)}
  </g>
  <g id="chassis">
    <image href="__BASE_IMAGE_URL__" x="0" y="0" width="512" height="512" preserveAspectRatio="none"/>
  </g>
  <g id="weapon-hardware">
    <desc>Signature hardware is integrated in the approved chassis illustration.</desc>
  </g>
  <g id="team-accents">
    <g mask="url(#team-light-mask)" filter="url(#team-light-glow)">
      <rect data-team-accent="true" fill="#38bdf8" x="0" y="0" width="512" height="256"/>
      <rect data-team-accent="true" fill="#38bdf8" x="0" y="256" width="512" height="256"/>
    </g>
  </g>
  <g id="emissives">
    <desc>Authored neutral emissives are integrated in the chassis illustration.</desc>
  </g>
</svg>
`;
}

function locomotion(cue) {
  if (cue === 'low-hover')
    return `<ellipse cx="256" cy="359" rx="128" ry="30" fill="#020509" fill-opacity=".42"/>
<ellipse cx="256" cy="347" rx="92" ry="17" fill="#d9f6ff" fill-opacity=".08"/>`;
  if (cue === 'treads')
    return '<ellipse cx="256" cy="357" rx="142" ry="29" fill="#020509" fill-opacity=".5"/>';
  if (cue === 'wheels')
    return '<ellipse cx="256" cy="354" rx="132" ry="27" fill="#020509" fill-opacity=".48"/>';
  return '<ellipse cx="256" cy="351" rx="122" ry="24" fill="#020509" fill-opacity=".44"/>';
}

function smoothstep(low, high, value) {
  const normalised = Math.max(0, Math.min(1, (value - low) / (high - low)));
  return normalised * normalised * (3 - 2 * normalised);
}

function mix(from, to, weight) {
  return Math.round(from + (to - from) * weight);
}

function indent(source, spaces) {
  const prefix = ' '.repeat(spaces);
  return source
    .split('\n')
    .map((line) => `${prefix}${line}`)
    .join('\n');
}
