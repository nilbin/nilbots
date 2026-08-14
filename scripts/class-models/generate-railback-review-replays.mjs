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
const root = path.join(repository, 'sandbox/longshot-railback-v1/game-review');
const matchesRoot = path.join(root, 'matches');
const mind = path.join(repository, 'arena-bots/arc-relay/tactical-playbook-mind-v1');
const sheetsRoot = path.join(
  repository,
  'arena-bots/arc-relay/tactical-playbook-v1-2026-08-03/playbooks',
);
const hunter = path.join(sheetsRoot, 'hunter-tapline-v2.json');
const matchRunner = path.join(repository, 'scripts/arc-relay-match.py');
const cliProject = path.join(repository, 'src/BotArena.Cli/BotArena.Cli.csproj');

run('dotnet', ['build', cliProject, '--configuration', 'Debug', '--nologo'], repository);
rmSync(matchesRoot, { recursive: true, force: true });
mkdirSync(matchesRoot, { recursive: true });

const matchId = 'railback-current-tapline3';
const output = path.join(matchesRoot, matchId);
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
    hunter,
    '--sheet1',
    hunter,
    '--seed',
    '9147',
    '--output',
    output,
    '--cohort-id',
    'longshot-railback-review-v1',
    '--match-id',
    matchId,
    '--entrant0-id',
    'Railback teal proof · Hunter',
    '--entrant1-id',
    'Railback orange proof · Hunter',
    '--runtime',
    'in-process',
    '--loop-profile',
    'tapline-3',
  ],
  repository,
);

const replayPath = path.join(output, 'replay.json.gz');
const broadcastPath = path.join(output, 'broadcast.json.gz');
const receipt = JSON.parse(readFileSync(path.join(output, 'run.json'), 'utf8'));
const replay = JSON.parse(gunzipSync(readFileSync(replayPath)));
const broadcast = JSON.parse(gunzipSync(readFileSync(broadcastPath)));
const longshotsByTeam = receipt.Participants.map((participant) => ({
  teamId: participant.TeamId,
  count: participant.Classes.filter((classId) => classId === 'longshot').length,
}));
const railLineFacts = replay.ticks.flatMap((tick, tickIndex) =>
  (tick.events ?? []).flatMap((event) => {
    const fact = event.kind === 'arc-relay' ? event.payload?.fact : null;
    return fact?.signatureId === 'rail-line' ? [{ tick: tickIndex, fact }] : [];
  }),
);
if (
  receipt.RulesetId !== 'arc-relay-tapline-03' ||
  replay.header?.replayVersion !== 3 ||
  replay.header?.contract?.rules?.gameMode?.kind !== 'arc-relay' ||
  replay.partial !== false ||
  replay.replayHash !== receipt.Replay?.Hash ||
  broadcast.broadcastVersion !== 1 ||
  broadcast.canonicalReplayHash !== replay.replayHash ||
  broadcast.worlds?.length !== replay.ticks?.length ||
  longshotsByTeam.length !== 2 ||
  longshotsByTeam.some((entry) => entry.count < 1)
)
  throw new Error('Fresh current replay does not contain an authoritative Longshot on each team.');
if (readFileSync(broadcastPath).length > 300 * 1024)
  throw new Error('Compact Railback review broadcast exceeds 300 KiB.');

const stageId = 'rail-line-focus';
const stageOutput = path.join(matchesRoot, stageId);
run(
  'dotnet',
  [
    'run',
    '--project',
    cliProject,
    '--no-build',
    '--',
    'experiment',
    'arc-relay-signature-stage',
    '--signature',
    'rail-line',
    '--out',
    path.relative(repository, stageOutput),
  ],
  repository,
);
const stage = JSON.parse(readFileSync(path.join(stageOutput, 'stage.json'), 'utf8'));
const stageReplayPath = path.join(stageOutput, stage.replay);
if (
  stage.signatureId !== 'rail-line' ||
  stage.classId !== 'longshot' ||
  stage.attempts < 1 ||
  !stage.replayHash ||
  readFileSync(stageReplayPath).length > 100 * 1024
)
  throw new Error('Focused Rail Line replay is not review-ready.');

writeFileSync(
  path.join(root, 'replay-generation.json'),
  `${JSON.stringify({
    schemaVersion: 1,
    purpose:
      'Fresh current Tapline-3 real-match and focused Rail Line replay for Railback review',
    generatedAt: new Date().toISOString(),
    match: {
      id: matchId,
      seed: '9147',
      runtime: 'in-process',
      loopProfile: 'tapline-3',
      rulesetId: receipt.RulesetId,
      engineVersion: replay.header.engineVersion,
      replayHash: replay.replayHash,
      ticks: replay.ticks.length,
      result: receipt.Result,
      participants: receipt.Participants,
      longshotsByTeam,
      railLineFacts: railLineFacts.length,
      railLineFactTicks: [...new Set(railLineFacts.map((entry) => entry.tick))],
      canonicalReplay: path.relative(repository, replayPath),
      broadcast: path.relative(repository, broadcastPath),
      broadcastGzipBytes: readFileSync(broadcastPath).length,
    },
    focusedStage: {
      id: stageId,
      ...stage,
      replay: path.relative(repository, stageReplayPath),
      replayGzipBytes: readFileSync(stageReplayPath).length,
    },
  }, null, 2)}\n`,
);
console.log('Generated one current two-team match and one focused Rail Line stage.');

function run(command, arguments_, cwd) {
  const result = spawnSync(command, arguments_, { cwd, encoding: 'utf8', stdio: 'inherit' });
  if (result.error) throw result.error;
  if (result.status !== 0)
    throw new Error(`${command} failed with exit code ${result.status}.`);
}
