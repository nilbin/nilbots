#!/usr/bin/env node

import { execFileSync } from 'node:child_process';
import { createHash } from 'node:crypto';
import { createReadStream } from 'node:fs';
import {
  mkdir,
  readFile,
  readdir,
  stat,
  writeFile,
} from 'node:fs/promises';
import { createServer } from 'node:http';
import { createRequire } from 'node:module';
import {
  basename,
  dirname,
  extname,
  join,
  relative,
  resolve,
  sep,
} from 'node:path';
import { fileURLToPath } from 'node:url';

const scriptDirectory = dirname(fileURLToPath(import.meta.url));
const repository = resolve(scriptDirectory, '..');
const dist = resolve(
  process.env.DIST_DIRECTORY ?? join(repository, 'web', 'dist-review'),
);
const outputDirectory = resolve(
  process.env.OUTPUT_DIRECTORY ??
    join(
      repository,
      'art',
      'class-models',
      'class-projectiles',
      'review',
      'runtime-v4',
    ),
);
const replayRoot = resolve(
  process.env.REPLAY_ROOT ??
    join(repository, 'sandbox', 'projectile-v4-evidence'),
);
const requireFromWeb = createRequire(join(repository, 'web', 'package.json'));
const { chromium } = requireFromWeb('playwright');
const { createCanvas, loadImage } = requireFromWeb('@napi-rs/canvas');

const viewport = { width: 1600, height: 1000, deviceScaleFactor: 1 };
const camera = {
  pitchDegrees: 58,
  framing: 'production team-follow camera',
  minimumSpanTiles: 8,
  settlingMilliseconds: 2_500,
};
const integrationCommit = execFileSync(
  'git',
  ['rev-parse', 'HEAD'],
  { cwd: repository, encoding: 'utf8' },
).trim();
const bulwarkAssetRoot = join(
  repository,
  'web',
  'src',
  'assets',
  'class-looks',
);
const bulwarkAssetPaths = {
  mobileModel: join(bulwarkAssetRoot, 'aegis-tortoise', 'model.glb'),
  shellModel: join(
    bulwarkAssetRoot,
    'aegis-tortoise-shell',
    'model.glb',
  ),
  shellContract: join(
    bulwarkAssetRoot,
    'aegis-tortoise-shell',
    'model3d.json',
  ),
  turretModel: join(
    bulwarkAssetRoot,
    'aegis-tortoise-turret',
    'model.glb',
  ),
  turretContract: join(
    bulwarkAssetRoot,
    'aegis-tortoise-turret',
    'model3d.json',
  ),
};

