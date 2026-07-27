import assert from 'node:assert/strict';
import { mkdtemp, mkdir, readFile, rm, writeFile } from 'node:fs/promises';
import os from 'node:os';
import path from 'node:path';
import test from 'node:test';
import { fileURLToPath, pathToFileURL } from 'node:url';
import {
  auditReplayDocuments,
  auditReplayPaths,
  collectReplayFiles,
  formatAuditText,
  parseCliArguments,
  usage,
} from '../audit_soundtrack_director.mjs';

const repositoryRoot = path.resolve(
  path.dirname(fileURLToPath(import.meta.url)),
  '..',
  '..',
);

const participants = [
  {
    slot: 0,
    name: 'Alpha',
    runtimeKind: 'test',
    artifactHash: 'a',
    accent: '#38bdf8',
    spawnX: 0,
    spawnY: 0,
    spawnFacing: 'East',
  },
  {
    slot: 1,
    name: 'Beta',
    runtimeKind: 'test',
    artifactHash: 'b',
    accent: '#f97316',
    spawnX: 8,
    spawnY: 8,
    spawnFacing: 'West',
  },
];

const header = {
  replayVersion: 1,
  engineVersion: 'test',
  gameRulesVersion: 'test',
  runtimeProtocolVersion: '0.1',
  runtimeConfigurationVersion: '0.1',
  mapId: 'test-map',
  mapVersion: 1,
  mapWidth: 10,
  mapHeight: 10,
  mapTiles: Array.from({ length: 10 }, () => '.'.repeat(10)),
  seed: 1,
  maxTicks: 100,
  maxHealth: 3,
  visionRange: 6,
  participants,
};

test('audits replay directories with normalized pacing and dwell metrics', async () => {
  const temporary = await mkdtemp(path.join(os.tmpdir(), 'nilbots-score-audit-'));
  try {
    const quietDirectory = path.join(temporary, 'quiet');
    const actionDirectory = path.join(temporary, 'action');
    await Promise.all([mkdir(quietDirectory), mkdir(actionDirectory)]);
    await Promise.all([
      writeFile(
        path.join(quietDirectory, 'replay.json'),
        JSON.stringify(
          replay(Array.from({ length: 10 }, (_, tick) => replayTick(tick))),
        ),
      ),
      writeFile(
        path.join(actionDirectory, 'replay.json'),
        JSON.stringify(
          replay([
            replayTick(0, {
              events: [
                {
                  type: 'Damage',
                  slot: 0,
                  targetSlot: 1,
                  amount: 1,
                  newHealth: 2,
                },
              ],
              health: [3, 2],
            }),
            replayTick(1, {
              events: [{ type: 'Destroyed', slot: 1 }],
              statuses: ['Active', 'Destroyed'],
              health: [3, 0],
            }),
          ]),
        ),
      ),
    ]);

    const files = await collectReplayFiles([temporary]);
    assert.deepEqual(
      files.map((file) => path.basename(path.dirname(file))),
      ['action', 'quiet'],
    );

    const report = await auditReplayPaths([temporary], {
      cwd: temporary,
      ticksPerSecond: 5,
    });
    assert.equal(report.replayCount, 2);
    assert.deepEqual(report.tickRange, {
      minimum: 2,
      median: 6,
      maximum: 10,
    });
    assert.equal(report.options.phraseBarTicks, 10);
    assert.equal(report.options.phraseBarBpm, 120);
    assert.equal(report.options.phraseBarBeats, 4);
    assert.equal(report.overall.stateOccupancy.sparse.ticks, 11);
    assert.equal(report.overall.stateOccupancy.combat.ticks, 0);
    assert.equal(report.overall.stateOccupancy.resolve.ticks, 1);
    assert.deepEqual(report.overall.stateRuns.sparse, {
      runCount: 2,
      phraseBarTicks: 10,
      runsReachingOneBar: 1,
      percentReachingOneBar: 50,
      minimumTicks: 1,
      medianTicks: 5.5,
      maximumTicks: 10,
    });
    assert.equal(report.overall.transitionCount, 1);
    assert.equal(report.overall.skippedRankLeapCount, 1);
    assert.deepEqual(report.overall.maxDwell, {
      state: 'sparse',
      ticks: 10,
      seconds: 2,
      path: path.join('quiet', 'replay.json'),
    });
    assert.equal(report.overall.dominance.averagePercent, 75);
    assert.equal(report.overall.nonAcuteClimaxTicks, 0);
    assert.deepEqual(report.overall.maxNonAcuteClimaxRun, {
      ticks: 0,
      seconds: 0,
      path: null,
    });
    assert.match(formatAuditText(report), /short \(≤10s\): 2 replays/);
    assert.match(formatAuditText(report), /skipped-rank leaps: 1/);
    assert.match(formatAuditText(report), /sparse 1\/2 \(median 5.5t\)/);
  } finally {
    await rm(temporary, { recursive: true, force: true });
  }
});

