#!/usr/bin/env node

import { createHash } from 'node:crypto';
import { gunzipSync } from 'node:zlib';
import {
  mkdirSync,
  readFileSync,
  writeFileSync,
} from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { withCanonicalTeamVision } from './arc-relay-team-vision.mjs';

const repository = path.resolve(
  path.dirname(fileURLToPath(import.meta.url)),
  '..',
);
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

const [command, ...args] = process.argv.slice(2);
if (command === 'prepare-sheets') {
  const output = requiredPath(args[0], 'sheet output directory');
  const stock = JSON.parse(
    readFileSync(
      path.join(repository, 'arena-bots', 'arc-relay', 'stock-mind-v0', 'sheet.json'),
      'utf8',
    ),
  );
  mkdirSync(output, { recursive: true });
  for (const [name, composition] of [
    ['team0.json', teamZero],
    ['team1.json', teamOne],
  ])
    writeFileSync(
      path.join(output, name),
      `${JSON.stringify({ ...stock, composition }, null, 2)}\n`,
    );
  console.log(`Prepared Arc Relay review sheets in ${output}`);
} else if (command === 'install') {
  const runPath = requiredPath(args[0], 'run receipt');
  const replayPath = requiredPath(args[1], 'canonical replay');
  const transportPath = args[2]
    ? requiredPath(args[2], 'broadcast transport')
    : replayPath;
  const dist = path.join(repository, 'web', 'dist-review');
  const review = path.join(repository, 'art', 'reviews', 'arc-relay-3d');
  const run = JSON.parse(readFileSync(runPath, 'utf8'));
  const replayGzip = readFileSync(replayPath);
  const replayJson = gunzipSync(replayGzip).toString('utf8');
  const replay = JSON.parse(replayJson);
  const transportGzip = readFileSync(transportPath);
  const transportJson = gunzipSync(transportGzip).toString('utf8');
  const transport = JSON.parse(transportJson);
  const classes = run.Participants.flatMap((participant) => participant.Classes);
  const expected = [...teamZero, ...teamOne];
  if (
    replay.header?.replayVersion !== 3 ||
    replay.header?.contract?.rules?.gameMode?.kind !== 'arc-relay' ||
    replay.ticks?.length < 2 ||
    new Set(classes).size !== 16 ||
    expected.some((classId) => !classes.includes(classId))
  )
    throw new Error('Review replay is not a full 16-class Arc Relay replay-v3.');
  if (
    transportPath !== replayPath &&
    (transport.broadcastVersion !== 1 ||
      transport.canonicalReplayHash !== run.Replay.Hash ||
      transport.worlds?.length !== replay.ticks.length)
  )
    throw new Error('Review broadcast does not preserve the canonical replay identity.');

  const runtimeTransport = withCanonicalTeamVision(transport, replay);
  const runtimeJson = runtimeTransport === transport
    ? transportJson
    : JSON.stringify(runtimeTransport);
  mkdirSync(dist, { recursive: true });
  writeFileSync(path.join(dist, 'replay.json'), runtimeJson);
  writeFileSync(path.join(dist, 'replay.json.gz'), transportGzip);
  writeFileSync(
    path.join(dist, 'replays.json'),
    `${JSON.stringify([
      {
        id: 'arc-relay-3d-fleet',
        url: 'replay.json',
        map: run.MapId,
        bots: run.Participants.map((participant) => participant.Name),
        ticks: replay.ticks.length,
        reason: null,
      },
    ], null, 2)}\n`,
  );

  mkdirSync(path.join(review, 'stills'), { recursive: true });
  const metadata = {
    schemaVersion: 1,
    replayVersion: replay.header.replayVersion,
    mapId: run.MapId,
    seed: run.Seed,
    canonicalReplayHash: run.Replay.Hash,
    transport: {
      kind: transport.broadcastVersion === 1 ? 'arc-relay-broadcast-v1' : 'canonical-replay-v3',
      gzipBytes: transportGzip.length,
      gzipSha256: createHash('sha256').update(transportGzip).digest('hex'),
      vision: transport.vision === undefined && runtimeTransport !== transport
        ? 'canonical-replay'
        : 'transport',
    },
    ticks: replay.ticks.length,
    viewport: { width: 1440, height: 900 },
    cameraPitchDegrees: 58,
    team0: teamZero,
    team1: teamOne,
    comparisonTicks: reviewTicks(replay),
  };
  writeFileSync(
    path.join(review, 'replay-metadata.json'),
    `${JSON.stringify(metadata, null, 2)}\n`,
  );
  writeFileSync(path.join(review, 'index.html'), reviewHtml(metadata));
  console.log(
    `Installed ${replay.ticks.length}-tick review replay ${run.Replay.Hash}`,
  );
} else {
  throw new Error(
    'Usage: build-arc-relay-3d-review.mjs prepare-sheets <dir> | install <run.json> <replay.json.gz> [broadcast.json.gz]',
  );
}

function requiredPath(value, label) {
  if (!value) throw new Error(`Missing ${label}.`);
  return path.resolve(repository, value);
}