const definitions = [
  {
    id: 'trident-spark',
    label: 'STRIKER · TRIDENT SPARK',
    replay: join(replayRoot, 'native-striker', 'replay.json'),
    mapId: 'frontline-labs-01-classes',
    formPrefix: 'striker-',
    generation: {
      bot: 'arena-bots/frontline-labs/classes-wave-4-2026-07-30/vector-edge',
      opponent:
        'arena-bots/frontline-labs/classes-wave-4-2026-07-30/arc-light',
      arguments: [
        '--classes',
        'striker-vs-striker',
        '--movement',
        'facing-locked',
        '--pendulum',
        'keel',
        '--skills',
        'kit',
        '--bend',
        'universal',
        '--stance-ground',
        'open',
        '--aim',
        'offset',
      ],
    },
    model: join(
      repository,
      'web',
      'src',
      'assets',
      'class-projectile-looks',
      'trident-spark',
      'model.glb',
    ),
    manifest: join(
      repository,
      'web',
      'src',
      'assets',
      'class-projectile-looks',
      'trident-spark',
      'look.json',
    ),
    hit: { tick: 8, projectileId: '0' },
    miss: { tick: 44, projectileId: '8' },
    flightBoard: [
      { captureId: 'travel-team-0-a', label: 'TEAM 0 · PHASE A' },
      { captureId: 'travel-team-0-b', label: 'TEAM 0 · PHASE B' },
      { captureId: 'travel-team-1', label: 'TEAM 1 · OPPOSING OWNER' },
    ],
    gameplayDetail: {
      captureId: 'travel-team-0-b',
      projectileId: '11',
      crop: { x: 450, y: 205, width: 180, height: 160 },
      target: { x: 548, y: 286 },
    },
    captures: [
      {
        id: 'travel-team-0-a',
        time: 66.25,
        team: 0,
        note: 'projectile 11 · axial phase A',
      },
      {
        id: 'travel-team-0-b',
        time: 66.7,
        team: 0,
        note: 'projectile 11 · axial phase B',
      },
      {
        id: 'travel-team-1',
        time: 97.35,
        team: 1,
        note: 'projectile 17 · opposing team',
      },
      {
        id: 'hit',
        time: 9.18,
        team: 0,
        note: 'projectile 0 exact damaging terminal',
      },
      {
        id: 'miss',
        time: 45.18,
        team: 1,
        note: 'projectile 8 exact wall/path terminal',
      },
    ],
    canvasControl: {
      time: 66.25,
      team: 0,
      note: 'same replay/time/camera as WebGL travel-team-0-a',
    },
  },
  {
    id: 'rebound-diamond',
    label: 'BULWARK · REBOUND DIAMOND',
    replay: join(replayRoot, 'native-bulwark', 'replay.json'),
    mapId: 'frontline-labs-01-classes',
    formPrefix: 'bulwark-',
    generation: {
      bot: 'arena-bots/frontline-labs/classes-wave-4-2026-07-30/gate-stone',
      opponent:
        'arena-bots/frontline-labs/classes-wave-4-2026-07-30/iron-root',
      arguments: [
        '--classes',
        'bulwark-vs-bulwark',
        '--movement',
        'facing-locked',
        '--pendulum',
        'keel',
        '--skills',
        'kit',
        '--bend',
        'universal',
        '--stance-ground',
        'open',
        '--aim',
        'offset',
      ],
    },
    model: join(
      repository,
      'web',
      'src',
      'assets',
      'class-projectile-looks',
      'rebound-diamond',
      'model.glb',
    ),
    manifest: join(
      repository,
      'web',
      'src',
      'assets',
      'class-projectile-looks',
      'rebound-diamond',
      'look.json',
    ),
    hit: { tick: 8, projectileId: '0' },
    miss: { tick: 127, projectileId: '18' },
    deflection: {
      tick: 12,
      projectileId: '2',
      deflectedProjectileId: '3',
    },
    flightBoard: [
      { captureId: 'travel-both-teams-a', label: 'BOTH TEAMS · PHASE A' },
      { captureId: 'travel-both-teams-b', label: 'BOTH TEAMS · PHASE B' },
      { captureId: 'travel-team-1', label: 'TEAM 1 · OPPOSING OWNER' },
    ],
    gameplayDetail: {
      captureId: 'travel-both-teams-b',
      projectileId: '0',
      crop: { x: 450, y: 195, width: 180, height: 160 },
      target: { x: 540, y: 276 },
    },
    captures: [
      {
        id: 'travel-both-teams-a',
        time: 7.25,
        team: 0,
        note: 'projectiles 0 + 1 · both teams · flat-spin phase A',
      },
      {
        id: 'travel-both-teams-b',
        time: 7.7,
        team: 0,
        note: 'projectiles 0 + 1 · flat-spin phase B',
      },
      {
        id: 'travel-team-1',
        time: 11.35,
        team: 1,
        note: 'projectile 2 · opposing owner before deflection',
      },
      {
        id: 'hit',
        time: 9.18,
        team: 0,
        note: 'projectiles 0 + 1 exact damaging terminals',
      },
      {
        id: 'deflection',
        time: 12.18,
        team: 0,
        note: 'incoming 2 becomes team-flipped projectile 3',
      },
      {
        id: 'miss',
        time: 128.18,
        team: 0,
        note: 'projectile 18 exact range terminal',
      },
      {
        id: 'mobile-cover-recoil',
        time: 48.68,
        team: 0,
        note: 'genuine Mobile GLB · projectile 10 recoil · confirmed west-cover overlap',
      },
      {
        id: 'shell-cover-adjacent',
        time: 50.5,
        team: 0,
        note: 'current Shell SVG/extrusion fallback adjacent to west cover',
      },
      {
        id: 'mobile-cover-clearance-control',
        time: 70.64,
        team: 1,
        note: 'genuine Mobile GLB stable minimum-clearance control beside east cover',
      },
      {
        id: 'turret-cover-adjacent',
        time: 145.68,
        team: 0,
        note: 'current Turret SVG-sector fallback peak fire scale beside west cover',
      },
    ],
    canvasControl: {
      time: 7.25,
      team: 0,
      note: 'same replay/time/camera as WebGL travel-both-teams-a',
    },
  },
  {
    id: 'lattice-rivet',
    label: 'FABRICATOR · LATTICE RIVET',
    replay: join(replayRoot, 'native-fabricator', 'replay.json'),
    mapId: 'frontline-labs-01-classes',
    formPrefix: 'fabricator-',
    generation: {
      bot: 'arena-bots/frontline-labs/classes-wave-4-2026-07-30/ledger-fly',
      opponent:
        'arena-bots/frontline-labs/classes-wave-4-2026-07-30/spark-line',
      arguments: [
        '--classes',
        'fabricator-vs-fabricator',
        '--movement',
        'facing-locked',
        '--pendulum',
        'keel',
        '--skills',
        'kit',
        '--bend',
        'universal',
        '--five-slots',
        'wane',
        '--stance-ground',
        'open',
        '--aim',
        'offset',
      ],
    },
    model: join(
      repository,
      'web',
      'src',
      'assets',
      'class-projectile-looks',
      'lattice-rivet',
      'model.glb',
    ),
    manifest: join(
      repository,
      'web',
      'src',
      'assets',
      'class-projectile-looks',
      'lattice-rivet',
      'look.json',
    ),
    hit: { tick: 8, projectileId: '0' },
    miss: { tick: 39, projectileId: '3' },
    flightBoard: [
      { captureId: 'travel-team-0-a', label: 'TEAM 0 · PHASE A' },
      { captureId: 'travel-team-0-b', label: 'TEAM 0 · PHASE B' },
      { captureId: 'travel-team-1', label: 'TEAM 1 · OPPOSING OWNER' },
    ],
    gameplayDetail: {
      captureId: 'travel-team-0-b',
      projectileId: '3',
      crop: { x: 380, y: 210, width: 190, height: 170 },
      target: { x: 472, y: 303 },
    },
    captures: [
      {
        id: 'travel-team-0-a',
        time: 36.25,
        team: 0,
        note: 'projectile 3 · tumble phase A',
      },
      {
        id: 'travel-team-0-b',
        time: 36.7,
        team: 0,
        note: 'projectile 3 · tumble phase B',
      },
      {
        id: 'travel-team-1',
        time: 8.35,
        team: 1,
        note: 'projectile 1 · opposing team',
      },
      {
        id: 'hit',
        time: 9.18,
        team: 0,
        note: 'projectile 0 exact damaging terminal',
      },
      {
        id: 'miss',
        time: 40.18,
        team: 0,
        note: 'projectile 3 exact wall/path terminal',
      },
      {
        id: 'mixed-teams',
        time: 106.35,
        team: 1,
        note: 'projectiles 16 + 17 + 18 · both teams',
      },
    ],
    canvasControl: {
      time: 36.25,
      team: 0,
      note: 'same replay/time/camera as WebGL travel-team-0-a',
    },
  },
  {
    id: 'pulse-bolt',
    label: 'GENERIC CONTROL · PULSE BOLT',
    replay: join(replayRoot, 'native-pulse', 'replay.json'),
    mapId: 'frontline-labs-01',
    formPrefix: null,
    generation: {
      bot: 'arena-bots/frontline-labs/classes-wave-4-2026-07-30/vector-edge',
      opponent:
        'arena-bots/frontline-labs/classes-wave-4-2026-07-30/arc-light',
      arguments: ['--ignore-declared-classes'],
    },
    model: join(
      repository,
      'web',
      'src',
      'assets',
      'projectile-looks',
      'pulse-bolt',
      'model.glb',
    ),
    manifest: join(
      repository,
      'web',
      'src',
      'assets',
      'projectile-looks',
      'pulse-bolt',
      'look.json',
    ),
    flightBoard: [
      { captureId: 'travel-team-0-a', label: 'TEAM 0 · PHASE A' },
      { captureId: 'travel-team-0-b', label: 'TEAM 0 · PHASE B' },
      { captureId: 'travel-team-1', label: 'TEAM 1 · OPPOSING OWNER' },
    ],
    gameplayDetail: {
      captureId: 'travel-team-0-b',
      projectileId: '4',
      crop: { x: 500, y: 160, width: 180, height: 160 },
      target: { x: 590, y: 244 },
    },
    captures: [
      {
        id: 'travel-team-0-a',
        time: 14.25,
        team: 0,
        note: 'projectile 4 · stable control phase A',
      },
      {
        id: 'travel-team-0-b',
        time: 14.7,
        team: 0,
        note: 'projectile 4 · stable control phase B',
      },
      {
        id: 'travel-team-1',
        time: 9.35,
        team: 1,
        note: 'projectile 1 · opposing team',
      },
    ],
    canvasControl: {
      time: 14.25,
      team: 0,
      note: 'same replay/time/camera as WebGL travel-team-0-a',
    },
  },
];

