#!/usr/bin/env node

import { createRequire } from 'node:module';
import { mkdir } from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const repository = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..', '..');
const requireFromWeb = createRequire(path.join(repository, 'web', 'package.json'));
const sharp = requireFromWeb('sharp');

const conceptDirectory = path.join(
  repository,
  'art/signature-models/arc-relay/gameplay-first-concepts-v2',
);
const providerInputDirectory = path.join(conceptDirectory, 'provider-inputs');

await mkdir(path.join(providerInputDirectory, 'trip-node'), { recursive: true });
await mkdir(path.join(providerInputDirectory, 'sentinel-seed'), { recursive: true });

const transparent = { r: 0, g: 0, b: 0, alpha: 0 };
const warmGray = { r: 226, g: 222, b: 216, alpha: 1 };

const tripTopTrimmed = await sharp(path.join(conceptDirectory, 'trip-node-top-source.png'))
  .rotate(270, { background: transparent })
  .trim({ background: transparent })
  .png()
  .toBuffer();
const sentinelTopTrimmed = await sharp(
  path.join(conceptDirectory, 'sentinel-seed-top-east-source.png'),
)
  .trim({ background: transparent })
  .png()
  .toBuffer();

await writeCanonicalTop(tripTopTrimmed, 'trip-node-top-east.png');
await writeCanonicalTop(sentinelTopTrimmed, 'sentinel-seed-top-east.png');

const panelSize = 627;
await writePanel('trip-node-provider-views.png', 0, 0, 'trip-node/front.png');
await writePanel('trip-node-provider-views.png', panelSize, 0, 'trip-node/right.png');
await writeTopProviderInput(tripTopTrimmed, 'trip-node/top.png');
await writePanel(
  'trip-node-provider-views.png',
  panelSize,
  panelSize,
  'trip-node/front-right-oblique.png',
);

await writePanel('sentinel-seed-provider-views-v2.png', 0, 0, 'sentinel-seed/front.png');
await writePanel(
  'sentinel-seed-provider-views-v2.png',
  panelSize,
  0,
  'sentinel-seed/right.png',
);
await writePanel(
  'sentinel-seed-provider-views-v2.png',
  0,
  panelSize,
  'sentinel-seed/back.png',
);
await writePanel(
  'sentinel-seed-provider-views-v2.png',
  panelSize,
  panelSize,
  'sentinel-seed/front-right-oblique.png',
);

await buildScaleProof();

console.log(
  'Built canonical top views, separate provider inputs, and gameplay-scale proof for Arc Relay signature concepts.',
);

async function writeCanonicalTop(trimmed, filename) {
  const subject = await sharp(trimmed)
    .resize(384, 384, { fit: 'inside', withoutEnlargement: true })
    .png()
    .toBuffer();
  const metadata = await sharp(subject).metadata();
  await sharp({
    create: { width: 512, height: 512, channels: 4, background: transparent },
  })
    .composite([
      {
        input: subject,
        left: Math.round((512 - metadata.width) / 2),
        top: Math.round((512 - metadata.height) / 2),
      },
    ])
    .png()
    .toFile(path.join(conceptDirectory, filename));
}

async function writeTopProviderInput(trimmed, relativeOutput) {
  const subject = await sharp(trimmed)
    .resize(900, 900, { fit: 'inside', withoutEnlargement: false })
    .png()
    .toBuffer();
  const metadata = await sharp(subject).metadata();
  await sharp({ create: { width: 1040, height: 1040, channels: 4, background: warmGray } })
    .composite([
      {
        input: subject,
        left: Math.round((1040 - metadata.width) / 2),
        top: Math.round((1040 - metadata.height) / 2),
      },
    ])
    .png()
    .toFile(path.join(providerInputDirectory, relativeOutput));
}

async function writePanel(sourceFilename, left, top, relativeOutput) {
  await sharp(path.join(conceptDirectory, sourceFilename))
    .extract({ left, top, width: panelSize, height: panelSize })
    .resize(1040, 1040, { fit: 'contain', background: warmGray })
    .png()
    .toFile(path.join(providerInputDirectory, relativeOutput));
}