function reviewTicks(replay) {
  const story = { birth: null, steal: null, bank: null, pulse: null };
  const signatures = {};
  const seenCores = new Set();
  const coreTeams = new Map();
  for (const tick of replay.ticks) {
    for (const event of tick.events ?? []) {
      const fact =
        event.kind === 'arc-relay'
          ? event.payload?.fact
          : event.arcRelayFact;
      const core = fact?.coreId ? `${fact.coreId.sourceWellId}:${fact.coreId.sourceOrdinal}` : null;
      if (fact?.kind === 'core-picked-up' && core) {
        const teamId = fact.carrierActorId?.teamId ?? fact.carrierActor?.teamId;
        const prior = coreTeams.get(core);
        if (prior !== undefined && prior !== teamId && story.steal === null)
          story.steal = tick.tick;
        coreTeams.set(core, teamId);
      }
      if (fact?.kind === 'core-handed-off' && core)
        coreTeams.set(
          core,
          fact.targetActorId?.teamId ?? fact.targetActor?.teamId,
        );
      if (fact?.kind === 'core-relocated' && core) {
        const teamId = fact.carrierActorId?.teamId ?? fact.carrierActor?.teamId;
        if (teamId !== undefined && teamId !== null) coreTeams.set(core, teamId);
      }
      if (fact?.kind === 'core-banked' && story.bank === null) story.bank = tick.tick;
      if (fact?.kind === 'pulse' && story.pulse === null) story.pulse = tick.tick;
    }
    const mode = tick.postState?.mode ?? tick.after?.mode;
    for (const core of mode?.visibleCores ?? []) {
      const key = `${core.coreId.sourceWellId}:${core.coreId.sourceOrdinal}`;
      if (!seenCores.has(key) && story.birth === null) story.birth = tick.tick;
      seenCores.add(key);
    }
    for (const signature of mode?.visibleSignatures ?? [])
      signatures[signature.signatureId] ??= tick.tick;
  }
  return { opening: 0, story, signatures };
}

function reviewHtml(metadata) {
  const rows = [...teamZero, ...teamOne]
    .map(
      (classId) => `<tr>
        <th>${title(classId)}</th>
        <td><img loading="lazy" src="stills/${classId}-2d.png" alt="${title(classId)} Canvas2D reference"></td>
        <td><img loading="lazy" src="stills/${classId}-3d.png" alt="${title(classId)} authored 3D model"></td>
      </tr>`,
    )
    .join('\n');
  return `<!doctype html>
<html lang="en">
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width,initial-scale=1">
  <title>Arc Relay 3D fleet review</title>
  <style>
    :root { color-scheme: dark; font: 15px/1.5 ui-monospace, SFMono-Regular, Menlo, monospace; background:#070b10; color:#dbe7ef; }
    body { margin:0 auto; max-width:1600px; padding:28px; }
    h1 { font:700 clamp(28px,4vw,54px)/1.05 system-ui,sans-serif; margin:.2em 0; }
    .meta { color:#8fa5b4; max-width:90ch; }
    .actions { display:flex; flex-wrap:wrap; gap:12px; margin:24px 0; }
    .arena-pair { display:grid; grid-template-columns:repeat(2,minmax(0,1fr)); gap:14px; margin:24px 0; }
    figure { margin:0; } figcaption { color:#8fa5b4; margin-top:6px; }
    a { color:#7de5f5; } .button { padding:10px 14px; border:1px solid #295365; background:#0e202a; text-decoration:none; }
    table { border-collapse:collapse; width:100%; table-layout:fixed; background:#0a1118; }
    th,td { border:1px solid #1d2a35; padding:10px; vertical-align:top; }
    th { width:9rem; text-align:left; color:#f0b876; }
    img { width:100%; height:auto; min-height:120px; object-fit:contain; background:#080d12; }
    code { color:#b7cbd7; }
    @media(max-width:760px){ body{padding:14px} .arena-pair{grid-template-columns:1fr} th{width:5.7rem} th,td{padding:5px;font-size:11px} }
  </style>
</head>
<body>
  <p class="meta">RENDERER REVIEW · PRESENTATION ONLY</p>
  <h1>Arc Relay authored 3D fleet</h1>
  <p class="meta">Pinned A/B frames use replay <code>${metadata.canonicalReplayHash}</code>, seed ${metadata.seed}, viewport ${metadata.viewport.width}×${metadata.viewport.height}, and the fixed 58° gameplay camera. Each table image pairs amber and cyan assignments for that class at real arena scale. The index intentionally contains no match outcome.</p>
  <p class="meta">The WebGL bodies use orthographic named-layer relief. They deliberately do not remap the canonical Canvas2D sprites' baked 20° projection through a second camera; this A/B keeps that treatment difference visible for the owner's taste gate.</p>
  <div class="actions">
    <a class="button" href="../../../web/dist-review/?standalone&amp;audio=off">Open full interactive 3D replay</a>
    <a class="button" href="replay-metadata.json">Replay and capture metadata</a>
  </div>
  <section class="arena-pair" aria-label="Pinned arena comparison">
    <figure><img src="stills/arena-tick000-2d.png" alt="Pinned Canvas2D Arc Relay opening frame"><figcaption>Canvas2D · tick 000 · fixed overview</figcaption></figure>
    <figure><img src="stills/arena-tick000-3d.png" alt="Pinned authored 3D Arc Relay opening frame"><figcaption>WebGL · tick 000 · fixed 58° overview</figcaption></figure>
  </section>
  <table>
    <thead><tr><th>Class</th><th>Canonical Canvas2D</th><th>Authored WebGL</th></tr></thead>
    <tbody>${rows}</tbody>
  </table>
</body>
</html>\n`;
}

function title(value) {
  return value.replace(/(^|-)([a-z])/g, (_match, separator, letter) =>
    `${separator ? ' ' : ''}${letter.toUpperCase()}`,
  );
}