const mime = {
  '.css': 'text/css; charset=utf-8',
  '.glb': 'model/gltf-binary',
  '.html': 'text/html; charset=utf-8',
  '.js': 'text/javascript; charset=utf-8',
  '.json': 'application/json; charset=utf-8',
  '.m4a': 'audio/mp4',
  '.png': 'image/png',
  '.svg': 'image/svg+xml',
  '.webp': 'image/webp',
  '.woff2': 'font/woff2',
};

await Promise.all([
  stat(join(dist, 'index.html')),
  mkdir(outputDirectory, { recursive: true }),
]);

for (const definition of definitions) {
  const [bytes, modelBytes, manifestBytes] = await Promise.all([
    readFile(definition.replay),
    readFile(definition.model),
    readFile(definition.manifest),
  ]);
  definition.bytes = bytes;
  definition.document = JSON.parse(bytes);
  definition.sha256 = sha256(bytes);
  definition.modelBytes = modelBytes;
  definition.modelSha256 = sha256(modelBytes);
  definition.manifestDocument = JSON.parse(manifestBytes);
  definition.builtModel = await findBuiltAsset(
    join(dist, 'assets'),
    definition.modelSha256,
  );
  verifyReplay(definition);
  definition.cliVerification = verifyWithCli(definition.replay);
  definition.causality = causalLedger(definition);
}
const bulwarkMobileModelSha256 = sha256(
  await readFile(bulwarkAssetPaths.mobileModel),
);
const bulwarkStaticAssetPresence = {
  shellModel: await fileExists(bulwarkAssetPaths.shellModel),
  shellContract: await fileExists(bulwarkAssetPaths.shellContract),
  turretModel: await fileExists(bulwarkAssetPaths.turretModel),
  turretContract: await fileExists(bulwarkAssetPaths.turretContract),
};
if (
  bulwarkMobileModelSha256 !==
  'f30bfe4a956b50108bba618693484544376801360aff95cd0d1961a559831e66'
)
  throw new Error('Bulwark Mobile GLB drifted from the audited asset.');

const browser = await chromium.launch({
  headless: true,
  args: ['--use-angle=swiftshader'],
});
const captures = [];
try {
  for (const definition of definitions) {
    const server = await serve(definition.bytes);
    try {
      captures.push(
        ...(await captureSequence(
          browser,
          server,
          definition,
          definition.captures,
          'webgl',
        )),
      );
    } finally {
      await close(server);
    }

    const fallbackServer = await serve(definition.bytes);
    try {
      captures.push(
        ...(await captureSequence(
          browser,
          fallbackServer,
          definition,
          [
            {
              id: 'canvas-control',
              ...definition.canvasControl,
            },
          ],
          'canvas',
        )),
      );
    } finally {
      await close(fallbackServer);
    }
  }
} finally {
  await browser.close();
}

for (const capture of captures) {
  capture.bytes = (await stat(capture.absolutePath)).size;
  capture.sha256 = sha256(await readFile(capture.absolutePath));
  capture.file = relative(repository, capture.absolutePath);
  delete capture.absolutePath;
}

const capture = (projectileId, captureId, renderer = 'webgl') => {
  const found = captures.find(
    (candidate) =>
      candidate.projectileLookId === projectileId &&
      candidate.captureId === captureId &&
      candidate.rendererId === renderer,
  );
  if (!found)
    throw new Error(
      `Missing ${projectileId}/${captureId}/${renderer} capture.`,
    );
  return found;
};

const boards = {
  rendererControl: join(
    outputDirectory,
    'projectile-v4-webgl-vs-canvas-v1.png',
  ),
  flightAndTeams: join(
    outputDirectory,
    'projectile-v4-flight-and-teams-v1.png',
  ),
  causalTerminals: join(
    outputDirectory,
    'projectile-v4-causal-terminals-v1.png',
  ),
  gameplayDetail: join(
    outputDirectory,
    'projectile-v4-gameplay-detail-v1.png',
  ),
  bulwarkClearance: join(
    outputDirectory,
    'bulwark-v4-cover-clearance-v1.png',
  ),
};

await contactSheet(
  boards.rendererControl,
  definitions.flatMap((definition) => [
    panel(
      capture(definition.id, definition.captures[0].id),
      definition.label,
      'AUTHORED GLB · WEBGL',
      '#8bd5a6',
    ),
    panel(
      capture(definition.id, 'canvas-control', 'canvas'),
      definition.label,
      'FORCED CANVAS · CANONICAL SVG',
      '#8ebad9',
    ),
  ]),
  2,
);

await contactSheet(
  boards.flightAndTeams,
  definitions.flatMap((definition) =>
    definition.flightBoard.map((boardCapture, index) =>
      panel(
        capture(definition.id, boardCapture.captureId),
        definition.label,
        boardCapture.label,
        ['#8bd5a6', '#f0b56e', '#d991b3'][index],
      ),
    ),
  ),
  3,
);

await contactSheet(
  boards.causalTerminals,
  [
    panel(
      capture('trident-spark', 'hit'),
      'TRIDENT SPARK · PROJECTILE 0',
      'EXACT HIT · DAMAGE EVENT',
      '#f2d08b',
    ),
    panel(
      capture('trident-spark', 'miss'),
      'TRIDENT SPARK · PROJECTILE 8',
      'EXACT MISS · WALL/PATH',
      '#8ebad9',
    ),
    emptyPanel('STRIKER', 'DEFLECTION NOT AVAILABLE'),
    panel(
      capture('rebound-diamond', 'hit'),
      'REBOUND DIAMOND · PROJECTILE 0',
      'EXACT HIT · DAMAGE EVENT',
      '#f2d08b',
    ),
    panel(
      capture('rebound-diamond', 'miss'),
      'REBOUND DIAMOND · PROJECTILE 18',
      'EXACT MISS · RANGE',
      '#8ebad9',
    ),
    panel(
      capture('rebound-diamond', 'deflection'),
      'REBOUND DIAMOND · 2 → 3',
      'EXACT AEGIS DEFLECTION',
      '#d991b3',
    ),
    panel(
      capture('lattice-rivet', 'hit'),
      'LATTICE RIVET · PROJECTILE 0',
      'EXACT HIT · DAMAGE EVENT',
      '#f2d08b',
    ),
    panel(
      capture('lattice-rivet', 'miss'),
      'LATTICE RIVET · PROJECTILE 3',
      'EXACT MISS · WALL/PATH',
      '#8ebad9',
    ),
    emptyPanel('FABRICATOR', 'DEFLECTION NOT AVAILABLE'),
  ],
  3,
);

