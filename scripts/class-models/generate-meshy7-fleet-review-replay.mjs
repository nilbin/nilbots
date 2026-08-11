#!/usr/bin/env node

import { spawnSync } from 'node:child_process';
import {
  existsSync,
  mkdirSync,
  readFileSync,
  rmSync,
  writeFileSync,
} from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { gunzipSync } from 'node:zlib';

const repository = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..', '..');
const cli = path.join(repository, 'src/BotArena.Cli/bin/Debug/net10.0/botarena.dll');
const stockBot = path.join(
  repository,
  'arena-bots/arc-relay/native-cohort-v2-fresh-2026-08-01/convoy-fresh',
);
const reviewDirectory = path.join(repository, 'sandbox/meshy7-pilot/fleet-game-review');
const matchDirectory = path.join(reviewDirectory, 'match');
const teams = [
  {
    id: 'meshy7-fleet-review-blue',
    composition: [
      'kestrel',
      'palisade',
      'towline',
      'patchbay',
      'lantern',
      'mortar',
      'minesmith',
      'hush',
    ],
  },
  {
    id: 'meshy7-fleet-review-orange',
    composition: [
      'relay',
      'switchback',
      'longshot',
      'mason',
      'sunder',
      'repulsor',
      'veil',
      'nest',
    ],
  },
];

if (!existsSync(cli))
  throw new Error(`Missing debug CLI ${path.relative(repository, cli)}; run dotnet build first.`);
if (!existsSync(stockBot)) throw new Error('Missing the sheet-free Arc Relay review mind.');

mkdirSync(reviewDirectory, { recursive: true });
rmSync(matchDirectory, { recursive: true, force: true });
run('dotnet', [
  cli,
  'experiment',
  'arc-relay',
  '--bot',
  stockBot,
  '--opponent',
  stockBot,
  '--classes0',
  teams[0].composition.join(','),
  '--classes1',
  teams[1].composition.join(','),
  '--runtime',
  'in-process',
  '--loop-profile',
  'depth-counterflow',
  '--seed',
  '70716',
  '--out',
  matchDirectory,
]);

const replayPath = path.join(matchDirectory, 'replay.json.gz');
const runPath = path.join(matchDirectory, 'run.json');
if (!existsSync(replayPath) || !existsSync(runPath))
  throw new Error('Arc Relay review match emitted no canonical replay or run receipt.');
const replay = JSON.parse(gunzipSync(readFileSync(replayPath)));
const runReceipt = JSON.parse(readFileSync(runPath, 'utf8'));
const classes = new Set(
  runReceipt.Participants?.flatMap((participant) => participant.Classes ?? []),
);
const expected = new Set(teams.flatMap((team) => team.composition));
if (
  replay.header?.replayVersion !== 3 ||
  replay.header?.contract?.rules?.gameMode?.kind !== 'arc-relay' ||
  replay.partial !== false ||
  replay.ticks?.length === 0 ||
  replay.replayHash !== runReceipt.Replay?.Hash ||
  expected.size !== 16 ||
  [...expected].some((classId) => !classes.has(classId))
)
  throw new Error('Generated canonical replay does not expose the complete Arc Relay fleet.');

writeFileSync(
  path.join(reviewDirectory, 'replay-generation.json'),
  `${JSON.stringify({
    schemaVersion: 1,
    seed: '70716',
    runtime: 'in-process',
    bot: path.relative(repository, stockBot),
    compositions: teams,
    replay: path.relative(repository, replayPath),
    run: path.relative(repository, runPath),
    ticks: replay.ticks.length,
    result: runReceipt.Result,
    presentedClasses: [...classes].sort(),
  }, null, 2)}\n`,
);
console.log(`Generated complete-fleet review replay with ${replay.ticks.length} ticks.`);

function run(command, arguments_) {
  const result = spawnSync(command, arguments_, {
    cwd: repository,
    encoding: 'utf8',
    stdio: 'inherit',
  });
  if (result.error) throw result.error;
  if (result.status !== 0)
    throw new Error(`${command} failed with exit code ${result.status}.`);
}
