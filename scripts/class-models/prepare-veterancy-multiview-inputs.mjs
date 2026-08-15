#!/usr/bin/env node

import { createHash } from 'node:crypto';
import { mkdir, readFile, writeFile } from 'node:fs/promises';
import { createRequire } from 'node:module';
import { join, relative, resolve } from 'node:path';

const repositoryRoot = resolve(import.meta.dirname, '../..');
const requireFromWeb = createRequire(resolve(repositoryRoot, 'web/package.json'));
const sharp = requireFromWeb('sharp');

const providerRoot = resolve(
  repositoryRoot,
  'art/class-models/provider-runs/meshy/arc-veterancy-modules-v1',
);
const inputsRoot = join(providerRoot, 'inputs');
const reviewRoot = resolve(
  repositoryRoot,
  'art/reviews/arc-relay-veterancy-modules-meshy7-v1',
);

const sourceLayout = {
  size: 1254,
  margin: 16,
  gutter: 16,
  panel: 603,
};
const outputSize = 1254;
const modules = [
  {
    id: 'veterancy-crown-spine',
    label: 'CROWN SPINE',
    rootIntent: 'centered dorsal part; split flat underside pads mount to the chassis',
  },
  {
    id: 'veterancy-swept-blade',
    label: 'SWEPT BLADE',
    rootIntent: 'flat underside and recessed inner edge mount to one chassis side; +X is the narrow forward end',
  },
  {
    id: 'veterancy-square-mantle',
    label: 'SQUARE MANTLE',
    rootIntent: 'notched open edge faces inward; flat underside mounts to one chassis side',
  },
  {
    id: 'veterancy-radial-petal',
    label: 'RADIAL PETAL',
    rootIntent: 'narrow inner root points toward the chassis center; broad clipped cap points outward',
  },
  {
    id: 'veterancy-curved-rail',
    label: 'CURVED RAIL',
    rootIntent: 'flat underside and capped-end pads mount around open chassis space',
  },
];
const views = [
  { id: 'top', label: 'TOP', column: 0, row: 0, file: '01-top.png' },
  { id: 'side', label: 'SIDE', column: 1, row: 0, file: '02-side.png' },
  {
    id: 'front-three-quarter',
    label: 'FRONT 3/4',
    column: 0,
    row: 1,
    file: '03-front-three-quarter.png',
  },
  {
    id: 'rear-three-quarter',
    label: 'REAR 3/4',
    column: 1,
    row: 1,
    file: '04-rear-three-quarter.png',
  },
];

await mkdir(reviewRoot, { recursive: true });
const metrics = [];

for (const module of modules) {
  const moduleDirectory = join(inputsRoot, module.id);
  const sourcePath = join(moduleDirectory, 'source-sheet.png');
  const sourceMetadata = await sharp(sourcePath).metadata();
  if (
    sourceMetadata.width !== sourceLayout.size ||
    sourceMetadata.height !== sourceLayout.size
  )
    throw new Error(
      `${relative(repositoryRoot, sourcePath)} must be ${sourceLayout.size}x${sourceLayout.size}; ` +
        `got ${sourceMetadata.width}x${sourceMetadata.height}.`,
    );

  const moduleMetrics = {
    id: module.id,
    source: await fileRecord(sourcePath),
    views: [],
  };
  for (const view of views) {
    const left = sourceLayout.margin + view.column * (sourceLayout.panel + sourceLayout.gutter);
    const top = sourceLayout.margin + view.row * (sourceLayout.panel + sourceLayout.gutter);
    const outputPath = join(moduleDirectory, view.file);
    await sharp(sourcePath)
      .extract({ left, top, width: sourceLayout.panel, height: sourceLayout.panel })
      .resize(outputSize, outputSize, { fit: 'fill', kernel: sharp.kernel.lanczos3 })
      .png({ compressionLevel: 9 })
      .toFile(outputPath);
    moduleMetrics.views.push({
      view: view.id,
      rootIntent: module.rootIntent,
      ...(await fileRecord(outputPath)),
    });
  }
  metrics.push(moduleMetrics);
}