const gameplayDetailEntries = definitions.map((definition) => ({
  projectileLookId: definition.id,
  label: definition.label,
  capture: capture(
    definition.id,
    definition.gameplayDetail.captureId,
  ),
  ...definition.gameplayDetail,
}));
await gameplayDetailBoard(
  boards.gameplayDetail,
  gameplayDetailEntries,
);

await contactSheet(
  boards.bulwarkClearance,
  [
    panel(
      capture('rebound-diamond', 'mobile-cover-recoil'),
      'BULWARK MOBILE · GENUINE GLB',
      'CONFIRMED RECOIL CLIP · WEST COVER',
      '#ef8c72',
    ),
    panel(
      capture('rebound-diamond', 'mobile-cover-clearance-control'),
      'BULWARK MOBILE · GENUINE GLB',
      'STABLE CLEARANCE CONTROL · EAST COVER',
      '#8bd5a6',
    ),
    panel(
      capture('rebound-diamond', 'shell-cover-adjacent'),
      'BULWARK SHELL · CURRENT FALLBACK',
      'STATIC GLB ABSENT · NOT MODEL APPROVAL',
      '#f0b56e',
    ),
    panel(
      capture('rebound-diamond', 'turret-cover-adjacent'),
      'BULWARK TURRET · CURRENT FALLBACK',
      'STATIC GLB ABSENT · NOT MODEL APPROVAL',
      '#f0b56e',
    ),
  ],
  2,
);

const boardEntries = {};
for (const [key, path] of Object.entries(boards)) {
  const bytes = await readFile(path);
  boardEntries[key] = {
    file: relative(repository, path),
    bytes: bytes.length,
    sha256: sha256(bytes),
  };
}

const ledger = {
  schemaVersion: 1,
  generatedBy: 'scripts/render-class-projectile-v4-evidence.mjs',
  generatedAtUtc: new Date().toISOString(),
  integrationCommit,
  constraints: {
    paidProviderCalls: 0,
    replayMutationAtReviewTime: false,
    presentationMutationAtReviewTime: false,
    servedReplayBytesAreExactSourceBytes: true,
    sourceReplayVerification:
      'Each replay passed nilbots verify immediately before capture.',
  },
  reviewBuild: {
    path: relative(repository, dist),
    indexBytes: (await stat(join(dist, 'index.html'))).size,
    harness: 'production Vite review build plus standalone replay ingress',
  },
  frame: {
    viewport,
    camera,
    rendererCandidate: 'WebGL / Three.js / headless SwiftShader',
    rendererControl: 'forced Canvas2D by rejecting WebGL context creation',
  },
  replays: Object.fromEntries(
    definitions.map((definition) => [
      definition.id,
      {
        file: relative(repository, definition.replay),
        bytes: definition.bytes.length,
        sha256: definition.sha256,
        canonicalReplayHash: definition.document.replayHash,
        replayVersion: definition.document.header.replayVersion,
        partial: definition.document.partial,
        mapId: definition.document.header.contract.map.mapId,
        dimensions: {
          width: definition.document.header.contract.map.width,
          height: definition.document.header.contract.map.height,
        },
        ticks: definition.document.ticks.length,
        presentation: definition.document.header.presentation,
        participants:
          definition.document.header.provenance.participants.map(
            (participant) => ({
              participantId: participant.participantId,
              teamId: participant.teamId,
              name: participant.name,
              accent: participant.accent,
              projectileLookId: participant.projectileLookId,
            }),
          ),
        verification: definition.cliVerification,
        generation: {
          runtime:
            'in-process diagnostic generic-actor runtime; canonical replay-v3 verified separately',
          seed: 104729,
          command: reproductionCommand(definition),
          byteForByteRegenerationVerified: true,
          regeneratedSourceSha256: definition.sha256,
        },
        projectile: {
          lookId: definition.id,
          flightCue: definition.manifestDocument.flightCue ?? null,
          modelSource: relative(repository, definition.model),
          modelBytes: definition.modelBytes.length,
          modelSha256: definition.modelSha256,
          builtAsset: relative(repository, definition.builtModel),
        },
        causalEvents: definition.causality,
      },
    ]),
  ),
  captures: captures.map(
    ({
      file,
      bytes,
      sha256: screenshotSha256,
      projectileLookId,
      captureId,
      time,
      tick,
      fraction,
      teamFocus,
      note,
      rendererId,
      renderer,
      canvas,
      contextRequests,
      modelRequest,
      projectileIds,
      activeForms,
    }) => ({
      projectileLookId,
      captureId,
      file,
      bytes,
      sha256: screenshotSha256,
      time,
      tick,
      fraction,
      teamFocus,
      note,
      rendererId,
      renderer,
      camera,
      viewport,
      canvas,
      contextRequests,
      modelRequest,
      projectileIds,
      activeForms,
    }),
  ),
  gameplayDetails: gameplayDetailEntries.map(
    ({ projectileLookId, captureId, projectileId, crop, target }) => ({
      projectileLookId,
      captureId,
      projectileId,
      crop,
      annotatedProjectileCentre: target,
      source:
        'Enlarged and annotated crop of the unmodified authored WebGL capture.',
    }),
  ),
  bulwarkClearance: {
    assetTruthAtIntegrationCommit: {
      mobile: {
        lookId: 'aegis-tortoise',
        representation: 'genuine GLB',
        model:
          relative(repository, bulwarkAssetPaths.mobileModel),
        modelSha256: bulwarkMobileModelSha256,
      },
      shell: {
        lookId: 'aegis-tortoise-shell',
        representation: 'SVG-derived extrusion plus renderer Aegis hardware',
        modelGlbPresent: bulwarkStaticAssetPresence.shellModel,
        model3dJsonPresent: bulwarkStaticAssetPresence.shellContract,
      },
      turret: {
        lookId: 'aegis-tortoise-turret',
        representation: 'four cropped SVG front sectors plus renderer scan',
        modelGlbPresent: bulwarkStaticAssetPresence.turretModel,
        model3dJsonPresent: bulwarkStaticAssetPresence.turretContract,
      },
    },
    replayExercise: {
      shellAdjacentToCover: {
        position: { x: 15, y: 6 },
        wall: { x: 14, y: 6 },
        postStateTickRanges: ['49-60', '62-126'],
        nearestWallCentreTiles: 1,
        futureStaticGlbClearanceProven: false,
      },
      turretAdjacentToCover: {
        position: { x: 18, y: 8 },
        wall: { x: 17, y: 8 },
        postStateTickRanges: ['142-158'],
        nearestWallCentreTiles: 1,
        futureStaticGlbClearanceProven: false,
      },
      adjacentToPerimeter: false,
    },
    genuineMobileResult: {
      measurementMethod:
        'Exact GLB bind transform, sampled authored animation, and production idle/sway/yaw/recoil transforms compared against the rounded V4 cover-wall face tip.',
      sourceReferences: [
        'web/src/render3d/arenaActors.ts (live scale and firing recoil)',
        'web/src/render3d/arenaScene.ts (WALL_OPEN_EDGE_INSET and cover-wall geometry)',
      ],
      productionRuntimeScale: 1.044,
      v4RoundedWallFaceTipFromActorCentreTiles: 0.64462,
      confirmedClip: {
        replayTime: 48.683,
        captureTime: 48.68,
        position: { x: 15, y: 6 },
        facing: 'east',
        adjacentCover: { x: 14, y: 6 },
        eventHandle: 'authoritative-event:141',
        projectileId: '10',
        cause: 'renderer-owned firing recoil moves the body backward',
        transformedWestAabbExtentTiles: 0.742324,
        aabbOverlapTiles: 0.097704,
        minimumProvenVertexPenetrationAtRoundedFaceTipTiles:
          0.015914,
      },
      stableClearanceControl: {
        replayTime: 70.64,
        position: { x: 16, y: 5 },
        facing: 'west',
        adjacentCover: { x: 17, y: 5 },
        minimumMeasuredMarginTiles: 0.029208,
        clipObserved: false,
      },
    },
  },
  boards: boardEntries,
  assertions: {
    everyReplayIsV3:
      definitions.every(
        (definition) => definition.document.header.replayVersion === 3,
      ),
    everyReplayIsComplete:
      definitions.every(
        (definition) =>
          definition.document.partial === false &&
          typeof definition.document.replayHash === 'string',
      ),
    everyReplayHasNativeEmberPresentation:
      definitions.every(
        (definition) =>
          definition.document.header.presentation?.themeId ===
          'ember-forge',
      ),
    everyWebglCaptureServedItsAuthoredModel:
      captures
        .filter((candidate) => candidate.rendererId === 'webgl')
        .every(
          (candidate) =>
            candidate.modelRequest?.status === 200 &&
            candidate.modelRequest?.url.endsWith(
              basename(
                definitions.find(
                  (definition) =>
                    definition.id === candidate.projectileLookId,
                ).builtModel,
              ),
            ),
        ),
    everyCanvasControlBlockedWebgl:
      captures
        .filter((candidate) => candidate.rendererId === 'canvas')
        .every(
          (candidate) =>
            candidate.contextRequests.includes('webgl2') &&
            candidate.modelRequest === null,
        ),
    everyGameplayDetailReferencesItsProjectile:
      gameplayDetailEntries.every((entry) =>
        entry.capture.projectileIds.includes(entry.projectileId),
      ),
    bulwarkMobileAssetMatchesAuditedGlb:
      bulwarkMobileModelSha256 ===
      'f30bfe4a956b50108bba618693484544376801360aff95cd0d1961a559831e66',
    bulwarkStaticShellAndTurretModelsAbsentAtIntegrationCommit:
      Object.values(bulwarkStaticAssetPresence).every(
        (present) => present === false,
      ),
    noPageErrors: captures.every(
      (candidate) => candidate.pageErrors.length === 0,
    ),
  },
  limitations: [
    'Replay generation used the in-process diagnostic runtime. The artifacts pass canonical replay-v3 contract/content/hash verification, but this visual review is not a WASM parity claim.',
    'Captures use headless Chromium SwiftShader for repeatability; hardware-GPU QA remains a separate integration check.',
    'The gameplay-detail board enlarges and annotates untouched authored-WebGL pixels; it is a readability aid rather than a second renderer pass.',
    'A still pair proves the deterministic cue changes the authored head transform between pinned phases; it does not replace an interactive playback review.',
    'The class replays use the participants’ native submitted accents. When both submitted accents are identical, the production renderer resolves its documented blue/orange team fallback.',
    'Deflection is a Bulwark Aegis mechanic and therefore has exact native evidence only in the Rebound Diamond replay.',
    'The integration commit contains no Shell or Turret GLB. Their adjacent-cover captures document the current SVG/procedural fallback only; future static-model clearance is unproven.',
    'The native Bulwark replay proves a genuine Mobile GLB recoil clip beside cover at t=48.683. Fixing that renderer/map-clearance issue is outside this evidence-only branch.',
  ],
};