async function buildScaleProof() {
  const width = 1500;
  const height = 880;
  const tile = 104;
  const tripLive = Math.round(tile * 0.44);
  const sentinelLive = Math.round(tile * 0.62);
  const tripLarge = await subjectAtWidth(tripTopTrimmed, 330);
  const sentinelLarge = await subjectAtWidth(sentinelTopTrimmed, 330);
  const trip48 = await subjectAtWidth(tripTopTrimmed, 48);
  const tripLiveImage = await subjectAtWidth(tripTopTrimmed, tripLive);
  const sentinel48 = await subjectAtWidth(sentinelTopTrimmed, 48);
  const sentinelLiveImage = await subjectAtWidth(sentinelTopTrimmed, sentinelLive);

  const tripMarkerCenter = { x: 112, y: 704 };
  const tripLiveCenter = { x: 330, y: 704 };
  const sentinelMarkerCenter = { x: 862, y: 704 };
  const sentinelLiveCenter = { x: 1080, y: 704 };

  const background = Buffer.from(`
    <svg width="${width}" height="${height}" xmlns="http://www.w3.org/2000/svg">
      <defs>
        <pattern id="grid" width="${tile}" height="${tile}" patternUnits="userSpaceOnUse">
          <rect width="${tile}" height="${tile}" fill="#17191a"/>
          <path d="M ${tile} 0 L 0 0 0 ${tile}" fill="none" stroke="#4b352d" stroke-width="2"/>
          <path d="M 10 16 L 88 8 M 18 92 L 96 76" stroke="#25211f" stroke-width="3" opacity="0.8"/>
        </pattern>
      </defs>
      <rect width="${width}" height="${height}" fill="#0b1115"/>
      <rect x="28" y="82" width="694" height="760" rx="12" fill="url(#grid)" stroke="#26333a"/>
      <rect x="778" y="82" width="694" height="760" rx="12" fill="url(#grid)" stroke="#26333a"/>
      <style>
        .title { fill: #e4eaed; font: 700 30px ui-monospace, SFMono-Regular, Menlo, monospace; }
        .name { fill: #eef2f4; font: 700 25px ui-monospace, SFMono-Regular, Menlo, monospace; }
        .label { fill: #aebbc1; font: 17px ui-monospace, SFMono-Regular, Menlo, monospace; }
        .note { fill: #829198; font: 15px ui-monospace, SFMono-Regular, Menlo, monospace; }
      </style>
      <text x="28" y="46" class="title">ARC RELAY SIGNATURE CONCEPTS · GAMEPLAY-SCALE GATE</text>
      <text x="52" y="119" class="name">TRIP NODE</text>
      <text x="802" y="119" class="name">SENTINEL SEED</text>
      <text x="52" y="151" class="label">broken triangle · floor trap · 0.44-tile target</text>
      <text x="802" y="151" class="label">fixed calyx base · yawing seed head · 0.62-tile target</text>
      <text x="111" y="547" class="label">large identity view</text>
      <text x="861" y="547" class="label">large identity view</text>
      <text x="92" y="640" class="label">48 px</text>
      <text x="286" y="640" class="label">live ≈ ${tripLive} px</text>
      <text x="842" y="640" class="label">48 px</text>
      <text x="1036" y="640" class="label">live ≈ ${sentinelLive} px</text>
      <text x="52" y="800" class="note">Goal: three broad jaws and open centre survive; no round-puck read.</text>
      <text x="802" y="800" class="note">Goal: four planted lobes survive; head remains visibly separate.</text>
      <text x="52" y="825" class="note">Scale is a 2D mockup on a ${tile}px tile, not real-renderer approval.</text>
      <text x="802" y="825" class="note">Runtime requires separate stationary-base and yaw-head nodes.</text>
    </svg>
  `);

  await sharp(background)
    .composite([
      { input: tripLarge, left: 210, top: 180 },
      { input: sentinelLarge, left: 960, top: 180 },
      {
        input: trip48,
        left: tripMarkerCenter.x - 24,
        top: tripMarkerCenter.y - 24,
      },
      {
        input: tripLiveImage,
        left: tripLiveCenter.x - Math.round(tripLive / 2),
        top: tripLiveCenter.y - Math.round(tripLive / 2),
      },
      {
        input: sentinel48,
        left: sentinelMarkerCenter.x - 24,
        top: sentinelMarkerCenter.y - 24,
      },
      {
        input: sentinelLiveImage,
        left: sentinelLiveCenter.x - Math.round(sentinelLive / 2),
        top: sentinelLiveCenter.y - Math.round(sentinelLive / 2),
      },
    ])
    .png()
    .toFile(path.join(conceptDirectory, 'gameplay-scale-proof.png'));
}

async function subjectAtWidth(trimmed, width) {
  return sharp(trimmed).resize({ width, withoutEnlargement: false }).png().toBuffer();
}