test('uses validated pack tempo and meter for the one-bar threshold', () => {
  const entry = {
    path: 'quiet/replay.json',
    replay: replay(
      Array.from({ length: 10 }, (_, tick) => replayTick(tick)),
    ),
  };
  const report = auditReplayDocuments([entry], {
    ticksPerSecond: 5,
    phraseBarBpm: 60,
    phraseBarBeats: 3,
  });

  assert.equal(report.options.phraseBarBpm, 60);
  assert.equal(report.options.phraseBarBeats, 3);
  assert.equal(report.options.phraseBarTicks, 15);
  assert.equal(report.overall.stateRuns.sparse.runsReachingOneBar, 0);
  assert.match(formatAuditText(report), /One-bar runs \(≥15 ticks\)/);

  assert.deepEqual(
    parseCliArguments([
      '--bpm',
      '90',
      '--beats-per-bar=3',
      'quiet/replay.json',
    ]),
    {
      help: false,
      inputs: ['quiet/replay.json'],
      options: {
        phraseBarBpm: '90',
        phraseBarBeats: '3',
      },
      json: false,
    },
  );
  assert.match(usage(), /--bpm NUMBER/);
  assert.match(usage(), /--beats-per-bar NUMBER/);

  assert.throws(
    () => auditReplayDocuments([entry], { phraseBarBpm: 0 }),
    /phraseBarBpm must be a positive number/,
  );
  assert.throws(
    () => auditReplayDocuments([entry], { phraseBarBeats: 3.5 }),
    /phraseBarBeats must be a positive integer/,
  );
});

test('normalizes replay-v1, replay-v2, and an existing ReplayModel', async () => {
  const replayV1 = replay([replayTick(0)]);
  const replayV2 = JSON.parse(
    await readFile(
      path.join(
        repositoryRoot,
        'web',
        'tests',
        'fixtures',
        'frontline-replay-v2.json',
      ),
      'utf8',
    ),
  );
  const wireReport = auditReplayDocuments([
    { path: 'duel-v1.json', replay: replayV1 },
    { path: 'frontline-v2.json', replay: replayV2 },
  ]);
  assert.equal(wireReport.replayCount, 2);
  assert.deepEqual(wireReport.replays.map(({ tickCount }) => tickCount), [1, 12]);

  const harness = await import(
    pathToFileURL(
      path.join(
        repositoryRoot,
        'web',
        'tests',
        '.harness',
        'harness.entry.js',
      ),
    ).href
  );
  const normalized = harness.loadReplayObject(replayV1).replay;
  const modelReport = auditReplayDocuments([
    { path: 'normalized-v1', replay: normalized },
  ]);
  assert.equal(modelReport.replayCount, 1);
  assert.equal(modelReport.replays[0].tickCount, 1);
});

test('reports non-acute climax ticks and their longest causal run', () => {
  const projectiles = Array.from({ length: 3 }, (_, index) => ({
    x: index + 2,
    y: 4,
    direction: 'East',
    ownerSlot: 0,
  }));
  const ticks = Array.from({ length: 30 }, (_, tick) =>
    replayTick(tick, {
      health: [1, 3],
      projectiles: tick <= 12 ? projectiles : undefined,
    }),
  );

  const report = auditReplayDocuments([
    { path: 'climax/replay.json', replay: replay(ticks) },
  ]);
  const summary = report.overall;

  assert.ok(summary.stateRuns.climax.runCount >= 1);
  assert.ok(summary.stateRuns.climax.runsReachingOneBar >= 1);
  assert.ok(summary.nonAcuteClimaxTicks > 0);
  assert.ok(summary.maxNonAcuteClimaxRun.ticks > 0);
  assert.equal(summary.maxNonAcuteClimaxRun.path, 'climax/replay.json');
  assert.match(formatAuditText(report), /Non-acute climax: [1-9]\d* ticks/);
});

function replay(ticks) {
  return {
    header,
    ticks,
    partial: true,
  };
}

function replayTick(
  tick,
  {
    events = [],
    statuses = ['Active', 'Active'],
    health = [3, 3],
    projectiles,
  } = {},
) {
  return {
    tick,
    bots: [0, 1].map((slot) => ({
      slot,
      chosenAction: 'Wait',
      validatedAction: 'Wait',
      result: 'Success',
      faulted: false,
      visibleTiles: [],
      visibleEnemies: [],
    })),
    events,
    state: [0, 1].map((slot) => ({
      slot,
      x: slot === 0 ? 0 : 8,
      y: slot === 0 ? 0 : 8,
      facing: slot === 0 ? 'East' : 'West',
      health: health[slot],
      cooldown: 0,
      status: statuses[slot],
    })),
    ...(projectiles === undefined ? {} : { projectiles }),
  };
}