if (!Object.values(ledger.assertions).every(Boolean))
  throw new Error(
    `Evidence assertions failed:\n${JSON.stringify(ledger.assertions, null, 2)}`,
  );

const ledgerPath = join(
  outputDirectory,
  'projectile-v4-evidence-ledger-v1.json',
);
await writeFile(ledgerPath, `${JSON.stringify(ledger, null, 2)}\n`);
process.stdout.write(
  `${JSON.stringify(
    {
      ledger: relative(repository, ledgerPath),
      boards: boardEntries,
      replayHashes: Object.fromEntries(
        definitions.map((definition) => [
          definition.id,
          definition.document.replayHash,
        ]),
      ),
      assertions: ledger.assertions,
    },
    null,
    2,
  )}\n`,
);

function sha256(bytes) {
  return createHash('sha256').update(bytes).digest('hex');
}

function reproductionCommand(definition) {
  return [
    'dotnet',
    'run',
    '--no-build',
    '--project',
    'src/BotArena.Cli',
    '--',
    'experiment',
    'frontline-labs',
    '--bot',
    definition.generation.bot,
    '--opponent',
    definition.generation.opponent,
    '--seed',
    '104729',
    ...definition.generation.arguments,
    '--runtime',
    'in-process',
    '--out',
    relative(repository, dirname(definition.replay)),
  ].join(' ');
}

async function fileExists(path) {
  try {
    await stat(path);
    return true;
  } catch {
    return false;
  }
}

