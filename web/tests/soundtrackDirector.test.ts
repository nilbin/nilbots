import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import test from 'node:test';
// The director reaches arenaThemes through replayPresentation. Vite resolves
// that module's import.meta.glob while building this shared SSR test harness.
import {
  buildAdaptiveTimeline,
  loadReplayObject,
  sampleAdaptiveTimeline,
} from './.harness/harness.entry.js';
import type {
  ReplayCausalEvent,
  ReplayModel,
  ReplayTick,
} from '../src/replayModel.ts';
import type {
  ReplayV1BotAction,
  ReplayV1BotStatus,
  ReplayV1CompleteDocument,
  ReplayV1GameEvent,
  ReplayV1Header,
  ReplayV1MatchResult,
  ReplayV1PartialDocument,
  ReplayV1Position,
  ReplayV1Projectile,
  ReplayV1ProjectileTraversal,
  ReplayV1Tick,
} from '../src/replayWireV1.ts';
import type {
  ReplayV2CompleteDocument,
  ReplayV2PartialDocument,
} from '../src/replayWireV2.ts';
import {
  replayV1LivePartialFixtureInput,
  replayV2FixtureInput,
  replayV2ZeroTickPartialFixtureInput,
} from './support/replayFixtureInputs.ts';

const V1_SLOTS = [0, 1] as const;
const V1_PARTICIPANTS = [
  {
    slot: 0,
    name: 'Alpha',
    runtimeKind: 'test',
    artifactHash: 'a',
    accent: '#38bdf8',
    spawnX: 0,
    spawnY: 0,
    spawnFacing: 'East' as const,
  },
  {
    slot: 1,
    name: 'Beta',
    runtimeKind: 'test',
    artifactHash: 'b',
    accent: '#f97316',
    spawnX: 8,
    spawnY: 8,
    spawnFacing: 'West' as const,
  },
];
const FRONTLINE_COMPLETE_WIRE = JSON.parse(
  readFileSync(
    new URL('./fixtures/frontline-replay-v2.json', import.meta.url),
    'utf8',
  ),
) as ReplayV2CompleteDocument;
const ARENA_COMPLETE_WIRE = JSON.parse(
  readFileSync(
    new URL('./fixtures/golden-replay.json', import.meta.url),
    'utf8',
  ),
) as ReplayV1CompleteDocument;

interface V1ReplayOptions {
  header?: Partial<ReplayV1Header>;
  result?: ReplayV1MatchResult;
}

interface V1TickOptions {
  events?: ReplayV1GameEvent[];
  positions?: readonly [ReplayV1Position, ReplayV1Position];
  health?: readonly [number, number];
  statuses?: readonly [ReplayV1BotStatus, ReplayV1BotStatus];
  visible?: boolean;
  actions?: readonly [ReplayV1BotAction, ReplayV1BotAction];
  controlPressure?: number;
  projectiles?: ReplayV1Projectile[];
  projectileTraversals?: ReplayV1ProjectileTraversal[];
  zoneTicks?: readonly [number, number];
}

test('zero-tick v1 and v2 partials produce the same safe sparse sample', () => {
  const partials = [
    v1Replay([]),
    loadReplayObject(replayV2ZeroTickPartialFixtureInput()).replay,
  ];

  for (const replay of partials) {
    const timeline = buildAdaptiveTimeline(replay);
    const sample = sampleAdaptiveTimeline(timeline, 0.5);

    assert.equal(timeline.frames.length, 0);
    assert.equal(sample.state, 'sparse');
    assert.equal(sample.sourceTick, -1);
    assert.equal(sample.intensity, timeline.config.stateIntensity.sparse);
    assert.deepEqual(sample.triggers, []);
  }
});

test('every v1 prefix is byte-equivalent to the same full causal prefix', () => {
  const ticks = Array.from({ length: 8 }, (_, index) =>
    v1Tick(index, {
      events:
        index === 5
          ? [
              {
                type: 'Damage',
                slot: 0,
                targetSlot: 1,
                amount: 1,
                newHealth: 2,
              },
            ]
          : index === 7
            ? [{ type: 'Destroyed', slot: 1 }]
            : [],
      health: index >= 5 ? [3, 2] : [3, 3],
      statuses:
        index === 7 ? ['Active', 'Destroyed'] : ['Active', 'Active'],
    }),
  );
  const result = v1Result(ticks, 7, 'Elimination', 0);
  const full = buildAdaptiveTimeline(v1Replay(ticks, { result }), {
    planningMode: 'causal',
  });
  const repeated = buildAdaptiveTimeline(v1Replay(ticks, { result }), {
    planningMode: 'causal',
  });

  assert.deepEqual(full, repeated);
  for (let count = 0; count <= ticks.length; count += 1) {
    const prefix = buildAdaptiveTimeline(v1Replay(ticks.slice(0, count)));
    assert.deepEqual(
      prefix.frames,
      full.frames.slice(0, count),
      `v1 prefix length ${count}`,
    );
  }
  assert.ok(full.frames.slice(0, 7).every((frame) => frame.state !== 'resolve'));
  assert.equal(full.frames[7]?.state, 'resolve');
});

