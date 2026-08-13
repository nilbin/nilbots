#!/usr/bin/env node

import { spawnSync } from 'node:child_process';
import {
  mkdirSync,
  readFileSync,
  rmSync,
  writeFileSync,
} from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { gunzipSync } from 'node:zlib';

const repository = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..', '..');
const root = path.join(repository, 'sandbox/longshot-objectives-meshy7-v1/combined-review');
const matchesRoot = path.join(root, 'matches');
const mind = path.join(repository, 'arena-bots/arc-relay/tactical-playbook-mind-v1');
const sheetsRoot = path.join(
  repository,
  'arena-bots/arc-relay/tactical-playbook-v1-2026-08-03/playbooks',
);
const hunter = path.join(sheetsRoot, 'hunter-tapline-v2.json');
const wellwright = path.join(sheetsRoot, 'wellwright-tapline-v2.json');
const matchRunner = path.join(repository, 'scripts/arc-relay-match.py');
const cliProject = path.join(repository, 'src/BotArena.Cli/BotArena.Cli.csproj');
const plans = [
  {
    id: 'all-four-hunter-west',
    sheet0: hunter,
    sheet1: wellwright,
    entrant0: 'Hunter Tapline v2 · Longshot west',
    entrant1: 'Wellwright Tapline v2 · Lantern east',
  },
  {
    id: 'all-four-hunter-east',
    sheet0: wellwright,
    sheet1: hunter,
    entrant0: 'Wellwright Tapline v2 · Lantern west',
    entrant1: 'Hunter Tapline v2 · Longshot east',
  },
];

run('dotnet', ['build', cliProject, '--configuration', 'Debug', '--nologo'], repository);
rmSync(matchesRoot, { recursive: true, force: true });
mkdirSync(matchesRoot, { recursive: true });

const records = [];
for (const plan of plans) {
  const output = path.join(matchesRoot, plan.id);
  run(
    'python3',
    [
      matchRunner,
      'run',
      '--artifact0',
      mind,
      '--artifact1',
      mind,
      '--sheet0',
      plan.sheet0,
      '--sheet1',
      plan.sheet1,
      '--seed',
      '9001',
      '--output',
      output,
      '--cohort-id',
      'longshot-objectives-combined-review-v1',
      '--match-id',
      plan.id,
      '--entrant0-id',
      plan.entrant0,
      '--entrant1-id',
      plan.entrant1,
      '--runtime',
      'in-process',
      '--loop-profile',
      'tapline-3',
    ],
    repository,
  );

  const replayPath = path.join(output, 'replay.json.gz');
  const broadcastPath = path.join(output, 'broadcast.json.gz');
  const runReceipt = JSON.parse(readFileSync(path.join(output, 'run.json'), 'utf8'));
  const replay = JSON.parse(gunzipSync(readFileSync(replayPath)));
  const broadcast = JSON.parse(gunzipSync(readFileSync(broadcastPath)));
  const classes = runReceipt.Participants.flatMap((participant) => participant.Classes);
  const surveyFlares = replay.ticks.flatMap((tick) => tick.events ?? []).filter(
    (event) =>
      event.kind === 'arc-relay' &&
      event.payload?.fact?.kind === 'signature-changed' &&
      event.payload.fact.signatureId === 'survey-flare' &&
      event.payload.fact.phase === 'in-flight',
  );

  if (
    runReceipt.RulesetId !== 'arc-relay-tapline-03' ||
    replay.header?.replayVersion !== 3 ||
    replay.header?.contract?.rules?.gameMode?.kind !== 'arc-relay' ||
    replay.partial !== false ||
    replay.replayHash !== runReceipt.Replay?.Hash ||
    broadcast.broadcastVersion !== 1 ||
    broadcast.canonicalReplayHash !== replay.replayHash ||
    broadcast.worlds?.length !== replay.ticks?.length ||
    !classes.includes('longshot') ||
    !classes.includes('lantern') ||
    surveyFlares.length === 0
  )
    throw new Error(`${plan.id}: current replay does not exercise all four review subjects.`);
  if (readFileSync(broadcastPath).length > 300 * 1024)
    throw new Error(`${plan.id}: compact review broadcast exceeds 300 KiB.`);

  records.push({
    id: plan.id,
    seed: '9001',
    runtime: 'in-process',
    loopProfile: 'tapline-3',
    rulesetId: runReceipt.RulesetId,
    engineVersion: replay.header.engineVersion,
    replayHash: replay.replayHash,
    ticks: replay.ticks.length,
    result: runReceipt.Result,
    participants: runReceipt.Participants,
    surveyFlareLaunches: surveyFlares.length,
    canonicalReplay: path.relative(repository, replayPath),
    broadcast: path.relative(repository, broadcastPath),
    broadcastGzipBytes: readFileSync(broadcastPath).length,
  });
}

writeFileSync(
  path.join(root, 'replay-generation.json'),
  `${JSON.stringify({
    schemaVersion: 1,
    purpose:
      'Fresh current Tapline-3 review pair for Longshot, Survey Flare, Core Wells, and both Team Reactors',
    generatedAt: new Date().toISOString(),
    matches: records,
  }, null, 2)}\n`,
);
console.log(`Generated ${records.length} current combined-review replays.`);

function run(command, arguments_, cwd) {
  const result = spawnSync(command, arguments_, {
    cwd,
    encoding: 'utf8',
    stdio: 'inherit',
  });
  if (result.error) throw result.error;
  if (result.status !== 0)
    throw new Error(`${command} failed with exit code ${result.status}.`);
}