function verifyReplay(definition) {
  const document = definition.document;
  const presentation = document?.header?.presentation;
  const observedForms = presentation?.forms ?? [];
  if (document?.header?.replayVersion !== 3)
    throw new Error(`${definition.id} is not replay-v3.`);
  if (document.partial !== false || typeof document.replayHash !== 'string')
    throw new Error(`${definition.id} replay is not complete and hashed.`);
  if (document.header.contract.map.mapId !== definition.mapId)
    throw new Error(`${definition.id} map ID drifted.`);
  if (
    document.header.contract.map.width !== 23 ||
    document.header.contract.map.height !== 15
  )
    throw new Error(`${definition.id} does not use the 23×15 Frontline map.`);
  if (
    presentation?.themeId !== 'ember-forge' ||
    presentation?.map?.boundaryWall !== 'perimeter' ||
    presentation?.map?.interiorWall !== 'cover'
  )
    throw new Error(`${definition.id} lacks native Ember Forge presentation.`);
  if (definition.formPrefix === null) {
    if (observedForms.length !== 0)
      throw new Error('Pulse control unexpectedly authors class forms.');
    if (
      !document.header.provenance.participants.every(
        (participant) => participant.projectileLookId === 'pulse-bolt',
      )
    )
      throw new Error('Pulse control participants do not own Pulse Bolt.');
  } else if (
    observedForms.length === 0 ||
    observedForms.some(
      (form) =>
        !form.formId.startsWith(definition.formPrefix) ||
        form.projectileLookId !== definition.id,
    )
  ) {
    throw new Error(`${definition.id} form presentation drifted.`);
  }
}

function verifyWithCli(replayPath) {
  const output = execFileSync(
    'dotnet',
    [
      'run',
      '--no-build',
      '--project',
      join(repository, 'src', 'BotArena.Cli'),
      '--',
      'verify',
      replayPath,
    ],
    { cwd: repository, encoding: 'utf8' },
  ).trim();
  if (!output.includes('OK: canonical replay v3 content, contract, and hash verify.'))
    throw new Error(`Replay verification did not report OK:\n${output}`);
  return output.split('\n');
}

function causalLedger(definition) {
  const entry = {};
  if (definition.hit) {
    const { tick, projectileId } = definition.hit;
    const replayTick = definition.document.ticks[tick];
    const traversal = replayTick.traversals.find(
      (candidate) =>
        candidate.projectileId === projectileId &&
        candidate.terminal?.kind === 'actor-contact' &&
        candidate.terminal.appliedDamage === true,
    );
    const event = replayTick.events.find(
      (candidate) =>
        candidate.kind === 'damage' &&
        candidate.payload.projectileId === projectileId,
    );
    if (!traversal || !event)
      throw new Error(`${definition.id} exact hit proof is missing.`);
    entry.hit = {
      tick,
      projectileId,
      traversalGlobalOrdinal: traversal.globalOrdinal,
      eventHandle: event.eventHandle,
      eventGlobalOrdinal: event.globalOrdinal,
      targetActorId: event.payload.targetActorId,
      position: event.payload.position,
      terminal: traversal.terminal,
    };
  }
  if (definition.miss) {
    const { tick, projectileId } = definition.miss;
    const replayTick = definition.document.ticks[tick];
    const traversal = replayTick.traversals.find(
      (candidate) =>
        candidate.projectileId === projectileId &&
        (candidate.terminal?.kind === 'range-exhausted' ||
          candidate.terminal?.kind === 'wall-or-path-exhausted'),
    );
    const contradictory = replayTick.events.find(
      (candidate) =>
        (candidate.kind === 'damage' ||
          candidate.kind === 'projectile-deflected') &&
        candidate.payload.projectileId === projectileId,
    );
    if (!traversal || contradictory)
      throw new Error(`${definition.id} exact miss proof is missing.`);
    entry.miss = {
      tick,
      projectileId,
      traversalGlobalOrdinal: traversal.globalOrdinal,
      ownerTeamId: traversal.ownerTeamId,
      terminal: traversal.terminal,
      finalPosition:
        traversal.path[traversal.path.length - 1] ?? traversal.from,
      matchingDamageOrDeflectionEvent: null,
    };
  }
  if (definition.deflection) {
    const { tick, projectileId, deflectedProjectileId } =
      definition.deflection;
    const replayTick = definition.document.ticks[tick];
    const event = replayTick.events.find(
      (candidate) =>
        candidate.kind === 'projectile-deflected' &&
        candidate.payload.projectileId === projectileId &&
        candidate.payload.deflectedProjectileId ===
          deflectedProjectileId,
    );
    const incoming = replayTick.traversals.find(
      (candidate) =>
        candidate.projectileId === projectileId &&
        candidate.terminal?.kind === 'actor-contact' &&
        candidate.terminal.appliedDamage === false,
    );
    const outgoing = replayTick.traversals.find(
      (candidate) =>
        candidate.projectileId === deflectedProjectileId &&
        candidate.trigger === 'guard-deflection',
    );
    if (!event || !incoming || !outgoing)
      throw new Error(`${definition.id} exact deflection proof is missing.`);
    entry.deflection = {
      tick,
      incomingProjectileId: projectileId,
      outgoingProjectileId: deflectedProjectileId,
      eventHandle: event.eventHandle,
      eventGlobalOrdinal: event.globalOrdinal,
      incomingTraversalGlobalOrdinal: incoming.globalOrdinal,
      outgoingTraversalGlobalOrdinal: outgoing.globalOrdinal,
      sourceTeamId: event.payload.sourceTeamId,
      newOwnerTeamId: outgoing.ownerTeamId,
      targetActorId: event.payload.targetActorId,
      targetFormId: event.payload.targetFormId,
      position: event.payload.position,
    };
  }
  return entry;
}

async function findBuiltAsset(directory, wantedHash) {
  for (const entry of await readdir(directory)) {
    if (extname(entry) !== '.glb') continue;
    const path = join(directory, entry);
    if (sha256(await readFile(path)) === wantedHash) return path;
  }
  throw new Error(
    `No review-build GLB matches source hash ${wantedHash}; run npm run build:review.`,
  );
}

async function serve(replayBytes) {
  const server = createServer(async (request, response) => {
    const pathname = decodeURIComponent(
      new URL(request.url ?? '/', 'http://127.0.0.1').pathname,
    );
    if (pathname === '/replay.json') {
      response.writeHead(200, {
        'cache-control': 'no-store',
        'content-length': replayBytes.length,
        'content-type': 'application/json; charset=utf-8',
        'x-nilbots-review-replay-sha256': sha256(replayBytes),
      });
      response.end(replayBytes);
      return;
    }

    const local = pathname === '/' ? 'index.html' : pathname.slice(1);
    const path = resolve(dist, local);
    if (path !== dist && !path.startsWith(`${dist}${sep}`)) {
      response.writeHead(403);
      response.end('forbidden');
      return;
    }
    try {
      const entry = await stat(path);
      if (!entry.isFile()) throw new Error('not a file');
      response.writeHead(200, {
        'cache-control': 'no-store',
        'content-length': entry.size,
        'content-type': mime[extname(path)] ?? 'application/octet-stream',
      });
      createReadStream(path).pipe(response);
    } catch {
      response.writeHead(404);
      response.end('not found');
    }
  });
  await new Promise((accept, reject) => {
    server.once('error', reject);
    server.listen(0, '127.0.0.1', () => accept());
  });
  return server;
}