test('finalized Arena replay gets one relative primary arc without moving evidence', () => {
  const replay = loadReplayObject(
    structuredClone(ARENA_COMPLETE_WIRE),
  ).replay;
  const planned = buildAdaptiveTimeline(replay);
  const causal = buildAdaptiveTimeline(replay, {
    planningMode: 'causal',
  });

  assert.equal(planned.mode, 'retrospective');
  assert.deepEqual(planned, buildAdaptiveTimeline(replay));
  assert.equal(planned.planningBarTicks, 10);
  assert.equal(planned.highlights.length, 1);
  assert.deepEqual(
    {
      startTick: planned.highlights[0]?.startTick,
      peakTick: planned.highlights[0]?.peakTick,
      endTick: planned.highlights[0]?.endTick,
      kind: planned.highlights[0]?.kind,
      primary: planned.highlights[0]?.primary,
      requestTick: planned.highlights[0]?.requestTick,
      buildStartTick: planned.highlights[0]?.buildStartTick,
    },
    {
      startTick: 65,
      peakTick: 96,
      endTick: 96,
      kind: 'decisive',
      primary: true,
      requestTick: 60,
      buildStartTick: 40,
    },
  );

  const gameplayTransitions = planned.frames.filter(
    (frame, index) =>
      index > 0 &&
      frame.state !== 'resolve' &&
      frame.state !== planned.frames[index - 1]?.state,
  );
  assert.deepEqual(
    gameplayTransitions.map((frame) => [frame.tick, frame.state]),
    [[60, 'climax']],
  );
  assert.ok(
    planned.frames.slice(0, 5).every((frame) => frame.intensity === 0.08),
  );
  assert.equal(planned.frames[6]?.state, 'sparse');
  assert.ok((planned.frames[6]?.intensity ?? 0) > 0.08);
  assert.ok((planned.frames[6]?.intensity ?? 1) < 0.14);
  assert.ok(
    (planned.frames[65]?.intensity ?? 1) <
      (causal.frames[65]?.intensity ?? 0),
  );
  assert.ok((causal.frames[65]?.intensity ?? 0) >= 0.62);
  assert.ok(
    Math.abs(
      (planned.frames[65]?.intensity ?? 0) -
        (planned.frames[64]?.intensity ?? 0),
    ) < 0.04,
  );
  assert.equal(planned.frames[96]?.state, 'resolve');

  assert.deepEqual(
    planned.frames.map((frame) => frame.features),
    causal.frames.map((frame) => frame.features),
  );
  assert.deepEqual(
    planned.frames.map((frame) => frame.triggers),
    causal.frames.map((frame) => frame.triggers),
  );
});

test('partial documents and live followers stay forced causal', () => {
  const completeReplay = loadReplayObject(
    structuredClone(ARENA_COMPLETE_WIRE),
  ).replay;
  const partialWire: ReplayV1PartialDocument = {
    header: structuredClone(ARENA_COMPLETE_WIRE.header),
    ticks: structuredClone(ARENA_COMPLETE_WIRE.ticks),
    result: null,
    replayHash: null,
    partial: true,
  };
  const partialReplay = loadReplayObject(partialWire).replay;
  const forced = buildAdaptiveTimeline(completeReplay, {
    planningMode: 'causal',
  });
  const following = buildAdaptiveTimeline(completeReplay, {
    followingLive: true,
  });
  const partial = buildAdaptiveTimeline(partialReplay, {
    planningMode: 'retrospective',
  });

  assert.equal(following.mode, 'causal');
  assert.equal(partial.mode, 'causal');
  assert.deepEqual(following.frames, forced.frames);
  assert.deepEqual(partial.frames, forced.frames);
  assert.deepEqual(following.highlights, []);
  assert.deepEqual(partial.highlights, []);
});

test('v1 prefix causality does not depend on a future inferred max health', () => {
  const ticks = [
    v1Tick(0, { health: [2, 3] }),
    v1Tick(1, { health: [5, 5] }),
  ];
  const options: V1ReplayOptions = {
    header: { maxHealth: undefined },
  };
  const full = buildAdaptiveTimeline(v1Replay(ticks, options));

  assert.equal(full.frames[0]?.features.healthUrgency, 0.5);
  for (let count = 0; count <= ticks.length; count += 1) {
    const prefix = buildAdaptiveTimeline(
      v1Replay(ticks.slice(0, count), options),
    );
    assert.deepEqual(
      prefix.frames,
      full.frames.slice(0, count),
      `v1 inferred-health prefix length ${count}`,
    );
  }
});

test('unseen approach stays quiet at range and raises causal pressure only nearby', () => {
  const positions: readonly (readonly [
    ReplayV1Position,
    ReplayV1Position,
  ])[] = [
    [[0, 0], [12, 0]],
    [[1, 0], [11, 0]],
    [[2, 0], [10, 0]],
    [[3, 0], [9, 0]],
    [[4, 0], [8, 0]],
    [[5, 0], [7, 0]],
    [[5, 0], [7, 0]],
    [[5, 0], [7, 0]],
  ];
  const ticks = positions.map((position, tick) =>
    v1Tick(tick, {
      positions: position,
      visible: false,
      actions: tick > 0 && tick < 6
        ? ['MoveForward', 'MoveForward']
        : ['Wait', 'Wait'],
    }),
  );
  const full = buildAdaptiveTimeline(v1Replay(ticks));

  assert.equal(full.frames[0]?.features.approach, 0);
  assert.equal(full.frames[1]?.features.approach, 0);
  assert.equal(full.frames[2]?.features.approach, 0);
  assert.ok((full.frames[3]?.features.approach ?? 0) > 0);
  assert.ok(
    (full.frames[5]?.features.closingPressure ?? 0) >
      (full.frames[3]?.features.closingPressure ?? 1),
  );
  assert.equal(full.frames[4]?.state, 'sparse');
  assert.equal(full.frames[5]?.state, 'tension');
  assert.ok(full.frames.every((frame) => frame.state !== 'pursuit'));
  assert.ok(
    full.frames.every(
      (frame) =>
        frame.features.contact < 1 &&
        !frame.triggers.includes('contact'),
    ),
  );
  for (let index = 1; index < full.frames.length; index += 1) {
    assert.ok(
      stateRank(full.frames[index]!.state) -
        stateRank(full.frames[index - 1]!.state) <=
        1,
      `state skipped upward at tick ${full.frames[index]!.tick}`,
    );
  }
  for (let count = 0; count <= ticks.length; count += 1) {
    const prefix = buildAdaptiveTimeline(v1Replay(ticks.slice(0, count)));
    assert.deepEqual(
      prefix.frames,
      full.frames.slice(0, count),
      `approach prefix length ${count}`,
    );
  }
});