await buildGateABoard(metrics);
await writeFile(
  join(reviewRoot, 'gate-a-input-metrics.json'),
  `${JSON.stringify(
    {
      schemaVersion: 1,
      generatedAt: new Date().toISOString(),
      sourceLayout,
      outputSize,
      providerContract: {
        oneObjectPerTask: true,
        separateImages: true,
        inputCountPerTask: 4,
        primaryView: 'front-three-quarter',
        providerOrder: [
          'front-three-quarter',
          'side',
          'rear-three-quarter',
          'top',
        ],
      },
      modules: metrics,
    },
    null,
    2,
  )}\n`,
);

console.log(
  `Prepared ${modules.length * views.length} provider inputs and ${relative(
    repositoryRoot,
    join(reviewRoot, 'gate-a-inputs.png'),
  )}.`,
);

async function buildGateABoard(records) {
  const sidebarWidth = 250;
  const headerHeight = 90;
  const cellSize = 288;
  const cellInset = 10;
  const boardWidth = sidebarWidth + views.length * cellSize;
  const boardHeight = headerHeight + modules.length * cellSize;
  const composites = [];

  composites.push({
    input: Buffer.from(
      `<svg width="${boardWidth}" height="${boardHeight}" xmlns="http://www.w3.org/2000/svg">
        <rect width="100%" height="100%" fill="#11161a"/>
        <text x="22" y="34" fill="#f0b44b" font-family="Arial, sans-serif" font-size="18" font-weight="700">VETERANCY MODULES</text>
        <text x="22" y="61" fill="#aab4ba" font-family="Arial, sans-serif" font-size="14">GATE A · MESHY 7 INPUTS</text>
        ${views
          .map(
            (view, index) =>
              `<text x="${sidebarWidth + index * cellSize + cellSize / 2}" y="55" ` +
              `fill="#dce3e7" text-anchor="middle" font-family="Arial, sans-serif" ` +
              `font-size="17" font-weight="700">${view.label}</text>`,
          )
          .join('')}
        ${modules
          .map(
            (module, index) =>
              `<rect x="0" y="${headerHeight + index * cellSize}" width="${boardWidth}" height="1" fill="#354047"/>` +
              `<text x="22" y="${headerHeight + index * cellSize + cellSize / 2 - 4}" ` +
              `fill="#f1f3f4" font-family="Arial, sans-serif" font-size="17" font-weight="700">${module.label}</text>` +
              `<text x="22" y="${headerHeight + index * cellSize + cellSize / 2 + 21}" ` +
              `fill="#8f9ba2" font-family="Arial, sans-serif" font-size="13">one standalone object</text>`,
          )
          .join('')}
      </svg>`,
    ),
    left: 0,
    top: 0,
  });

  for (const [moduleIndex, module] of modules.entries()) {
    const record = records.find((entry) => entry.id === module.id);
    for (const [viewIndex, view] of views.entries()) {
      const viewRecord = record.views.find((entry) => entry.view === view.id);
      const thumbnail = await sharp(resolve(repositoryRoot, viewRecord.file))
        .resize(cellSize - cellInset * 2, cellSize - cellInset * 2, { fit: 'cover' })
        .png()
        .toBuffer();
      composites.push({
        input: thumbnail,
        left: sidebarWidth + viewIndex * cellSize + cellInset,
        top: headerHeight + moduleIndex * cellSize + cellInset,
      });
    }
  }

  const boardPath = join(reviewRoot, 'gate-a-inputs.png');
  await sharp({
    create: {
      width: boardWidth,
      height: boardHeight,
      channels: 3,
      background: '#11161a',
    },
  })
    .composite(composites)
    .png({ compressionLevel: 9 })
    .toFile(boardPath);
  await sharp(boardPath)
    .resize({ width: 900, withoutEnlargement: true })
    .png({ compressionLevel: 9 })
    .toFile(join(reviewRoot, 'gate-a-inputs-phone.png'));
}

async function fileRecord(path) {
  const bytes = await readFile(path);
  const metadata = await sharp(bytes).metadata();
  return {
    file: relative(repositoryRoot, path),
    width: metadata.width,
    height: metadata.height,
    bytes: bytes.length,
    sha256: createHash('sha256').update(bytes).digest('hex'),
  };
}