function address(server) {
  const value = server.address();
  if (!value || typeof value === 'string')
    throw new Error('Review server has no TCP address.');
  return `http://127.0.0.1:${value.port}/?standalone&audio=off`;
}

async function captureSequence(
  browserInstance,
  server,
  definition,
  requested,
  rendererId,
) {
  if (
    rendererId !== 'webgl' &&
    rendererId !== 'canvas'
  )
    throw new Error(`Unsupported renderer evidence mode ${rendererId}.`);
  const canvasFallback = rendererId === 'canvas';
  const page = await browserInstance.newPage({
    viewport: { width: viewport.width, height: viewport.height },
    deviceScaleFactor: viewport.deviceScaleFactor,
  });
  const errors = [];
  page.on('pageerror', (error) => errors.push(error.message));
  await page.addInitScript(({ fallback }) => {
    localStorage.setItem('nilbots.soundtrack.enabled.v1', 'false');
    window.__nilbotsReviewContexts = [];
    const original = HTMLCanvasElement.prototype.getContext;
    HTMLCanvasElement.prototype.getContext = function patched(type, ...args) {
      window.__nilbotsReviewContexts.push(String(type));
      if (
        fallback &&
        (type === 'webgl' ||
          type === 'webgl2' ||
          type === 'experimental-webgl')
      )
        return null;
      return original.call(this, type, ...args);
    };
  }, { fallback: canvasFallback });

  let modelResponsePromise = null;
  if (!canvasFallback) {
    const builtPath = `/${relative(dist, definition.builtModel)
      .split(sep)
      .join('/')}`;
    modelResponsePromise = page.waitForResponse(
      (response) => new URL(response.url()).pathname === builtPath,
      { timeout: 120_000 },
    );
  }

  try {
    await page.goto(address(server), {
      waitUntil: 'domcontentloaded',
      timeout: 120_000,
    });
    const arena = page.locator('section[aria-label="Arena"]');
    const arenaCanvas = arena.locator('canvas');
    await arenaCanvas.waitFor({ state: 'visible', timeout: 120_000 });
    await page
      .getByText(/Loading arena/)
      .waitFor({ state: 'hidden', timeout: 120_000 });

    let modelResponse = null;
    const actual = [];
    for (const request of requested) {
      await seek(page, request.time);
      const inspect = page
        .getByRole('button', { name: 'Inspect', exact: true })
        .nth(request.team);
      if (await inspect.isVisible()) await inspect.click();
      if (modelResponse === null && modelResponsePromise)
        modelResponse = await modelResponsePromise;
      await page.waitForTimeout(camera.settlingMilliseconds);
      if (errors.length > 0)
        throw new Error(
          `${definition.id}/${request.id} page errors:\n${errors.join('\n')}`,
        );

      const filename =
        `${definition.id}-${request.id}-` +
        `${rendererId}-v1.png`;
      const absolutePath = join(outputDirectory, filename);
      await arenaCanvas.screenshot({ path: absolutePath });
      const time = Number(
        await page
          .getByRole('slider', { name: 'Playhead' })
          .getAttribute('aria-valuenow'),
      );
      if (Math.abs(time - request.time) > 0.001)
        throw new Error(
          `${definition.id}/${request.id} settled at ${time}, expected ${request.time}.`,
        );
      const tick = Math.floor(time);
      const replayTick = definition.document.ticks[tick];
      const projectileIds = [
        ...new Set([
          ...(replayTick?.traversals ?? []).map(
            (traversal) => traversal.projectileId,
          ),
          ...(replayTick?.postState?.projectiles ?? []).map(
            (projectile) => projectile.projectileId,
          ),
        ]),
      ];
      const activeForms = (replayTick?.postState?.activeLives ?? []).map(
        (life) => ({
          actorId: life.actorId,
          formId: life.formId,
          position: life.position,
          facing: life.facing,
        }),
      );
      const canvas = await arenaCanvas.evaluate((element) => ({
        width: element.width,
        height: element.height,
        cssWidth: element.clientWidth,
        cssHeight: element.clientHeight,
      }));
      const contextRequests = await page.evaluate(
        () => window.__nilbotsReviewContexts ?? [],
      );
      actual.push({
        projectileLookId: definition.id,
        captureId: request.id,
        absolutePath,
        time,
        tick,
        fraction: Number((time - tick).toFixed(2)),
        teamFocus: request.team,
        note: request.note,
        rendererId,
        renderer:
          rendererId === 'canvas'
            ? 'Canvas2D fallback'
            : 'WebGL / Three.js / SwiftShader',
        canvas,
        contextRequests: [...new Set(contextRequests)].sort(),
        modelRequest:
          modelResponse === null
            ? null
            : {
                url: basename(new URL(modelResponse.url()).pathname),
                status: modelResponse.status(),
                contentLength:
                  Number(
                    (await modelResponse.allHeaders())['content-length'],
                  ) || null,
              },
        projectileIds,
        activeForms,
        pageErrors: [...errors],
      });
    }
    return actual;
  } finally {
    await page.close();
  }
}

async function seek(page, target) {
  const pause = page.getByRole('button', { name: 'Pause', exact: true });
  if (await pause.isVisible()) {
    await pause.click();
    await page.waitForTimeout(25);
  }
  const root = page.locator(
    '[aria-label="Match timeline — drag to seek"]',
  );
  const thumb = page.getByRole('slider', { name: 'Playhead' });
  await root.waitFor({ state: 'visible', timeout: 120_000 });
  await thumb.waitFor({ state: 'attached', timeout: 120_000 });
  await root.scrollIntoViewIfNeeded();
  const maximum = Number(await thumb.getAttribute('aria-valuemax'));
  if (!Number.isFinite(maximum) || target > maximum)
    throw new Error(`Cannot seek to ${target}; maximum is ${maximum}.`);
  const box = await root.boundingBox();
  if (!box) throw new Error('Timeline has no measurable bounds.');

  await page.mouse.click(
    box.x + box.width * Math.max(0, Math.min(target / maximum, 1)),
    box.y + box.height / 2,
  );
  await settleFrames(page);
  await thumb.focus();

  let current = Number(await thumb.getAttribute('aria-valuenow'));
  let steps = Math.round((target - current) * 100);
  if (Math.abs(steps) > 250)
    throw new Error(
      `Pointer seek drifted too far: ${current} versus ${target}.`,
    );
  const key = steps >= 0 ? 'ArrowRight' : 'ArrowLeft';
  for (let index = 0; index < Math.abs(steps); index += 1)
    await page.keyboard.press(key);
  await settleFrames(page);
  current = Number(await thumb.getAttribute('aria-valuenow'));
  if (Math.abs(current - target) > 0.001)
    throw new Error(`Timeline settled at ${current}, expected ${target}.`);
}