test('movement and distant approach cannot seed a retrospective highlight', () => {
  const ticks = Array.from({ length: 40 }, (_, tick) =>
    v1Tick(tick, {
      positions: [
        [0, 0],
        [Math.max(11, 20 - tick), 0],
      ],
      actions: tick < 10
        ? ['Wait', 'MoveForward']
        : ['Wait', 'Wait'],
    }),
  );
  const replay = v1Replay(ticks, {
    header: { maxTicks: 40 },
    result: v1Result(ticks, 39, 'MaxTicks'),
  });
  const timeline = buildAdaptiveTimeline(replay);

  assert.equal(timeline.mode, 'retrospective');
  assert.deepEqual(timeline.highlights, []);
  assert.ok(
    timeline.frames
      .slice(0, -1)
      .every(
        (frame) =>
          frame.state === 'sparse' &&
          frame.intensity === timeline.config.stateIntensity.sparse,
      ),
  );
  assert.equal(timeline.frames.at(-1)?.state, 'resolve');
});

test('a short replay keeps an opening hold instead of snapping its peak request to tick zero', () => {
  const ticks = Array.from({ length: 35 }, (_, tick) =>
    v1Tick(tick, {
      events: tick === 34 ? [{ type: 'Destroyed', slot: 1 }] : [],
      health: tick === 34 ? [3, 0] : [3, 3],
      statuses:
        tick === 34 ? ['Active', 'Destroyed'] : ['Active', 'Active'],
    }),
  );
  const timeline = buildAdaptiveTimeline(
    v1Replay(ticks, {
      header: { maxTicks: 35 },
      result: v1Result(ticks, 34, 'Elimination', 0),
    }),
  );

  assert.equal(timeline.highlights.length, 1);
  assert.equal(timeline.highlights[0]?.requestTick, null);
  assert.ok(
    timeline.frames
      .slice(0, -1)
      .every((frame) => frame.state === 'sparse'),
  );
  assert.equal(timeline.frames.at(-1)?.state, 'resolve');
});

test('retrospective highlights are relatively ranked, capped, and separated', () => {
  const ticks = Array.from({ length: 150 }, (_, tick) => {
    const events: ReplayV1GameEvent[] =
      tick === 10
        ? [{ type: 'Shot', slot: 0 }]
        : tick === 75
          ? [
              {
                type: 'Damage',
                slot: 0,
                targetSlot: 1,
                amount: 1,
                newHealth: 2,
              },
            ]
          : tick === 149
            ? [{ type: 'Destroyed', slot: 1 }]
            : [];
    return v1Tick(tick, {
      events,
      health: tick >= 149
        ? [3, 0]
        : tick >= 75
          ? [3, 2]
          : [3, 3],
      statuses:
        tick === 149 ? ['Active', 'Destroyed'] : ['Active', 'Active'],
    });
  });
  const timeline = buildAdaptiveTimeline(
    v1Replay(ticks, {
      header: { maxTicks: 150 },
      result: v1Result(ticks, 149, 'Elimination', 0),
    }),
  );

  assert.equal(timeline.mode, 'retrospective');
  assert.equal(timeline.highlights.length, 2);
  assert.deepEqual(
    timeline.highlights.map((highlight) => ({
      peakTick: highlight.peakTick,
      kind: highlight.kind,
      rank: highlight.relativeRank,
      primary: highlight.primary,
    })),
    [
      { peakTick: 75, kind: 'damage', rank: 1, primary: false },
      { peakTick: 149, kind: 'decisive', rank: 0, primary: true },
    ],
  );
  assert.ok(
    Math.abs(
      timeline.highlights[1]!.peakTick -
        timeline.highlights[0]!.peakTick,
    ) >= 6 * timeline.planningBarTicks!,
  );
  assert.deepEqual(
    timeline.frames
      .filter(
        (frame, index) =>
          index > 0 &&
          frame.state !== 'resolve' &&
          frame.state !== timeline.frames[index - 1]?.state,
      )
      .map((frame) => [frame.tick, frame.state]),
    [[110, 'climax']],
  );
});

test('every v2 prefix is byte-equivalent to the same full causal prefix', () => {
  const complete = structuredClone(FRONTLINE_COMPLETE_WIRE);
  const full = buildAdaptiveTimeline(loadReplayObject(complete).replay, {
    planningMode: 'causal',
  });

  for (let count = 0; count <= complete.ticks.length; count += 1) {
    const prefixWire: ReplayV2PartialDocument = {
      header: structuredClone(complete.header),
      ticks: structuredClone(complete.ticks.slice(0, count)),
      result: null,
      replayHash: null,
      partial: true,
    };
    const prefix = buildAdaptiveTimeline(loadReplayObject(prefixWire).replay);
    assert.deepEqual(
      prefix.frames,
      full.frames.slice(0, count),
      `v2 prefix length ${count}`,
    );
  }
});