async function settleFrames(page) {
  await page.evaluate(
    () =>
      new Promise((accept) =>
        requestAnimationFrame(() =>
          requestAnimationFrame(() =>
            requestAnimationFrame(accept),
          ),
        ),
      ),
  );
}

function panel(captureEntry, title, subtitle, color) {
  return {
    path: resolve(repository, captureEntry.file),
    title,
    subtitle,
    detail:
      `t=${captureEntry.time.toFixed(2)} · Team ${captureEntry.teamFocus} focus · ` +
      `${captureEntry.renderer}`,
    color,
  };
}

function emptyPanel(title, subtitle) {
  return {
    path: null,
    title,
    subtitle,
    detail: 'No mechanic exists in this native class replay.',
    color: '#776d65',
  };
}

async function contactSheet(path, panels, columns) {
  const panelWidth = 760;
  const panelHeight = 485;
  const header = 90;
  const gap = 2;
  const rows = Math.ceil(panels.length / columns);
  const board = createCanvas(
    panelWidth * columns + gap * (columns - 1),
    (panelHeight + header) * rows + gap * (rows - 1),
  );
  const context = board.getContext('2d');
  context.fillStyle = '#090706';
  context.fillRect(0, 0, board.width, board.height);

  for (let index = 0; index < panels.length; index += 1) {
    const panelEntry = panels[index];
    const column = index % columns;
    const row = Math.floor(index / columns);
    const x = column * (panelWidth + gap);
    const y = row * (panelHeight + header + gap);
    context.fillStyle = '#090706';
    context.fillRect(x, y, panelWidth, panelHeight + header);
    context.font = '700 18px sans-serif';
    context.fillStyle = panelEntry.color;
    context.fillText(panelEntry.title, x + 20, y + 28);
    context.font = '700 13px sans-serif';
    context.fillStyle = '#d5c3ae';
    context.fillText(panelEntry.subtitle, x + 20, y + 51);
    context.font = '12px monospace';
    context.fillStyle = '#8f8174';
    context.fillText(panelEntry.detail, x + 20, y + 72);
    context.fillStyle = '#080706';
    context.fillRect(x, y + header, panelWidth, panelHeight);
    if (panelEntry.path === null) {
      context.font = '700 28px sans-serif';
      context.fillStyle = '#443d38';
      context.fillText('N/A', x + panelWidth / 2 - 28, y + header + 240);
      continue;
    }
    const image = await loadImage(panelEntry.path);
    const cropWidth = Math.min(image.width, 1_120);
    const cropHeight = Math.min(image.height, 710);
    const cropX = Math.max(0, (image.width - cropWidth) / 2);
    const cropY = Math.max(0, (image.height - cropHeight) / 2);
    const scale = Math.min(
      panelWidth / cropWidth,
      panelHeight / cropHeight,
    );
    const width = cropWidth * scale;
    const height = cropHeight * scale;
    context.drawImage(
      image,
      cropX,
      cropY,
      cropWidth,
      cropHeight,
      x + (panelWidth - width) / 2,
      y + header + (panelHeight - height) / 2,
      width,
      height,
    );
  }
  await writeFile(path, board.toBuffer('image/png'));
}

async function gameplayDetailBoard(path, entries) {
  const panelWidth = 600;
  const panelHeight = 570;
  const columns = 2;
  const rows = Math.ceil(entries.length / columns);
  const gap = 2;
  const board = createCanvas(
    panelWidth * columns + gap,
    panelHeight * rows + gap * (rows - 1),
  );
  const context = board.getContext('2d');
  context.fillStyle = '#090706';
  context.fillRect(0, 0, board.width, board.height);
  for (let index = 0; index < entries.length; index += 1) {
    const entry = entries[index];
    const column = index % columns;
    const row = Math.floor(index / columns);
    const x = column * (panelWidth + gap);
    const y = row * (panelHeight + gap);
    const image = await loadImage(
      resolve(repository, entry.capture.file),
    );
    const header = 90;
    const availableHeight = panelHeight - header;
    const scale = Math.min(
      panelWidth / entry.crop.width,
      availableHeight / entry.crop.height,
    );
    const width = entry.crop.width * scale;
    const height = entry.crop.height * scale;
    const imageX = x + (panelWidth - width) / 2;
    const imageY = y + header + (availableHeight - height) / 2;
    context.font = '700 18px sans-serif';
    context.fillStyle = '#8bd5a6';
    context.fillText(entry.label, x + 18, y + 28);
    context.font = '700 13px sans-serif';
    context.fillStyle = '#d5c3ae';
    context.fillText(
      `PROJECTILE ${entry.projectileId} · AUTHORED WEBGL`,
      x + 18,
      y + 51,
    );
    context.font = '11px monospace';
    context.fillStyle = '#8f8174';
    context.fillText(
      `native t=${entry.capture.time.toFixed(2)} · ` +
        `${scale.toFixed(2)}× enlarged · corner brackets are review annotation`,
      x + 18,
      y + 72,
    );
    context.drawImage(
      image,
      entry.crop.x,
      entry.crop.y,
      entry.crop.width,
      entry.crop.height,
      imageX,
      imageY,
      width,
      height,
    );
    const targetX =
      imageX + (entry.target.x - entry.crop.x) * scale;
    const targetY =
      imageY + (entry.target.y - entry.crop.y) * scale;
    const radius = 22 * scale;
    const arm = 8 * scale;
    context.strokeStyle = '#f2d08b';
    context.lineWidth = Math.max(2, scale * 0.8);
    for (const [horizontal, vertical] of [
      [-1, -1],
      [1, -1],
      [-1, 1],
      [1, 1],
    ]) {
      const cornerX = targetX + horizontal * radius;
      const cornerY = targetY + vertical * radius;
      context.beginPath();
      context.moveTo(cornerX, cornerY + vertical * arm);
      context.lineTo(cornerX, cornerY);
      context.lineTo(cornerX + horizontal * arm, cornerY);
      context.stroke();
    }
  }
  await writeFile(path, board.toBuffer('image/png'));
}

function close(server) {
  return new Promise((accept) => server.close(accept));
}