test('approach pairs stable v2 actor lives and ignores a respawn position jump', () => {
  const replay = loadReplayObject(
    structuredClone(FRONTLINE_COMPLETE_WIRE),
  ).replay;
  replay.ticks = replay.ticks.slice(2, 4);
  replay.result = null;

  const rows = [
    [[0, 0], [0, 10], [20, 0], [20, 10]],
    [[0, 0], [0, 10], [20, 0], [1, 10]],
  ] as const;
  for (let tickIndex = 0; tickIndex < replay.ticks.length; tickIndex += 1) {
    const actors = replay.ticks[tickIndex]!.after.actors;
    for (let actorIndex = 0; actorIndex < actors.length; actorIndex += 1) {
      actors[actorIndex]!.position = {
        x: rows[tickIndex]![actorIndex]![0],
        y: rows[tickIndex]![actorIndex]![1],
      };
    }
  }
  const respawn = replay.ticks[1]!.after.actors.find(
    (actor) => actor.identity.teamId === 1 && actor.identity.unitId === 1,
  )!;
  assert.equal(respawn.identity.kind, 'frontline');
  if (respawn.identity.kind !== 'frontline') {
    throw new Error('expected a Frontline actor');
  }
  respawn.identity = {
    ...respawn.identity,
    lifeId: 1,
    actorKey: 'frontline:1:unit:1:life:1',
  };
  respawn.actorKey = respawn.identity.actorKey;

  const timeline = buildAdaptiveTimeline(replay);
  assert.ok((timeline.frames[1]?.features.proximity ?? 0) > 0.8);
  assert.equal(timeline.frames[1]?.features.approach, 0);
  assert.equal(timeline.frames[1]?.features.closingPressure, 0);
});

test('v2 approach sees a closing engagement when the nearest pair opens', () => {
  const replay = loadReplayObject(
    structuredClone(FRONTLINE_COMPLETE_WIRE),
  ).replay;
  replay.ticks = replay.ticks.slice(2, 7);
  replay.result = null;
  const tracks = new Map([
    ['0:0', [[1, 4], [1, 4], [1, 4], [1, 4], [1, 4]]],
    ['1:0', [[4, 4], [6, 4], [8, 4], [10, 4], [12, 4]]],
    ['0:1', [[2, 6], [3, 6], [4, 6], [5, 6], [6, 6]]],
    ['1:1', [[12, 6], [11, 6], [10, 6], [9, 6], [8, 6]]],
  ]);
  for (let tickIndex = 0; tickIndex < replay.ticks.length; tickIndex += 1) {
    for (const actor of replay.ticks[tickIndex]!.after.actors) {
      const position = tracks.get(
        `${actor.identity.teamId}:${actor.identity.unitId}`,
      )![tickIndex]!;
      actor.position = { x: position[0], y: position[1] };
    }
  }

  const full = buildAdaptiveTimeline(replay);
  assert.equal(full.frames[1]?.features.approach, 0);
  assert.ok((full.frames[2]?.features.approach ?? 0) > 0);
  assert.ok(
    (full.frames[3]?.features.closingPressure ?? 0) >
      (full.frames[2]?.features.closingPressure ?? 1),
  );
  for (let count = 0; count <= replay.ticks.length; count += 1) {
    const prefix = buildAdaptiveTimeline({
      ...replay,
      ticks: replay.ticks.slice(0, count),
      result: null,
    });
    assert.deepEqual(
      prefix.frames,
      full.frames.slice(0, count),
      `v2 multi-unit approach prefix length ${count}`,
    );
  }
});

test('a complete result is ignored until its authoritative end tick', () => {
  const ticks = Array.from({ length: 5 }, (_, index) => v1Tick(index));
  const result = v1Result(ticks, 4, 'MaxTicks');
  const complete = buildAdaptiveTimeline(v1Replay(ticks, { result }), {
    planningMode: 'causal',
  });
  const withheld = buildAdaptiveTimeline(v1Replay(ticks));

  assert.deepEqual(complete.frames.slice(0, 4), withheld.frames.slice(0, 4));
  assert.ok(
    complete.frames.slice(0, 4).every((frame) => frame.state !== 'resolve'),
  );
  assert.equal(complete.frames[4]?.state, 'resolve');
  assert.ok(complete.frames[4]?.triggers.includes('resolve'));
  assert.notEqual(withheld.frames[4]?.state, 'resolve');
});

test('fractional samples never interpolate toward an unrevealed damage tick', () => {
  const timeline = buildAdaptiveTimeline(
    v1Replay([
      v1Tick(0),
      v1Tick(1, {
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
    ]),
  );

  const before = sampleAdaptiveTimeline(timeline, 0.999);
  const impact = sampleAdaptiveTimeline(timeline, 1);
  assert.equal(before.sourceTick, 0);
  assert.equal(before.state, 'sparse');
  assert.ok(!before.triggers.includes('damage'));
  assert.equal(impact.sourceTick, 1);
  assert.equal(impact.state, 'sparse');
  assert.notEqual(impact.state, 'combat');
  assert.ok(impact.intensity >= 0.84);
  assert.ok(impact.triggers.includes('damage'));
  assert.equal(impact.trend, 'rising');
});

test('v1 destruction resolves immediately and releases toward its causal target', () => {
  const timeline = buildAdaptiveTimeline(
    v1Replay([
      v1Tick(0, {
        events: [{ type: 'Destroyed', slot: 1 }],
        statuses: ['Active', 'Destroyed'],
        health: [3, 0],
      }),
    ]),
  );

  const boundary = sampleAdaptiveTimeline(timeline, 0);
  const release = sampleAdaptiveTimeline(timeline, 0.75);
  assert.equal(boundary.state, 'resolve');
  assert.equal(boundary.intensity, 1);
  assert.equal(boundary.trend, 'falling');
  assert.deepEqual(boundary.triggers, ['destruction', 'resolve']);
  assert.ok(release.intensity < boundary.intensity);
  assert.ok(release.intensity > boundary.targetIntensity);
});

test('v2 destruction is an accent, not a terminal signal', () => {
  const replay = v2PartialModel();
  const tick = replay.ticks[0]!;
  tick.events.push(causalEvent(tick, 'destroyed'));

  const frame = buildAdaptiveTimeline(replay).frames[0]!;

  assert.notEqual(frame.state, 'resolve');
  assert.ok(frame.triggers.includes('destruction'));
  assert.ok(!frame.triggers.includes('resolve'));
  assert.equal(frame.intensity, 1);
});

test('a shot and its newly spawned projectile count as one launch', () => {
  const shot = { type: 'Shot', slot: 0 } as const;
  const bare = buildAdaptiveTimeline(
    v1Replay([v1Tick(0, { events: [shot] })]),
  ).frames[0]!;
  const launched = buildAdaptiveTimeline(
    v1Replay([
      v1Tick(0, {
        events: [shot],
        projectiles: [
          { x: 1, y: 0, direction: 'East', ownerSlot: 0 },
        ],
        projectileTraversals: [
          {
            id: 1,
            ownerSlot: 0,
            direction: 'East',
            fromX: 0,
            fromY: 0,
            path: [[1, 0]],
          },
        ],
      }),
    ]),
  ).frames[0]!;

  assert.equal(bare.features.combatPressure, 0.34);
  assert.equal(launched.features.projectileThreat, 0.5);
  assert.equal(launched.features.combatPressure, bare.features.combatPressure);
  assert.equal(launched.state, 'sparse');
  assert.ok(launched.intensity >= 0.62);
});

test('Frontline base-breach events and authoritative winners are terminal', () => {
  const breached = v2PartialModel();
  const breachTick = breached.ticks[0]!;
  breachTick.events.push(causalEvent(breachTick, 'base-breached'));

  const winner = v2PartialModel();
  const winnerObjective = winner.ticks[0]!.after.objective;
  assert.equal(winnerObjective.kind, 'frontline');
  if (winnerObjective.kind !== 'frontline') {
    throw new Error('expected a Frontline objective fixture');
  }
  winnerObjective.winnerTeamId = 0;

  for (const replay of [breached, winner]) {
    const frame = buildAdaptiveTimeline(replay).frames[0]!;
    assert.equal(frame.state, 'resolve');
    assert.ok(frame.triggers.includes('resolve'));
  }
});

test('closing contact and projectile pressure produce stable pursuit then combat phrases', () => {
  const ticks = [v1Tick(0, { positions: [[0, 0], [9, 0]] })];
  for (let tick = 1; tick <= 16; tick += 1) {
    const closing = Math.min(tick, 4);
    ticks.push(
      v1Tick(tick, {
        positions: [[closing, 0], [9 - closing, 0]],
        visible: true,
        events:
          tick <= 5
            ? [
                {
                  type: 'Move',
                  slot: 0,
                  fromX: closing - 1,
                  fromY: 0,
                  toX: closing,
                  toY: 0,
                },
              ]
            : [],
        actions: tick <= 5 ? ['MoveForward', 'Wait'] : ['Wait', 'Wait'],
        projectiles:
          tick >= 6
            ? [
                {
                  x: 4,
                  y: 0,
                  direction: 'East',
                  ownerSlot: 0,
                },
              ]
            : undefined,
      }),
    );
  }
  const timeline = buildAdaptiveTimeline(v1Replay(ticks));

  assert.equal(timeline.frames[1]?.state, 'sparse');
  assert.ok(timeline.frames[1]?.triggers.includes('contact'));
  assert.equal(timeline.frames[2]?.state, 'tension');
  assert.equal(timeline.frames[6]?.state, 'pursuit');
  assert.ok((timeline.frames[6]?.features.pursuitPressure ?? 0) > 0.5);
  assert.ok(
    timeline.frames
      .slice(6, 16)
      .every((frame) => frame.state === 'pursuit'),
  );
  assert.ok((timeline.frames[6]?.intensity ?? 0) >= 0.58);
  assert.equal(timeline.frames[16]?.state, 'combat');
  assert.ok((timeline.frames[16]?.features.combatPressure ?? 0) > 0.56);
});

test('a shot reacts vertically while sustained shots earn a minimum combat phrase', () => {
  const timeline = buildAdaptiveTimeline(
    v1Replay([
      v1Tick(0, { events: [{ type: 'Shot', slot: 0 }] }),
      v1Tick(1),
      v1Tick(2),
      v1Tick(3),
      v1Tick(4, { events: [{ type: 'Shot', slot: 0 }] }),
      v1Tick(5, { events: [{ type: 'Shot', slot: 0 }] }),
      v1Tick(6, { events: [{ type: 'Shot', slot: 0 }] }),
      v1Tick(7, { events: [{ type: 'Shot', slot: 0 }] }),
      v1Tick(8, { events: [{ type: 'Shot', slot: 0 }] }),
      v1Tick(9),
      v1Tick(10),
      v1Tick(11),
      v1Tick(12),
      v1Tick(13),
    ]),
    {
      combatPressureDecay: 0.7,
      releaseTicks: 2,
      stateEntryTicks: { pursuit: 1, combat: 1 },
      minDwellTicks: { tension: 0, pursuit: 0, combat: 4 },
    },
  );

  assert.equal(timeline.frames[0]?.state, 'sparse');
  assert.ok((timeline.frames[0]?.intensity ?? 0) >= 0.62);
  assert.ok(timeline.frames[0]?.triggers.includes('shot'));
  assert.ok(
    timeline.frames.slice(0, 6).every((frame) => frame.state === 'sparse'),
  );
  assert.equal(timeline.frames[6]?.state, 'tension');
  assert.equal(timeline.frames[7]?.state, 'pursuit');
  assert.ok(
    timeline.frames.slice(8, 13).every((frame) => frame.state === 'combat'),
  );
  assert.equal(timeline.frames[13]?.state, 'sparse');
});

test('a generation-3 match is scored exactly like the same fight spelled v1', () => {
  // Same fight, other vocabulary: replay-v1/v2 write `shot`/`destroyed`/`move`/`turn`,
  // replay-v3 writes `attack`/`destruction`/`movement`/`rotation`, and the model keeps
  // each document's own words deliberately. The director compared against one spelling,
  // so a generation-3 match scored as an empty field — no shots, no deaths, nobody
  // moving — and the timeline never left `sparse`.
  const ticks = [
    v1Tick(0, { positions: [[0, 0], [6, 0]] }),
    v1Tick(1, {
      positions: [[1, 0], [6, 0]],
      visible: true,
      actions: ['MoveForward', 'Wait'],
      events: [{ type: 'Move', slot: 0, fromX: 0, fromY: 0, toX: 1, toY: 0 }],
    }),
    v1Tick(2, {
      positions: [[2, 0], [6, 0]],
      visible: true,
      actions: ['MoveForward', 'TurnLeft'],
      events: [
        { type: 'Move', slot: 0, fromX: 1, fromY: 0, toX: 2, toY: 0 },
        { type: 'Turn', slot: 1, fromFacing: 'West', toFacing: 'North' },
      ],
    }),
    v1Tick(3, {
      positions: [[2, 0], [6, 0]],
      visible: true,
      actions: ['Shoot', 'Wait'],
      events: [{ type: 'Shot', slot: 0 }],
    }),
    v1Tick(4, {
      positions: [[2, 0], [6, 0]],
      visible: true,
      health: [3, 1],
      events: [
        { type: 'Damage', slot: 0, targetSlot: 1, amount: 2, newHealth: 1 },
      ],
    }),
    v1Tick(5, {
      positions: [[2, 0], [6, 0]],
      visible: true,
      health: [3, 0],
      statuses: ['Active', 'Destroyed'],
      events: [{ type: 'Destroyed', slot: 1 }],
    }),
  ];
  const duel = v1Replay(ticks);
  const generation3 = structuredClone(duel) as ReplayModel;
  const v3Spelling: Record<string, string> = {
    shot: 'attack',
    destroyed: 'destruction',
    move: 'movement',
    turn: 'rotation',
    disqualified: 'participant-disqualified',
  };
  let renamed = 0;
  for (const tick of generation3.ticks) {
    for (const event of [...tick.events, ...tick.lifecycleEvents]) {
      const generic = v3Spelling[event.type];
      if (generic === undefined) continue;
      event.type = generic;
      renamed += 1;
    }
  }
  assert.ok(renamed >= 5, `expected renamed v3 events, got ${renamed}`);

  const scored = buildAdaptiveTimeline(generation3);
  assert.deepEqual(scored.frames, buildAdaptiveTimeline(duel).frames);
  assert.ok(scored.frames.some((frame) => frame.triggers.includes('shot')));
  assert.ok(
    scored.frames.some((frame) => frame.triggers.includes('destruction')),
  );
  // Tick 3 is the shot alone: nobody moved and nothing was hit, so only the attack
  // event can lift activity past what mere visible contact is worth.
  const shotFrame = scored.frames.find((frame) => frame.tick === 3);
  assert.ok((shotFrame?.features.activity ?? 0) >= 0.84);
});

test('long stationary quiet runs expose stall, thin sparse, and never invent climax', () => {
  const timeline = buildAdaptiveTimeline(
    v1Replay(Array.from({ length: 36 }, (_, index) => v1Tick(index))),
  );
  const final = timeline.frames.at(-1)!;

  assert.equal(final.state, 'sparse');
  assert.ok(final.features.quietTicks >= 35);
  assert.ok(final.features.stationaryTicks >= 35);
  assert.ok(final.features.stall > 0.95);
  assert.ok(final.targetIntensity < timeline.config.stateIntensity.sparse);
  assert.equal(final.trend, 'falling');
  assert.ok(timeline.frames.every((frame) => frame.state !== 'climax'));
});

test('low health and proximity alone never sustain a climax phrase', () => {
  const timeline = buildAdaptiveTimeline(
    v1Replay(
      Array.from({ length: 24 }, (_, tick) =>
        v1Tick(tick, {
          positions: [[0, 0], [3, 0]],
          health: [1, 3],
          visible: false,
        }),
      ),
    ),
  );

  assert.ok(timeline.frames.every((frame) => frame.state !== 'climax'));
  assert.ok(
    timeline.frames.every((frame) => frame.features.healthUrgency === 1),
  );
  assert.ok(
    timeline.frames.every((frame) => frame.features.acuteThreat < 0.5),
  );
});

test('critical health with recurring low-grade fire stays combat, not climax', () => {
  const highThreatProjectiles: ReplayV1Projectile[] = Array.from(
    { length: 3 },
    (_, index) => ({
      x: index,
      y: 0,
      direction: 'East',
      ownerSlot: 0,
    }),
  );
  const routineProjectiles = highThreatProjectiles.slice(0, 2);
  const burstEvents = new Map<number, ReplayV1GameEvent[]>([
    [6, [{ type: 'Shot', slot: 0 }]],
    [
      7,
      [
        { type: 'Shot', slot: 0 },
        {
          type: 'Damage',
          slot: 0,
          targetSlot: 1,
          amount: 1,
          newHealth: 2,
        },
      ],
    ],
    [
      8,
      [
        {
          type: 'Damage',
          slot: 0,
          targetSlot: 1,
          amount: 1,
          newHealth: 1,
        },
      ],
    ],
    [9, [{ type: 'Shot', slot: 0 }]],
    [
      10,
      [
        {
          type: 'Damage',
          slot: 0,
          targetSlot: 1,
          amount: 1,
          newHealth: 1,
        },
      ],
    ],
    [11, [{ type: 'Shot', slot: 0 }]],
  ]);
  const timeline = buildAdaptiveTimeline(
    v1Replay(
      Array.from({ length: 42 }, (_, tick) => {
        const routineShot = tick >= 16 && (tick - 16) % 5 === 0;
        return v1Tick(tick, {
          positions: [[0, 0], [2, 0]],
          health: tick >= 8 ? [3, 1] : [3, 3],
          visible: tick >= 6,
          events:
            burstEvents.get(tick) ??
            (routineShot ? [{ type: 'Shot', slot: 0 }] : []),
          projectiles:
            tick >= 6 && tick <= 11
              ? highThreatProjectiles
              : routineShot
                ? routineProjectiles
                : undefined,
        });
      }),
    ),
  );

  assert.ok(timeline.frames.some((frame) => frame.state === 'combat'));
  assert.ok(timeline.frames.every((frame) => frame.state !== 'climax'));
  assert.equal(timeline.frames.at(-1)?.state, 'combat');
});

test('sustained critical combat and a fast objective ETA can earn climax', () => {
  const healthTimeline = buildAdaptiveTimeline(
    v1Replay(
      Array.from({ length: 12 }, (_, tick) =>
        v1Tick(tick, {
          positions: [[tick, 0], [20 - tick, 0]],
          health: [1, 3],
          visible: true,
          events:
            tick === 11
              ? [
                  { type: 'Shot', slot: 0 },
                  {
                    type: 'Damage',
                    slot: 0,
                    targetSlot: 1,
                    amount: 1,
                    newHealth: 2,
                  },
                ]
              : [{ type: 'Shot', slot: 0 }],
        }),
      ),
    ),
    {
      stateEntryTicks: {
        tension: 1,
        pursuit: 1,
        combat: 1,
        climax: 1,
      },
      minDwellTicks: {
        sparse: 0,
        tension: 0,
        pursuit: 0,
        combat: 0,
        climax: 0,
      },
    },
  );
  assert.equal(healthTimeline.frames.at(-1)?.state, 'climax');
  assert.ok(
    (healthTimeline.frames.at(-1)?.features.combatPressure ?? 0) > 0.78,
  );

  const controlTimeline = buildAdaptiveTimeline(
    v1Replay(
      [
        v1Tick(4, { controlPressure: 2 }),
        v1Tick(5, { controlPressure: 8 }),
        v1Tick(6, { controlPressure: 8 }),
        v1Tick(7, { controlPressure: 8 }),
        v1Tick(8, { controlPressure: 8 }),
      ],
      {
        header: {
          zoneTiles: [[0, 0]],
          controlPressureLimit: 20,
          controlOvertimeStartTick: 5,
          controlOvertimePressureLimit: 10,
        },
      },
    ),
    {
      stateEntryTicks: {
        tension: 1,
        pursuit: 1,
        combat: 1,
        climax: 1,
      },
      minDwellTicks: {
        sparse: 0,
        tension: 0,
        pursuit: 0,
        combat: 0,
        climax: 0,
      },
    },
  );
  const overtime = controlTimeline.frames[1]!;
  assert.equal(overtime.state, 'tension');
  assert.equal(controlTimeline.frames.at(-1)?.state, 'climax');
  assert.equal(overtime.features.overtime, true);
  assert.equal(overtime.features.controlUrgency, 0.8);
  assert.ok(overtime.features.objectiveImminence > 0);
  assert.ok(overtime.triggers.includes('overtime'));
});

test('high but slowly advancing control pressure remains below climax', () => {
  let pressure = 79;
  const timeline = buildAdaptiveTimeline(
    v1Replay(
      Array.from({ length: 60 }, (_, tick) => {
        if (tick % 5 === 0) pressure += 1;
        return v1Tick(tick, { controlPressure: pressure });
      }),
      {
        header: {
          maxTicks: 500,
          zoneTiles: [[0, 0]],
          controlPressureLimit: 100,
        },
      },
    ),
  );

  assert.ok(
    timeline.frames.some((frame) => frame.features.controlUrgency >= 0.8),
  );
  assert.ok(
    timeline.frames
      .filter((frame) => frame.features.controlUrgency < 1)
      .every((frame) => frame.state !== 'climax'),
  );
  assert.ok(
    (timeline.frames.at(-1)?.features.objectiveImminence ?? 1) < 0.1,
  );
});

test('normal fractional sampling relaxes only toward the current frame target', () => {
  const timeline = buildAdaptiveTimeline(
    v1Replay([
      v1Tick(0, {
        positions: [[0, 0], [4, 0]],
        visible: true,
      }),
      v1Tick(1, {
        positions: [[0, 0], [4, 0]],
        visible: true,
      }),
    ]),
  );
  const boundary = sampleAdaptiveTimeline(timeline, 1);
  const halfway = sampleAdaptiveTimeline(timeline, 1.5);

  assert.equal(timeline.frames[0]?.state, 'sparse');
  assert.equal(boundary.state, 'tension');
  assert.equal(halfway.sourceTick, 1);
  assert.ok(
    Math.abs(halfway.intensity - boundary.targetIntensity) <
      Math.abs(boundary.intensity - boundary.targetIntensity),
  );
});

function v1Replay(
  ticks: ReplayV1Tick[],
  options: V1ReplayOptions = {},
): ReplayModel {
  const partial = replayV1LivePartialFixtureInput();
  partial.header = {
    ...partial.header,
    engineVersion: 'test',
    gameRulesVersion: '0.5',
    mapId: 'soundtrack-test-map',
    mapVersion: 1,
    mapWidth: 24,
    mapHeight: 24,
    mapTiles: Array.from({ length: 24 }, () => '.'.repeat(24)),
    seed: 1,
    maxTicks: 100,
    maxHealth: 3,
    visionRange: 6,
    participants: structuredClone(V1_PARTICIPANTS),
    ...options.header,
  };
  if (
    options.header !== undefined &&
    Object.hasOwn(options.header, 'maxHealth') &&
    options.header.maxHealth === undefined
  ) {
    delete partial.header.maxHealth;
  }
  partial.ticks = ticks;
  if (options.result === undefined) {
    return loadReplayObject(partial).replay;
  }

  const complete: ReplayV1CompleteDocument = {
    header: partial.header,
    ticks,
    result: options.result,
    replayHash: '1'.repeat(64),
  };
  return loadReplayObject(complete).replay;
}

function v1Tick(tick: number, options: V1TickOptions = {}): ReplayV1Tick {
  const positions = options.positions ?? [[0, 0], [8, 8]];
  const health = options.health ?? [3, 3];
  const statuses = options.statuses ?? ['Active', 'Active'];
  const actions = options.actions ?? ['Wait', 'Wait'];
  return {
    tick,
    bots: V1_SLOTS.map((slot) => ({
      slot,
      chosenAction: actions[slot],
      validatedAction: actions[slot],
      result: 'Success',
      faulted: false,
      visibleTiles: [],
      visibleEnemies: options.visible
        ? [
            {
              slot: 1 - slot,
              x: positions[1 - slot][0],
              y: positions[1 - slot][1],
              facing: slot === 0 ? 'West' : 'East',
              health: health[1 - slot],
            },
          ]
        : [],
    })),
    events: options.events ?? [],
    state: V1_SLOTS.map((slot) => ({
      slot,
      x: positions[slot][0],
      y: positions[slot][1],
      facing: slot === 0 ? 'East' : 'West',
      health: health[slot],
      cooldown: 0,
      status: statuses[slot],
      ...(options.zoneTicks === undefined
        ? {}
        : { zoneTicks: options.zoneTicks[slot] }),
    })),
    ...(options.controlPressure === undefined
      ? {}
      : { controlPressure: options.controlPressure }),
    ...(options.projectiles === undefined
      ? {}
      : { projectiles: options.projectiles }),
    ...(options.projectileTraversals === undefined
      ? {}
      : { projectileTraversals: options.projectileTraversals }),
  };
}

function v1Result(
  ticks: ReplayV1Tick[],
  endTick: number,
  reason: ReplayV1MatchResult['reason'],
  winnerSlot?: number,
): ReplayV1MatchResult {
  const terminal = ticks.find((tick) => tick.tick === endTick) ?? ticks.at(-1);
  return {
    ...(winnerSlot === undefined ? {} : { winnerSlot }),
    reason,
    endTick,
    bots: V1_SLOTS.map((slot) => {
      const state = terminal?.state.find((candidate) => candidate.slot === slot);
      return {
        slot,
        outcome:
          winnerSlot === undefined
            ? ('Draw' as const)
            : winnerSlot === slot
              ? ('Win' as const)
              : ('Loss' as const),
        finalHealth: state?.health ?? 3,
        damageDealt: 0,
        faults: 0,
        finalStatus: state?.status ?? 'Active',
      };
    }),
  };
}

function v2PartialModel(): ReplayModel {
  const complete = replayV2FixtureInput();
  const partial: ReplayV2PartialDocument = {
    header: structuredClone(complete.header),
    ticks: structuredClone(complete.ticks),
    result: null,
    replayHash: null,
    partial: true,
  };
  return loadReplayObject(partial).replay;
}

function causalEvent(tick: ReplayTick, type: string): ReplayCausalEvent {
  return {
    eventId: `test:${tick.tick}:0`,
    tick: tick.tick,
    ordinal: 0,
    type,
    teamId: 0,
    unitId: 0,
    sourceActor: tick.after.actors[0]?.identity ?? null,
    targetActor: tick.after.actors[1]?.identity ?? null,
    projectileId: null,
    from: null,
    to: null,
    fromFacing: null,
    toFacing: null,
    projectileHeading: null,
    fromFormId: null,
    toFormId: null,
    formTransitionStartedAtTick: null,
    formTransitionCompletesAtTick: null,
    actionPayload: null,
    actionId: null,
    actionCode: null,
    actionResult: null,
    amount: null,
    newHealth: null,
    lifecycleStatus: type === 'destroyed' ? 'destroyed' : null,
    spawnReason: null,
    respawnAtTick: null,
    unlockAtTick: null,
    rebuildReadyAtTick: null,
    fabricationAtTick: null,
    fromPositionIndex: null,
    toPositionIndex: null,
    claimingTeamId: null,
    captureProgress: null,
    controlResumesAtTick: null,
    completeness: 'exact',
  };
}

function stateRank(state: string): number {
  return ['sparse', 'tension', 'pursuit', 'combat', 'climax', 'resolve']
    .indexOf(state);
}
